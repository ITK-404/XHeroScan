using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class SplitRoomManager : MonoBehaviour
{
    private CheckpointManager checkPointManager;

    void Start()
    {
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
    }

    public void DetectAndSplitRoomIfNecessary(Room originalRoom)
    {
        if (originalRoom == null) return;

        var allLoops = GeometryUtils.ListLoopsInRoom(originalRoom);
        if (allLoops == null || allLoops.Count <= 1) return;

        const float AREA_MIN = 0.001f;
        var validLoops = allLoops.Where(lp => GeometryUtils.AbsArea(lp) > AREA_MIN).ToList();
        if (validLoops.Count <= 1) return;

        var largestLoop = validLoops.OrderByDescending(lp => GeometryUtils.AbsArea(lp)).First();

        // Lọc ra các loop duy nhất (loại trùng lặp hình học với largest)
        var uniqueLoops = validLoops
            .Where(lp => !GeometryUtils.IsSamePolygonFlexible(lp, largestLoop))
            .Aggregate(new List<List<Vector2>>(), (acc, lp) =>
            {
                if (!acc.Any(u => GeometryUtils.IsSamePolygonFlexible(u, lp))) acc.Add(lp);
                return acc;
            });

        if (uniqueLoops.Count == 0) return;

        string gid = !string.IsNullOrEmpty(originalRoom.groupID) ? originalRoom.groupID : originalRoom.ID;
        string fid = !string.IsNullOrEmpty(originalRoom.floorID) ? originalRoom.floorID : originalRoom.ID;

        // === 1) BACKUP TOÀN BỘ TƯỜNG (giữ nguyên hết) ===
        var allWallsBackup = originalRoom.wallLines?.Select(w => new WallLine(w)).ToList() ?? new List<WallLine>();

        // === 2) Tạo danh sách các room mới theo loops (chưa gán wallLines ở đây) ===
        List<Room> newRooms = new List<Room>();

        // loop0 -> giữ lại originalRoom.ID để tránh đổi id
        var loop0 = uniqueLoops[0];
        originalRoom.groupID = gid;
        originalRoom.floorID = fid;
        originalRoom.checkpoints = loop0;
        originalRoom.center = GeoUtil.Centroid(originalRoom.checkpoints);
        // Tạm thời để trống, sẽ gán khi rebuild
        originalRoom.wallLines = new List<WallLine>();
        RoomStorage.UpdateOrAddRoom(originalRoom);
        newRooms.Add(originalRoom);

        var affectedRooms = new List<Room>();
        affectedRooms.Add(originalRoom);
        for (int i = 1; i < uniqueLoops.Count; i++)
        {
            var lp = uniqueLoops[i];
            Room r = new Room();
            r.SetID(Guid.NewGuid().ToString());
            r.groupID = gid;
            r.floorID = fid;
            r.checkpoints = lp;
            r.center = GeoUtil.Centroid(r.checkpoints);
            r.wallLines = new List<WallLine>();
            RoomStorage.UpdateOrAddRoom(r);
            affectedRooms.Add(r);
        }

        // Xóa floor cũ của originalRoom (nếu còn)
        var floorGO = GameObject.Find($"RoomFloor_{originalRoom.ID}");
        if (floorGO != null) GameObject.Destroy(floorGO);

        // Gom rooms theo group id (phòng cũ + mới)
        var rooms = affectedRooms;

        // Xóa visual cũ trong group (checkpoint GO, mesh, temp door/window, vv.)
        ClearRoomVisuals(affectedRooms);

        // Xóa sàn cũ có tên trùng với phòng gốc nếu còn
        var oldFloors = GameObject.FindGameObjectsWithTag("RoomFloor");
        foreach (var f in oldFloors)
        {
            if (f != null && f.name.Contains($"RoomFloor_{originalRoom.ID}"))
                GameObject.Destroy(f);
        }

        // === 3) REBUILD: dò & gán lại tường cho từng room từ allWallsBackup (đơn giản) ===
        Color[] palette = null; // có thể set palette nếu muốn
        RebuildSplitRoom(affectedRooms, allWallsBackup, palette);
    }

public void RebuildSplitRoom(List<Room> rooms, List<WallLine> allWallsBackup, Color[] colors = null)
{
    if (rooms == null || rooms.Count == 0) return;

    const float EPS_EDGE = 1e-3f;

    // Đảm bảo có map lưu extra points
    if (checkPointManager.placedPointsByRoom == null)
        checkPointManager.placedPointsByRoom = new Dictionary<string, List<GameObject>>();

    // Dọn extra points cũ (nếu có) để tránh trùng
    foreach (var room in rooms)
    {
        if (checkPointManager.placedPointsByRoom.TryGetValue(room.ID, out var oldExtras) && oldExtras != null)
        {
            foreach (var go in oldExtras) if (go) Destroy(go);
            checkPointManager.placedPointsByRoom.Remove(room.ID);
        }
    }

    for (int i = 0; i < rooms.Count; i++)
    {
        var room = rooms[i];

        // 1) Gán lại tường cho room từ backup
        room.wallLines = PickWallsForRoom(allWallsBackup, room.checkpoints);

        // 2) Spawn lại checkpoint GO cho polygon
        var loopGO = new List<GameObject>();
        foreach (var pt in room.checkpoints)
        {
            Vector3 worldPos = new Vector3(pt.x, 0, pt.y);
            var cp = Instantiate(checkPointManager.checkpointPrefab, worldPos, Quaternion.identity);
            loopGO.Add(cp);
        }
        checkPointManager.AllCheckpoints.Add(loopGO);
        checkPointManager.loopMappings.Add(new LoopMap(room.ID, loopGO));

        // 3) Floor
        var floorGO = new GameObject($"RoomFloor_{room.ID}");
        floorGO.transform.position = Vector3.zero;
        var meshCtrl = floorGO.AddComponent<RoomMeshController>();
        var floorColor = (colors != null && i < colors.Length) ? colors[i] : Color.white;
        meshCtrl.Initialize(room.ID, floorColor);

        // 4) Vẽ lại tường & tạo EXTRA POINTS cho tường NỘI BỘ (không tạo trên biên)
        if (room.wallLines != null)
        {
            foreach (var wl in room.wallLines)
            {
                // vẽ line
                checkPointManager.DrawingTool.currentLineType = wl.type;
                checkPointManager.DrawLineAndDistance(wl.start, wl.end, room.thickness);

                if (wl.type != LineType.Wall) continue;

                // Phân loại: trong/biên cho 2 đầu mút
                Vector2 s2 = new Vector2(wl.start.x, wl.start.z);
                Vector2 e2 = new Vector2(wl.end.x,   wl.end.z);

                bool sOnB = IsOnBoundaryPoint(s2, room.checkpoints, EPS_EDGE);
                bool eOnB = IsOnBoundaryPoint(e2, room.checkpoints, EPS_EDGE);

                bool sInsideStrict = PointInPolygonStrict(s2, room.checkpoints) && !sOnB;
                bool eInsideStrict = PointInPolygonStrict(e2, room.checkpoints) && !eOnB;
            }
        }

        // 5) Cập nhật storage
        RoomStorage.UpdateOrAddRoom(room);
    }
}
// "Trong" kiểu ray casting (strict): không nới lỏng sát biên
private static bool PointInPolygonStrict(Vector2 p, List<Vector2> poly)
{
    if (poly == null || poly.Count < 3) return false;
    bool inside = false;
    for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
    {
        var pi = poly[i];
        var pj = poly[j];
        bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                         (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) == 0 ? float.Epsilon : (pj.y - pi.y)) + pi.x);
        if (intersect) inside = !inside;
    }
    return inside;
}

// Điểm có nằm trên bất kỳ cạnh nào của polygon (khoảng cách < eps)?
private static bool IsOnBoundaryPoint(Vector2 p, List<Vector2> poly, float eps)
{
    if (poly == null || poly.Count < 2) return false;
    for (int i = 0; i < poly.Count; i++)
    {
        var a = poly[i];
        var b = poly[(i + 1) % poly.Count];
        if (DistancePointToSegment2D(p, a, b) <= eps) return true;
    }
    return false;
}
    private List<WallLine> PickWallsForRoom(List<WallLine> allWalls, List<Vector2> roomLoop)
    {
        const float EPS_IN = 1e-3f;
        const float EPS_EDGE = 1e-3f;

        var result = new List<WallLine>();
        foreach (var w in allWalls)
        {
            Vector2 s = new Vector2(w.start.x, w.start.z);
            Vector2 e = new Vector2(w.end.x, w.end.z);

            bool onBoundary = GeometryUtils.EdgeInLoop(roomLoop, s, e);

            bool sInside = PointInPolygonEps(s, roomLoop, EPS_IN);
            bool eInside = PointInPolygonEps(e, roomLoop, EPS_IN);
            bool bothInside = sInside && eInside;

            bool sOnBoundary = IsOnBoundary(s, roomLoop, EPS_EDGE);
            bool eOnBoundary = IsOnBoundary(e, roomLoop, EPS_EDGE);
            bool oneInsideOneBoundary = (sInside && eOnBoundary) || (eInside && sOnBoundary);

            bool midpointInside = MidSampleInside(s, e, roomLoop, EPS_IN);

            if (onBoundary || bothInside || oneInsideOneBoundary || midpointInside)
                result.Add(new WallLine(w)); // clone
        }
        return result;
    }
    
    void ClearRoomVisuals(List<Room> roomsToClear)
    {
        if (roomsToClear == null || roomsToClear.Count == 0) return;

        // Xoá line visual trong DrawingTool của các phòng này
        checkPointManager.DrawingTool.wallLines.RemoveAll(wl =>
            roomsToClear.Any(r =>
                r.wallLines != null && r.wallLines.Any(gwl =>
                    (Vector3.Distance(gwl.start, wl.start) < 0.001f &&
                     Vector3.Distance(gwl.end, wl.end) < 0.001f) ||
                    (Vector3.Distance(gwl.start, wl.end) < 0.001f &&
                     Vector3.Distance(gwl.end, wl.start) < 0.001f)
                )
            )
        );

        // Xóa checkpoint + loopMap + mesh + temp points
        foreach (var room in roomsToClear)
        {
            var loopMap = checkPointManager.loopMappings.FirstOrDefault(lm => lm.RoomID == room.ID);
            if (loopMap != null)
            {
                foreach (var cp in loopMap.CheckpointsGO)
                    if (cp != null) Destroy(cp);

                checkPointManager.loopMappings.Remove(loopMap);
                checkPointManager.AllCheckpoints.Remove(loopMap.CheckpointsGO);
            }

            if (checkPointManager.RoomFloorMap.TryGetValue(room.ID, out var oldGO))
            {
                Destroy(oldGO);
                checkPointManager.RoomFloorMap.Remove(room.ID);
            }

            if (checkPointManager.tempDoorWindowPoints.ContainsKey(room.ID))
            {
                foreach (var (_, p1, p2) in checkPointManager.tempDoorWindowPoints[room.ID])
                {
                    if (p1 != null) Destroy(p1);
                    if (p2 != null) Destroy(p2);
                }
                checkPointManager.tempDoorWindowPoints.Remove(room.ID);
            }

            // (Nếu có tempManualLinePoints hoặc placedPointsByRoom thì dọn tương tự)
            if (checkPointManager.tempManualLinePoints != null &&
                checkPointManager.tempManualLinePoints.ContainsKey(room.ID))
            {
                foreach (var (_, p1, p2) in checkPointManager.tempManualLinePoints[room.ID])
                {
                    if (p1 != null) Destroy(p1);
                    if (p2 != null) Destroy(p2);
                }
                checkPointManager.tempManualLinePoints.Remove(room.ID);
            }
        }
    }

    private static bool PointInPolygonEps(Vector2 p, List<Vector2> poly, float eps = 1e-4f)
    {
        if (poly == null || poly.Count < 3) return false;

        // Ray casting basic + chấp nhận sát biên
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];

            // Nếu sát biên thì coi là "trong"
            if (DistancePointToSegment2D(p, pj, pi) <= eps) return true;

            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) == 0 ? float.Epsilon : (pj.y - pi.y)) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    private static float DistancePointToSegment2D(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float denom = Vector2.Dot(ab, ab);
        if (denom <= Mathf.Epsilon) return Vector2.Distance(p, a);
        float t = Vector2.Dot(p - a, ab) / denom;
        t = Mathf.Clamp01(t);
        var proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }

    private static bool IsOnBoundary(Vector2 p, List<Vector2> poly, float eps)
    {
        if (poly == null || poly.Count < 2) return false;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            if (DistancePointToSegment2D(p, a, b) <= eps) return true;
        }
        return false;
    }

    private static bool MidSampleInside(Vector2 s, Vector2 e, List<Vector2> poly, float eps)
    {
        Vector2 m = 0.5f * (s + e);
        if (PointInPolygonEps(m, poly, eps)) return true;

        Vector2 m1 = Vector2.Lerp(s, e, 0.4f);
        Vector2 m2 = Vector2.Lerp(s, e, 0.6f);
        return PointInPolygonEps(m1, poly, eps) || PointInPolygonEps(m2, poly, eps);
    }
}
