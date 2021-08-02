using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Ocean))]
public class GPUTerrian : MonoBehaviour
{
    private Ocean ocean;
    private RenderTexture heightRT;
    private TerrainBuilder terrainBuilder;
    TerrainBuilder terrain;
    [Header("Mesh 相关参数")]
    public int meshSize = 10240;
    private Mesh mesh;
    private MeshFilter filter;
    private MeshRenderer render;
    [Range(0.1f, 2.0f)]
    public float nodeEvaluationDist = 1.4f;
    private void Awake()
    {
        filter = gameObject.GetComponent<MeshFilter>();
        if (filter == null)
            filter = gameObject.AddComponent<MeshFilter>();
        render = gameObject.GetComponent<MeshRenderer>();
        if (render == null)
            render = gameObject.AddComponent<MeshRenderer>();
        ocean = transform.GetComponent<Ocean>();
        heightRT = ocean.GetHeightRT();
        int heightSize = (int)Mathf.Pow(2, ocean.fftRatio);
        MinMaxTextureMgr.instance.Generate(heightRT, heightSize);
        Vector3 worldSize = new Vector3(meshSize, 2048, meshSize);
        terrain = new TerrainBuilder(MinMaxTextureMgr.instance.GetHeightMipMaps(), worldSize);
        terrain.conrollerC = this.nodeEvaluationDist;

        render.material = ocean.oceanMat;
    }
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        MinMaxTextureMgr.instance.RefreshInfos();
        terrain.Dispatch();
        // TODO: 给material传入buffer，调用Graphics.DrawMeshInstancedIndirect(),批量绘制mesh
        GPUCreateMesh();
    }
    void OnDestroy()
    {
        terrain.Dispose();
    }
    private void GPUCreateMesh(){
        Graphics.DrawMeshInstancedIndirect(mesh, );
    }
}
