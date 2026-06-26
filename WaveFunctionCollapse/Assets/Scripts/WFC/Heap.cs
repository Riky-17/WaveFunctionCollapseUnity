using System;
using UnityEngine;

public class Heap
{
    Node[] heap;
    public Vector2Int direction => directions[directionsIndex];
    Vector2Int[] directions;
    public int directionsIndex = 0;
    public Vector2Int startCoord;
    public int HeapSize {get; private set;}

    public Heap(int maxHeapSize) : this(maxHeapSize, null, default) {}

    public Heap(int maxHeapSize, Vector2Int[] directions, Vector2Int startCoord)
    {
        heap = new Node[maxHeapSize];
        this.directions = directions;
        this.startCoord = startCoord;
    }

    public void Add(Node item)
    {
        item.heapIndex = HeapSize;
        heap[HeapSize] = item;
        SortUp(item);
        HeapSize++;
    }

    public Node RemoveFirst()
    {
        Node itemToReturn = heap[0];
        HeapSize--;
        heap[0] = heap[HeapSize];
        heap[0].heapIndex = 0;
        SortDown(heap[0]);

        return itemToReturn; 
    }

    public Node LookFirst()
    {
        if(HeapSize == 0)
            throw new ArgumentOutOfRangeException();
        return heap[0];
    }

    public void SortUp(Node item)
    {
        while (true)
        {
            int parentIndex = (item.heapIndex - 1) / 2;
            if (item.CompareTo(heap[parentIndex]) <= 0)
                break;

            Swap(item, heap[parentIndex]);
        }
    }

    void SortDown(Node item)
    {
        while (true)
        {
            int childLeftIndex = item.heapIndex * 2 + 1;
            int childRightIndex = item.heapIndex * 2 + 2;
            int swapIndex;

            if(childLeftIndex < HeapSize)
            {
                swapIndex = childLeftIndex;
                if(childRightIndex < HeapSize)
                {
                    if(heap[childLeftIndex].CompareTo(heap[childRightIndex]) < 0)
                        swapIndex = childRightIndex;
                }
                if(item.CompareTo(heap[swapIndex]) < 0)
                    Swap(item, heap[swapIndex]);
                else
                    break;
            }
            else
                break;
        }
    }

    void Swap(Node item1, Node item2)
    {
        heap[item1.heapIndex] = item2;
        heap[item2.heapIndex] = item1;
        (item1.heapIndex, item2.heapIndex) = (item2.heapIndex, item1.heapIndex);
    }

    public bool IsDone() => directionsIndex >= directions.Length || directions == null;
}
