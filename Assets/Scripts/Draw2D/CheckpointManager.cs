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
    public static CheckpointManager Instance;

    [Header("Prefabs")]
    public GameObject checkpointPrefab;

    public DrawingTool DrawingTool;
    public PenManager penManager;
    public UndoRedoManager undoRedoManager;
    public StoragePermissionRequester permissionRequester;

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

    public Dictionary<string, GameObject> RoomFloorMap = new(); // roomID → floor GameObject

    private float closeThreshold = 0.2f; // Khoảng cách tối đa để chọn điểm
    private Vector3 previewPosition; // Vị trí preview
    private GameObject firstPoint = null;

    private WallLine selectedWallLineForDoor; // đoạn tường được chọn
    private Room selectedRoomForDoor;
    private Room currentRoom; // Room hiện tại vừa được tạo khi vẽ loop

    private Vector3? firstDoorPoint = null; // lưu P1

    // Map loop checkpoint list => Room ID
    private List<LoopMap> loopMappings = new List<LoopMap>();
    // Lưu lại tất cả các cửa / cửa sổ để chèn lại sau khi rebuild wallLines

    // [RoomID] → List<(WallLine, GameObject p1, GameObject p2)>
    public Dictionary<string, List<(WallLine line, GameObject p1, GameObject p2)>> tempDoorWindowPoints
        = new Dictionary<string, List<(WallLine, GameObject, GameObject)>>();
    // Dictionary<string (roomID), List<(WallLine, GameObject, GameObject)>> tempDoorWindowPoints;

    // Dictionary<string, List<GameObject>> ExtraCheckpointVisuals;
    Dictionary<string, List<GameObject>> ExtraCheckpointVisuals = new Dictionary<string, List<GameObject>>();
    public string lastSelectedRoomID = null;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        LoadPointsFromRoomStorage();
    }

    void Update()
    {
        // AutoDetectRoomsNoGeometryUtils();

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

            if (currentLineType == LineType.Wall)
                HandleSingleWallPlacement(previewPosition);
            else
                HandleCheckpointPlacement(previewPosition);

            DeselectCheckpoint();
            isDragging = false;
        }
    }

    public void SelectCheckpoint()
    {
        Vector3 clickPosition = GetWorldPositionFromScreen(Input.mousePosition);
        TrySelectCheckpoint(clickPosition);
    }

    public void HandleCheckpointPlacement(Vector3 position)
    {
        if (selectedCheckpoint != null) return; // Nếu đã chọn điểm, không cần đặt mới
        
        // === Nếu là cửa sổ/cửa và không đang vẽ loop thì xử lý riêng ===
        if (currentLineType == LineType.Door || currentLineType == LineType.Window)
        {
            InsertDoorOrWindow(position, currentLineType);
            return;
        }
    }

    public void HandleSingleWallPlacement(Vector3 position)
    {
        if (ConnectManager.isConnectActive) return;

        List<GameObject> tempCreatedPoints = new();

        // 1. CLICK ĐẦU 
        if (firstPoint == null)
        {
            firstPoint = Instantiate(checkpointPrefab, position, Quaternion.identity);
            firstPoint.transform.SetParent(null);
            return;                         // click 2 mới xử lý
        }

        //  2. CLICK THỨ 2 
        Vector3 startWorld = firstPoint.transform.position;
        Vector3 endWorld = position;

        Vector2 aOrig = new(startWorld.x, startWorld.z);
        Vector2 bOrig = new(endWorld.x, endWorld.z);

        bool anyRoomUpdated = false; // có room nào thay đổi?
        bool firstPointInsideRoom = false; // point 1 có trong phòng nào không?
        List<Room> roomsToSplit = new(); // phòng sẽ split sau khi vẽ

        // 2.1. Duyệt qua snapshot rooms
        foreach (Room room in RoomStorage.rooms.ToList())
        {
            bool aInside = PointInPolygon(aOrig, room.checkpoints);
            bool bInside = PointInPolygon(bOrig, room.checkpoints);
            var intersections = GetLinePolygonIntersections(aOrig, bOrig, room.checkpoints);

            if (!aInside && !bInside && intersections.Count == 0)
                continue;                           // hoàn toàn không ảnh hưởng room

            if (aInside) firstPointInsideRoom = true;

            // --- lấy / tạo LoopMap ---
            var map = loopMappings.FirstOrDefault(m => m.RoomID == room.ID);
            if (map == null)
            {
                map = new LoopMap(room.ID, new List<GameObject>());
                loopMappings.Add(map);
                allCheckpoints.Add(map.CheckpointsGO);
            }
            // tính lại a, b sau khi cắt
            Vector2 a = aOrig, b = bOrig;
            if (!aInside)
            {
                if (intersections.Count > 0)
                    a = intersections.OrderBy(p => Vector2.Distance(p, b)).First();
                else continue;
            }

            if (!aInside && !bInside)
            {
                if (intersections.Count >= 2)
                {
                    a = intersections[0];
                    b = intersections[1];
                    aInside = bInside = true;
                }
                else continue;
            }
            else if (!aInside || !bInside)
            {
                if (intersections.Count >= 1)
                {
                    if (!aInside) a = intersections.OrderBy(p => Vector2.Distance(p, b)).First();
                    if (!bInside) b = intersections.OrderBy(p => Vector2.Distance(p, a)).First();
                    aInside = bInside = true;
                }
                else continue;
            }

            Vector3 start = new(a.x, 0, a.y);
            Vector3 end = new(b.x, 0, b.y);

            // 2.2. Chèn checkpoint vào cạnh nếu cần
            if (aInside &&
                room.wallLines.Any(w => PointOnSegment(a, new(w.start.x, w.start.z),
                                                          new(w.end.x, w.end.z), 0.001f)) &&
                !room.checkpoints.Any(p => Vector2.Distance(p, a) < 0.001f))
            {
                InsertPointIntoWall(room, a);
            }

            if (bInside &&
                room.wallLines.Any(w => PointOnSegment(b, new(w.start.x, w.start.z),
                                                          new(w.end.x, w.end.z), 0.001f)) &&
                !room.checkpoints.Any(p => Vector2.Distance(p, b) < 0.001f))
            {
                InsertPointIntoWall(room, b);
            }

            // 2.3. Giao cắt với các wall hiện có
            foreach (var wall in room.wallLines.ToList())
            {
                Vector2 w1 = new(wall.start.x, wall.start.z);
                Vector2 w2 = new(wall.end.x, wall.end.z);

                if (LineSegmentsIntersect(a, b, w1, w2, out Vector2 inter)) 
                {
                    Vector3 inter3D = new(inter.x, 0, inter.y);
                    GameObject ipGO = Instantiate(checkpointPrefab, inter3D, Quaternion.identity);
                    ipGO.transform.SetParent(null);
                    map.CheckpointsGO.Add(ipGO);

                    if (!room.checkpoints.Contains(inter))
                        room.checkpoints.Add(inter);

                    tempCreatedPoints.Add(ipGO); // cũng là point sinh tạm
                }
            }

            bool aOnEdge = room.wallLines.Any(w => PointOnSegment(a, new(w.start.x, w.start.z),
                                                                     new(w.end.x, w.end.z), 0.001f));
            bool bOnEdge = room.wallLines.Any(w => PointOnSegment(b, new(w.start.x, w.start.z),
                                                                     new(w.end.x, w.end.z), 0.001f));
            if (!aOnEdge && !bOnEdge) continue;

            // 2.4. Vẽ line mới
            DrawingTool.currentLineType = currentLineType;
            DrawingTool.DrawLineAndDistance(start, end);

            WallLine newline = new() { start = start, end = end, type = currentLineType };
            room.wallLines.Add(newline);
            DrawingTool.wallLines.Add(newline);

            RoomStorage.UpdateOrAddRoom(room);
            anyRoomUpdated = true;
            roomsToSplit.Add(room);
        } 

        // 3. KẾT THÚC
        if (!anyRoomUpdated)
        {
            // -> Không cập-nhật phòng nào: xoá mọi thứ vừa tạo
            if (firstPoint) Destroy(firstPoint);
            foreach (var p in tempCreatedPoints) if (p) Destroy(p);
        }
        else
        {
            // -> Có cập-nhật phòng
            if (!firstPointInsideRoom && firstPoint) Destroy(firstPoint); // point 1 ngoài -> xoá

            // foreach (var r in roomsToSplit.Distinct()) DetectAndSplitRoomIfNecessary(r);
            StartCoroutine(WaitAndSplitRooms(roomsToSplit.Distinct().ToList()));

            RedrawAllRooms();
        }

        firstPoint = null; // reset trạng thái click
    }
    private IEnumerator WaitAndSplitRooms(List<Room> rooms)
    {
        yield return null; // chờ 1 frame

        foreach (var r in rooms)
            DetectAndSplitRoomIfNecessary(r);

        RedrawAllRooms();
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
            RedrawAllRooms();
        }
    }

    // Kiểm tra point nằm trên đoạn
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

    private string FindRoomIDByPoint(Vector3 worldPos)
    {
        Vector2 point2D = new Vector2(worldPos.x, worldPos.z);
        foreach (Room room in RoomStorage.rooms)
        {
            if (IsPointInPolygon(point2D, room.checkpoints))
            {
                return room.ID;
            }
        }

        return null;
    }

    // Hàm kiểm tra điểm có nằm trong polygon (ray casting algorithm)
    private bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
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

    public void InsertDoorOrWindow(Vector3 clickPosition, LineType type)
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

                    Vector3 projected = ProjectPointOnLineSegment(wl.start, wl.end, clickPosition);
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
            GameObject p1Obj = Instantiate(checkpointPrefab, firstDoorPoint.Value, Quaternion.identity);
            p1Obj.name = $"{type}_P1_PREVIEW";

            Debug.Log($"[InsertDoorOrWindow] Đã chọn P1: {firstDoorPoint}");
            return;
        }
        else
        {
            // Lần click thứ 2: xác định đoạn cửa
            Vector3 p1 = firstDoorPoint.Value;
            Vector3 p2 = ProjectPointOnLineSegment(selectedWallLineForDoor.start, selectedWallLineForDoor.end,
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

            GameObject p1Obj = Instantiate(checkpointPrefab, p1, Quaternion.identity);
            GameObject p2Obj = Instantiate(checkpointPrefab, p2, Quaternion.identity);
            p1Obj.name = $"{type}_P1";
            p2Obj.name = $"{type}_P2";

            if (!tempDoorWindowPoints.ContainsKey(selectedRoomForDoor.ID))
                tempDoorWindowPoints[selectedRoomForDoor.ID] = new List<(WallLine, GameObject, GameObject)>();

            tempDoorWindowPoints[selectedRoomForDoor.ID].Add((door, p1Obj, p2Obj));

            Debug.Log($"[InsertDoorOrWindow] Đã thêm {type}: {p1} -> {p2}");

            string roomID = selectedRoomForDoor.ID;

            // Reset
            firstDoorPoint = null;
            selectedWallLineForDoor = null;
            selectedRoomForDoor = null;

            RedrawAllRooms();

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

    Vector3 ProjectPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(point - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    public void RedrawAllRooms()
    {
        DrawingTool.ClearAllLines();

        foreach (Room room in RoomStorage.rooms)
        {
            foreach (var wl in room.wallLines)
            {
                DrawingTool.currentLineType = wl.type;
                DrawingTool.DrawLineAndDistance(wl.start, wl.end);
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
                Debug.LogWarning($"[GIỮ LẠI] Không gỡ vì line = {length:F2} ➜ giữ kết nối.");
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

        DetectAndSplitRoomIfNecessary(room);
    }

    public void MoveSelectedCheckpoint()
    {
        if (IsClickingOnBackgroundBlackUI(Input.mousePosition))
        {
            Debug.Log("Đang nhấn Background Black ➜ Không move checkpoint");
            return;
        }

        if (selectedCheckpoint == null) return;

        Vector3 newPosition = GetWorldPositionFromScreen(Input.mousePosition);
        Vector3 oldPos = selectedCheckpoint.transform.position;

        // === Nếu là điểm cửa/cửa sổ ===
        foreach (var kvp in tempDoorWindowPoints)
        {
            foreach (var (line, p1GO, p2GO) in kvp.Value)
            {
                if (selectedCheckpoint == p1GO || selectedCheckpoint == p2GO)
                {
                    WallLine wall = FindClosestWallLine(line, kvp.Key);
                    if (wall == null) return;

                    Vector3 projected = ProjectPointOnLineSegment(wall.start, wall.end, newPosition);
                    if (selectedCheckpoint == p1GO) line.start = projected;
                    else line.end = projected;

                    selectedCheckpoint.transform.position = projected;
                    RedrawAllRooms();
                    return;
                }
            }
        }

        // === Nếu là checkpoint chính trong polygon ===
        selectedCheckpoint.transform.position = newPosition;

        foreach (var loop in allCheckpoints)
        {
            if (!loop.Contains(selectedCheckpoint)) continue;

            string roomID = FindRoomIDForLoop(loop);
            if (string.IsNullOrEmpty(roomID)) return;
            Room room = RoomStorage.GetRoomByID(roomID);
            if (room == null) return;

            bool isDuplicate = false;

            // ======= PHẦN 1: Cập nhật checkpoints chính =======
            List<Vector2> newCheckpoints = new();

            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 pos = loop[i].transform.position;

                for (int j = 0; j < i; j++)
                {
                    Vector3 otherPos = loop[j].transform.position;
                    if (Vector3.Distance(pos, otherPos) < 0.01f)
                    {
                        Debug.LogWarning($"[BỎ QUA] Điểm {i} trùng với điểm {j} ➜ Không update checkpoint để tránh mesh lỗi.");
                        isDuplicate = true;
                        break;
                    }
                }

                if (isDuplicate) break;

                newCheckpoints.Add(new Vector2(pos.x, pos.z));
            }

            // Gán mới toàn bộ nếu hợp lệ
            if (!isDuplicate)
            {
                room.checkpoints = newCheckpoints;
            }

            // ======= PHẦN 2: Nếu không duplicate, update wallLines =======
            if (!isDuplicate)
            {
                int wallLineIndex = 0;
                int wallCount = room.checkpoints.Count;
                for (int i = 0; i < room.wallLines.Count; i++)
                {
                    // if (room.wallLines[i].type != LineType.Wall || room.wallLines[i].isManualConnection) continue;

                    Vector2 p1 = room.checkpoints[wallLineIndex % wallCount];
                    Vector2 p2 = room.checkpoints[(wallLineIndex + 1) % wallCount];

                    room.wallLines[i].start = new Vector3(p1.x, 0, p1.y);
                    room.wallLines[i].end = new Vector3(p2.x, 0, p2.y);
                    wallLineIndex++;
                }
            }

            // ======= PHẦN 3: Cập nhật manual connections =======
            foreach (var line in room.wallLines)
            {
                // if (!line.isManualConnection) continue;

                bool movedStart = Vector3.Distance(line.start, oldPos) < 0.15f;
                bool movedEnd = Vector3.Distance(line.end, oldPos) < 0.15f;

                if (movedStart && movedEnd)
                {
                    Vector3 direction = (line.end - line.start).normalized;
                    if (direction == Vector3.zero)
                    {
                        int hash = oldPos.GetHashCode();
                        direction = Quaternion.Euler(0, hash % 360, 0) * Vector3.forward;
                    }

                    line.start = newPosition - direction * 0.001f;
                    line.end = newPosition + direction * 0.001f;
                }
                else if (movedStart)
                {
                    line.start = newPosition;
                }
                else if (movedEnd)
                {
                    line.end = newPosition;
                }
                else if (Vector3.Distance(line.start, line.end) < 0.001f)
                {
                    Vector3 direction = Quaternion.Euler(0, 137f, 0) * Vector3.forward;
                    line.start = newPosition - direction * 0.001f;
                    line.end = newPosition + direction * 0.001f;
                    Debug.LogWarning($"[Auto-fix] Manual line degenerate (start==end). Cưỡng bức kéo rộng tại vị trí: {newPosition}");
                }
            }

            // ======= PHẦN 4: Cập nhật cửa/cửa sổ =======
            foreach (var door in room.wallLines.Where(w => w.type != LineType.Wall))
            {
                WallLine parentWall = null;
                float minDistance = float.MaxValue;

                foreach (var wall in room.wallLines)
                {
                    if (wall.type != LineType.Wall) continue;

                    float dist = GetDistanceFromSegment(door.start, wall.start, wall.end)
                               + GetDistanceFromSegment(door.end, wall.start, wall.end);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        parentWall = wall;
                    }
                }

                if (parentWall == null) continue;

                float r1 = Mathf.Clamp01(GetRatioAlongLine(door.start, parentWall.start, parentWall.end));
                float r2 = Mathf.Clamp01(GetRatioAlongLine(door.end, parentWall.start, parentWall.end));

                door.start = Vector3.Lerp(parentWall.start, parentWall.end, r1);
                door.end = Vector3.Lerp(parentWall.start, parentWall.end, r2);

                if (tempDoorWindowPoints.TryGetValue(room.ID, out var doorsInRoom))
                {
                    foreach (var (line, p1GO, p2GO) in doorsInRoom)
                    {
                        p1GO.transform.position = line.start;
                        p2GO.transform.position = line.end;
                    }
                }
            }

            // ======= PHẦN 5: Cập nhật lại room và mesh =======
            RoomStorage.UpdateOrAddRoom(room);

            var floorGO = GameObject.Find($"RoomFloor_{roomID}");
            if (floorGO != null)
            {
                floorGO.GetComponent<RoomMeshController>()?.GenerateMesh(room.checkpoints);
            }

            DrawingTool.ClearAllLines();
            RedrawAllRooms();
            break;
        }
    }

    private void DetectAndSplitRoomIfNecessary(Room originalRoom)
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
            var loopMap = loopMappings.FirstOrDefault(lm => lm.RoomID == room.ID);
            if (loopMap != null)
            {
                foreach (var checkpointGO in loopMap.CheckpointsGO)
                {
                    if (checkpointGO != null) Destroy(checkpointGO);
                }
                loopMappings.Remove(loopMap);
                allCheckpoints.Remove(loopMap.CheckpointsGO);
            }

            // Xóa cửa sổ, cửa cũ liên quan room này
            if (tempDoorWindowPoints.ContainsKey(room.ID))
            {
                foreach (var (_, p1, p2) in tempDoorWindowPoints[room.ID])
                {
                    if (p1 != null) Destroy(p1);
                    if (p2 != null) Destroy(p2);
                }
                tempDoorWindowPoints.Remove(room.ID);
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
                GameObject cp = Instantiate(checkpointPrefab, worldPos, Quaternion.identity);
                loopGO.Add(cp);
            }

            allCheckpoints.Add(loopGO);
            loopMappings.Add(new LoopMap(room.ID, loopGO));

            // === Tạo lại mesh sàn
            GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
            floorGO.transform.position = Vector3.zero;
            var meshCtrl = floorGO.AddComponent<RoomMeshController>();

            Color floorColor = (colors != null && i < colors.Length) ? colors[i] : Color.white;
            meshCtrl.Initialize(room.ID, floorColor);

            // === Vẽ lại wallLines
            foreach (var wl in room.wallLines)
            {
                DrawingTool.currentLineType = wl.type;
                DrawingTool.DrawLineAndDistance(wl.start, wl.end);

                // Nếu là cửa/cửa sổ
                if (wl.type == LineType.Door || wl.type == LineType.Window)
                {
                    GameObject p1 = Instantiate(checkpointPrefab, wl.start, Quaternion.identity);
                    GameObject p2 = Instantiate(checkpointPrefab, wl.end, Quaternion.identity);
                    p1.name = $"{wl.type}_P1";
                    p2.name = $"{wl.type}_P2";

                    if (!tempDoorWindowPoints.ContainsKey(room.ID))
                        tempDoorWindowPoints[room.ID] = new List<(WallLine, GameObject, GameObject)>();

                    tempDoorWindowPoints[room.ID].Add((wl, p1, p2));
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
        DrawingTool.wallLines.RemoveAll(wl =>
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
            var loopMap = loopMappings.FirstOrDefault(lm => lm.RoomID == room.ID);
            if (loopMap != null)
            {
                foreach (var cp in loopMap.CheckpointsGO)
                    if (cp != null) Destroy(cp);

                loopMappings.Remove(loopMap);
                allCheckpoints.Remove(loopMap.CheckpointsGO);
            }

            // 3. Xóa mesh floor
            if (RoomFloorMap.TryGetValue(room.ID, out var oldGO))
            {
                Destroy(oldGO);
                RoomFloorMap.Remove(room.ID);
            }

            // 4. Xóa cửa / cửa sổ
            if (tempDoorWindowPoints.ContainsKey(room.ID))
            {
                foreach (var (_, p1, p2) in tempDoorWindowPoints[room.ID])
                {
                    if (p1 != null) Destroy(p1);
                    if (p2 != null) Destroy(p2);
                }
                tempDoorWindowPoints.Remove(room.ID);
            }
        }
    }

    private WallLine FindClosestWallLine(WallLine doorLine, string roomID)
    {
        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null) return null;

        WallLine closest = null;
        float minDist = float.MaxValue;

        foreach (var wall in room.wallLines)
        {
            if (wall.type != LineType.Wall) continue;

            float dist = GetDistanceFromSegment(doorLine.start, wall.start, wall.end)
                         + GetDistanceFromSegment(doorLine.end, wall.start, wall.end);

            if (dist < minDist)
            {
                minDist = dist;
                closest = wall;
            }
        }

        return closest;
    }

    private float GetDistanceFromSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 projected = ProjectPointOnLineSegment(a, b, point);
        return Vector3.Distance(point, projected);
    }

    private float GetRatioAlongLine(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        Vector3 ap = point - a;
        return Vector3.Dot(ap, ab) / ab.sqrMagnitude;
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

    // === Load points from RoomStorage
    void LoadPointsFromRoomStorage()
    {
        var rooms = RoomStorage.rooms;
        if (rooms.Count == 0)
        {
            Debug.Log("Không có Room nào để hiển thị.");
            return;
        }

        foreach (var room in rooms)
        {
            // === Tạo lại checkpoint GameObject từ room.checkpoints
            List<GameObject> loopGO = new List<GameObject>();
            foreach (var pt in room.checkpoints)
            {
                Vector3 worldPos = new Vector3(pt.x, 0, pt.y);
                GameObject cp = Instantiate(checkpointPrefab, worldPos, Quaternion.identity);
                loopGO.Add(cp);
            }

            // === Lưu vào ánh xạ checkpoint<->RoomID
            allCheckpoints.Add(loopGO);
            loopMappings.Add(new LoopMap(room.ID, loopGO));

            // === Tạo lại mesh sàn (có thể drag)
            GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
            floorGO.transform.position = Vector3.zero;
            floorGO.transform.rotation = Quaternion.identity;
            floorGO.transform.localScale = Vector3.one;
            var meshCtrl = floorGO.AddComponent<RoomMeshController>();
            meshCtrl.Initialize(room.ID); // tự gọi GenerateMesh(room.checkpoints)

            // === Vẽ lại các wallLines
            foreach (var wl in room.wallLines)
            {
                DrawingTool.currentLineType = wl.type;
                DrawingTool.DrawLineAndDistance(wl.start, wl.end);

                // Nếu là cửa hoặc cửa sổ: tạo 2 điểm đầu/cuối riêng
                if (wl.type == LineType.Door || wl.type == LineType.Window)
                {
                    GameObject p1 = Instantiate(checkpointPrefab, wl.start, Quaternion.identity);
                    GameObject p2 = Instantiate(checkpointPrefab, wl.end, Quaternion.identity);
                    p1.name = $"{wl.type}_P1";
                    p2.name = $"{wl.type}_P2";

                    if (!tempDoorWindowPoints.ContainsKey(room.ID))
                        tempDoorWindowPoints[room.ID] = new List<(WallLine, GameObject, GameObject)>();

                    tempDoorWindowPoints[room.ID].Add((wl, p1, p2));
                }
            }
        }

        Debug.Log($"[LoadPointsFromRoomStorage] Đã load lại {rooms.Count} phòng, {allCheckpoints.Count} loop.");
    }

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
        var popup = Instantiate(ModularPopup.Prefab);
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

        Debug.Log("Đã xóa toàn bộ dữ liệu vẽ chưa khép kín.");
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

        // Nếu đang kéo mesh ➜ lấy RoomID từ RoomMeshController đang hoạt động
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

        // Nếu đang không chọn gì nhưng vẫn có room đã chọn trước đó → giữ nguyên
        return lastSelectedRoomID;
    }

    // === Hàm ko cho move trên UI
    private bool IsClickingOnBackgroundBlackUI(Vector2 screenPosition)
    {
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.name == "Background Black")
            {
                Debug.Log("Click UI trên Background Black ➜ Không cho move point");
                return true;
            }
        }

        return false;
    }

    public void CreateRegularPolygonRoom(int sides, float edgeLength)
    {
        if (sides < 3)
        {
            Debug.LogError("Số cạnh phải >= 3");
            return;
        }

        if (edgeLength <= 0)
        {
            Debug.LogError("Chiều dài cạnh phải > 0");
            return;
        }

        // Tìm center trên mặt phẳng y=0 theo camera
        Camera cam = drawingCamera != null ? drawingCamera : Camera.main;
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float enter = 0;
        Vector3 center = Vector3.zero;

        if (groundPlane.Raycast(ray, out enter))
        {
            center = ray.GetPoint(enter);
        }
        else
        {
            Debug.LogError("Không raycast được xuống mặt phẳng y=0");
            return;
        }

        Debug.Log($"Tâm room tại: {center}");

        // Tính bán kính từ chiều dài cạnh
        float radius = edgeLength / (2 * Mathf.Sin(Mathf.PI / sides));
        Debug.Log($"Bán kính: {radius}");

        // Tạo các checkpoint prefab
        float angleOffset = Mathf.PI / 2; // Quay để cạnh đầu hướng lên
        for (int i = 0; i < sides; i++)
        {
            float angle = 2 * Mathf.PI * i / sides + angleOffset;
            float x = center.x + radius * Mathf.Cos(angle);
            float z = center.z + radius * Mathf.Sin(angle);
            Vector3 pos = new Vector3(x, 0, z);

            var cp = Instantiate(checkpointPrefab, pos, Quaternion.identity);
            currentCheckpoints.Add(cp);
        }

        // Tạo wallLines & vẽ line
        for (int i = 0; i < currentCheckpoints.Count; i++)
        {
            Vector3 p1 = currentCheckpoints[i].transform.position;
            Vector3 p2 = (i == currentCheckpoints.Count - 1)
                ? currentCheckpoints[0].transform.position
                : currentCheckpoints[i + 1].transform.position;

            DrawingTool.DrawLineAndDistance(p1, p2);
            wallLines.Add(new WallLine(p1, p2, LineType.Wall));
        }

        // Tạo Room & lưu
        Room newRoom = new Room();
        foreach (GameObject cp in currentCheckpoints)
        {
            Vector3 pos = cp.transform.position;
            pos.y = 0f;
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
        GameObject floorGO = new GameObject($"RoomFloor_{newRoom.ID}");
        RoomMeshController meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(newRoom.ID);

        // Ánh xạ loop
        List<GameObject> loopRef = new List<GameObject>(currentCheckpoints);
        allCheckpoints.Add(loopRef);
        loopMappings.Add(new LoopMap(newRoom.ID, loopRef));

        currentCheckpoints.Clear();
        wallLines.Clear();

        Debug.Log($"Đã tạo Room tự động: {sides} cạnh, cạnh dài ~{edgeLength}m, RoomID: {newRoom.ID}");
    }

    public void CreateRectangleRoom(float width, float height)
    {
        if (width <= 0 || height <= 0)
        {
            Debug.LogError("Chiều dài và chiều rộng phải > 0");
            return;
        }

        // Xoá dữ liệu tạm nếu đang vẽ dở

        // Tìm center trên mặt phẳng y=0 theo camera
        Camera cam = drawingCamera != null ? drawingCamera : Camera.main;
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float enter = 0;
        Vector3 center = Vector3.zero;

        if (groundPlane.Raycast(ray, out enter))
        {
            center = ray.GetPoint(enter);
        }
        else
        {
            Debug.LogError("Không raycast được xuống mặt phẳng y=0");
            return;
        }

        Debug.Log($"Tâm room (rectangle) tại: {center}");

        // Tính 4 đỉnh hình chữ nhật quanh center
        CreateRectangleRoom(width, height, center,null,true);
    }

    public void CreateRectangleRoom(float width, float height, Vector3 center,string ID,bool isCreateCommand)
    {
        DeleteCurrentDrawingData();

        Vector3 p1 = new Vector3(center.x - width / 2, 0, center.z - height / 2);
        Vector3 p2 = new Vector3(center.x - width / 2, 0, center.z + height / 2);
        Vector3 p3 = new Vector3(center.x + width / 2, 0, center.z + height / 2);
        Vector3 p4 = new Vector3(center.x + width / 2, 0, center.z - height / 2);

        List<Vector3> corners = new List<Vector3> { p1, p2, p3, p4 };

        // Tạo checkpoint prefab tại từng góc
        CreateCheckPointGameObject(corners);

        // Tạo wallLines & vẽ line
        for (int i = 0; i < currentCheckpoints.Count; i++)
        {
            Vector3 start = currentCheckpoints[i].transform.position;
            Vector3 end = (i == currentCheckpoints.Count - 1)
                ? currentCheckpoints[0].transform.position
                : currentCheckpoints[i + 1].transform.position;

            DrawingTool.DrawLineAndDistance(start, end);
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
        CreateRoomMeshCtrl(newRoom,center);

        // Ánh xạ loop
        AddGameObjectCheckPointToGlobalVariable(newRoom.ID, currentCheckpoints);

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
        UndoRedoController.Instance.AddToUndo(new CreateRectangularCommand(data));
    }

    public void CreateRoomByRoomData(Room room,Vector3 position)
    {
        // chuyển đổi list sang vector3
        Debug.Log("Create Room by room data: "+room.ID);
        var convertList = new List<Vector3>();
        foreach(var item in room.checkpoints)
        {
            Vector3 pos = new Vector3(item.x, 0, item.y);
            convertList.Add(pos);
        }
        // tạo check dạng game object
        CreateCheckPointGameObject(convertList);
    
        // vẽ line dữa theo wall line
        foreach (WallLine item in room.wallLines)
        {
            Debug.Log($"Start {item.start} End{item.end}");
            DrawingTool.DrawLineAndDistance(item.start, item.end);
        }
        // đảm bảo data trong command độc lập với data runtime
        RoomStorage.rooms.Add(new Room(room));

        // thêm list game object check point vào global data
        AddGameObjectCheckPointToGlobalVariable(room.ID, currentCheckpoints);
        // init floor mesh 
        CreateRoomMeshCtrl(room,position);
        
        
        currentCheckpoints.Clear();
        wallLines.Clear();
        
        DrawingTool.DrawAllLinesFromRoomStorage();
    }

    private void CreateCheckPointGameObject(List<Vector3> corners)
    {
        foreach (Vector3 pos in corners)
        {
            var cp = Instantiate(checkpointPrefab, pos, Quaternion.identity);
            currentCheckpoints.Add(cp);
        }
    }

    private void AddGameObjectCheckPointToGlobalVariable(string roomID,List<GameObject> checkPoints)
    {
        List<GameObject> loopRef = new List<GameObject>(checkPoints);
        allCheckpoints.Add(loopRef);
        loopMappings.Add(new LoopMap(roomID, loopRef));
    }

    private void CreateRoomMeshCtrl(Room newRoom,Vector3 position)
    {
        GameObject floorGO = new GameObject($"RoomFloor_{newRoom.ID}");
        RoomMeshController meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(newRoom.ID);
        floorGO.transform.position = position;
    }

}