using UnityEngine;
using System.Collections.Generic;

public class Ocean : MonoBehaviour
{
    #region
    public int fftRatio { get; private set; } // 纹理尺寸&进行FFT的次数
    // public int meshWidth = 200; // 长宽
    // public int meshSize = 10; // 网格长度
    [Header("FFT控制参数")]
    public float A = 10;
    public Vector4 windAndSeed = new Vector4(1.0f, 2.0f, 0, 0);
    public float windScale = 2.0f; // 风的强度
    public float lambda = -1.0f;
    public float timeScale = 1.0f;
    public float heightScale = 1.0f;
    public float bubbleScale = 1.0f;
    public float bubbleThreshold = 1.0f;
    #endregion

    private float time;
    private int fftSize;
    # region 
    public ComputeShader oceanShader;
    private RenderTexture gaussianRT;
    private RenderTexture heightSpectrumRT;
    private RenderTexture displaceXSpectrumRT;
    private RenderTexture displaceZSpectrumRT;
    private RenderTexture outputRT;
    private RenderTexture displaceRT;
    private RenderTexture normalRT;
    private RenderTexture bubbleRT;
    private int kernelComputeGaussian;
    private int kernelCreateHeightSpectrum;
    private int kernelCreateDisplaceSpectrum;
    private int kernelFFTHorizontal;
    private int kernelFFTVertical;
    private int kernelFFTHorizontalEnd;
    private int kernelFFTVerticalEnd;
    private int kernelGenerateDisplaceTexture;
    private int kernelGenerateBubblesAndNormals;
    #endregion

    [Header("海洋材质")]
    public Material oceanMat;
    // public Material GaussianMat;
    // public Material displaceXMat;
    // public Material heightMat;
    // public Material displaceZMat;
    // public Material displaceMat;
    // public Material normalMat;
    // public Material bubbleMat;
    // #region
    // [Header("Mesh相关参数")]
    // private MeshFilter filter;
    // private Mesh mesh;
    // private MeshRenderer render;
    // private int[] vertIndexes; // 三角形面索引
    // private Vector3[] positions; // 位置索引
    // private Vector2[] uvs; // uv坐标信息
    // private MeshCollider meshCollider;
    // #endregion
    void Awake()
    {
        fftRatio = 9;
        // // 添加mesh renderer、mesh Filter
        // filter = gameObject.GetComponent<MeshFilter>();
        // if (!filter)
        // {
        //     filter = gameObject.AddComponent<MeshFilter>();
        // }

        // render = gameObject.GetComponent<MeshRenderer>();
        // if (!render)
        //     render = gameObject.AddComponent<MeshRenderer>();
        // meshCollider = gameObject.GetComponent<MeshCollider>();
        // if (!meshCollider)
        //     meshCollider = gameObject.AddComponent<MeshCollider>();

        // mesh = GenerateMesh(this.meshWidth, this.meshSize);
        // filter.mesh = mesh;
        // render.material = oceanMat;
        // meshCollider.sharedMesh = mesh;
        InitOceanValue();
    }
    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime * timeScale;
        ComputeOceanValues();
    }

    /// <summary>
    /// 生成海洋参数，获取kernel id
    /// </summary>
    void InitOceanValue()
    {
        fftSize = (int)Mathf.Pow(2, fftRatio);
        if (gaussianRT && gaussianRT.IsCreated())
        {
            gaussianRT.Release();
            heightSpectrumRT.Release();
            displaceXSpectrumRT.Release();
            displaceZSpectrumRT.Release();
            displaceRT.Release();
            normalRT.Release();
            bubbleRT.Release();
            outputRT.Release();
        }
        gaussianRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);
        heightSpectrumRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);
        displaceXSpectrumRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);
        displaceZSpectrumRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);
        displaceRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);
        bubbleRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);
        normalRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);
        outputRT = CreateRT(fftSize, RenderTextureFormat.ARGBFloat);

        // 获得kernel ID
        kernelComputeGaussian = oceanShader.FindKernel("ComputeGaussian");
        kernelCreateHeightSpectrum = oceanShader.FindKernel("CreateHeightSpectrum");
        kernelCreateDisplaceSpectrum = oceanShader.FindKernel("CreateDisplaceSpectrum");
        kernelFFTHorizontal = oceanShader.FindKernel("FFTHorizontal");
        kernelFFTVertical = oceanShader.FindKernel("FFTVertical");
        kernelFFTHorizontalEnd = oceanShader.FindKernel("FFTHorizontalEnd");
        kernelFFTVerticalEnd = oceanShader.FindKernel("FFTVerticalEnd");
        kernelGenerateDisplaceTexture = oceanShader.FindKernel("GenerateDisplaceTexture");
        kernelGenerateBubblesAndNormals = oceanShader.FindKernel("GenerateBubblesAndNormals");

        // 传入关键参数
        oceanShader.SetInt("textureSize", fftSize);
        // oceanShader.SetFloat("oceanLength", meshSize);
        // 计算高斯随机数
        oceanShader.SetTexture(kernelComputeGaussian, "gaussianRT", gaussianRT);
        oceanShader.Dispatch(kernelComputeGaussian, fftSize / 32, fftSize / 32, 1);
    }
    void ComputeOceanValues()
    {
        oceanShader.SetFloat("A", A);
        windAndSeed.z = Random.Range(1, 10.0f);
        windAndSeed.w = Random.Range(1, 10.0f);
        Vector2 wind = new Vector2(windAndSeed.x, windAndSeed.y);
        wind.Normalize();
        wind *= windScale;
        oceanShader.SetVector("windAndSeed", new Vector4(wind.x, wind.y, windAndSeed.z, windAndSeed.w));
        oceanShader.SetFloat("time", time);
        oceanShader.SetFloat("lambda", lambda);
        oceanShader.SetFloat("heightScale", heightScale);
        oceanShader.SetFloat("bubbleThreshold", bubbleThreshold);
        oceanShader.SetFloat("bubbleScale", bubbleScale);

        //生成高度频谱
        oceanShader.SetTexture(kernelCreateHeightSpectrum, "gaussianRT", gaussianRT);
        oceanShader.SetTexture(kernelCreateHeightSpectrum, "heightSpectrumRT", heightSpectrumRT);
        oceanShader.Dispatch(kernelCreateHeightSpectrum, fftSize / 32, fftSize / 32, 1);

        //生成偏移频谱
        oceanShader.SetTexture(kernelCreateDisplaceSpectrum, "heightSpectrumRT", heightSpectrumRT);
        oceanShader.SetTexture(kernelCreateDisplaceSpectrum, "displaceXSpectrumRT", displaceXSpectrumRT);
        oceanShader.SetTexture(kernelCreateDisplaceSpectrum, "displaceZSpectrumRT", displaceZSpectrumRT);
        oceanShader.Dispatch(kernelCreateDisplaceSpectrum, fftSize / 32, fftSize / 32, 1);

        // 横向FFT
        for (int i = 1; i <= fftRatio; ++i)
        {
            int Ns = (int)Mathf.Pow(2, i - 1);
            oceanShader.SetInt("Ns", Ns);
            if (i != fftRatio)
            {
                FFT(kernelFFTHorizontal, ref heightSpectrumRT);
                FFT(kernelFFTHorizontal, ref displaceXSpectrumRT);
                FFT(kernelFFTHorizontal, ref displaceZSpectrumRT);
            }
            else
            {
                FFT(kernelFFTHorizontalEnd, ref heightSpectrumRT);
                FFT(kernelFFTHorizontalEnd, ref displaceXSpectrumRT);
                FFT(kernelFFTHorizontalEnd, ref displaceZSpectrumRT);
            }
        }
        // 纵向FFT
        for (int i = 1; i <= fftRatio; ++i)
        {
            int Ns = (int)Mathf.Pow(2, i - 1);
            oceanShader.SetInt("Ns", Ns);
            if (i != fftRatio)
            {
                FFT(kernelFFTVertical, ref heightSpectrumRT);
                FFT(kernelFFTVertical, ref displaceXSpectrumRT);
                FFT(kernelFFTVertical, ref displaceZSpectrumRT);
            }
            else
            {
                FFT(kernelFFTVerticalEnd, ref heightSpectrumRT);
                FFT(kernelFFTVerticalEnd, ref displaceXSpectrumRT);
                FFT(kernelFFTVerticalEnd, ref displaceZSpectrumRT);
            }
        }

        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "heightSpectrumRT", heightSpectrumRT);
        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "displaceXSpectrumRT", displaceXSpectrumRT);
        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "displaceZSpectrumRT", displaceZSpectrumRT);
        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "displaceRT", displaceRT);
        oceanShader.Dispatch(kernelGenerateDisplaceTexture, fftSize / 32, fftSize / 32, 1);

        oceanShader.SetTexture(kernelGenerateBubblesAndNormals, "displaceRT", displaceRT);
        oceanShader.SetTexture(kernelGenerateBubblesAndNormals, "normalRT", normalRT);
        oceanShader.SetTexture(kernelGenerateBubblesAndNormals, "bubblesRT", bubbleRT);
        oceanShader.Dispatch(kernelGenerateBubblesAndNormals, fftSize / 32, fftSize / 32, 1);
        SetMaterial();
    }
    void SetMaterial()
    {
        oceanMat.SetTexture("_Displace", displaceRT);
        oceanMat.SetTexture("_Normal", normalRT);
        oceanMat.SetTexture("_Bubbles", bubbleRT);

        // GaussianMat.SetTexture("_MainTex", gaussianRT);
        // displaceXMat.SetTexture("_MainTex", displaceXSpectrumRT);
        // heightMat.SetTexture("_MainTex", heightSpectrumRT);
        // displaceZMat.SetTexture("_MainTex", displaceZSpectrumRT);
        // displaceMat.SetTexture("_MainTex", displaceRT);
        // normalMat.SetTexture("_MainTex", normalRT);
        // bubbleMat.SetTexture("_MainTex", bubbleRT);
    }
    void FFT(int kernel, ref RenderTexture input)
    {
        oceanShader.SetTexture(kernel, "inputRT", input);
        oceanShader.SetTexture(kernel, "outputRT", outputRT);
        oceanShader.Dispatch(kernel, fftSize / 32, fftSize / 32, 1);
        var temp = input;
        input = outputRT;
        outputRT = temp;
    }
    public RenderTexture GetHeightRT()
    {
        return displaceRT;
    }
    public static Mesh GenerateMesh(int size)
    {
        // var mesh = new Mesh();
        // int[] vertIndexes = new int[(triWidth) * (triWidth) * 6];
        // Vector3[] positions = new Vector3[(triWidth + 1) * (triWidth + 1)];
        // Vector2[] uvs = new Vector2[(triWidth + 1) * (triWidth + 1)];
        // int idx = 0;
        // for (int i = 0; i < triWidth + 1; ++i)
        // {
        //     for (int j = 0; j < triWidth + 1; ++j)
        //     {
        //         int index = i * triWidth + j;

        //         positions[index] = new Vector3((j - triWidth * 0.5f) * meshWidth / triWidth, 0, (i - triWidth * 0.5f) * meshWidth / triWidth);
        //         uvs[index] = new Vector2(j / (triWidth - 1.0f), i / (triWidth - 1.0f));

        //         if (i != triWidth && j != triWidth)
        //         {
        //             vertIndexes[idx++] = index;
        //             vertIndexes[idx++] = index + triWidth;
        //             vertIndexes[idx++] = index + triWidth + 1;

        //             vertIndexes[idx++] = index;
        //             vertIndexes[idx++] = index + triWidth + 1;
        //             vertIndexes[idx++] = index + 1;
        //         }
        //     }
        // }

        // mesh.vertices = positions;
        // mesh.SetIndices(vertIndexes, MeshTopology.Triangles, 0);
        // mesh.uv = uvs;
        // return mesh;

        var mesh = new Mesh();

        var sizePerGrid = 0.5f;
        var totalMeterSize = size * sizePerGrid;
        var gridCount = size * size;
        var triangleCount = gridCount * 2;

        var vOffset = -totalMeterSize * 0.5f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        float uvStrip = 1f / size;
        for (var z = 0; z <= size; z++)
        {
            for (var x = 0; x <= size; x++)
            {
                vertices.Add(new Vector3(vOffset + x * 0.5f, 0, vOffset + z * 0.5f));
                uvs.Add(new Vector2(x * uvStrip, z * uvStrip));
            }
        }
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);

        int[] indices = new int[triangleCount * 3];

        for (var gridIndex = 0; gridIndex < gridCount; gridIndex++)
        {
            var offset = gridIndex * 6;
            var vIndex = (gridIndex / size) * (size + 1) + (gridIndex % size);

            indices[offset] = vIndex;
            indices[offset + 1] = vIndex + size + 1;
            indices[offset + 2] = vIndex + 1;
            indices[offset + 3] = vIndex + 1;
            indices[offset + 4] = vIndex + size + 1;
            indices[offset + 5] = vIndex + size + 2;
        }
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.UploadMeshData(false);
        return mesh;
    }
    public static RenderTexture CreateRT(int size)
    {
        var desc = new RenderTextureDescriptor(size, size, RenderTextureFormat.RG32);
        desc.enableRandomWrite = true;
        desc.autoGenerateMips = true;
        desc.useMipMap = true;
        var rt = new RenderTexture(desc);
        rt.filterMode = FilterMode.Point;
        rt.Create();
        return rt;
    }
    public static RenderTexture CreateRT(int size, RenderTextureFormat format)
    {
        var rt = new RenderTexture(size, size, 0, format);
        rt.enableRandomWrite = true;
        rt.autoGenerateMips = false;
        rt.Create();
        return rt;
    }
}
