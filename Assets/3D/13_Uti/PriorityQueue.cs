using System;
using System.Collections.Generic;

public class PriorityQueue<T>
{
    private readonly List<T> m_Listheap;
    private readonly IComparer<T> m_Icomparer;

    public PriorityQueue() :
        this(Comparer<T>.Default)
    { }

    public PriorityQueue(IComparer<T> comparer)
    {
        m_Listheap = new List<T>();
        m_Icomparer = comparer ?? Comparer<T>.Default;
    }

    public int Count => m_Listheap.Count;
    public bool Any() => m_Listheap.Count > 0;
    public void Clear() { m_Listheap.Clear(); }

    public void Enqueue(T Iitem)
    {
        m_Listheap.Add(Iitem);
        SiftUp(m_Listheap.Count - 1);
    }

    public bool IsEmpty() => m_Listheap.Count == 0;

    public T Peek()
    {
        if (m_Listheap.Count == 0)
            throw new InvalidOperationException("Empty PQ");

        return m_Listheap[0];
    }

    public T Dequeue()
    {
        if (m_Listheap.Count == 0)
            throw new InvalidOperationException("Empty PQ");

        T root = m_Listheap[0];
        int last = m_Listheap.Count - 1;

        //최상위 값 맨 마지막으로(O(1) 삭제 위해)
        m_Listheap[0] = m_Listheap[last];
        m_Listheap.RemoveAt(last);

        //0번 노드 맞는 위치까지 내리기
        if (m_Listheap.Count > 0)
            SiftDown(0);

        return root;
    }

    private void SiftUp(int _iIdx)
    {
        while (_iIdx > 0)
        {
            //내 부모 노드와 비교후 크거나 같으면 올리기
            int parent = (_iIdx - 1) / 2;
            if (m_Icomparer.Compare(m_Listheap[_iIdx], m_Listheap[parent]) >= 0)
                break;

            Swap(_iIdx, parent);
            _iIdx = parent;
        }
    }


    //O(log n)
    private void SiftDown(int _iIdx)
    {
        int n = m_Listheap.Count;
        while (true)
        {
            int left = _iIdx * 2 + 1;
            int right = left + 1;

            int smallest = _iIdx;

            //내 자식 노드들과 비교후 더 작은 자식과 교체
            if (left < n && m_Icomparer.Compare(m_Listheap[left], m_Listheap[smallest]) < 0)
                smallest = left;
            if (right < n && m_Icomparer.Compare(m_Listheap[right], m_Listheap[smallest]) < 0)
                smallest = right;

            //더 작은게 없다면 현재 위치에 기록
            if (smallest == _iIdx)
                break;

            Swap(_iIdx, smallest);
            _iIdx = smallest;
        }
    }



    private void Swap(int a, int b)
    {
        T t = m_Listheap[a];
        m_Listheap[a] = m_Listheap[b];
        m_Listheap[b] = t;
    }
}