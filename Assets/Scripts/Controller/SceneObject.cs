using System;
using System.Collections.Generic;
using UnityEngine;

// 物体创建标记
public enum CreateFlag
{
    uncreated = 0,
    brand = 1,
    old = 2,
    outOfBound = 4,
}
// 物体销毁标记
public enum LoadFlag
{
    none = 0,
    readyToLoad = 1,
    readyToDestory = 2,
}

public class SceneObject : ISceneObject, ILinkedListNode, IComparable<SceneObject>
{
    public Bounds bounds { get => _sceneObj.bounds; }
    public float weight { get; private set; }
    // 真正被加载或创建的物体
    public ISceneObject sceneObj { get => _sceneObj; }
    private ISceneObject _sceneObj;
    public CreateFlag createFlag;
    public LoadFlag loadFlag;
    // TODO: 有装箱拆箱的消耗
    private Dictionary<uint, object> childDict;
    public SceneObject(ISceneObject obj)
    {
        _sceneObj = obj;
        weight = 0;
    }
    public LinkedListNode<T> GetLinkedListNode<T>(uint id) where T : ISceneObject
    {
        if (childDict != null && childDict.ContainsKey(id))
            return (LinkedListNode<T>)childDict[id];
        return null;
    }

    public Dictionary<uint, object> GetChildren() => childDict;

    public void Load(Transform parent)
    {
        _sceneObj.Load(parent);
    }
    public void SetLinkedListNode<T>(uint id, LinkedListNode<T> node)
    {
        if (childDict == null)
            childDict = new Dictionary<uint, object>();
        childDict[id] = node;
    }
    public void Unload()
    {
        _sceneObj.Unload();
    }
    public void RaiseWeight()
    {
        this.weight++;
    }

    public int CompareTo(SceneObject other)
    {
        if (this.weight < other.weight)
            return 1;
        else if (this.weight > other.weight)
            return -1;
        else
            return 0;
    }
}
