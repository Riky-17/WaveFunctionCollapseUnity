using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class WFC : MonoBehaviour
{
    [SerializeField] ComputeShader NodeRelaxation;
    int kernelIndex;

    //grid fields
    [SerializeField] List<TileWFC> tiles;

    float nodeRadius = .5f;
    float NodeDiameter => nodeRadius * 2;
    public float gridSizeX = 70f;
    public float gridSizeY = 60f;
    public int NodesAmountX => Mathf.RoundToInt(gridSizeX / NodeDiameter);
    public int NodesAmountY => Mathf.RoundToInt(gridSizeY / NodeDiameter);

    uint[] compat;

    List<Node> grid;
    NodeInfo[] gridCurrent;
    NodeInfo[] gridNext;
    Heap<Node> nodesToCollapse;

    int[] changeFlag = new int[1];

    ComputeBuffer gridCurrentBuff;
    ComputeBuffer gridNextBuff;
    ComputeBuffer changeFlagBuff;

    public float timeToGenerate;

    Stopwatch whileSW;

    bool updateGrid;

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

    void Awake()
    {
        GetTilesCompat();
        whileSW = new();
    }

    public void WaveFunctionCollapse()
    {
        Stopwatch sw = new();
        sw.Start();
        GetTilesCompat();
        CreateGrid();
        InitComputeShader();
        StartWaveFunctionCollapse();
        sw.Stop();
        timeToGenerate = sw.ElapsedMilliseconds / 1000f;
    }

    void GetTilesCompat()
    {
        compat = new uint[tiles.Count * 4];

        for (int i = 0; i < tiles.Count; i++)
        {
            TileWFC tile = tiles[i];

            for (int d = 0; d < directions.Count; d++)
            {
                uint compTiles = 0;

                for (int j = 0; j < tiles.Count; j++)
                {
                    TileWFC compTile = tiles[j];

                    if(tile.GetSocket(d) == compTile.GetSocket((d + 2) % directions.Count))
                        compTiles |= (uint)(1 << j);
                }

                compat[i * 4 + d] = compTiles;
            }
        }
    }

    void CreateGrid()
    {
        int totalNodes = NodesAmountX * NodesAmountY;

        grid = new();
        gridCurrent = new NodeInfo[totalNodes];
        gridNext = new NodeInfo[totalNodes];
        nodesToCollapse = new(totalNodes);

        Vector2 bottomLeft = new(-(gridSizeX / 2), -(gridSizeY / 2));

        for (int x = 0; x < NodesAmountX; x++)
        {
            for (int y = 0; y < NodesAmountY; y++)
            {
                float xPos = nodeRadius + NodeDiameter * x;
                float yPos = nodeRadius + NodeDiameter * y;
                Vector2 nodePos = new Vector2(xPos, yPos) + bottomLeft;
                NodeInfo nodeInfo = new(x, y, 0b11111111);

                // if(x == 0 || x == NodesAmountX - 1 || y == 0 || y == NodesAmountY - 1)
                // {
                //     for (int i = 0; i < 4; i++)
                //     {
                //         if (!HasNeighbour(i, x, y))
                //         {
                //             uint compTiles
                //         }
                //     }
                // }

                Node node = new(nodePos, nodeInfo);
                grid.Add(node);
                gridCurrent[x * NodesAmountY + y] = nodeInfo;
                nodesToCollapse.Add(node);
            }
        }
    }

    void InitComputeShader()
    {
        kernelIndex = NodeRelaxation.FindKernel("NodeRelaxation");

        ComputeBuffer directionsBuff = new(directions.Count, sizeof(int) * 2);
        directionsBuff.SetData(directions);
        NodeRelaxation.SetBuffer(kernelIndex, "directions", directionsBuff);

        gridCurrentBuff = new(gridCurrent.Length, sizeof(uint) * 2 + sizeof(int) * 3);
        gridCurrentBuff.SetData(gridCurrent);
        NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

        gridNextBuff = new(gridNext.Length, sizeof(uint) * 2 + sizeof(int) * 3);
        gridNextBuff.SetData(gridNext);
        NodeRelaxation.SetBuffer(kernelIndex, "gridNext", gridNextBuff);

        ComputeBuffer compatBuff = new(compat.Length, sizeof(uint));
        compatBuff.SetData(compat);
        NodeRelaxation.SetBuffer(kernelIndex, "compat", compatBuff);

        changeFlag[0] = 0;
        changeFlagBuff = new(1, sizeof(int));
        changeFlagBuff.SetData(changeFlag);
        NodeRelaxation.SetBuffer(kernelIndex, "changeFlag", changeFlagBuff);

        NodeRelaxation.SetInt("tilesCount", tiles.Count);

        NodeRelaxation.SetInt("gridSizeX", NodesAmountX);
        NodeRelaxation.SetInt("gridSizeY", NodesAmountY);
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
            CreateTile(currentNode);
            NodeInfo currentNodeInfo = currentNode.NodeInfo;
            gridCurrent[currentNodeInfo.x * NodesAmountY + currentNodeInfo.y] = currentNodeInfo;

            while (nodesToCollapse.HeapSize > 0 && nodesToCollapse.LookFirst().NodeInfo.entropy == 1)
            {
                currentNode = nodesToCollapse.RemoveFirst();
                currentNode.Collapse();
                CreateTile(currentNode);
                currentNodeInfo = currentNode.NodeInfo;
                gridCurrent[currentNodeInfo.x * NodesAmountY + currentNodeInfo.y] = currentNodeInfo;
            }

            if(nodesToCollapse.HeapSize == 0)
                break;

            gridCurrentBuff.SetData(gridCurrent);
            NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

            NodeRelaxation.Dispatch(kernelIndex, NodesAmountX, NodesAmountY, 1);

            gridNextBuff.GetData(gridNext);
            changeFlagBuff.GetData(changeFlag);

            gridCurrentBuff.SetData(gridNext);
            NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

            int flag = changeFlag[0];
            updateGrid = flag == 1;

            while(flag == 1)
            {
                changeFlag[0] = 0;
                changeFlagBuff.SetData(changeFlag);
                NodeRelaxation.SetBuffer(kernelIndex, "changeFlag", changeFlagBuff);

                NodeRelaxation.Dispatch(kernelIndex, NodesAmountX, NodesAmountY, 1);

                gridNextBuff.GetData(gridNext);
                changeFlagBuff.GetData(changeFlag);

                gridCurrentBuff.SetData(gridNext);
                NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

                flag = changeFlag[0];
            }

            if(!updateGrid)
                continue;

            for (int i = 0; i < gridNext.Length; i++)
            {
                Node node = grid[i];

                if(node.NodeInfo.tile != 0)
                    continue;

                NodeInfo updatedInfo = gridNext[i];

                if(updatedInfo.entropy == node.NodeInfo.entropy)
                    continue;

                // Debug.Log(node.NodeInfo.entropy + " " + updatedInfo.entropy);
                // Debug.Log("x: " + updatedInfo.x + " y: " + updatedInfo.y + " Old: " + Convert.ToString(node.NodeInfo.possibleTiles, 2).PadLeft(8, '0') + " New: " + Convert.ToString(updatedInfo.possibleTiles, 2).PadLeft(8, '0'));
                node.UpdateInfo(updatedInfo);
                gridCurrent[i] = updatedInfo;
                nodesToCollapse.SortUp(node);
            }
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

    public void CreateTile(Node node)
    {
        uint tileBit = node.NodeInfo.tile;
        for (int i = 0; i < tiles.Count; i++)
        {
            if ((tileBit & (1 << i)) != 0)
            {
                GameObject tileObj = tiles[i].tile;
                Instantiate(tileObj, node.nodePos, Quaternion.identity);
                break;
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawLine(new(-(gridSizeX / 2), gridSizeY / 2, 0), new(gridSizeX / 2, gridSizeY / 2, 0));
        Gizmos.DrawLine(new(-(gridSizeX / 2), -(gridSizeY / 2), 0), new(gridSizeX / 2, -(gridSizeY / 2), 0));
        Gizmos.DrawLine(new(-(gridSizeX / 2), -(gridSizeY / 2), 0), new(-(gridSizeX / 2), gridSizeY / 2, 0));
        Gizmos.DrawLine(new(gridSizeX / 2, -(gridSizeY / 2), 0), new(gridSizeX / 2, gridSizeY / 2, 0));
    }
}
