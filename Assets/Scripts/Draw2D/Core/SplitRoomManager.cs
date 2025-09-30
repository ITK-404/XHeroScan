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

        // === 2) Tạo danh sách các room mới theo loops ===
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

        const float EPS_EDGE   = 1e-3f;  // nhận biết điểm nằm trên biên
        const float TOL_ATTACH = 0.08f;  // khoảng cách "hít" line vào cạnh
        const float TOL_ONEDGE = 0.02f;  // coi như nằm trên biên

        // đảm bảo map extra
        if (checkPointManager.placedPointsByRoom == null)
            checkPointManager.placedPointsByRoom = new Dictionary<string, List<GameObject>>();

        // dọn extra cũ
        foreach (var room in rooms)
        {
            if (checkPointManager.placedPointsByRoom.TryGetValue(room.ID, out var oldExtras) && oldExtras != null)
            {
                foreach (var go in oldExtras) if (go) Destroy(go);
                checkPointManager.placedPointsByRoom.Remove(room.ID);
            }
        }

        // dọn loop mapping cũ cho từng phòng
        if (checkPointManager.loopMappings != null)
        {
            for (int k = checkPointManager.loopMappings.Count - 1; k >= 0; k--)
            {
                var lm = checkPointManager.loopMappings[k];
                if (lm == null) continue;
                if (rooms.Any(r => r.ID == lm.RoomID))
                    checkPointManager.loopMappings.RemoveAt(k);
            }
        }

        if (checkPointManager.RoomFloorMap == null)
            checkPointManager.RoomFloorMap = new Dictionary<string, GameObject>();

        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];

            // Xoá line/distance cũ của room này (nếu còn sót)
            ClearRoomLinesAndDistances(room.ID);

            var perimeter = BuildPerimeterWallsFromPolygon(room, room.checkpoints);

            // GHÉP line nội bộ từ backup
            var internalLines = AttachFromBackupToRoom(allWallsBackup, room.checkpoints, TOL_ATTACH, TOL_ONEDGE, EPS_EDGE);

            room.wallLines = new List<WallLine>();
            room.wallLines.AddRange(perimeter);
            room.wallLines.AddRange(internalLines);

            // SPAWN checkpoint GO chu vi
            var loopGO = new List<GameObject>();
            foreach (var pt in room.checkpoints)
            {
                Vector3 worldPos = new Vector3(pt.x, 0f, pt.y);
                var cp = Instantiate(checkPointManager.checkpointPrefab, worldPos, Quaternion.identity);
                loopGO.Add(cp);
            }
            checkPointManager.AllCheckpoints.Add(loopGO);
            checkPointManager.loopMappings.Add(new LoopMap(room.ID, loopGO));

            // FLOOR
            var floorGO = new GameObject($"RoomFloor_{room.ID}");
            floorGO.tag = "RoomFloor";
            floorGO.transform.position = Vector3.zero;
            var meshCtrl = floorGO.AddComponent<RoomMeshController>();
            var floorColor = (colors != null && i < colors.Length) ? colors[i] : Color.white;
            meshCtrl.Initialize(room.ID, floorColor);
            checkPointManager.RoomFloorMap[room.ID] = floorGO;

            // VẼ lại line (visual). Nếu DrawingTool có SetParents, gán parent để lần sau xoá gọn.
            var lineRoot = new GameObject($"RoomLines_{room.ID}");
            lineRoot.transform.SetParent(floorGO.transform, false);
            var distRoot = new GameObject($"RoomDists_{room.ID}");
            distRoot.transform.SetParent(floorGO.transform, false);

            var extras = new List<GameObject>();
            var seen = new HashSet<string>(); // chống trùng extra

            foreach (var wl in room.wallLines)
            {
                checkPointManager.DrawingTool.currentLineType = wl.type;
                checkPointManager.DrawLineAndDistance(wl.start, wl.end, room.thickness);

                if (wl.type != LineType.Wall) continue;

                Vector2 s2 = new Vector2(wl.start.x, wl.start.z);
                Vector2 e2 = new Vector2(wl.end.x,   wl.end.z);

                bool sOnB = IsOnBoundaryPoint(s2, room.checkpoints, EPS_EDGE);
                bool eOnB = IsOnBoundaryPoint(e2, room.checkpoints, EPS_EDGE);

                bool sInsideStrict = PointInPolygonStrict(s2, room.checkpoints) && !sOnB;
                bool eInsideStrict = PointInPolygonStrict(e2, room.checkpoints) && !eOnB;

                // extra cho tường nội bộ (không trên biên)
                void TrySpawnExtra(Vector3 pos)
                {
                    string key = $"{Mathf.Round(pos.x*1000f)}|{Mathf.Round(pos.z*1000f)}";
                    if (seen.Contains(key)) return;
                    seen.Add(key);

                    var go = Instantiate(checkPointManager.checkpointPrefab, pos, Quaternion.identity);
                    go.tag = "CheckpointExtra";
                    extras.Add(go);
                }

                if (sInsideStrict) TrySpawnExtra(wl.start);
                if (eInsideStrict) TrySpawnExtra(wl.end);
            }

            if (extras.Count > 0)
                checkPointManager.placedPointsByRoom[room.ID] = extras;

            // STORAGE
            RoomStorage.UpdateOrAddRoom(room);
        }
    }

    private void ClearRoomLinesAndDistances(string roomID)
    {
        var oldLines = GameObject.Find($"RoomLines_{roomID}");
        if (oldLines) Destroy(oldLines);

        var oldDists = GameObject.Find($"RoomDists_{roomID}");
        if (oldDists) Destroy(oldDists);
    }

    private List<WallLine> BuildPerimeterWallsFromPolygon(Room room, List<Vector2> poly)
    {
        var res = new List<WallLine>();
        if (poly == null || poly.Count < 2) return res;

        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            Vector3 A = new Vector3(poly[i].x, 0f, poly[i].y);
            Vector3 B = new Vector3(poly[j].x, 0f, poly[j].y);

            var wl = new WallLine(A, B, LineType.Wall, 0f);
            wl.isManualConnection = false;
            res.Add(wl);
        }
        return res;
    }

    private List<WallLine> AttachFromBackupToRoom(List<WallLine> backup, List<Vector2> poly, float tolAttach, float tolOnEdge, float epsEdge)
    {
        var result = new List<WallLine>();
        if (backup == null || backup.Count == 0 || poly == null || poly.Count < 2) return result;

        // dựng cạnh chu vi
        var edges = new List<(Vector3 A, Vector3 B)>();
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            edges.Add((new Vector3(poly[i].x, 0f, poly[i].y),
                    new Vector3(poly[j].x, 0f, poly[j].y)));
        }

        foreach (var w in backup)
        {
            // bỏ chu vi cũ
            if (w.type == LineType.Wall && !w.isManualConnection) continue;
            Vector2 mid = new Vector2((w.start.x + w.end.x) * 0.5f, (w.start.z + w.end.z) * 0.5f);
            if (IsOnBoundaryPoint(mid, poly, Mathf.Max(epsEdge, tolOnEdge)))
                continue;
            bool attached = false;
            Vector3 s = w.start, e = w.end;

            foreach (var (A, B) in edges)
            {
                (Vector3 ps, float ts, float ds) = ProjectToSegment(s, A, B);
                (Vector3 pe, float te, float de) = ProjectToSegment(e, A, B);

                if (ts >= 0f && ts <= 1f && te >= 0f && te <= 1f && ds <= tolAttach && de <= tolAttach)
                {
                    var nw = new WallLine(ps, pe, w.type);
                    nw.isManualConnection = w.isManualConnection;
                    result.Add(nw);
                    attached = true;
                    break;
                }
            }
            if (attached) continue;

            // midpoint trong/sát polygon thì giữ cho phòng này
            Vector2 mid2 = new Vector2((w.start.x + w.end.x) * 0.5f, (w.start.z + w.end.z) * 0.5f);
            bool midInside = PointInPolygonStrict(mid2, poly) || IsOnBoundaryPoint(mid2, poly, Mathf.Max(epsEdge, tolOnEdge));

            if (midInside) result.Add(new WallLine(w));
        }

        return result;
    }

    private (Vector3 proj, float t, float dist) ProjectToSegment(Vector3 P, Vector3 A, Vector3 B)
    {
        Vector3 AB = B - A;
        float ab2 = Vector3.Dot(AB, AB);
        if (ab2 < 1e-9f) return (A, 0f, Vector3.Distance(P, A));
        float t = Mathf.Clamp01(Vector3.Dot(P - A, AB) / ab2);
        Vector3 proj = A + t * AB;
        float d = Vector3.Distance(P, proj);
        return (proj, t, d);
    }


    // // "Trong" kiểu ray casting (strict): không nới lỏng sát biên
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

    // // Điểm có nằm trên bất kỳ cạnh nào của polygon (khoảng cách < eps)?
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
}
