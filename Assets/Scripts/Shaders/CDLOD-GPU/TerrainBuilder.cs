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
    public Vector3 worldSize { get; private set; }
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
            if (_plane == null)
            {
                _plane = Ocean.GenerateMesh(16, 8);
            }
            return _plane;
        }
    }
    private RenderTexture _minMaxHeightRT;
    public RenderTexture minMaxHeightRT
    {
        get
        {
            if (_minMaxHeightRT == null)
                _minMaxHeightRT = Ocean.CreateRT(minMaxHeightMaps, RenderTextureFormat.ARGB32);
            return _minMaxHeightRT;
        }
    }
    private RenderTexture _quadTreeRT;
    public RenderTexture quadTreeRT
    {
        get
        {
            if (_quadTreeRT == null)
                _quadTreeRT = Ocean.CreateRT(quadTreeMaps, RenderTextureFormat.R16);
            return _quadTreeRT;
        }
    }
    public TerrainBase(RenderTexture[] _minMaxHeightMaps, RenderTexture[] _quadTreeMaps, Vector3 _worldSize)
    {
        minMaxHeightMaps = _minMaxHeightMaps;
        quadTreeMaps = _quadTreeMaps;
        worldSize = _worldSize;
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
    #region "Compute buffer nameID"
    private static readonly int controlID = Shader.PropertyToID("controllerC");
    private static readonly int camPosID = Shader.PropertyToID("cameraPos");
    private static readonly int worldSizeID = Shader.PropertyToID("worldSize");
    private static readonly int curLodID = Shader.PropertyToID("curLOD");
    #endregion
    #region "Compute shader kernel"
    private static readonly int kernelCreateQuadTree = quadTreeCS.FindKernel("CreateQuadTree");
    private static readonly int kernelTraverseQuadTree = quadTreeCS.FindKernel("TraverseQuadTree");
    private static readonly int kernelCreateLodMap = quadTreeCS.FindKernel("CreateLodMap");
    private static readonly int kernelCreatePatches = quadTreeCS.FindKernel("CreatePatches");
    #endregion
    #region "Compute Buffer"
    // 具体参考https://docs.unity.cn/cn/2021.1/ScriptReference/ComputeBuffer.html
    private ComputeBuffer consumeNodeList;
    private ComputeBuffer appendNodeList;
    private ComputeBuffer appendFinalNodeList;
    private ComputeBuffer nodeInfoList;
    private ComputeBuffer finalNodeList;
    private ComputeBuffer maxNodeList;
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
    private CommandBuffer commandBuffer = new CommandBuffer();
    private Plane[] cameraFrustumPlane = new Plane[6];
    private Vector4[] cameraFrustumPlanesV4 = new Vector4[6];
    private List<RenderTexture> quadMaps = new List<RenderTexture>();
    /// <summary>
    /// 分配buffer的大小
    /// </summary>
    private int maxBufferSize;
    int maxLodSize;
    int lodCount;
    #region "节点评价系数C"
    private Vector4 _controllerC = new Vector4(1, 0, 0, 0);
    private bool changeC = true;
    public float conrollerC
    {
        set
        {
            _controllerC.x = value;
            changeC = true;
        }
    }
    #endregion
    public TerrainBuilder(RenderTexture[] _minMaxHeightMaps, Vector3 _worldSize, int _maxBufferSize = 200, int _maxLodSize = 5, int _lodCount = 6)
    {
        maxLodSize = _maxLodSize;
        lodCount = _lodCount;
        commandBuffer.name = "InfinityTerrain";
        maxBufferSize = _maxBufferSize;
        // 构造函数第一个参数接收输出buffer的长度，第二个参数代表每个元素的长度，第三个参数表示compute buffer的类型
        consumeNodeList = new ComputeBuffer(50, 8, ComputeBufferType.Append);
        maxNodeList = new ComputeBuffer(TerrainBase.MAX_LOD_DEPTH * TerrainBase.MAX_LOD_DEPTH, 8, ComputeBufferType.Append);
        appendNodeList = new ComputeBuffer(50, 8, ComputeBufferType.Append);
        appendFinalNodeList = new ComputeBuffer(maxBufferSize, 12, ComputeBufferType.Append);
        nodeInfoList = new ComputeBuffer((int)(TerrainBase.MAX_NODE_ID + 1), 4);
        finalNodeList = new ComputeBuffer(maxBufferSize, 12, ComputeBufferType.Append);
        _culledPatchList = new ComputeBuffer(maxBufferSize * 64, 20, ComputeBufferType.Append);
        _indirectArgs = new ComputeBuffer(3, 4, ComputeBufferType.IndirectArguments);
        _indirectArgs.SetData(new uint[] { 1, 1, 1 });
        _patchIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        _patchIndirectArgs.SetData(new uint[] { TerrainBase.plane.GetIndexCount(0), 0, 0, 0, 0 });

        InitParams(_minMaxHeightMaps, _worldSize);
    }

    public void Dispose()
    {
        consumeNodeList.Dispose();
        maxNodeList.Dispose();
        appendNodeList.Dispose();
        appendFinalNodeList.Dispose();
        nodeInfoList.Dispose();
        finalNodeList.Dispose();
        _culledPatchList.Dispose();
        _indirectArgs.Dispose();
        _patchIndirectArgs.Dispose();
    }
    void InitParams(RenderTexture[] _minMaxHeightMaps, Vector3 worldSize)
    {
        BindShader(kernelCreateQuadTree);
        tb = new TerrainBase(_minMaxHeightMaps, quadMaps.ToArray(), worldSize);
        BindShader(kernelTraverseQuadTree);
        BindShader(kernelCreatePatches);
        BindShader(kernelCreateLodMap);
        InitWorldParams();
    }
    void InitWorldParams()
    {
        Vector3 worldSize = tb.worldSize;
        int nodeCount = TerrainBase.MAX_LOD_DEPTH;
        Vector4[] worldLodParams = new Vector4[TerrainBase.MAX_LOD_DEPTH + 1];
        int[] offsetOfNodeID = new int[(TerrainBase.MAX_LOD_DEPTH + 1) * 4];
        int offset = 0;

        for (int lod = TerrainBase.MAX_LOD_DEPTH; lod > -1; --lod)
        {
            float nodeSize = (worldSize.x * 1.0f) / nodeCount;
            float halfExtent = nodeSize / 16.0f;
            worldLodParams[lod] = new Vector4(nodeSize, nodeCount, halfExtent, 0);
            nodeCount *= 2;
            // Debug.Log("worldLodPararms[" + lod + "] = " + worldLodParams[lod]);

            offsetOfNodeID[lod * 4] = offset;
            offset += nodeCount * nodeCount;
        }
        quadTreeCS.SetVectorArray("worldLodParams", worldLodParams);
        quadTreeCS.SetInts("offsetOfNodeID", offsetOfNodeID);
    }
    void BindShader(int index)
    {
        if (index == kernelTraverseQuadTree)
        {
            quadTreeCS.SetBuffer(index, "consumeNodeList", consumeNodeList);
            quadTreeCS.SetBuffer(index, "appendNodeList", appendNodeList);
            quadTreeCS.SetBuffer(index, "appendFinalNodeList", appendFinalNodeList);
            quadTreeCS.SetBuffer(index, "nodeInfoList", nodeInfoList);
            quadTreeCS.SetTexture(index, "minMaxHeightRT", tb.minMaxHeightRT);
        }
        else if (index == kernelCreatePatches)
        {
            quadTreeCS.SetBuffer(index, "culledPatchList", culledPatchList);
            quadTreeCS.SetBuffer(index, "finalNodeList", finalNodeList);
            quadTreeCS.SetTexture(index, "minMaxHeightRT", tb.minMaxHeightRT);
        }
        else if (index == kernelCreateLodMap)
        {
            /*========== DO NOTHING! ==========*/
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
    public void Dispatch()
    {
        // 清空缓存区
        commandBuffer.Clear();
        maxNodeList.SetCounterValue((uint)maxNodeList.count);
        consumeNodeList.SetCounterValue(0);
        appendNodeList.SetCounterValue(0);
        culledPatchList.SetCounterValue(0);
        finalNodeList.SetCounterValue(0);

        var cam = Camera.main;
        GeometryUtility.CalculateFrustumPlanes(cam, cameraFrustumPlane);
        for (int i = 0; i < cameraFrustumPlane.Length; ++i)
        {
            Vector4 v4 = cameraFrustumPlane[i].normal;
            v4.w = cameraFrustumPlane[i].distance;
            cameraFrustumPlanesV4[i] = v4;
        }
        quadTreeCS.SetVectorArray("_CameraFrustumPlanes", cameraFrustumPlanesV4);

        // 传入节点评价参数
        if (changeC)
        {
            changeC = false;
            commandBuffer.SetComputeVectorParam(quadTreeCS, controlID, _controllerC);
        }
        commandBuffer.SetComputeVectorParam(quadTreeCS, camPosID, cam.transform.position);
        commandBuffer.SetComputeVectorParam(quadTreeCS, worldSizeID, tb.worldSize);

        // TODO:四叉树分割计算得到初步的patches 
        for (int lod = TerrainBase.MAX_LOD_DEPTH; lod > -1; --lod)
        {
            commandBuffer.SetComputeIntParam(quadTreeCS, curLodID, lod);
        }

        Graphics.ExecuteCommandBuffer(commandBuffer);
    }
}
