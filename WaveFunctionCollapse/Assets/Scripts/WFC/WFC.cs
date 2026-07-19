using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEngine.Rendering;
using System;

public struct ProgressData
{
    public int doneFlag;
    public int collapsedNodes;
}

public struct Chunk
{
    public Vector2Int startCoord;
    public int edgeSize;
    Vector2Int[] passDirections;
    public int passIndex;

    public Chunk(Vector2Int startCoord, int edgeSize, Vector2Int[] passDirections)
    {
        this.startCoord = startCoord;
        this.edgeSize = edgeSize;
        this.passDirections = passDirections;
        passIndex = 0;
    }

    public bool UpdatePass()
    {
        if(passIndex >= passDirections.Length)
            return false;

        Vector2Int passDirection = passDirections[passIndex];
        startCoord = new(startCoord.x + (edgeSize * passDirection.x), startCoord.y + (edgeSize * passDirection.y));
        passIndex++;
        return true;
    }
}

public class WFC : MonoBehaviour
{

    [SerializeField] ComputeShader NodeRelaxation;

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
    NodeInfo[] collapsedNodes;
    ProgressData[] progress;
    int[] dispatchCounter;

    const int SubChunkSize = 16;
    [SerializeField] int edgeSize = 4;
    int TotalChunkSize => SubChunkSize + edgeSize;
    int totalChunksX;
    int totalChunksY;

    int nodePropagationKernel;
    int collapseKernel;
    int updateGridKernel;
    int gridDoneKernel;

    List<Chunk> chunks;
    Vector2Int[] startCoords;
    Vector2Int[] PassDirections = new Vector2Int[3]
    {
      new Vector2Int(0, 1),
      new Vector2Int(1, 0),
      new Vector2Int(0, -1),
    };

    ComputeBuffer startCoordsBuff;
    ComputeBuffer gridCurrentBuff;
    ComputeBuffer gridNextBuff;
    ComputeBuffer compatBuff;
    ComputeBuffer indexesToCollapseBuff;
    ComputeBuffer progressDataBuff;
    ComputeBuffer collapsedNodesBuff;
    ComputeBuffer dispatchCounterBuff;

    bool progressBuffDone = false;
    bool collapsedBuffDone = false;

    int dispatchIterations = 32;

    public float timeToGenerate;

    Stopwatch sw;

    readonly List<Vector2Int> directions = new()
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0)
    };

    void Awake()
    {
        GetTilesCompat();
    }

    void OnDisable()
    {
        startCoordsBuff?.Release();
        gridCurrentBuff?.Release();
        gridNextBuff?.Release();
        compatBuff?.Release();
        indexesToCollapseBuff?.Release();
        progressDataBuff?.Release();
        collapsedNodesBuff?.Release();
        dispatchCounterBuff?.Release();
    }

    public void WaveFunctionCollapse()
    {
        sw = new();
        sw.Start();
        GetTilesCompat();
        CreateGrid();
        InitComputeShader();
        WaveFunctionCollapseIteration();
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

        totalChunksX = chunksAmountX + extraChunkX;
        totalChunksY = chunksAmountY + extraChunkY;

        startCoords = new Vector2Int[totalChunksX * totalChunksY];
        chunks = new();

        for (int x = 0; x < chunksAmountX; x++)
        {
            for (int y = 0; y < chunksAmountY; y++)
            {
                Vector2Int startCoord = new(x * TotalChunkSize, y * TotalChunkSize);
                Chunk chunk = new(startCoord, edgeSize, PassDirections);
                chunks.Add(chunk);
                startCoords[x * totalChunksY + y] = startCoord;
            }
        }

        // Vector2Int[] chunksDirections = new Vector2Int[]
        // {
        //     new(0, 1),
        //     new(1, 0),
        //     new(0, -1)
        // };

        // Vector2Int startCoord;

        // for (int x = 0; x < chunksAmountX; x++)
        // {
        //     for (int y = 0; y < chunksAmountY; y++)
        //     {
        //         startCoord = new(x * TotalChunkSize, y * TotalChunkSize);
        //         heaps[x * totalChunksY + y] = new(SubChunkSize * SubChunkSize, chunksDirections, startCoord);
        //     }

        //     if (extraChunkY > 0)
        //     {
        //         startCoord = new(x * TotalChunkSize, chunksAmountY * TotalChunkSize);
        //         heaps[x * totalChunksY + totalChunksY - 1] = new(leftoverY * SubChunkSize, new Vector2Int[] { new(1, 0) }, startCoord);
        //     }
        // }

        // if(extraChunkX > 0)
        // {
        //     for (int y = 0; y < chunksAmountY; y++)
        //     {
        //         startCoord = new(chunksAmountX * TotalChunkSize, y * TotalChunkSize);
        //         heaps[(totalChunksX - 1) * totalChunksY + y] = new(leftoverX * SubChunkSize, new Vector2Int[] { new(0, 1) }, startCoord);
        //     }
        // }

        // if (extraChunkX > 0 && extraChunkY > 0)
        // {
        //     heaps[^1] = new(leftoverX * leftoverY);
        // }

        int totalNodes = NodesAmountX * NodesAmountY;

        grid = new Node[NodesAmountX, NodesAmountY];
        gridCurrent = new NodeInfo[totalNodes];
        gridNext = new NodeInfo[totalNodes];
        uint allTiles = 0;

        for (int i = 0; i < tiles.Count; i++)
            allTiles |= (uint)(1 << i);

        Vector2 bottomLeft = new(-(gridSizeX / 2), -(gridSizeY / 2));

        for (int x = 0; x < NodesAmountX; x++)
        {
            for (int y = 0; y < NodesAmountY; y++)
            {
                float xPos = nodeRadius + NodeDiameter * x;
                float yPos = nodeRadius + NodeDiameter * y;
                Vector2 nodePos = new Vector2(xPos, yPos) + bottomLeft;
                NodeInfo nodeInfo = new(x, y, allTiles);

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

                if(x % TotalChunkSize < SubChunkSize && y % TotalChunkSize < SubChunkSize)
                {
                    int chunkX = Mathf.FloorToInt(x / TotalChunkSize);
                    int chunkY = Mathf.FloorToInt(y / TotalChunkSize);
                    int chunkIndex = chunkX * (chunksAmountY + extraChunkY) + chunkY;
                    node.chunkIndex = chunkIndex;
                }

                grid[x, y] = node;
                gridCurrent[x * NodesAmountY + y] = nodeInfo;
            }
        }
    }

    void InitComputeShader()
    {
        nodePropagationKernel = NodeRelaxation.FindKernel("NodeRelaxation");
        collapseKernel = NodeRelaxation.FindKernel("Collapse");
        updateGridKernel = NodeRelaxation.FindKernel("UpdateGrid");
        gridDoneKernel = NodeRelaxation.FindKernel("GridDone");

        var nodeSize = sizeof(uint) * 2 + sizeof(int) * 4;

        startCoordsBuff = new(startCoords.Length, sizeof(int) * 2);
        startCoordsBuff.SetData(startCoords);
        NodeRelaxation.SetBuffer(collapseKernel, "startCoords", startCoordsBuff);
        NodeRelaxation.SetBuffer(gridDoneKernel, "startCoords", startCoordsBuff);

        gridCurrentBuff = new(gridCurrent.Length, nodeSize);
        gridCurrentBuff.SetData(gridCurrent);
        NodeRelaxation.SetBuffer(collapseKernel, "gridCurrent", gridCurrentBuff);
        NodeRelaxation.SetBuffer(nodePropagationKernel, "gridCurrent", gridCurrentBuff);
        NodeRelaxation.SetBuffer(updateGridKernel, "gridCurrent", gridCurrentBuff);
        NodeRelaxation.SetBuffer(gridDoneKernel, "gridCurrent", gridCurrentBuff);

        gridNextBuff = new(gridNext.Length, nodeSize);
        gridNextBuff.SetData(gridNext);
        NodeRelaxation.SetBuffer(nodePropagationKernel, "gridNext", gridNextBuff);
        NodeRelaxation.SetBuffer(updateGridKernel, "gridNext", gridNextBuff);

        NodeRelaxation.SetInt("dispatchIterations", dispatchIterations);

        collapsedNodes = new NodeInfo[totalChunksX * totalChunksY * dispatchIterations];
        collapsedNodesBuff = new(collapsedNodes.Length, nodeSize);
        collapsedNodesBuff.SetData(collapsedNodes);
        NodeRelaxation.SetBuffer(collapseKernel, "collapsedNodes", collapsedNodesBuff);

        dispatchCounter = new int[1];
        dispatchCounterBuff = new(dispatchCounter.Length, sizeof(int));
        dispatchCounterBuff.SetData(dispatchCounter);
        NodeRelaxation.SetBuffer(collapseKernel, "dispatchCounter", dispatchCounterBuff);

        compatBuff = new(compat.Length, sizeof(uint));
        compatBuff.SetData(compat);
        NodeRelaxation.SetBuffer(nodePropagationKernel, "compat", compatBuff);

        NodeRelaxation.SetInt("tilesCount", tiles.Count);

        NodeRelaxation.SetInt("gridSizeX", NodesAmountX);
        NodeRelaxation.SetInt("gridSizeY", NodesAmountY);

        NodeRelaxation.SetInt("groupsAmountY", totalChunksY);
        NodeRelaxation.SetInt("edgeSize", edgeSize);

        indexesToCollapseBuff = new(totalChunksX * totalChunksY, sizeof(int));
        NodeRelaxation.SetBuffer(collapseKernel, "indexesToCollapse", indexesToCollapseBuff);

        NodeRelaxation.SetInt("seed", UnityEngine.Random.Range(0, 300000));

        progress = new ProgressData[1];
        progressDataBuff = new(progress.Length, sizeof(int) * 2);
        progressDataBuff.SetData(progress);
        NodeRelaxation.SetBuffer(collapseKernel, "progressData", progressDataBuff);
        NodeRelaxation.SetBuffer(gridDoneKernel, "progressData", progressDataBuff);
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

    void WaveFunctionCollapseIteration()
    {
        for (int i = 0; i < dispatchIterations; i++)
        {
            NodeRelaxation.Dispatch(collapseKernel, totalChunksX, totalChunksY, 1);
            NodeRelaxation.Dispatch(nodePropagationKernel, NodesAmountX, NodesAmountY, 1);
            NodeRelaxation.Dispatch(updateGridKernel, NodesAmountX, NodesAmountY, 1);
        }

        NodeRelaxation.Dispatch(gridDoneKernel, totalChunksX, totalChunksY, 1);

        AsyncGPUReadback.Request(progressDataBuff, request =>
        {
            if(request.hasError)
                Debug.LogError("Error");

            progressBuffDone = true;
            progressDataBuff.GetData(progress);
            TryAnotherDispatch();
        });

        AsyncGPUReadback.Request(collapsedNodesBuff, request =>
        {
            if(request.hasError)
                Debug.LogError("Error");

            collapsedBuffDone = true;
            collapsedNodesBuff.GetData(collapsedNodes);
            TryAnotherDispatch(); 
        });
    }

    void TryAnotherDispatch()
    {
        if (!progressBuffDone || !collapsedBuffDone)
            return;

        progressBuffDone = false;
        collapsedBuffDone = false;
        // if(chunks[0].passIndex == 1)
        //     Debug.Log(progress[0].collapsedNodes);

        foreach(NodeInfo nodeInfo in collapsedNodes)
        {
            if(nodeInfo.entropy < 1)
                continue;

            // if(nodeInfo.x == 0 && nodeInfo.y == 25)
            //     Debug.Log("Hello");
            // Debug.Log(nodeInfo.x + " " + nodeInfo.y + " " + nodeInfo.test);

            grid[nodeInfo.x, nodeInfo.y].UpdateInfo(nodeInfo);
            gridCurrent[nodeInfo.x * NodesAmountY + nodeInfo.y] = nodeInfo;
        }

        if(progress[0].doneFlag == 0)
            UpdatePass();
        else
        {
            progress[0] = new();
            progressDataBuff.SetData(progress);
            NodeRelaxation.SetBuffer(collapseKernel, "progressData", progressDataBuff);
            NodeRelaxation.SetBuffer(gridDoneKernel, "progressData", progressDataBuff);
            dispatchCounter[0] = 0;
            dispatchCounterBuff.SetData(dispatchCounter);
            NodeRelaxation.SetBuffer(collapseKernel, "dispatchCounter", dispatchCounterBuff);
            WaveFunctionCollapseIteration();
        }
    }

    void UpdatePass()
    {
        bool done = true;
        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk chunk = chunks[i];

            if(!chunk.UpdatePass())
                continue;
            
            chunks[i] = chunk;
            done = false;
            Vector2Int startCoord = chunk.startCoord;
            startCoords[i] = startCoord;
            // Debug.Log(startCoord);
            for (int x = startCoord.x; x < startCoord.x + SubChunkSize; x++)
            {
                for (int y = startCoord.y; y < startCoord.y + SubChunkSize; y++)
                {
                    int index = x * NodesAmountY + y;
                    Node node = grid[x, y];
                    node.Reset();
                    
                    // if(node.NodeInfo.x == 0 && node.NodeInfo.y == 25)
                    //     Debug.Log("Hello");
                    gridCurrent[index] = node.NodeInfo;
                }
            }
        }

        if(done)
        {
            EndIt();
            return;
        }

        gridCurrentBuff.SetData(gridCurrent);
        NodeRelaxation.SetBuffer(collapseKernel, "gridCurrent", gridCurrentBuff);
        NodeRelaxation.SetBuffer(nodePropagationKernel, "gridCurrent", gridCurrentBuff);
        NodeRelaxation.SetBuffer(updateGridKernel, "gridCurrent", gridCurrentBuff);
        NodeRelaxation.SetBuffer(gridDoneKernel, "gridCurrent", gridCurrentBuff);

        startCoordsBuff.SetData(startCoords);
        NodeRelaxation.SetBuffer(collapseKernel, "startCoords", startCoordsBuff);
        NodeRelaxation.SetBuffer(gridDoneKernel, "startCoords", startCoordsBuff);
        
        progress[0] = new();
        progressDataBuff.SetData(progress);
        NodeRelaxation.SetBuffer(collapseKernel, "progressData", progressDataBuff);
        NodeRelaxation.SetBuffer(gridDoneKernel, "progressData", progressDataBuff);
        dispatchCounter[0] = 0;
        dispatchCounterBuff.SetData(dispatchCounter);
        NodeRelaxation.SetBuffer(collapseKernel, "dispatchCounter", dispatchCounterBuff);

        NodeRelaxation.Dispatch(nodePropagationKernel, NodesAmountX, NodesAmountY, 1);
        NodeRelaxation.Dispatch(updateGridKernel, NodesAmountX, NodesAmountY, 1);
        NodeRelaxation.Dispatch(nodePropagationKernel, NodesAmountX, NodesAmountY, 1);
        NodeRelaxation.Dispatch(updateGridKernel, NodesAmountX, NodesAmountY, 1);
        AsyncGPUReadback.Request(gridCurrentBuff, request => 
        {
            if(request.hasError)
                Debug.LogError("Error");
            
            WaveFunctionCollapseIteration();
        });
    }

    void EndIt()
    {
        foreach (Node node in grid)
        {
            if(node.NodeInfo.tile != 0)
                CreateTile(node);
        }
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
