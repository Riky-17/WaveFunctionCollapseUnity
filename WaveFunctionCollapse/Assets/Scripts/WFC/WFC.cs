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

    Node[,] grid;
    NodeInfo[] gridCurrent;
    NodeInfo[] gridNext;

    [SerializeField] int subChunkSize = 16;
    [SerializeField] int edgeLength = 5;
    int TotalChunkSize => subChunkSize + edgeLength;

    Heap[] heaps; 
    int heapsDone = 0;

    int[] changeFlag = new int[1];
    int flag = 0;

    ComputeBuffer directionsBuff;
    ComputeBuffer gridCurrentBuff;
    ComputeBuffer gridNextBuff;
    ComputeBuffer compatBuff;
    ComputeBuffer changeFlagBuff;

    public float timeToGenerate;

    bool updateGrid;

    bool aDone = false;
    bool bDone = false;

    public static int test = 0;
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

        int chunksAmountX = NodesAmountX / TotalChunkSize;
        int chunksAmountY = NodesAmountY / TotalChunkSize;

        int leftoverX = NodesAmountX % TotalChunkSize;
        int leftoverY = NodesAmountY % TotalChunkSize;

        int extraChunkX = leftoverX != 0 ? 1 : 0;
        int extraChunkY = leftoverY != 0 ? 1 : 0;

        int totalChunksX = chunksAmountX + extraChunkX;
        int totalChunksY = chunksAmountY + extraChunkY;

        heaps = new Heap[(chunksAmountX + extraChunkX) * totalChunksY];
        Vector2Int[] chunksDirections = new Vector2Int[]
        {
            new(0, 1),
            new(1, 0),
            new(0, -1)
        };

        Vector2Int startCoord;

        for (int x = 0; x < chunksAmountX; x++)
        {
            for (int y = 0; y < chunksAmountY; y++)
            {
                startCoord = new(x * TotalChunkSize, y * TotalChunkSize);
                heaps[x * totalChunksY + y] = new(subChunkSize * subChunkSize, chunksDirections, startCoord);
            }

            if (extraChunkY > 0)
            {
                startCoord = new(x * TotalChunkSize, chunksAmountY * TotalChunkSize);
                heaps[x * totalChunksY + totalChunksY - 1] = new(leftoverY * subChunkSize, new Vector2Int[] { new(1, 0) }, startCoord);
            }
        }

        if(extraChunkX > 0)
        {
            for (int y = 0; y < chunksAmountY; y++)
            {
                startCoord = new(chunksAmountX * TotalChunkSize, y * TotalChunkSize);
                heaps[(totalChunksX - 1) * totalChunksY + y] = new(leftoverX * subChunkSize, new Vector2Int[] { new(0, 1) }, startCoord);
            }
        }

        if (extraChunkX > 0 && extraChunkY > 0)
        {
            heaps[^1] = new(leftoverX * leftoverY);
        }

        int totalNodes = NodesAmountX * NodesAmountY;

        grid = new Node[NodesAmountX, NodesAmountY];
        gridCurrent = new NodeInfo[totalNodes];
        gridNext = new NodeInfo[totalNodes];

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

                Node node = new(nodePos, nodeInfo);

                if(x % TotalChunkSize < subChunkSize && y % TotalChunkSize < subChunkSize)
                {
                    int chunkX = Mathf.FloorToInt(x / TotalChunkSize);
                    int chunkY = Mathf.FloorToInt(y / TotalChunkSize);
                    int chunkIndex = chunkX * (chunksAmountY + extraChunkY) + chunkY;
                    node.chunkIndex = chunkIndex;
                    heaps[chunkIndex].Add(node);
                }

                grid[x, y] = node;
                gridCurrent[x * NodesAmountY + y] = nodeInfo;
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
        Collapse();
    }

    private void UpdateInfo()
    {
        for (int i = 0; i < gridNext.Length; i++)
        {
            NodeInfo nodeInfo = gridCurrent[i];
            Node node = grid[nodeInfo.x, nodeInfo.y];

            if(nodeInfo.tile != 0)
                continue;

            NodeInfo updatedInfo = gridNext[i];
            if(test == 3)
                Debug.Log(nodeInfo.x + " " + nodeInfo.y + " " + Convert.ToString(nodeInfo.possibleTiles, 2).PadLeft(8, '0') + " " + Convert.ToString(updatedInfo.possibleTiles, 2).PadLeft(8, '0') + " " + Convert.ToString(node.NodeInfo.possibleTiles, 2).PadLeft(8, '0') + " " + node.chunkIndex);

            if(nodeInfo.possibleTiles == updatedInfo.possibleTiles)
                continue;

            node.UpdateInfo(updatedInfo);
            gridCurrent[i] = updatedInfo;
            if(node.chunkIndex >= 0)
                heaps[node.chunkIndex].SortUp(node);
        }

        updateGrid = false;
        UpdatePass();
    }

    private void Dispatch()
    {
        NodeRelaxation.Dispatch(kernelIndex, NodesAmountX, NodesAmountY, 1);

        AsyncGPUReadback.Request(gridNextBuff, request =>
        {
            if(request.hasError)
                return;
            gridNext = request.GetData<NodeInfo>().ToArray();
            // if (test)
            // {
            //     for (int i = 0; i < gridNext.Length; i++)
            //     {
            //         NodeInfo old = gridCurrent[i];
            //         if(old.tile != 0)
            //             continue;
    
            //         NodeInfo newInfo = gridNext[i];
            //         Debug.Log(old.x + " " + old.y + " " + Convert.ToString(old.possibleTiles, 2).PadLeft(8, '0') + " " + Convert.ToString(newInfo.possibleTiles, 2).PadLeft(8, '0'));
    
            //     }
            // }
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

            gridCurrentBuff.SetData(gridNext);
            NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);

            changeFlagBuff.SetData(changeFlag);
            NodeRelaxation.SetBuffer(kernelIndex, "changeFlag", changeFlagBuff);
            Dispatch();
            return;
        }

        if(updateGrid)
            UpdateInfo();
        else
            UpdatePass();
    }

    private void Collapse()
    {
        for (int i = heaps.Length - 1; i >= 0; i--)
        {
            Heap heap = heaps[i];
            if(heap == null || heap.HeapSize == 0)
                continue;
            CollapseHeap(heap);
        }

        Dispatch();
    }

    private void CollapseHeap(Heap nodesToCollapse)
    {
        Node currentNode = nodesToCollapse.RemoveFirst();
        currentNode.chunkIndex = -1;
        if(currentNode.NodeInfo.possibleTiles == 0)
        {
            Debug.Log("Error At: " + currentNode.NodeInfo.x + " " + currentNode.NodeInfo.y);
            EndIt();
        }

        if(test == 3)
            Debug.Log("collapsing: " + currentNode.NodeInfo.x + " " + currentNode.NodeInfo.y + " " + Convert.ToString(currentNode.NodeInfo.possibleTiles, 2).PadLeft(8, '0') + " " + Convert.ToString(currentNode.NodeInfo.tile).PadLeft(8, '0'));
        currentNode.Collapse();

        NodeInfo currentNodeInfo = currentNode.NodeInfo;
        gridCurrent[currentNodeInfo.x * NodesAmountY + currentNodeInfo.y] = currentNodeInfo;

        while (nodesToCollapse.HeapSize > 0 && nodesToCollapse.LookFirst().NodeInfo.entropy == 1)
        {
            currentNode = nodesToCollapse.RemoveFirst();
            currentNode.chunkIndex = -1;
            if(currentNode.NodeInfo.possibleTiles == 0)
            {
                Debug.Log(currentNode.NodeInfo.x + " " + currentNode.NodeInfo.y);
                EndIt();
            }
            currentNode.Collapse();

            currentNodeInfo = currentNode.NodeInfo;
            gridCurrent[currentNodeInfo.x * NodesAmountY + currentNodeInfo.y] = currentNodeInfo;
        }

        gridCurrentBuff.SetData(gridCurrent);
        NodeRelaxation.SetBuffer(kernelIndex, "gridCurrent", gridCurrentBuff);
    }

    void EndIt()
    {
        foreach (Node node in grid)
        {
            if(node.NodeInfo.tile != 0)
                CreateTile(node);
        }
    }

    void UpdatePass()
    {
        bool done = true;

        foreach (Heap heap in heaps)
        {
            if (heap == null)
                continue;

            if (heap.HeapSize > 0)
            {
                done = false;
                break;
            }
        }

        if (!done)
        {
            Collapse();
            return;
        }
        test++;
        for (int i = heaps.Length - 1; i >= 0; i--)
        {
            Heap heap = heaps[i];
            if (heap == null)
                continue;

            if (heap.IsDone())
            {
                heaps[i] = null;
                heapsDone++;
                continue;
            }

            heap.startCoord += heap.direction * new Vector2Int(edgeLength, edgeLength);
            if(i == 0 && heap != null)
                Debug.Log(heap.startCoord);
            int startX = heap.startCoord.x;
            int startY = heap.startCoord.y;

            for (int x = startX; x < startX + subChunkSize; x++)
            {
                for (int y = startY; y < startY + subChunkSize; y++)
                {
                    Node node = grid[x, y];
                    node.Reset();
                    node.chunkIndex = i;
                    heap.Add(node);
                    gridCurrent[x * NodesAmountY + y] = node.NodeInfo;
                    // Debug.Log(node.NodeInfo.x + " " + node.NodeInfo.y + " " + node.NodeInfo.tile);
                    // Debug.Log(i + " " + node.NodeInfo.x + " " + node.NodeInfo.y + " " + Convert.ToString(node.NodeInfo.possibleTiles, 2).PadLeft(8, '0'));
                }
            }
            heap.directionsIndex++;
        }

        if(heapsDone >= heaps.Length)
        {
            sw.Stop();
            timeToGenerate = sw.ElapsedMilliseconds / 1000f;

            foreach (Node node in grid)
                CreateTile(node);
                
            return;
        }

        gridCurrentBuff.SetData(gridCurrent);

        Dispatch();
    }

    public bool IsInRange(int x, int min, int max) => x >= min && x <= max;

    void CreateTile(Node node)
    {
        uint tileBit = node.NodeInfo.tile;
        for (int i = 0; i < tiles.Count; i++)
        {
            if ((tileBit & (1 << i)) != 0)
            {
                GameObject tileObj = tiles[i].tile;
                GameObject inst = Instantiate(tileObj, node.nodePos, Quaternion.identity);
                inst.name = node.NodeInfo.x + " " + node.NodeInfo.y;
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
