using UnityEngine;

[RequireComponent(typeof(Ocean))]
public class GPUTerrian : MonoBehaviour
{
    private Ocean ocean;
    private TerrainBuilder terrain;
    [Range(0.1f, 2.0f)]
    public float nodeEvaluationDist = 1.4f;
    [Range(25.0f, 75.0f)]
    public float boundRedundance = 50.0f;
    public bool patchDebug = false;
    private int meshSize = 8192;
    private void Awake()
    {
        ocean = transform.GetComponent<Ocean>();
        int heightSize = (int)Mathf.Pow(2, ocean.fftRatio);
        MinMaxTextureMgr.instance.Generate(ocean.GetHeightRT(), heightSize);
        Vector3 worldSize = new Vector3(meshSize, 1024, meshSize);
        terrain = new TerrainBuilder(MinMaxTextureMgr.instance.GetMinMaxHeightRT(), worldSize);
    }
    private void Start()
    {
        ocean.oceanShader.SetFloat("oceanLength", meshSize);
        ocean.oceanMat.SetBuffer("patchList", terrain.culledPatchList);
        ocean.oceanShader.SetFloats("worldSize", new float[3] { meshSize, ocean.heightScale, meshSize });

        terrain.conrollerC = this.nodeEvaluationDist;
    }

    // Update is called once per frame
    void Update()
    {
        MinMaxTextureMgr.instance.RefreshInfos();
        if (this.nodeEvaluationDist != terrain.conrollerC)
            terrain.conrollerC = this.nodeEvaluationDist;
        if (this.boundRedundance != terrain.boundRedundance)
            terrain.boundRedundance = this.boundRedundance;
        if (patchDebug)
            ocean.oceanMat.EnableKeyword("USE_PATCH_DEBUG");
        else
            ocean.oceanMat.DisableKeyword("USE_PATCH_DEBUG");

        terrain.Dispatch();
        Graphics.DrawMeshInstancedIndirect(TerrainBase.plane, 0, ocean.oceanMat,
         new Bounds(Vector3.zero, Vector3.one * meshSize), terrain.patchIndirectArgs);
    }
    void OnDestroy()
    {
        terrain.Dispose();
    }
}
