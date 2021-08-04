using System;
using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(Ocean))]
public class GPUTerrian : MonoBehaviour
{
    private Ocean ocean;
    private TerrainBuilder terrain;
    [Range(0.1f, 2.0f)]
    public float nodeEvaluationDist = 1.4f;
    private bool patchDebug = false;
    private int meshSize = 8192;
    private void Awake()
    {
        ocean = transform.GetComponent<Ocean>();
        int heightSize = (int)Mathf.Pow(2, ocean.fftRatio);
        MinMaxTextureMgr.instance.Generate(ocean.GetHeightRT(), heightSize);
        Vector3 worldSize = new Vector3(meshSize, 1024, meshSize);
        terrain = new TerrainBuilder(MinMaxTextureMgr.instance.GetHeightMipMaps(), worldSize);
    }
    private void Start()
    {
        ocean.oceanShader.SetFloat("oceanLength", meshSize);
        ocean.oceanMat.SetBuffer("patchList", terrain.culledPatchList);

        terrain.conrollerC = this.nodeEvaluationDist;
    }

    // Update is called once per frame
    void Update()
    {
        if (this.nodeEvaluationDist != terrain.conrollerC)
            terrain.conrollerC = this.nodeEvaluationDist;
        if (patchDebug)
            ocean.oceanMat.EnableKeyword("USE_PATCH_DEBUG");
        else
            ocean.oceanMat.DisableKeyword("USE_PATCH_DEBUG");

        MinMaxTextureMgr.instance.RefreshInfos();
        terrain.Dispatch();
        Graphics.DrawMeshInstancedIndirect(TerrainBase.plane, 0, ocean.oceanMat,
         new Bounds(Vector3.zero, Vector3.one * meshSize), terrain.patchIndirectArgs);

        var data = new uint2x3[terrain.culledPatchList.count];
        terrain.culledPatchList.GetData(data);
    }
    void OnDestroy()
    {
        terrain.Dispose();
    }
}
