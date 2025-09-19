using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InputCustomers : MonoBehaviour
{
    [Header("Refs")]
    private RoomInfoDisplay roomInfoDisplay;
    private CheckpointManager checkpointManager;
    private DragFromButtonSpawnFloor spawnFloor;
    TMP_InputField inputLength;
    TMP_InputField inputHeight;
    TMP_InputField inputWidth;
    TMP_InputField thicknessInput;
    //[SerializeField] private Button buttonOk;

    [SerializeField] FloorSettingPanel floorSettingPanel;

    [Header("Rebuild Settings")]
    [Tooltip("Độ nhô của line/marker so với nền phòng khi rebuild (fallback nếu không tìm thấy CreateRoomOnFloor).")]
    [SerializeField] private float fallbackRoomWallLift = 0.003f;

    private void Awake()
    {
        if (!roomInfoDisplay) roomInfoDisplay = FindFirstObjectByType<RoomInfoDisplay>();
        if (!checkpointManager) checkpointManager = FindFirstObjectByType<CheckpointManager>();
        if (!spawnFloor) spawnFloor = FindFirstObjectByType<DragFromButtonSpawnFloor>();
        //if (buttonOk) buttonOk.onClick.AddListener(ApplyDimensionsForSelectedRoom);
        //if (buttonOk) buttonOk.onClick.AddListener(ApplyDimensionsForSelectedFloor);

        floorSettingPanel.OnApplyAction += ApplyDimensionsForSelectedRoom;
        floorSettingPanel.OnApplyAction += ApplyDimensionsForSelectedFloor;

    }
    private void Start()
    {
        Invoke(nameof(Find), 0.5f);
    }

    private void Find()
    {
        inputLength = floorSettingPanel.GetParameterInputField(IntParameterType.Height).InputField;
        inputHeight = floorSettingPanel.GetParameterInputField(IntParameterType.DistanceFromGround).InputField;
        inputWidth = floorSettingPanel.GetParameterInputField(IntParameterType.Width).InputField;
        thicknessInput = floorSettingPanel.GetParameterInputField(IntParameterType.Thickness).InputField;
    }

    private void OnDestroy()
    {
        //if (buttonOk) buttonOk.onClick.RemoveListener(ApplyDimensionsForSelectedRoom);
        //if (buttonOk) buttonOk.onClick.RemoveListener(ApplyDimensionsForSelectedFloor);
    }

    public void LoadDataWhenShow()
    {
        if (!roomInfoDisplay.TryGetSelection(out RoomInfoDisplay.SelType kind, out string roomId))
        {
            Debug.LogWarning("[DimOK] Không có mục nào đang được chọn -> không áp dụng.");
            return;
        }

        if (kind != RoomInfoDisplay.SelType.Room || string.IsNullOrEmpty(roomId))
        {
            Debug.LogWarning($"[DimOK] Đang chọn {kind}, không phải ROOM hoặc ID rỗng -> không áp dụng.");
            return;
        }
        var room = RoomStorage.GetRoomByID(roomId);
        thicknessInput.text = room.thickness.ToString();

        UpdateInputWhenShow(room.center,room.checkpoints);
    }

    private void UpdateInputWhenShow(Vector3 center, List<Vector2> checkPoints)
    {
        Bounds bounds = new();
        bounds.center = center;
        foreach (var item in checkPoints)
        {
            bounds.Encapsulate(item);
        }

        inputWidth.text = bounds.size.x.ToString();
        inputLength.text = bounds.size.y.ToString();
        inputHeight.text = "0.1";
    }

    // ROOM đang chọn
    private void ApplyDimensionsForSelectedRoom()
    {
        if (checkpointManager == null)
        {
            Debug.LogWarning("[DimOK] checkpointManager NULL");
            return;
        }

        if (roomInfoDisplay == null)
        {
            Debug.LogWarning("[DimOK] roomInfoDisplay NULL");
            return;
        }

        // Chỉ dùng TryGetSelection để lấy ID phòng
        if (!roomInfoDisplay.TryGetSelection(out RoomInfoDisplay.SelType kind, out string roomId))
        {
            Debug.LogWarning("[DimOK] Không có mục nào đang được chọn -> không áp dụng.");
            return;
        }

        if (kind != RoomInfoDisplay.SelType.Room || string.IsNullOrEmpty(roomId))
        {
            Debug.LogWarning($"[DimOK] Đang chọn {kind}, không phải ROOM hoặc ID rỗng -> không áp dụng.");
            return;
        }

        Debug.Log($"[DimOK] Editing ROOM ID = {roomId}");
        RecreateRoomWithInputDims(roomId);
    }
    private (float, float, float, bool) GetWidthLengthFromInput()
    {
        if (inputLength == null || inputWidth == null || inputHeight == null)
        {
            Debug.LogWarning("[DimOK] Không tìm thấy input Length/Width trong FloorSettingPanel.");
            return (0, 0, 0, false);
        }
        if (!TryParse(inputLength?.text, out float L) || !TryParse(inputWidth?.text, out float W)|| !TryParse(inputHeight?.text, out float H))
        {
            Debug.LogWarning("[DimOK] Cần nhập đủ Chiều dài & Chiều rộng cho FLOOR.");
            return (0, 0, 0, false);
        }
        return (W, L, H, true);
    }
    private Vector2 GetWidthAndHeight(Room room)
    {
        Bounds bounds = new();
        bounds.center = room.center;
        foreach (var item in room.checkpoints)
        {
            Debug.Log("Encapsulate");
            bounds.Encapsulate(item);
        }
        return bounds.size;
    }
    // Update dims cho ROOM
    private void RecreateRoomWithInputDims(string roomId)
    {
        var (W, L, H, ok) = GetWidthLengthFromInput();
        if (!ok) return;

        // Lấy room
        Room room = RoomStorage.GetRoomByID(roomId);
        var size = GetWidthAndHeight(room);
        Debug.Log($"Size from input {W} {L} : Size from room{size}");
        bool heightUnchanged = room.heights != null && room.heights.Count > 0 && Mathf.Approximately(room.heights[0], H);
        if (size.x == W && size.y == L && heightUnchanged)
        {
            Debug.Log("This is same, does not create it againt");
            return;
        }

        UndoRedoController.Instance.AddToUndo(new EditRoomCommand(new Room(room)));
        if (room == null)
        {
            Debug.LogWarning($"[DimOK] Không tìm thấy ROOM với ID={roomId}");
            return;
        }
        float thickness = thicknessInput != null && TryParse(thicknessInput.text, out float t) ? t : room.thickness;
        room.thickness = thickness;
        // Tính centroid hiện có
        Vector2 centroid = ComputeCentroid2D(room.checkpoints);
        room.center = centroid;
        // Force index = 2
        var cr = FindFirstObjectByType<CreateRoomOnFloor>();
        float layerStep = (cr != null) ? cr.layerStepY : 0.002f; // fallback 2mm
        float baseY = 2f * layerStep;                        // index = 2
        float roomWallLift = (cr != null) ? cr.roomWallLift : fallbackRoomWallLift;

        // Ghi lại polygon LxW quanh centroid (GIỮ THỨ TỰ NHƯ KHI TẠO)
        float hx = W * 0.5f, hy = L * 0.5f;
        var rect = new List<Vector2>(4)
        {
            new Vector2(centroid.x - hx, centroid.y - hy), // v0 
            new Vector2(centroid.x - hx, centroid.y + hy), // v1 
            new Vector2(centroid.x + hx, centroid.y + hy), // v2 
            new Vector2(centroid.x + hx, centroid.y - hy)  // v3 
        };
        room.checkpoints = rect;

        // Rebuild wallLines 4 cạnh ở cao độ baseY + roomWallLift
        room.extraCheckpoints?.Clear();
        if (room.wallLines == null) room.wallLines = new List<WallLine>(); else room.wallLines.Clear();

        Vector3 v0 = new Vector3(rect[0].x, baseY + roomWallLift, rect[0].y);
        Vector3 v1 = new Vector3(rect[1].x, baseY + roomWallLift, rect[1].y);
        Vector3 v2 = new Vector3(rect[2].x, baseY + roomWallLift, rect[2].y);
        Vector3 v3 = new Vector3(rect[3].x, baseY + roomWallLift, rect[3].y);

        room.wallLines.Add(new WallLine(v0, v1, LineType.Wall));
        room.wallLines.Add(new WallLine(v1, v2, LineType.Wall));
        room.wallLines.Add(new WallLine(v2, v3, LineType.Wall));
        room.wallLines.Add(new WallLine(v3, v0, LineType.Wall));

        // Sync storage
        RoomStorage.UpdateOrAddRoom(room);

        // Cập nhật / tạo mesh GO (RoomFloor_<id>) theo baseY mới
        GameObject floorGO = null;
        if (checkpointManager.RoomFloorMap != null &&
            checkpointManager.RoomFloorMap.TryGetValue(roomId, out var existGO) &&
            existGO != null)
        {
            floorGO = existGO;
        }
        else
        {
            floorGO = new GameObject($"RoomFloor_{room.ID}");
            checkpointManager.RoomFloorMap ??= new Dictionary<string, GameObject>();
            checkpointManager.RoomFloorMap[room.ID] = floorGO;
        }

        floorGO.transform.position = new Vector3(0f, baseY, 0f);
        var meshCtrl = floorGO.GetComponent<RoomMeshController>();
        if (meshCtrl == null) meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(room.ID);
        meshCtrl.GenerateMesh(room.checkpoints);

        // Di chuyển checkpoint GOs của room về 4 đỉnh mới (nếu tìm được mapping)
        var mappedList = TryGetCheckpointListForRoom(roomId);
        if (mappedList != null)
        {
            var newVerts = new Vector3[] { v0, v1, v2, v3 };
            int n = Mathf.Min(mappedList.Count, 4);
            for (int i = 0; i < n; i++)
                if (mappedList[i]) mappedList[i].transform.position = newVerts[i];

            for (int i = n; i < 4; i++)
            {
                if (checkpointManager.checkpointPrefab != null)
                {
                    var go = Instantiate(checkpointManager.checkpointPrefab, newVerts[i], Quaternion.identity);
                    mappedList.Add(go);
                }
            }
            for (int i = mappedList.Count - 1; i >= 4; i--)
            {
                if (mappedList[i]) Destroy(mappedList[i]);
                mappedList.RemoveAt(i);
            }
        }



        // Redraw để line được vẽ lại theo wallLines mới
        checkpointManager.RedrawAllRooms();
        FurnitureManager.Instance.CheckWallLineValidInRoom();
        FurnitureManager.Instance.TrySnapToNearestWall();
        Debug.Log($"[DimOK] UPDATED room {roomId}: points+lines+mesh (index=2) -> {L}x{W}, baseY={baseY}, lift={roomWallLift}");
        floorSettingPanel.ResetAllParameters();

        // === HEIGHT update ===
        room.heights.Clear(); // xóa cũ để tránh dư
        for (int i = 0; i < room.wallLines.Count; i++)
        {
            // đồng bộ list heights
            room.heights.Add(H);
        }

    }

    private List<GameObject> TryGetCheckpointListForRoom(string id)
    {
        if (checkpointManager == null || checkpointManager.loopMappings == null) return null;

        foreach (var any in (IEnumerable)checkpointManager.loopMappings)
        {
            if (any == null) continue;

            // lấy giá trị ID từ bất kỳ field/property string nào có vẻ là ID
            string lmId = TryReadStringId(any);
            if (lmId != id) continue;

            // lấy field/property kiểu List<GameObject>
            var pts = TryReadPointsList(any);
            if (pts != null) return pts;
        }
        return null;
    }

    // Floor đang chọn
    private void ApplyDimensionsForSelectedFloor()
    {
        // Lấy ID floor 
        if (roomInfoDisplay == null ||
            !roomInfoDisplay.TryGetSelection(out RoomInfoDisplay.SelType kind, out string floorId) ||
            kind != RoomInfoDisplay.SelType.Floor ||
            string.IsNullOrEmpty(floorId))
        {
            Debug.LogWarning("[DimOK] Không có FLOOR đang được chọn.");
            return;
        }

        Debug.Log($"[DimOK] Editing FLOOR ID = {floorId}");
        RecreateFloorWithInputDims(floorId);
    }

    // Update dims cho Floor
    private void RecreateFloorWithInputDims(string floorId)
    {
        var target = FindFloor(floorId);
        if (target == null) return;
        var cloneOfTarget = Floor.Clone(target);
        float prevousWidth = target.width;
        float previousLength = target.length;
        // chú ý: length = chiều dọc (Z), width = chiều ngang (X)
        // Đọc L & W

        var (W, L, H, ok) = GetWidthLengthFromInput();

        if (!ok) return;

        // W và L bị đảo ngươc4
        if (TryUpdateFloor(target, L, W))
        {
            UndoRedoController.Instance.AddToUndo(new EditFloorCommand(cloneOfTarget));
            floorSettingPanel.ResetAllParameters();
        }
    }


    public Floor FindFloor(string floorId)
    {
        Floor target = null;
        if (FloorStorage.floors != null)
        {
            for (int i = 0; i < FloorStorage.floors.Count; i++)
            {
                var f = FloorStorage.floors[i];
                if (f != null && f.ID == floorId) { target = f; break; }
            }
        }
        if (target == null)
        {
            Debug.LogWarning($"[DimOK] Không tìm thấy FLOOR với ID={floorId}");
            return null;
        }
        return target;
    }

    public bool TryUpdateFloor(Floor target, float W, float L)
    {
        // Tìm Floor trong FloorStorage
        string floorId = target.ID;

        // Tính centroid hiện có
        Vector2 centroid = ComputeCentroid2D(target.checkpoints);
        // Input của hàm bị ngược lúc truyền vào S
        target.width = L;
        target.length = W;
        // Ghi lại polygon L×W (axis-aligned theo world) vào storage
        float hx = L * 0.5f, hy = W * 0.5f;
        target.checkpoints = new List<Vector2>(4)
        {
            new Vector2(centroid.x - hx, centroid.y - hy),
            new Vector2(centroid.x + hx, centroid.y - hy),
            new Vector2(centroid.x + hx, centroid.y + hy),
            new Vector2(centroid.x - hx, centroid.y + hy)
        };

        // UPDATE POINTS + LINE bằng công cụ hiện trường
        if (spawnFloor != null)
        {
            spawnFloor.LoadStateFromFloorId(floorId);
        }
        else
        {
            Debug.LogWarning("[DimOK] spawnFloor NULL — không thể vẽ lại points/line. Hãy đảm bảo DragFromButtonSpawnFloor có trong scene.");
        }
        Debug.Log($"[DimOK] UPDATED FLOOR {floorId}: points + line + mesh -> {L}x{W}, area={L * W}");
        CameraResizeByFloor.Instance.Resize(target.center, target.checkpoints);

        return true;
    }

    private static string TryReadStringId(object obj)
    {
        var t = obj.GetType();

        // thử các tên phổ biến trước để nhanh
        string[] candidates = { "roomId", "RoomId", "roomID", "RoomID", "id", "ID" };

        foreach (var name in candidates)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string))
            {
                return f.GetValue(obj) as string;
            }
            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string) && p.CanRead)
            {
                return p.GetValue(obj) as string;
            }
        }

        // quét mọi string field/property
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (f.FieldType == typeof(string))
                return f.GetValue(obj) as string;

        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (p.PropertyType == typeof(string) && p.CanRead)
                return p.GetValue(obj) as string;

        return null;
    }

    private static List<GameObject> TryReadPointsList(object obj)
    {
        var t = obj.GetType();

        // Ưu tiên field/property chính xác List<GameObject>
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.FieldType == typeof(List<GameObject>))
                return f.GetValue(obj) as List<GameObject>;
        }
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (p.CanRead && p.PropertyType == typeof(List<GameObject>))
                return p.GetValue(obj) as List<GameObject>;
        }

        // tìm field generic List<T> mà T kế thừa UnityEngine.Object, thử cast phần tử sang GameObject
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elem = f.FieldType.GetGenericArguments()[0];
                if (typeof(UnityEngine.Object).IsAssignableFrom(elem))
                {
                    var listObj = f.GetValue(obj) as IEnumerable;
                    if (listObj == null) continue;

                    var result = new List<GameObject>();
                    foreach (var item in listObj)
                    {
                        if (item is GameObject go) result.Add(go);
                        else if (item is Component c) result.Add(c.gameObject);
                    }
                    // nếu gom được ít nhất 1 phần tử, coi như hợp lệ
                    if (result.Count > 0) return result;
                }
            }
        }

        return null;
    }

    private static Vector2 ComputeCentroid2D(List<Vector2> poly)
    {
        if (poly == null || poly.Count < 3) return Vector2.zero;

        float A = 0f, cx = 0f, cy = 0f;
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var p = poly[i];
            var q = poly[(i + 1) % n];
            float cr = p.x * q.y - q.x * p.y;
            A += cr;
            cx += (p.x + q.x) * cr;
            cy += (p.y + q.y) * cr;
        }
        A *= 0.5f;
        if (Mathf.Abs(A) > 1e-8f) return new Vector2(cx / (6f * A), cy / (6f * A));

        // Degenerate -> trung bình hình học
        Vector2 c = Vector2.zero;
        for (int i = 0; i < poly.Count; i++) c += poly[i];
        return c / Mathf.Max(1, poly.Count);
    }

    // parse số với ,/.
    private static bool TryParse(string s, out float v)
    {
        v = 0f;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace(',', '.');
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}
