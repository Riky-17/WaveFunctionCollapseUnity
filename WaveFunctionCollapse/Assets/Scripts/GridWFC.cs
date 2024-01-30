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

    public List<Node> getNeighbours(Node node)
    {
        List<Node> neighbours = new();
        for (int x = - 1; x < 2; x += 2)
        {
            int neighbourX = node.x + x;
            if(neighbourX < 0 || neighbourX >= nodesAmountX)
                continue;
            
            Node neighbourNode = grid[neighbourX, node.y];
            if(neighbourNode.isCollapsed)
                continue;

            neighbours.Add(neighbourNode);
        }

        for (int y = - 1; y < 2; y += 2)
        {
            int neighbourY = node.y + y;
            if(neighbourY < 0 || neighbourY >= nodesAmountY)
                continue;

            Node neighbourNode = grid[node.x, neighbourY];
            if(neighbourNode.isCollapsed)
                continue;
            
            neighbours.Add(neighbourNode);
        }
        return neighbours;
    }

    public List<Node> ConvertArrayToList()
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        List<Node> nodes = new();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                nodes.Add(grid[x, y]);
            }            
        }
        return nodes;
    }

    // bool IsNodeOnEdge(Node node)
    // {
    //     if(node.x == 0 || node.y == 0 || node.x == nodesAmountX - 1 || node.y == nodesAmountY - 1)
    //         return true;

    //     return false;
    // }
}
