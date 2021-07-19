using System;
using System.Collections.Generic;

public class PriorityQueue<T>
{
    private T[] _data;
    private IComparer<T> _cmp;
    public int count { get; private set; }
    public bool isEmpty => _data.Length <= 0;

    public PriorityQueue() : this(8, Comparer<T>.Default) { }
    public PriorityQueue(int size) : this(size, Comparer<T>.Default) { }
    public PriorityQueue(int size, IComparer<T> comparer)
    {
        _cmp = (comparer == null) ? Comparer<T>.Default : comparer;
        _data = new T[size];
    }
    public void Push(T item)
    {
        if (count >= _data.Length)
            Array.Resize(ref _data, count * 2);

        _data[count] = item;
        FlowUp(count++);
    }
    public T Pop()
    {
        T res = Top();
        _data[0] = _data[--count];
        if (count > 0)
            SinkDown(0);
        return res;
    }
    public T Top()
    {
        if (count > 0)
            return _data[0];
        throw new InvalidOperationException("优先队列内无元素");
    }
    private void FlowUp(int pos)
    {
        T temp = _data[pos];
        int parent = pos / 2;
        while (pos > 0 && _cmp.Compare(temp, _data[parent]) > 0)
        {
            _data[pos] = _data[parent];
            pos = parent;
            parent /= 2;
        }
        _data[pos] = temp;
    }
    private void SinkDown(int pos)
    {
        T temp = _data[pos];
        int children = pos * 2;
        while (children < count)
        {
            if (children + 1 < count && _cmp.Compare(_data[children + 1], _data[children]) > 0)
            {
                children++;
            }
            if (_cmp.Compare(temp, _data[children]) >= 0)
                break;
            _data[pos] = _data[children];
            pos = children;
            children *= 2;
        }
        _data[pos] = temp;
    }
}
