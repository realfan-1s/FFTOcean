using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadTree<T> : ISceneSeparateTree<T> where T : ISceneObject, ILinkedListNode
{
    public Bounds bounds => curBounds;

    public int maxDepth => curMaxDepth;
    private Bounds curBounds;
    private int curMaxDepth;
    private float width;
    private float height;
    // uid保存子节点
    private Dictionary<uint, QuadLeaf<T>> childrenDict;
    public QuadTree(Vector3 center, Vector3 size, int maxDepth)
    {
        curMaxDepth = maxDepth;
        curBounds = new Bounds(center, size);
        childrenDict = new Dictionary<uint, QuadLeaf<T>>();
        width = curBounds.size.x / Mathf.Pow(2, maxDepth);
        height = curBounds.size.y / Mathf.Pow(2, maxDepth);
    }
    public void Clear()
    {
        childrenDict.Clear();
    }

    public bool Find(T node)
    {
        if (childrenDict.Count == 0)
            return false;

        foreach (var item in childrenDict)
        {
            if (item.Value.Contains(node))
                return true;
        }
        return false;
    }

    public void Remove(T node)
    {
        if (node == null)
            return;
        if (childrenDict == null || childrenDict.Count == 0)
            return;
        var nodes = node.GetNode();
        if (nodes == null)
            return;

        foreach (var item in nodes)
        {
            if (childrenDict.ContainsKey(item.Key))
            {
                var temp = childrenDict[item.Key];
                if (temp.data != null)
                {
                    var value = (LinkedListNode<T>)item.Value;
                    temp.data.Remove(value);
                }
            }
        }
        nodes.Clear();
    }

    public void Trigger(IDetector detector, TriggerHandler<T> handler)
    {
        if (handler == null)
            return;
        if (curMaxDepth == 0)
        {
            if (childrenDict.ContainsKey(0) && childrenDict[0] != null)
                childrenDict[0].Trigger(detector, handler);
        }
        else
        {
            OnNodeTrigger(detector, handler, 0, curBounds.center, 0.5f * curBounds.size);
        }
    }
    public void Add(T node)
    {
        if (node == null)
            return;
        if (curBounds.Intersects(node.bounds))
        {
            if (curMaxDepth == 0)
            {
                if (!childrenDict.ContainsKey(0))
                    childrenDict[0] = new QuadLeaf<T>();
                var temp = childrenDict[0].Insert(node);
                node.SetLinkedListNode<T>(0, temp);
            }
            else
            {
                InsertNode(node, 0, curBounds.center, curBounds.size);
            }
        }
    }
    // 四叉树递归遍历出发,四叉树下碰撞代号
    /// |2|8
    /// |1|4
    private void OnNodeTrigger(IDetector detector, TriggerHandler<T> handler, int depth, Vector3 center, Vector3 size)
    {
        float centerX = center.x, centerZ = center.z, sizeX = size.x, sizeZ = size.z;
        if (depth == curMaxDepth)
        {
            uint m = MortronFromWorldPos(centerX, centerZ);
            if (childrenDict.ContainsKey(m) && childrenDict[m] != null)
                childrenDict[0].Trigger(detector, handler);
        }
        else
        {
            // 从上下左右四个方向进一步划分
            int detectCode = (int)detector.GetDetectedCode(centerX, center.y, centerZ);
            if ((detectCode & 1) != 0)
            {
                OnNodeTrigger(detector, handler, depth + 1,
                new Vector3(center.x - sizeX, center.y, center.z - sizeZ), size);
            }
            if ((detectCode & 2) != 0)
            {
                OnNodeTrigger(detector, handler, depth + 1,
                new Vector3(center.x - sizeX, center.y, center.z + sizeZ), size);
            }
            if ((detectCode & 4) != 0)
            {
                OnNodeTrigger(detector, handler, depth + 1,
                new Vector3(center.x + sizeX, center.y, centerZ - sizeZ), size);
            }
            if ((detectCode & 8) != 0)
            {
                OnNodeTrigger(detector, handler, depth + 1,
                new Vector3(center.x + sizeX, center.y, center.z + sizeZ), size);
            }
        }
    }
    // 四叉树插入节点
    private void InsertNode(T item, int depth, Vector3 center, Vector3 size)
    {
        float centerX = center.x, centerZ = center.z, sizeX = size.x, sizeZ = size.z;
    }

    // 参见 http://taggedwiki.zubiaga.org/new_content/89c18a3d7f533b80eeca890f06517ec7
    private uint MortronFromWorldPos(float x, float z)
    {
        uint px = (uint)Mathf.FloorToInt((x - curBounds.min.x) / width);
        uint pz = (uint)Mathf.FloorToInt((z - curBounds.min.z) / height);
        return Morton(px, pz);
    }

    private uint Morton(uint x, uint y)
    {
        return (Cast(y) << 1) + Cast(x);
    }
    private uint Cast(uint n)
    {
        n = (n ^ (n << 8)) & 0x00ff00ff;
        n = (n ^ (n << 4)) & 0x0f0f0f0f;
        n = (n ^ (n << 2)) & 0x33333333;
        n = (n ^ (n << 1)) & 0x55555555;
        return n;
    }
    public static implicit operator bool(QuadTree<T> tree)
    {
        return tree != null;
    }
}

public class QuadLeaf<T> where T : ISceneObject, ILinkedListNode
{
    public LinkedList<T> data => m_data;
    private LinkedList<T> m_data;
    public QuadLeaf()
    {
        m_data = new LinkedList<T>();
    }
    public LinkedListNode<T> Insert(T item)
    {
        return m_data.AddLast(item);
    }
    public bool Contains(T item)
    {
        return m_data.Contains(item);
    }
    public void Trigger(IDetector detector, TriggerHandler<T> handler)
    {
        if (handler != null)
        {
            var node = m_data.Last;
            while (node != null)
            {
                if (detector.isDectcted(node.Value.bounds))
                    handler(node.Value);
                node = node.Next;
            }
        }
    }
}