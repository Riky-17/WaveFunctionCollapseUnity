using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node
{
    List<TileWFC> tiles;
    public Vector2 nodePos;
    public int x;
    public int y;

    public Node(Vector2 pos, IEnumerable<TileWFC> tiles, int x, int y)
    {
        nodePos = pos;
        this.tiles = new(tiles);
        this.x = x;
        this.y = y;
    }
}
