using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEngine.Rendering;
using System;

public class WFC : MonoBehaviour
{
    [SerializeField] ComputeShader NodeRelaxation;

    [SerializeField] List<TileWFC> tiles;

    List<GameObject> createdTiles = new();

    // Grid Fields
    float nodeRadius = .5f;
    float NodeDiameter => nodeRadius * 2;
    public float gridSizeX = 70f;
    public float gridSizeY = 60f;
    public int NodesAmountX => Mathf.RoundToInt(gridSizeX / NodeDiameter);
    public int NodesAmountY => Mathf.RoundToInt(gridSizeY / NodeDiameter);

    // Chunk Fields
    const int SubChunkSize = 16;
    [SerializeField] int edgeSize = 4;
    int TotalChunkSize => SubChunkSize + edgeSize;
    int totalChunksX;
    int totalChunksY;

    // Algo Arrays
    uint[] compat;
    Node[,] grid;
    NodeInfo[] gridCurrent;
    NodeInfo[] collapsedNodes;
    ProgressData[] progressData;

    // Kernels
    int nodePropagationKernel;
    int collapseKernel;
    int updateGridKernel;
    int gridDoneKernel;

    List<Chunk> chunks;
    Vector2Int[] startCoords;
    Vector2Int[] PassDirections = new Vector2Int[3]
    {
      new(0, 1),
      new(1, 0),
      new(0, -1),
    };

    // Buffers
    ComputeBuffer startCoordsBuff;
    ComputeBuffer gridCurrentBuff;
    ComputeBuffer gridNextBuff;
    ComputeBuffer compatBuff;
    ComputeBuffer indexesToCollapseBuff;
    ComputeBuffer progressDataBuff;
    ComputeBuffer collapsedNodesBuff;

    bool progressBuffDone = false;
    bool collapsedBuffDone = false;

    int dispatchIterations = 16;

    float collapsedNodesCount = 0;

    int progress = 0;
    int maxNodesToCollapse;

    Stopwatch sw;

    public event Action<float> OnGridDone;
    public event Action<int> OnProgressUpdate;

    readonly List<Vector2Int> directions = new()
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0)
    };

    void Awake() => GetTilesCompat();

    void OnDisable() => ReleaseBuffers();

    public void DeleteGrid()
    {
        foreach (GameObject tile in createdTiles)
            Destroy(tile);
        
        createdTiles.Clear();
        collapsedNodesCount = 0;

    }

    public void WaveFunctionCollapse()
    {
        if(createdTiles.Count > 0)
            return;

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

        int edgeSizeExtraY = Mathf.Max(0, leftoverY - SubChunkSize);
        int edgeSizeExtraX = Mathf.Max(0, leftoverX - SubChunkSize);

        Vector2Int[] extraPassDirectionY = edgeSizeExtraY > 0 ? PassDirections : new Vector2Int[1] { new (1, 0) };
        Vector2Int[] extraPassDirectionX = edgeSizeExtraX > 0 ? PassDirections : new Vector2Int[1] { new (0, 1) };
        Vector2Int[] extraPassDirectionFinalChunk = new Vector2Int[0];

        if(edgeSizeExtraX > 0 && edgeSizeExtraY > 0)
            extraPassDirectionFinalChunk = PassDirections;
        else if(edgeSizeExtraX > 0)
            extraPassDirectionFinalChunk = new Vector2Int[1] { new (1, 0) };
        else if(edgeSizeExtraY > 0)
            extraPassDirectionFinalChunk = new Vector2Int[1] { new (0, 1) };

        int normalNodesToCollapse = chunksAmountX * chunksAmountY * SubChunkSize * SubChunkSize * 4;

        int minExtraX = Mathf.Min(leftoverX, SubChunkSize);
        int minExtraY = Mathf.Min(leftoverY, SubChunkSize);

        int extraNodesToCOllapseX = extraChunkX * chunksAmountY * minExtraX * SubChunkSize * (extraPassDirectionX.Length + 1);
        int extraNodesToCollapseY = chunksAmountX * extraChunkY * SubChunkSize * minExtraY * (extraPassDirectionY.Length + 1);
        int extraNodesToCollapseFinalChunk = extraChunkX * extraChunkY * minExtraX * minExtraY * (extraPassDirectionFinalChunk.Length + 1);

        maxNodesToCollapse = normalNodesToCollapse + extraNodesToCOllapseX + extraNodesToCollapseY + extraNodesToCollapseFinalChunk;

        for (int x = 0; x < chunksAmountX; x++)
        {
            for (int y = 0; y < chunksAmountY; y++)
            {
                Vector2Int startCoord = new(x * TotalChunkSize, y * TotalChunkSize);
                Chunk chunk = new(startCoord, SubChunkSize, edgeSize, PassDirections);
                chunks.Add(chunk);
                startCoords[x * totalChunksY + y] = startCoord;
            }

            if(extraChunkY > 0)
            {
                Vector2Int startCoord = new(x * TotalChunkSize, chunksAmountY * TotalChunkSize);
                chunks.Add(new(startCoord, SubChunkSize, Mathf.Min(leftoverY, SubChunkSize), edgeSize, edgeSizeExtraY, extraPassDirectionY));
                startCoords[x * totalChunksY + chunksAmountY] = startCoord;
            }
        }

        if(extraChunkX > 0)
        {
            for (int y = 0; y < chunksAmountY; y++)
            {
                Vector2Int startCoord = new(chunksAmountX * TotalChunkSize, y * TotalChunkSize);
                chunks.Add(new(startCoord, Mathf.Min(leftoverX, SubChunkSize), SubChunkSize, edgeSizeExtraX, edgeSize, extraPassDirectionX));
                startCoords[chunksAmountX * totalChunksY + y] = startCoord;
            }
        }

        if(extraChunkX > 0 && extraChunkY > 0)
        {
            Vector2Int startCoord = new(chunksAmountX * TotalChunkSize, chunksAmountY * TotalChunkSize);

            chunks.Add(new(startCoord, Mathf.Min(leftoverX, SubChunkSize), Mathf.Min(leftoverY, SubChunkSize), edgeSizeExtraX, edgeSizeExtraY, extraPassDirectionFinalChunk));
            startCoords[^1] = startCoord;
        }

        int totalNodes = NodesAmountX * NodesAmountY;

        grid = new Node[NodesAmountX, NodesAmountY];
        gridCurrent = new NodeInfo[totalNodes];
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

        gridNextBuff = new(gridCurrent.Length, nodeSize);
        NodeRelaxation.SetBuffer(nodePropagationKernel, "gridNext", gridNextBuff);
        NodeRelaxation.SetBuffer(updateGridKernel, "gridNext", gridNextBuff);

        NodeRelaxation.SetInt("dispatchIterations", dispatchIterations);

        collapsedNodes = new NodeInfo[totalChunksX * totalChunksY * dispatchIterations];
        collapsedNodesBuff = new(collapsedNodes.Length, nodeSize);
        collapsedNodesBuff.SetData(collapsedNodes);
        NodeRelaxation.SetBuffer(collapseKernel, "collapsedNodes", collapsedNodesBuff);

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

        progressData = new ProgressData[totalChunksX * totalChunksY];
        progressDataBuff = new(progressData.Length, sizeof(int) * 2);
        progressDataBuff.SetData(progressData);
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
            NodeRelaxation.SetInt("dispatchCounter", i);
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
            progressDataBuff.GetData(progressData);
            TryAnotherDispatch();
        });

        AsyncGPUReadback.Request(collapsedNodesBuff, request =>
        {
            if(request.hasError)
                Debug.LogError("Error");

            collapsedBuffDone = true;
            collapsedNodes = request.GetData<NodeInfo>().ToArray();
            TryAnotherDispatch(); 
        });
    }

    void TryAnotherDispatch()
    {
        if (!progressBuffDone || !collapsedBuffDone)
            return;

        progressBuffDone = false;
        collapsedBuffDone = false;

        foreach(NodeInfo nodeInfo in collapsedNodes)
        {
            // Debug.Log(nodeInfo.x + " " + nodeInfo.y + " " + nodeInfo.test);
            if(nodeInfo.entropy < 1)
                continue;

            grid[nodeInfo.x, nodeInfo.y].UpdateInfo(nodeInfo);
            gridCurrent[nodeInfo.x * NodesAmountY + nodeInfo.y] = nodeInfo;
            collapsedNodesCount++;
        }

        if(CheckProgress())
            UpdatePass();
        else
        {
            progressData = new ProgressData[totalChunksX * totalChunksY];
            progressDataBuff.SetData(progressData);
            NodeRelaxation.SetBuffer(collapseKernel, "progressData", progressDataBuff);
            NodeRelaxation.SetBuffer(gridDoneKernel, "progressData", progressDataBuff);

            WaveFunctionCollapseIteration();
        }
    }

    bool CheckProgress()
    {
        bool flag = true;
        for (int i = 0; i < progressData.Length; i++)
        {
            // collapsedNodesCount += progressData[i].collapsedNodes;
            if(progressData[i].doneFlag == 1)
                flag = false;
        }
        progress = (int)(collapsedNodesCount / maxNodesToCollapse * 100);
        OnProgressUpdate?.Invoke(progress);
        return flag;
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
            for (int x = startCoord.x; x < startCoord.x + chunk.chunkSizeX; x++)
            {
                for (int y = startCoord.y; y < startCoord.y + chunk.chunkSizeY; y++)
                {
                    int index = x * NodesAmountY + y;
                    Node node = grid[x, y];
                    node.Reset();
                    
                    gridCurrent[index] = node.NodeInfo;
                }
            }
        }

        if(done)
        {
            EndAlgo();
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
        
        progressData = new ProgressData[totalChunksX * totalChunksY];
        progressDataBuff.SetData(progressData);
        NodeRelaxation.SetBuffer(collapseKernel, "progressData", progressDataBuff);
        NodeRelaxation.SetBuffer(gridDoneKernel, "progressData", progressDataBuff);

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

    void EndAlgo()
    {
        gridCurrentBuff.GetData(gridCurrent);
        foreach (NodeInfo nodeInfo in gridCurrent)
        {
            Debug.Log(nodeInfo.x + " " + nodeInfo.y + " " + Convert.ToString(nodeInfo.tile, 2).PadLeft(12, '0') + " " + nodeInfo.test);
        }
        Debug.Log("--------------------");
        foreach (Node node in grid)
        {
            if(node.NodeInfo.tile != 0)
                CreateTile(node);
            else
                Debug.Log(node.NodeInfo.entropy + " " + node.NodeInfo.x + " " + node.NodeInfo.y + " " + node.NodeInfo.possibleTiles + " " + node.NodeInfo.tile);
        }

        
        ReleaseBuffers();
        sw.Stop();
        OnGridDone?.Invoke(sw.ElapsedMilliseconds / 1000f);
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
                createdTiles.Add(inst);
                break;
            }
        }
    }

    void ReleaseBuffers()
    {
        startCoordsBuff?.Release();
        gridCurrentBuff?.Release();
        gridNextBuff?.Release();
        compatBuff?.Release();
        indexesToCollapseBuff?.Release();
        progressDataBuff?.Release();
        collapsedNodesBuff?.Release();
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawLine(new(-(gridSizeX / 2), gridSizeY / 2, 0), new(gridSizeX / 2, gridSizeY / 2, 0));
        Gizmos.DrawLine(new(-(gridSizeX / 2), -(gridSizeY / 2), 0), new(gridSizeX / 2, -(gridSizeY / 2), 0));
        Gizmos.DrawLine(new(-(gridSizeX / 2), -(gridSizeY / 2), 0), new(-(gridSizeX / 2), gridSizeY / 2, 0));
        Gizmos.DrawLine(new(gridSizeX / 2, -(gridSizeY / 2), 0), new(gridSizeX / 2, gridSizeY / 2, 0));
    }
}
