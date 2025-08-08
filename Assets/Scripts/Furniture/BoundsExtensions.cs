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

        Vector3 extents = bounds.extents;
        Vector3 center = bounds.center;

        Vector3[] offsets = new Vector3[]
        {
            // middle points
            new Vector3(0, 0, extents.z),   
            new Vector3(0, 0, -extents.z),  
            new Vector3(extents.x, 0, 0),   
            new Vector3(-extents.x, 0, 0),  

            // corner points
            new Vector3(extents.x, 0, extents.z),    
            new Vector3(-extents.x, 0, extents.z),   
            new Vector3(extents.x, 0, -extents.z),   
            new Vector3(-extents.x, 0, -extents.z)   
        };

        foreach (var offset in offsets)
        {
            
            points.Add(transform.position + offset);
        }

        return points;
    }
}