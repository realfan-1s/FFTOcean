using System.Collections;
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
    private RenderTexture minMaxHeightRT;
    /* =============  生成HeightMipMapRT  ============= */
    private Dictionary<int, RenderTexture> reduceDict;
    private List<RenderTexture> mipMapRTs;
    private int kernelHeightMipMap;
    public MinMaxTextureMgr()
    {
        mipMapRTs = new List<RenderTexture>();
        reduceDict = new Dictionary<int, RenderTexture>();
        kernelHeightMipMap = minMaxCS.FindKernel("HeightMipMap");
        kernelPatchMinMaxHeight = minMaxCS.FindKernel("PatchMinMaxHeight");
    }
    public void Generate(RenderTexture _heightMap, int _patchMapSize)
    {
        heightRT = _heightMap;
        patchMapSize = _patchMapSize;
        minMaxHeightRT = CreateMinMaxTexture(patchMapSize);
        isInit = true;
    }
    public void RefreshInfos()
    {
        if (!isInit)
            return;
        CalculateHeightMap((tex) =>
        {
            mipMapRTs.Add(tex);
            // TODO: 需要在第一次时生成九个reduceRT
            CalculateMipMap(9, 0, () =>
            {
                for (int i = 0; i < mipMapRTs.Count; ++i)
                    RenderTexture.ReleaseTemporary(mipMapRTs[i]);
            });
        });
    }
    private RenderTexture CreateMinMaxTexture(int texSize)
    {
        RenderTextureDescriptor desc = new RenderTextureDescriptor(texSize, texSize, RenderTextureFormat.ARGB32, 1);
        desc.enableRandomWrite = true;
        desc.autoGenerateMips = true;
        var rt = RenderTexture.GetTemporary(desc);
        rt.filterMode = FilterMode.Point;
        rt.Create();
        return rt;
    }
    void CalculateMipMap(int limit, int count, System.Action callback)
    {
        CalculateMipMap(mipMapRTs[mipMapRTs.Count - 1], count, (tex) =>
        {
            mipMapRTs.Add(tex);
            if (mipMapRTs.Count < limit)
                CalculateMipMap(limit, count++, callback);
            else
                callback();
        });
    }
    void CalculateMipMap(RenderTexture inTex, int count, System.Action<RenderTexture> callback)
    {
        if (reduceDict.ContainsKey(count))
        {
            reduceDict.Add(count, CreateMinMaxTexture(inTex.width / 2));
        }
        int groupX, groupY;
        CalculateGroupXY(kernelHeightMipMap, reduceDict[count].width, out groupX, out groupY);
        minMaxCS.SetTexture(kernelHeightMipMap, "inRT", inTex);
        minMaxCS.SetTexture(kernelHeightMipMap, "reduceRT", reduceDict[count]);
        minMaxCS.Dispatch(kernelHeightMipMap, groupX, groupY, 1);
    }
    void CalculateGroupXY(int index, int size, out int groupX, out int groupY)
    {
        uint threadX, threadY, threadZ;
        minMaxCS.GetKernelThreadGroupSizes(index, out threadX, out threadY, out threadZ);
        groupX = (int)(size / threadX);
        groupY = (int)(size / threadY);
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
}
