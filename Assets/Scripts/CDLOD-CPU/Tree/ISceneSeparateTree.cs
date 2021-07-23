using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void TriggerHandler<T>(T trigger);

// 树结构可以是四叉树，后期也可以扩展到八叉树
public interface ISceneSeparateTree<T> where T : ISceneObject, ILinkedListNode
{
    // 树节点的包围盒、最大深度等
    Bounds bounds { get; }
    int maxDepth { get; }
    void Clear();
    void Remove(T node);
    void Add(T node);
    bool Find(T node);
    void Trigger(IDetector detector, TriggerHandler<T> handler);
}