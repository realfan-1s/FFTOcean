using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    PriorityQueue<int> heap;
    private void Awake()
    {
        heap = new PriorityQueue<int>(20);
        for (int i = 0; i < 20; ++i)
        {
            heap.Push((int)Random.Range(0, 20));
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        print("堆中元素的数量为: " + heap.count);
        while (heap.count > 0)
        {
            print(heap.Pop());
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
