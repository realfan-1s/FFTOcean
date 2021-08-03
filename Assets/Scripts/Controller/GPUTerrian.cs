using System;
using UnityEngine;

[RequireComponent(typeof(Ocean))]
public class GPUTerrian : MonoBehaviour
{
    private Ocean ocean;
    private TerrainBuilder terrain;
    [Header("整体长度")]
    public int meshSize = 10240;
    [Range(0.1f, 2.0f)]
    public float nodeEvaluationDist = 1.4f;
    public Material test1;
    public Material test2;
    public Material test3;
    private void Awake()
    {
        ocean = transform.GetComponent<Ocean>();
        int heightSize = (int)Mathf.Pow(2, ocean.fftRatio);
        MinMaxTextureMgr.instance.Generate(ocean.GetHeightRT(), heightSize);
        Vector3 worldSize = new Vector3(meshSize, 2048, meshSize);
        terrain = new TerrainBuilder(MinMaxTextureMgr.instance.GetHeightMipMaps(), worldSize);
    }
    private void Start()
    {
        ocean.oceanShader.SetFloat("oceanLength", meshSize);
        ocean.oceanMat.SetBuffer("patchList", terrain.culledPatchList);

        // TODO: minMaxHeightRTs生成成功，但还未开始计算，导致minMaxRT无法及时更新
        test1.SetTexture("_MainTex", terrain.tb.minMaxHeightRT);
        test2.SetTexture("_MainTex", MinMaxTextureMgr.instance.GetHeightMipMaps()[0]);
        test3.SetTexture("_MainTex", terrain.tb.quadTreeRT);
        terrain.conrollerC = this.nodeEvaluationDist;
    }

    // Update is called once per frame
    void Update()
    {
        if (this.nodeEvaluationDist != terrain.conrollerC)
            terrain.conrollerC = this.nodeEvaluationDist;
        MinMaxTextureMgr.instance.RefreshInfos();
        terrain.Dispatch();
        // TODO:调用Graphics.DrawMeshInstancedIndirect(),批量绘制mesh
        Graphics.DrawMeshInstancedIndirect(TerrainBase.plane, 0, ocean.oceanMat,
         new Bounds(Vector3.zero, Vector3.one * meshSize), terrain.patchIndirectArgs);
    }
    void OnDestroy()
    {
        terrain.Dispose();
    }
}
