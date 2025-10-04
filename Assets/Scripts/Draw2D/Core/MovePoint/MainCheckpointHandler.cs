using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainCheckpointHandler
{
    static bool IsPerimeter(WallLine l) => l.type == LineType.Wall && !l.isManualConnection;

    private readonly CheckpointManager checkPointManager;
    private readonly MovePointManager movePointManager;
    private readonly SplitRoomManager splitRoomManager;
    public MainCheckpointHandler(CheckpointManager cpm, MovePointManager mover, SplitRoomManager split)
    {
        this.checkPointManager = cpm;
        this.movePointManager = mover;
        this.splitRoomManager = split;
    }
    private void RemoveEdge(GameObject a, GameObject b)
    {
        if (a == null || b == null) return;
        if (movePointManager._weldAdj.TryGetValue(a, out var sa)) sa.Remove(b);
        if (movePointManager._weldAdj.TryGetValue(b, out var sb)) sb.Remove(a);
    }
    
    private IEnumerable<GameObject> Neighbors(GameObject a)
    {
        if (a != null && movePointManager._weldAdj.TryGetValue(a, out var sa))
            foreach (var x in sa) if (x != null) yield return x;
    }

    private float XZDist(Vector3 a, Vector3 b)
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
            Debug.Log("Đang chọn extra point");
            if (movePointManager.MoveSelectedCheckpointExtra()) return;
        }
        if (Input.touchCount >= 2)
        {
            if (checkPointManager.selectedCheckpoint != null)
                checkPointManager.DeselectCheckpoint();
            return;
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

            if (isSameLoop && n != selected && d <= movePointManager.WELD_ON)
            {
                if (ownerLoop.Count - sameLoopToRemove.Count > 3)
                {
                    sameLoopToRemove.Add(n);
                    neighbors.Remove(n);
                    neighborOldPos.Remove(n);
                    continue;
                }
            }

            if (d <= movePointManager.WELD_ON)
            {
                n.transform.position = selected.transform.position;
            }
            else if (d > movePointManager.WELD_OFF)
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
                if (d > movePointManager.WELD_ON) continue;

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
                    movePointManager.AddEdge(selected, cp);
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
                if (movePointManager._weldAdj.ContainsKey(cp)) movePointManager._weldAdj.Remove(cp);
                ownerLoop.Remove(cp);
                if (cp != null) UnityEngine.Object.Destroy(cp);
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

            movePointManager.FastRebuildPerimeter(nRoomID, nLoop);

            var nRoom = RoomStorage.GetRoomByID(nRoomID);
            if (nRoom != null && nRoom.checkpoints != null && nRoom.checkpoints.Count >= 1)
                nRoom.center = GeoUtil.Centroid(nRoom.checkpoints);

            rebuilt.Add(nRoomID);
            checkPointManager.TryAddChangedRoomID(nRoomID);

        }
        
        checkPointManager.ClearAllLines();
        checkPointManager.RedrawAllRooms();

        checkPointManager.TryAddChangedRoomID(room.ID);

    }
}
