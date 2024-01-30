using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node
{
    public List<TileWFC> possibleTiles;
    public int entropy => possibleTiles.Count;
    public TileWFC nodeTile;
    public bool isCollapsed => nodeTile != null;
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
}
