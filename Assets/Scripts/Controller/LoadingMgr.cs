using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DontMoveObj : ISceneObject
{
    public Bounds bounds { get => _bounds; }
    [SerializeField]
    private Bounds _bounds;
    [SerializeField]
    private string _path;
    [SerializeField]
    private Vector3 _pos;
    [SerializeField]
    private Vector3 _rotation;
    [SerializeField]
    private Vector3 _size;
    private GameObject prefab;
    public DontMoveObj(Bounds bound, Vector3 pos, Vector3 rotation, float sizeX = 1, float sizeY = 1, float sizeZ = 1, string path = "Asset/Prefabs")
    {
        _bounds = bound;
        _path = path;
        _pos = pos;
        _size = new Vector3(sizeX,  sizeY, sizeZ);
        _rotation = rotation;
    }
    public void Load(Transform parent)
    {
        var request = Resources.LoadAsync<GameObject>(_path);
        if (request.isDone)
        {
            var obj = request.asset as GameObject;
            prefab = GameObject.Instantiate<GameObject>(obj);
            prefab.transform.SetParent(parent);
            prefab.transform.position = _pos;
            prefab.transform.eulerAngles = _rotation;
            prefab.transform.localScale = _size;
        }
    }

    public void Unload()
    {
        if (prefab)
        {
            GameObject.Destroy(prefab);
            prefab = null;
        }
    }
}

public class LoadingMgr : MonoBehaviour
{
    public Bounds loadBounds;
    public SceneDetector detector;
    public List<DontMoveObj> loadList;
    private SceneObjectController sceneController;
    private void Awake()
    {
        sceneController = GetComponent<SceneObjectController>();
        if (!sceneController)
            sceneController = gameObject.AddComponent<SceneObjectController>();
        sceneController.Init(loadBounds.center, loadBounds.size);
    }
    void Start()
    {
        foreach (var item in loadList)
        {
            sceneController.AddSceneObj(item);
        }
    }
    void Update()
    {
        sceneController.UpdateDetectorArea(detector);
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(loadBounds.center, loadBounds.size);
    }
}
