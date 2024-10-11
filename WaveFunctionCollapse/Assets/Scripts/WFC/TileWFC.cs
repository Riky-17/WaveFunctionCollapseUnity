using System;
using UnityEngine;

[CreateAssetMenu(fileName = "TileWFC", menuName = "TileWFC")]
public class TileWFC : ScriptableObject
{
    public GameObject Object;

    public int upSocket;
    public int rightSocket;
    public int downSocket;
    public int leftSocket;

    public int GetSocket(int dir) => dir switch
    {
        0 => upSocket,
        1 => rightSocket,
        2 => downSocket,
        3 => leftSocket,
        _ => throw new ArgumentOutOfRangeException()
    };
}
