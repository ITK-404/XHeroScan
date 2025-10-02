using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class MovePointManager : MonoBehaviour
{
    #region Variables
    public float WELD_ON = 0.5f;    // <= khoảng này thì dính + snap trùng
    public float WELD_OFF = 0.6f;    // > khoảng này thì tách
    public Dictionary<string, List<GameObject>> ExtraCheckpointVisuals = new Dictionary<string, List<GameObject>>();


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
    public readonly Dictionary<GameObject, HashSet<GameObject>> _weldAdj = new();

    public static MovePointManager Instance;

    public void AddEdge(GameObject a, GameObject b)
    {
        if (a == null || b == null || a == b) return;
        if (!_weldAdj.TryGetValue(a, out var sa)) { sa = new HashSet<GameObject>(); _weldAdj[a] = sa; }
        if (!_weldAdj.TryGetValue(b, out var sb)) { sb = new HashSet<GameObject>(); _weldAdj[b] = sb; }
        sa.Add(b); sb.Add(a);
    }

    public static float XZDist(Vector3 a, Vector3 b)
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

    public void MoveSelectedCheckpoint()
    {
        var handler = new MainCheckpointHandler(checkPointManager, this, splitRoomManager);
        handler.MoveSelectedCheckpoint();
    }
public bool MoveSelectedCheckpointExtra()
    {
        var handler = new ExtraCheckpointHandler(checkPointManager, this, splitRoomManager);
        return handler.MoveSelectedCheckpointExtra();
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

    public void FastRebuildPerimeter(string roomID, List<GameObject> loop)
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
        }
        
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
