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
    /* ========== 生成minMaxHeightRT ========== */
    private int patchMapSize;
    private static int kernelPatchMinMaxHeight = minMaxCS.FindKernel("PatchMinMaxHeight");
    private bool isInit = false;
    private RenderTexture minMaxHeightRT;
    public void Generate(RenderTexture _heightRT, int _patchSize)
    {
        heightRT = _heightRT;
        patchMapSize = _patchSize;
        minMaxHeightRT = Ocean.CreateRT(patchMapSize);
        isInit = true;
    }
    public void RefreshInfos()
    {
        if (!isInit)
            return;
        CalculateHeightMap();
    }
    public void CalculateHeightMap()
    {
        minMaxCS.SetTexture(kernelPatchMinMaxHeight, "heightRT", heightRT);
        minMaxCS.SetTexture(kernelPatchMinMaxHeight, "minMaxHeightRT", minMaxHeightRT);
        int groupX, groupY;
        CalculateGroupXY(patchMapSize, out groupX, out groupY);
        minMaxCS.Dispatch(kernelPatchMinMaxHeight, groupX, groupY, 1);
    }
    public void CalculateGroupXY(int size, out int groupX, out int groupY)
    {
        uint threadX, threadY, threadZ;
        minMaxCS.GetKernelThreadGroupSizes(kernelPatchMinMaxHeight, out threadX, out threadY, out threadZ);
        groupX = (int)(size / threadX);
        groupY = (int)(size / threadY);
    }
    public RenderTexture GetMinMaxHeightRT()
    {
        return minMaxHeightRT;
    }
}
