using System.Collections.Generic;
using System.Diagnostics;
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

    byte[] compat;

    Node[,] grid;
    public float timeToGenerate;

    Heap<Node> nodesToCollapse;
    readonly Stack<Node> nodesStack = new();

    readonly List<Vector2Int> directions = new()
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0)
    };

    // void Start()
    // {
    //     int a = 1;
    //     int b = a << 1;
    //     UnityEngine.Debug.Log(b);
    // }

    public void WaveFunctionCollapse()
    {
        GetTilesCompat();
        CreateGrid();
        ConvertArrayToHeap();
        Stopwatch sw = new();
        sw.Start();
        StartWaveFunctionCollapse();
        sw.Stop();
        timeToGenerate = sw.ElapsedMilliseconds / 1000f;
    }

    void GetTilesCompat()
    {
        compat = new byte[tiles.Count * 4];

        for (int i = 0; i < tiles.Count; i++)
        {
            TileWFC tile = tiles[i];

            for (int d = 0; d < directions.Count; d++)
            {
                byte compTiles = 0;

                for (int j = 0; j < tiles.Count; j++)
                {
                    TileWFC compTile = tiles[j];

                    if(tile.GetSocket(d) == compTile.GetSocket((d + 2) % directions.Count))
                        compTiles |= (byte)(1 << j);
                }

                compat[i * 4 + d] = compTiles;
            }
        }
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
                NodeInfo nodeInfo = new(x, y, 0b11111111);

                if(x == 0 || x == NodesAmountX - 1 || y == 0 || y == NodesAmountY - 1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (!HasNeighbour(i, x, y))
                        {
                            // foreach (TileWFC tile in tiles)
                            // {
                            //     if(tile.GetSocket(i) > 0)
                            //     node.possibleTiles.Remove(tile);
                            // }
                            // node.collapsedNeighbours++;
                        }
                    }
                }

                Node node = new(nodePos, nodeInfo);
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

    bool TryGetNeighbourFromDirection(int socketDir, Node currentNode, out Node neighbour)
    {
        Vector2Int direction = directions[socketDir];
        int neighbourX = currentNode.NodeInfo.x + direction.x;
        int neighbourY = currentNode.NodeInfo.y + direction.y;

        if(neighbourX < 0 || neighbourX >= NodesAmountX || neighbourY < 0 || neighbourY >= NodesAmountY)
        {
            neighbour = null;
            return false;
        }

        neighbour = grid[neighbourX, neighbourY];
        return true;
    }

    bool HasNeighbour(int dir, int x, int y)
    {
        Vector2Int direction = directions[dir];
        int neighbourX = x + direction.x;
        int neighbourY = y + direction.y;

        if(neighbourX < 0 || neighbourX >= NodesAmountX || neighbourY < 0 || neighbourY >= NodesAmountY)
            return false;

        return true;
    }

    void StartWaveFunctionCollapse()
    {
        Node currentNode;

        while(nodesToCollapse.HeapSize > 0)
        {
            currentNode = nodesToCollapse.RemoveFirst();
            currentNode.Collapse();


        }
































        // Node currentNode;

        // while(nodesToCollapse.HeapSize > 0)
        // {
        //     currentNode = nodesToCollapse.RemoveFirst();

        //     //set the tile of the current node
        //     int tileIndex = currentNode.possibleTiles.Count > 1 ? Random.Range(0, currentNode.possibleTiles.Count) : 0;
        //     currentNode.nodeTile = currentNode.possibleTiles[tileIndex];
        //     currentNode.ReduceEntropy();
        //     Instantiate(currentNode.nodeTile.Object, currentNode.nodePos, Quaternion.identity, transform);
            
        //     for (int i = 0; i < 4; i++)
        //     {
        //         if(TryGetNeighborFromDirection(i, currentNode, out Node neighbour))
        //             neighbour.collapsedNeighbours++;
        //     }

        //     if(nodesToCollapse.HeapSize <= 0)
        //         break;

        //     nodesStack.Push(currentNode);
            
        //     //Propagate the collapse to the neighbours
        //     if(nodesStack.Count > 0)
        //         PropagateCollapse();
            
        // }
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
