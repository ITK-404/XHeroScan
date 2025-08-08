using System;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureItem : MonoBehaviour
{
    public FurnitureData data;
    public SpriteRenderer spriteRenderer;
    public FurniturePoint pointPrefab;

    private List<FurniturePoint> pointsList = new();
    public static GameObject pointHolder;

    private void Awake()
    {
        if (pointHolder == null)
        {
            pointHolder = new GameObject("Point Holder");
        }

        for (int i = 0; i < 8; i++)
        {
            var point = Instantiate(pointPrefab, pointHolder.transform);
            pointsList.Add(point);
        }

        RefreshPointPosition();
    }

    private void RefreshPointPosition()
    {
        var list = spriteRenderer.bounds.GetHandlePositions(transform);
        for (int i = 0; i < pointsList.Count; i++)
        {
            pointsList[i].transform.localPosition = list[i];
        }
    }

    private void OnDrawGizmos()
    {
        if (spriteRenderer == null) return;
        Bounds bounds = spriteRenderer.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Vector3 dragSize = new Vector3(0.5f, 0, 0.5f);
        // top middle
        Gizmos.DrawWireCube(bounds.center + new Vector3(0, 0, bounds.size.z / 2), dragSize);
        // bottom middle
        Gizmos.DrawWireCube(bounds.center - new Vector3(0, 0, bounds.size.z / 2), dragSize);
        // right middle
        Gizmos.DrawWireCube(bounds.center + new Vector3(bounds.size.x / 2, 0, 0), dragSize);
        // left middle
        Gizmos.DrawWireCube(bounds.center - new Vector3(bounds.size.x / 2, 0, 0), dragSize);

        // top right
        Gizmos.DrawWireCube(bounds.center + new Vector3(bounds.size.x / 2, 0, bounds.size.z / 2), dragSize);
        // top left
        Gizmos.DrawWireCube(bounds.center + new Vector3(-bounds.size.x / 2, 0, bounds.size.z / 2), dragSize);
        // bottom right
        Gizmos.DrawWireCube(bounds.center + new Vector3(bounds.size.x / 2, 0, -bounds.size.z / 2), dragSize);
        // bottom left
        Gizmos.DrawWireCube(bounds.center + new Vector3(-bounds.size.x / 2, 0, -bounds.size.z / 2), dragSize);
    }

    private void OnMouseDown(Vector3 position)
    {
        var bounds = spriteRenderer.bounds;
        var extend = position - bounds.center;
        var mockPos = bounds.center - extend;
        var center = Vector3.Lerp(position, mockPos, 0.5f);
    }

    private void Update()
    {
    }

    public void ChangeScale(Vector3 deltaScale)
    {
        var bounds = spriteRenderer.bounds;
        var oldScale = transform.localScale;
        var oldPos = transform.localPosition;
        var pivot = bounds.center;
        var newPos = Vector3.zero;
        var finalScale = oldScale + deltaScale;
        newPos.x = pivot.x + (oldPos.x - pivot.x) * (deltaScale.x / oldScale.x);
        newPos.y = pivot.y + (oldPos.y - pivot.y) * (deltaScale.y / oldScale.y);
        transform.localScale = finalScale;
        transform.localPosition = newPos;
    }
}

[Serializable]
public class FurnitureData
{
    public string ItemID = "Item";
    public float Width = 1;
    public float Height = 1;
    public float ObjectHeight = 0.5f;
}