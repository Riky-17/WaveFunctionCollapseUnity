using System.Collections.Generic;
using UnityEngine;

public class WFC : MonoBehaviour
{
    //grid fields
    [SerializeField] List<TileWFC> tiles;

    float nodeRadius = .5f;
    float NodeDiameter => nodeRadius * 2;
    public float gridSizeX = 70f;
    public float gridSizeY = 60f;
    public int NodesAmountX => Mathf.RoundToInt(gridSizeX / NodeDiameter);
    public int NodesAmountY => Mathf.RoundToInt(gridSizeY / NodeDiameter);
    Node[,] grid;

    Heap<Node> nodesToCollapse;
    readonly Stack<Node> nodesStack = new();

    public void WaveFunctionCollapse()
    {
        float timer;
        timer = Time.time;
        CreateGrid();
        ConvertArrayToHeap();
        StartWaveFunctionCollapse();
        timer -= Time.time;
        Debug.Log(timer);
    }

    void CreateGrid()
    {
        grid = new Node[NodesAmountX, NodesAmountY];
        Vector2 bottomLeft = new(-(gridSizeX / 2), -(gridSizeY / 2));

        for (int x = 0; x < NodesAmountX; x++)
        {
            for (int y = 0; y < NodesAmountY; y++)
            {
                float xPos = nodeRadius + NodeDiameter * x;
                float yPos = nodeRadius + NodeDiameter * y;
                Vector2 nodePos = new Vector2(xPos, yPos) + bottomLeft;
                Node node = new(nodePos, tiles, x, y);

                if(x == 0 || x == NodesAmountX - 1 || y == 0 || y == NodesAmountY - 1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (!TryGetNeighborFromDirection(i, node, out _))
                        {
                            foreach (TileWFC tile in tiles)
                            {
                                if(tile.GetSocket(i) > 0)
                                node.possibleTiles.Remove(tile);
                            }
                        }
                    }
                }
                grid[x, y] = node;
            }
        }
    }

    void ConvertArrayToHeap()
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        nodesToCollapse = new(width * height);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                nodesToCollapse.Add(grid[x, y]);
            }            
        }
    }

    bool TryGetNeighborFromDirection(int socketDir, Node currentNode, out Node neighbour)
    {
        switch (socketDir)
        {
            case 0:
                int up = currentNode.y + 1;
                if(up >= NodesAmountY)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[currentNode.x, up];
                return true;
            case 1:
                int right = currentNode.x + 1;
                if (right >= NodesAmountX)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[right, currentNode.y];
                return true;
            case 2:
                int down = currentNode.y - 1;
                if(down < 0)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[currentNode.x, down];
                return true;
            case 3:
                int left = currentNode.x - 1;
                if (left < 0)
                {
                    neighbour = null;
                    return false;
                }
                neighbour = grid[left, currentNode.y];
                return true;
            default:
                throw new System.ArgumentOutOfRangeException();
        }
    }

    void StartWaveFunctionCollapse()
    {
        Node currentNode;

        while(nodesToCollapse.HeapSize > 0)
        {
            currentNode = nodesToCollapse.RemoveFirst();
            if(currentNode.Entropy == 0)
                Debug.Log(currentNode.nodePos);

            //set the tile of the current node
            int tileIndex = currentNode.possibleTiles.Count > 1 ? Random.Range(0, currentNode.possibleTiles.Count) : 0;
            currentNode.nodeTile = currentNode.possibleTiles[tileIndex];
            currentNode.ReduceEntropy();
            Instantiate(currentNode.nodeTile.Object, currentNode.nodePos, Quaternion.identity, transform);

            if(nodesToCollapse.HeapSize <= 0)
                break;

            nodesStack.Push(currentNode);
            
            //Propagate the collapse to the neighbours
            if(nodesStack.Count > 0)
                PropagateCollapse();
            
        }
    }

    void PropagateCollapse()
    {
        List<int> possibleSockets = new();
        List<TileWFC> neighbourTiles;
        while(nodesStack.Count > 0)
        {
            Node currentNode = nodesStack.Pop();
            for (int i = 0; i < 4; i++)
            {
                if(!TryGetNeighborFromDirection(i, currentNode, out Node neighbour) || neighbour.IsCollapsed)
                    continue;
                
                foreach (TileWFC tile in currentNode.possibleTiles)
                {
                    if(!possibleSockets.Contains(tile.GetSocket(i)))
                        possibleSockets.Add(tile.GetSocket(i));
                }

                neighbourTiles = new(neighbour.possibleTiles);
                foreach (TileWFC tile in neighbourTiles)
                {
                    int neighbourSocket = tile.GetSocket(GetOppositeSide(i));
                    if(!possibleSockets.Contains(neighbourSocket))
                        neighbour.possibleTiles.Remove(tile);
                }

                if(neighbourTiles.Count != neighbour.possibleTiles.Count)
                {
                    nodesToCollapse.SortUp(neighbour);
                    nodesStack.Push(neighbour);
                }

                possibleSockets.Clear();
            }
        }
    }

    int GetOppositeSide(int side) => side + 2 < 4 ? side + 2 : side - 2;

    void OnDrawGizmos()
    {
        Gizmos.DrawLine(new(-(gridSizeX / 2), gridSizeY / 2, 0), new(gridSizeX / 2, gridSizeY / 2, 0));
        Gizmos.DrawLine(new(-(gridSizeX / 2), -(gridSizeY / 2), 0), new(gridSizeX / 2, -(gridSizeY / 2), 0));
        Gizmos.DrawLine(new(-(gridSizeX / 2), -(gridSizeY / 2), 0), new(-(gridSizeX / 2), gridSizeY / 2, 0));
        Gizmos.DrawLine(new(gridSizeX / 2, -(gridSizeY / 2), 0), new(gridSizeX / 2, gridSizeY / 2, 0));
    }
}
