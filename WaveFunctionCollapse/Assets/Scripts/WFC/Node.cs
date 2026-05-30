using System.Collections.Generic;
using UnityEngine;

public struct NodeInfo
{
    public byte possibleTiles;
    public int entropy;
    public byte tile;
    public int x;
    public int y;

    public NodeInfo(int x, int y, byte possibleTiles)
    {
        this.x = x;
        this.y = y;
        this.possibleTiles = possibleTiles;
        entropy = 0;

        for (int i = 0; i < 8; i++)
        {
            if((possibleTiles & 1 << i) != 0)
                entropy++;
        }

        tile = 0;
    }
}

public class Node : IHeapItem<Node>
{
    public NodeInfo NodeInfo => nodeInfo;
    NodeInfo nodeInfo;
    int heapIndex;
    public Vector2 nodePos;

    public Node(Vector2 pos, NodeInfo nodeInfo)
    {
        nodePos = pos;
        this.nodeInfo = nodeInfo;
    }

    public void Collapse()
    {
        if(nodeInfo.possibleTiles == 0)
            throw new System.IndexOutOfRangeException();

        if(nodeInfo.entropy == 1)
        {
            nodeInfo.tile = nodeInfo.possibleTiles;
            return;
        }

        int[] positions = new int[8];
        int count = 0;

        for (int i = 0; i < 8; i++)
        {
            if((nodeInfo.possibleTiles & 1 << i) != 0)
            {
                positions[count] = i;
                count++;
            }
        }

        nodeInfo.tile = (byte)(1 << positions[Random.Range(0, count)]);
        nodeInfo.possibleTiles = nodeInfo.tile;
        nodeInfo.entropy = 1;
    }

    public int HeapIndex { get => heapIndex; set => heapIndex = value; }

    public int CompareTo(Node other)
    {
        int compare = nodeInfo.entropy.CompareTo(nodeInfo.entropy);
        return -compare;
    }
}
