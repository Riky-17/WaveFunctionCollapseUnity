using System.Collections.Generic;
using UnityEngine;

public class GridWFC : MonoBehaviour
{
    [SerializeField] List<TileWFC> tiles;

    float nodeRadius = .5f;
    float nodeDiameter => nodeRadius * 2;
    float gridSizeX = 200f;
    float gridSizeY = 200f;
    public int nodesAmountX => Mathf.RoundToInt(gridSizeX / nodeDiameter);
    public int nodesAmountY => Mathf.RoundToInt(gridSizeY / nodeDiameter);
    public Node[,] grid {get; private set;}

    public void CreateGrid()
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

                if(x == 0 || x == nodesAmountX - 1 || y == 0 || y == nodesAmountY - 1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if(!TryGetNeighborFromDirection(i, node, out Node neighbour))
                        {
                            foreach (TileWFC tile in tiles)
                            {
                                if(tile.sockets[i] > 0)
                                node.possibleTiles.Remove(tile);
                            }
                        }
                    }
                }
                grid[x, y] = node;
            }
        }
    }

    public bool TryGetNeighborFromDirection(int socketDir, Node currenNode, out Node neighbour)
    {
        switch (socketDir)
        {
            case 0:
                int up = currenNode.y + 1;
                if(up >= nodesAmountY)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[currenNode.x, up];
                return true;
            case 1:
                int right = currenNode.x + 1;
                if (right >= nodesAmountX)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[right, currenNode.y];
                return true;
            case 2:
                int down = currenNode.y - 1;
                if(down < 0)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[currenNode.x, down];
                return true;
            case 3:
                int left = currenNode.x - 1;
                if (left < 0)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[left, currenNode.y];
                return true;
            default:
                neighbour = null;
                return false;
        }
    }

    public Heap<Node> ConvertArrayToHeap()
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        Heap<Node> nodes = new(width * height);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                nodes.Add(grid[x, y]);
            }            
        }
        return nodes;
    }
}
