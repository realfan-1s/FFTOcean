using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* TODO: 链接上ComputeShader，并调试 */
// [RequireComponent(typeof(Ocean))]
public class GPUTerrian : MonoBehaviour
{
    // private Ocean terrian;
    // private RenderTexture heightRT;
    // public ComputeShader terrianLod;
    private void Awake()
    {
        // terrian = transform.GetComponent<Ocean>();
        // heightRT = terrian.GetDisplaceRT();
        // int heightSize = (int)Mathf.Pow(2, terrian.fftRatio);
        // CreateMinMaxTexture(heightSize);
    }
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

    }
}
