using System.Collections.Generic;
using UnityEngine;


public class WFC : MonoBehaviour
{
    GridWFC gridWFC;
    Heap<Node> nodesToCollapse;
    int[] currentNodeSockets;
    List<TileWFC> neighbourTiles;
    Queue<Node> collapsedNeighbours = new();

    void Awake()
    {
        gridWFC = GetComponent<GridWFC>();
        gridWFC.CreateGrid();
        nodesToCollapse = gridWFC.ConvertArrayToHeap();
    }

    void Start() => StartWaveFunctionCollapse();

    void StartWaveFunctionCollapse()
    {
        Node currentNode;

        while(nodesToCollapse.HeapSize > 0)
        {
            currentNode = nodesToCollapse.RemoveFirst();
            //set the tile of the current node
            int tileIndex = currentNode.possibleTiles.Count > 1 ? Random.Range(0, currentNode.possibleTiles.Count) : 0;
            currentNode.nodeTile = currentNode.possibleTiles[tileIndex];

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
                int neighbourSocketIndex = GetOppositeSide(i);
                neighbourTiles = new(neighbour.possibleTiles);

                for (int n = 0; n < neighbourTiles.Count; n++)
                {
                    TileWFC tile = neighbourTiles[n];
                    if (currentNode.nodeTile.sockets[i] != tile.sockets[neighbourSocketIndex])
                        neighbour.possibleTiles.Remove(tile);

                    if(neighbour.possibleTiles.Count != neighbourTiles.Count)
                    {
                        nodesToCollapse.SortUp(neighbour);
                        collapsedNeighbours.Enqueue(neighbour);
                    }
                }
            }
            Instantiate(currentNode.nodeTile, currentNode.nodePos, Quaternion.identity);
            
            //Lower the entropy of the neighbour nodes of the neighbour
            if(collapsedNeighbours.Count > 0)
                CollapseNeighboursNeighbour();
            
        }
    }

    void CollapseNeighboursNeighbour()
    {
        List<int> possibleSockets = new();
        while (collapsedNeighbours.Count > 0)
        {
            Node currentNode = collapsedNeighbours.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                if(gridWFC.TryGetNeighborFromDirection(i, currentNode, out Node neighbour))
                {
                    if (neighbour.isCollapsed)
                        continue;
                }
                else 
                    continue;

                foreach (TileWFC tile in currentNode.possibleTiles)
                {
                    if(possibleSockets.Contains(tile.sockets[i]))
                        continue;
                    possibleSockets.Add(tile.sockets[i]);
                }

                int neighbourSocketIndex = GetOppositeSide(i);
                neighbourTiles = new(neighbour.possibleTiles); 

                foreach (TileWFC tile in neighbourTiles)
                {
                    if(!possibleSockets.Contains(tile.sockets[neighbourSocketIndex]))
                        neighbour.possibleTiles.Remove(tile);
                }

                if(neighbour.possibleTiles.Count != neighbourTiles.Count)
                {
                    nodesToCollapse.SortUp(neighbour);
                    if(!collapsedNeighbours.Contains(neighbour))
                        collapsedNeighbours.Enqueue(neighbour);
                }
            }
        }
    }

    int GetOppositeSide(int side) => side + 2 < 4 ? side + 2 : side - 2;
}
