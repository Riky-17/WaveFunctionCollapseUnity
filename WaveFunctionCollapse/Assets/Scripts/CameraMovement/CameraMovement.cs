using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    float speed = 15f;

    void LateUpdate()
    {
        Vector2 moveDir = Vector2.zero;

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
