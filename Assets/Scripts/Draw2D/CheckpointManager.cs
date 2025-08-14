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
    public GameObject firstPoint = null;
    
    private SplitRoomManager splitRoomManager;
    private HandleCheckpointManger handleCheckpointManger;

    // Map loop checkpoint list => Room ID
    public List<LoopMap> loopMappings = new List<LoopMap>();
    // Lưu lại tất cả các cửa / cửa sổ để chèn lại sau khi rebuild wallLines

    // [RoomID] → List<(WallLine, GameObject p1, GameObject p2)>
    public Dictionary<string, List<(WallLine line, GameObject p1, GameObject p2)>> tempDoorWindowPoints
        = new Dictionary<string, List<(WallLine, GameObject, GameObject)>>();
    public string lastSelectedRoomID = null;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {        
        // splitRoomManager = FindFirstObjectByType<SplitRoomManager>();
        handleCheckpointManger = FindFirstObjectByType<HandleCheckpointManger>();
        LoadPointsFromRoomStorage();
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

            if (currentLineType == LineType.Wall)
            {
                handleCheckpointManger.HandleSingleWallPlacement(previewPosition);
                // handleCheckpointManger.HandleWallLoopPlacement(previewPosition);
            }
            else
                handleCheckpointManger.HandleCheckpointPlacement(previewPosition);

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
    public bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
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
                if (!wl.isVisible) continue;
                
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

    public void ClearAllLines()=> DrawingTool.ClearAllLines();
    public void DrawAllLinesFromRoomStorage()=> DrawingTool.DrawAllLinesFromRoomStorage();
    public void DrawLineAndDistance(Vector3 start, Vector3 end) => DrawingTool.DrawLineAndDistance(start, end);

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
        CreateRoomMeshCtrl(newRoom, center);

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