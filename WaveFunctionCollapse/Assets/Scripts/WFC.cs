using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WFC : MonoBehaviour
{
    GridWFC grid;

    void Awake()
    {
        grid = GetComponent<GridWFC>();

    }
}
