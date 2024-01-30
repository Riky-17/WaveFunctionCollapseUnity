using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class WFC : MonoBehaviour
{
    GridWFC gridWFC;
    HashSet<Node> collapsedNodes = new();

    void Awake()
    {
        gridWFC = GetComponent<GridWFC>();
        gridWFC.CreateGrid();
    }

    void Start()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        StartWaveFunctionCollapse();
        sw.Stop();
        UnityEngine.Debug.Log(sw.Elapsed);
    }

    void StartWaveFunctionCollapse()
    {
        List<Node> nodesToCollapse = gridWFC.ConvertArrayToList();
        Node currentNode = null;
        int[] currentNodeSockets;
        List<TileWFC> neighbourTiles;
        List<TileWFC> neighbourTilesCopy;

        while(nodesToCollapse.Count > 0)
        {
            if (currentNode == null)
            {
                currentNode = nodesToCollapse[Random.Range(0, nodesToCollapse.Count)];
            }
            else
            {
                int entropy = 20;
                foreach (Node node in nodesToCollapse)
                {
                    if(node.entropy < entropy)
                        currentNode = node;
                }
            }

            //set the tile of the current node
            currentNode.nodeTile = currentNode.possibleTiles[Random.Range(0, currentNode.possibleTiles.Count)];

            nodesToCollapse.Remove(currentNode);
            collapsedNodes.Add(currentNode);

            if(nodesToCollapse.Count <= 0)
            {
                Instantiate(currentNode.nodeTile, currentNode.nodePos, Quaternion.identity);
                break;
            }

            //get the sockets of the current node
            currentNodeSockets = currentNode.nodeTile.sockets;
            //lower the entropy of neighbour nodes
            //neighbours = gridWFC.getNeighbours(currentNode);
            for (int i = 0; i < currentNodeSockets.Length; i++)
            {
                if(!gridWFC.TryGetNeighborFromDirection(i, currentNode, out Node neighbour))
                    continue;

                int neighbourSocketIndex = i + 2 < currentNodeSockets.Length ? i + 2 : i - 2;
                neighbourTiles = neighbour.possibleTiles;
                neighbourTilesCopy = new(neighbourTiles);

                for (int n = 0; n < neighbourTilesCopy.Count; n++)
                {
                    TileWFC tile = neighbourTilesCopy[n];
                    if (currentNode.nodeTile.sockets[i] != tile.sockets[neighbourSocketIndex])
                        neighbourTiles.Remove(tile);
                }
            }

            Instantiate(currentNode.nodeTile, currentNode.nodePos, Quaternion.identity);
            
        }
    }

    
}
