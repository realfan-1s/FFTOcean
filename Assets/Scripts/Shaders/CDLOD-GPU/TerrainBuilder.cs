using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Unity.Mathematics;

/*
----------------------------------------------------------------
地面尺寸大小
(10240, 2048, 10240)
QuadTree有六层，精度从高到低分别是lod0-lod5
每一个基础mesh有512个三角形面，大小为8m*8m,一个网格分辨率0.5m
lod0 -> 0.5m; lod1 -> 1m; lod2 -> 2m; lod3 -> 4m; lod4 -> 8m; lod5 -> 16m
总共的node总数是4*4+8*8+16*16+32*32+64*64+128*128=21840
----------------------------------------------------------------
*/
public class TerrainBase
{
    public Vector3 worldSize { get; private set; }
    // MAX_NODE_ID = 4 * 4 + 8 * 8 + 16 * 16 + 32 * 32 + 64 * 64 + 128 * 128
    public const uint MAX_NODE_ID = 21840;
    public const int MAX_LOD_DEPTH = 5;
    // MAX LOD下，世界由4x4个区块组成
    public const int MAX_LOD_COUNT = 4;
    private static Mesh _plane;
    public static Mesh plane
    {
        get
        {
            if (_plane == null)
            {
                _plane = Ocean.GenerateMesh(16);
            }
            return _plane;
        }
    }
    private RenderTexture _minMaxHeightRT;
    public RenderTexture minMaxHeightRT
    {
        get
        {
            return _minMaxHeightRT;
        }
    }
    public TerrainBase(RenderTexture _minMaxHeightMaps, Vector3 _worldSize)
    {
        _minMaxHeightRT = _minMaxHeightMaps;
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
    private static readonly int boundRedundanceID = Shader.PropertyToID("boundRedundance");
    private static readonly int controlID = Shader.PropertyToID("controllerC");
    private static readonly int camPosID = Shader.PropertyToID("cameraPos");
    private static readonly int worldSizeID = Shader.PropertyToID("worldSize");
    private static readonly int curLodID = Shader.PropertyToID("curLOD");
    private static readonly int consumeNodeListID = Shader.PropertyToID("consumeNodeList");
    private static readonly int appendNodeListID = Shader.PropertyToID("appendNodeList");
    #endregion
    #region "Compute shader kernel"
    private static readonly int kernelTraverseQuadTree = quadTreeCS.FindKernel("TraverseQuadTree");
    private static readonly int kernelCreatePatches = quadTreeCS.FindKernel("CreatePatches");
    #endregion
    #region "Compute Buffer"
    // 具体参考https://docs.unity.cn/cn/2021.1/ScriptReference/ComputeBuffer.html
    private ComputeBuffer consumeNodeList;
    private ComputeBuffer appendNodeList;
    private ComputeBuffer nodeInfoList;
    private ComputeBuffer finalNodeList;
    private ComputeBuffer maxNodeList;
    // 传入shader中
    private ComputeBuffer _culledPatchList;
    public ComputeBuffer culledPatchList { get => _culledPatchList; }
    // indirectArgs表示创建patch所需要的线程组的数量
    private ComputeBuffer _indirectArgs;
    private ComputeBuffer _patchIndirectArgs;
    public ComputeBuffer patchIndirectArgs { get => _patchIndirectArgs; }
    #endregion
    private CommandBuffer commandBuffer = new CommandBuffer { name = "InfinityTerrain" };
    private Plane[] cameraFrustumPlane = new Plane[6];
    private Vector4[] cameraFrustumPlanesV4 = new Vector4[6];
    /// <summary>
    /// 分配buffer的大小
    /// </summary>
    private int maxBufferSize;
    int maxLodSize;
    int lodCount;
    #region "节点评价系数C"
    private float _controllerC = 1.0f;
    private bool changeC = true;
    public float conrollerC
    {
        get => _controllerC;
        set
        {
            _controllerC = value;
            changeC = true;
        }
    }
    #endregion
    #region "冗余长度"
    private bool setRedundance = true;
    private float _boundRedundance;
    public float boundRedundance
    {
        get
        {
            return _boundRedundance;
        }
        set
        {
            _boundRedundance = value;
            setRedundance = true;
        }
    }
    #endregion
    public TerrainBuilder(RenderTexture _minMaxHeightMaps, Vector3 _worldSize,
    int _maxBufferSize = 200, int _maxLodSize = 4, int _lodCount = 6)
    {
        maxLodSize = _maxLodSize;
        lodCount = _lodCount;
        maxBufferSize = _maxBufferSize;
        // 构造函数第一个参数接收输出buffer的长度，第二个参数代表每个元素的长度，第三个参数表示compute buffer的类型
        consumeNodeList = new ComputeBuffer(50, 8, ComputeBufferType.Append);
        appendNodeList = new ComputeBuffer(50, 8, ComputeBufferType.Append);
        maxNodeList = new ComputeBuffer(TerrainBase.MAX_LOD_COUNT * TerrainBase.MAX_LOD_COUNT, 8, ComputeBufferType.Append);
        InitMaxLodData();
        nodeInfoList = new ComputeBuffer((int)TerrainBase.MAX_NODE_ID, 4);
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
        nodeInfoList.Dispose();
        finalNodeList.Dispose();
        _culledPatchList.Dispose();
        _indirectArgs.Dispose();
        _patchIndirectArgs.Dispose();
    }
    void InitParams(RenderTexture _minMaxHeightMaps, Vector3 worldSize)
    {
        tb = new TerrainBase(_minMaxHeightMaps, worldSize);
        BindShader(kernelTraverseQuadTree);
        BindShader(kernelCreatePatches);
        InitWorldParams();
    }
    void InitWorldParams()
    {
        float wSize = tb.worldSize.x;
        int nodeCount = TerrainBase.MAX_LOD_COUNT;
        Vector4[] worldLodParams = new Vector4[TerrainBase.MAX_LOD_DEPTH + 1];
        int[] offsetOfNodeID = new int[(TerrainBase.MAX_LOD_DEPTH + 1) * 4];
        int offset = 0;

        for (int lod = TerrainBase.MAX_LOD_DEPTH; lod > -1; --lod)
        {
            float nodeSize = wSize / nodeCount;
            float halfExtent = nodeSize / 16.0f;
            float sector = Mathf.Pow(2, lod);
            worldLodParams[lod] = new Vector4(nodeSize, nodeCount, halfExtent, sector);

            offsetOfNodeID[lod * 4] = offset;
            offset += nodeCount * nodeCount;
            nodeCount *= 2;
        }
        quadTreeCS.SetVectorArray("worldLodParams", worldLodParams);
        quadTreeCS.SetInts("offsetOfNodeID", offsetOfNodeID);
    }
    void InitMaxLodData()
    {
        int lodCountPerNode = TerrainBase.MAX_LOD_COUNT;
        uint2[] datas = new uint2[lodCountPerNode * lodCountPerNode];
        var index = 0;
        for (uint i = 0; i < lodCountPerNode; i++)
        {
            for (uint j = 0; j < lodCountPerNode; j++)
            {
                datas[index] = new uint2(i, j);
                index++;
            }
        }
        maxNodeList.SetData(datas);
    }
    void BindShader(int index)
    {
        if (index == kernelTraverseQuadTree)
        {
            quadTreeCS.SetBuffer(index, "consumeNodeList", consumeNodeList);
            quadTreeCS.SetBuffer(index, "appendNodeList", appendNodeList);
            quadTreeCS.SetBuffer(index, "appendFinalNodeList", finalNodeList);
            quadTreeCS.SetBuffer(index, "nodeInfoList", nodeInfoList);
            quadTreeCS.SetTexture(index, "minMaxHeightRT", tb.minMaxHeightRT);
        }
        else if (index == kernelCreatePatches)
        {
            quadTreeCS.SetBuffer(index, "culledPatchList", _culledPatchList);
            quadTreeCS.SetBuffer(index, "finalNodeList", finalNodeList);
            quadTreeCS.SetTexture(index, "minMaxHeightRT", tb.minMaxHeightRT);
        }
    }
    public void Dispatch()
    {
        var cam = Camera.main;
        // 清空缓存区
        commandBuffer.Clear();
        maxNodeList.SetCounterValue((uint)maxNodeList.count);
        consumeNodeList.SetCounterValue(0);
        appendNodeList.SetCounterValue(0);
        _culledPatchList.SetCounterValue(0);
        finalNodeList.SetCounterValue(0);
        commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

        GeometryUtility.CalculateFrustumPlanes(cam, cameraFrustumPlane);
        for (int i = 0; i < cameraFrustumPlane.Length; ++i)
        {
            Vector4 v4 = (Vector4)cameraFrustumPlane[i].normal;
            v4.w = cameraFrustumPlane[i].distance;
            cameraFrustumPlanesV4[i] = v4;
        }
        quadTreeCS.SetVectorArray("cameraFrustumPlanes", cameraFrustumPlanesV4);

        // 传入节点评价参数
        if (changeC)
        {
            changeC = false;
            commandBuffer.SetComputeFloatParam(quadTreeCS, controlID, _controllerC);
        }
        if (setRedundance)
        {
            setRedundance = false;
            commandBuffer.SetComputeFloatParam(quadTreeCS, boundRedundanceID, boundRedundance);
        }
        commandBuffer.SetComputeVectorParam(quadTreeCS, camPosID, cam.transform.position);
        commandBuffer.SetComputeVectorParam(quadTreeCS, worldSizeID, tb.worldSize);

        // commandBuffer.CopyCounterValue(maxNodeList, _indirectArgs, 0);
        for (int lod = TerrainBase.MAX_LOD_DEPTH; lod > -1; --lod)
        {
            commandBuffer.SetComputeIntParam(quadTreeCS, curLodID, lod);
            if (lod == TerrainBase.MAX_LOD_DEPTH)
            {
                commandBuffer.SetComputeBufferParam(quadTreeCS, kernelTraverseQuadTree, consumeNodeListID, maxNodeList);
            }
            else
            {
                commandBuffer.SetComputeBufferParam(quadTreeCS, kernelTraverseQuadTree, consumeNodeListID, consumeNodeList);
            }
            commandBuffer.SetComputeBufferParam(quadTreeCS, kernelTraverseQuadTree, appendNodeListID, appendNodeList);
            commandBuffer.DispatchCompute(quadTreeCS, kernelTraverseQuadTree, 1, 1, 1);
            commandBuffer.CopyCounterValue(appendNodeList, _indirectArgs, 0);
            var temp = consumeNodeList;
            consumeNodeList = appendNodeList;
            appendNodeList = temp;
        }

        commandBuffer.CopyCounterValue(finalNodeList, _indirectArgs, 0);
        commandBuffer.DispatchCompute(quadTreeCS, kernelCreatePatches, _indirectArgs, 0);
        commandBuffer.CopyCounterValue(_culledPatchList, _patchIndirectArgs, 4);
        Graphics.ExecuteCommandBufferAsync(commandBuffer, ComputeQueueType.Background);
    }
}
