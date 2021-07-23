using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LoadingMgr))]
public class SceneObjectController : MonoBehaviour
{
    // private WaitForEndOfFrame endOfFrame;
    private TriggerHandler<SceneObject> handler;
    // 超出视野后的存活时间
    private float maxLivingTime;
    // 每次检测更新的时间
    private float maxUpdateTime;
    // private int minCount;
    private int maxCount;
    // 销毁队列
    private PriorityQueue<SceneObject> destoryHeap;
    // 任务队列
    private Queue<SceneObject> loadQueue;
    // 加载队列
    private LinkedList<SceneObject> screenList;
    // 四叉树
    private QuadTree<SceneObject> quadTree;
    public IDetector detector;
    private bool initSuccess;
    #region "更新位置相关项"
    private float livingTime;
    private Vector3 destoryPos;
    private float updateTime;
    private Vector3 updatePos;
    #endregion
    public void Init(Vector3 _center, Vector3 _size, int _maxCount = 25, // int _minCount = 5,
                    float _maxUpdateTime = 1, float _maxLivingTime = 5, int _maxDepth = 5)
    {
        if (initSuccess)
        {
            print("已经完成初始化");
            return;
        }
        quadTree = new QuadTree<SceneObject>(_center, _size, _maxDepth);
        destoryHeap = new PriorityQueue<SceneObject>();
        loadQueue = new Queue<SceneObject>();
        screenList = new LinkedList<SceneObject>();
        maxCount = Mathf.Max(0, _maxCount);
        // minCount = Mathf.Clamp(_minCount, 0, maxCount);
        maxLivingTime = _maxLivingTime;
        maxUpdateTime = _maxUpdateTime;
        handler = new TriggerHandler<SceneObject>(this.QuadTreeHandler);
        // 确保一初始化就检测一次
        livingTime = maxLivingTime;
        updateTime = maxUpdateTime;
        initSuccess = true;
    }
    /// <summary>
    /// 将物体加载到内存中
    /// </summary>
    /// <param name="obj">真正从硬盘中读取的物体</param>
    public void AddSceneObj(ISceneObject obj)
    {
        if (!initSuccess || quadTree == null || obj == null)
            return;
        SceneObject so = new SceneObject(obj);
        quadTree.Add(so);
        if (detector != null && detector.isDetected(so.bounds))
            CreateObj(so);
    }
    /// <summary>
    /// 更新探测器所处的位置，动态添加/删除物体
    /// </summary>
    /// <param name="detector"></param>
    public void UpdateDetectorArea(IDetector detector)
    {
        if (!initSuccess)
            return;
        if (updatePos != detector.position)
        {
            updateTime += Time.deltaTime;
            // 更新位置，生成物体
            if (updateTime >= maxUpdateTime)
            {
                updatePos = detector.position;
                updateTime = 0;
                this.detector = detector;
                quadTree.Trigger(detector, this.handler);
                MarkDownObj();
            }
        }
        // if (DestoryPos != detector.position)
        // {
        //     livingTime += Time.deltaTime;
        //     if (livingTime >= maxLivingTime)
        //     {
        //         DestoryPos = detector.position;
        //         livingTime = 0;
        //     }
        // }
        if (destoryPos != detector.position && destoryHeap != null && destoryHeap.count >= maxCount)
        {
            livingTime += Time.deltaTime;
            if (livingTime >= maxLivingTime)
            {
                destoryPos = detector.position;
                livingTime = 0;
                DestoryObj();
            }
        }
    }

    /// <summary>
    /// 回调更新四叉树
    /// </summary>
    /// <param name="data">与当前包围盒发生接触的物体</param>
    public void QuadTreeHandler(SceneObject data)
    {
        if (!initSuccess)
            return;
        data.createFlag = CreateFlag.brand;
        switch (data.createFlag)
        {
            case CreateFlag.old:
                data.RaiseWeight();
                break;
            case CreateFlag.outOfBound:
                screenList.AddLast(data);
                break;
            case CreateFlag.uncreated:
                screenList.AddLast(data);
                CreateObj(data);
                break;
            default:
                break;
        }

    }
    /// <summary>
    /// 在界面创建物体
    /// </summary>
    /// <param name="data"></param>
    public void CreateObj(SceneObject data)
    {
        if (data.sceneObj != null && data.createFlag == CreateFlag.uncreated)
        {
            StartCreateAsync(data);
            data.createFlag = CreateFlag.brand;
        }
    }
    /// <summary>
    /// 从LinkedList中把离开包围盒的物体标记
    /// </summary>
    public void MarkDownObj()
    {

    }
    /// <summary>
    /// 删除区域外且超时的物体,从优先队列中弹出
    /// </summary>
    public void DestoryObj()
    {
        while (destoryHeap.count > 0)
        {
            var obj = destoryHeap.Pop();
            if (obj.createFlag == CreateFlag.outOfBound && obj.sceneObj != null)
            {
                StartDestoryAsync(obj);
                obj.createFlag = CreateFlag.uncreated;
            }
        }
    }
    private void StartCreateAsync(SceneObject data)
    {
        if (data.loadFlag == LoadFlag.readyToDestory)
        {
            data.loadFlag = LoadFlag.none;
            return;
        }
        else if (data.loadFlag == LoadFlag.readyToLoad)
            return;
        data.loadFlag = LoadFlag.readyToLoad;
        loadQueue.Enqueue(data);
        StartCoroutine(TaskAsync());
    }
    private void StartDestoryAsync(SceneObject data)
    {
        if (data.loadFlag == LoadFlag.readyToLoad)
        {
            data.loadFlag = LoadFlag.none;
            return;
        }
        else if (data.loadFlag == LoadFlag.readyToDestory)
            return;
        data.loadFlag = LoadFlag.readyToDestory;
        loadQueue.Enqueue(data);
        StartCoroutine(TaskAsync());
    }
    /// <summary>
    /// 从任务队列中发送
    /// </summary>
    /// <returns></returns>
    private IEnumerator TaskAsync()
    {
        if (loadQueue.Count == 0)
            yield return 0;
        while (loadQueue.Count > 0)
        {
            SceneObject obj = loadQueue.Dequeue();
            if (obj.loadFlag == LoadFlag.readyToLoad)
            {
                obj.Load(transform);
                yield return new WaitForEndOfFrame();
            }
            else if (obj.loadFlag == LoadFlag.readyToDestory)
            {
                obj.Unload();
                yield return new WaitForEndOfFrame();
            }
            obj.loadFlag = LoadFlag.none;
        }
    }
    void OnDestroy()
    {
        if (!initSuccess)
            return;
        quadTree.Clear();
        loadQueue.Clear();
        screenList.Clear();
        quadTree = null;
        handler = null;
        loadQueue = null;
    }
}
