using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using System;

public class MovePointManager : MonoBehaviour
{
    private float WELD_ON = 0.5f;    // <= khoảng này thì dính + snap trùng
    private float WELD_OFF = 0.6f;    // > khoảng này thì tách
    public Dictionary<string, List<GameObject>> ExtraCheckpointVisuals = new Dictionary<string, List<GameObject>>();
    static bool IsPerimeter(WallLine l) => l.type == LineType.Wall && !l.isManualConnection;


    public Dictionary<string, List<GameObject>> placedPointsByRoom = new();
    Dictionary<string, GameObject> RoomFloorMap = new();

    private CheckpointManager checkPointManager;
    private SplitRoomManager splitRoomManager;

    private bool _magnetLatch = false;

    void Start()
    {
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
        splitRoomManager = FindFirstObjectByType<SplitRoomManager>();
    }

    //  WELD CLUSTER (nhiều-điểm)
    // 1 điểm có thể dính với n điểm khác
    private readonly Dictionary<GameObject, HashSet<GameObject>> _weldAdj = new();

    private void AddEdge(GameObject a, GameObject b)
    {
        if (a == null || b == null || a == b) return;
        if (!_weldAdj.TryGetValue(a, out var sa)) { sa = new HashSet<GameObject>(); _weldAdj[a] = sa; }
        if (!_weldAdj.TryGetValue(b, out var sb)) { sb = new HashSet<GameObject>(); _weldAdj[b] = sb; }
        sa.Add(b); sb.Add(a);
    }

    private void RemoveEdge(GameObject a, GameObject b)
    {
        if (a == null || b == null) return;
        if (_weldAdj.TryGetValue(a, out var sa)) sa.Remove(b);
        if (_weldAdj.TryGetValue(b, out var sb)) sb.Remove(a);
    }
    
    private IEnumerable<GameObject> Neighbors(GameObject a)
    {
        if (a != null && _weldAdj.TryGetValue(a, out var sa))
            foreach (var x in sa) if (x != null) yield return x;
    }

    private static float XZDist(Vector3 a, Vector3 b)
        => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private List<GameObject> GetLoopByRoomID(string roomID)
    {
        foreach (var lp in checkPointManager.AllCheckpoints)
            if (checkPointManager.FindRoomIDForLoop(lp) == roomID) return lp;
        return null;
    }
    private List<GameObject> FindLoopContains(GameObject go)
    {
        foreach (var lp in checkPointManager.AllCheckpoints) if (lp.Contains(go)) return lp;
        return null;
    }

    private bool IsClickingOnBackgroundBlackUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;
        var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var result in results)
            if (result.gameObject.name == "Background Black") return true;
        return false;
    }

    private WallLine FindClosestWallLine(WallLine doorLine, string roomID)
    {
        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null) return null;

        WallLine closest = null; float minDist = float.MaxValue;
        foreach (var wall in room.wallLines)
        {
            if (wall.type != LineType.Wall) continue;
            float dist = GetDistanceFromSegment(doorLine.start, wall.start, wall.end)
                       + GetDistanceFromSegment(doorLine.end,   wall.start, wall.end);
            if (dist < minDist) { minDist = dist; closest = wall; }
        }
        return closest;
    }

    private float GetRatioAlongLine(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a; Vector3 ap = point - a;
        return Vector3.Dot(ap, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude);
    }
    private float GetDistanceFromSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 projected = checkPointManager.ProjectPointOnLineSegment(a, b, point);
        return Vector3.Distance(point, projected);
    }

    private bool TryGetNearestNeighborDelta(
        Vector3 endpointOldPos,
        Dictionary<GameObject, Vector3> neighborOldPos,
        Dictionary<GameObject, Vector3> neighborDelta,
        float radius,
        out Vector3 deltaOut)
    {
        deltaOut = Vector3.zero;
        float best = radius;
        foreach (var kv in neighborOldPos)
        {
            float d = XZDist(endpointOldPos, kv.Value);
            if (d < best)
            {
                best = d;
                deltaOut = neighborDelta.TryGetValue(kv.Key, out var dd) ? dd : Vector3.zero;
            }
        }
        return best < radius;
    }

    // MOVE POINT (WELD CLUSTER)
    public void MoveSelectedCheckpoint()
    {
        if (IsClickingOnBackgroundBlackUI(Input.mousePosition)) return;
        if (checkPointManager.selectedCheckpoint == null) return;

        // === Nếu là checkpoint phụ (CheckpointExtra) ===
        if (checkPointManager.selectedCheckpoint.CompareTag("CheckpointExtra"))
        {
            if (MoveSelectedCheckpointExtra()) return;
        }

        Vector3 newPosition = checkPointManager.GetWorldPositionFromScreen(Input.mousePosition);
        var selected = checkPointManager.selectedCheckpoint;
        Vector3 oldPos = selected.transform.position;
        Vector3 selectedDelta = newPosition - oldPos;

        // === Nếu là điểm cửa/cửa sổ ===
        foreach (var kvp in checkPointManager.tempDoorWindowPoints)
        {
            foreach (var (line, p1GO, p2GO) in kvp.Value)
            {
                if (selected == p1GO || selected == p2GO)
                {
                    WallLine wall = FindClosestWallLine(line, kvp.Key);
                    if (wall == null) return;

                    Vector3 projected = checkPointManager.ProjectPointOnLineSegment(wall.start, wall.end, newPosition);
                    if (selected == p1GO) line.start = projected; else line.end = projected;

                    selected.transform.position = projected;
                    checkPointManager.RedrawAllRooms();
                    return;
                }
            }
        }

        // === Nếu là checkpoint chính trong polygon
        selected.transform.position = newPosition;

        foreach (var loop in checkPointManager.AllCheckpoints)
        {
            if (!loop.Contains(selected)) continue;

            string roomID = checkPointManager.FindRoomIDForLoop(loop);
            if (string.IsNullOrEmpty(roomID)) return;
            Room room = RoomStorage.GetRoomByID(roomID);
            if (room == null) return;

            // WELD / SNAP (CLUSTER)
            // Ghi lại vị trí cũ của tất cả neighbor đang dính để tính delta riêng
            var neighbors = new HashSet<GameObject>(Neighbors(selected));
            var neighborOldPos = new Dictionary<GameObject, Vector3>(neighbors.Count);
            foreach (var n in neighbors) if (n != null) neighborOldPos[n] = n.transform.position;

            // Danh sách điểm cùng loop sẽ xóa (không sửa list trong foreach)
            var sameLoopToRemove = new List<GameObject>();

            // A) Duy trì hysteresis cho các neighbor hiện có
            foreach (var n in neighbors.ToList())
            {
                if (n == null) { RemoveEdge(selected, n); neighbors.Remove(n); continue; }

                float d = XZDist(selected.transform.position, n.transform.position);
                bool isSameLoop = FindLoopContains(n) == loop;

                // NEW: Nếu là cùng phòng và đã chạm (<= WELD_ON) thì gộp về selected bằng cách xóa n
                if (isSameLoop && n != selected && d <= WELD_ON)
                {
                    if (loop.Count - sameLoopToRemove.Count > 3) // giữ >=3 đỉnh
                    {
                        sameLoopToRemove.Add(n);
                        neighbors.Remove(n);
                        neighborOldPos.Remove(n);
                        continue;
                    }
                    // nếu chỉ còn 3 đỉnh thì KHÔNG xóa; rơi xuống dưới xử lý bình thường
                }

                if (d <= WELD_ON)
                {
                    // khác phòng: snap trùng selected
                    n.transform.position = selected.transform.position;
                }
                else if (d > WELD_OFF)
                {
                    RemoveEdge(selected, n);
                    neighbors.Remove(n);
                    neighborOldPos.Remove(n);
                }
                else
                {
                    // vùng trễ: kéo theo delta của selected
                    n.transform.position += selectedDelta;
                }
            }

            // B) Tạo weld mới với MỌI điểm trong WELD_ON
            foreach (var lp in checkPointManager.AllCheckpoints)
            {
                foreach (var cp in lp)
                {
                    if (cp == selected) continue;
                    if (neighbors.Contains(cp)) continue;

                    float d = XZDist(selected.transform.position, cp.transform.position);
                    if (d > WELD_ON) continue;

                    bool isSameLoop = (lp == loop);

                    if (isSameLoop)
                    {
                        // NEW: cùng phòng -> xóa bớt điểm trùng (giữ selected)
                        if (cp != selected && loop.Count - sameLoopToRemove.Count > 3)
                        {
                            sameLoopToRemove.Add(cp);
                        }
                        // nếu còn đúng 3 đỉnh thì thôi, không xóa để bảo toàn polygon
                    }
                    else
                    {
                        // khác phòng -> weld + snap
                        if (!neighborOldPos.ContainsKey(cp)) neighborOldPos[cp] = cp.transform.position;
                        AddEdge(selected, cp);
                        neighbors.Add(cp);
                        cp.transform.position = selected.transform.position;
                    }
                }
            }

            // THỰC XÓA các điểm cùng phòng đã gom
            if (sameLoopToRemove.Count > 0)
            {
                foreach (var cp in sameLoopToRemove)
                {
                    // cắt tất cả liên kết weld của cp
                    foreach (var nb in Neighbors(cp).ToList()) RemoveEdge(cp, nb);
                    // bỏ khỏi đồ thị
                    if (_weldAdj.ContainsKey(cp)) _weldAdj.Remove(cp);
                    // bỏ khỏi loop và hủy GO
                    loop.Remove(cp);
                    if (cp != null) Destroy(cp);
                }
            }

            // Tính delta cho từng neighbor (sau khi đã cập nhật vị trí)
            var neighborDelta = new Dictionary<GameObject, Vector3>(neighbors.Count);
            foreach (var n in neighbors)
                if (neighborOldPos.TryGetValue(n, out var o))
                    neighborDelta[n] = n.transform.position - o;

            bool isDuplicate = false;

            // 1: Ghi checkpoints từ loop (chặn duplicate XZ)
            List<Vector2> newCheckpoints = new();
            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 pos = loop[i].transform.position;
                for (int j = 0; j < i; j++)
                {
                    Vector3 otherPos = loop[j].transform.position;
                    if (XZDist(pos, otherPos) < 0.01f)
                    {
                        Debug.LogWarning($"[BỎ QUA] Điểm {i} trùng điểm {j} ➜ Không update checkpoint để tránh mesh lỗi.");
                        isDuplicate = true; break;
                    }
                }
                if (isDuplicate) break;
                newCheckpoints.Add(new Vector2(pos.x, pos.z));
            }
            if (!isDuplicate) room.checkpoints = newCheckpoints;

            // 2: Update tường viền (KHÔNG đụng manual)
            if (!isDuplicate)
            {
                int wallLineIndex = 0;
                int n = room.checkpoints.Count;
                for (int i = 0; i < room.wallLines.Count; i++)
                {
                    // if (room.wallLines[i].type != LineType.Wall) continue;
                    var wl = room.wallLines[i];
                    if (!IsPerimeter(wl)) continue;  // bỏ qua manual + door/window

                    Vector2 p1 = room.checkpoints[wallLineIndex % n];
                    Vector2 p2 = room.checkpoints[(wallLineIndex + 1) % n];
                    room.wallLines[i].start = new Vector3(p1.x, 0, p1.y);
                    room.wallLines[i].end = new Vector3(p2.x, 0, p2.y);
                    wallLineIndex++;
                }
            }

            // 3: Manual connections bám theo CẢ selected + NHIỀU partner 
            foreach (var line in room.wallLines)
            {
                bool nearSelectedStart = XZDist(line.start, oldPos) < 0.15f;
                bool nearSelectedEnd = XZDist(line.end, oldPos) < 0.15f;

                Vector3 partnerStartDelta, partnerEndDelta;
                bool nearPartnerStart = TryGetNearestNeighborDelta(line.start, neighborOldPos, neighborDelta, 0.15f, out partnerStartDelta);
                bool nearPartnerEnd = TryGetNearestNeighborDelta(line.end, neighborOldPos, neighborDelta, 0.15f, out partnerEndDelta);

                bool movedStart = nearSelectedStart || nearPartnerStart;
                bool movedEnd = nearSelectedEnd || nearPartnerEnd;

                if (movedStart && movedEnd)
                {
                    Vector3 direction = (line.end - line.start).normalized;
                    if (direction == Vector3.zero)
                    {
                        int hash = oldPos.GetHashCode();
                        direction = Quaternion.Euler(0, hash % 360, 0) * Vector3.forward;
                    }
                    line.start = selected.transform.position - direction * 0.001f;
                    line.end = selected.transform.position + direction * 0.001f;
                }
                else if (movedStart)
                {
                    line.start += nearPartnerStart ? partnerStartDelta : selectedDelta;
                }
                else if (movedEnd)
                {
                    line.end += nearPartnerEnd ? partnerEndDelta : selectedDelta;
                }
                else if (Vector3.Distance(line.start, line.end) < 0.001f)
                {
                    Vector3 direction = Quaternion.Euler(0, 137f, 0) * Vector3.forward;
                    line.start = selected.transform.position - direction * 0.001f;
                    line.end = selected.transform.position + direction * 0.001f;
                    Debug.LogWarning($"[Auto-fix] Manual line degenerate (start==end). Cưỡng bức kéo rộng tại: {selected.transform.position}");
                }
            }

            // 4: Cập nhật cửa/cửa sổ bám tường gần nhất 
            foreach (var door in room.wallLines.Where(w => w.type != LineType.Wall))
            {
                WallLine parentWall = null; float minDistance = float.MaxValue;
                foreach (var wall in room.wallLines)
                {
                    if (wall.type != LineType.Wall) continue;
                    float dist = GetDistanceFromSegment(door.start, wall.start, wall.end)
                               + GetDistanceFromSegment(door.end, wall.start, wall.end);
                    if (dist < minDistance) { minDistance = dist; parentWall = wall; }
                }
                if (parentWall == null) continue;

                float r1 = Mathf.Clamp01(GetRatioAlongLine(door.start, parentWall.start, parentWall.end));
                float r2 = Mathf.Clamp01(GetRatioAlongLine(door.end, parentWall.start, parentWall.end));
                door.start = Vector3.Lerp(parentWall.start, parentWall.end, r1);
                door.end = Vector3.Lerp(parentWall.start, parentWall.end, r2);

                if (checkPointManager.tempDoorWindowPoints.TryGetValue(room.ID, out var doorsInRoom))
                    foreach (var (line, p1GO, p2GO) in doorsInRoom)
                    { p1GO.transform.position = line.start; p2GO.transform.position = line.end; }
            }

            // 5: Lưu & redraw (phòng của selected)
            RoomStorage.UpdateOrAddRoom(room);
            var floorGO = GameObject.Find($"RoomFloor_{roomID}");
            if (floorGO != null)
                floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);

            // 6: Rebuild các phòng chứa neighbor 
            var rebuilt = new HashSet<string>();
            foreach (var nGo in neighbors)
            {
                var nLoop = FindLoopContains(nGo);
                if (nLoop == null) continue;
                string nRoomID = checkPointManager.FindRoomIDForLoop(nLoop);
                if (string.IsNullOrEmpty(nRoomID) || nRoomID == roomID || rebuilt.Contains(nRoomID)) continue;

                // FastRebuildPerimeter(nRoomID, nLoop);
                FastRebuildPerimeter(nRoomID, nLoop);
                rebuilt.Add(nRoomID);
            }

            checkPointManager.ClearAllLines();
            checkPointManager.RedrawAllRooms();
            break;
        }
    }
    public bool MoveSelectedCheckpointExtra()
    {
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

        if (!checkPointManager.IsPointInPolygon(new2D, room.checkpoints))
        {
            Debug.LogWarning("Không cho phép kéo CheckpointExtra ra ngoài room.");
            return false;
        }

        float minDist = float.MaxValue;
        int insertIndex = -1;
        float maxSnapDistance = 0.3f;

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
            // === CONVERTING EXTRA TO MAIN CHECKPOINT ===

            // Remove from extraCheckpoints
            int nearestExtraIndex = -1;
            float minDistExtra = float.MaxValue;
            for (int i = 0; i < room.extraCheckpoints.Count; i++)
            {
                float dist = Vector2.Distance(room.extraCheckpoints[i], oldLocal2D);
                if (dist < minDistExtra)
                {
                    minDistExtra = dist;
                    nearestExtraIndex = i;
                }
            }
            if (nearestExtraIndex != -1)
                room.extraCheckpoints.RemoveAt(nearestExtraIndex);

            if (placedPointsByRoom.TryGetValue(room.ID, out var checkpointList))
            {
                checkpointList.RemoveAll(go =>
                    go != null &&
                    go.CompareTag("CheckpointExtra") &&
                    Vector3.Distance(go.transform.position, oldWorldPos) < 0.05f
                );
            }

            Vector3 worldPosAfterMove = RoomToWorld(local2D, floorGO);

            // Update manual lines that connect to oldWorldPos
            foreach (var line in room.wallLines)
            {
                if (!line.isManualConnection) continue;
                if (Vector3.Distance(line.start, oldWorldPos) < 0.15f) line.start = worldPosAfterMove;
                if (Vector3.Distance(line.end, oldWorldPos) < 0.15f) line.end = worldPosAfterMove;
            }

            // Insert checkpoint into main polygon
            room.checkpoints.Insert(insertIndex, local2D);
            checkPointManager.selectedCheckpoint.transform.position = worldPosAfterMove;
            checkPointManager.selectedCheckpoint.tag = "Untagged";
            checkPointManager.selectedCheckpoint.transform.SetParent(null);

            // Update allCheckpoints list
            var loop = checkPointManager.AllCheckpoints.Find(l => checkPointManager.FindRoomIDForLoop(l) == room.ID);
            if (loop != null)
                loop.Insert(insertIndex, checkPointManager.selectedCheckpoint);

            // Rebuild perimeter walls (preserve manual)
            List<WallLine> manualLinesBackup = room.wallLines
                .Where(w => w.isManualConnection)
                .Select(w => new WallLine
                {
                    start = w.start,
                    end = w.end,
                    type = w.type,
                    isManualConnection = true
                }).ToList();

            FastRebuildPerimeter(room.ID, loop);
            room.wallLines.AddRange(manualLinesBackup);

            // Snap line endpoints to exact checkpoint positions
            if (placedPointsByRoom.TryGetValue(room.ID, out List<GameObject> checkpointGOs))
            {
                foreach (var line in room.wallLines)
                {
                    if (!line.isManualConnection) continue;

                    foreach (var cp in checkpointGOs)
                    {
                        Vector3 cpPos = cp.transform.position;
                        float tolerance = (cp == checkPointManager.selectedCheckpoint) ? 0.15f : 0.05f;

                        if (Vector3.Distance(line.start, cpPos) < tolerance) line.start = cpPos;
                        if (Vector3.Distance(line.end, cpPos) < tolerance) line.end = cpPos;
                    }
                }
            }

            // === NGAY LẬP TỨC: kiểm tra & kích hoạt tách nếu đủ điều kiện
            {
                // Duyệt snapshot để an toàn nếu helper xóa line
                var manualsNow = room.wallLines.Where(w => w.isManualConnection).ToList();
                foreach (var ml in manualsNow)
                {
                    if (HandleManualLineBetweenTwoExtras(room, floorGO, ml))
                    {
                        checkPointManager.RedrawAllRooms();
                        return true;
                    }
                }
            }

            RoomStorage.UpdateOrAddRoom(room);
            floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
            checkPointManager.ClearAllLines();
            checkPointManager.RedrawAllRooms();
            return true;
        }
        else
        {
            // === MOVING WITHIN EXTRA CHECKPOINTS ===
            int nearestExtraIndex = -1;
            float minDistExtra = float.MaxValue;

            for (int i = 0; i < room.extraCheckpoints.Count; i++)
            {
                Vector2 worldExtra = room.extraCheckpoints[i] + new Vector2(floorGO.transform.position.x, floorGO.transform.position.z);
                float dist = Vector2.Distance(new2D, worldExtra);
                if (dist < minDistExtra)
                {
                    minDistExtra = dist;
                    nearestExtraIndex = i;
                }
            }

            if (nearestExtraIndex != -1)
            {
                Vector3 worldPosAfterMove = RoomToWorld(local2D, floorGO);

                room.extraCheckpoints[nearestExtraIndex] = local2D;
                checkPointManager.selectedCheckpoint.transform.position = worldPosAfterMove;

                // Update các line thủ công gắn với điểm cũ
                foreach (var line in room.wallLines)
                {
                    if (!line.isManualConnection) continue;
                    if (Vector3.Distance(line.start, oldWorldPos) < 0.15f) line.start = worldPosAfterMove;
                    if (Vector3.Distance(line.end, oldWorldPos) < 0.15f) line.end = worldPosAfterMove;
                }

                UpdateWallLinesFromExtraCheckpoint(room, oldLocal2D, local2D, floorGO);
                UpdateExtraCheckpointVisual(room.ID, nearestExtraIndex, local2D);

                // === NGAY LẬP TỨC: kiểm tra & kích hoạt tách nếu đủ điều kiện
                var manualsNow = room.wallLines.Where(w => w.isManualConnection).ToList();
                foreach (var ml in manualsNow)
                {
                    if (HandleManualLineBetweenTwoExtras(room, floorGO, ml))
                    {
                        checkPointManager.RedrawAllRooms();
                        return true;
                    }
                }
            }

            RoomStorage.UpdateOrAddRoom(room);
            checkPointManager.ClearAllLines();
            checkPointManager.RedrawAllRooms();
            return true;
        }
    }
    private bool HandleManualLineBetweenTwoExtras(
        Room room,
        GameObject floorGO,
        WallLine newLine,
        float tolMain = 0.16f,   // ngưỡng bám đỉnh chính (snap)
        float tolExtra = 0.16f,  // (không dùng trong phiên bản robust, giữ tham số để tương thích)
        float tolPair = 0.18f,   // khoảng cách 2 free-end coi như “dính cặp”
        bool requireNonAdjacentMainPair = true)
    {
        if (room == null || floorGO == null || newLine == null) return false;
        if (!newLine.isManualConnection) return false;

        // ===== Chuẩn bị toạ độ world =====
        Vector2 floorPos = new(floorGO.transform.position.x, floorGO.transform.position.z);
        var mainWorld = room.checkpoints
                            .Select(p => new Vector3(p.x + floorPos.x, 0f, p.y + floorPos.y))
                            .ToList();

        int nMain = mainWorld.Count;
        if (nMain < 2) return false;

        // ===== util: tìm main gần nhất (SNAP vào pt cục bộ, KHÔNG đổi line khác) =====
        int NearestMainSnap(ref Vector3 pt)
        {
            int idx = -1; float best = tolMain + 1f;
            for (int i = 0; i < nMain; i++)
            {
                float d = Vector3.Distance(mainWorld[i], pt);
                if (d <= tolMain && d < best) { best = d; idx = i; }
            }
            if (idx != -1) pt = mainWorld[idx]; // SNAP
            return idx;
        }

        bool AreAdjacent(int i, int j)
        {
            if (i == -1 || j == -1 || nMain < 2) return false;
            if (i == j) return true;
            return (Mathf.Abs(i - j) == 1) || (i == 0 && j == nMain - 1) || (j == 0 && i == nMain - 1);
        }

        // ===== Phân loại 2 đầu của newLine (sau snap) =====
        Vector3 s = newLine.start, e = newLine.end;
        int si = NearestMainSnap(ref s);
        int ei = NearestMainSnap(ref e);
        newLine.start = s; newLine.end = e; // áp lại snap cho chính newLine

        bool sIsMain = si != -1;
        bool eIsMain = ei != -1;

        // ===== finalize: split + rebuild + filter manual main-main =====
        bool FinalizeNow(bool alsoDeleteLine)
        {
            bool prev = checkPointManager.isMovingCheckpoint;
            checkPointManager.isMovingCheckpoint = false;

            splitRoomManager?.DetectAndSplitRoomIfNecessary(room);
            if (alsoDeleteLine) room.wallLines.Remove(newLine);

            // Rebuild nhanh (gộp logic của bạn)
            var loop = checkPointManager.AllCheckpoints.Find(l => checkPointManager.FindRoomIDForLoop(l) == room.ID);
            FastRebuildPerimeter(room.ID, loop);

            // Lọc mọi manual mà 2 đầu đều bám main (tránh hồi sinh)
            var mainWorld2 = room.checkpoints
                .Select(p => new Vector3(p.x + floorPos.x, 0f, p.y + floorPos.y)).ToList();

            bool NearAnyMain(Vector3 v)
            {
                for (int i = 0; i < mainWorld2.Count; i++)
                    if (Vector3.Distance(v, mainWorld2[i]) <= tolMain) return true;
                return false;
            }

            room.wallLines = room.wallLines.Where(w =>
            {
                if (!w.isManualConnection) return true;
                bool atMainStart = NearAnyMain(w.start);
                bool atMainEnd = NearAnyMain(w.end);
                return !(atMainStart && atMainEnd);
            }).ToList();

            RoomStorage.UpdateOrAddRoom(room);
            floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
            checkPointManager.ClearAllLines();
            checkPointManager.RedrawAllRooms();

            checkPointManager.isMovingCheckpoint = prev;
            return true;
        }

        // ===== Rule 1: 2 đầu cùng bám 2 đỉnh chính khác nhau =====
        if (sIsMain && eIsMain && si != ei)
        {
            if (!requireNonAdjacentMainPair || !AreAdjacent(si, ei))
                return FinalizeNow(alsoDeleteLine: true);
        }

        // ===== Rule 2 (robust): đúng 1 đầu bám main; tìm 1 manual khác cũng 1-main-1-free.
        // So khoảng cách giữa HAI FREE-END; nếu <= tolPair thì tách ngay.
        if (sIsMain ^ eIsMain)
        {
            // free-end của newLine (đầu KHÔNG bám main)
            Vector3 freeA = sIsMain ? e : s;

            foreach (var l in room.wallLines)
            {
                if (!l.isManualConnection) continue;
                if (ReferenceEquals(l, newLine)) continue;

                // Bỏ qua line trùng hình học (đảo đầu) với chính nó
                bool sameGeom =
                    (Vector3.Distance(l.start, newLine.start) < 1e-4f && Vector3.Distance(l.end, newLine.end) < 1e-4f) ||
                    (Vector3.Distance(l.start, newLine.end) < 1e-4f && Vector3.Distance(l.end, newLine.start) < 1e-4f);
                if (sameGeom) continue;

                // Phân loại line l theo main (dùng bản copy để snap cục bộ)
                Vector3 ls = l.start, le = l.end;
                int lsi = NearestMainSnap(ref ls);
                int lei = NearestMainSnap(ref le);

                bool oneMainOneFree = (lsi != -1) ^ (lei != -1);
                if (!oneMainOneFree) continue;

                // free-end của line đối tác
                Vector3 freeB = (lsi != -1) ? le : ls;

                // Hai free-end “dính cặp”
                if (Vector3.Distance(freeA, freeB) <= tolPair)
                    return FinalizeNow(alsoDeleteLine: false);
            }
        }

        return false;
    }
    Vector3 RoomToWorld(Vector2 localPos, GameObject floorGO)
    {
        return new Vector3(localPos.x, 0, localPos.y) + floorGO.transform.position;
    }
    void UpdateWallLinesFromExtraCheckpoint(Room room, Vector2 oldLocal, Vector2 newLocal, GameObject floorGO)
    {
        Vector3 oldWorld = RoomToWorld(oldLocal, floorGO);
        Vector3 newWorld = RoomToWorld(newLocal, floorGO);

        foreach (var line in room.wallLines)
        {
            if (!line.isManualConnection) continue;

            if (Vector3.Distance(line.start, oldWorld) < 0.01f)
                line.start = newWorld;

            if (Vector3.Distance(line.end, oldWorld) < 0.01f)
                line.end = newWorld;
        }
    }

    void UpdateExtraCheckpointVisual(string roomID, int index, Vector2 local2D)
    {
        if (!ExtraCheckpointVisuals.TryGetValue(roomID, out var visuals))
        {
            return;
        }

        if (index < 0 || index >= visuals.Count)
        {
            return;
        }

        if (!RoomFloorMap.TryGetValue(roomID, out var floor))
        {
            return;
        }

        if (visuals[index] == null)
        {
            return;
        }

        visuals[index].transform.position = new Vector3(local2D.x, 0f, local2D.y) + floor.transform.position;
    }

    // MOVE ROOM
    public void MoveRoomSnap(string roomID, Vector3 delta)
    {
        if (delta.sqrMagnitude < 1e-10f) return;
        var loop = GetLoopByRoomID(roomID);
        if (loop == null || loop.Count == 0) return;

        // Giới hạn bước tối đa mỗi frame (mượt tay)
        const float MAX_STEP = 0.001f;
        Vector2 u = new Vector2(delta.x, delta.z);
        float mag = u.magnitude;
        if (mag > MAX_STEP) u *= (MAX_STEP / mag);

        float tLimit = 1f;
        if (u.sqrMagnitude > 1e-12f)
        {
            foreach (var a in loop)
            {
                Vector2 p = new Vector2(a.transform.position.x, a.transform.position.z);
                foreach (var lp in checkPointManager.AllCheckpoints)
                {
                    if (lp == loop) continue;
                    foreach (var b in lp)
                    {
                        Vector2 q = new Vector2(b.transform.position.x, b.transform.position.z);
                        float R = WELD_ON;
                        Vector2 w = p - q;
                        float A = Vector2.Dot(u, u);
                        float B = 2f * Vector2.Dot(u, w);
                        float C = Vector2.Dot(w, w) - R * R;

                        if (A < 1e-12f || B >= 0f) continue;
                        float disc = B * B - 4f * A * C;
                        if (disc < 0f) continue;
                        float sqrt = Mathf.Sqrt(disc);
                        float tHit = (-B - sqrt) / (2f * A);
                        if (tHit >= 0f && tHit <= 1f) tLimit = Mathf.Min(tLimit, tHit);
                    }
                }
            }
        }

        if (tLimit < 1f && !_magnetLatch) { _magnetLatch = true; return; }

        Vector3 clamped = new Vector3(u.x * tLimit, 0f, u.y * tLimit);
        if (clamped.sqrMagnitude < 1e-10f) return;

        foreach (var a in loop) a.transform.position += clamped;

        // FastRebuildPerimeter(roomID, loop);
        FastRebuildPerimeter(roomID, loop);
        checkPointManager.ClearAllLines();
        checkPointManager.RedrawAllRooms();
    }
    // Tìm tất cả cặp (a trong movingLoop, b ở phòng khác) với d <= WELD_ON
    // Mỗi a và b chỉ bắt cặp 1 lần để tránh mâu thuẫn vị trí (giữ shape tốt hơn)
    private List<(GameObject a, GameObject b, Vector3 mid)> CollectSnapPairs(List<GameObject> movingLoop)
    {
        var pairs = new List<(GameObject a, GameObject b, Vector3 mid)>();
        var usedA = new HashSet<GameObject>();
        var usedB = new HashSet<GameObject>();

        foreach (var a in movingLoop)
        {
            GameObject bestB = null;
            float bestD = WELD_ON;

            foreach (var lp in checkPointManager.AllCheckpoints)
            {
                if (lp == movingLoop) continue;
                foreach (var b in lp)
                {
                    if (usedB.Contains(b)) continue;
                    float d = XZDist(a.transform.position, b.transform.position);
                    if (d <= bestD)
                    {
                        bestD = d;
                        bestB = b;
                    }
                }
            }

            if (bestB != null && !usedA.Contains(a))
            {
                Vector3 mid = 0.5f * (a.transform.position + bestB.transform.position);
                pairs.Add((a, bestB, mid));
                usedA.Add(a);
                usedB.Add(bestB);
            }
        }

        return pairs;
    }

    public void CommitRoomMagnet(string roomID)
    {
        _magnetLatch = false;

        var movingLoop = GetLoopByRoomID(roomID);
        if (movingLoop == null || movingLoop.Count == 0) return;

        // Gom tất cả cặp đủ gần
        var pairs = CollectSnapPairs(movingLoop);
        if (pairs.Count == 0) return;

        // Snap + Weld + thu thập phòng bị ảnh hưởng
        var affectedRoomIDs = new HashSet<string> { roomID };

        foreach (var (a, b, mid) in pairs)
        {
            if (a == null || b == null) continue;

            a.transform.position = mid;
            b.transform.position = mid;
            AddEdge(a, b);

            var bLoop = FindLoopContains(b);
            if (bLoop != null)
            {
                string rid = checkPointManager.FindRoomIDForLoop(bLoop);
                if (!string.IsNullOrEmpty(rid)) affectedRoomIDs.Add(rid);
            }
        }

        // Rebuild moving room
        // FastRebuildPerimeter(roomID, movingLoop);
        FastRebuildPerimeter(roomID, movingLoop);

        // Rebuild all rooms
        foreach (var rid in affectedRoomIDs)
        {
            if (rid == roomID) continue;
            var lp = GetLoopByRoomID(rid);
            // if (lp != null) FastRebuildPerimeter(rid, lp);
            if (lp != null) FastRebuildPerimeter(rid, lp);
        }

        checkPointManager.ClearAllLines();
        checkPointManager.RedrawAllRooms();
    }

    // REBUILD PERIMETER
    private void FastRebuildPerimeter(string roomID, List<GameObject> loop)
    {
        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null || loop == null || loop.Count == 0) return;

        // 1. Cập nhật checkpoint mới từ vị trí point
        room.checkpoints = loop.Select(go =>
        {
            var p = go.transform.position;
            return new Vector2(p.x, p.z);
        }).ToList();

        // 2. Tạo lại line chính từ checkpoints
        int n = room.checkpoints.Count;
        List<WallLine> newWalls = new List<WallLine>(n);
        for (int i = 0; i < n; i++)
        {
            Vector2 a = room.checkpoints[i];
            Vector2 b = room.checkpoints[(i + 1) % n];

            newWalls.Add(new WallLine
            {
                start = new Vector3(a.x, 0, a.y),
                end = new Vector3(b.x, 0, b.y),
                type = LineType.Wall,
                isManualConnection = false,
                distanceHeight = 0f,
                Height = 3f, // hoặc lấy từ room.heights nếu bạn có chiều cao riêng từng đoạn
                materialFront = "Default",
                materialBack = "Default"
            });
        }

        // 3. Giữ lại line phụ và cửa sổ / cửa
        var preserved = room.wallLines.Where(w => w.isManualConnection || w.type != LineType.Wall).ToList();

        // 4. Gộp lại và lưu
        room.wallLines = newWalls.Concat(preserved).ToList();

        RoomStorage.UpdateOrAddRoom(room);

        // 5. Cập nhật mesh sàn (nếu có)
        GameObject.Find($"RoomFloor_{roomID}")
            ?.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
    }
}
