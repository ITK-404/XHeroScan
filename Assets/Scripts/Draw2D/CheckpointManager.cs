using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class CheckpointManager : MonoBehaviour
{
    #region Variables
    public static CheckpointManager Instance;

    [Header("Prefabs")]
    public GameObject checkpointPrefab;

    public DrawingTool DrawingTool;

    public LineType currentLineType = LineType.Wall;
    public List<WallLine> wallLines = new List<WallLine>();
    public List<Room> rooms = new List<Room>();

    [Header("Camera")]
    public Camera drawingCamera; // Gán Camera chính vẽ 2D

    public bool isMovingCheckpoint = false;
    public List<GameObject> currentCheckpoints = new List<GameObject>();
    public GameObject selectedCheckpoint = null; // Điểm được chọn để di chuyển  
    public bool isDragging = false; // Kiểm tra xem có đang kéo điểm không 
    public bool isPreviewing = false; // Trạng thái preview
    public bool isClosedLoop = false; // Biến kiểm tra xem mạch đã khép kín chưa 

    public bool IsDraggingRoom = false;
    public GameObject previewCheckpoint = null;

    private List<List<GameObject>> allCheckpoints = new List<List<GameObject>>();

    public List<List<GameObject>> AllCheckpoints =>
        allCheckpoints; // Truy cập danh sách tất cả các checkpoint từ bên ngoài

    public Dictionary<string, GameObject> RoomFloorMap = new(); // roomID -> floor GameObject

    private float closeThreshold = 0.2f; // Khoảng cách tối đa để chọn điểm
    private Vector3 previewPosition; // Vị trí preview
    public GameObject firstPoint = null;

    private SplitRoomManager splitRoomManager;
    private HandleCheckpointManger handleCheckpointManger;
    private MovePointManager movePointManager;

    // Map loop checkpoint list => Room ID
    public List<LoopMap> loopMappings = new List<LoopMap>();
    // Lưu lại tất cả các cửa / cửa sổ để chèn lại sau khi rebuild wallLines

    // [RoomID] -> List<(WallLine, GameObject p1, GameObject p2)>
    public Dictionary<string, List<(WallLine line, GameObject p1, GameObject p2)>> tempDoorWindowPoints
        = new Dictionary<string, List<(WallLine, GameObject, GameObject)>>();
    public string lastSelectedRoomID = null;
    #endregion

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // splitRoomManager = FindFirstObjectByType<SplitRoomManager>();
        handleCheckpointManger = FindFirstObjectByType<HandleCheckpointManger>();
        movePointManager = FindFirstObjectByType<MovePointManager>();
        LoadPointsFromStorage();
    }

    void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            isPreviewing = false;
            DrawingTool.ClearPreviewLine();
            if (previewCheckpoint != null)
            {
                Destroy(previewCheckpoint);
                previewCheckpoint = null;
            }
            return;
        }

        if (Input.GetMouseButton(0))
        {
            isPreviewing = true;
            previewPosition = GetWorldPositionFromScreen(Input.mousePosition);

            // Nếu đã có điểm đầu thì vẽ preview line đến chuột
            if (firstPoint != null)
            {
                Vector3 start = firstPoint.transform.position;
                DrawingTool.DrawPreviewLine(start, previewPosition);
            }

            if (previewCheckpoint == null)
            {
                previewCheckpoint = Instantiate(checkpointPrefab, previewPosition, Quaternion.identity);
                previewCheckpoint.name = "PreviewCheckpoint";
            }
            previewCheckpoint.transform.position = previewPosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isPreviewing = false;
            DrawingTool.ClearPreviewLine();

            if (previewCheckpoint != null)
            {
                Destroy(previewCheckpoint);
            }
            InteractionFlags.IsRoomFloorDragging = true; // dùng tạm khoa khóa để đặt
            if (currentLineType == LineType.Wall)
            {
                handleCheckpointManger.HandleSingleWallPlacement(previewPosition);
                // handleCheckpointManger.HandleWallLoopPlacement(previewPosition);
            }
            // else
            //     handleCheckpointManger.HandleCheckpointPlacement(previewPosition);

            DeselectCheckpoint();
            isDragging = false;
        }
    }

    public void SelectCheckpoint()
    {
        Vector3 clickPosition = GetWorldPositionFromScreen(Input.mousePosition);
        TrySelectCheckpoint(clickPosition);
    }

    public string FindRoomIDByPoint(Vector3 worldPos)
    {
        return FindRoomByPoint(worldPos)?.ID;
    }

    public Room FindRoomByPoint(Vector3 worldPos)
    {
        Vector2 point2D = new Vector2(worldPos.x, worldPos.z);
        foreach (Room room in RoomStorage.rooms)
        {
            if (IsPointInPolygon(point2D, room.checkpoints))
            {
                return room;
            }
        }

        return null;
    }

    // Hàm kiểm tra điểm có nằm trong polygon (ray casting algorithm)
    public static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        int j = polygon.Count - 1;
        bool inside = false;

        for (int i = 0; i < polygon.Count; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public Vector3 ProjectPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        a.y = 0;
        b.y = 0;
        point.y = 0;

        Vector3 ab = b - a;
        float t = Vector3.Dot(point - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }


    public void RedrawAllRooms()
    {
        // ===== Layering: room redraw ở index = 2 =====
        const int REDRAW_INDEX = 2;        // phòng luôn trên floor
        const float LAYER_STEP_Y = 0.002f;   // mỗi index cách nhau ~2mm
        const float LINE_LIFT = 0.0015f;  // nhô thêm chút để tránh z-fighting
        float yLine = REDRAW_INDEX * LAYER_STEP_Y + LINE_LIFT;

        // Vẽ lại từ đầu
        DrawingTool.ClearAllLines();

        // Đảm bảo dict tồn tại
        tempDoorWindowPoints ??= new Dictionary<string, List<(WallLine, GameObject, GameObject)>>();

        // Vẽ line + đồng bộ (reuse) handle cửa/cửa sổ
        foreach (Room room in RoomStorage.rooms)
        {
            // Lấy danh sách mapping hiện có cho room (nếu chưa có, tạo list rỗng)
            if (!tempDoorWindowPoints.TryGetValue(room.ID, out var dwList) || dwList == null)
            {
                dwList = new List<(WallLine, GameObject, GameObject)>();
                tempDoorWindowPoints[room.ID] = dwList;
            }

            // Set các line Door/Window đang tồn tại để dọn entry mồ côi
            var aliveDW = new HashSet<WallLine>(
                room.wallLines.Where(w => (w.type == LineType.Door || w.type == LineType.Window) && w.isVisible)
            );

            foreach (var wl in room.wallLines)
            {
                if (wl.type != LineType.Wall) continue;
                if (!wl.isVisible) continue;

                // --- dùng Y theo index 2 khi vẽ ---
                Vector3 s = wl.start; s.y = yLine;
                Vector3 e = wl.end; e.y = yLine;

                DrawingTool.currentLineType = wl.type;
                DrawingTool.DrawLineAndDistance(s, e, room.thickness);

                // chỉ sync handle cho cửa/cửa sổ
                if (wl.type != LineType.Door && wl.type != LineType.Window) continue;

                // tìm entry mapping cho wl
                int idx = dwList.FindIndex(t => ReferenceEquals(t.Item1, wl));
                if (idx >= 0)
                {
                    // đã có entry -> cập nhật vị trí & bù handle nếu thiếu
                    var (lineRef, p1, p2) = dwList[idx];

                    if (p1 == null) p1 = Instantiate(checkpointPrefab, s, Quaternion.identity);
                    else p1.transform.position = s;

                    if (p2 == null) p2 = Instantiate(checkpointPrefab, e, Quaternion.identity);
                    else p2.transform.position = e;

                    // ghi lại entry đã được cập nhật
                    dwList[idx] = (lineRef, p1, p2);
                }
                else
                {
                    // chưa có entry -> tạo mới 2 handle đúng dữ liệu đang lưu
                    var p1GO = Instantiate(checkpointPrefab, s, Quaternion.identity);
                    var p2GO = Instantiate(checkpointPrefab, e, Quaternion.identity);
                    dwList.Add((wl, p1GO, p2GO));
                }
            }

            // Dọn các entry mồ côi (line đã bị xóa hoặc ẩn) + đảm bảo handle đúng Y lớp 2
            for (int i = dwList.Count - 1; i >= 0; i--)
            {
                var (lineRef, p1, p2) = dwList[i];
                if (!aliveDW.Contains(lineRef))
                {
                    if (p1) Destroy(p1);
                    if (p2) Destroy(p2);
                    dwList.RemoveAt(i);
                }
                else
                {
                    // re-raise đến yLine nếu handle bị tụt Y
                    Vector3 s = lineRef.start; s.y = yLine;
                    Vector3 e = lineRef.end; e.y = yLine;

                    if (p1 == null) p1 = Instantiate(checkpointPrefab, s, Quaternion.identity);
                    else p1.transform.position = s;

                    if (p2 == null) p2 = Instantiate(checkpointPrefab, e, Quaternion.identity);
                    else p2.transform.position = e;

                    dwList[i] = (lineRef, p1, p2);
                }
            }
        }

    }

    bool TrySelectCheckpoint(Vector3 position)
    {
        float minDistance = closeThreshold;
        GameObject nearestCheckpoint = null;

        foreach (var loop in allCheckpoints)
        {
            foreach (var checkpoint in loop)
            {
                if (checkpoint == null) continue;

                float distance = Vector3.Distance(checkpoint.transform.position, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCheckpoint = checkpoint;
                }
            }
        }

        if (!isClosedLoop)
        {
            foreach (var checkpoint in currentCheckpoints)
            {
                if (checkpoint == null) continue;

                float distance = Vector3.Distance(checkpoint.transform.position, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCheckpoint = checkpoint;
                }
            }
        }

        foreach (var kvp in tempDoorWindowPoints)
        {
            foreach (var (line, p1GO, p2GO) in kvp.Value)
            {
                if (p1GO != null)
                {
                    float dist1 = Vector3.Distance(p1GO.transform.position, position);
                    if (dist1 < minDistance)
                    {
                        minDistance = dist1;
                        nearestCheckpoint = p1GO;
                    }
                }

                if (p2GO != null)
                {
                    float dist2 = Vector3.Distance(p2GO.transform.position, position);
                    if (dist2 < minDistance)
                    {
                        minDistance = dist2;
                        nearestCheckpoint = p2GO;
                    }
                }
            }
        }

        if (nearestCheckpoint != null)
        {
            selectedCheckpoint = nearestCheckpoint;
            return true;
        }

        return false;
    }

    public void ToggleConnectionBetweenCheckpoints(GameObject pointA, GameObject pointB)
    {
        Vector3 start = pointA.transform.position;
        Vector3 end = pointB.transform.position;

        string roomID = FindRoomIDByPoint(start);
        if (string.IsNullOrEmpty(roomID)) return;

        if (!RoomFloorMap.TryGetValue(roomID, out GameObject floorGO)) return;
        Room room = RoomStorage.GetRoomByID(roomID);
        if (room == null) return;

        // Kiểm tra đã tồn tại line chưa
        WallLine existingLine = room.wallLines.FirstOrDefault(w =>
            (Vector3.Distance(w.start, start) < 0.01f && Vector3.Distance(w.end, end) < 0.01f) ||
            (Vector3.Distance(w.start, end) < 0.01f && Vector3.Distance(w.end, start) < 0.01f)
        );

        if (existingLine != null)
        {
            float length = Vector3.Distance(existingLine.start, existingLine.end);

            if (length > 0.01f)
            {
                room.wallLines.Remove(existingLine);
                Debug.Log($"[Disconnect] Gỡ nối {pointA.name} ↔ {pointB.name}");
            }
            else
            {
                Debug.LogWarning($"[GIỮ LẠI] Không gỡ vì line = {length:F2} -> giữ kết nối.");
            }
        }
        else
        {
            WallLine line = new WallLine(start, end, LineType.Wall);
            room.wallLines.Add(line);
            Debug.Log($"[Connect] Nối {pointA.name} ↔ {pointB.name}");
        }

        RoomStorage.UpdateOrAddRoom(room);
        DrawingTool.ClearAllLines();
        RedrawAllRooms();

        splitRoomManager.DetectAndSplitRoomIfNecessary(room);
    }

    public string FindRoomIDForLoop(List<GameObject> loop)
    {
        foreach (var mapping in loopMappings)
        {
            if (ReferenceEquals(mapping.CheckpointsGO, loop)) return mapping.RoomID;
        }

        Debug.LogWarning("Loop không tìm thấy RoomID!");
        return null;
    }

    public void DeselectCheckpoint()
    {
        selectedCheckpoint = null;
        isMovingCheckpoint = false;
    }

    private EditRoomCommandCreator editRoomCommand;
    public void InitAndClearData()
    {
        editRoomCommand = new();
    }

    public void TryAddChangedRoomID(string roomID)
    {
        editRoomCommand.TryAddChangedRoomID(roomID);
    }

    public void CreateCommandHere()
    {
        editRoomCommand.CreateAndAddUndoList();
    }

    public Vector3 GetWorldPositionFromScreen(Vector3 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Mặt phẳng ngang y=0
        float distance;
        if (groundPlane.Raycast(ray, out distance))
        {
            return ray.GetPoint(distance);
        }

        return ray.GetPoint(5f);
    }
    const float layerStepY = 0.002f;
    const int floorIndex = 1;
    const float floorIndexY = floorIndex * layerStepY;
    const float roomIndexY = 2 * layerStepY;
    const float lineLift = 0.0005f;
    const float lineWidth = 0.03f;

    private HashSet<string> _alignedRooms = new HashSet<string>();
    void LoadPointsFromStorage()
    {
        var floors = FloorStorage.floors;
        // ====== FLOOR ======
        foreach (var floor in floors)
        {
            if (floor == null || floor.checkpoints == null || floor.checkpoints.Count < 3) continue;

            var cps = floor.checkpoints;

            // Parent visual
            var floorVis = new GameObject($"FloorVis_{floor.ID}");
            floorVis.tag = "RoomFloor";
            floorVis.transform.position = new Vector3(0f, floorIndexY, 0f);

            // ----- LineRenderer (viền) -----
            var lr = floorVis.AddComponent<LineRenderer>();
            lr.positionCount = cps.Count + 1;
            lr.loop = false;
            lr.widthMultiplier = lineWidth;
            lr.useWorldSpace = true;
            lr.numCornerVertices = 4;
            lr.sortingOrder = floorIndex;

            var unlit = Shader.Find("Unlit/Color");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            lr.material = new Material(unlit);
            if (unlit != null && unlit.name == "Unlit/Color")
                lr.material.SetColor("_Color", new Color(0.1f, 0.1f, 0.1f, 1f));

            for (int i = 0; i < cps.Count; i++)
                lr.SetPosition(i, new Vector3(cps[i].x, floorIndexY + lineLift, cps[i].y));
            lr.SetPosition(cps.Count, new Vector3(cps[0].x, floorIndexY + lineLift, cps[0].y));

            // ----- Mesh (mặt sàn) -----
            var mf = floorVis.AddComponent<MeshFilter>();
            var mr = floorVis.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = $"FloorMesh_{floor.ID}" };

            var verts = new List<Vector3>(cps.Count);
            for (int i = 0; i < cps.Count; i++)
                verts.Add(new Vector3(cps[i].x, floorIndexY, cps[i].y));

            var tris = new List<int>();
            for (int i = 1; i < cps.Count - 1; i++)
            {
                tris.Add(0);
                tris.Add(i);
                tris.Add(i + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);

            Vector2 min = cps[0], max = cps[0];
            for (int i = 1; i < cps.Count; i++) { min = Vector2.Min(min, cps[i]); max = Vector2.Max(max, cps[i]); }
            var size = max - min; if (size.x == 0) size.x = 1; if (size.y == 0) size.y = 1;
            var uvs = new Vector2[cps.Count];
            for (int i = 0; i < cps.Count; i++)
                uvs[i] = new Vector2((cps[i].x - min.x) / size.x, (cps[i].y - min.y) / size.y);

            mesh.SetUVs(0, new List<Vector2>(uvs));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var fill = new Material(Shader.Find("Standard"));
            fill.SetFloat("_Mode", 3);
            fill.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fill.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fill.SetInt("_ZWrite", 0);
            fill.DisableKeyword("_ALPHATEST_ON");
            fill.EnableKeyword("_ALPHABLEND_ON");
            fill.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            fill.renderQueue = 3000;
            fill.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            mr.sharedMaterial = fill;
            mr.sortingOrder = floorIndex;

            // ====== TẠO POINT MARKERS TỪ CHECKPOINTS ======
            foreach (var room in RoomStorage.rooms)
            {
                if (room == null || room.checkpoints == null || room.checkpoints.Count < 3) continue;

                // 1) Lấy heading mong muốn từ dữ liệu tường đã lưu (nếu room.headingCompass chưa có)
                ComputeRoomHeadingFromSavedLines(room);

                // 2) Xoay DỮ LIỆU ROOM quanh tâm để đồng bộ hình học với heading
                AlignRoomToHeading(room);

                // 3) Bây giờ mới sinh GameObjects dựa trên dữ liệu đã xoay
                AddGameObjectCheckPointToGlobalVariable(room);
                CreateRoomMeshCtrl(room);

                // 4) Vẽ line theo dữ liệu (EnsureLineHeadingFromRoom chỉ fill khi 0)
                DrawWallLineByRoom(room);
            }
            // Room
        }
        if (FloorStorage.floors.Count > 0)
        {
            // Resize lại camera khi load
            var floor = floors[0];
            CameraResizeByFloor.Instance.Resize(floor.checkpoints);
        }
        foreach (var room in RoomStorage.rooms)
        {
            if (room == null || room.checkpoints == null || room.checkpoints.Count < 3) continue;

            AlignRoomToSystemOrientation(room);

            AddGameObjectCheckPointToGlobalVariable(room);
            CreateRoomMeshCtrl(room);
            DrawWallLineByRoom(room);
        }
        if (RoomStorage.rooms.Count > 0)
        {
            var room = RoomStorage.rooms[0];
            CameraResizeByFloor.Instance.Resize(room.checkpoints);
        }
    }

    private const float EPS_LEN = 1e-3f;

    // 0° = +Z (Bắc), +90° = +X (Đông)
    private static Vector3 DirFromHeading(float deg)
    {
        return (Quaternion.Euler(0f, deg, 0f) * Vector3.back).normalized;
    }

    private float InferYawFromWalls(Room room, out int usedCount, bool ignoreManual = true, float minLen = 0.05f)
    {
        double sx = 0, cx = 0, wsum = 0;
        int cnt = 0;

        if (room.wallLines != null)
        {
            foreach (var wl in room.wallLines)
            {
                if (wl == null || !wl.isVisible) continue;
                if (wl.type != LineType.Wall) continue;
                if (ignoreManual && wl.isManualConnection) continue;

                Vector3 d = wl.end - wl.start; d.y = 0f;
                float len = d.magnitude;
                if (len < Mathf.Max(EPS_LEN, minLen)) continue;

                Vector3 gDir = d.normalized;               // hướng từ hình học hiện tại
                Vector3 sDir = DirFromHeading(wl.headingCompass); // hướng mục tiêu từ bearing

                // Góc có dấu để quay gDir -> sDir quanh trục Y (CCW dương)
                float delta = Vector3.SignedAngle(gDir, sDir, Vector3.up);

                float w = Mathf.Max(0.01f, len);
                sx += Mathf.Sin(delta * Mathf.Deg2Rad) * w;
                cx += Mathf.Cos(delta * Mathf.Deg2Rad) * w;
                wsum += w;
                cnt++;
            }
        }

        usedCount = cnt;
        if (wsum <= 1e-6) return 0f;

        // Trung bình tròn
        float phi = Mathf.Atan2((float)sx, (float)cx) * Mathf.Rad2Deg; // [-180..180]
        return phi; // quay +phi (CCW) sẽ làm g ~ s
    }

    private void AlignRoomToSystemOrientation(Room room)
    {
        if (room == null || room.checkpoints == null || room.checkpoints.Count < 3) return;
        if (_alignedRooms.Contains(room.ID)) return;

        // 1) Tâm polygon (x,z)
        Vector2 c2 = GeoUtil.Centroid(room.checkpoints);
        Vector3 c3 = new Vector3(c2.x, 0f, c2.y);

        // 2) Lấy phi từ các tường (KHÔNG dùng room.headingCompass nữa)
        int used;
        float phi = InferYawFromWalls(room, out used, ignoreManual: true, minLen: 0.05f);

        // Nếu không có tường hợp lệ → không xoay (hoặc tuỳ bạn có thể return)
        if (used == 0)
        {
            Debug.LogWarning($"[Align] room {room.ID}: no valid walls to infer heading.");
            _alignedRooms.Add(room.ID);
            return;
        }

        // 3) Quay tâm theo phi
        Quaternion R = Quaternion.Euler(0f, phi + 90f, 0f);

        // 4) Quay checkpoints quanh tâm
        for (int i = 0; i < room.checkpoints.Count; i++)
        {
            var p = room.checkpoints[i];
            Vector3 v = new Vector3(p.x, 0f, p.y);
            Vector3 v2 = R * (v - c3) + c3;
            room.checkpoints[i] = new Vector2(v2.x, v2.z);
        }

        // 5) Quay wallLines (giữ nguyên cao độ Y)
        if (room.wallLines != null)
        {
            foreach (var wl in room.wallLines)
            {
                Vector3 s2 = R * (wl.start - c3) + c3;
                Vector3 e2 = R * (wl.end - c3) + c3;

                wl.start = new Vector3(s2.x, wl.start.y, s2.z);
                wl.end = new Vector3(e2.x, wl.end.y, e2.z);
            }
        }

        _alignedRooms.Add(room.ID);

        Debug.Log($"[Align] room={room.ID} walls_used={used} phi={phi:0.0}°, totalYaw={phi + 180f:0.0}°");
    }

    public void AddGameObjectCheckPointToGlobalVariable(Room room)
    {
        var loopGO = new List<GameObject>();
        foreach (var p in room.checkpoints)
        {
            var wp = new Vector3(p.x, roomIndexY, p.y);
            loopGO.Add(Instantiate(checkpointPrefab, wp, Quaternion.identity));
        }
        loopMappings.Add(new LoopMap(room.ID, loopGO));
        allCheckpoints.Add(loopGO);
    }

    public void CreateRoomMeshCtrl(Room room)
    {
        GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
        RoomMeshController meshCtrl = floorGO.AddComponent<RoomMeshController>();
        Vector2 centerPostion = GeoUtil.Centroid(room.checkpoints);

        meshCtrl.Initialize(room.ID);
        meshCtrl.GenerateMesh(room.checkpoints);
        floorGO.transform.position = new Vector3(centerPostion.x, roomIndexY, centerPostion.y);
        //floorGO.transform.position = new Vector3(centerPostion.x, roomIndexY, centerPostion.z);

        Debug.Log("Center position: " + centerPostion);
        Debug.Log("FloorGO position: " + floorGO.transform.position);
        RoomFloorMap[room.ID] = floorGO;

    }

    public void DrawWallLineByRoom(Room room)
    {
        // room.headingCompass = 0f;
        // room.Compass = new Vector2(0f, 1f);
        foreach (var wl in room.wallLines)
        {
            // wl.headingCompass = HeadingManager.HeadingDeg(wl.start, wl.end);

            var s = new Vector3(wl.start.x, roomIndexY + lineLift, wl.start.z);
            var e = new Vector3(wl.end.x, roomIndexY + lineLift, wl.end.z);
            // Vẽ đoạn tường gốc như cũ
            DrawingTool.currentLineType = wl.type;
            DrawingTool.DrawLineAndDistance(s, e, room.thickness);
        }
    }

    public void ClearAllLines() => DrawingTool.ClearAllLines();
    public void DrawAllLinesFromRoomStorage() => DrawingTool.DrawAllLinesFromRoomStorage();
    public void DrawLineAndDistance(Vector3 start, Vector3 end, float width) => DrawingTool.DrawLineAndDistance(start, end, width);

    void ShowIncompleteLoopPopup()
    {
        // PopupController.Show(
        //     "Mạch chưa khép kín!\nBạn muốn xóa dữ liệu vẽ tạm không?",
        //     onYes: () =>
        //     {
        //         Debug.Log("Người dùng chọn YES: Xóa toàn bộ checkpoint + line.");
        //         DeleteCurrentDrawingData();
        //     },
        //     onNo: () =>
        //     {
        //         Debug.Log("Người dùng chọn NO: Tiếp tục vẽ để khép kín.");
        //     }
        // );
        //
        var popup = Instantiate(ModularPopup.PopupAsset.modularPopupYesNo).GetComponent<ModularPopup>();
        popup.AutoFindCanvasAndSetup();
        popup.Header = "Mạch chưa khép kín!\\nBạn muốn xóa dữ liệu vẽ tạm không?";
        popup.ClickYesEvent = () =>
        {
            Debug.Log("Người dùng chọn YES: Xóa toàn bộ checkpoint + line.");
            DeleteCurrentDrawingData();
        };
        popup.ClickNoEvent = () => { Debug.Log("Người dùng chọn NO: Tiếp tục vẽ để khép kín."); };
        // popup.EventWhenClickButtons = () => { BackgroundUI.Instance.Hide(); };
        // BackgroundUI.Instance.Show(popup.gameObject, null);

        popup.autoClearWhenClick = true;
    }

    public void DeleteCurrentDrawingData()
    {
        foreach (var cp in currentCheckpoints)
        {
            if (cp != null)
                Destroy(cp);
        }

        currentCheckpoints.Clear();

        wallLines.Clear();
        DrawingTool.ClearAllLines();

        isClosedLoop = false;
        previewCheckpoint = null;
        selectedCheckpoint = null;

        foreach (var list in tempDoorWindowPoints.Values)
        {
            foreach (var (line, p1, p2) in list)
            {
                if (p1 != null) Destroy(p1);
                if (p2 != null) Destroy(p2);
            }
        }

        tempDoorWindowPoints.Clear();

        Debug.Log("Đã xóa toàn bộ dữ liệu vẽ chưa khép kín. ");
    }

    public string GetSelectedRoomID()
    {
        if (selectedCheckpoint != null)
        {
            foreach (var loop in allCheckpoints)
            {
                if (loop.Contains(selectedCheckpoint))
                {
                    lastSelectedRoomID = FindRoomIDForLoop(loop);
                    return lastSelectedRoomID;
                }
            }
        }

        // Nếu đang kéo mesh -> lấy RoomID từ RoomMeshController đang hoạt động
        if (IsDraggingRoom)
        {
            var activeFloors = GameObject.FindObjectsByType<RoomMeshController>(FindObjectsSortMode.None);
            foreach (var floor in activeFloors)
            {
                if (floor.isDragging) // đã gán từ RoomMeshController
                {
                    lastSelectedRoomID = floor.RoomID;
                    return lastSelectedRoomID;
                }
            }
        }

        // Nếu đang không chọn gì nhưng vẫn có room đã chọn trước đó -> giữ nguyên
        return lastSelectedRoomID;
    }
    public void ClearSelectedRoom()
    {
        lastSelectedRoomID = null;
        selectedCheckpoint = null;
        IsDraggingRoom = false;
    }
    public void CreateRectangleRoom(float width, float height, Vector3 center, string ID, bool isCreateCommand)
    {
        DeleteCurrentDrawingData();

        Vector3 p1 = new Vector3(center.x - width / 2, 0, center.z - height / 2);
        Vector3 p2 = new Vector3(center.x - width / 2, 0, center.z + height / 2);
        Vector3 p3 = new Vector3(center.x + width / 2, 0, center.z + height / 2);
        Vector3 p4 = new Vector3(center.x + width / 2, 0, center.z - height / 2);

        List<Vector3> corners = new List<Vector3> { p1, p2, p3, p4 };

        // Tạo checkpoint prefab tại từng góc

        // Tạo wallLines & vẽ line
        for (int i = 0; i < currentCheckpoints.Count; i++)
        {
            Vector3 start = currentCheckpoints[i].transform.position;
            Vector3 end = (i == currentCheckpoints.Count - 1)
                ? currentCheckpoints[0].transform.position
                : currentCheckpoints[i + 1].transform.position;

            DrawingTool.DrawLineAndDistance(start, end, Room.Thickness);
            wallLines.Add(new WallLine(start, end, LineType.Wall));
        }

        // Tạo Room & lưu
        Room newRoom = new Room();
        if (!string.IsNullOrEmpty(ID))
        {
            newRoom.SetID(ID);
        }

        foreach (GameObject cp in currentCheckpoints)
        {
            Vector3 pos = cp.transform.position;
            newRoom.checkpoints.Add(new Vector2(pos.x, pos.z));
        }

        if (MeshGenerator.CalculateArea(newRoom.checkpoints) > 0)
        {
            newRoom.checkpoints.Reverse();
            Debug.Log("Đã đảo chiều polygon để mesh đúng mặt.");
        }

        newRoom.wallLines.AddRange(wallLines);

        RoomStorage.rooms.Add(newRoom);

        // Tạo mesh sàn
        CreateRoomMeshCtrl(newRoom);

        // Ánh xạ loop

        currentCheckpoints.Clear();
        wallLines.Clear();

        DrawingTool.DrawAllLinesFromRoomStorage();
        Debug.Log($"Đã tạo Room hình chữ nhật: {width} x {height} m, RoomID: {newRoom.ID}");

        if (!isCreateCommand) return;

        var data = new RectangularCreatingData();
        data.width = width;
        data.heigh = height;
        data.RoomID = newRoom.ID;
        data.position = center;
        //UndoRedoController.Instance.AddToUndo(new CreateRectangularCommand(data));
    }

    public void ClearRoomById(string roomID)
    {
        if (string.IsNullOrEmpty(roomID)) { Debug.LogWarning("[ClearRoomById] roomID rỗng."); return; }

        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null) { Debug.LogWarning($"[ClearRoomById] Không tìm thấy phòng: {roomID}"); return; }

        // XÓA EXTRA GOs theo roomID (placedPointsByRoom)
        var mpm = FindFirstObjectByType<MovePointManager>();
        if (mpm != null && mpm.placedPointsByRoom != null &&
            mpm.placedPointsByRoom.TryGetValue(roomID, out var extras) && extras != null)
        {
            foreach (var go in extras) if (go) Destroy(go);
            mpm.placedPointsByRoom.Remove(roomID);
        }

        // Xóa floor mesh
        var floors = GameObject.FindObjectsByType<RoomMeshController>(FindObjectsSortMode.None);
        foreach (var floor in floors) if (floor.RoomID == roomID) Destroy(floor.gameObject);

        // Xóa checkpoints (main) theo loop
        var loop = GetLoopByRoomID(roomID);
        if (loop != null)
        {
            if (
                selectedCheckpoint != null &&
                loop.Contains(selectedCheckpoint))
            {
                DeselectCheckpoint();
                isDragging = false;
                isMovingCheckpoint = false;
            }

            foreach (var cp in loop) if (cp) Destroy(cp);
            AllCheckpoints.Remove(loop);
        }
        LoopMap mapping = null;
        foreach (LoopMap item in loopMappings)
        {
            if (item.RoomID == room.ID)
            {
                mapping = item;
                Debug.Log("Có duplicate roomID trong loopMappings: " + room.ID);
                break;
            }
        }
        loopMappings.Remove(mapping);
        // Gỡ mapping khác
        RoomFloorMap.Remove(roomID);
        if (currentCheckpoints != null)
            currentCheckpoints.RemoveAll(go => !go); // dọn null

        // Xóa cửa/cửa sổ tạm
        if (
            tempDoorWindowPoints != null &&
            tempDoorWindowPoints.TryGetValue(roomID, out var doorPts))
        {
            foreach (var (_, p1GO, p2GO) in doorPts)
            {
                if (p1GO) Destroy(p1GO);
                if (p2GO) Destroy(p2GO);
            }
            tempDoorWindowPoints.Remove(roomID);
        }

        // Xóa dữ liệu phòng trong RoomStorage
        RoomStorage.rooms.RemoveAll(r => r.ID == roomID);

        // Vẽ lại
        ClearAllLines();
        RedrawAllRooms();
        ClearSelectedRoom();
        Debug.Log($"Đã xóa phòng: {roomID}");
    }

    private List<GameObject> GetLoopByRoomID(string roomID)
    {
        foreach (var lp in AllCheckpoints)
            if (FindRoomIDForLoop(lp) == roomID) return lp;
        return null;
    }

    public void RestoreRoom(Room roomSnapShot)
    {
        var room = new Room(roomSnapShot); // copy
        RoomStorage.UpdateOrAddRoom(room);

        CreateRoomMeshCtrl(room);
        DrawWallLineByRoom(room);
        AddGameObjectCheckPointToGlobalVariable(room);
        RedrawAllRooms();
    }
void ComputeRoomHeadingFromSavedLines(Room room)
{
    // Nếu room.headingCompass đã có (!=0) thì giữ; nếu chưa, lấy trung bình từ wl.headingCompass
    if (room == null) return;

    if (room.headingCompass != 0f) return;

    float sum = 0f; int cnt = 0;
    foreach (var wl in room.wallLines)
    {
        // Ưu tiên những wall có headingCompass đã lưu
        if (wl.headingCompass != 0f)
        {
            sum += wl.headingCompass;
            cnt++;
        }
    }
    if (cnt > 0)
    {
        room.headingCompass = NormalizeAngleDeg(sum / cnt);
    }
    else
    {
        // fallback: dùng hình học hiện tại
        room.headingCompass = GetDominantGeometryHeading(room);
    }
}

void AlignRoomToHeading(Room room, float epsDeg = 0.1f)
{
    if (room == null || room.checkpoints == null || room.checkpoints.Count < 3) return;

    // Tâm phòng (Vector2) – bạn đã có GeoUtil.Centroid
    Vector2 center = GeoUtil.Centroid(room.checkpoints);

    // Hướng hình học hiện tại (từ đoạn dài nhất)
    float currentGeomHeading = GetDominantGeometryHeading(room);

    // Hướng mong muốn (đã được ComputeRoomHeadingFromSavedLines suy ra)
    float targetHeading = NormalizeAngleDeg(room.headingCompass);

    // Góc cần quay
    float delta = NormalizeAngleDeg(targetHeading - currentGeomHeading);
    if (Mathf.Abs(delta) < epsDeg) return; // không cần xoay

// 1) Xoay checkpoints (polygon chính)
for (int i = 0; i < room.checkpoints.Count; i++)
    room.checkpoints[i] = RotateAround((Vector2)room.checkpoints[i], center, delta);

// 2) Xoay extra checkpoints
if (room.extraCheckpoints != null)
{
    for (int i = 0; i < room.extraCheckpoints.Count; i++)
        room.extraCheckpoints[i] = RotateAround((Vector2)room.extraCheckpoints[i], center, delta);
}

// 3) Xoay wallLines start/end
foreach (var wl in room.wallLines)
{
    wl.start = RotateAround((Vector3)wl.start, new Vector3(center.x, wl.start.y, center.y), delta);
    wl.end   = RotateAround((Vector3)wl.end,   new Vector3(center.x, wl.end.y, center.y), delta);
    wl.headingCompass = SegmentHeadingDeg(wl.start, wl.end);
}


    // 4) Cập nhật room.headingCompass (giữ nguyên target)
    room.headingCompass = targetHeading;
}
static float GetDominantGeometryHeading(Room room)
{
    // Lấy tường dài nhất (type=Wall hoặc bất kỳ nếu thiếu) để làm hướng hình học hiện tại
    WallLine best = null;
    float bestLen = -1f;
    foreach (var wl in room.wallLines)
    {
        float len = Vector3.Distance(wl.start, wl.end);
        if (len > bestLen)
        {
            bestLen = len;
            best = wl;
        }
    }
    if (best != null) return SegmentHeadingDeg(best.start, best.end);

    // Nếu chưa có wallLines (trường hợp tối giản), suy ra từ cạnh polygon dài nhất
    if (room.checkpoints != null && room.checkpoints.Count >= 2)
    {
        int n = room.checkpoints.Count;
        float bestLenCp = -1f;
        Vector2 a, b;
        a = room.checkpoints[0];
        for (int i = 0; i < n; i++)
        {
            a = room.checkpoints[i];
            b = room.checkpoints[(i + 1) % n];
            float len = Vector2.Distance(a, b);
            if (len > bestLenCp)
            {
                bestLenCp = len;
                // convert to Vector3 for heading calc
                Vector3 s = new Vector3(a.x, 0f, a.y);
                Vector3 e = new Vector3(b.x, 0f, b.y);
                // return ở cuối vòng để chắc chắn chọn cạnh dài nhất
                // nhưng ở đây ta lưu heading tạm thời:
            }
        }
        // Tính lại heading của cạnh dài nhất
        bestLenCp = -1f;
        float heading = 0f;
        for (int i = 0; i < n; i++)
        {
            var p0 = room.checkpoints[i];
            var p1 = room.checkpoints[(i + 1) % n];
            float len = Vector2.Distance(p0, p1);
            if (len > bestLenCp)
            {
                bestLenCp = len;
                heading = SegmentHeadingDeg(new Vector3(p0.x,0f,p0.y), new Vector3(p1.x,0f,p1.y));
            }
        }
        return heading;
    }
    return 0f;
}
    static float NormalizeAngleDeg(float deg)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }
    static float SegmentHeadingDeg(Vector3 s, Vector3 e)
    {
        // 0° = +Z (Bắc), 90° = +X (Đông). Dùng atan2(x, z)
        Vector3 d = e - s; d.y = 0f;
        if (d.sqrMagnitude < 1e-8f) return 0f;
        float rad = Mathf.Atan2(d.x, d.z);
        return NormalizeAngleDeg(rad * Mathf.Rad2Deg);
    }
static Vector2 RotateAround(Vector2 p, Vector2 pivot, float angleDeg)
{
    float rad = angleDeg * Mathf.Deg2Rad;
    float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
    Vector2 t = p - pivot;
    return new Vector2(t.x * cs - t.y * sn, t.x * sn + t.y * cs) + pivot;
}

static Vector3 RotateAround(Vector3 p, Vector3 pivot, float angleDeg)
{
    float rad = angleDeg * Mathf.Deg2Rad;
    float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
    Vector3 t = p - new Vector3(pivot.x, p.y, pivot.y);
    // Quay trên trục Y, dùng (x,z)
    float rx =  t.x * cs + t.z * sn;
    float rz = -t.x * sn + t.z * cs;
    return new Vector3(rx + pivot.x, p.y, rz + pivot.y);
}
}