# Multithreaded Wave Function Collapse Algorithm in Unity
![WFC Generation](./Images/WFC-Generation.gif)
## What is Wave Function Collapse?
The Wave Function Collapse (WFC) is an algorithm used to generate world by following a set of rules. The algorithm starts with a grid of nodes, every node, initially, can be any type of tile, the amount of possible tiles that each node can be is referred to as its entropy.\
The algorithm will pick the node with the least entropy, and collapse it, which means that it will force the chosen node to pick a possible tile it can be, then there is the propagation part, where the neighboring nodes will see this change, and remove the possible tiles that cannot connect with their collapsed neighbor, and by doing so they will lower their entropy. From here the algorithm will keep on iterating until every node is collapsed.

## Multithreading Wave Function Collapse

Since each iteration's result is dependent on the previous one, it can be quite tricky to implement multithreading in the Wave Function Collapse algorithm without the risk of producing conflicting results. While researching some possible solutions, I came across this approach described by BorisTheBrave in [Infinite Modifying In Blocks](https://www.boristhebrave.com/2021/11/08/infinite-modifying-in-blocks/). This approach divides the grid into chunks that are separated from each other by a gap, these chunks will each collapse in parallel. ![WFC Boris' Approach First Layer](./Images/WFCBorisApproach1Layer.png)

Once they are all done, the next layer will move the chunks in one direction along the gap by half of the size of the chunk, this results in each chunk of the new layer now overlapping with two of the chunks of the previous layer, the chunks will then uncollapse the overlapping nodes and then collapse each node. As a result one of the gaps between the chunks of the first layer will be collapsed connecting the 2 chunks of the first layer. ![WFC Boris' Approach Second Layer](./Images/WFCBorisApproach2Layer.png) 

This process will be repeated 2 more times so each node of the entire grid will be collapsed at least once. ![WFC Boris' Approach Third Layer](./Images/WFCBorisApproach3Layer.png) ![WFC Boris' Approach Fourth Layer](./Images/WFCBorisApproach4Layer.png) ![WFC Boris' Approach Final Result](./Images/WFCBorisApproachResult.png)

The strongest benefit from this approach is that the amount of work to do is always the same, if the algorithm assigns each chunk to a thread group, then each thread group has to collapse its chunk 4 times before the entire grid is finished.\
The biggest drawback is that, because of the layers and the overlapping chunks, the algorithm is essentially doing a significant amount of work that is later undone by a subsequent layer.

## My Approach

Because of this wasted work, I tried to modify the approach in a way that the algorithm would avoid doing work that would later be discarded.\
My approach still divides the grid into chunks separated by a gap. The first pass collapses each chunk normally. Then, in the next pass, the algorithm shifts the chunks upward so that they cover the gaps connecting the two chunks of the first pass.\
Here in BorisTheBrave's approach the new chunk in the new layer would overlap two chunks of the old layer, in my approach each new chunk only overlaps one chunk.\
Another difference is that the algorithm doesn't uncollapse the overlapping nodes, instead, the algorithm will only need to collapse the nodes that have not already been collapsed.\
As a result, the gap connecting the two chunks in the first pass will now be collapsed.\
the algorithm will repeat this process two more times by switching the chunk to the right on the third pass and downward on the fourth and final pass.

The result is a multithreaded WFC approach that aims to minimize redundant work while still allowing the chunks to be processed in parallel.