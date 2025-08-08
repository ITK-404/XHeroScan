using System.Collections.Generic;
using UnityEngine;

public static class BoundsExtensions
{
    /// <summary>
    /// Lấy các điểm handle (8 điểm: 4 cạnh, 4 góc) của bounds với rotation
    /// </summary>
    public static List<Vector3> GetHandlePositions(this Bounds bounds, Transform transform)
    {
        List<Vector3> points = new List<Vector3>();

        Vector3 extents = bounds.extents; // half size
        Vector3 center = bounds.center;

        // Các offset trong local space (theo X-Z, giữ nguyên Y)
        Vector3[] offsets = new Vector3[]
        {
            // middle points
            new Vector3(0, 0, extents.z),   // top middle
            new Vector3(0, 0, -extents.z),  // bottom middle
            new Vector3(extents.x, 0, 0),   // right middle
            new Vector3(-extents.x, 0, 0),  // left middle

            // corner points
            new Vector3(extents.x, 0, extents.z),    // top right
            new Vector3(-extents.x, 0, extents.z),   // top left
            new Vector3(extents.x, 0, -extents.z),   // bottom right
            new Vector3(-extents.x, 0, -extents.z)   // bottom left
        };

        foreach (var offset in offsets)
        {
            // local point → world point
            Vector3 localPoint = transform.InverseTransformPoint(center) + offset;
            points.Add(transform.TransformPoint(localPoint));
            // points.Add(transform.position + offset);
        }

        return points;
    }
}