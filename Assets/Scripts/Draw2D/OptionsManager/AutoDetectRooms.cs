using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomLoopDetector
{
    public static void DetectAndUpdateRooms(SplitRoomManager splitRoomManager)
    {
        // Lấy toàn bộ wallLines hiện tại
        List<WallLine> allWallLines = new List<WallLine>();
        foreach (var room in RoomStorage.GetAllRooms())
            allWallLines.AddRange(room.wallLines);

        DetectAndUpdateRoomsInternal(splitRoomManager, allWallLines);
    }

    public static void DetectAndUpdateRoomsInternal(SplitRoomManager splitRoomManager, List<WallLine> allWallLines)
    {
        // Lưu room cũ
        Dictionary<string, Room> oldRooms = new();
        foreach (var room in RoomStorage.GetAllRooms())
        {
            string key = EdgeKey(SimplifyLoop(room.checkpoints));
            if (!oldRooms.ContainsKey(key))
                oldRooms[key] = room;
        }

        // Gom edges
        Dictionary<Vector2, HashSet<Vector2>> graph = new();
        foreach (var w in allWallLines)
        {
            Vector2 a = new(w.start.x, w.start.z);
            Vector2 b = new(w.end.x, w.end.z);
            if (Vector2.Distance(a, b) < 0.001f) continue;

            if (!graph.ContainsKey(a)) graph[a] = new HashSet<Vector2>();
            if (!graph.ContainsKey(b)) graph[b] = new HashSet<Vector2>();

            graph[a].Add(b);
            graph[b].Add(a);
        }

        // Tìm loops
        const float AREA_EPS = 0.001f;
        const float MIN_AREA = 1.0f; // m² tối thiểu để tránh rác

        List<List<Vector2>> loops = FindAllLoops(graph)
            .Select(lp => SimplifyLoop(lp))
            .Where(lp =>
            {
                float area = Mathf.Abs(PolygonArea(lp));
                return area > AREA_EPS && area >= MIN_AREA;
            })
            .GroupBy(lp => EdgeKey(lp)) // chỉ bỏ trùng polygon hoàn toàn
            .Select(g => g.First())
            .ToList();

        // Loại bỏ loop lồng nhau
        loops = RemoveNestedLoops(loops);

        // Cập nhật RoomStorage
        HashSet<string> newKeys = new();
        List<Room> changedRooms = new();

        foreach (var loop in loops)
        {
            string loopKey = EdgeKey(loop);
            newKeys.Add(loopKey);

            if (!oldRooms.ContainsKey(loopKey))
            {
                Room newRoom = new();
                newRoom.SetID(Guid.NewGuid().ToString());
                newRoom.checkpoints = loop;
                newRoom.wallLines = BuildWallLinesFromLoop(loop);

                RoomStorage.UpdateOrAddRoom(newRoom);
                changedRooms.Add(newRoom);
            }
            else
            {
                var existingRoom = oldRooms[loopKey];
                existingRoom.checkpoints = loop;
                existingRoom.wallLines = BuildWallLinesFromLoop(loop);
                changedRooms.Add(existingRoom);
            }
        }

        // Xóa room không còn
        var toRemove = RoomStorage.GetAllRooms()
            .Where(r => !newKeys.Contains(EdgeKey(SimplifyLoop(r.checkpoints))))
            .ToList();
        foreach (var r in toRemove)
        {
            RemoveFloorOfRoom(r);
            RoomStorage.RemoveRoom(r.ID);
        }

        // Rebuild
        if (changedRooms.Count > 0)
        {
            Color[] palette = { new(1f, .95f, .6f), new(.7f, 1f, .7f), new(.7f, .9f, 1f), new(1f, .75f, .85f) };
            splitRoomManager.RebuildSplitRoom(changedRooms, palette);
        }
    }

    // ======= Helper =======
    private static List<Vector2> SimplifyLoop(List<Vector2> src, float eps = 1e-3f)
    {
        if (src.Count < 3) return src.ToList();
        List<Vector2> dst = new();
        for (int i = 0; i < src.Count; i++)
        {
            Vector2 prev = src[(i - 1 + src.Count) % src.Count];
            Vector2 cur = src[i];
            Vector2 next = src[(i + 1) % src.Count];

            float cross = Mathf.Abs((next.x - prev.x) * (cur.y - prev.y) -
                                    (next.y - prev.y) * (cur.x - prev.x));
            if (cross > eps) dst.Add(cur);
        }
        return dst;
    }

    private static string EdgeKey(List<Vector2> loop)
    {
        return string.Join("|",
            loop.Select((p, i) =>
            {
                Vector2 q = loop[(i + 1) % loop.Count];
                if (p.x < q.x || (Mathf.Approximately(p.x, q.x) && p.y < q.y))
                    return $"{p.x:F3},{p.y:F3}-{q.x:F3},{q.y:F3}";
                else
                    return $"{q.x:F3},{q.y:F3}-{p.x:F3},{p.y:F3}";
            }).OrderBy(s => s));
    }

    private static void RemoveFloorOfRoom(Room room)
    {
        var floorGO = GameObject.Find($"RoomFloor_{room.ID}");
        if (floorGO != null) GameObject.Destroy(floorGO);
    }

    private static List<WallLine> BuildWallLinesFromLoop(List<Vector2> loop)
    {
        var lines = new List<WallLine>();
        for (int i = 0; i < loop.Count; i++)
        {
            Vector2 s = loop[i];
            Vector2 e = loop[(i + 1) % loop.Count];
            lines.Add(new WallLine(
                new Vector3(s.x, 0, s.y),
                new Vector3(e.x, 0, e.y),
                LineType.Wall,
                3.0f,
                0.2f,
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()
            ));
        }
        return lines;
    }

    private static float PolygonArea(List<Vector2> poly)
    {
        double area = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            area += (double)poly[j].x * poly[i].y - (double)poly[i].x * poly[j].y;
        return (float)(area * 0.5);
    }

    private static List<List<Vector2>> FindAllLoops(Dictionary<Vector2, HashSet<Vector2>> graph)
    {
        List<List<Vector2>> loops = new();
        HashSet<string> seen = new();

        foreach (var start in graph.Keys)
        {
            Stack<Vector2> path = new();
            HashSet<Vector2> visited = new();
            DFSFindLoops(start, start, graph, path, visited, seen, loops);
        }
        return loops;
    }

    private static void DFSFindLoops(Vector2 current, Vector2 target,
                                     Dictionary<Vector2, HashSet<Vector2>> graph,
                                     Stack<Vector2> path, HashSet<Vector2> visited,
                                     HashSet<string> seen, List<List<Vector2>> loops)
    {
        path.Push(current);
        visited.Add(current);

        foreach (var neighbor in graph[current])
        {
            if (!visited.Contains(neighbor))
            {
                DFSFindLoops(neighbor, target, graph, path, visited, seen, loops);
            }
            else if (neighbor.Equals(target) && path.Count >= 3)
            {
                var loop = SimplifyLoop(path.Reverse().ToList());
                string key = EdgeKey(loop);

                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    loops.Add(loop);
                }
            }
        }

        path.Pop();
        visited.Remove(current);
    }

    private static List<List<Vector2>> RemoveNestedLoops(List<List<Vector2>> loops)
    {
        var result = new List<List<Vector2>>(loops);
        for (int i = result.Count - 1; i >= 0; i--)
        {
            for (int j = 0; j < result.Count; j++)
            {
                if (i != j && PolygonInsidePolygon(result[i], result[j]))
                {
                    result.RemoveAt(i);
                    break;
                }
            }
        }
        return result;
    }

    private static bool PolygonInsidePolygon(List<Vector2> inner, List<Vector2> outer)
    {
        foreach (var p in inner)
            if (!PointInPolygon(p, outer)) return false;
        return true;
    }

    private static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) *
                 (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
                inside = !inside;
        }
        return inside;
    }
}
