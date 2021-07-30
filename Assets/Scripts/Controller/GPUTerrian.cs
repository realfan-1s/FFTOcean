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
    private void Awake()
    {
        ocean = transform.GetComponent<Ocean>();
        heightRT = ocean.GetHeightRT();
        int heightSize = (int)Mathf.Pow(2, ocean.fftRatio);
        MinMaxTextureMgr.instance.Generate(heightRT, heightSize);
        terrain = new TerrainBuilder(MinMaxTextureMgr.instance.GetHeightMipMaps());
    }
    // Start is called before the first frame update
    void Start()
    {
        test.SetTexture("_MainTex", terrain.tb.quadTreeMap);
    }

    // Update is called once per frame
    void Update()
    {
        MinMaxTextureMgr.instance.RefreshInfos();
    }
    private void OnDestroy()
    {
        terrain.Dispose();
    }
}
