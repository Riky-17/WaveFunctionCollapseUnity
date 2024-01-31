using System.Collections.Generic;
using UnityEngine;

public class WFC : MonoBehaviour
{
    GridWFC gridWFC;
    Heap<Node> nodesToCollapse;
    int[] currentNodeSockets;
    List<TileWFC> neighbourTiles;
    List<TileWFC> neighbourTilesCopy;

    void Awake()
    {
        gridWFC = GetComponent<GridWFC>();
        gridWFC.CreateGrid();
        nodesToCollapse = gridWFC.ConvertArrayToHeap();
    }

    void Start()
    {
        StartWaveFunctionCollapse();
    }

    void StartWaveFunctionCollapse()
    {
        Node currentNode;

        while(nodesToCollapse.HeapSize > 0)
        {
            currentNode = nodesToCollapse.RemoveFirst();
            //set the tile of the current node
            int index = Random.Range(0, currentNode.possibleTiles.Count);
            currentNode.nodeTile = currentNode.possibleTiles[index];

            if(nodesToCollapse.HeapSize <= 0)
            {
                Instantiate(currentNode.nodeTile, currentNode.nodePos, Quaternion.identity);
                break;
            }

            //get the sockets of the current node
            currentNodeSockets = currentNode.nodeTile.sockets;
            //lower the entropy of neighbour nodes
            for (int i = 0; i < currentNodeSockets.Length; i++)
            {
                if(!gridWFC.TryGetNeighborFromDirection(i, currentNode, out Node neighbour) || neighbour.isCollapsed)
                    continue;
                //get the socket of the neighbour that is facing the oppist was from the on of the current node
                int neighbourSocketIndex = i + 2 < currentNodeSockets.Length ? i + 2 : i - 2;
                neighbourTiles = neighbour.possibleTiles;
                neighbourTilesCopy = new(neighbourTiles);

                for (int n = 0; n < neighbourTilesCopy.Count; n++)
                {
                    TileWFC tile = neighbourTilesCopy[n];
                    if (currentNode.nodeTile.sockets[i] != tile.sockets[neighbourSocketIndex])
                        neighbourTiles.Remove(tile);

                    if(neighbourTilesCopy != neighbourTiles)
                        nodesToCollapse.SortUp(neighbour);
                }
            }

            Instantiate(currentNode.nodeTile, currentNode.nodePos, Quaternion.identity);   
        }
    }
}
