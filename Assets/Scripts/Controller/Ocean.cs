using UnityEngine;

public class Ocean : MonoBehaviour
{
    #region
    [Header("FFT控制参数")]
    [Range(3, 14)]
    public int fftRatio = 8; // 纹理尺寸&进行FFT的次数
    public int meshWidth = 200; // 长宽
    public float meshSize = 10; // 网格长度
    public float A = 10;
    public Vector4 windAndSeed = new Vector4(1.0f, 2.0f, 0, 0);
    public float windScale = 2.0f; // 风的强度
    public float lambda = -1.0f;
    public float timeScale = 1.0f;
    public float heightScale = 1.0f;
    public float bubbleScale = 1.0f;
    public float bubbleThreshold = 1.0f;
    [Range(0, 12)]
    public int M = 12; // 执行FFT的次数
    public bool horizontalOrVertical = true; // 控制横向FFT(false)还是纵向FFT(true)
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
    #region
    public Material oceanMat;
    public Material displaceXMat;
    public Material heightMat;
    public Material displaceZMat;
    public Material displaceMat;
    public Material normalMat;
    public Material bubbleMat;
    #endregion
    #region 
    [Header("Mesh相关参数")]
    private MeshFilter filter;
    private Mesh mesh;
    private MeshRenderer render;
    private int[] vertIndexes; // 三角形面索引
    private Vector3[] positions; // 位置索引
    private Vector2[] uvs; // uv坐标信息
    private MeshCollider meshCollider;
    #endregion
    void Awake()
    {
        // 添加mesh renderer、mesh Filter
        filter = gameObject.GetComponent<MeshFilter>();
        if (!filter)
        {
            filter = gameObject.AddComponent<MeshFilter>();
        }

        render = gameObject.GetComponent<MeshRenderer>();
        if (!render)
            render = gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.GetComponent<MeshCollider>();
        if (!meshCollider)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        mesh = new Mesh();
        filter.mesh = mesh;
        render.material = oceanMat;
        meshCollider.sharedMesh = mesh;
    }
    // Start is called before the first frame update
    void Start()
    {
        GenerateMesh();
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
        gaussianRT = CreateRT(fftSize);
        heightSpectrumRT = CreateRT(fftSize);
        displaceXSpectrumRT = CreateRT(fftSize);
        displaceZSpectrumRT = CreateRT(fftSize);
        displaceRT = CreateRT(fftSize);
        bubbleRT = CreateRT(fftSize);
        normalRT = CreateRT(fftSize);
        outputRT = CreateRT(fftSize);

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
        oceanShader.SetFloat("oceanLength", meshSize);

        // 计算高斯随机数
        oceanShader.SetTexture(kernelComputeGaussian, "gaussianRT", gaussianRT);
        oceanShader.Dispatch(kernelComputeGaussian, fftSize / 8, fftSize / 8, 1);
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
        oceanShader.Dispatch(kernelCreateHeightSpectrum, fftSize / 8, fftSize / 8, 1);

        //生成偏移频谱
        oceanShader.SetTexture(kernelCreateDisplaceSpectrum, "heightSpectrumRT", heightSpectrumRT);
        oceanShader.SetTexture(kernelCreateDisplaceSpectrum, "displaceXSpectrumRT", displaceXSpectrumRT);
        oceanShader.SetTexture(kernelCreateDisplaceSpectrum, "displaceZSpectrumRT", displaceZSpectrumRT);
        oceanShader.Dispatch(kernelCreateDisplaceSpectrum, fftSize / 8, fftSize / 8, 1);

        if (M == 0)
        {
            SetMaterial();
            return;
        }

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
            if (horizontalOrVertical && M == i)
            {
                SetMaterial();
                return;
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
            if (!horizontalOrVertical && M == i)
            {
                SetMaterial();
                return;
            }
        }

        // 生成偏移纹理
        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "heightSpectrumRT", heightSpectrumRT);
        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "displaceXSpectrumRT", displaceXSpectrumRT);
        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "displaceZSpectrumRT", displaceZSpectrumRT);
        oceanShader.SetTexture(kernelGenerateDisplaceTexture, "displaceRT", displaceRT);
        oceanShader.Dispatch(kernelGenerateDisplaceTexture, fftSize / 8, fftSize / 8, 1);

        oceanShader.SetTexture(kernelGenerateBubblesAndNormals, "displaceRT", displaceRT);
        oceanShader.SetTexture(kernelGenerateBubblesAndNormals, "normalRT", normalRT);
        oceanShader.SetTexture(kernelGenerateBubblesAndNormals, "bubblesRT", bubbleRT);
        oceanShader.Dispatch(kernelGenerateBubblesAndNormals, fftSize / 8, fftSize / 8, 1);

        SetMaterial();
    }
    RenderTexture CreateRT(int size)
    {
        var res = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        res.enableRandomWrite = true;
        res.Create();
        return res;
    }
    void SetMaterial()
    {
        oceanMat.SetTexture("_Displace", displaceRT);
        oceanMat.SetTexture("_Normal", normalRT);
        oceanMat.SetTexture("_Bubbles", bubbleRT);

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
        oceanShader.Dispatch(kernel, fftSize / 8, fftSize / 8, 1);

        // 纹理交换
        var rtTemp = input;
        input = outputRT;
        outputRT = rtTemp;
    }
    void GenerateMesh()
    {
        vertIndexes = new int[(meshWidth - 1) * (meshWidth - 1) * 6];
        positions = new Vector3[meshWidth * meshWidth];
        uvs = new Vector2[meshWidth * meshWidth];
        int idx = 0;
        for (int i = 0; i < meshWidth; ++i)
        {
            for (int j = 0; j < meshWidth; ++j)
            {
                int index = i * meshWidth + j;

                positions[index] = new Vector3((j - meshWidth / 2.0f) * meshSize / meshWidth, 0, (i - meshWidth / 2.0f) * meshSize / meshWidth);
                uvs[index] = new Vector2(j / (meshWidth - 1.0f), i / (meshWidth - 1.0f));

                if (i != meshWidth - 1 && j != meshWidth - 1)
                {
                    vertIndexes[idx++] = index;
                    vertIndexes[idx++] = index + meshWidth;
                    vertIndexes[idx++] = index + meshWidth + 1;

                    vertIndexes[idx++] = index;
                    vertIndexes[idx++] = index + meshWidth + 1;
                    vertIndexes[idx++] = index + 1;
                }
            }
        }

        mesh.vertices = positions;
        mesh.SetIndices(vertIndexes, MeshTopology.Triangles, 0);
        mesh.uv = uvs;
    }
}
