using System.Collections.Generic;
using UnityEngine;

// 必须被动态加载的物体
public interface ISceneObject
{
    Bounds bounds { get; }
    void Unload();
    void Load(Transform parent);
}

public interface ILinkedListNode
{
    Dictionary<uint, System.Object> GetNode();
    LinkedListNode<T> GetLinkedListNode<T>(uint id) where T : ISceneObject;
    void SetLinkedListNode<T>(uint id, LinkedListNode<T> node);
}