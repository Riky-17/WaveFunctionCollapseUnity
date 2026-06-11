using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Unity.Profiling;
using UnityEngine.Rendering;

public class WFC : MonoBehaviour
{
    //Profiling
    // static readonly ProfilerMarker CollapseMarker = new("Collapse Marker");
    // static readonly ProfilerMarker DispatchMarker = new("Dispatch Marker");
    // static readonly ProfilerMarker UpdateInfMarker = new("UpdateInfo Marker");


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
    List<Node> collapsedNodes;
    NodeInfo[] gridCurrent;
    NodeInfo[] gridNext;
    Heap<Node> nodesToCollapse;

    int[] changeFlag = new int[1];
    int flag = 0;

    ComputeBuffer directionsBuff;
    ComputeBuffer gridCurrentBuff;
    ComputeBuffer gridNextBuff;
    ComputeBuffer compatBuff;
    ComputeBuffer changeFlagBuff;

    public float timeToGenerate;

    bool updateGrid;
    int dispatchCount;

    bool aDone = false;
    bool bDone = false;
    Stopwatch sw;

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
    }

    void OnDisable()
    {
        directionsBuff?.Release();
        gridCurrentBuff?.Release();
        gridNextBuff?.Release();
        compatBuff?.Release();
        changeFlagBuff?.Release();
    }

    public void WaveFunctionCollapse()
    {
        sw = new();
        sw.Start();
        GetTilesCompat();
        CreateGrid();
        InitComputeShader();
        StartWaveFunctionCollapse();
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

                if(x == 0 || x == NodesAmountX - 1 || y == 0 || y == NodesAmountY - 1)
                {
                    for (int i = 0; i < directions.Count; i++)
                    {
                        if (!HasNeighbour(i, x, y))
                        {
                            uint compTiles = compat[2 * 4 + ((i + 2) % 4)];
                            nodeInfo.possibleTiles &= compTiles;
                        }
                    }

                    int entropy = 0;

                    for (int i = 0; i < tiles.Count; i++)
                        if((nodeInfo.possibleTiles & 1 << i) != 0)
                            entropy++;

                    nodeInfo.entropy = entropy;
                }

                // Debug.Log(x + " " + y + " " + Convert.ToString(nodeInfo.possibleTiles, 2).PadLeft(8, '0'));
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

        directionsBuff = new(directions.Count, sizeof(int) * 2);
        directionsBuff.SetData(directions);
        NodeRelaxation.SetBuffer(kernelIndex, "directions", directionsBuff);

        gridCurrentBuff = new(gridCurrent.Length, sizeof(uint) * 2 + sizeof(int) * 3);
        gridCurrentBuff.SetData(gridCurrent);
        NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

        gridNextBuff = new(gridNext.Length, sizeof(uint) * 2 + sizeof(int) * 3);
        gridNextBuff.SetData(gridNext);
        NodeRelaxation.SetBuffer(kernelIndex, "gridNext", gridNextBuff);

        compatBuff = new(compat.Length, sizeof(uint));
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

        if(IsInRange(neighbourX, 0, NodesAmountX - 1) && IsInRange(neighbourY, 0, NodesAmountY - 1))
            return true;

        return false;
    }

    void StartWaveFunctionCollapse()
    {
        collapsedNodes = new();

        RunIteration();

        // while(nodesToCollapse.HeapSize > 0)
        // {
        //     Collapse();

        //     if (nodesToCollapse.HeapSize == 0)
        //         break;

        //         Dispatch();

        //     if (!updateGrid)
        //         continue;

        //     UpdateInfo();
        // }
    }

    void RunIteration()
    {
        if(nodesToCollapse.HeapSize == 0)
        {
            sw.Stop();
            timeToGenerate = sw.ElapsedMilliseconds / 1000f;
            return;
        }

        Collapse();
        Dispatch();
    }

    private void UpdateInfo()
    {
        Debug.Log(dispatchCount);
        for (int i = 0; i < collapsedNodes.Count; i++)
        {
            Node collapsedNode = collapsedNodes[i];
            NodeInfo collapsedInfo = collapsedNode.NodeInfo;

            for (int j = -dispatchCount; j < dispatchCount; j++)
            {
                int nodeIndex = j + collapsedInfo.x * NodesAmountY + collapsedInfo.y;

                if (!IsInRange(nodeIndex, 0, (NodesAmountX * NodesAmountY) - 1))
                    continue;

                Node node = grid[nodeIndex];
                NodeInfo updatedInfo = gridNext[nodeIndex]; 

                if (node.NodeInfo.tile != 0)
                    continue;


                if (updatedInfo.entropy == node.NodeInfo.entropy)
                    continue;

                node.UpdateInfo(updatedInfo);
                gridCurrent[nodeIndex] = updatedInfo;
                nodesToCollapse.SortUp(node);
            }
        }

        foreach (NodeInfo info in gridCurrent)
        {
            Debug.Log(info.x + " " + info.y + " " + Convert.ToString(info.possibleTiles, 2).PadLeft(8, '0') + " " + Convert.ToString(info.tile, 2).PadLeft(8, '0') + " " + info.entropy);
        }
        updateGrid = false;
        RunIteration();
    }

    private void Dispatch()
    {
        NodeRelaxation.Dispatch(kernelIndex, NodesAmountX, NodesAmountY, 1);

        AsyncGPUReadback.Request(gridNextBuff, request =>
        {
            if(request.hasError)
                return;
            gridNext = request.GetData<NodeInfo>().ToArray();
            aDone = true;
            TryAnotherDispatch();
        });

        AsyncGPUReadback.Request(changeFlagBuff, request =>
        {
            if(request.hasError)
                return;
            flag = request.GetData<int>()[0];
            bDone = true;
            TryAnotherDispatch();
        });
    }

    void TryAnotherDispatch()
    {
        if(!aDone || !bDone)
            return;

        aDone = false;
        bDone = false;

        if(flag == 1)
        {
            updateGrid = true;
            dispatchCount++;

            gridCurrentBuff.SetData(gridNext);
            NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

            changeFlag[0] = 0;
            changeFlagBuff.SetData(changeFlag);
            NodeRelaxation.SetBuffer(kernelIndex, "changeFlag", changeFlagBuff);
            Dispatch();
            return;
        }

        if(updateGrid)
            UpdateInfo();
        else
            RunIteration();
    }

    private void Collapse()
    {
        collapsedNodes.Clear();

        Node currentNode = nodesToCollapse.RemoveFirst();
        currentNode.Collapse();

        CreateTile(currentNode);

        collapsedNodes.Add(currentNode);


        NodeInfo currentNodeInfo = currentNode.NodeInfo;
        gridCurrent[currentNodeInfo.x * NodesAmountY + currentNodeInfo.y] = currentNodeInfo;

        // Node testNode = nodesToCollapse.LookFirst();
        // if(testNode.NodeInfo.x == 9 && testNode.NodeInfo.y == 0)
        // {
        //     Node testNeighbour = grid[9 * NodesAmountY + 1];
        //     Debug.Log(testNeighbour.NodeInfo.entropy + " " + Convert.ToString(testNeighbour.NodeInfo.possibleTiles, 2).PadLeft(8, '0'));
        //     Debug.Log(testNode.NodeInfo.entropy + " " + Convert.ToString(testNode.NodeInfo.possibleTiles, 2).PadLeft(8, '0'));
        //     Debug.Log(Convert.ToString(testNode.NodeInfo.possibleTiles & testNeighbour.NodeInfo.tile, 2).PadLeft(8, '0'));
        // }

        
        while (nodesToCollapse.HeapSize > 0 && nodesToCollapse.LookFirst().NodeInfo.entropy == 1)
        {
            currentNode = nodesToCollapse.RemoveFirst();
            currentNode.Collapse();

            CreateTile(currentNode);

            collapsedNodes.Add(currentNode);

            currentNodeInfo = currentNode.NodeInfo;
            gridCurrent[currentNodeInfo.x * NodesAmountY + currentNodeInfo.y] = currentNodeInfo;
        }

        gridCurrentBuff.SetData(gridCurrent);
        NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

        dispatchCount = 1;
    }

    public bool IsInRange(int x, int min, int max) => x >= min && x <= max;

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
