using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    float speed = 15f;
    float minZoomSize = 15f;
    float maxZoomSize = 40f;
    float currentZoomSize = 15f;

    Camera camera2D;

    void Awake()
    {
        camera2D = GetComponent<Camera>();
        camera2D.orthographicSize = currentZoomSize;
    }

    void LateUpdate()
    {
        Vector2 moveDir = Vector2.zero;

        float scrollDelta = -Input.mouseScrollDelta.y; //upward -1

        currentZoomSize += scrollDelta;
        currentZoomSize = Mathf.Clamp(currentZoomSize, minZoomSize, maxZoomSize);

        camera2D.orthographicSize = currentZoomSize;

        if(Input.GetKey(KeyCode.W))
            moveDir += Vector2.up;
        if(Input.GetKey(KeyCode.S))
            moveDir += Vector2.down;
        if(Input.GetKey(KeyCode.D))
            moveDir += Vector2.right;
        if(Input.GetKey(KeyCode.A))
            moveDir += Vector2.left;

        moveDir = moveDir.normalized;
        transform.position += speed * Time.deltaTime * (Vector3)moveDir;
    }
}
