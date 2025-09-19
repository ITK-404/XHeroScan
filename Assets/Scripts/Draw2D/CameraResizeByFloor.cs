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


    public void Resize(List<Vector2> checkPoints)
    {
        moveTween?.Kill();
        sizeTween?.Kill();

        Bounds bounds = new Bounds(checkPoints[0],Vector3.zero);
        foreach (var item in checkPoints)
        {
            bounds.Encapsulate(item);
        }
        Vector3 boundCenter = bounds.center;
        Vector3 correctCenter = new Vector3(boundCenter.x, mainCamera.transform.position.y, boundCenter.y);
        // tỉ lệ màn hình
        Debug.Log("Target Size: " + bounds.ToString());
        float targetSize = GetTargetSize(bounds);
        targetSize = Mathf.Clamp(targetSize, 3, PenManager.MAX_CAMERA_ZOOM);
        
        moveTween = DOVirtual.Float(mainCamera.orthographicSize, targetSize, 0.4f, (x) =>
        {
            mainCamera.orthographicSize = x;
        });
        
        sizeTween = mainCamera.transform.DOMove(correctCenter, 0.4f);
    }
    public void BreakMove()
    {
        moveTween?.Kill();
    }

    public void BreakZoom()
    {
        sizeTween?.Kill();
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
