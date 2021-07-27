using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Ocean))]
public class GPUTerrian : MonoBehaviour
{
    private Ocean terrian;
    private RenderTexture heightRT;
    public ComputeShader terrianLod;
    public ComputeShader heightMipMap;
    private void Awake()
    {
        terrian = transform.GetComponent<Ocean>();
        heightRT = terrian.GetDisplaceRT();
        int heightSize = (int)Mathf.Pow(2, terrian.fftRatio);
        CreateMinMaxTexture(heightSize);
    }
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

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
}
