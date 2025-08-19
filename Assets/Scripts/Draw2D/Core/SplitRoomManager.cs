using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class SplitRoomManager: MonoBehaviour
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
        if (allLoops.Count <= 1) return;

        const float AREA_MIN = 0.001f;
        var validLoops = allLoops.Where(lp => GeometryUtils.AbsArea(lp) > AREA_MIN).ToList();
        if (validLoops.Count <= 1) return;

        var largestLoop = validLoops.OrderByDescending(lp => GeometryUtils.AbsArea(lp)).First();
        var uniqueLoops = validLoops.Where(lp => !GeometryUtils.IsSamePolygonFlexible(lp, largestLoop))
                                    .Aggregate(new List<List<Vector2>>(), (acc, lp) =>
                                    { if (!acc.Any(u => GeometryUtils.IsSamePolygonFlexible(u, lp))) acc.Add(lp); return acc; });
        if (uniqueLoops.Count == 0) return;

        string gid = !string.IsNullOrEmpty(originalRoom.groupID) ? originalRoom.groupID : originalRoom.ID;

        // === BACKUP trước khi mutate
        var backupWalls = originalRoom.wallLines.Select(w => new WallLine(w)).ToList();

        // GOM NHỮNG LINE CẦN GIỮ (door/window + manual) TỪ BACKUP 
        var preservedLines = backupWalls
            .Where(w => w.isManualConnection || w.type != LineType.Wall)
            .Select(w => new WallLine(w)) // clone
            .ToList();

        // --- mutate các room theo loops ---
        var loop0 = uniqueLoops[0];
        originalRoom.groupID = gid;
        originalRoom.checkpoints = loop0;
        originalRoom.wallLines = backupWalls
            .Where(w => GeometryUtils.EdgeInLoop(loop0,
                        new Vector2(w.start.x, w.start.z),
                        new Vector2(w.end.x, w.end.z)))
            .ToList();
        RoomStorage.UpdateOrAddRoom(originalRoom);

        var floorGO = GameObject.Find($"RoomFloor_{originalRoom.ID}");
        if (floorGO != null) GameObject.Destroy(floorGO);

        for (int i = 1; i < uniqueLoops.Count; i++)
        {
            var lp = uniqueLoops[i];
            Room r = new Room();
            r.SetID(Guid.NewGuid().ToString());
            r.groupID = gid;
            r.checkpoints = lp;
            r.wallLines = backupWalls
                .Where(w => GeometryUtils.EdgeInLoop(lp,
                            new Vector2(w.start.x, w.start.z),
                            new Vector2(w.end.x, w.end.z)))
                .ToList();
            RoomStorage.UpdateOrAddRoom(r);
        }

        var rooms = RoomStorage.GetRoomsByGroupID(gid);

        // Xoá visual cũ
        ClearAllRoomVisuals(gid);

        // (nếu bạn có tag RoomFloor, giữ lại phần này)
        var oldFloors = GameObject.FindGameObjectsWithTag("RoomFloor");
        foreach (var floor in oldFloors)
        {
            if (floor.name.Contains($"RoomFloor_{originalRoom.ID}"))
                GameObject.Destroy(floor);
        }

        // >>> TRUYỀN preservedLines VÀO HÀM REBUILD <<<
        // Color[] palette = { new(1f, .95f, .6f), new(.7f, 1f, .7f), new(.7f, .9f, 1f), new(1f, .75f, .85f) };
        Color[] palette = null;
        RebuildSplitRoom(rooms, palette, preservedLines);
    }
    public void RebuildSplitRoom(List<Room> rooms, Color[] colors = null, List<WallLine> preservedLines = null)
    {
        if (rooms == null || rooms.Count == 0)
        {
            Debug.Log("Không có Room nào để hiển thị.");
            return;
        }

        string groupID = rooms[0].groupID;

        // === REBUILD VISUAL CHO CÁC ROOM MỚI ===
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];

            // Checkpoint GOs
            var loopGO = new List<GameObject>();
            foreach (var pt in room.checkpoints)
            {
                var cp = Instantiate(checkPointManager.checkpointPrefab, new Vector3(pt.x, 0f, pt.y), Quaternion.identity);
                loopGO.Add(cp);
            }
            checkPointManager.AllCheckpoints.Add(loopGO);
            checkPointManager.loopMappings.Add(new LoopMap(room.ID, loopGO));

            // Mesh floor (nhớ gán tag nếu bạn có logic find-by-tag)
            GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
            floorGO.tag = "RoomFloor";
            floorGO.transform.position = Vector3.zero;
            var meshCtrl = floorGO.AddComponent<RoomMeshController>();
            Color floorColor = (colors != null && i < colors.Length) ? colors[i] : Color.white;
            meshCtrl.Initialize(room.ID, floorColor);

            // Vẽ lines sẵn có của room (nếu room đã có door/window từ bước trước)
            foreach (var wl in room.wallLines)
            {
                checkPointManager.DrawingTool.currentLineType = wl.type;
                checkPointManager.DrawLineAndDistance(wl.start, wl.end);

                if (wl.type == LineType.Door || wl.type == LineType.Window)
                {
                    Vector3 s = wl.start; if (s.y < 0.02f) s.y = 0.02f;
                    Vector3 e = wl.end; if (e.y < 0.02f) e.y = 0.02f;
                    var p1 = Instantiate(checkPointManager.checkpointPrefab, s, Quaternion.identity);
                    var p2 = Instantiate(checkPointManager.checkpointPrefab, e, Quaternion.identity);

                    checkPointManager.tempDoorWindowPoints ??= new Dictionary<string, List<(WallLine, GameObject, GameObject)>>();
                    if (!checkPointManager.tempDoorWindowPoints.ContainsKey(room.ID))
                        checkPointManager.tempDoorWindowPoints[room.ID] = new List<(WallLine, GameObject, GameObject)>();
                    checkPointManager.tempDoorWindowPoints[room.ID].Add((wl, p1, p2));
                }
            }
        }

        // === GẮN LẠI preservedLines VÀO PHÒNG MỚI PHÙ HỢP NHẤT ===
        if (preservedLines != null && preservedLines.Count > 0)
        {
            foreach (var w in preservedLines)
            {
                Vector2 s2 = new Vector2(w.start.x, w.start.z);
                Vector2 e2 = new Vector2(w.end.x, w.end.z);

                Room bestRoom = null; int bestEdge = -1;
                float bestScore = float.MaxValue, bestTA = 0f, bestTB = 0f;

                foreach (var r in rooms)
                {
                    var cps = r.checkpoints; int n = cps.Count; if (n < 2) continue;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 A = cps[i], B = cps[(i + 1) % n];
                        Vector2 AB = B - A; float len2 = AB.sqrMagnitude < 1e-12f ? 1e-12f : AB.sqrMagnitude;
                        float t1 = Mathf.Clamp01(Vector2.Dot(s2 - A, AB) / len2);
                        float t2 = Mathf.Clamp01(Vector2.Dot(e2 - A, AB) / len2);
                        Vector2 ps = A + t1 * AB, pe = A + t2 * AB;
                        float score = Mathf.Max(Vector2.Distance(s2, ps), Vector2.Distance(e2, pe));
                        if (score < bestScore) { bestScore = score; bestEdge = i; bestTA = t1; bestTB = t2; bestRoom = r; }
                    }
                }

                if (bestRoom == null || bestEdge < 0) continue;

                Vector2 AA = bestRoom.checkpoints[bestEdge];
                Vector2 BB = bestRoom.checkpoints[(bestEdge + 1) % bestRoom.checkpoints.Count];

                Vector3 sNew = new Vector3(Mathf.Lerp(AA.x, BB.x, bestTA), 0f, Mathf.Lerp(AA.y, BB.y, bestTA));
                Vector3 eNew = new Vector3(Mathf.Lerp(AA.x, BB.x, bestTB), 0f, Mathf.Lerp(AA.y, BB.y, bestTB));

                w.start = sNew; w.end = eNew;

                bestRoom.wallLines.Add(w);

                checkPointManager.DrawingTool.currentLineType = w.type;
                checkPointManager.DrawLineAndDistance(w.start, w.end);

                if (w.type == LineType.Door || w.type == LineType.Window)
                {
                    Vector3 sH = sNew; if (sH.y < 0.02f) sH.y = 0.02f;
                    Vector3 eH = eNew; if (eH.y < 0.02f) eH.y = 0.02f;

                    var p1 = Instantiate(checkPointManager.checkpointPrefab, sH, Quaternion.identity);
                    var p2 = Instantiate(checkPointManager.checkpointPrefab, eH, Quaternion.identity);

                    checkPointManager.tempDoorWindowPoints ??= new Dictionary<string, List<(WallLine, GameObject, GameObject)>>();
                    if (!checkPointManager.tempDoorWindowPoints.ContainsKey(bestRoom.ID))
                        checkPointManager.tempDoorWindowPoints[bestRoom.ID] = new List<(WallLine, GameObject, GameObject)>();
                    checkPointManager.tempDoorWindowPoints[bestRoom.ID].Add((w, p1, p2));
                }
            }
        }

        Debug.Log($"[RebuildSplitRoom] Đã build lại {rooms.Count} phòng từ group {groupID} (giữ {(preservedLines?.Count ?? 0)} line non-wall/manual).");
    }

    void ClearAllRoomVisuals(string groupID)
    {
        var roomsInGroup = RoomStorage.GetRoomsByGroupID(groupID);
        var roomIDs = roomsInGroup.Select(r => r.ID).ToHashSet();

        // 1. Xóa line trong DrawingTool.wallLines của các room này
        checkPointManager.DrawingTool.wallLines.RemoveAll(wl =>
            roomsInGroup.Any(r =>
                r.wallLines.Any(gwl =>
                    (Vector3.Distance(gwl.start, wl.start) < 0.001f &&
                     Vector3.Distance(gwl.end, wl.end) < 0.001f) ||
                    (Vector3.Distance(gwl.start, wl.end) < 0.001f &&
                     Vector3.Distance(gwl.end, wl.start) < 0.001f)
                )
            )
        );

        // 2. Xóa checkpoint + loopMap
        foreach (var room in roomsInGroup)
        {
            var loopMap = checkPointManager.loopMappings.FirstOrDefault(lm => lm.RoomID == room.ID);
            if (loopMap != null)
            {
                foreach (var cp in loopMap.CheckpointsGO)
                    if (cp != null) Destroy(cp);

                checkPointManager.loopMappings.Remove(loopMap);
                checkPointManager.AllCheckpoints.Remove(loopMap.CheckpointsGO);
            }

            // 3. Xóa mesh floor
            if (checkPointManager.RoomFloorMap.TryGetValue(room.ID, out var oldGO))
            {
                Destroy(oldGO);
                checkPointManager.RoomFloorMap.Remove(room.ID);
            }

            // 4. Xóa cửa / cửa sổ
            // if (checkPointManager.tempDoorWindowPoints.ContainsKey(room.ID))
            // {
            //     foreach (var (_, p1, p2) in checkPointManager.tempDoorWindowPoints[room.ID])
            //     {
            //         if (p1 != null) Destroy(p1);
            //         if (p2 != null) Destroy(p2);
            //     }
            //     checkPointManager.tempDoorWindowPoints.Remove(room.ID);
            // }
        }
    }
}
