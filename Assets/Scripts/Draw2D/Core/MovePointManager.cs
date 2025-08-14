using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using System;

public class MovePointManager : MonoBehaviour
{
    private float WELD_ON = 0.5f;    // <= khoảng này thì dính + snap trùng
    private float WELD_OFF = 0.6f;    // > khoảng này thì tách
    private const float EDGE_EPS   = 0.001f; // kiểm tra nằm trên biên
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
        // ===== Guard =====
        if (checkPointManager.selectedCheckpoint == null) return false;
        if (!checkPointManager.selectedCheckpoint.CompareTag("CheckpointExtra")) return false;

        Vector3 newWorld = checkPointManager.GetWorldPositionFromScreen(Input.mousePosition);
        Vector3 oldWorld = checkPointManager.selectedCheckpoint.transform.position;

        string roomID = checkPointManager.FindRoomIDByPoint(oldWorld);
        if (string.IsNullOrEmpty(roomID)) return false;

        Room room = RoomStorage.GetRoomByID(roomID);
        if (room == null || !checkPointManager.RoomFloorMap.TryGetValue(room.ID, out GameObject floorGO))
            return false;

        Vector2 floorPos = new Vector2(floorGO.transform.position.x, floorGO.transform.position.z);
        Vector2 newLocal = new Vector2(newWorld.x, newWorld.z) - floorPos;
        Vector2 oldLocal = new Vector2(oldWorld.x, oldWorld.z) - floorPos;

        // Không cho kéo ra ngoài (kiểm tra theo local)
        if (!checkPointManager.IsPointInPolygon(newLocal, room.checkpoints))
            return false;

        // ===== Tìm cạnh gần nhất để chèn vào polygon =====
        const float maxSnap = 0.30f;
        float best = float.MaxValue;
        int insertIndex = -1;

        // Lưu thêm để kiểm tra MERGE về đỉnh
        int iBest = -1;
        float tBest = -1f;
        Vector2 aBest = default, bBest = default;

        for (int i = 0; i < room.checkpoints.Count; i++)
        {
            Vector2 a = room.checkpoints[i];
            Vector2 b = room.checkpoints[(i + 1) % room.checkpoints.Count];
            Vector2 ab = b - a;
            float abLen = ab.magnitude;
            if (abLen < 1e-5f) continue;

            float t = Vector2.Dot((newLocal - a), ab) / (abLen * abLen);
            if (t < 0f || t > 1f) continue;

            Vector2 proj = a + ab * t;
            float d = Vector2.Distance(newLocal, proj);
            if (d < best && d < maxSnap)
            {
                best = d;
                insertIndex = i + 1;

                // save for merge check
                iBest = i;
                tBest = t;
                aBest = a;
                bBest = b;
            }
        }

        // ====== CASE 1: Convert Extra -> Main (snap vào cạnh) ======
        if (insertIndex != -1)
        {
            // A) Gỡ khỏi danh sách extra (theo local) + xoá visual tương ứng
            int nearestExtra = -1;
            float bestExtra = float.MaxValue;
            for (int i = 0; i < room.extraCheckpoints.Count; i++)
            {
                float d = Vector2.Distance(room.extraCheckpoints[i], oldLocal);
                if (d < bestExtra) { bestExtra = d; nearestExtra = i; }
            }
            if (nearestExtra != -1)
            {
                room.extraCheckpoints.RemoveAt(nearestExtra);
                RemoveExtraCheckpointVisual(room.ID, nearestExtra);
            }

            // B) Loại bỏ GO Extra cũ khỏi placedPointsByRoom (nếu có)
            if (placedPointsByRoom.TryGetValue(room.ID, out var placedList))
            {
                placedList.RemoveAll(go =>
                    go && go.CompareTag("CheckpointExtra") &&
                    Vector3.Distance(go.transform.position, oldWorld) < 0.05f);
            }

            // C) Manual line đang dính điểm extra cũ
            var attachedManual = room.wallLines
                .Where(l => l.isManualConnection &&
                            (Vector3.Distance(l.start, oldWorld) < 0.15f ||
                             Vector3.Distance(l.end, oldWorld) < 0.15f))
                .Select(l => new
                {
                    line = l,
                    atStart = Vector3.Distance(l.start, oldWorld) < 0.15f,
                    atEnd = Vector3.Distance(l.end, oldWorld) < 0.15f
                }).ToList();

            // --- MERGE nếu snap rất sát đỉnh hiện có ---
            const float MERGE_TO_VERTEX_EPS = 0.12f; // local
            bool doMerge = false;
            int mergeMainIndex = -1;

            if (iBest != -1)
            {
                if (Vector2.Distance(newLocal, aBest) <= MERGE_TO_VERTEX_EPS)
                {
                    doMerge = true; mergeMainIndex = iBest;
                }
                else if (Vector2.Distance(newLocal, bBest) <= MERGE_TO_VERTEX_EPS)
                {
                    doMerge = true; mergeMainIndex = (iBest + 1) % room.checkpoints.Count;
                }
            }

            if (doMerge)
            {
                // 1) Lấy world của đỉnh main mục tiêu
                Vector2 mergeLocal = room.checkpoints[mergeMainIndex];
                Vector3 mergeWorld = new Vector3(mergeLocal.x + floorPos.x, 0f, mergeLocal.y + floorPos.y);

                // 2) Reattach manual lines về đỉnh main
                foreach (var m in attachedManual)
                {
                    if (m.atStart) m.line.start = mergeWorld;
                    if (m.atEnd) m.line.end = mergeWorld;

                    if (Vector3.Distance(m.line.start, m.line.end) < 0.001f)
                    {
                        Vector3 dir = (m.line.end - m.line.start).normalized;
                        if (dir == Vector3.zero) dir = Quaternion.Euler(0, 137f, 0) * Vector3.forward;
                        m.line.start -= dir * 0.001f;
                        m.line.end += dir * 0.001f;
                    }
                }

                // 3) Hủy GO extra (không thêm vào loop)
                var goDel = checkPointManager.selectedCheckpoint;
                Destroy(goDel);
                checkPointManager.selectedCheckpoint = null;

                // 4) Lưu & redraw
                RoomStorage.UpdateOrAddRoom(room);
                floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
                checkPointManager.ClearAllLines();
                checkPointManager.RedrawAllRooms();
                return true;
            }

            // --- Không MERGE: Insert một đỉnh mới lên cạnh ---
            {
                // D) Insert đỉnh mới (local) & cập nhật GO
                room.checkpoints.Insert(insertIndex, newLocal);
                Vector3 snappedWorld = new Vector3(newLocal.x + floorPos.x, 0f, newLocal.y + floorPos.y);
                var go = checkPointManager.selectedCheckpoint;
                go.transform.position = snappedWorld;
                go.tag = "Untagged";
                go.transform.SetParent(null);

                // E) Đồng bộ AllCheckpoints & placedPointsByRoom
                var loop = checkPointManager.AllCheckpoints.Find(l => checkPointManager.FindRoomIDForLoop(l) == room.ID);
                if (loop != null) { loop.Remove(go); loop.Insert(insertIndex, go); }
                if (placedPointsByRoom.TryGetValue(room.ID, out var listMain))
                    if (!listMain.Contains(go)) listMain.Add(go);

                // F) Rebuild viền (giữ cửa/sổ), KHÔNG đụng manual bên trong hàm này
                var manualRefs = room.wallLines.Where(l => l.isManualConnection).ToList();
                RebuildWallLinesPreservingDoors(room); // đảm bảo không xoá manual bên trong
                foreach (var m in manualRefs)
                    if (!room.wallLines.Contains(m)) room.wallLines.Add(m);

                // G) Gắn lại manual vào vị trí mới & chống suy biến
                foreach (var m in attachedManual)
                {
                    if (m.atStart) m.line.start = snappedWorld;
                    if (m.atEnd) m.line.end = snappedWorld;
                    if (Vector3.Distance(m.line.start, m.line.end) < 0.001f)
                    {
                        Vector3 dir = (m.line.end - m.line.start).normalized;
                        if (dir == Vector3.zero) dir = Quaternion.Euler(0, 137f, 0) * Vector3.forward;
                        m.line.start -= dir * 0.001f;
                        m.line.end += dir * 0.001f;
                    }
                }

                // H) Snap nhẹ đầu manual về đúng checkpoint (bao gồm đỉnh mới)
                if (placedPointsByRoom.TryGetValue(room.ID, out var checkpointGOs))
                {
                    foreach (var line in room.wallLines)
                    {
                        if (!line.isManualConnection) continue;
                        foreach (var cp in checkpointGOs)
                        {
                            if (!cp) continue;
                            Vector3 p = cp.transform.position;
                            float tol = (cp == go) ? 0.15f : 0.05f;
                            if (Vector3.Distance(line.start, p) < tol) line.start = p;
                            if (Vector3.Distance(line.end, p) < tol) line.end = p;
                        }
                    }
                }

                // (tuỳ chọn) tách phòng nếu đủ anchor
                if (ShouldSplitByAnchorsOrEdgesGeom(room, floorGO,
                    out int anchorsOnPerimeter, out var mainsTouched, out var extrasTouched))
                {
                    if (splitRoomManager != null)
                        splitRoomManager.DetectAndSplitRoomIfNecessary(room);
                }
                else
                {
                    Debug.Log("[MovePointManager] ShouldSplitByAnchorsOrEdgesGeom: false");
                }

                // J) Lưu & redraw
                RoomStorage.UpdateOrAddRoom(room);
                floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
                checkPointManager.ClearAllLines();
                checkPointManager.RedrawAllRooms();
                return true;
            }
        }

        // ====== CASE 2: Chỉ di chuyển trong nhóm Extra ======
        {
            int nearestExtra = -1;
            float bestExtra = float.MaxValue;
            for (int i = 0; i < room.extraCheckpoints.Count; i++)
            {
                Vector2 worldExtra = room.extraCheckpoints[i] + floorPos;
                float d = Vector2.Distance(new Vector2(newWorld.x, newWorld.z), worldExtra);
                if (d < bestExtra) { bestExtra = d; nearestExtra = i; }
            }

            if (nearestExtra != -1)
            {
                // vị trí mặc định (chưa weld)
                Vector2 finalLocal = newLocal;
                Vector3 finalWorld = new Vector3(finalLocal.x + floorPos.x, 0f, finalLocal.y + floorPos.y);

                // ===== WELD extra↔extra + MERGE (giữ điểm đích, xoá điểm đang kéo) =====
                int weldIdx = -1;
                float weldBest = WELD_ON;
                for (int i = 0; i < room.extraCheckpoints.Count; i++)
                {
                    if (i == nearestExtra) continue;
                    float d = Vector2.Distance(finalLocal, room.extraCheckpoints[i]);
                    if (d <= weldBest) { weldBest = d; weldIdx = i; }
                }

                if (weldIdx != -1)
                {
                    // snap về vị trí của điểm đích (weld target)
                    Vector2 targetLocal = room.extraCheckpoints[weldIdx];
                    Vector3 targetWorld = new Vector3(targetLocal.x + floorPos.x, 0f, targetLocal.y + floorPos.y);

                    // Kéo theo các manual line đang dính với điểm đang kéo (oldWorld) về targetWorld
                    foreach (var line in room.wallLines)
                    {
                        if (!line.isManualConnection) continue;
                        if (Vector3.Distance(line.start, oldWorld) < 0.15f) line.start = targetWorld;
                        if (Vector3.Distance(line.end, oldWorld) < 0.15f) line.end = targetWorld;
                    }

                    // Cập nhật data: xoá điểm đang kéo (nearestExtra), giữ điểm đích (weldIdx)
                    room.extraCheckpoints.RemoveAt(nearestExtra);
                    RemoveExtraCheckpointVisual(room.ID, nearestExtra);

                    // Xoá GO extra đang kéo khỏi placedPointsByRoom (nếu có track)
                    if (placedPointsByRoom.TryGetValue(room.ID, out var placedList2))
                    {
                        var sel = checkPointManager.selectedCheckpoint;
                        placedList2.RemoveAll(go => go && (go == sel ||
                                               Vector3.Distance(go.transform.position, oldWorld) < 0.05f));
                    }

                    // Hủy GO đang kéo và clear selection
                    if (checkPointManager.selectedCheckpoint)
                        Destroy(checkPointManager.selectedCheckpoint);
                    checkPointManager.selectedCheckpoint = null;

                    // (tuỳ chọn) cập nhật hệ tường nếu bạn có logic dựa vào extra
                    UpdateWallLinesFromExtraCheckpoint(room, oldLocal, targetLocal, floorGO);

                    // (tuỳ chọn) tách phòng
                    if (ShouldSplitByAnchorsOrEdgesGeom(room, floorGO,
                        out int anchorsOnPerimeter2, out var mainsTouched2, out var extrasTouched2))
                    {
                        if (splitRoomManager != null)
                            splitRoomManager.DetectAndSplitRoomIfNecessary(room);
                    }

                    RoomStorage.UpdateOrAddRoom(room);
                    checkPointManager.ClearAllLines();
                    checkPointManager.RedrawAllRooms();
                    return true;
                }

                // --- không weld: chỉ move extra bình thường ---
                room.extraCheckpoints[nearestExtra] = finalLocal;
                checkPointManager.selectedCheckpoint.transform.position = finalWorld;

                // Kéo theo manual line đang dính điểm cũ
                foreach (var line in room.wallLines)
                {
                    if (!line.isManualConnection) continue;
                    if (Vector3.Distance(line.start, oldWorld) < 0.15f) line.start = finalWorld;
                    if (Vector3.Distance(line.end, oldWorld) < 0.15f) line.end = finalWorld;
                }

                UpdateWallLinesFromExtraCheckpoint(room, oldLocal, finalLocal, floorGO);
                UpdateExtraCheckpointVisual(room.ID, nearestExtra, finalLocal);
            }

            // (tuỳ chọn) tách phòng
            if (ShouldSplitByAnchorsOrEdgesGeom(room, floorGO,
                out int anchorsOnPerimeter3, out var mainsTouched3, out var extrasTouched3))
            {
                if (splitRoomManager != null)
                    splitRoomManager.DetectAndSplitRoomIfNecessary(room);
            }
            else
            {
                Debug.Log("[MovePointManager] ShouldSplitByAnchorsOrEdgesGeom: false");
            }

            RoomStorage.UpdateOrAddRoom(room);
            checkPointManager.ClearAllLines();
            checkPointManager.RedrawAllRooms();
            return true;
        }
    }
    // Xoá visual của extra khi convert/merge
    void RemoveExtraCheckpointVisual(string roomID, int index)
    {
        if (!ExtraCheckpointVisuals.TryGetValue(roomID, out var visuals)) return;
        if (index < 0 || index >= visuals.Count) return;

        if (visuals[index] != null) Destroy(visuals[index]);
        visuals.RemoveAt(index);
    }
    
    private bool ShouldSplitByAnchorsOrEdgesGeom(
        Room room,
        GameObject floorGO,
        out int anchorsOnPerimeter,
        out HashSet<int> mainsTouched,
        out HashSet<int> extrasTouched,
        float tolMain = 0.25f,
        float tolExtra = 0.25f,
        float tolEdge = 0.25f)
    {
        mainsTouched = new HashSet<int>();
        extrasTouched = new HashSet<int>();
        anchorsOnPerimeter = 0;

        if (room == null || floorGO == null) return false;

        Vector2 floorPos = new Vector2(floorGO.transform.position.x, floorGO.transform.position.z);

        // Main/Extra in world space
        var mainWorld = new List<Vector3>(room.checkpoints.Count);
        foreach (var p in room.checkpoints)
            mainWorld.Add(new Vector3(p.x + floorPos.x, 0f, p.y + floorPos.y));

        var extraWorld = new List<Vector3>(room.extraCheckpoints.Count);
        foreach (var p in room.extraCheckpoints)
            extraWorld.Add(new Vector3(p.x + floorPos.x, 0f, p.y + floorPos.y));

        if (mainWorld.Count == 0) return false;

        // Perimeter segments
        var segs = new List<(Vector3 a, Vector3 b)>(mainWorld.Count);
        float totalLen = 0f;
        for (int i = 0; i < mainWorld.Count; i++)
        {
            var a = mainWorld[i];
            var b = mainWorld[(i + 1) % mainWorld.Count];
            segs.Add((a, b));
            totalLen += Vector3.Distance(a, b);
        }
        float avgLen = totalLen / Mathf.Max(1, segs.Count); // (không dùng trực tiếp nhưng hữu ích để debug)

        // --- Dedup anchors by (edgeIndex, t)
        // key: edgeIndex -> list of t's (0..1) recorded on that edge
        var edgeAnchorMap = new Dictionary<int, List<float>>();

        void AddEdgeAnchor(int edgeIdx, float t)
        {
            if (!edgeAnchorMap.TryGetValue(edgeIdx, out var ts))
            {
                ts = new List<float>();
                edgeAnchorMap[edgeIdx] = ts;
            }

            var (a, b) = segs[edgeIdx];
            float len = Vector3.Distance(a, b);
            // convert world tolerance to parameter-space tolerance on this edge
            float minDeltaT = (len <= 1e-5f) ? 1f : Mathf.Clamp01(tolEdge / len);

            foreach (float t0 in ts)
                if (Mathf.Abs(t0 - t) <= minDeltaT)
                    return; // already have a very close anchor on the same edge

            ts.Add(t);
        }

        // --- Scan manual lines and record anchors (ALWAYS record, regardless of the other endpoint type)
        foreach (var l in room.wallLines)
        {
            if (!l.isManualConnection) continue;

            int sMain = IndexNearAny(l.start, mainWorld, tolMain);
            int eMain = IndexNearAny(l.end, mainWorld, tolMain);
            int sExtra = IndexNearAny(l.start, extraWorld, tolExtra);
            int eExtra = IndexNearAny(l.end, extraWorld, tolExtra);

            bool sOnEdge = NearestEdgeAnchor(l.start, segs, tolEdge, out int sEdgeIdx, out float sT, out Vector3 _sProj);
            bool eOnEdge = NearestEdgeAnchor(l.end, segs, tolEdge, out int eEdgeIdx, out float eT, out Vector3 _eProj);

            // Record MAIN vertices (distinct by index)
            if (sMain >= 0) mainsTouched.Add(sMain);
            if (eMain >= 0) mainsTouched.Add(eMain);

            // Record EDGE anchors (distinct by edge + t)
            if (sOnEdge) AddEdgeAnchor(sEdgeIdx, sT);
            if (eOnEdge) AddEdgeAnchor(eEdgeIdx, eT);

            // Keep extras for debugging/telemetry
            if (sExtra >= 0) extrasTouched.Add(sExtra);
            if (eExtra >= 0) extrasTouched.Add(eExtra);
        }

        // Counts
        int edgeAnchorCount = 0;
        foreach (var kv in edgeAnchorMap) edgeAnchorCount += kv.Value.Count;
        anchorsOnPerimeter = edgeAnchorCount;

        int distinctEdgeCount = edgeAnchorMap.Count;

        // --- Split condition (deterministic, no dependency on extras)
        // 1) at least two independent anchors on two different edges, or
        // 2) at least two different main vertices touched, or
        // 3) one edge anchor AND one main vertex
        bool should =
            (distinctEdgeCount >= 2) ||
            (mainsTouched.Count >= 2) ||
            (distinctEdgeCount >= 1 && mainsTouched.Count >= 1);

        if (!should)
        {
            Debug.Log($"[SplitCheck] edgeAnchors={edgeAnchorCount} (distinctEdges={distinctEdgeCount}), " +
                      $"mainsTouched={mainsTouched.Count}, extrasTouched={extrasTouched.Count} | " +
                      $"mains={mainWorld.Count}, extras={extraWorld.Count}, tolEdge={tolEdge}");
        }

        return should;
    }

    private bool NearestEdgeAnchor(
        Vector3 p,
        List<(Vector3 a, Vector3 b)> segs,
        float tolEdge,
        out int edgeIndex,
        out float t,
        out Vector3 proj)
    {
        edgeIndex = -1;
        t = 0f;
        proj = default;

        float bestD = float.MaxValue;

        for (int i = 0; i < segs.Count; i++)
        {
            var (a, b) = segs[i];
            Vector3 ab = b - a;
            float ab2 = Vector3.Dot(ab, ab);
            if (ab2 < 1e-8f) continue;

            float ti = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab2);
            Vector3 pi = a + ab * ti;
            float di = Vector3.Distance(p, pi);

            if (di <= tolEdge && di < bestD)
            {
                bestD = di;
                edgeIndex = i;
                t = ti;
                proj = pi;
            }
        }

        return edgeIndex != -1;
    }
    private int IndexNearAny(Vector3 p, List<Vector3> pts, float tol)
    {
        for (int i = 0; i < pts.Count; i++)
            if (Vector3.Distance(pts[i], p) <= tol) return i;
        return -1;
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

    void RebuildWallLinesPreservingDoors(Room room)
    {
        // 1. Backup all old wall lines
        List<WallLine> oldWalls = new List<WallLine>(room.wallLines);

        // 2. Backup cửa/cửa sổ kèm parent wall + tỷ lệ theo wall gốc
        var preservedDoorWindowLines = oldWalls
            .Where(w => w.type != LineType.Wall)
            .Select(dw =>
            {
                WallLine parent = oldWalls
                    .FirstOrDefault(w => w.type == LineType.Wall &&
                                         GetDistanceFromSegment(dw.start, w.start, w.end) +
                                         GetDistanceFromSegment(dw.end, w.start, w.end) < 0.1f);

                if (parent == null) return (null, 0f, 0f, dw);

                float r1 = GetRatioAlongLine(dw.start, parent.start, parent.end);
                float r2 = GetRatioAlongLine(dw.end, parent.start, parent.end);

                return (parent, r1, r2, dw);
            })
            .Where(p => p.parent != null)
            .ToList();

        // 3. Rebuild wall lines (Wall only)
        room.wallLines.Clear();
        for (int i = 0; i < room.checkpoints.Count; i++)
        {
            Vector2 p1 = room.checkpoints[i];
            Vector2 p2 = room.checkpoints[(i + 1) % room.checkpoints.Count];

            Vector3 start = new Vector3(p1.x, 0, p1.y);
            Vector3 end = new Vector3(p2.x, 0, p2.y);

            var existing = oldWalls.FirstOrDefault(w =>
                (Vector3.Distance(w.start, start) < 0.01f && Vector3.Distance(w.end, end) < 0.01f) ||
                (Vector3.Distance(w.start, end) < 0.01f && Vector3.Distance(w.end, start) < 0.01f));

            if (existing != null && existing.type == LineType.Wall)
                room.wallLines.Add(new WallLine(existing));
            else
                room.wallLines.Add(new WallLine(start, end, LineType.Wall));
        }

        // 4. Chèn lại các cửa/cửa sổ dựa trên tỷ lệ theo đoạn wall mới
        foreach (var (oldParent, r1, r2, dw) in preservedDoorWindowLines)
        {
            WallLine newWall = room.wallLines.FirstOrDefault(w =>
                w.type == LineType.Wall &&
                Vector3.Distance(w.start, oldParent.start) < 0.1f &&
                Vector3.Distance(w.end, oldParent.end) < 0.1f);

            if (newWall == null) continue;

            Vector3 newStart = Vector3.Lerp(newWall.start, newWall.end, Mathf.Clamp01(r1));
            Vector3 newEnd = Vector3.Lerp(newWall.start, newWall.end, Mathf.Clamp01(r2));

            room.wallLines.Add(new WallLine(newStart, newEnd, dw.type, dw.distanceHeight, dw.Height));
        }

        // 5. Cập nhật lại tempDoorWindowPoints để giữ reference chính xác
        if (checkPointManager.tempDoorWindowPoints.TryGetValue(room.ID, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                var (_, p1, p2) = list[i];

                // Tìm lại door mới từ wallLines
                var newLine = room.wallLines.FirstOrDefault(w =>
                    (w.type == LineType.Door || w.type == LineType.Window) &&
                    Vector3.Distance(w.start, p1.transform.position) < 0.1f &&
                    Vector3.Distance(w.end, p2.transform.position) < 0.1f);

                if (newLine != null)
                {
                    list[i] = (newLine, p1, p2); // gán lại line mới vào tuple
                }
            }
        }

        // === Cập nhật lại mesh sàn sau khi checkpoint thay đổi
        if (RoomFloorMap.TryGetValue(room.ID, out GameObject floorGO))
        {
            var allExtraWorldPoints = room.extraCheckpoints
                .Select(local => new Vector3(local.x, 0, local.y) + floorGO.transform.position)
                .ToList();

            var allMainWorldPoints = room.checkpoints
                .Select(p => new Vector3(p.x, 0, p.y) + floorGO.transform.position)
                .ToList();

            // Tạo map vị trí tuyệt đối
            var snapPointMap = new Dictionary<Vector3, Vector3>();

            foreach (var wp in allExtraWorldPoints.Concat(allMainWorldPoints))
            {
                Vector3 key = new Vector3((float)Math.Round(wp.x, 4), 0, (float)Math.Round(wp.z, 4));
                if (!snapPointMap.ContainsKey(key))
                    snapPointMap[key] = wp;
            }

            // Snap line về đúng point nếu có trong map
            foreach (var line in room.wallLines)
            {
                if (!line.isManualConnection) continue;

                Vector3 startKey = new Vector3((float)Math.Round(line.start.x, 4), 0, (float)Math.Round(line.start.z, 4));
                Vector3 endKey = new Vector3((float)Math.Round(line.end.x, 4), 0, (float)Math.Round(line.end.z, 4));

                if (snapPointMap.TryGetValue(startKey, out var snappedStart))
                    line.start = snappedStart;
                if (snapPointMap.TryGetValue(endKey, out var snappedEnd))
                    line.end = snappedEnd;
            }

            // Cập nhật lại mesh
            floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
        }
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
        FastRebuildPerimeter(roomID, movingLoop);

        // Rebuild all rooms
        foreach (var rid in affectedRoomIDs)
        {
            if (rid == roomID) continue;
            var lp = GetLoopByRoomID(rid);
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
