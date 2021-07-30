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
            if (_plane == null)
                Ocean.GenerateMesh(16, 8);
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
    private ComputeBuffer consumeNodeList;
    private ComputeBuffer appendNodeList;
    private ComputeBuffer appendFinalNodeList;
    private ComputeBuffer nodeInfos;
    private ComputeBuffer finalNodeList;
    #endregion
    private CommandBuffer commandBuffer = new CommandBuffer();
    private Plane[] cameraFrustumPlane = new Plane[6];
    private Vector4[] cameraFrustumPalnesV4 = new Vector4[6];
    private List<RenderTexture> quadMaps;
    /// <summary>
    /// 分配buffer的大小
    /// </summary>
    private int maxBufferSize = 200;
    int maxLodSize;
    int lodCount;
    public TerrainBuilder(RenderTexture[] _minMaxHeightMaps, int _maxLodSize = 5, int _lodCount = 6)
    {
        // TODO:创建ComputeBuffer，为LoadQuadTree赋值
        maxLodSize = _maxLodSize;
        lodCount = _lodCount;
        quadMaps = new List<RenderTexture>();
        // consumeNodeList = new ComputeBuffer();
        // appendNodeList = new ComputeBuffer();
        // appendFinalNodeList = new ComputeBuffer();
        // nodeInfos = new ComputeBuffer();
        // finalNodeList = new ComputeBuffer();
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
    }
    private void InitKernel()
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
    private void BindShader(int index)
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
}
