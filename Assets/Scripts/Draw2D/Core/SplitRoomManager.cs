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

        var allLoops = GeometryUtils.ListLoopsInRoom(originalRoom); // list of loops

        if (allLoops.Count <= 1) return;

        var originalArea = GeometryUtils.AbsArea(originalRoom.checkpoints);
        // const float AREA_RATIO_EPS = 0.01f; // 1% sai số

        // var innerLoops = allLoops
        //     .Where(lp =>
        //         !GeometryUtils.IsSamePolygonFlexible(lp, originalRoom.checkpoints) &&
        //         Mathf.Abs(GeometryUtils.AbsArea(lp) - originalArea) > originalArea * AREA_RATIO_EPS &&
        //         GeometryUtils.AbsArea(lp) < originalArea * (1f - AREA_RATIO_EPS)) //|A - A₀| < 0.02 * A₀  --> chỉ đúng nếu phòng đó gần bằng 98% diện tích gốc
        //     .ToList();

        const float AREA_MIN = 0.001f;

        var validLoops = allLoops
            .Where(lp => GeometryUtils.AbsArea(lp) > AREA_MIN)
            .ToList();

        if (validLoops.Count <= 1) return;

        var largestLoop = validLoops.OrderByDescending(lp => GeometryUtils.AbsArea(lp)).First();

        var innerLoops = validLoops
            .Where(lp => !GeometryUtils.IsSamePolygonFlexible(lp, largestLoop))
            .ToList();

        List<List<Vector2>> uniqueLoops = new();
        foreach (var lp in innerLoops)
            if (!uniqueLoops.Any(u => GeometryUtils.IsSamePolygonFlexible(u, lp)))
                uniqueLoops.Add(lp);

        if (uniqueLoops.Count == 0) return;

        // Giữ groupID gốc nếu có, nếu chưa thì dùng ID hiện tại
        string gid = !string.IsNullOrEmpty(originalRoom.groupID)
                    ? originalRoom.groupID
                    : originalRoom.ID;

        var backupWalls = originalRoom.wallLines.Select(w => new WallLine(w)).ToList();

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

        // Tạo lại danh sách các Room mới dựa trên các vòng kín
        for (int i = 1; i < uniqueLoops.Count; i++)
        {
            var lp = uniqueLoops[i];
            Room r = new Room();
            r.SetID(Guid.NewGuid().ToString());
            r.groupID = gid; // Giữ cùng groupID
            r.checkpoints = lp;
            r.wallLines = backupWalls
                .Where(w => GeometryUtils.EdgeInLoop(lp,
                            new Vector2(w.start.x, w.start.z),
                            new Vector2(w.end.x, w.end.z)))
                .ToList();
            RoomStorage.UpdateOrAddRoom(r);
        }

        var rooms = RoomStorage.GetRoomsByGroupID(gid);
        Color[] palette = { new(1f, .95f, .6f), new(.7f, 1f, .7f), new(.7f, .9f, 1f), new(1f, .75f, .85f) };
        ClearAllRoomVisuals(gid);

        var oldFloors = GameObject.FindGameObjectsWithTag("RoomFloor");
        foreach (var floor in oldFloors)
        {
            if (floor.name.Contains($"RoomFloor_{originalRoom.ID}"))
                GameObject.Destroy(floor);
        }

        RebuildSplitRoom(rooms, palette);
    }

    public void RebuildSplitRoom(List<Room> rooms, Color[] colors = null)
    {
        if (rooms == null || rooms.Count == 0)
        {
            Debug.Log("Không có Room nào để hiển thị.");
            return;
        }

        // Lấy groupID từ phòng đầu tiên (cùng group)
        string groupID = rooms[0].groupID;

        // === Xoá checkpoints, loopMaps, door/window cũ chỉ thuộc group này ===
        var roomsToRemove = RoomStorage.GetRoomsByGroupID(groupID);

        foreach (var room in roomsToRemove)
        {
            var loopMap = checkPointManager.loopMappings.FirstOrDefault(lm => lm.RoomID == room.ID);
            if (loopMap != null)
            {
                foreach (var checkpointGO in loopMap.CheckpointsGO)
                {
                    if (checkpointGO != null) Destroy(checkpointGO);
                }
                checkPointManager.loopMappings.Remove(loopMap);
                checkPointManager.AllCheckpoints.Remove(loopMap.CheckpointsGO);
            }

            // Xóa cửa sổ, cửa cũ liên quan room này
            if (checkPointManager.tempDoorWindowPoints.ContainsKey(room.ID))
            {
                foreach (var (_, p1, p2) in checkPointManager.tempDoorWindowPoints[room.ID])
                {
                    if (p1 != null) Destroy(p1);
                    if (p2 != null) Destroy(p2);
                }
                checkPointManager.tempDoorWindowPoints.Remove(room.ID);
            }

            // Xóa mesh cũ của room này
            var oldFloor = GameObject.Find($"RoomFloor_{room.ID}");
            if (oldFloor != null) Destroy(oldFloor);
        }

        // === Rebuild lại toàn bộ các room vừa chia mới ===
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];

            // === Tạo lại checkpoint GameObject từ room.checkpoints
            List<GameObject> loopGO = new List<GameObject>();
            foreach (var pt in room.checkpoints)
            {
                Vector3 worldPos = new Vector3(pt.x, 0, pt.y);
                GameObject cp = Instantiate(checkPointManager.checkpointPrefab, worldPos, Quaternion.identity);
                loopGO.Add(cp);
            }

            checkPointManager.AllCheckpoints.Add(loopGO);
            checkPointManager.loopMappings.Add(new LoopMap(room.ID, loopGO));

            // === Tạo lại mesh sàn
            GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
            floorGO.transform.position = Vector3.zero;
            var meshCtrl = floorGO.AddComponent<RoomMeshController>();

            Color floorColor = (colors != null && i < colors.Length) ? colors[i] : Color.white;
            meshCtrl.Initialize(room.ID, floorColor);

            // === Vẽ lại wallLines
            foreach (var wl in room.wallLines)
            {
                checkPointManager.DrawingTool.currentLineType = wl.type;
                checkPointManager.DrawingTool.DrawLineAndDistance(wl.start, wl.end);

                // Nếu là cửa/cửa sổ
                if (wl.type == LineType.Door || wl.type == LineType.Window)
                {
                    GameObject p1 = Instantiate(checkPointManager.checkpointPrefab, wl.start, Quaternion.identity);
                    GameObject p2 = Instantiate(checkPointManager.checkpointPrefab, wl.end, Quaternion.identity);
                    p1.name = $"{wl.type}_P1";
                    p2.name = $"{wl.type}_P2";

                    if (!checkPointManager.tempDoorWindowPoints.ContainsKey(room.ID))
                        checkPointManager.tempDoorWindowPoints[room.ID] = new List<(WallLine, GameObject, GameObject)>();

                    checkPointManager.tempDoorWindowPoints[room.ID].Add((wl, p1, p2));
                }
            }
        }

        Debug.Log($"[RebuildSplitRoom] Đã build lại {rooms.Count} phòng từ group {groupID}.");
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
            if (checkPointManager.tempDoorWindowPoints.ContainsKey(room.ID))
            {
                foreach (var (_, p1, p2) in checkPointManager.tempDoorWindowPoints[room.ID])
                {
                    if (p1 != null) Destroy(p1);
                    if (p2 != null) Destroy(p2);
                }
                checkPointManager.tempDoorWindowPoints.Remove(room.ID);
            }
        }
    }
}
