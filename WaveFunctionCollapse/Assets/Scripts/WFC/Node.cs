using System;
using System.Collections.Generic;
using UnityEngine;

public struct NodeInfo
{
    public uint possibleTiles;
    public int entropy;
    public uint tile;
    public int x;
    public int y;

    public NodeInfo(int x, int y, uint possibleTiles)
    {
        this.x = x;
        this.y = y;
        this.possibleTiles = possibleTiles;
        entropy = 0;

        for (int i = 0; i < 12; i++)
        {
            if((possibleTiles & 1 << i) != 0)
                entropy++;
        }

        tile = 0;
    }
}

public class Node
{
    public NodeInfo NodeInfo => nodeInfo;
    NodeInfo nodeInfo;
    NodeInfo originalInfo;
    public Vector2 nodePos;
    public int heapIndex;
    public int chunkIndex = -1;

    public Node(Vector2 pos, NodeInfo nodeInfo)
    {
        nodePos = pos;
        this.nodeInfo = nodeInfo;
        originalInfo = nodeInfo;
    }

    public void Reset() => nodeInfo = originalInfo;

    public void UpdateInfo(NodeInfo nodeInfo) => this.nodeInfo = nodeInfo;
}
