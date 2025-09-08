using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CameraResizeByFloor : MonoBehaviour
{
    private Camera mainCamera;
    private Tween moveTween;
    private Tween sizeTween;
    public void Resize(Vector2 center, List<Vector2> checkPoints)
    {
        moveTween?.Kill();
        sizeTween?.Kill();
        Debug.Log("Resize camera here");

        Vector3 newCenter = new Vector3(center.x, mainCamera.transform.position.y, center.y);
        Bounds bounds = new Bounds();
        foreach(var item in checkPoints)
        {
            bounds.Encapsulate(item);
        }
        float size = Mathf.Max(bounds.size.x, bounds.size.y);

        //mainCamera.transform.position = newCenter;
        moveTween = DOVirtual.Float(mainCamera.orthographicSize, size + size * 0.1f,0.4f, (x) =>
        {
            mainCamera.orthographicSize = x;
        });
        sizeTween = mainCamera.transform.DOMove(newCenter,0.4f);
    }

    private void Awake()
    {
        mainCamera = Camera.main;
    }

}
