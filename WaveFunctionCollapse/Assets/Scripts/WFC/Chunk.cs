using UnityEngine;

public struct Chunk
{
    public Vector2Int startCoord;
    public int edgeSizeX;
    public int edgeSizeY;
    Vector2Int[] passDirections;
    public int passIndex;
    public int subChunkSizeX;
    public int subChunkSizeY;

    public Chunk(Vector2Int startCoord, int subChunkSize, int edgeSize, Vector2Int[] passDirections) : this(startCoord, subChunkSize, subChunkSize, edgeSize, edgeSize, passDirections) {}

    public Chunk(Vector2Int startCoord, int subChunkSizeX, int subChunkSizeY, int edgeSizeX, int edgeSizeY, Vector2Int[] passDirections)
    {
        this.startCoord = startCoord;
        this.subChunkSizeX = subChunkSizeX;
        this.subChunkSizeY = subChunkSizeY;
        this.edgeSizeX = edgeSizeX;
        this.edgeSizeY = edgeSizeY;
        this.passDirections = passDirections;
        passIndex = 0;
    }

    public bool UpdatePass()
    {
        if(passDirections == null || passIndex >= passDirections.Length)
            return false;

        Vector2Int passDirection = passDirections[passIndex];
        startCoord = new(startCoord.x + (edgeSizeX * passDirection.x), startCoord.y + (edgeSizeY * passDirection.y));
        passIndex++;
        return true;
    }
}
