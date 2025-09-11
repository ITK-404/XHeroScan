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

        float aspect = (float)Screen.width / (float)Screen.height;

        // cần cover theo chiều cao
        float sizeByHeight = bounds.size.y * 0.5f;

        // cần cover theo chiều ngang
        float sizeByWidth = (bounds.size.x * 0.5f) / aspect;

        // chọn size lớn hơn để đảm bảo cover đủ
        float targetSize = Mathf.Max(sizeByHeight, sizeByWidth);

        // thêm padding 15%
        targetSize *= 1.15f;

        moveTween = DOVirtual.Float(mainCamera.orthographicSize, targetSize, 0.4f, (x) =>
        {
            mainCamera.orthographicSize = x;
        });
        sizeTween = mainCamera.transform.DOMove(newCenter, 0.4f);
    }

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
    }

}
