using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MinMaxTextureMgr
{
    private static MinMaxTextureMgr _instance;
    public static MinMaxTextureMgr instance
    {
        get
        {
            if (_instance == null)
                _instance = new MinMaxTextureMgr();
            return _instance;
        }
    }
    private static ComputeShader _minMaxCS;
    private static ComputeShader minMaxCS
    {
        get
        {
            if (_minMaxCS == null)
                _minMaxCS = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Scripts/Shaders/CDLOD-GPU/MinMaxHeight.compute");
            return _minMaxCS;
        }
    }
    private RenderTexture heightRT;
    /* =============  生成MinMaxHeightRT  ============= */
    private int patchMapSize;
    private int kernelPatchMinMaxHeight;
    private bool isInit = false;
    /* =============  生成HeightMipMapRT  ============= */
    private List<RenderTexture> mipMapRTs;
    private int kernelHeightMipMap;
    public MinMaxTextureMgr()
    {
        mipMapRTs = new List<RenderTexture>();
        kernelHeightMipMap = minMaxCS.FindKernel("HeightMipMap");
        kernelPatchMinMaxHeight = minMaxCS.FindKernel("PatchMinMaxHeight");
    }
    public void Generate(RenderTexture _heightMap, int _patchMapSize)
    {
        heightRT = _heightMap;
        patchMapSize = _patchMapSize;
        mipMapRTs.Add(Ocean.CreateRT(patchMapSize, RenderTextureFormat.ARGB32));
        CreateMipMap(8);
        isInit = true;
    }
    public void RefreshInfos()
    {
        if (!isInit)
            return;
        // CalculateHeightMap((tex) =>
        // {
        //     mipMapRTs.Add(tex);
        //     CalculateMipMap(9, 0, () =>
        //     {
        //     });
        // });
        CalculateHeightMap(mipMapRTs[0]);
        for (int i = 1; i < mipMapRTs.Count; ++i)
        {
            CalculateMipMap(mipMapRTs[i - 1], mipMapRTs[i]);
        }
    }
    // public RenderTexture GetMinMaxHeightRT(int pos = 0)
    // {
    //     if (pos >= mipMapRTs.Count)
    //         Debug.Log("所给值超出mipmap个数,mipmap最多为 " + mipMapRTs.Count + "个");
    //     return this.mipMapRTs[pos];
    // }
    void CalculateGroupXY(int index, int size, out int groupX, out int groupY)
    {
        uint threadX, threadY, threadZ;
        minMaxCS.GetKernelThreadGroupSizes(index, out threadX, out threadY, out threadZ);
        groupX = (int)(size / threadX);
        groupY = (int)(size / threadY);
    }

    void CreateMipMap(int limit)
    {
        if (mipMapRTs.Count == 0)
        {
            Debug.Log("初始化失败！");
        }
        while (mipMapRTs.Count < limit)
        {
            CreateMipMap(mipMapRTs[mipMapRTs.Count - 1]);
        }
    }
    void CreateMipMap(RenderTexture inRT)
    {
        var reduceRT = Ocean.CreateRT(inRT.width / 2, RenderTextureFormat.ARGB32);
        mipMapRTs.Add(reduceRT);
        CalculateMipMap(inRT, reduceRT);
    }
    void CalculateMipMap(RenderTexture inRT, RenderTexture reduceRT)
    {
        minMaxCS.SetTexture(kernelHeightMipMap, "inRT", inRT);
        minMaxCS.SetTexture(kernelHeightMipMap, "reduceRT", reduceRT);
        int groupX, groupY;
        CalculateGroupXY(kernelHeightMipMap, reduceRT.width, out groupX, out groupY);
        minMaxCS.Dispatch(kernelHeightMipMap, groupX, groupY, 1);
    }
    void CalculateHeightMap(RenderTexture minMaxHeightRT)
    {
        minMaxCS.SetTexture(kernelPatchMinMaxHeight, "heightRT", heightRT);
        minMaxCS.SetTexture(kernelPatchMinMaxHeight, "minMaxHeightRT", minMaxHeightRT);
        int groupX, groupY;
        CalculateGroupXY(kernelPatchMinMaxHeight, minMaxHeightRT.width, out groupX, out groupY);
        minMaxCS.Dispatch(kernelPatchMinMaxHeight, groupX, groupY, 1);
    }
    public RenderTexture[] GetHeightMipMaps()
    {
        return mipMapRTs.ToArray();
    }
    /*
    TODO: 同步改写成异步调用, 算了没那能力
    void CalculateMipMap(int limit, int count)
    {
        CalculateMipMap(mipMapRTs[mipMapRTs.Count - 1], count, (tex) =>
        {
            mipMapRTs.Add(tex);
            if (mipMapRTs.Count < limit)
                p(limit, count++, callback);
        });
    }
    void CalculateMipMap(RenderTexture inRT, int count, System.Action<RenderTexture> callback)
    {
        if (reduceDict.ContainsKey(count))
        {
            reduceDict.Add(count, CreateMinMaxTexture(inRT.width / 2));
        }
        int groupX, groupY;
        CalculateGroupXY(kernelHeightMipMap, reduceDict[count].width, out groupX, out groupY);
        minMaxCS.SetTexture(kernelHeightMipMap, "inRT", inRT);
        minMaxCS.SetTexture(kernelHeightMipMap, "reduceRT", reduceDict[count]);
        minMaxCS.Dispatch(kernelHeightMipMap, groupX, groupY, 1);
    }
    void CalculateHeightMap(System.Action<RenderTexture> callback)
    {
        if (minMaxHeightRT != null)
            minMaxHeightRT = CreateMinMaxTexture(patchMapSize);
        int groupX, groupY;
        CalculateGroupXY(kernelPatchMinMaxHeight, patchMapSize, out groupX, out groupY);
        minMaxCS.SetTexture(kernelPatchMinMaxHeight, "displaceRT", heightRT);
        minMaxCS.SetTexture(kernelPatchMinMaxHeight, "minMaxHeightRT", minMaxHeightRT);
        minMaxCS.Dispatch(kernelPatchMinMaxHeight, groupX, groupY, 1);
    }
    */
}
