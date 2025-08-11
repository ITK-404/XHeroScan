using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

public class MovePointManager: MonoBehaviour
{
    private float WELD_ON = 0.5f;    // <= khoảng này thì dính + snap trùng
    private float WELD_OFF = 0.6f;    // > khoảng này thì tách

    private CheckpointManager checkPointManager;

    private bool _magnetLatch = false;

    void Start()
    {
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
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

    private (GameObject partner, List<GameObject> loop, float dist)
    FindNearestGlobal(GameObject except, Vector3 pos, float maxDist)
    {
        GameObject best = null; List<GameObject> bestLoop = null; float bestD = maxDist;
        foreach (var lp in checkPointManager.AllCheckpoints)
        {
            foreach (var cp in lp)
            {
                if (cp == except) continue;
                float d = XZDist(pos, cp.transform.position);
                if (d < bestD) { bestD = d; best = cp; bestLoop = lp; }
            }
        }
        return (best, bestLoop, bestD);
    }

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
                    if (room.wallLines[i].type != LineType.Wall) continue;
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

            checkPointManager.DrawingTool.ClearAllLines();
            checkPointManager.RedrawAllRooms();
            break;
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
        checkPointManager.DrawingTool.ClearAllLines();
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

        checkPointManager.DrawingTool.ClearAllLines();
        checkPointManager.RedrawAllRooms();
    }

    // REBUILD PERIMETER
    private void FastRebuildPerimeter(string roomID, List<GameObject> loop)
    {
        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null || loop == null || loop.Count == 0) return;

        room.checkpoints = loop.Select(go => {
            var p = go.transform.position; return new Vector2(p.x, p.z);
        }).ToList();

        int n = room.checkpoints.Count, wi = 0;
        for (int i = 0; i < room.wallLines.Count; i++)
        {
            if (room.wallLines[i].type != LineType.Wall) continue;
            var a = room.checkpoints[wi % n];
            var b = room.checkpoints[(wi + 1) % n];
            room.wallLines[i].start = new Vector3(a.x, 0, a.y);
            room.wallLines[i].end   = new Vector3(b.x, 0, b.y);
            wi++;
        }

        RoomStorage.UpdateOrAddRoom(room);
        GameObject.Find($"RoomFloor_{roomID}")
            ?.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
    }
}
