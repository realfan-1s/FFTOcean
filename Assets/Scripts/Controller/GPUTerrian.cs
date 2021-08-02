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
    public Material test;
    public int meshSize = 10240;
    [Range(0.1f, 2.0f)]
    public float nodeEvaluationDist = 1.4f;
    private void Awake()
    {
        ocean = transform.GetComponent<Ocean>();
        heightRT = ocean.GetHeightRT();
        int heightSize = (int)Mathf.Pow(2, ocean.fftRatio);
        MinMaxTextureMgr.instance.Generate(heightRT, heightSize);
        Vector3 worldSize = new Vector3(meshSize, 2048, meshSize);
        terrain = new TerrainBuilder(MinMaxTextureMgr.instance.GetHeightMipMaps(), worldSize);
        terrain.conrollerC = this.nodeEvaluationDist;
    }
    // Start is called before the first frame update
    void Start()
    {
        test.SetTexture("_MainTex", terrain.tb.quadTreeRT);
    }

    // Update is called once per frame
    void Update()
    {
        MinMaxTextureMgr.instance.RefreshInfos();
        terrain.Dispatch();
    }
    // TODO: 给material传入buffer，调用Graphics.DrawMeshInstancedIndirect(),批量绘制mesh
    public void DrawMesh(){
        
    }
    private void OnDestroy()
    {
        terrain.Dispose();
    }
}
