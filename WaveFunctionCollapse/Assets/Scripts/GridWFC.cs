using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridWFC : MonoBehaviour
{
    [SerializeField] List<TileWFC> tiles;

    float nodeRadius = .5f;
    float nodeDiameter => nodeRadius * 2;
    float gridSizeX = 20f;
    float gridSizeY = 20f;
    public int nodesAmountX => Mathf.RoundToInt(gridSizeX / nodeDiameter);
    public int nodesAmountY => Mathf.RoundToInt(gridSizeY / nodeDiameter);
    Node[,] grid;

    void Awake()
    {
        CreateGrid();
    }

    void CreateGrid()
    {
        grid = new Node[nodesAmountX, nodesAmountY];
        Vector2 bottomLeft = new(-(gridSizeX / 2), -(gridSizeY / 2));

        for (int x = 0; x < nodesAmountX; x++)
        {
            for (int y = 0; y < nodesAmountY; y++)
            {
                float xPos = nodeRadius + nodeDiameter * x;
                float yPos = nodeRadius + nodeDiameter * y;
                Vector2 nodePos = new Vector2(xPos, yPos) + bottomLeft;
                Node node = new(nodePos, tiles, x, y);
                grid[x, y] = node;
            }
        }
    }

    // bool IsNodeOnEdge(Node node)
    // {
    //     if(node.x == 0 || node.y == 0 || node.x == nodesAmountX - 1 || node.y == nodesAmountY - 1)
    //         return true;

    //     return false;
    // }
}
