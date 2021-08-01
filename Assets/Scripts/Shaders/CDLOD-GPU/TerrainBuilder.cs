using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/*
----------------------------------------------------------------
地面尺寸大小
(10240, 2048, 10240)
QuadTree有六层，精度从高到低分别是lod0-lod5
每一个基础mesh有512个三角形面，大小为8m*8m,一个网格分辨率0.5m
lod0 -> 0.5m; lod1 -> 1m; lod2 -> 2m; lod3 -> 4m; lod4 -> 8m; lod5 -> 16m
总共的node总数是5*5+10*10+20*20+40*40+80*80+160*160=34125
----------------------------------------------------------------
*/
public class TerrainBase
{
    private Vector3 worldSize;
    // MAX_NODE_ID = 5 * 5 + 10 * 10 + 20 * 20 + 40 * 40 + 80 * 80 + 160 * 160
    public const uint MAX_NODE_ID = 34125;
    public const int MAX_LOD_DEPTH = 5;
    private RenderTexture[] minMaxHeightMaps;
    private RenderTexture[] quadTreeMaps;
    private static Mesh _plane;
    public static Mesh plane
    {
        get
        {
            if (_plane == null){
                _plane = Ocean.GenerateMesh(16, 8);
            }
            return _plane;
        }
    }
    private RenderTexture _minMaxHeightMap;
    public RenderTexture minMaxHeightMap
    {
        get
        {
            if (_minMaxHeightMap == null)
                _minMaxHeightMap = Ocean.CreateRT(minMaxHeightMaps, RenderTextureFormat.ARGB32);
            return _minMaxHeightMap;
        }
    }
    private RenderTexture _quadTreeMap;
    public RenderTexture quadTreeMap
    {
        get
        {
            if (_quadTreeMap == null)
                _quadTreeMap = Ocean.CreateRT(quadTreeMaps, RenderTextureFormat.R16);
            return _quadTreeMap;
        }
    }
    public TerrainBase(RenderTexture[] _minMaxHeightMaps, RenderTexture[] _quadTreeMaps)
    {
        minMaxHeightMaps = _minMaxHeightMaps;
        quadTreeMaps = _quadTreeMaps;
    }
}

public class TerrainBuilder : IDisposable
{
    public TerrainBase tb;
    private static ComputeShader _quadTreeCS;
    private static ComputeShader quadTreeCS
    {
        get
        {
            if (_quadTreeCS == null)
                _quadTreeCS = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Scripts/Shaders/CDLOD-GPU/LoadQuadTree.compute");
            return _quadTreeCS;
        }
    }
    #region "Compute shader kernel"
    private int kernelTraverseQuadTree;
    private int kernelCreateLodMap;
    private int kernelCreatePatches;
    private int kernelCreateQuadTree;
    #endregion
    #region "Compute Buffer"
    // 具体参考https://docs.unity.cn/cn/2021.1/ScriptReference/ComputeBuffer.html
    private ComputeBuffer consumeNodeList;
    private ComputeBuffer appendNodeList;
    private ComputeBuffer appendFinalNodeList;
    private ComputeBuffer nodeInfos;
    private ComputeBuffer finalNodeList;
    // 传入shader中
    private ComputeBuffer _culledPatchList;
    private ComputeBuffer culledPatchList { get => _culledPatchList; }
    // 从gpu中拷贝到cpu中，用于之后创建patch
    // indirectArgs 拷贝自finalNodeListBuffer, patchIndirectArgs 拷贝自culledPatchList
    private ComputeBuffer _indirectArgs;
    public ComputeBuffer indirectArgs { get => _indirectArgs; }
    private ComputeBuffer _patchIndirectArgs;
    public ComputeBuffer patchIndirectArgs { get => _patchIndirectArgs; }
    #endregion
    private CommandBuffer commandBuffer;
    private Plane[] cameraFrustumPlane;
    private Vector4[] cameraFrustumPlanesV4;
    private List<RenderTexture> quadMaps;
    /// <summary>
    /// 分配buffer的大小
    /// </summary>
    private int maxBufferSize;
    int maxLodSize;
    int lodCount;
    public TerrainBuilder(RenderTexture[] _minMaxHeightMaps, int _maxBufferSize = 200, int _maxLodSize = 5, int _lodCount = 6)
    {
        maxLodSize = _maxLodSize;
        lodCount = _lodCount;
        quadMaps = new List<RenderTexture>();
        commandBuffer = new CommandBuffer();
        commandBuffer.name = "InfinityTerrain";
        cameraFrustumPlane = new Plane[6];
        cameraFrustumPlanesV4 = new Vector4[6];
        maxBufferSize = _maxBufferSize;
        // 构造函数第一个参数接收输出buffer的长度，第二个参数代表每个元素的长度，第三个参数表示compute buffer的类型
        consumeNodeList = new ComputeBuffer(TerrainBase.MAX_LOD_DEPTH * TerrainBase.MAX_LOD_DEPTH, 8, ComputeBufferType.Append);
        appendNodeList = new ComputeBuffer(50, 8, ComputeBufferType.Append);
        appendFinalNodeList = new ComputeBuffer(maxBufferSize, 12, ComputeBufferType.Append);
        nodeInfos = new ComputeBuffer((int)(TerrainBase.MAX_NODE_ID + 1), 4);
        finalNodeList = new ComputeBuffer(maxBufferSize, 12, ComputeBufferType.Append);
        _culledPatchList = new ComputeBuffer(maxBufferSize * 32, 20, ComputeBufferType.Append);
        // TODO:为两个拷贝到缓冲区的buffer设置初始值的目的？？
        _indirectArgs = new ComputeBuffer(3, 4, ComputeBufferType.IndirectArguments);
        _indirectArgs.SetData(new uint[]{1, 1, 1});
        _patchIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        _patchIndirectArgs.SetData(new uint[]{TerrainBase.plane.GetIndexCount(0), 0, 0, 0, 0});

        InitKernel();
        tb = new TerrainBase(_minMaxHeightMaps, quadMaps.ToArray());
    }

    public void Dispose()
    {
        consumeNodeList.Dispose();
        appendNodeList.Dispose();
        appendFinalNodeList.Dispose();
        nodeInfos.Dispose();
        finalNodeList.Dispose();
        _culledPatchList.Dispose();
        _indirectArgs.Dispose();
        _patchIndirectArgs.Dispose();
    }
    void InitKernel()
    {
        kernelCreateQuadTree = quadTreeCS.FindKernel("CreateQuadTree");
        kernelTraverseQuadTree = quadTreeCS.FindKernel("TraverseQuadTree");
        kernelCreateLodMap = quadTreeCS.FindKernel("CreateLodMap");
        kernelCreatePatches = quadTreeCS.FindKernel("CreatePatches");
        BindShader(kernelCreateQuadTree);
        BindShader(kernelTraverseQuadTree);
        BindShader(kernelCreatePatches);
        BindShader(kernelCreateLodMap);
    }
    // TODO:自CPU中向GPU中传值
    void BindShader(int index)
    {
        if (index == kernelTraverseQuadTree)
        {
        }
        else if (index == kernelCreatePatches)
        {

        }
        else if (index == kernelCreateLodMap)
        {

        }
        else if (index == kernelCreateQuadTree)
        {
            CreateQuadMipMaps(this.lodCount - 1, 0);
            quadMaps.Reverse();
        }
    }
    void CreateQuadMipMaps(int mipLevel, int nodeOffset)
    {
        int mipSize = Convert.ToInt32(maxLodSize * Mathf.Pow(2, lodCount - 1 - mipLevel));
        var desc = new RenderTextureDescriptor(mipSize, mipSize, RenderTextureFormat.R16, 0, 1);
        desc.autoGenerateMips = false;
        desc.enableRandomWrite = true;
        RenderTexture rt = new RenderTexture(desc);
        rt.Create();
        CalculateQuadMipMaps(rt, mipLevel, nodeOffset);
        quadMaps.Add(rt);
        if (mipLevel > 0)
            CreateQuadMipMaps(mipLevel - 1, nodeOffset + mipSize * mipSize);
    }
    void CalculateQuadMipMaps(RenderTexture rt, int mipLevel, int nodeOffset)
    {
        quadTreeCS.SetTexture(kernelCreateQuadTree, "quadTreeRT", rt);
        quadTreeCS.SetInt("mipSize", rt.width);
        quadTreeCS.SetInt("maxLodOffset", nodeOffset);
        int group = Convert.ToInt32(Mathf.Pow(2, lodCount - mipLevel - 1));
        quadTreeCS.Dispatch(kernelCreateQuadTree, group, group, 1);
    }
    // TODO:分割四叉树生成patch
    void Dispatch()
    {

    }
}
