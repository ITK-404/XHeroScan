using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class HandleCheckpointManger : MonoBehaviour
{
    private Vector3? firstDoorPoint = null; // lưu P1
    private WallLine selectedWallLineForDoor; // đoạn tường được chọn
    private Room selectedRoomForDoor;

    private CheckpointManager checkPointManager;
    private SplitRoomManager splitRoomManager;
    void Start()
    {
        splitRoomManager = FindFirstObjectByType<SplitRoomManager>();
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
    }

    // ==== Đặt Point thường và chia tường ====

    private const float EDGE_EPS = 0.001f; // kiểm tra nằm trên biên
    private const float DUP_EPS2 = 1e-6f;  // bỏ giao trùng A/B
    private const float DEDUP_EPS2 = 1e-6f;  // dedup intersections
    private const float SNAP_EPS = 0.01f;   // ~ 1cm
    static float Dist2(Vector2 a, Vector2 b) => (a - b).magnitude;

    // Project p lên đoạn ab (clamp trong đoạn)
    static Vector2 ProjectOnSeg(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 < 1e-12f) return a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
        return a + t * ab;
    }

    // Snap vào 1 vertex trong 1 room
    static bool TrySnapVertex(Room room, Vector2 p, float eps, out Vector2 snapped)
    {
        foreach (var v in room.checkpoints)
        {
            if (Dist2(p, v) <= eps) { snapped = v; return true; }
        }
        snapped = default;
        return false;
    }

    // Snap vào cạnh polygon của room; nếu gần mút thì trả về mút
    static bool TrySnapEdgePreferVertex(Room room, Vector2 p, float eps, out Vector2 snapped)
    {
        int n = room.checkpoints.Count;
        if (n < 2) { snapped = default; return false; }

        float bestD = float.MaxValue;
        Vector2 best = default;
        bool found = false;

        for (int i = 0; i < n; i++)
        {
            var a = room.checkpoints[i];
            var b = room.checkpoints[(i + 1) % n];

            // gần đầu mút -> chọn mút luôn
            if (Dist2(p, a) <= eps) { snapped = a; return true; }
            if (Dist2(p, b) <= eps) { snapped = b; return true; }

            var proj = ProjectOnSeg(a, b, p);
            float d = Dist2(p, proj);
            if (d <= eps && d < bestD)
            {
                bestD = d; best = proj; found = true;
            }
        }

        snapped = best;
        return found;
    }

    // Snap trên toàn bộ rooms: ưu tiên vertex, sau đó edge (edge vẫn ưu tiên mút)
    bool TrySnapAcrossAllRooms(Vector2 p, float eps, out Vector2 snapped, out bool toVertex)
    {
        // 1) ưu tiên snap vào vertex
        foreach (var room in RoomStorage.rooms)
        {
            if (TrySnapVertex(room, p, eps, out snapped)) { toVertex = true; return true; }
        }

        // 2) edge (ưu tiên mút)
        float bestD = float.MaxValue; Vector2 best = default; bool found = false;
        foreach (var room in RoomStorage.rooms)
        {
            if (TrySnapEdgePreferVertex(room, p, eps, out var s))
            {
                float d = Dist2(p, s);
                if (d < bestD) { bestD = d; best = s; found = true; }
            }
        }

        snapped = best; toVertex = false;
        return found;
    }

    public void HandleSingleWallPlacement(Vector3 position)
    {
        if (ConnectManager.isConnectActive) return;

        List<GameObject> tempCreatedPoints = new();

        // ===== CLICK 1 =====
        if (checkPointManager.firstPoint == null)
        {
            // Snap sớm vị trí click vào vertex/edge gần nhất (ưu tiên vertex)
            Vector2 p2 = new(position.x, position.z);
            if (TrySnapAcrossAllRooms(p2, SNAP_EPS, out var s2, out _))
                position = new Vector3(s2.x, 0, s2.y);

            checkPointManager.firstPoint = Instantiate(checkPointManager.checkpointPrefab, position, Quaternion.identity);
            checkPointManager.firstPoint.transform.SetParent(null);
            return;
        }

        // ===== CLICK 2 =====
        Vector3 startWorld = checkPointManager.firstPoint.transform.position;
        Vector3 endWorld = position;

        Vector2 aOrig = new(startWorld.x, startWorld.z);
        Vector2 bOrig = new(endWorld.x, endWorld.z);

        bool anyRoomUpdated = false;
        bool firstPointInsideRoom = false;
        List<Room> roomsToSplit = new();

        // ===== ƯU TIÊN: Nếu cả 2 điểm cùng nằm trong 1 room → line phụ =====
        Room insideRoom = RoomStorage.rooms.FirstOrDefault(r =>
            PointInPolygon(aOrig, r.checkpoints) && PointInPolygon(bOrig, r.checkpoints));

        if (insideRoom != null)
        {
            bool ok = HandleWallLoopPlacement(startWorld, endWorld); // stateless
            if (ok) anyRoomUpdated = true;
            // Không destroy vì đã tái dụng làm Extra trong HandleWallLoopPlacement
            checkPointManager.firstPoint = null;
            return; // kết thúc luôn vì đã xử lý “vẽ trong”
        }

        // ===== NGƯỢC LẠI: “vẽ ngoài” → GIỮ NGUYÊN TÍNH CHẤT HIỆN TẠI =====
        foreach (Room room in RoomStorage.rooms.ToList())
        {
            bool aInside = PointInPolygon(aOrig, room.checkpoints);
            bool bInside = PointInPolygon(bOrig, room.checkpoints);
            bool aOnBoundary = OnBoundary(room, aOrig, EDGE_EPS);

            if (aInside || aOnBoundary) firstPointInsideRoom = true;

            // --- lấy / tạo LoopMap ---
            var map = checkPointManager.loopMappings.FirstOrDefault(m => m.RoomID == room.ID);
            if (map == null)
            {
                map = new LoopMap(room.ID, new List<GameObject>());
                checkPointManager.loopMappings.Add(map);
                checkPointManager.AllCheckpoints.Add(map.CheckpointsGO);
            }
            if ((aInside || aOnBoundary) && checkPointManager.firstPoint != null && !map.CheckpointsGO.Contains(checkPointManager.firstPoint))
                map.CheckpointsGO.Add(checkPointManager.firstPoint);

            // 2.0. Chọn A,B (hình học) từ hit ban đầu
            Vector2 A, B;
            bool insideInsideCase, A_fromHit, B_fromHit, haveValidPair;
            DetermineSegmentForRoom(room, aOrig, bOrig,
                                    out A, out B,
                                    out insideInsideCase,
                                    out A_fromHit, out B_fromHit,
                                    out haveValidPair);
            if (!haveValidPair) continue;

            // --- Snap ưu tiên vertex > edge cho A/B trên toàn bộ rooms ---
            Vector2 A_snap = A, B_snap = B;
            bool A_toVertex = false, B_toVertex = false;

            if (TrySnapAcrossAllRooms(A, SNAP_EPS, out var sA, out var sA_isV))
            { A_snap = sA; A_toVertex = sA_isV; }
            if (TrySnapAcrossAllRooms(B, SNAP_EPS, out var sB, out var sB_isV))
            { B_snap = sB; B_toVertex = sB_isV; }

            // Fallback cũ: chỉ dùng nếu vẫn chưa “đứng” trên boundary/vertex
            if (!insideInsideCase && (A_fromHit || !PointInPolygon(A_snap, room.checkpoints)))
            {
                if (!A_toVertex && !OnBoundary(room, A_snap, EDGE_EPS))
                    TrySnapToAnyEdge(room, A_snap, SNAP_EPS, out A_snap);
            }
            if (!insideInsideCase && (B_fromHit || !PointInPolygon(B_snap, room.checkpoints)))
            {
                if (!B_toVertex && !OnBoundary(room, B_snap, EDGE_EPS))
                    TrySnapToAnyEdge(room, B_snap, SNAP_EPS, out B_snap);
            }

            // cập nhật A/B cuối cùng
            A = A_snap; B = B_snap;

            // Tính lại trạng thái sau snap
            bool aNowInside = PointInPolygon(A, room.checkpoints);
            bool bNowInside = PointInPolygon(B, room.checkpoints);
            bool A_onBoundary2 = OnBoundary(room, A, EDGE_EPS);
            bool B_onBoundary2 = OnBoundary(room, B, EDGE_EPS);

            // 2.1. Quyết định vẽ
            bool A_anchored = A_onBoundary2 || A_fromHit;
            bool B_anchored = B_onBoundary2 || B_fromHit;
            bool shouldDraw = insideInsideCase || A_anchored || B_anchored;
            if (!shouldDraw) continue;

            Vector3 start = new(A.x, 0, A.y);
            Vector3 end = new(B.x, 0, B.y);

            // 2.2. Spawn/insert điểm biên (chỉ khi chưa tồn tại)
            if (A_onBoundary2 && !room.checkpoints.Any(p => Vector2.Distance(p, A) < EDGE_EPS))
                tempCreatedPoints.Add(SpawnEdgeCheckpoint(room, map, start, A));
            if (B_onBoundary2 && !room.checkpoints.Any(p => Vector2.Distance(p, B) < EDGE_EPS))
                tempCreatedPoints.Add(SpawnEdgeCheckpoint(room, map, end, B));

            // marker cho đầu bên trong nhưng không trên biên (để người dùng thấy)
            if (bNowInside && !B_onBoundary2)
                tempCreatedPoints.Add(SpawnMarker(map, end));
            // (đầu A nếu trong và không trên biên chính là firstPoint)

            // 2.3. Marker các giao cắt với tường nội bộ
            SpawnInternalIntersections(room, map, A, B, tempCreatedPoints);

            // 2.4. Vẽ line
            checkPointManager.DrawingTool.currentLineType = checkPointManager.currentLineType;
            checkPointManager.DrawLineAndDistance(start, end);

            var newline = new WallLine { start = start, end = end, type = checkPointManager.currentLineType };
            room.wallLines.Add(newline);
            checkPointManager.DrawingTool.wallLines.Add(newline);

            RoomStorage.UpdateOrAddRoom(room);
            anyRoomUpdated = true;

            // 2.5. Tách nếu tạo mạch kín
            if (ShouldSplitSegment(room, A, B, A_onBoundary2, B_onBoundary2))
                roomsToSplit.Add(room);
        }

        // ===== KẾT THÚC =====
        if (!anyRoomUpdated)
        {
            if (!firstPointInsideRoom && checkPointManager.firstPoint) Destroy(checkPointManager.firstPoint);
            foreach (var p in tempCreatedPoints) if (p) Destroy(p);
        }
        else
        {
            if (!firstPointInsideRoom && checkPointManager.firstPoint) Destroy(checkPointManager.firstPoint);

            if (roomsToSplit.Count > 0)
                StartCoroutine(WaitAndSplitRooms(roomsToSplit.Distinct().ToList()));

            checkPointManager.RedrawAllRooms();
        }

        checkPointManager.firstPoint = null; // reset
    }

    /// <summary>
    /// Vẽ trong room: biến p1 thành CheckpointExtra (tái dùng firstPoint), tạo thêm Extra ở p2,
    /// rồi nối line phụ isManualConnection=true. Stateless (không dùng manualStartGO).
    /// </summary>
    public bool HandleWallLoopPlacement(Vector3 p1, Vector3 p2)
{
    if (checkPointManager.selectedCheckpoint != null) return false;
    if (RoomStorage.rooms.Count == 0)
    {
        Debug.LogWarning("RoomStorage chưa sẵn sàng.");
        return false;
    }

    // Xác định room bằng p1 (vẽ trong cùng 1 room)
    string roomID1 = checkPointManager.FindRoomIDByPoint(p1);
    string roomID2 = checkPointManager.FindRoomIDByPoint(p2);

    if (string.IsNullOrEmpty(roomID1) || roomID1 != roomID2)
    {
        Debug.LogWarning($"Hai điểm không cùng room: room1={roomID1}, room2={roomID2}");
        return false;
    }

    if (!checkPointManager.RoomFloorMap.TryGetValue(roomID1, out GameObject parentFloor) || parentFloor == null)
    {
        Debug.LogWarning($"RoomFloorMap không có parentFloor cho room {roomID1}");
        return false;
    }

    Room room = RoomStorage.GetRoomByID(roomID1);
    if (room == null)
    {
        Debug.LogWarning("Không tìm thấy Room khi tạo line phụ.");
        return false;
    }

    // === Local-2D theo room/floor (để thêm vào room đúng roomID) ===
    Vector2 parentXZ = new(parentFloor.transform.position.x, parentFloor.transform.position.z);
    Vector2 aLocal2D = new Vector2(p1.x, p1.z) - parentXZ;
    Vector2 bLocal2D = new Vector2(p2.x, p2.z) - parentXZ;

    // A) Tái dùng firstPoint làm Extra ở p1 (KHÔNG parent vào floor)
    GameObject aGO = checkPointManager.firstPoint;
    if (aGO == null) aGO = Instantiate(checkPointManager.checkpointPrefab, p1, Quaternion.identity);
    else aGO.transform.position = p1;
    aGO.transform.SetParent(null);                 // KHÔNG parent
    aGO.tag = "CheckpointExtra";
    if (!checkPointManager.currentCheckpoints.Contains(aGO))
        checkPointManager.currentCheckpoints.Add(aGO);

    // B) Tạo Extra ở p2 (KHÔNG parent vào floor)
    GameObject bGO = Instantiate(checkPointManager.checkpointPrefab, p2, Quaternion.identity);
    bGO.transform.SetParent(null);                 // KHÔNG parent
    bGO.tag = "CheckpointExtra";
    if (!checkPointManager.currentCheckpoints.Contains(bGO))
        checkPointManager.currentCheckpoints.Add(bGO);
        
    // C) Thêm vào room.extraCheckpoints (tránh trùng bằng EDGE_EPS)
        float eps2 = EDGE_EPS * EDGE_EPS;

    bool hasA = false;
    for (int i = 0; i < room.extraCheckpoints.Count; i++)
    {
        Vector2 q = room.extraCheckpoints[i];
        Vector2 d = q - aLocal2D;
        if (d.sqrMagnitude <= eps2) { hasA = true; break; }
    }
    if (!hasA) room.extraCheckpoints.Add(aLocal2D);

    bool hasB = false;
    for (int i = 0; i < room.extraCheckpoints.Count; i++)
    {
        Vector2 q = room.extraCheckpoints[i];
        Vector2 d = q - bLocal2D;
        if (d.sqrMagnitude <= eps2) { hasB = true; break; }
    }
    if (!hasB) room.extraCheckpoints.Add(bLocal2D);

    RoomStorage.UpdateOrAddRoom(room);

    // D) Vẽ line phụ
    checkPointManager.DrawingTool.currentLineType = checkPointManager.currentLineType;
    checkPointManager.DrawLineAndDistance(p1, p2);

    var newline = new WallLine
    {
        start = p1,
        end = p2,
        type = checkPointManager.currentLineType,
        isManualConnection = true
    };
    room.wallLines.Add(newline);
    checkPointManager.DrawingTool.wallLines.Add(newline);
    RoomStorage.UpdateOrAddRoom(room);

    // E) Redraw
    checkPointManager.RedrawAllRooms();

    return true;
}

    private float DistPointSeg2(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-12f);
        t = Mathf.Clamp01(t);
        Vector2 proj = a + t * ab;
        return (p - proj).sqrMagnitude;
    }

    private bool TrySnapToAnyEdge(Room room, Vector2 p, float snapEps, out Vector2 snapped)
    {
        float bestD2 = float.MaxValue;
        Vector2 best = p;
        foreach (var w in room.wallLines)
        {
            Vector2 a = new(w.start.x, w.start.z);
            Vector2 b = new(w.end.x, w.end.z);
            float d2 = DistPointSeg2(p, a, b);
            if (d2 < bestD2)
            {
                bestD2 = d2;
                Vector2 ab = b - a;
                float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-12f);
                t = Mathf.Clamp01(t);
                best = a + t * ab;
            }
        }
        if (bestD2 <= snapEps * snapEps) { snapped = best; return true; }
        snapped = p; return false;
    }

    private IEnumerable<(Vector2 a, Vector2 b)> GetBoundaryEdges(Room room)
    {
        var cps = room.checkpoints;
        for (int i = 0, n = cps.Count; i < n; i++)
            yield return (cps[i], cps[(i + 1) % n]);
    }

    private bool OnBoundary(Room room, Vector2 p, float eps)
    {
        foreach (var (a, b) in GetBoundaryEdges(room))
            if (PointOnSegment(p, a, b, eps)) return true;
        return false;
    }

    private List<Vector2> DedupIntersections(List<Vector2> raw)
    {
        List<Vector2> outList = new();
        foreach (var p in raw)
        {
            bool dup = false;
            foreach (var q in outList)
                if ((p - q).sqrMagnitude < DEDUP_EPS2) { dup = true; break; }
            if (!dup) outList.Add(p);
        }
        return outList;
    }

    private bool SegIntersectOrTouchEps(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float eps, out Vector2 inter)
    {
        inter = default;

        float d12 = Mathf.Min(
            DistPointSeg2(a1, b1, b2),
            Mathf.Min(DistPointSeg2(a2, b1, b2),
            Mathf.Min(DistPointSeg2(b1, a1, a2),
                    DistPointSeg2(b2, a1, a2))));
        if (d12 <= eps * eps)
        {
            Vector2 mid = Vector2.zero; int c = 0;
            if (DistPointSeg2(a1, b1, b2) <= eps * eps) { mid += a1; c++; }
            if (DistPointSeg2(a2, b1, b2) <= eps * eps) { mid += a2; c++; }
            if (DistPointSeg2(b1, a1, a2) <= eps * eps) { mid += b1; c++; }
            if (DistPointSeg2(b2, a1, a2) <= eps * eps) { mid += b2; c++; }
            if (c > 0) inter = mid / c;
            return true;
        }

        if (LineSegmentsIntersect(a1, a2, b1, b2, out inter)) return true;
        return false;
    }

    private bool TryBoundaryIntersection(Room room, Vector2 A, Vector2 B, out Vector2 hit, bool preferNearB)
    {
        hit = default; bool found = false; float best = float.MaxValue;
        foreach (var (e1, e2) in GetBoundaryEdges(room))
        {
            if (SegIntersectOrTouchEps(A, B, e1, e2, EDGE_EPS, out Vector2 inter))
            {
                float score = preferNearB ? (inter - B).sqrMagnitude : (inter - A).sqrMagnitude;
                if (score < best) { best = score; hit = inter; found = true; }
            }
        }
        return found;
    }

    private void DetermineSegmentForRoom(Room room, Vector2 aOrig, Vector2 bOrig, out Vector2 A, out Vector2 B, out bool insideInsideCase, out bool A_fromHit, out bool B_fromHit, out bool haveValidPair)
    {
        insideInsideCase = false; A_fromHit = B_fromHit = false; haveValidPair = false;
        A = aOrig; B = bOrig;

        bool aInside = PointInPolygon(aOrig, room.checkpoints);
        bool bInside = PointInPolygon(bOrig, room.checkpoints);

        var intersections = DedupIntersections(GetLinePolygonIntersections(aOrig, bOrig, room.checkpoints));

        Vector2 dir = (bOrig - aOrig);
        float dirLen2 = dir.sqrMagnitude + 1e-9f;
        var hits = intersections
            .Select(p => (p, t: Mathf.Clamp01(Vector2.Dot(p - aOrig, dir) / dirLen2)))
            .OrderBy(x => x.t)
            .ToList();

        if (!aInside && !bInside)
        {
            if (hits.Count >= 2)
            {
                int pickI = -1; float pickLen = -1f;
                for (int i = 0; i < hits.Count - 1; i++)
                {
                    float midT = 0.5f * (hits[i].t + hits[i + 1].t);
                    Vector2 mid = aOrig + (bOrig - aOrig) * midT;
                    if (PointInPolygon(mid, room.checkpoints))
                    {
                        float segLen = hits[i + 1].t - hits[i].t;
                        if (segLen > pickLen) { pickLen = segLen; pickI = i; }
                    }
                }
                if (pickI >= 0)
                {
                    A = hits[pickI].p; A_fromHit = true;
                    B = hits[pickI + 1].p; B_fromHit = true;
                    haveValidPair = true;
                }
            }
        }
        else if (!aInside && bInside)
        {
            if (hits.Count >= 1)
            {
                A = hits.OrderBy(h => Vector2.SqrMagnitude(h.p - bOrig)).First().p;
                A_fromHit = true; haveValidPair = true;
            }
            else if (TryBoundaryIntersection(room, aOrig, bOrig, out var h, preferNearB: true))
            {
                A = h; A_fromHit = true; haveValidPair = true;
            }
        }
        else if (aInside && !bInside)
        {
            if (hits.Count >= 1)
            {
                B = hits.OrderBy(h => Vector2.SqrMagnitude(h.p - aOrig)).First().p;
                B_fromHit = true; haveValidPair = true;
            }
            else if (TryBoundaryIntersection(room, aOrig, bOrig, out var h, preferNearB: false))
            {
                B = h; B_fromHit = true; haveValidPair = true;
            }
        }
        else
        {
            insideInsideCase = true; haveValidPair = true;
        }
    }

    private GameObject SpawnEdgeCheckpoint(Room room, LoopMap map, Vector3 pos, Vector2 p2)
    {
        InsertPointIntoWall(room, p2);
        var go = Instantiate(checkPointManager.checkpointPrefab, pos, Quaternion.identity);
        go.transform.SetParent(null, true);
        map.CheckpointsGO.Add(go);
        return go;
    }

    private GameObject SpawnMarker(LoopMap map, Vector3 pos)
    {
        var go = Instantiate(checkPointManager.checkpointPrefab, pos, Quaternion.identity);
        go.transform.SetParent(null, true);
        map.CheckpointsGO.Add(go);
        return go;
    }

    private void SpawnInternalIntersections(Room room, LoopMap map, Vector2 A, Vector2 B, List<GameObject> bucket)
    {
        foreach (var wall in room.wallLines.ToList())
        {
            Vector2 w1 = new(wall.start.x, wall.start.z);
            Vector2 w2 = new(wall.end.x, wall.end.z);

            if (SegIntersectOrTouchEps(A, B, w1, w2, EDGE_EPS, out Vector2 inter))
            {
                if ((inter - A).sqrMagnitude < DUP_EPS2 || (inter - B).sqrMagnitude < DUP_EPS2) continue;

                var ipGO = Instantiate(checkPointManager.checkpointPrefab, new Vector3(inter.x, 0, inter.y), Quaternion.identity);
                ipGO.transform.SetParent(null, true);
                map.CheckpointsGO.Add(ipGO);
                bucket.Add(ipGO);
            }
        }
    }

    private (HashSet<int> compsHit, bool[] compAnchored) BuildWallsGraphAndHits(Room room, Vector2 A, Vector2 B)
    {
        int n = room.wallLines.Count;
        bool[] anchored = new bool[n];
        List<int>[] adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        for (int i = 0; i < n; i++)
        {
            var wl = room.wallLines[i];
            Vector2 s = new(wl.start.x, wl.start.z);
            Vector2 e = new(wl.end.x, wl.end.z);
            anchored[i] = OnBoundary(room, s, EDGE_EPS) || OnBoundary(room, e, EDGE_EPS);
        }

        for (int i = 0; i < n; i++)
        {
            var wi = room.wallLines[i];
            Vector2 a1 = new(wi.start.x, wi.start.z);
            Vector2 a2 = new(wi.end.x, wi.end.z);
            for (int j = i + 1; j < n; j++)
            {
                var wj = room.wallLines[j];
                Vector2 b1 = new(wj.start.x, wj.start.z);
                Vector2 b2 = new(wj.end.x, wj.end.z);

                if (SegIntersectOrTouchEps(a1, a2, b1, b2, EDGE_EPS, out _)
                    || (a1 - b1).sqrMagnitude < DUP_EPS2 || (a1 - b2).sqrMagnitude < DUP_EPS2
                    || (a2 - b1).sqrMagnitude < DUP_EPS2 || (a2 - b2).sqrMagnitude < DUP_EPS2)
                {
                    adj[i].Add(j);
                    adj[j].Add(i);
                }
            }
        }

        int[] comp = new int[n];
        Array.Fill(comp, -1);
        List<bool> compHasAnchor = new();

        int cid = 0;
        for (int i = 0; i < n; i++)
        {
            if (comp[i] != -1) continue;
            bool hasAnchor = false;
            Stack<int> st = new(); st.Push(i);
            comp[i] = cid;
            while (st.Count > 0)
            {
                int u = st.Pop();
                if (anchored[u]) hasAnchor = true;
                foreach (var v in adj[u])
                    if (comp[v] == -1) { comp[v] = cid; st.Push(v); }
            }
            compHasAnchor.Add(hasAnchor);
            cid++;
        }

        HashSet<int> hitComps = new();
        for (int i = 0; i < n; i++)
        {
            var wl = room.wallLines[i];
            Vector2 s = new(wl.start.x, wl.start.z);
            Vector2 e = new(wl.end.x, wl.end.z);
            if (SegIntersectOrTouchEps(A, B, s, e, EDGE_EPS, out _))
                hitComps.Add(comp[i]);
        }

        return (hitComps, compHasAnchor.ToArray());
    }

    private bool ShouldSplitSegment(Room room, Vector2 A, Vector2 B, bool A_onBoundary, bool B_onBoundary)
    {
        var (compsHit, compAnchored) = BuildWallsGraphAndHits(room, A, B);
        bool hitAnyAnchoredComp = compsHit.Any(c => compAnchored[c]);
        int anchoredCompsHitCount = compsHit.Where(c => compAnchored[c]).Distinct().Count();

        return (A_onBoundary && B_onBoundary)
            || ((A_onBoundary || B_onBoundary) && hitAnyAnchoredComp)
            || (anchoredCompsHitCount >= 2);
    }

    private IEnumerator WaitAndSplitRooms(List<Room> rooms)
    {
        yield return null; // chờ 1 frame

        foreach (var r in rooms)
            splitRoomManager.DetectAndSplitRoomIfNecessary(r);

        checkPointManager.RedrawAllRooms();
    }
    private bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        int crossings = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            if (((a.y > point.y) != (b.y > point.y)) &&
                (point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y + 1e-6f) + a.x))
                crossings++;
        }
        return (crossings % 2 == 1);
    }
    private void InsertPointIntoWall(Room room, Vector2 point)
    {
        List<(int index, WallLine wall)> toSplit = new List<(int, WallLine)>();

        for (int i = 0; i < room.wallLines.Count; i++)
        {
            var wall = room.wallLines[i];
            Vector2 w1 = new Vector2(wall.start.x, wall.start.z);
            Vector2 w2 = new Vector2(wall.end.x, wall.end.z);

            if (PointOnSegment(point, w1, w2, 0.001f))
            {
                toSplit.Add((i, wall));
            }
        }

        // Cắt tất cả line tìm được (theo thứ tự ngược để tránh index lệch)
        for (int j = toSplit.Count - 1; j >= 0; j--)
        {
            var (index, wall) = toSplit[j];
            Vector3 point3D = new Vector3(point.x, 0, point.y);

            // Thêm checkpoint nếu chưa có
            if (!room.checkpoints.Any(p => Vector2.Distance(p, point) < 0.001f))
                room.checkpoints.Add(point);

            WallLine firstHalf = new WallLine { start = wall.start, end = point3D, type = wall.type };
            WallLine secondHalf = new WallLine { start = point3D, end = wall.end, type = wall.type };

            room.wallLines.RemoveAt(index);
            room.wallLines.Insert(index, secondHalf);
            room.wallLines.Insert(index, firstHalf);
        }

        // Lưu và vẽ lại
        if (toSplit.Count > 0)
        {
            RoomStorage.UpdateOrAddRoom(room);
            checkPointManager.RedrawAllRooms();
        }
    }
    private bool PointOnSegment(Vector2 p, Vector2 a, Vector2 b, float tolerance)
    {
        float cross = Mathf.Abs((p.y - a.y) * (b.x - a.x) - (p.x - a.x) * (b.y - a.y));
        if (cross > tolerance) return false;

        float dot = (p.x - a.x) * (b.x - a.x) + (p.y - a.y) * (b.y - a.y);
        if (dot < 0) return false;

        float sqLen = (b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y);
        if (dot > sqLen) return false;

        return true;
    }
    private List<Vector2> GetLinePolygonIntersections(Vector2 a, Vector2 b, List<Vector2> polygon)
    {
        List<Vector2> intersections = new();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 p1 = polygon[i];
            Vector2 p2 = polygon[(i + 1) % polygon.Count];
            if (LineSegmentsIntersect(a, b, p1, p2, out Vector2 ip))
            {
                if (!intersections.Any(p => Vector2.Distance(p, ip) < 0.001f))
                    intersections.Add(ip);
            }
        }
        return intersections;
    }

    public static bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out Vector2 intersection)
    {
        intersection = Vector2.zero;

        float A1 = p2.y - p1.y;
        float B1 = p1.x - p2.x;
        float C1 = A1 * p1.x + B1 * p1.y;

        float A2 = q2.y - q1.y;
        float B2 = q1.x - q2.x;
        float C2 = A2 * q1.x + B2 * q1.y;

        float delta = A1 * B2 - A2 * B1;
        if (Mathf.Abs(delta) < 0.0001f) return false; // Song song

        float x = (B2 * C1 - B1 * C2) / delta;
        float y = (A1 * C2 - A2 * C1) / delta;

        Vector2 r = new Vector2(x, y);
        if (IsPointOnSegment(r, p1, p2) && IsPointOnSegment(r, q1, q2))
        {
            intersection = r;
            return true;
        }

        return false;
    }
    public static bool IsPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        return Mathf.Min(a.x, b.x) - 0.001f <= p.x && p.x <= Mathf.Max(a.x, b.x) + 0.001f &&
               Mathf.Min(a.y, b.y) - 0.001f <= p.y && p.y <= Mathf.Max(a.y, b.y) + 0.001f;
    }

    //==== Logic Add cửa và cửa sổ ====
    public void HandleCheckpointPlacement(Vector3 position)
    {
        if (checkPointManager.selectedCheckpoint != null) return; // Nếu đã chọn điểm, không cần đặt mới

        // === Nếu là cửa sổ/cửa và không đang vẽ loop thì xử lý riêng ===
        if (checkPointManager.currentLineType == LineType.Door || checkPointManager.currentLineType == LineType.Window)
        {
            InsertDoorOrWindow(position, checkPointManager.currentLineType);
            return;
        }
    }
    void InsertDoorOrWindow(Vector3 clickPosition, LineType type)
    {
        if (firstDoorPoint == null)
        {
            // Lần click đầu tiên: chọn đoạn wall gần nhất
            float minDist = float.MaxValue;
            selectedRoomForDoor = null;
            selectedWallLineForDoor = null;

            foreach (Room room in RoomStorage.rooms)
            {
                foreach (var wl in room.wallLines)
                {
                    if (wl.type != LineType.Wall) continue; // chỉ chọn từ tường thường

                    Vector3 projected = checkPointManager.ProjectPointOnLineSegment(wl.start, wl.end, clickPosition);
                    float dist = Vector3.Distance(clickPosition, projected);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        selectedRoomForDoor = room;
                        selectedWallLineForDoor = wl;
                        firstDoorPoint = projected;
                    }
                }
            }

            if (selectedWallLineForDoor == null)
            {
                Debug.LogWarning("Không tìm thấy đoạn tường phù hợp.");
                firstDoorPoint = null;
                return;
            }

            // Hiển thị preview
            GameObject p1Obj = Instantiate(checkPointManager.checkpointPrefab, firstDoorPoint.Value, Quaternion.identity);
            p1Obj.name = $"{type}_P1_PREVIEW";

            Debug.Log($"[InsertDoorOrWindow] Đã chọn P1: {firstDoorPoint}");
            return;
        }
        else
        {
            // Lần click thứ 2: xác định đoạn cửa
            Vector3 p1 = firstDoorPoint.Value;
            Vector3 p2 = checkPointManager.ProjectPointOnLineSegment(selectedWallLineForDoor.start, selectedWallLineForDoor.end,
                clickPosition);

            if (Vector3.Distance(p1, p2) < 0.01f)
            {
                Debug.LogWarning("P2 trùng P1, không hợp lệ.");
                return;
            }

            // Xoá P1_PREVIEW nếu còn
            var preview = GameObject.Find($"{type}_P1_PREVIEW");
            if (preview != null) Destroy(preview);

            // Tạo WallLine mới cho cửa/cửa sổ
            WallLine door = new WallLine(p1, p2, type);
            selectedRoomForDoor.wallLines.Add(door);

            GameObject p1Obj = Instantiate(checkPointManager.checkpointPrefab, p1, Quaternion.identity);
            GameObject p2Obj = Instantiate(checkPointManager.checkpointPrefab, p2, Quaternion.identity);
            p1Obj.name = $"{type}_P1";
            p2Obj.name = $"{type}_P2";

            if (!checkPointManager.tempDoorWindowPoints.ContainsKey(selectedRoomForDoor.ID))
                checkPointManager.tempDoorWindowPoints[selectedRoomForDoor.ID] = new List<(WallLine, GameObject, GameObject)>();

            checkPointManager.tempDoorWindowPoints[selectedRoomForDoor.ID].Add((door, p1Obj, p2Obj));

            Debug.Log($"[InsertDoorOrWindow] Đã thêm {type}: {p1} -> {p2}");

            string roomID = selectedRoomForDoor.ID;

            // Reset
            firstDoorPoint = null;
            selectedWallLineForDoor = null;
            selectedRoomForDoor = null;

            checkPointManager.RedrawAllRooms();

            // Cập nhật lại mesh sàn (dù checkpoint không đổi, vẫn gọi vì có thể cần render lại mặt sàn)
            var floorGO = GameObject.Find($"RoomFloor_{roomID}");
            if (floorGO != null)
            {
                var meshCtrl = floorGO.GetComponent<RoomMeshController>();
                if (meshCtrl != null)
                    meshCtrl.GenerateMesh(RoomStorage.GetRoomByID(roomID).checkpoints);
            }
            else
            {
                Debug.LogWarning($"Không tìm thấy RoomFloor_{roomID} để cập nhật mesh.");
            }
        }
    }
}
