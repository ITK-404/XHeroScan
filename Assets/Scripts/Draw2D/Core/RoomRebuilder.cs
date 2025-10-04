using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RoomRebuilder
{
    // ==== THAM SỐ LAYERING MẶC ĐỊNH ====
    public const int   DEFAULT_ROOM_INDEX = 2;        // phòng vẽ ở index 2
    public const float DEFAULT_LAYER_STEP = 0.002f;   // mỗi index cách nhau ~2mm
    public const float DEFAULT_WALL_LIFT  = 0.003f;   // line nhô lên khỏi sàn

    // ------------------------------------------------------------
    // 1) REBUILD perimeter cho 1 phòng (khi move/snap), BẢO TOÀN manual + door/window
    // ------------------------------------------------------------
    public static void RebuildPerimeterAndVisuals(
        CheckpointManager cpm,
        string roomID,
        List<GameObject> loopGOs,
        int roomIndex   = DEFAULT_ROOM_INDEX,
        float layerStep = DEFAULT_LAYER_STEP,
        float wallLift  = DEFAULT_WALL_LIFT
    )
    {
        if (cpm == null) return;
        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null || loopGOs == null || loopGOs.Count == 0) return;

        float baseY = roomIndex * layerStep;
        float lineY = baseY + wallLift;

        // 1) Update polygon theo vị trí GO
        room.checkpoints = loopGOs.Select(go =>
        {
            var p = go.transform.position;
            return new Vector2(p.x, p.z);
        }).ToList();

        int n = room.checkpoints.Count;
        if (n < 3) return;

        // 2) Bảo toàn manual lines + openings
        var manuals  = room.wallLines?.Where(l => l != null && l.isManualConnection)
                                      .Select(CloneWallLine).ToList()
                        ?? new List<WallLine>();
        var openings = room.wallLines?.Where(l => l != null && l.type != LineType.Wall)
                                      .Select(CloneWallLine).ToList()
                        ?? new List<WallLine>();

        // 3) Dựng perimeter mới từ polygon
        var newPerimeter = new List<WallLine>(n);
        for (int i = 0; i < n; i++)
        {
            Vector2 a = room.checkpoints[i];
            Vector2 b = room.checkpoints[(i + 1) % n];

            newPerimeter.Add(new WallLine
            {
                start = new Vector3(a.x, lineY, a.y),
                end   = new Vector3(b.x, lineY, b.y),
                type  = LineType.Wall,
                isVisible = true,
                isManualConnection = false,
            });
        }

        // 4) Re-project openings lên MỘT perimeter segment gần midpoint nhất
        foreach (var op in openings)
        {
            var seg = FindNearestSegmentByMidpoint(op, newPerimeter, cpm);
            if (seg == null) continue;

            // Project cả hai đầu mút lên cùng 1 segment
            Vector3 pa = cpm.ProjectPointOnLineSegment(seg.start, seg.end, op.start);
            Vector3 pb = cpm.ProjectPointOnLineSegment(seg.start, seg.end, op.end);

            // Nếu coi như cùng điểm -> tách nhẹ để tránh length=0
            if (Vector3.Distance(pa, pb) < 1e-4f)
            {
                Vector3 dir = (seg.end - seg.start);
                float len = dir.magnitude;
                if (len < 1e-6f) { dir = Vector3.right; len = 1f; }
                dir /= len;
                float eps = 0.01f;
                pa = Vector3.Lerp(seg.start, seg.end, 0.5f - eps);
                pb = Vector3.Lerp(seg.start, seg.end, 0.5f + eps);
            }

            pa.y = lineY; pb.y = lineY;
            op.start = pa; op.end = pb;
            op.isManualConnection = false;
            op.isVisible = true;
        }

        // 5) Khử manual trùng cạnh perimeter
        manuals = manuals.Where(m => !ExistsSameEdge(newPerimeter, m, 1e-4f)).ToList();

        // 6) Hợp nhất
        var merged = new List<WallLine>(newPerimeter.Count + manuals.Count + openings.Count);
        merged.AddRange(newPerimeter);
        merged.AddRange(manuals);
        merged.AddRange(openings);
        foreach (var l in merged) { l.start.y = lineY; l.end.y = lineY; }

        room.wallLines = merged;

        // 7) Heading + lưu
        HeadingManager.UpdateAllWallHeadings(room, room.headingCompass);
        RoomStorage.UpdateOrAddRoom(room);

        // 8) Floor: đảm bảo tồn tại + cập nhật mesh & map
        var floorGO = EnsureFloorExists(cpm, roomID);
        if (floorGO != null)
        {
            var pos = floorGO.transform.position; pos.y = baseY;
            floorGO.transform.position = pos;
            floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
        }

        // 9) Nâng Y checkpoint GO
        foreach (var go in loopGOs)
        {
            if (!go) continue;
            var p = go.transform.position; p.y = lineY; go.transform.position = p;
        }

        // 10) Đồng bộ temp manipulators (door/window) nếu có
        if (cpm.tempDoorWindowPoints != null && cpm.tempDoorWindowPoints.TryGetValue(roomID, out var list))
        {
            foreach (var (ln, p1, p2) in list)
            {
                if (p1) p1.transform.position = ln.start;
                if (p2) p2.transform.position = ln.end;
            }
        }

        cpm.TryAddChangedRoomID(roomID);
    }

    // ------------------------------------------------------------
    // 2) REBUILD nhiều phòng SAU KHI TÁCH (dùng lại cho SplitRoomManager)
    //    - Gán lại tường theo backup
    //    - Spawn checkpoint GO, tạo Floor, map, chuẩn hoá bằng RebuildPerimeterAndVisuals
    // ------------------------------------------------------------
    public static void RebuildRoomsFromBackup(
        CheckpointManager cpm,
        List<Room> rooms,
        List<WallLine> allWallsBackup,
        Color[] colors = null
    )
    {
        if (cpm == null || rooms == null || rooms.Count == 0) return;

        // đảm bảo các map tồn tại
        if (cpm.placedPointsByRoom == null) cpm.placedPointsByRoom = new Dictionary<string, List<GameObject>>();
        if (cpm.RoomFloorMap == null)       cpm.RoomFloorMap       = new Dictionary<string, GameObject>();

        // dọn extra cũ
        foreach (var r in rooms)
        {
            if (cpm.placedPointsByRoom.TryGetValue(r.ID, out var olds) && olds != null)
            {
                foreach (var go in olds) if (go) UnityEngine.Object.Destroy(go);
                cpm.placedPointsByRoom.Remove(r.ID);
            }
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];

            // 1) pick wall từ backup theo loop
            room.wallLines = PickWallsForRoom(allWallsBackup, room.checkpoints);

            // 2) spawn checkpoint GO (MAIN)
            var loopGO = new List<GameObject>();
            foreach (var pt in room.checkpoints)
            {
                var cp = UnityEngine.Object.Instantiate(cpm.checkpointPrefab,
                    new Vector3(pt.x, 0, pt.y), Quaternion.identity);
                cp.tag = "Checkpoint";
                loopGO.Add(cp);

                // Đăng ký vào placedPointsByRoom để logic snap/split khác nhận ra main CP
                if (!cpm.placedPointsByRoom.TryGetValue(room.ID, out var lst) || lst == null)
                {
                    lst = new List<GameObject>();
                    cpm.placedPointsByRoom[room.ID] = lst;
                }
                lst.Add(cp);
            }
            cpm.AllCheckpoints.Add(loopGO);
            cpm.loopMappings.Add(new LoopMap(room.ID, loopGO));

            // 3) floor + map (tạo tại (0,0,0) cho thống nhất)
            var floorGO = new GameObject($"RoomFloor_{room.ID}");
            floorGO.tag = "RoomFloor";
            floorGO.transform.position = Vector3.zero;

            var meshCtrl = floorGO.AddComponent<RoomMeshController>();
            var color = (colors != null && i < colors.Length) ? colors[i] : Color.white;
            meshCtrl.Initialize(room.ID, color);
            cpm.RoomFloorMap[room.ID] = floorGO;

            // 4) Chuẩn hoá Y + mesh + openings bằng PerimeterAndVisuals
            RebuildPerimeterAndVisuals(cpm, room.ID, loopGO);

            // (KHÔNG vẽ line ở đây; để caller gọi RedrawAllRooms)
            RoomStorage.UpdateOrAddRoom(room);
        }
    }

    // ====================== Helpers ======================

    // Chọn 1 segment perimeter gần nhất dựa trên midpoint của opening
    private static WallLine FindNearestSegmentByMidpoint(WallLine opening, List<WallLine> perimeter, CheckpointManager cpm)
    {
        if (opening == null || perimeter == null || perimeter.Count == 0) return null;
        Vector3 mid = 0.5f * (opening.start + opening.end);

        WallLine best = null; float bestD = float.MaxValue;
        foreach (var s in perimeter)
        {
            Vector3 proj = cpm.ProjectPointOnLineSegment(s.start, s.end, mid);
            float d = Vector3.Distance(mid, proj);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    private static float DistPointToSeg(Vector3 p, Vector3 a, Vector3 b, CheckpointManager cpm)
    {
        var proj = cpm.ProjectPointOnLineSegment(a, b, p);
        return Vector3.Distance(p, proj);
    }

    private static bool ExistsSameEdge(List<WallLine> perimeter, WallLine m, float eps)
    {
        if (m.type != LineType.Wall) return false;
        foreach (var p in perimeter)
            if (SameEdge(m.start, m.end, p.start, p.end, eps)) return true;
        return false;
    }
    private static bool SameEdge(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, float eps)
    {
        bool sameDir = (Vector3.Distance(a1, b1) <= eps && Vector3.Distance(a2, b2) <= eps);
        bool oppDir  = (Vector3.Distance(a1, b2) <= eps && Vector3.Distance(a2, b1) <= eps);
        return sameDir || oppDir;
    }

    private static WallLine CloneWallLine(WallLine s)
    {
        return new WallLine
        {
            start = s.start,
            end = s.end,
            type = s.type,
            isVisible = s.isVisible,
            isManualConnection = s.isManualConnection,
            headingCompass = s.headingCompass,
            distanceHeight = s.distanceHeight,
            Height = s.Height,
            materialFront = s.materialFront,
            materialBack = s.materialBack
        };
    }

    // ---- pick wall theo loop (bản đã siết điều kiện, giống logic bạn dùng)
    private static List<WallLine> PickWallsForRoom(List<WallLine> allWalls, List<Vector2> roomLoop)
    {
        const float EPS = 1e-3f;
        var result = new List<WallLine>();
        foreach (var w in allWalls)
        {
            Vector2 s = new Vector2(w.start.x, w.start.z);
            Vector2 e = new Vector2(w.end.x,   w.end.z);

            bool sOn = IsOnBoundaryPoint(s, roomLoop, EPS);
            bool eOn = IsOnBoundaryPoint(e, roomLoop, EPS);

            bool sIn = PointInPolygonEps(s, roomLoop, EPS);
            bool eIn = PointInPolygonEps(e, roomLoop, EPS);

            bool midIn = MidSampleInside(s, e, roomLoop, EPS);

            bool ok = (sIn && eIn) ||
                      (sIn && eOn) || (eIn && sOn) ||
                      (sOn && eOn) ||
                      (midIn && !( (!sIn && !sOn) && (!eIn && !eOn) ));

            if (ok) result.Add(CloneWallLine(w));
        }
        return result;
    }

    private static bool MidSampleInside(Vector2 s, Vector2 e, List<Vector2> poly, float eps)
    {
        Vector2 m = 0.5f * (s + e);
        if (PointInPolygonEps(m, poly, eps)) return true;
        Vector2 m1 = Vector2.Lerp(s, e, 0.4f);
        Vector2 m2 = Vector2.Lerp(s, e, 0.6f);
        return PointInPolygonEps(m1, poly, eps) || PointInPolygonEps(m2, poly, eps);
    }
    private static bool PointInPolygonEps(Vector2 p, List<Vector2> poly, float eps = 1e-4f)
    {
        if (poly == null || poly.Count < 3) return false;

        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];

            if (DistancePointToSegment2D(p, pj, pi) <= eps) return true;

            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) == 0 ? float.Epsilon : (pj.y - pi.y)) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }
    private static float DistancePointToSegment2D(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float denom = Vector2.Dot(ab, ab);
        if (denom <= Mathf.Epsilon) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        var proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }
    private static bool IsOnBoundaryPoint(Vector2 p, List<Vector2> poly, float eps)
    {
        if (poly == null || poly.Count < 2) return false;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = (i + 1) % poly.Count;
            var vb = poly[b];
            if (DistancePointToSegment2D(p, a, vb) <= eps) return true;
        }
        return false;
    }

    private static GameObject EnsureFloorExists(CheckpointManager cpm, string roomID)
    {
        var floorGO = GameObject.Find($"RoomFloor_{roomID}");
        if (floorGO != null) return floorGO;

        floorGO = new GameObject($"RoomFloor_{roomID}");
        floorGO.tag = "RoomFloor";
        floorGO.transform.position = Vector3.zero;

        var meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(roomID, Color.white);

        if (cpm != null)
        {
            if (cpm.RoomFloorMap == null) cpm.RoomFloorMap = new Dictionary<string, GameObject>();
            cpm.RoomFloorMap[roomID] = floorGO;
        }
        return floorGO;
    }
}
