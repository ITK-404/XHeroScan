using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CameraResizeByFloor : MonoBehaviour
{
    public static CameraResizeByFloor Instance;
    private Camera mainCamera;
    private Tween moveTween;
    private Tween sizeTween;

    public bool isLandscape = false;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
    }


    public void Resize(Vector2 center, List<Vector2> checkPoints)
    {
        moveTween?.Kill();
        sizeTween?.Kill();

        Vector3 newCenter = new Vector3(center.x, mainCamera.transform.position.y, center.y);
        Bounds bounds = new Bounds();
        foreach (var item in checkPoints)
        {
            bounds.Encapsulate(item);
        }
        // tỉ lệ màn hình
        float targetSize = GetTargetSize(bounds);

        moveTween = DOVirtual.Float(mainCamera.orthographicSize, targetSize, 0.4f, (x) =>
        {
            mainCamera.orthographicSize = x;
        });
        sizeTween = mainCamera.transform.DOMove(newCenter, 0.4f);
    }

    private float GetTargetSize(Bounds bounds)
    {
        // Kiểm tra nên match width hay height theo độ lớn của size X và size Y
        isLandscape = bounds.size.x > bounds.size.y;
        // tỉ lệ màn hình
        float screenAspect = (float)Screen.width / (float)Screen.height;

        float width = bounds.size.x;
        float height = bounds.size.y;
        
        width = width * 0.5f;
        height = height * 0.5f;

        float sizeByHeight = height, sizeByWidth;
        
        if (isLandscape)
        {
            sizeByWidth = width / screenAspect;
        }
        else
        {
            sizeByWidth = width * screenAspect;
        }
        float offsetRatio = isLandscape ? 1.15f : 1.40f;
        float targetSize = Mathf.Max(sizeByHeight, sizeByWidth) * offsetRatio;

        return targetSize;
    }
}
