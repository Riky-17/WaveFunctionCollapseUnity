# Multithreaded Wave Function Collapse Algorithm in Unity
![WFC Generation](./Images/WFC-Generation.gif)

## Table of Content
[What is Wave Function Collapse?](#what-is-wave-function-collapse)

[Multithreading Wave Function Collapse](#multithreading-wave-function-collapse)

[My Approach](#my-approach)

## What is Wave Function Collapse?
The Wave Function Collapse (WFC) is an algorithm used to generate worlds by following a set of rules. The algorithm starts with a grid of nodes. Every node, initially, can be any type of tile, the amount of possible tiles that each node can be is referred to as its entropy.

As an example, here we have 3x3 grid.

![WFC Example 1](./Images/WFCExample1.png)

Each node can be any of the four tiles shown above the grid, so, every node of the grid currently has an entropy of 4. The algorithm starts by picking the node with least entropy, in this case it will pick the first node of the grid, the one on the bottom left.\
The node is then collapsed into one of its possible tiles.

![WFC Example 2](./Images/WFCExample2.png)

After collapsing the selected node, the algorithm goes to the propagation part, where the neighboring nodes detect the change of the collapsed node, and reduce their entropy by removing the possible tiles that cannot connect to the collapsed node's tile. In the example's case, since the selected node was collapsed into the tile with the horizontal path, the node above has removed the tiles with a vertical path as they don't connect to the collapsed node, and the node to the right has removed the tiles that don't have a horizontal path. The propagation phase doesn't end here, the nodes will also detect if their neighbor have had a change in their entropy, and if they did, they will lower their entropy by removing the possible tiles that don't connect with the possible tiles of the neighbor.

The final result of the first iteration will look something like this.

![WFC Example 3](./Images/WFCExample3.png)

From here the algorithm will select another node with the lowest entropy, collapse it, propagate the change to the neighboring nodes, and will continue to do this until the entire grid has been collapsed.

## Multithreading Wave Function Collapse

Since each iteration's result is dependent on the previous one, it can be quite tricky to implement multithreading in the Wave Function Collapse algorithm without the risk of producing conflicting results. While researching some possible solutions, I came across this approach described by BorisTheBrave in [Infinite Modifying In Blocks](https://www.boristhebrave.com/2021/11/08/infinite-modifying-in-blocks/). This approach divides the grid into chunks that are separated from each other by a gap, these chunks will each collapse in parallel. 

![WFC Boris' Approach First Layer](./Images/WFCBorisApproach1Layer.png)

Once they are all done, the next layer will move the chunks in one direction along the gap by half of the size of the chunk, this results in each chunk of the new layer now overlapping with two of the chunks of the previous layer, the chunks will then uncollapse the overlapping nodes and then collapse each node. As a result one of the gaps between the chunks of the first layer will be collapsed connecting the two chunks. 

![WFC Boris' Approach Second Layer](./Images/WFCBorisApproach2Layer.png) 

This process will be repeated 2 more times so each node of the entire grid will be collapsed at least once. 

![WFC Boris' Approach Third Layer](./Images/WFCBorisApproach3Layer.png) 

![WFC Boris' Approach Fourth Layer](./Images/WFCBorisApproach4Layer.png) 

![WFC Boris' Approach Final Result](./Images/WFCBorisApproachResult.png)

The strongest benefit from this approach is that the amount of work to do is always the same, if the algorithm assigns each chunk to a thread group, then each thread group has to collapse its chunk 4 times before the entire grid is finished.\
The biggest drawback is that, because of the layers and the overlapping chunks, the algorithm is essentially doing a significant amount of work that is later undone by a subsequent layer.

## My Approach

Because of this wasted work, I tried to modify the approach in a way that the algorithm would avoid doing work that would later be discarded.\
My approach still divides the grid into chunks separated by a gap. The first pass collapses each chunk normally. Then, in the next pass, the algorithm shifts the chunks upward by the size of the gap, so that they just cover the gap connecting the two chunks of the first pass, and overlapping only the one chunk of their previous layer.\
Here in BorisTheBrave's approach the new chunk in the new layer would overlap two chunks of the old layer, in my approach each new chunk only overlaps one chunk.\
Another difference is that the algorithm doesn't uncollapse the overlapping nodes, instead, the algorithm will only need to collapse the nodes that have not already been collapsed.\
As a result, the gap connecting the two chunks in the first pass will now be collapsed.\
the algorithm will repeat this process two more times by switching the chunk to the right on the third pass and downward on the fourth and final pass.

The result is a multithreaded WFC approach that aims to minimize redundant work while still allowing the chunks to be processed in parallel.

## Code Explanation

The first thing to do is to get the compatibility of the tiles to each other. We do that in the Awake method. [Here](https://github.com/Riky-17/WaveFunctionCollapseUnity/blob/b4c4e5b720a9f1379be8d75cb2a6fbe6fdcb80f8/WaveFunctionCollapse/Assets/Scripts/WFC/WFC.cs#L124-L146) is the full reference.

* Initialize the uint array
    - the idea is an array that takes the index of the tile + the direction of the neighbour as input, and it will output the compatible tiles that the tile can have in that direction.\
    For this reason the size of the array is the amount of tiles multiplied by the amount of directions each tile can be connected to.

```C#

    compat = new uint[tiles.Count * 4];

```

* Populate the array
    - We iterate through the tiles list.
    - We iterate through each direction the tiles can be connected to.
        - here we have a directions array, a simple Vector2D array holding 4 directions. It starts upward, and goes around clockwise.
    - We iterate through the tiles list again.
        - These will be the tiles that we use to see if they can be connected with the tile we got in the first for loop.
    - We compare tile A's socket that faces the current direction with tile B's socket that faces the opposite direction.
        - The tiles sockets are int fields, if the sockets hold the same value, that means they connect with each other.
    - we mark the connection by setting a bit in the j position, where j is the index of tile B in the tiles list. By doing so we have a bitmask holding information of the tiles that can connect to tile A

```

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

```
