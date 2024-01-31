using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Heap<T> where T : IHeapItem<T>
{
    T[] heap;
    public int HeapSize {get; private set;}

    public Heap(int maxHeapSize)
    {
        heap = new T[maxHeapSize];
    }

    public void Add(T item)
    {
        item.HeapIndex = HeapSize;
        heap[HeapSize] = item;
        SortUp(item);
        HeapSize++;
    }

    public T RemoveFirst()
    {
        T itemToReturn = heap[0];
        HeapSize--;
        heap[0] = heap[HeapSize];
        heap[0].HeapIndex = 0;
        SortDown(heap[0]);

        return itemToReturn; 
    }

    public void SortUp(T item)
    {
        while (true)
        {
            int parentIndex = (item.HeapIndex - 1) / 2;
            if (item.CompareTo(heap[parentIndex]) <= 0)
                break;

            Swap(item, heap[parentIndex]);
        }
    }

    void SortDown(T item)
    {
        while (true)
        {
            int childLeftIndex = item.HeapIndex * 2 + 1;
            int childRightIndex = item.HeapIndex * 2 + 2;
            int swapIndex;

            if(childLeftIndex < HeapSize)
            {
                swapIndex = childLeftIndex;
                if(childRightIndex < HeapSize)
                {
                    if(heap[childLeftIndex].CompareTo(heap[childRightIndex]) < 0)
                    {
                        swapIndex = childRightIndex;
                    }
                }
                if(item.CompareTo(heap[swapIndex]) < 0)
                {
                    Swap(item, heap[swapIndex]);
                }
                else
                    break;

            }
            else
                break;
        }
    }

    void Swap(T item1, T item2)
    {
        heap[item1.HeapIndex] = item2;
        heap[item2.HeapIndex] = item1;
        (item1.HeapIndex, item2.HeapIndex) = (item2.HeapIndex, item1.HeapIndex);
    }
}

public interface IHeapItem<T> : IComparable<T>
{
    int HeapIndex { get; set; }
}
