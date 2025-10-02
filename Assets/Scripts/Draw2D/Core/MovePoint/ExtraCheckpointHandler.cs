using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class ExtraCheckpointHandler
{
    private readonly CheckpointManager checkPointManager;
    private readonly MovePointManager movePointManager;
    private readonly SplitRoomManager splitRoomManager;
    public ExtraCheckpointHandler(CheckpointManager cpm, MovePointManager mover, SplitRoomManager split)
    {
        this.checkPointManager = cpm;
        this.movePointManager = mover;
        this.splitRoomManager = split;
    }
    public bool MoveSelectedCheckpointExtra()
    {
        if (Input.touchCount >= 2)
        {
            if (checkPointManager.selectedCheckpoint != null)
                checkPointManager.DeselectCheckpoint();
            return false;
        }
        Vector3 newPosition = checkPointManager.GetWorldPositionFromScreen(Input.mousePosition);
        Vector3 oldWorldPos = checkPointManager.selectedCheckpoint.transform.position;

        checkPointManager.isMovingCheckpoint = true;

        if (!checkPointManager.selectedCheckpoint.CompareTag("CheckpointExtra"))
            return false;

        string roomID = checkPointManager.FindRoomIDByPoint(oldWorldPos);
        if (string.IsNullOrEmpty(roomID))
            return false;

        Room room = RoomStorage.GetRoomByID(roomID);
        if (room == null || !checkPointManager.RoomFloorMap.TryGetValue(room.ID, out GameObject floorGO))
            return false;

        Vector2 new2D = new Vector2(newPosition.x, newPosition.z);
        Vector2 local2D = new2D - new Vector2(floorGO.transform.position.x, floorGO.transform.position.z);
        Vector2 oldLocal2D = new Vector2(oldWorldPos.x, oldWorldPos.z) - new Vector2(floorGO.transform.position.x, floorGO.transform.position.z);

        if (!CheckpointManager.IsPointInPolygon(new2D, room.checkpoints))
        {
            Debug.LogWarning("Không cho phép kéo CheckpointExtra ra ngoài room.");
            return false;
        }

        float minDist = float.MaxValue;
        int insertIndex = -1;
        float maxSnapDistance = 0.3f;

        // xét snap vào cạnh: convert extra -> main
        for (int i = 0; i < room.checkpoints.Count; i++)
        {
            Vector2 a = room.checkpoints[i];
            Vector2 b = room.checkpoints[(i + 1) % room.checkpoints.Count];
            Vector2 ab = b - a;
            Vector2 ap = new2D - a;

            float abLength = ab.magnitude;
            if (abLength < 0.0001f) continue;

            float projection = Vector2.Dot(ap, ab) / abLength;
            float t = projection / abLength;

            if (t >= 0f && t <= 1f)
            {
                Vector2 projectedPoint = a + ab * t;
                float dist = Vector2.Distance(new2D, projectedPoint);

                if (dist < minDist && dist < maxSnapDistance)
                {
                    minDist = dist;
                    insertIndex = i + 1;
                }
            }
        }

        if (insertIndex != -1)
        {
            Debug.Log("Tích trữ wall line");
            SplitRoomCommand splitRoomCommand = new();

            // TÍCH TRỮ TẤT CẢ MANUAL LINES TRƯỚC KHI REBUILD
            var manualBefore = new List<WallLine>();
            foreach (var w in room.wallLines)
                if (w.isManualConnection) manualBefore.Add(CloneWallLine(w));

            // Tính worldPos sau khi move + cập nhật selected GO
            Vector3 worldPosAfterMove = RoomToWorld(local2D, floorGO);

            // Cập nhật selected checkpoint GO thành checkpoint chính
            room.checkpoints.Insert(insertIndex, local2D);
            checkPointManager.selectedCheckpoint.transform.position = worldPosAfterMove;
            checkPointManager.selectedCheckpoint.tag = "Untagged";
            checkPointManager.selectedCheckpoint.transform.SetParent(null);

            EnsureCheckpointGORegistered(room.ID, checkPointManager.selectedCheckpoint);

            // Đồng bộ loop GO polygon (để sau còn snap theo GO)
            var loop = checkPointManager.AllCheckpoints.Find(l => checkPointManager.FindRoomIDForLoop(l) == room.ID);
            if (loop != null) loop.Insert(insertIndex, checkPointManager.selectedCheckpoint);

            // REMAP CÁC MANUAL LINES THEO OLD->NEW + SNAP VỀ CHECKPOINT GO
            foreach (var ml in manualBefore)
            {
                RemapManualLineEndpoints(ml, room.ID, oldWorldPos, worldPosAfterMove, checkPointManager.selectedCheckpoint);
            }

            // REBUILD PERIMETER (chỉ làm tường chính)
            movePointManager.FastRebuildPerimeter(room.ID, loop);

            // GHÉP THÊM MANUAL LINES TRỞ LẠI SAU KHI REBUILD
            var merged = new List<WallLine>();
            foreach (var w in room.wallLines) merged.Add(w);

            // chỉ xem là trùng nếu CẢ HAI đều là manual
            bool IsSameLineManual(WallLine a, WallLine b)
            {
                if (!(a.isManualConnection && b.isManualConnection)) return false;
                const float eps = 0.001f;
                bool sameDir = Vector3.Distance(a.start, b.start) < eps && Vector3.Distance(a.end, b.end) < eps;
                bool oppDir = Vector3.Distance(a.start, b.end) < eps && Vector3.Distance(a.end, b.start) < eps;
                return (sameDir || oppDir) && a.type == b.type;
            }

            foreach (var ml in manualBefore)
            {
                bool dupManual = merged.Any(w => IsSameLineManual(ml, w));
                if (!dupManual) merged.Add(ml); // giữ manual ngay cả khi trùng perimeter
            }
            room.wallLines = merged;

            MaybeSplitByManualDiagonal(room, floorGO);

            // Lưu + vẽ lại
            RoomStorage.UpdateOrAddRoom(room);
            floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
            checkPointManager.ClearAllLines();
            checkPointManager.RedrawAllRooms();
            Debug.Log("Tích trữ wall line hoàn tấc");


            splitRoomCommand.InitNewRoomsData();
            UndoRedoController.Instance.AddToUndo(splitRoomCommand);
            return true;
        }

        else
        {
            const float tolLine = 0.15f; // cập nhật line-end

            // xác định entry extra đang kéo theo oldLocal2D
            int movingIdx = -1;
            float bestOld = float.MaxValue;
            for (int i = 0; i < room.extraCheckpoints.Count; i++)
            {
                float d = Vector2.Distance(room.extraCheckpoints[i], oldLocal2D);
                if (d < bestOld) { bestOld = d; movingIdx = i; }
            }
            if (movingIdx == -1)
            {
                Debug.LogWarning("[MoveExtra] Không tìm thấy extra theo oldLocal2D.");
                return false;
            }

            // cập nhật vị trí selected extra
            Vector3 worldPosAfterMove = RoomToWorld(local2D, floorGO);
            room.extraCheckpoints[movingIdx] = local2D;
            checkPointManager.selectedCheckpoint.transform.position = worldPosAfterMove;

            // cập nhật line-end đang dính vào vị trí cũ
            foreach (var line in room.wallLines)
            {
                if (!line.isManualConnection) continue;
                if (Vector3.Distance(line.start, oldWorldPos) < tolLine) line.start = worldPosAfterMove;
                if (Vector3.Distance(line.end, oldWorldPos) < tolLine) line.end = worldPosAfterMove;
            }
            // Sau khối cập nhật vị trí và line dính oldWorldPos
            if (TrySnapExtra(room, floorGO, movingIdx, checkPointManager.selectedCheckpoint, tolExtra: 0.12f, tolMain: 0.12f, tolLine: 0.15f))
            {
                // Nếu snap xảy ra -> lưu & vẽ lại, rồi return
                MaybeSplitByManualDiagonal(room, floorGO);
                RoomStorage.UpdateOrAddRoom(room);
                checkPointManager.ClearAllLines();
                checkPointManager.RedrawAllRooms();
                return true;
            }

            MaybeSplitByManualDiagonal(room, floorGO);

            RoomStorage.UpdateOrAddRoom(room);
            checkPointManager.ClearAllLines();
            checkPointManager.RedrawAllRooms();
            return true;
        }
    }
    
    private bool TrySnapExtra(Room room, GameObject floorGO, int movingIdx, GameObject movingGO, float tolExtra = 0.12f, float tolMain = 0.12f, float tolLine = 0.15f)
    {
        if (room == null || floorGO == null || movingGO == null) return false;

        // Vị trí của extra đang kéo (world & local)
        Vector3 movingWorld = movingGO.transform.position;
        Vector2 movingLocal = new Vector2(movingWorld.x - floorGO.transform.position.x,
                                          movingWorld.z - floorGO.transform.position.z);

        // === 1) Thử snap vào MAIN checkpoint gần nhất trước (ưu tiên) ===
        Vector3 bestMainWorld = Vector3.zero;
        float bestMainD = float.MaxValue;
        bool hasMainNear = false;

        // Tìm trong GO trước (đã render sẵn)
        if (movePointManager.placedPointsByRoom.TryGetValue(room.ID, out var goList) && goList != null)
        {
            foreach (var go in goList)
            {
                if (!go || go.CompareTag("CheckpointExtra")) continue; // chỉ main
                float d = Vector3.Distance(go.transform.position, movingWorld);
                if (d <= tolMain && d < bestMainD)
                {
                    bestMainD = d;
                    bestMainWorld = go.transform.position;
                    hasMainNear = true;
                }
            }
        }

        // Nếu không thấy main GO đủ gần, có thể check theo room.checkpoints (hình học)
        if (!hasMainNear && room.checkpoints != null && room.checkpoints.Count > 0)
        {
            foreach (var cp in room.checkpoints)
            {
                Vector3 cpWorld = new Vector3(cp.x, 0, cp.y) + floorGO.transform.position;
                float d = Vector3.Distance(cpWorld, movingWorld);
                if (d <= tolMain && d < bestMainD)
                {
                    bestMainD = d;
                    bestMainWorld = cpWorld;
                    hasMainNear = true;
                }
            }
        }

        if (hasMainNear)
        {
            // Dồn mọi line-end đang dính vào movingWorld về bestMainWorld
            foreach (var line in room.wallLines)
            {
                if (!line.isManualConnection) continue;
                if (Vector3.Distance(line.start, movingWorld) <= tolLine) line.start = bestMainWorld;
                if (Vector3.Distance(line.end, movingWorld) <= tolLine) line.end = bestMainWorld;
            }

            // Xoá entry extra tương ứng (movingIdx)
            if (movingIdx >= 0 && movingIdx < room.extraCheckpoints.Count)
                room.extraCheckpoints.RemoveAt(movingIdx);

            // Gỡ GO extra đang kéo khỏi danh sách & Destroy
            if (movePointManager.placedPointsByRoom.TryGetValue(room.ID, out var listGO) && listGO != null)
                listGO.Remove(movingGO);
            UnityEngine.Object.Destroy(movingGO);

            // Clear selection để tránh dùng GO đã bị destroy
            if (checkPointManager.selectedCheckpoint == movingGO)
                checkPointManager.selectedCheckpoint = null;

            return true;
        }

        // === 2) Không có main gần -> thử snap vào EXTRA khác ===
        GameObject bestExtraGO = null;
        float bestExtraD = float.MaxValue;

        if (movePointManager.placedPointsByRoom.TryGetValue(room.ID, out var list) && list != null)
        {
            foreach (var go in list)
            {
                if (!go || go == movingGO) continue;
                if (!go.CompareTag("CheckpointExtra")) continue; // chỉ extra
                float d = Vector3.Distance(go.transform.position, movingWorld);
                if (d <= tolExtra && d < bestExtraD)
                {
                    bestExtraD = d;
                    bestExtraGO = go;
                }
            }
        }

        if (bestExtraGO != null)
        {
            Vector3 targetWorld = bestExtraGO.transform.position;

            // Dồn line-end từ movingWorld về targetWorld
            foreach (var line in room.wallLines)
            {
                if (!line.isManualConnection) continue;
                if (Vector3.Distance(line.start, movingWorld) <= tolLine) line.start = targetWorld;
                if (Vector3.Distance(line.end, movingWorld) <= tolLine) line.end = targetWorld;
            }

            // Xoá entry extra tương ứng (movingIdx)
            if (movingIdx >= 0 && movingIdx < room.extraCheckpoints.Count)
                room.extraCheckpoints.RemoveAt(movingIdx);

            // Gỡ GO moving khỏi list & Destroy
            if (movePointManager.placedPointsByRoom.TryGetValue(room.ID, out var listGO) && listGO != null)
                listGO.Remove(movingGO);
            UnityEngine.Object.Destroy(movingGO);

            if (checkPointManager.selectedCheckpoint == movingGO)
                checkPointManager.selectedCheckpoint = null;

            return true;
        }

        return false; // không snap được
    }

    // clone 1 WallLine
    private WallLine CloneWallLine(WallLine s)
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

    // remap một manual line theo cụm thay đổi old->new và snap đầu mút về checkpoint GO gần nhất
    private void RemapManualLineEndpoints(WallLine ml, string roomId, Vector3 oldWorldPos, Vector3 newWorldPos, GameObject selectedCp)
    {
        const float tolLine = 0.18f; // hơi rộng một chút cho chắc
                                     // Nếu đầu mút đang ở gần oldWorldPos thì dời sang newWorldPos
        if (Vector3.Distance(ml.start, oldWorldPos) < tolLine) ml.start = newWorldPos;
        if (Vector3.Distance(ml.end, oldWorldPos) < tolLine) ml.end = newWorldPos;
    }

    private void EnsureCheckpointGORegistered(string roomId, GameObject cpGO)
    {
        if (!cpGO) return;
        if (!movePointManager.placedPointsByRoom.TryGetValue(roomId, out var list) || list == null)
        {
            movePointManager.placedPointsByRoom[roomId] = new List<GameObject> { cpGO };
            return;
        }
        if (!list.Contains(cpGO)) list.Add(cpGO);
    }

    // TRUE nếu có manual line mà CẢ HAI đầu đang "đứng" ở checkpoint MAIN (theo GO, tag != CheckpointExtra)
    private bool MaybeSplitByManualDiagonal(Room room, GameObject floorGO, float tolWorld = 0.30f)
    {
        if (room == null) return false;
        if (!movePointManager.placedPointsByRoom.TryGetValue(room.ID, out var goList) || goList == null || goList.Count == 0)
            return false;

        float eps = Mathf.Max(1e-4f, tolWorld * 0.1f);
        float meetTol = tolWorld;
        float nearTol = tolWorld * 0.75f;

        // --- Chuẩn hoá mặt phẳng: ép Y về cùng mặt phẳng ---
        float planeY = floorGO != null ? floorGO.transform.position.y : 0f;
        Vector3 Flatten(Vector3 v) => new Vector3(v.x, planeY, v.z);

        // --- Tập MAIN từ room.checkpoints---
        var mainWorld = new List<Vector3>(room.checkpoints.Count);
        foreach (var cp in room.checkpoints)
            mainWorld.Add(new Vector3(cp.x, planeY, cp.y));

        // --- Tập EXTRA từ goList ---
        var extraWorld = new List<Vector3>();
        foreach (var go in goList)
            if (go && go.CompareTag("CheckpointExtra"))
                extraWorld.Add(Flatten(go.transform.position));

        //tìm khoảng cách gần nhất tới bộ MAIN/EXTRA ---
        float MinDistTo(IList<Vector3> pts, Vector3 p)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < pts.Count; i++)
                best = Mathf.Min(best, Vector3.Distance(pts[i], p));
            return best;
        }

        bool IsNearMain(Vector3 p) => MinDistTo(mainWorld, Flatten(p)) <= nearTol;
        bool IsNearExtra(Vector3 p) => MinDistTo(extraWorld, Flatten(p)) <= nearTol;

        // --- Intersection XZ có dung sai ---
        bool SegmentsIntersectXZ_tol(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2)
        {
            Vector2 p = new Vector2(a1.x, a1.z);
            Vector2 r = new Vector2(a2.x - a1.x, a2.z - a1.z);
            Vector2 q = new Vector2(b1.x, b1.z);
            Vector2 s = new Vector2(b2.x - b1.x, b2.z - b1.z);

            float rxs = r.x * s.y - r.y * s.x;
            float q_pxr = (q.x - p.x) * r.y - (q.y - p.y) * r.x;

            // Gần song song: coi như không cắt trừ khi đầu mút gần nhau
            if (Mathf.Abs(rxs) <= eps)
            {
                // nếu 2 đoạn gần đồng tuyến, kiểm tra “đầu mút gần nhau” để coi như cắt
                if (Vector3.Distance(Flatten(a1), Flatten(b1)) <= meetTol) return true;
                if (Vector3.Distance(Flatten(a1), Flatten(b2)) <= meetTol) return true;
                if (Vector3.Distance(Flatten(a2), Flatten(b1)) <= meetTol) return true;
                if (Vector3.Distance(Flatten(a2), Flatten(b2)) <= meetTol) return true;
                return false;
            }

            float t = ((q.x - p.x) * s.y - (q.y - p.y) * s.x) / rxs;
            float u = q_pxr / rxs;

            // Cho phép vượt biên 1 epsilon nhỏ
            return (t >= -eps && t <= 1f + eps && u >= -eps && u <= 1f + eps);
        }

        // --- Gom & phân loại tất cả manual lines trước, không return sớm ---
        bool shouldSplit = false;

        var oneMainLines = new List<(WallLine line, bool startIsMain)>(); // line có đúng 1 đầu MAIN

        foreach (var line in room.wallLines)
        {
            if (!line.isManualConnection || !line.isVisible) continue;

            Vector3 A = Flatten(line.start);
            Vector3 B = Flatten(line.end);

            bool aMain = IsNearMain(A);
            bool bMain = IsNearMain(B);
            bool aExtra = !aMain && IsNearExtra(A);
            bool bExtra = !bMain && IsNearExtra(B);

            // CASE 1: cả hai đầu đều main
            if (aMain && bMain) { shouldSplit = true; continue; }

            // CASE 2: đúng 1 đầu main
            if (aMain ^ bMain)
            {
                // Cho phép: đầu kia có thể là Extra hoặc "không phân loại được" nhưng vẫn coi là hợp lệ để tách
                shouldSplit = true; // vẫn bật cờ tách ngay cho case 2
                                    // Đồng thời lưu lại để phục vụ CASE 4
                bool startIsMain = aMain;
                oneMainLines.Add((line, startIsMain));
                continue;
            }

            // CASE 3: cả hai đầu không main (có thể là extra-extra)
            if (!aMain && !bMain)
            {
                // Nếu cả hai gần vertex polygon (MAIN), coi như nối 2 vertex phụ thuộc vào polygon
                bool aNearVertex = MinDistTo(mainWorld, A) <= nearTol;
                bool bNearVertex = MinDistTo(mainWorld, B) <= nearTol;
                if (aNearVertex && bNearVertex) { shouldSplit = true; continue; }
            }
        }

        // CASE 4: "phụ nối phụ"
        // Hai line mỗi line có đúng 1 đầu MAIN; nếu hai đầu EXTRA của chúng gặp nhau hoặc hai đoạn cắt nhau -> tách.
        for (int i = 0; i < oneMainLines.Count; i++)
        {
            var L1 = oneMainLines[i];
            Vector3 L1Extra = L1.startIsMain ? Flatten(L1.line.end) : Flatten(L1.line.start);

            for (int j = i + 1; j < oneMainLines.Count; j++)
            {
                var L2 = oneMainLines[j];
                Vector3 L2Extra = L2.startIsMain ? Flatten(L2.line.end) : Flatten(L2.line.start);

                bool extraMeet = Vector3.Distance(L1Extra, L2Extra) <= meetTol;
                bool linesCross = SegmentsIntersectXZ_tol(
                                    Flatten(L1.line.start), Flatten(L1.line.end),
                                    Flatten(L2.line.start), Flatten(L2.line.end));

                if (extraMeet || linesCross)
                {
                    shouldSplit = true;
                }
            }
        }

        if (shouldSplit)
        {
            splitRoomManager?.DetectAndSplitRoomIfNecessary(room);

        int removed = DeleteLonelyExtras(
            room, floorGO,
            tolConnect: 0.08f,
            includePerimeter: false,   // chỉ tính manual nếu bạn muốn giữ extra sát tường
            useAllRoomsLines: true     // QUAN TRỌNG: kiểm tra với toàn bộ line trong scene
        );
            return true;
        }

        return false;
    }
    
    private static bool NearlySameXZ(Vector3 a, Vector3 b, float tol)
        => (new Vector2(a.x - b.x, a.z - b.z)).sqrMagnitude <= tol * tol;

    private static Vector3 ToWorld(Vector2 local, GameObject floorGO, float planeY)
        => new Vector3(local.x + floorGO.transform.position.x, planeY, local.y + floorGO.transform.position.z);

    private static float DistancePointToSegmentXZ(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector2 A = new Vector2(a.x, a.z);
        Vector2 B = new Vector2(b.x, b.z);
        Vector2 P = new Vector2(p.x, p.z);
        var AB = B - A;
        float denom = Vector2.Dot(AB, AB);
        if (denom <= Mathf.Epsilon) return Vector2.Distance(P, A);
        float t = Mathf.Clamp01(Vector2.Dot(P - A, AB) / denom);
        Vector2 proj = A + t * AB;
        return Vector2.Distance(P, proj);
    }

    // Kiểm tra "có dính line" (endpoints hoặc nằm sát đoạn) — có thể chọn có/không tính perimeter
    private bool IsConnectedToAnyLine(Vector3 eWorld, IEnumerable<WallLine> lines, float tol, bool includePerimeter)
    {
        if (lines == null) return false;
        foreach (var wl in lines)
        {
            if (!includePerimeter && !wl.isManualConnection) continue; // chỉ tính manual khi includePerimeter=false
            Vector3 s = wl.start, t = wl.end;

            // gần đầu mút
            if (Vector2.Distance(new Vector2(eWorld.x, eWorld.z), new Vector2(s.x, s.z)) <= tol) return true;
            if (Vector2.Distance(new Vector2(eWorld.x, eWorld.z), new Vector2(t.x, t.z)) <= tol) return true;

            // gần đoạn
            if (DistancePointToSegmentXZ(s, t, eWorld) <= tol) return true;
        }
        return false;
    }
    public int DeleteLonelyExtras(
        Room room,
        GameObject floorGO,
        float tolConnect = 0.08f,
        bool includePerimeter = false,
        bool useAllRoomsLines = true   // NEW: kiểm tra với mọi room
    )
    {
        if (room == null || floorGO == null) return 0;

        float planeY = floorGO.transform.position.y;

        // 0) Chọn pool line để kiểm tra kết nối
        IEnumerable<WallLine> linePool;
        if (useAllRoomsLines)
        {
            var allRooms = RoomStorage.GetAllRooms() ?? new List<Room>();
            linePool = allRooms.SelectMany(r => r.wallLines ?? Enumerable.Empty<WallLine>());
        }
        else
        {
            linePool = room.wallLines ?? Enumerable.Empty<WallLine>();
        }

        // Nếu chưa có line nào (rebuild chưa xong) => không dọn để tránh xóa nhầm
        if (!linePool.Any())
            return 0;

        // 1) Gom tất cả EXTRA ở WORLD (data + GO)
        var extraWorld = new List<Vector3>();
        if (room.extraCheckpoints != null)
            foreach (var e in room.extraCheckpoints)
                extraWorld.Add(ToWorld(e, floorGO, planeY));

        if (movePointManager.placedPointsByRoom != null && movePointManager.placedPointsByRoom.TryGetValue(room.ID, out var goList) && goList != null)
            foreach (var go in goList)
                if (go && go.CompareTag("CheckpointExtra"))
                {
                    var pw = new Vector3(go.transform.position.x, planeY, go.transform.position.z);
                    if (!extraWorld.Any(p => NearlySameXZ(p, pw, 1e-4f))) extraWorld.Add(pw);
                }

        if (extraWorld.Count == 0) return 0;

        // 2) Tìm các extra thật sự "đứng một mình" trong TOÀN SCENE
        var lonely = new List<Vector3>();
        foreach (var eW in extraWorld)
        {
            bool connected = IsConnectedToAnyLine(eW, linePool, tolConnect, includePerimeter);
            if (!connected) lonely.Add(eW);
        }
        if (lonely.Count == 0) return 0;

        // 3) Xóa trong DATA (local)
        if (room.extraCheckpoints != null)
        {
            for (int i = room.extraCheckpoints.Count - 1; i >= 0; i--)
            {
                var lw = ToWorld(room.extraCheckpoints[i], floorGO, planeY);
                if (lonely.Any(p => NearlySameXZ(p, lw, tolConnect)))
                    room.extraCheckpoints.RemoveAt(i);
            }
        }

        // 4) Xóa GO
        if (movePointManager.placedPointsByRoom != null && movePointManager.placedPointsByRoom.TryGetValue(room.ID, out var listGO) && listGO != null)
        {
            for (int i = listGO.Count - 1; i >= 0; i--)
            {
                var go = listGO[i];
                if (!go || !go.CompareTag("CheckpointExtra")) continue;

                var gw = new Vector3(go.transform.position.x, planeY, go.transform.position.z);
                if (lonely.Any(p => NearlySameXZ(p, gw, tolConnect)))
                {
                    UnityEngine.Object.Destroy(go);
                    listGO.RemoveAt(i);
                }
            }
            if (listGO.Count == 0) movePointManager.placedPointsByRoom.Remove(room.ID);
        }

        return lonely.Count;
    }

    private static Vector3 RoomToWorld(Vector2 localPos, GameObject floorGO)
    {
        return new Vector3(localPos.x, 0, localPos.y) + floorGO.transform.position;
    }
}
