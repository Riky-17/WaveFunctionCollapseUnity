using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node : IHeapItem<Node>
{
    public List<TileWFC> possibleTiles;
    public int entropy => possibleTiles.Count;
    public TileWFC nodeTile;
    public bool isCollapsed => nodeTile != null;
    int heapIndex;
    public Vector2 nodePos;
    public int x;
    public int y;

    public Node(Vector2 pos, IEnumerable<TileWFC> tiles, int x, int y)
    {
        nodePos = pos;
        possibleTiles = new(tiles);
        this.x = x;
        this.y = y;
    }

    public int HeapIndex {get => heapIndex; set => heapIndex = value;}

    public int CompareTo(Node other)
    {
        int compare = entropy.CompareTo(other.entropy);
        return -compare;
    }
}
