using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class RoomInfoDisplay : MonoBehaviour
{
    [Header("Reference")]
    private CheckpointManager checkpointManager;

    [Header("Pick/Raycast")]
    [SerializeField] private LayerMask floorRaycastMask = ~0;

    [Header("Floor Highlight")]
    [SerializeField] private Color floorDefaultColor = Color.white;
    [SerializeField] private Color floorSelectedColor = Color.yellow;

    [Header("World Label")]
    [SerializeField] private TMP_FontAsset labelFont; // optional
    [SerializeField] private float labelFontSize = 5f;
    [SerializeField] private Color labelColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color labelOutlineColor = Color.white;
    [SerializeField] private float labelYLift = 0.02f;
    [SerializeField] private bool billboardToCamera = true;

    [Header("Selection Rules")]
    [SerializeField] private bool allowFloorPickWhenNoRoomSelected = false;

    [Header("Popup UI (Screen-Space)")]
    [SerializeField] private GameObject ActionSpace;        // RectTransform trong Canvas (Screen Space)
    [SerializeField] private GameObject PopupRoom;          // Prefab panel (không nhất thiết có Canvas)
    [SerializeField] private Vector2 popupScreenOffset = new Vector2(0, 40); // px

    [Header("Popup World Fallback")]
    [SerializeField] private float popupY = 0.2f;
    [SerializeField] private float popupX = 0.2f;
    [SerializeField] private float popupZ = 0.2f;

    // ===== State =====
    private enum SelectionKind { None, Room, Floor }
    private SelectionKind selectionKind = SelectionKind.None;
    private string selectedRoomID = "";
    private string selectedFloorID = "";
    private string highlightedID = "";

    private bool forceSelectFirstRoom = false;
    private bool suppressAutoPick = false;
    private int lastRoomsCount = 0;

    // Labels
    private readonly Dictionary<string, TextMeshPro> roomLabels = new();
    private readonly Dictionary<string, TextMeshPro> floorLabels = new();

    // Popup (UI) + Canvas refs
    private GameObject popupUI;            // instance của PopupRoom (child của ActionSpace)
    private RectTransform popupRect;       // rect của popupUI
    private RectTransform actionSpaceRect; // rect của container
    private Canvas uiCanvas;               // canvas chứa ActionSpace

    // Fallback world-space (nếu không có ActionSpace)
    private GameObject popupWS;

    void Start()
    {
        checkpointManager = FindFirstObjectByType<CheckpointManager>();
        lastRoomsCount = RoomStorage.rooms.Count;

        if (ActionSpace)
        {
            actionSpaceRect = ActionSpace.GetComponent<RectTransform>();
            uiCanvas = actionSpaceRect ? actionSpaceRect.GetComponentInParent<Canvas>() : null;
        }
    }

    void Update()
    {
        // Click chọn
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (TryPickRoomUnderPointer(out var roomId))
            {
                SelectRoom(roomId);
                return;
            }

            bool canPickFloorNow = (selectionKind != SelectionKind.None) || allowFloorPickWhenNoRoomSelected;
            if (canPickFloorNow && TryPickFloorUnderPointer(out var floorId, out var _))
            {
                SelectFloor(floorId);
                return;
            }

            DeselectAll();
            return;
        }

        // Xoá room => dọn dẹp
        int curCount = RoomStorage.rooms.Count;
        if (curCount < lastRoomsCount)
        {
            if (!string.IsNullOrEmpty(highlightedID))
            {
                SetFloorColor(highlightedID, floorDefaultColor);
                highlightedID = "";
            }
            ResetAfterDelete();
            CleanupOrphanRoomLabels();
            CleanupOrphanFloorLabels();
        }
        lastRoomsCount = curCount;

        if (RoomStorage.rooms.Count == 0)
        {
            if (!string.IsNullOrEmpty(highlightedID))
            {
                SetFloorColor(highlightedID, floorDefaultColor);
                highlightedID = "";
            }
            selectedRoomID = "";
            selectedFloorID = "";
            selectionKind = SelectionKind.None;

            suppressAutoPick = false;
            forceSelectFirstRoom = false;

            HideAllLabels();
            if (popupUI) popupUI.SetActive(false);
            if (popupWS) popupWS.SetActive(false);
            return;
        }

        // Cập nhật label realtime
        if (selectionKind != SelectionKind.Floor)
        {
            string currentRoomID = checkpointManager.GetSelectedRoomID();
            if (!string.IsNullOrEmpty(currentRoomID) && currentRoomID != selectedRoomID)
                SelectRoom(currentRoomID);

            if (selectionKind == SelectionKind.None &&
                forceSelectFirstRoom && RoomStorage.rooms.Count > 0 && string.IsNullOrEmpty(selectedRoomID))
            {
                SelectRoom(RoomStorage.rooms[0].ID);
                forceSelectFirstRoom = false;
                return;
            }

            if (selectionKind == SelectionKind.None &&
                !suppressAutoPick &&
                string.IsNullOrEmpty(currentRoomID) &&
                string.IsNullOrEmpty(selectedRoomID) &&
                RoomStorage.rooms.Count > 0)
            {
                SelectRoom(RoomStorage.rooms[0].ID);
            }

            if (!string.IsNullOrEmpty(selectedRoomID) && RoomStorage.GetRoomByID(selectedRoomID) == null)
            {
                if (!string.IsNullOrEmpty(highlightedID))
                {
                    SetFloorColor(highlightedID, floorDefaultColor);
                    highlightedID = "";
                }
                HideRoomLabel(selectedRoomID);

                selectedRoomID = "";
                selectionKind = SelectionKind.None;
                if (popupUI) popupUI.SetActive(false);
                if (popupWS) popupWS.SetActive(false);
                return;
            }

            if (selectionKind == SelectionKind.Room && !string.IsNullOrEmpty(selectedRoomID))
            {
                var room = RoomStorage.GetRoomByID(selectedRoomID);
                if (room != null) UpdateRoomLabel(room);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(selectedFloorID))
            {
                var f = FindFloorByID(selectedFloorID);
                if (f != null) UpdateFloorLabel(f);
            }
        }
    }

    void LateUpdate()
    {
        // Lấy ID đang chọn
        string curId = (selectionKind == SelectionKind.Room) ? selectedRoomID :
                       (selectionKind == SelectionKind.Floor) ? selectedFloorID : "";
        if (string.IsNullOrEmpty(curId)) return;

        // Lấy checkpoints hiện tại
        List<Vector2> cps = null;
        if (selectionKind == SelectionKind.Room)
        {
            var room = RoomStorage.GetRoomByID(curId);
            if (room != null) cps = room.checkpoints;
        }
        else
        {
            var floor = FindFloorByID(curId);
            if (floor != null) cps = floor.checkpoints;
        }
        if (cps == null || cps.Count < 3) return;

        // Tính anchor world: cùng X/Z với label, Y = max(centroidY, top of mesh) + lift
        var targetGO = GetFloorGO(curId);
        float baseY = targetGO ? targetGO.transform.position.y : 0f;
        Vector2 c2 = PolygonCentroid(cps);

        // ===== Offset theo camera: X (right) và Z (forward), đều tính theo mét =====
        // ===== Offset theo camera: X (right) và Z (forward), đều tính theo mét =====
        Vector3 lateral = Vector3.zero; // X (right)
        Vector3 depth = Vector3.zero; // Z (forward)

        var cam = Camera.main;
        if (cam)
        {
            // Right (X)
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            if (camRight.sqrMagnitude < 1e-6f) camRight = Vector3.right; // fallback an toàn
            camRight.Normalize();
            lateral = camRight * popupX;

            // Forward (Z) phẳng theo XZ. Nếu top-down (≈0) thì dùng up × right để tạo fwd nằm trên mặt phẳng.
            Vector3 camFwd = cam.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 1e-6f)
                camFwd = Vector3.Cross(Vector3.up, camRight); // quay 90° quanh Y từ right
            camFwd.Normalize();
            depth = camFwd * popupZ;
        }

        // Vị trí gốc theo centroid + offset XZ từ camera
        Vector3 worldPos = new Vector3(
            c2.x + lateral.x + depth.x,
            baseY + labelYLift,
            c2.y + lateral.z + depth.z
        );

        // Y: đỉnh mesh + popupY
        if (targetGO)
        {
            var rs = targetGO.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                worldPos.y = Mathf.Max(worldPos.y, b.max.y) + popupY;
            }
            else worldPos.y += popupY;
        }
        else worldPos.y += popupY;


        // ==== ƯU TIÊN: đặt popup trong ActionSpace (UI Screen Space) ====
        if (actionSpaceRect && uiCanvas && popupUI && popupUI.activeSelf)
        {
            Camera camWS;    // camera để WorldToScreen
            Camera camLocal; // camera cho ScreenPointToLocalPointInRectangle
            if (uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                camWS = Camera.main; // dùng camera nhìn scene
                camLocal = null;       // overlay => null
            }
            else // ScreenSpaceCamera
            {
                camWS = uiCanvas.worldCamera ? uiCanvas.worldCamera : Camera.main;
                camLocal = camWS;
            }
            if (!camWS) return;

            // Ẩn nếu điểm ở sau camera (tránh warning)
            var view = camWS.WorldToViewportPoint(worldPos);
            if (view.z <= 0f) { popupUI.SetActive(false); return; }

            Vector3 screen = camWS.WorldToScreenPoint(worldPos) + (Vector3)popupScreenOffset;

            // clamp trong màn hình
            screen.x = Mathf.Clamp(screen.x, 0, Screen.width);
            screen.y = Mathf.Clamp(screen.y, 0, Screen.height);

            if (!popupRect) popupRect = popupUI.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(actionSpaceRect, screen, camLocal, out var local))
            {
                popupRect.anchoredPosition = local;
                if (!popupUI.activeSelf) popupUI.SetActive(true);
            }
            return;
        }

        // ==== Fallback: world-space popup (khi không set ActionSpace) ====
        if (popupWS && popupWS.activeSelf)
        {
            popupWS.transform.position = worldPos;
            popupWS.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    // ===================== SELECTION =====================
    private void SelectRoom(string roomId)
    {
        if (selectionKind == SelectionKind.Floor && !string.IsNullOrEmpty(selectedFloorID))
        {
            HideFloorLabel(selectedFloorID);
            selectedFloorID = "";
        }
        if (!string.IsNullOrEmpty(highlightedID) && highlightedID != roomId)
            SetFloorColor(highlightedID, floorDefaultColor);

        selectedRoomID = roomId;
        selectionKind = SelectionKind.Room;
        highlightedID = roomId;
        SetFloorColor(highlightedID, floorSelectedColor);

        var room = RoomStorage.GetRoomByID(selectedRoomID);
        ShowOnlyRoomLabel(room);

        // Spawn/attach popup: ưu tiên UI container
        var targetGO = GetFloorGO(roomId);
        SpawnOrAttachPopup(targetGO);

        forceSelectFirstRoom = false;
        suppressAutoPick = false;
    }

    private void SelectFloor(string floorId)
    {
        if (selectionKind == SelectionKind.Room && !string.IsNullOrEmpty(selectedRoomID))
        {
            HideRoomLabel(selectedRoomID);
            selectedRoomID = "";
        }
        if (!string.IsNullOrEmpty(highlightedID) && highlightedID != floorId)
            SetFloorColor(highlightedID, floorDefaultColor);

        selectedFloorID = floorId;
        selectionKind = SelectionKind.Floor;
        highlightedID = floorId;
        SetFloorColor(highlightedID, floorSelectedColor);

        var floor = FindFloorByID(selectedFloorID);
        ShowOnlyFloorLabel(floor);

        var targetGO = GetFloorGO(floorId);
        SpawnOrAttachPopup(targetGO);

        forceSelectFirstRoom = false;
        suppressAutoPick = true;
    }

    private void DeselectAll()
    {
        if (!string.IsNullOrEmpty(highlightedID))
        {
            SetFloorColor(highlightedID, floorDefaultColor);
            highlightedID = "";
        }
        selectedRoomID = "";
        selectedFloorID = "";
        selectionKind = SelectionKind.None;

        HideAllLabels();
        if (popupUI) popupUI.SetActive(false);
        if (popupWS) popupWS.SetActive(false);
    }

    public void ResetState()
    {
        if (!string.IsNullOrEmpty(highlightedID))
        {
            SetFloorColor(highlightedID, floorDefaultColor);
            highlightedID = "";
        }
        selectedRoomID = "";
        selectedFloorID = "";
        selectionKind = SelectionKind.None;

        forceSelectFirstRoom = true;
        suppressAutoPick = false;
        HideAllLabels();
        if (popupUI) popupUI.SetActive(false);
        if (popupWS) popupWS.SetActive(false);
    }

    public void ResetAfterDelete()
    {
        if (!string.IsNullOrEmpty(highlightedID))
        {
            SetFloorColor(highlightedID, floorDefaultColor);
            highlightedID = "";
        }
        selectedRoomID = "";
        selectedFloorID = "";
        selectionKind = SelectionKind.None;

        forceSelectFirstRoom = false;
        suppressAutoPick = true;
        HideAllLabels();
        if (popupUI) popupUI.SetActive(false);
        if (popupWS) popupWS.SetActive(false);
    }

    // ===================== LABELS =====================
    private void ShowOnlyRoomLabel(Room room)
    {
        HideAllLabels();
        if (room == null) return;
        var label = EnsureRoomLabel(room.ID);
        if (label != null) { label.gameObject.SetActive(true); UpdateRoomLabel(room); }
    }

    private TextMeshPro EnsureRoomLabel(string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return null;
        if (roomLabels.TryGetValue(roomId, out var tmp) && tmp != null) return tmp;

        var floorGO = GetFloorGO(roomId);
        if (!floorGO) return null;

        var go = new GameObject($"RoomLabel_{roomId}");
        go.transform.SetParent(floorGO.transform, true);

        var text = go.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false;
        text.fontSize = labelFontSize;
        text.color = labelColor;
        text.outlineWidth = 0.15f;
        text.outlineColor = labelOutlineColor;
        if (labelFont) text.font = labelFont;

        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        roomLabels[roomId] = text;
        return text;
    }

    private void UpdateRoomLabel(Room room)
    {
        if (room == null) return;
        if (!roomLabels.TryGetValue(room.ID, out var tmp) || tmp == null)
        {
            tmp = EnsureRoomLabel(room.ID);
            if (tmp == null) return;
        }
        var cps = room.checkpoints;
        if (cps == null || cps.Count < 3) { tmp.text = ""; return; }

        float area = Mathf.Abs(SignedArea(cps)) * 0.5f;
        Vector2 centroid2 = PolygonCentroid(cps);
        var floorGO = GetFloorGO(room.ID);
        float baseY = floorGO ? floorGO.transform.position.y : 0f;

        tmp.transform.position = new Vector3(centroid2.x, baseY + labelYLift, centroid2.y);
        string displayName = !string.IsNullOrEmpty(room.roomName) ? room.roomName : $"Room {room.ID}";
        tmp.text = $"{displayName}\n{area:F2} m²";

        if (billboardToCamera && Camera.main)
        {
            var camFwd = Camera.main.transform.forward; camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 1e-6f)
            {
                var yaw = Quaternion.LookRotation(camFwd.normalized, Vector3.up);
                tmp.transform.rotation = yaw * Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }

    private void HideRoomLabel(string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;
        if (roomLabels.TryGetValue(roomId, out var tmp) && tmp != null)
            tmp.gameObject.SetActive(false);
    }

    private void ShowOnlyFloorLabel(Floor floor)
    {
        HideAllLabels();
        if (floor == null) return;
        var label = EnsureFloorLabel(floor.ID);
        if (label != null) { label.gameObject.SetActive(true); UpdateFloorLabel(floor); }
    }

    private TextMeshPro EnsureFloorLabel(string floorId)
    {
        if (string.IsNullOrEmpty(floorId)) return null;
        if (floorLabels.TryGetValue(floorId, out var tmp) && tmp != null) return tmp;

        var floorGO = GetFloorGO(floorId);
        if (!floorGO) return null;

        var go = new GameObject($"FloorLabel_{floorId}");
        go.transform.SetParent(floorGO.transform, true);

        var text = go.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false;
        text.fontSize = labelFontSize;
        text.color = labelColor;
        text.outlineWidth = 0.15f;
        text.outlineColor = labelOutlineColor;
        if (labelFont) text.font = labelFont;

        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        floorLabels[floorId] = text;
        return text;
    }

    private void UpdateFloorLabel(Floor floor)
    {
        if (floor == null) return;
        if (!floorLabels.TryGetValue(floor.ID, out var tmp) || tmp == null)
        {
            tmp = EnsureFloorLabel(floor.ID);
            if (tmp == null) return;
        }
        var cps = floor.checkpoints;
        if (cps == null || cps.Count < 3) { tmp.text = ""; return; }

        float area = Mathf.Abs(SignedArea(cps)) * 0.5f;
        Vector2 centroid2 = PolygonCentroid(cps);
        var floorGO = GetFloorGO(floor.ID);
        float baseY = floorGO ? floorGO.transform.position.y : 0f;

        tmp.transform.position = new Vector3(centroid2.x, baseY + labelYLift, centroid2.y);
        tmp.text = $"Floor {floor.ID}\n{area:F2} m²";

        if (billboardToCamera && Camera.main)
        {
            var camFwd = Camera.main.transform.forward; camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 1e-6f)
            {
                var yaw = Quaternion.LookRotation(camFwd.normalized, Vector3.up);
                tmp.transform.rotation = yaw * Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }

    private void HideFloorLabel(string floorId)
    {
        if (string.IsNullOrEmpty(floorId)) return;
        if (floorLabels.TryGetValue(floorId, out var tmp) && tmp != null)
            tmp.gameObject.SetActive(false);
    }

    private void HideAllLabels()
    {
        foreach (var kv in roomLabels) if (kv.Value) kv.Value.gameObject.SetActive(false);
        foreach (var kv in floorLabels) if (kv.Value) kv.Value.gameObject.SetActive(false);
    }

    private void CleanupOrphanRoomLabels()
    {
        var toRemove = new List<string>();
        foreach (var kv in roomLabels)
        {
            if (RoomStorage.GetRoomByID(kv.Key) == null)
            {
                if (kv.Value) Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var id in toRemove) roomLabels.Remove(id);
    }

    private void CleanupOrphanFloorLabels()
    {
        var alive = new HashSet<string>();
        if (FloorStorage.floors != null)
        {
            for (int i = 0; i < FloorStorage.floors.Count; i++)
            {
                var f = FloorStorage.floors[i];
                if (f != null) alive.Add(f.ID);
            }
        }
        var toRemove = new List<string>();
        foreach (var kv in floorLabels)
        {
            if (!alive.Contains(kv.Key))
            {
                if (kv.Value) Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var id in toRemove) floorLabels.Remove(id);
    }

    // ===================== PICK =====================
    private bool IsPointerOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    private GameObject GetFloorGO(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (checkpointManager != null &&
            checkpointManager.RoomFloorMap != null &&
            checkpointManager.RoomFloorMap.TryGetValue(id, out var go) && go)
            return go;

        var byNew = GameObject.Find($"Floor_{id}");
        if (byNew) return byNew;

        var byOld = GameObject.Find($"RoomFloor_{id}");
        return byOld;
    }

    private static bool TryExtractIdFromFloorName(string name, out string id)
    {
        id = "";
        if (string.IsNullOrEmpty(name)) return false;
        int i = name.IndexOf('_');
        if (i >= 0 && i + 1 < name.Length)
        {
            id = name.Substring(i + 1);
            return !string.IsNullOrEmpty(id);
        }
        return false;
    }

    private bool TryPickFloorUnderPointer(out string id, out GameObject floorGO)
    {
        id = "";
        floorGO = null;

        var cam = Camera.main; if (!cam) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 10000f, floorRaycastMask))
            return false;

        Transform t = hit.collider ? hit.collider.transform : null;
        while (t != null && !t.CompareTag("RoomFloor")) t = t.parent;
        if (t == null) return false;

        floorGO = t.gameObject;

        if (TryExtractIdFromFloorName(floorGO.name, out var byName))
        {
            id = byName;
            return true;
        }

        if (checkpointManager != null && checkpointManager.RoomFloorMap != null)
        {
            foreach (var kv in checkpointManager.RoomFloorMap)
                if (kv.Value == floorGO) { id = kv.Key; return true; }
        }
        return false;
    }

    private Floor FindFloorByID(string id)
    {
        if (string.IsNullOrEmpty(id) || FloorStorage.floors == null) return null;
        for (int i = 0; i < FloorStorage.floors.Count; i++)
        {
            var f = FloorStorage.floors[i];
            if (f != null && f.ID == id) return f;
        }
        return null;
    }

    // ===================== GEOMETRY =====================
    private static float SignedArea(List<Vector2> pts)
    {
        float a = 0f;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            a += p.x * q.y - q.x * p.y;
        }
        return 0.5f * a;
    }

    private static Vector2 PolygonCentroid(List<Vector2> pts)
    {
        int n = pts.Count;
        float a = 0f, cx = 0f, cy = 0f;
        for (int i = 0; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            float cross = p.x * q.y - q.x * p.y;
            a += cross;
            cx += (p.x + q.x) * cross;
            cy += (p.y + q.y) * cross;
        }
        a *= 0.5f;
        if (Mathf.Abs(a) < 1e-8f)
        {
            Vector2 avg = Vector2.zero;
            for (int i = 0; i < n; i++) avg += pts[i];
            return avg / Mathf.Max(1, n);
        }
        cx /= (6f * a); cy /= (6f * a);
        return new Vector2(cx, cy);
    }

    private bool TryPickRoomUnderPointer(out string roomId)
    {
        roomId = "";

        var cam = Camera.main; if (!cam) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        Vector3 p;
        int mask = (floorRaycastMask.value == 0) ? Physics.DefaultRaycastLayers : floorRaycastMask.value;
        if (Physics.Raycast(ray, out var hit, 10000f, mask)) p = hit.point;
        else
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter)) return false;
            p = ray.GetPoint(enter);
        }

        Vector2 p2 = new Vector2(p.x, p.z);
        string bestId = ""; float bestArea = float.MaxValue;

        var rooms = RoomStorage.rooms;
        if (rooms == null || rooms.Count == 0) return false;

        for (int i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            if (r == null || r.checkpoints == null || r.checkpoints.Count < 3) continue;

            var poly = r.checkpoints;
            if (PointIn(poly, p2) || OnBoundary(p2, poly, 1e-3f))
            {
                float areaAbs = Mathf.Abs(SignedArea(poly));
                if (areaAbs < bestArea) { bestArea = areaAbs; bestId = r.ID; }
            }
        }

        if (!string.IsNullOrEmpty(bestId)) { roomId = bestId; return true; }
        return false;
    }

    private static bool PointIn(List<Vector2> poly, Vector2 p)
    {
        if (poly == null || poly.Count < 3) return false;
        int c = 0;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var a = poly[i];
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];
            if (((a.y > p.y) != (b.y > p.y)) &&
                (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-12f) + a.x))
                c++;
        }
        return (c & 1) == 1;
    }

    private static float DistPointToSeg2(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float ab2 = Vector2.Dot(ab, ab);
        if (ab2 < 1e-12f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
        var proj = a + t * ab;
        return (p - proj).sqrMagnitude;
    }

    private static bool OnBoundary(Vector2 p, List<Vector2> poly, float eps = 1e-4f)
    {
        if (poly == null || poly.Count < 2) return false;
        float eps2 = eps * eps;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var a = poly[i];
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];
            if (DistPointToSeg2(p, a, b) <= eps2) return true;
        }
        return false;
    }

    private void SetFloorColor(string id, Color color)
    {
        if (string.IsNullOrEmpty(id)) return;
        var floorGO = GetFloorGO(id);
        if (!floorGO) return;
        var rend = floorGO.GetComponent<MeshRenderer>() ?? floorGO.GetComponentInChildren<MeshRenderer>();
        if (rend != null) rend.material.color = color;
    }

    // === spawn/attach popup vào container nếu có, ngược lại tạo world-space fallback
    [SerializeField] private bool popupStickToMesh = true; // ép world-space

    private void SpawnOrAttachPopup(GameObject targetGO)
    {
        // Ưu tiên world-space nếu bật cờ
        if (popupStickToMesh || !actionSpaceRect || !uiCanvas)
        {
            if (!PopupRoom || !targetGO) return;
            if (!popupWS) popupWS = Instantiate(PopupRoom);

            var cv = popupWS.GetComponentInChildren<Canvas>(true) ?? popupWS.AddComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            if (!cv.worldCamera) cv.worldCamera = Camera.main;
            if (!popupWS.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>())
                popupWS.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // pivot bottom-center để position là “chân” popup
            var rt = popupWS.GetComponent<RectTransform>();
            if (!rt) rt = popupWS.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);

            popupWS.transform.SetParent(targetGO.transform, true);
            popupWS.transform.localRotation = Quaternion.identity;
            popupWS.transform.localScale = Vector3.one * 0.01f;
            popupWS.SetActive(true);

            if (popupUI) popupUI.SetActive(false); // tắt UI screen-space nếu có
            return;
        }

        // Ngược lại: screen-space trong ActionSpace
        if (!popupUI) popupUI = Instantiate(PopupRoom);
        popupRect = popupUI.GetComponent<RectTransform>() ?? popupUI.AddComponent<RectTransform>();
        popupUI.transform.SetParent(actionSpaceRect, false);
        popupUI.SetActive(true);
        if (popupWS) popupWS.SetActive(false);
    }
    public enum SelType { None, Room, Floor }

    public bool TryGetSelection(out SelType kind, out string id)
    {
        if (selectionKind == SelectionKind.Room && !string.IsNullOrEmpty(selectedRoomID))
        {
            kind = SelType.Room; id = selectedRoomID; return true;
        }
        if (selectionKind == SelectionKind.Floor && !string.IsNullOrEmpty(selectedFloorID))
        {
            kind = SelType.Floor; id = selectedFloorID; return true;
        }
        kind = SelType.None; id = ""; return false;
    }

}
