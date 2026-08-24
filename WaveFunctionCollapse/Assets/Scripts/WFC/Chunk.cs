using UnityEngine;

public struct Chunk
{
    public Vector2Int startCoord;
    public int edgeSizeX;
    public int edgeSizeY;
    Vector2Int[] passDirections;
    public int passIndex;
    public int chunkSizeX;
    public int chunkSizeY;

    public Chunk(Vector2Int startCoord, int chunkSize, int edgeSize, Vector2Int[] passDirections) : this(startCoord, chunkSize, chunkSize, edgeSize, edgeSize, passDirections) {}

    public Chunk(Vector2Int startCoord, int chunkSizeX, int chunkSizeY, int edgeSizeX, int edgeSizeY, Vector2Int[] passDirections)
    {
        this.startCoord = startCoord;
        this.chunkSizeX = chunkSizeX;
        this.chunkSizeY = chunkSizeY;
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
