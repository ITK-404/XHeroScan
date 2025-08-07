using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GeometryUtils
{
    // 1. Xây graph
    static void BuildGraph(Room room,
                           out Dictionary<Vector2, List<Vector2>> adj,
                           out int edgeCount)
    {
        adj = new Dictionary<Vector2, List<Vector2>>();
        edgeCount = 0;

        foreach (var wall in room.wallLines)
        {
            // if (wall.type != LineType.Wall && !wall.isManualConnection) continue;

            Vector2 a = new Vector2(wall.start.x, wall.start.z);
            Vector2 b = new Vector2(wall.end.x, wall.end.z);
            if (Vector2.Distance(a, b) < 0.001f) continue;

            if (!adj.ContainsKey(a)) adj[a] = new List<Vector2>();
            if (!adj.ContainsKey(b)) adj[b] = new List<Vector2>();

            if (!adj[a].Contains(b))          // tránh thêm trùng
            {
                adj[a].Add(b);
                adj[b].Add(a);
                edgeCount++;
            }
        }
    }

    // 2. Đếm thành phần liên thông (Euler) 
    static int CountComponents(Dictionary<Vector2, List<Vector2>> adj)
    {
        HashSet<Vector2> seen = new();
        int comp = 0;
        foreach (var v in adj.Keys)
        {
            if (seen.Contains(v)) continue;
            comp++;

            var st = new Stack<Vector2>();
            st.Push(v); seen.Add(v);

            while (st.Count > 0)
            {
                var cur = st.Pop();
                foreach (var nb in adj[cur])
                    if (!seen.Contains(nb)) { seen.Add(nb); st.Push(nb); }
            }
        }
        return comp;
    }

    // 3. Đếm nhanh bằng Euler
    public static int CountLoopsInRoom(Room room)
    {
        if (room == null) return 0;
        BuildGraph(room, out var adj, out int E);
        int V = adj.Count;
        int C = CountComponents(adj);
        ListLoopsInRoom(room);
        return Math.Max(E - V + C, 0);
    }

    // 4. Liệt kê tất cả loop và log ra

    public static List<List<Vector2>> ListLoopsInRoom(Room room)
    {
        if (room == null) return new();

        BuildGraph(room, out var adj, out _);

        // dùng thuật toán “face-tracing” đơn giản dựa trên left-hand rule
        HashSet<string> known = new();         // tránh trùng
        List<List<Vector2>> loops = new();
        HashSet<(Vector2, Vector2)> usedDir = new();   // <từ, tới> đã duyệt

        foreach (var v in adj.Keys)
            foreach (var n in adj[v])
            {
                if (usedDir.Contains((v, n))) continue;

                List<Vector2> loop = new() { v };
                Vector2 prev = v, cur = n;

                while (true)
                {
                    loop.Add(cur);
                    usedDir.Add((prev, cur));

                    // tìm “hàng xóm bên trái nhất” (left-hand rule) của cạnh prev→cur
                    Vector2 next = adj[cur]
                        .Where(nb => nb != prev)
                        .OrderBy(nb => LeftTurnAngle(prev, cur, nb))
                        .FirstOrDefault();

                    if (next == default) break;
                    if (next == loop[0])
                    {
                        // khép vòng
                        loop.Add(next);
                        if (IsSimpleLoop(loop) && AddIfNew(loop, known, loops))
                            Debug.Log($"[DEBUG][LOOP] {room.ID}  ⟹  {string.Join(" → ", loop.Select(p => p.ToString()))}");
                        break;
                    }

                    prev = cur;
                    cur = next;

                    // quá dài ⇒ an toàn thoát
                    if (loop.Count > 1000) break;
                }
            }
        Debug.Log($">>> Phòng {room.ID} có {loops.Count} loop được liệt kê.");
        return loops;
    }

    static float LeftTurnAngle(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 v1 = (a - b).normalized;
        Vector2 v2 = (c - b).normalized;
        float angle = Vector2.SignedAngle(v1, v2);
        return (angle < 0) ? angle + 360f : angle; // 0..360 (trái nhỏ nhất)
    }

    static bool IsSimpleLoop(List<Vector2> loop)
    {
        if (loop.Count < 4) return false; // 3 cạnh + quay về đầu
        // bỏ điểm cuối
        var pts = loop.Take(loop.Count - 1).ToList();
        // không trùng đỉnh
        return pts.Distinct().Count() == pts.Count;
    }

    static bool AddIfNew(List<Vector2> rawLoop,
                         HashSet<string> known,
                         List<List<Vector2>> loops)
    {
        // chuẩn hoá: bỏ điểm cuối, xoay sao cho điểm min đầu, lấy 2 chiều
        var loop = rawLoop.Take(rawLoop.Count - 1).ToList();
        Vector2 min = loop.Aggregate((x, y) => (x.x < y.x || (Mathf.Approximately(x.x, y.x) && x.y < y.y)) ? x : y);
        int idx = loop.IndexOf(min);
        var cw = loop.Skip(idx).Concat(loop.Take(idx)).ToList();
        var ccw = cw.AsEnumerable().Reverse().ToList();

        string h1 = string.Join("|", cw);
        string h2 = string.Join("|", ccw);

        string key = string.CompareOrdinal(h1, h2) < 0 ? h1 : h2;
        if (known.Contains(key)) return false;

        known.Add(key);
        loops.Add(loop);
        return true;
    }
    public static bool EdgeInLoop(List<Vector2> loop, Vector2 a, Vector2 b)
    {
        for (int i = 0; i < loop.Count; i++)
        {
            Vector2 p1 = loop[i];
            Vector2 p2 = loop[(i + 1) % loop.Count];

            bool matchForward = Vector2.Distance(p1, a) < 0.01f && Vector2.Distance(p2, b) < 0.01f;
            bool matchReverse = Vector2.Distance(p1, b) < 0.01f && Vector2.Distance(p2, a) < 0.01f;

            if (matchForward || matchReverse)
                return true;
        }
        return false;
    }
    // Overload mới có includeEdge
    public static bool PointInPolygon(Vector2 point, List<Vector2> polygon, bool includeEdge)
    {
        if (!includeEdge)
            return PointInPolygon(point, polygon); // gọi hàm gốc

        // Cho phép điểm nằm trên cạnh cũng được tính là "nằm trong"
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            if (PointOnSegment(point, a, b, 0.001f)) return true;
        }

        return PointInPolygon(point, polygon); // fallback kiểm tra trong polygon
    }

    public static bool PointOnSegment(Vector2 p, Vector2 a, Vector2 b, float eps = 0.001f)
    {
        float lengthAB = Vector2.Distance(a, b);
        float lengthAP = Vector2.Distance(a, p);
        float lengthPB = Vector2.Distance(p, b);
        return Mathf.Abs((lengthAP + lengthPB) - lengthAB) < eps;
    }

    public static bool IsSamePolygonFlexible(List<Vector2> a, List<Vector2> b, float tol = 0.001f)
{
    // ✅ Chuẩn hóa: loại điểm cuối nếu trùng điểm đầu
    if (Vector2.Distance(a[0], a[^1]) < tol) a = a.Take(a.Count - 1).ToList();
    if (Vector2.Distance(b[0], b[^1]) < tol) b = b.Take(b.Count - 1).ToList();

    // ✅ So sánh sau khi đã chuẩn hóa
    if (a.Count != b.Count) return false;
    int n = a.Count;

    for (int dir = 0; dir < 2; dir++) // 0: thuận, 1: ngược
    {
        List<Vector2> bb = (dir == 0) ? b : b.AsEnumerable().Reverse().ToList();

        for (int offset = 0; offset < n; offset++)
        {
            bool match = true;
            for (int i = 0; i < n; i++)
            {
                Vector2 ap = a[i];
                Vector2 bp = bb[(i + offset) % n];
                if (Vector2.Distance(ap, bp) > tol)
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
    }

    return false;
}


    public static Vector2 GetCentroid(List<Vector2> polygon)
    {
        int n = polygon.Count;
        if (n < 3) return Vector2.zero;

        float cx = 0f, cy = 0f;
        float signedArea = 0f;

        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = polygon[i];
            Vector2 p1 = polygon[(i + 1) % n];

            float a = p0.x * p1.y - p1.x * p0.y;
            signedArea += a;
            cx += (p0.x + p1.x) * a;
            cy += (p0.y + p1.y) * a;
        }

        signedArea *= 0.5f;
        if (Mathf.Abs(signedArea) < 1e-6f)
        {
            Vector2 avg = Vector2.zero;
            foreach (var p in polygon)
                avg += p;
            avg /= polygon.Count;
            return avg;
        }

        cx /= (6f * signedArea);
        cy /= (6f * signedArea);

        return new Vector2(cx, cy);
    }

    public static float AbsArea(List<Vector2> poly)
    {
        double area = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            area += (double)poly[j].x * poly[i].y - (double)poly[i].x * poly[j].y;
        return Mathf.Abs((float)(area * 0.5));
    }
    // Ray-casting algorithm
    public static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;
        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            bool intersect = ((pi.y > point.y) != (pj.y > point.y)) &&
                             (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x);
            if (intersect)
                inside = !inside;
        }
        return inside;
    }
    
}
