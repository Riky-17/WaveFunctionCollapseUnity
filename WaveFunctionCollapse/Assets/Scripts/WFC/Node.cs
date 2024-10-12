using System.Collections.Generic;
using UnityEngine;

public class Node : IHeapItem<Node>
{
    public List<TileWFC> possibleTiles;
    public int Entropy => possibleTiles.Count;
    public int collapsedNeighbours;
    public TileWFC nodeTile;
    public bool IsCollapsed => nodeTile != null;
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

    public void ReduceEntropy()
    {
        List<TileWFC> possibleTilesDup = new(possibleTiles);
        foreach (TileWFC tile in possibleTilesDup)
            if(tile != nodeTile)
                possibleTiles.Remove(tile);
    }

    public int HeapIndex { get => heapIndex; set => heapIndex = value; }

    public int CompareTo(Node other)
    {
        int compare = Entropy.CompareTo(other.Entropy);
        if(compare == 0)
            return collapsedNeighbours.CompareTo(other.collapsedNeighbours);
        return -compare;
    }
}
