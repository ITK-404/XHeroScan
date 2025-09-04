using System;
using System.Collections.Generic;
using UnityEngine;

public static class GeoUtil
{
    public static Vector2 Centroid(IList<Vector2> pts)
    {
        if (pts == null || pts.Count < 3)
        {
            if (pts != null && pts.Count > 0)
            {
                Vector2 s = Vector2.zero; foreach (var p in pts) s += p;
                return s / pts.Count; // fallback
            }
            return Vector2.zero;
        }

        double a = 0, cx = 0, cy = 0;
        for (int i = 0, n = pts.Count; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            double cross = (double)p.x * q.y - (double)q.x * p.y;
            a  += cross;
            cx += (p.x + q.x) * cross;
            cy += (p.y + q.y) * cross;
        }
        a *= 0.5;
        if (Math.Abs(a) < 1e-8)
        {
            Vector2 s = Vector2.zero; foreach (var p in pts) s += p; 
            return s / pts.Count;
        }
        cx /= (6.0 * a); cy /= (6.0 * a);
        return new Vector2((float)cx, (float)cy);
    }
}
