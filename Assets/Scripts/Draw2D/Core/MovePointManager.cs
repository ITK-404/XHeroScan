using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class MovePointManager : MonoBehaviour
{
    #region Variables
    private float WELD_ON = 0.5f;    // <= khoảng này thì dính + snap trùng
    private float WELD_OFF = 0.6f;    // > khoảng này thì tách
    public Dictionary<string, List<GameObject>> ExtraCheckpointVisuals = new Dictionary<string, List<GameObject>>();
    static bool IsPerimeter(WallLine l) => l.type == LineType.Wall && !l.isManualConnection;


    public Dictionary<string, List<GameObject>> placedPointsByRoom = new();
    Dictionary<string, GameObject> RoomFloorMap = new();

    private CheckpointManager checkPointManager;
    private SplitRoomManager splitRoomManager;

    private bool _magnetLatch = false;
    #endregion

    void Start()
    {
        Instance = this;
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
        splitRoomManager = FindFirstObjectByType<SplitRoomManager>();
    }

    //  WELD CLUSTER (nhiều-điểm)
    // 1 điểm có thể dính với n điểm khác
    private readonly Dictionary<GameObject, HashSet<GameObject>> _weldAdj = new();

    public static MovePointManager Instance;

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
    
    private static Floor FindFloorById(string id)
    {
        if (string.IsNullOrEmpty(id) || FloorStorage.floors == null) return null;
        for (int i = 0; i < FloorStorage.floors.Count; i++)
        {
            var f = FloorStorage.floors[i];
            if (f != null && f.ID == id) return f;
        }
        return null;
    }

    private static bool IsInsideFloorXZ(Vector3 p, Floor floor, float boundaryEps = 1e-4f)
    {
        if (floor == null || floor.checkpoints == null || floor.checkpoints.Count < 3) return true; // không có floor => cho qua
        Vector2 q = new Vector2(p.x, p.z);

        // ray-casting + on-boundary
        int c = 0; var poly = floor.checkpoints;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var a = poly[i];
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];
            bool cond = ((a.y > q.y) != (b.y > q.y)) &&
                        (q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y + 1e-12f) + a.x);
            if (cond) c++;
            // on-boundary quick check
            float d2 = DistPointToSegment2(q, a, b);
            if (d2 <= boundaryEps * boundaryEps) return true;
        }
        return (c & 1) == 1;
    }

    private static float DistPointToSegment2(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 < 1e-12f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
        var proj = a + t * ab;
        return (p - proj).sqrMagnitude;
    }

    private static Vector3 ClosestPointOnPolygonXZ(Vector3 p, List<Vector2> poly)
    {
        if (poly == null || poly.Count < 2) return p;
        Vector2 q = new Vector2(p.x, p.z);

        float bestD2 = float.MaxValue;
        Vector2 best = q;

        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var a = poly[i];
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];

            // project q onto segment ab
            Vector2 ab = b - a;
            float ab2 = Vector2.Dot(ab, ab);
            Vector2 cand;
            if (ab2 < 1e-12f) cand = a;
            else
            {
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab2);
                cand = a + t * ab;
            }

            float d2 = (q - cand).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = cand; }
        }

        return new Vector3(best.x, p.y, best.y);
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

        // Lấy checkpoint được chọn + vị trí mới theo chuột
        var selected = checkPointManager.selectedCheckpoint;

        // TÌM LOOP & ROOM trước để còn biết FLOOR nào mà kẹp (clamp)
        List<GameObject> ownerLoop = null;
        foreach (var lp in checkPointManager.AllCheckpoints)
        {
            if (lp != null && lp.Contains(selected)) { ownerLoop = lp; break; }
        }
        if (ownerLoop == null) return;

        string roomID = checkPointManager.FindRoomIDForLoop(ownerLoop);
        if (string.IsNullOrEmpty(roomID)) return;
        Room room = RoomStorage.GetRoomByID(roomID);
        if (room == null) return;

        // If this selected is one of the door/window anchor points, handle early-returns
        foreach (var kvp in checkPointManager.tempDoorWindowPoints)
        {
            foreach (var (line, p1GO, p2GO) in kvp.Value)
            {
                if (selected == p1GO || selected == p2GO)
                {
                    Vector3 newPosRaw = checkPointManager.GetWorldPositionFromScreen(Input.mousePosition);
                    // Cửa/cửa sổ: vẫn bám tường gần nhất của chính ROOM này (không kẹp theo floor vì đã nằm trong perimeter)
                    WallLine wall = FindClosestWallLine(line, kvp.Key);
                    if (wall == null) return;

                    Vector3 projected = checkPointManager.ProjectPointOnLineSegment(wall.start, wall.end, newPosRaw);
                    if (selected == p1GO) line.start = projected; else line.end = projected;

                    selected.transform.position = projected;
                    checkPointManager.RedrawAllRooms();
                    return;
                }
            }
        }

        // === KẸP VỊ TRÍ THEO FLOOR ===
        Vector3 newPositionRaw = checkPointManager.GetWorldPositionFromScreen(Input.mousePosition);
        Vector3 oldPos = selected.transform.position;

        // tìm floor để giới hạn
        Floor floor = FindFloorById(room.floorID);
        Vector3 newPositionClamped = newPositionRaw;
        if (floor != null && floor.checkpoints != null && floor.checkpoints.Count >= 3)
        {
            // nếu ra ngoài -> kẹp về biên gần nhất
            if (!IsInsideFloorXZ(newPositionRaw, floor, 1e-3f))
                newPositionClamped = ClosestPointOnPolygonXZ(newPositionRaw, floor.checkpoints);
            // giữ nguyên cao độ hiện tại của checkpoint
            newPositionClamped.y = oldPos.y;
        }

        selected.transform.position = newPositionClamped;
        Vector3 selectedDelta = newPositionClamped - oldPos;
        
        // WELD / SNAP (CLUSTER)
        var neighbors = new HashSet<GameObject>(Neighbors(selected));
        var neighborOldPos = new Dictionary<GameObject, Vector3>(neighbors.Count);
        foreach (var n in neighbors) if (n != null) neighborOldPos[n] = n.transform.position;

        var sameLoopToRemove = new List<GameObject>();

        foreach (var n in neighbors.ToList())
        {
            if (n == null) { RemoveEdge(selected, n); neighbors.Remove(n); continue; }

            float d = XZDist(selected.transform.position, n.transform.position);
            bool isSameLoop = FindLoopContains(n) == ownerLoop;

            if (isSameLoop && n != selected && d <= WELD_ON)
            {
                if (ownerLoop.Count - sameLoopToRemove.Count > 3)
                {
                    sameLoopToRemove.Add(n);
                    neighbors.Remove(n);
                    neighborOldPos.Remove(n);
                    continue;
                }
            }

            if (d <= WELD_ON)
            {
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
                n.transform.position += selectedDelta;
            }
        }

        foreach (var lp in checkPointManager.AllCheckpoints)
        {
            foreach (var cp in lp)
            {
                if (cp == selected) continue;
                if (neighbors.Contains(cp)) continue;

                float d = XZDist(selected.transform.position, cp.transform.position);
                if (d > WELD_ON) continue;

                bool isSameLoop = (lp == ownerLoop);

                if (isSameLoop)
                {
                    if (cp != selected && ownerLoop.Count - sameLoopToRemove.Count > 3)
                    {
                        sameLoopToRemove.Add(cp);
                    }
                }
                else
                {
                    if (!neighborOldPos.ContainsKey(cp)) neighborOldPos[cp] = cp.transform.position;
                    AddEdge(selected, cp);
                    neighbors.Add(cp);
                    cp.transform.position = selected.transform.position;
                }
            }
        }

        if (sameLoopToRemove.Count > 0)
        {
            foreach (var cp in sameLoopToRemove)
            {
                foreach (var nb in Neighbors(cp).ToList()) RemoveEdge(cp, nb);
                if (_weldAdj.ContainsKey(cp)) _weldAdj.Remove(cp);
                ownerLoop.Remove(cp);
                if (cp != null) Destroy(cp);
            }
        }

        var neighborDelta = new Dictionary<GameObject, Vector3>(neighbors.Count);
        foreach (var n in neighbors)
            if (neighborOldPos.TryGetValue(n, out var o))
                neighborDelta[n] = n.transform.position - o;

        bool isDuplicate = false;

        List<Vector2> newCheckpoints = new();
        for (int i = 0; i < ownerLoop.Count; i++)
        {
            Vector3 pos = ownerLoop[i].transform.position;
            for (int j = 0; j < i; j++)
            {
                Vector3 otherPos = ownerLoop[j].transform.position;
                if (XZDist(pos, otherPos) < 0.01f)
                {
                    Debug.LogWarning($"[BỎ QUA] Điểm {i} trùng điểm {j} -> Không update checkpoint để tránh mesh lỗi.");
                    isDuplicate = true; break;
                }
            }
            if (isDuplicate) break;
            newCheckpoints.Add(new Vector2(pos.x, pos.z));
        }

        if (!isDuplicate)
        {
            room.checkpoints = newCheckpoints;
            room.center = GeoUtil.Centroid(room.checkpoints); // cập nhật tâm phòng sau khi kéo
        }

        if (!isDuplicate)
        {
            int wallLineIndex = 0;
            int n = room.checkpoints.Count;
            for (int i = 0; i < room.wallLines.Count; i++)
            {
                var wl = room.wallLines[i];
                if (!IsPerimeter(wl)) continue;

                Vector2 p1 = room.checkpoints[wallLineIndex % n];
                Vector2 p2 = room.checkpoints[(wallLineIndex + 1) % n];
                room.wallLines[i].start = new Vector3(p1.x, 0, p1.y);
                room.wallLines[i].end = new Vector3(p2.x, 0, p2.y);
                wallLineIndex++;
            }
        }

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
                Vector3 dir = (line.end - line.start).normalized;
                if (dir == Vector3.zero)
                {
                    int hash = oldPos.GetHashCode();
                    dir = Quaternion.Euler(0, hash % 360, 0) * Vector3.forward;
                }
                line.start = selected.transform.position - dir * 0.001f;
                line.end = selected.transform.position + dir * 0.001f;
            }
            else if (movedStart) line.start += nearPartnerStart ? partnerStartDelta : selectedDelta;
            else if (movedEnd) line.end += nearPartnerEnd ? partnerEndDelta : selectedDelta;
            else if (Vector3.Distance(line.start, line.end) < 0.001f)
            {
                Vector3 dir = Quaternion.Euler(0, 137f, 0) * Vector3.forward;
                line.start = selected.transform.position - dir * 0.001f;
                line.end = selected.transform.position + dir * 0.001f;
            }
            // line.headingCompass= HeadingManager.HeadingDeg(line.start, line.end);
            HeadingManager.UpdateAllWallHeadings(room, room.headingCompass);
        }

        foreach (var door in room.wallLines.Where(w => w.type != LineType.Wall))
        {
            WallLine parentWall = null; float minDist = float.MaxValue;
            foreach (var wall in room.wallLines)
            {
                if (wall.type != LineType.Wall) continue;
                float dist = GetDistanceFromSegment(door.start, wall.start, wall.end)
                           + GetDistanceFromSegment(door.end, wall.start, wall.end);
                if (dist < minDist) { minDist = dist; parentWall = wall; }
            }
            if (parentWall != null)
            {
                float r1 = Mathf.Clamp01(GetRatioAlongLine(door.start, parentWall.start, parentWall.end));
                float r2 = Mathf.Clamp01(GetRatioAlongLine(door.end, parentWall.start, parentWall.end));
                door.start = Vector3.Lerp(parentWall.start, parentWall.end, r1);
                door.end = Vector3.Lerp(parentWall.start, parentWall.end, r2);

                if (checkPointManager.tempDoorWindowPoints.TryGetValue(room.ID, out var doorsInRoom))
                    foreach (var (line, p1GO, p2GO) in doorsInRoom)
                    { p1GO.transform.position = line.start; p2GO.transform.position = line.end; }
            }
        }

        var roomGO = GameObject.Find($"RoomFloor_{roomID}");
        if (roomGO != null)
            roomGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);

        var rebuilt = new HashSet<string>();
        foreach (var nGo in neighbors)
        {
            var nLoop = FindLoopContains(nGo);
            if (nLoop == null) continue;
            string nRoomID = checkPointManager.FindRoomIDForLoop(nLoop);
            if (string.IsNullOrEmpty(nRoomID) || nRoomID == roomID || rebuilt.Contains(nRoomID)) continue;

            FastRebuildPerimeter(nRoomID, nLoop);

            var nRoom = RoomStorage.GetRoomByID(nRoomID);
            if (nRoom != null && nRoom.checkpoints != null && nRoom.checkpoints.Count >= 1)
                nRoom.center = GeoUtil.Centroid(nRoom.checkpoints);

            rebuilt.Add(nRoomID);
            checkPointManager.TryAddChangedRoomID(nRoomID);

        }
        //Debug.Log($"Room of point selected {room.ID}");
        //foreach(var item in rebuilt)
        //{
        //    Debug.Log($"Rebuilt room {item} due to neighbor move");
        //}
        
        checkPointManager.ClearAllLines();
        checkPointManager.RedrawAllRooms();

        checkPointManager.TryAddChangedRoomID(room.ID);

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
            FastRebuildPerimeter(room.ID, loop);

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
        if (placedPointsByRoom.TryGetValue(room.ID, out var goList) && goList != null)
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
            if (placedPointsByRoom.TryGetValue(room.ID, out var listGO) && listGO != null)
                listGO.Remove(movingGO);
            Destroy(movingGO);

            // Clear selection để tránh dùng GO đã bị destroy
            if (checkPointManager.selectedCheckpoint == movingGO)
                checkPointManager.selectedCheckpoint = null;

            return true;
        }

        // === 2) Không có main gần -> thử snap vào EXTRA khác ===
        GameObject bestExtraGO = null;
        float bestExtraD = float.MaxValue;

        if (placedPointsByRoom.TryGetValue(room.ID, out var list) && list != null)
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
            if (placedPointsByRoom.TryGetValue(room.ID, out var listGO) && listGO != null)
                listGO.Remove(movingGO);
            Destroy(movingGO);

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
        if (!placedPointsByRoom.TryGetValue(roomId, out var list) || list == null)
        {
            placedPointsByRoom[roomId] = new List<GameObject> { cpGO };
            return;
        }
        if (!list.Contains(cpGO)) list.Add(cpGO);
    }

    // TRUE nếu có manual line mà CẢ HAI đầu đang "đứng" ở checkpoint MAIN (theo GO, tag != CheckpointExtra)
    private bool MaybeSplitByManualDiagonal(Room room, GameObject floorGO, float tolWorld = 0.30f)
    {
        if (room == null) return false;
        if (!placedPointsByRoom.TryGetValue(room.ID, out var goList) || goList == null || goList.Count == 0)
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
            return true;
        }

        return false;
    }

    Vector3 RoomToWorld(Vector2 localPos, GameObject floorGO)
    {
        return new Vector3(localPos.x, 0, localPos.y) + floorGO.transform.position;
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


    public void RebuildRoomMeshh(string roomID)
    {
        var loop = GetLoopByRoomID(roomID);

        FastRebuildPerimeter(roomID, loop);
        checkPointManager.ClearAllLines();
        checkPointManager.RedrawAllRooms();
    }

    private void FastRebuildPerimeter(string roomID, List<GameObject> loop)
    {
        // ==== Layering để room luôn cao hơn floor ====
        const int ROOM_INDEX = 2;       // phòng ở index 2
        const float LAYER_STEPY = 0.002f;  // mỗi index cách nhau ~2mm
        const float WALL_LIFT = 0.003f;  // line nhô lên khỏi mặt sàn phòng để tránh z-fighting

        float baseY = ROOM_INDEX * LAYER_STEPY; // Y cho mesh phòng
        float lineY = baseY + WALL_LIFT;        // Y cho line & checkpoint hiển thị

        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null || loop == null || loop.Count == 0) return;

        // Cập nhật checkpoints (x,z) từ vị trí point (bỏ qua y)
        Debug.Log($"Room ID: {roomID} {room.checkpoints.Count} {loop.Count}");
        room.checkpoints = loop.Select(go =>
        {
            var p = go.transform.position;
            return new Vector2(p.x, p.z);
        }).ToList();

        // Tạo lại line tường chính từ checkpoints (đặt ở lineY)
        int n = room.checkpoints.Count;
        List<WallLine> newWalls = new List<WallLine>(n);
        Debug.Log($"Tạo lại wall line liên tục");
        for (int i = 0; i < n; i++)
        {
            Vector2 a = room.checkpoints[i];
            Vector2 b = room.checkpoints[(i + 1) % n];
            room.wallLines[i].start = new Vector3(a.x, lineY, a.y);
            room.wallLines[i].end = new Vector3(b.x, lineY, b.y);
            //newWalls.Add(new WallLine
            //{
            //    start = new Vector3(a.x, lineY, a.y),
            //    end = new Vector3(b.x, lineY, b.y),
            //    type = LineType.Wall,
            //    isManualConnection = false,
            //    distanceHeight = 0f,
            //    Height = 3f,
            //    materialFront = "Default",
            //    materialBack = "Default"
            //});
        }

        // Giữ lại line phụ & cửa/cửa sổ, nhưng chuẩn hoá Y = lineY để không bị chìm
        //var preserved = room.wallLines
        //    .Where(w => w.isManualConnection || w.type != LineType.Wall)
        //    .Select(w =>
        //    {
        //        var s = w.start; s.y = lineY;
        //        var e = w.end; e.y = lineY;
        //        w.start = s; w.end = e;
        //        return w;
        //    })
        //    .ToList();

        // Gộp & lưu
        //room.wallLines = newWalls.Concat(preserved).ToList();
        RoomStorage.UpdateOrAddRoom(room);
        
        // Cập nhật mesh sàn phòng: đặt holder lên baseY rồi build lại
        var floorGO = GameObject.Find($"RoomFloor_{roomID}");
        if (floorGO != null)
        {
            var pos = floorGO.transform.position;
            pos.y = baseY;
            floorGO.transform.position = pos;

            var meshCtrl = floorGO.GetComponent<RoomMeshController>();
            if (meshCtrl != null)
                meshCtrl.GenerateMesh(room.checkpoints);
        }

        // Nâng vị trí hiển thị của các checkpoint GO trong vòng lên lineY cho khớp trực quan
        foreach (var go in loop)
        {
            if (!go) continue;
            var p = go.transform.position;
            p.y = lineY;
            go.transform.position = p;
        }
    }

}
