using System;
using System.Collections.Generic;
using UnityEngine;

public class CameraResizeByFloor : MonoBehaviour
{
    private Camera mainCamera;

    public void Resize(Vector2 center, List<Vector2> checkPoints)
    {
        Debug.Log("Resize camera here");

        Vector3 newCenter = new Vector3(center.x, mainCamera.transform.position.y, center.y);
        Bounds bounds = new Bounds();
        foreach(var item in checkPoints)
        {
            bounds.Encapsulate(item);
        }
        float size = Mathf.Max(bounds.size.x, bounds.size.y);

        mainCamera.orthographicSize = size + size * 0.1f;
        mainCamera.transform.position = newCenter;
    }

    private void Awake()
    {
        mainCamera = Camera.main;
    }

}
