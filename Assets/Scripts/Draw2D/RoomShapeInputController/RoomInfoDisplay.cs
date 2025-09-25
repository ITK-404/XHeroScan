using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class RoomInfoDisplay : MonoBehaviour
{
    [Header("Reference")]
    private CheckpointManager checkpointManager;
    private RoomMidpointEditor midpointEditor;

    [Header("Pick/Raycast")]
    [SerializeField] private LayerMask floorRaycastMask = ~0;

    [Header("Floor Highlight")]
    [SerializeField] private Color floorDefaultColor  = Color.white;
    [SerializeField] private Color floorSelectedColor = Color.white; // giữ mesh trắng khi selected

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
    [SerializeField] private GameObject ActionSpace;
    [SerializeField] private GameObject PopupRoom;
    [SerializeField] private Vector2 popupScreenOffset = new Vector2(0, 40);

    [Header("Popup World Fallback")]
    [SerializeField] private float popupY = 0.2f;
    [SerializeField] private float popupX = 0.2f;
    [SerializeField] private float popupZ = 0.2f;

    public enum SelectionKind { None, Room, Floor, Furniture }
    private SelectionKind selectionKind = SelectionKind.None;
    public SelectionKind SelectionItem => selectionKind;
    private string selectedRoomID = "";
    private string selectedFloorID = "";
    private string highlightedID = "";

    private bool forceSelectFirstRoom = false;
    private bool suppressAutoPick = false;
    private int lastRoomsCount = 0;

    private readonly Dictionary<string, TextMeshPro> roomLabels = new();
    private readonly Dictionary<string, TextMeshPro> floorLabels = new();

    private GameObject popupUI;
    private RectTransform popupRect;
    private RectTransform actionSpaceRect;
    private Canvas uiCanvas;
    private GameObject popupWS;

    [SerializeField] RoomToggleFurnitureVisible roomToggle;

    // ===== HIGHLIGHT =====
    [Header("Highlight (points + lines)")]
    [SerializeField] private Color highlightColor = Color.red;

    private readonly Dictionary<Renderer, Color> _origRendererColor = new();
    private readonly Dictionary<LineRenderer, (Color start, Color end)> _origLineColor = new();
    private readonly Dictionary<LineRenderer, (int propId, Color col)> _origLineMatColor = new();

    private static readonly int PROP_COLOR     = Shader.PropertyToID("_Color");
    private static readonly int PROP_BASECOLOR = Shader.PropertyToID("_BaseColor");
    private static readonly int PROP_TINT      = Shader.PropertyToID("_TintColor");

    void Start()
    {
        roomToggle.gameObject.SetActive(false);
        checkpointManager = FindFirstObjectByType<CheckpointManager>();
        midpointEditor = FindFirstObjectByType<RoomMidpointEditor>();
        lastRoomsCount = RoomStorage.rooms.Count;

        if (ActionSpace)
        {
            actionSpaceRect = ActionSpace.GetComponent<RectTransform>();
            uiCanvas = actionSpaceRect ? actionSpaceRect.GetComponentInParent<Canvas>() : null;
        }
    }

    void Update()
    {
        if (RoomMidpointEditor.IsDraggingMidpoint)
    return;
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (FurnitureManager.Instance.TryPickFurniture())
            {
                DeselectAll();
                ResetState();
                selectionKind = SelectionKind.Furniture;
                return;
            }

            if (TryPickRoomUnderPointer(out var roomId))
            {
                SelectRoom(roomId);
                return;
            }

            // bool canPickFloorNow = (selectionKind != SelectionKind.None) || allowFloorPickWhenNoRoomSelected;
            // if (canPickFloorNow && TryPickFloorUnderPointer(out var floorId, out var _))
            if (TryPickFloorUnderPointer(out var floorId, out var _))
            {
                SelectFloor(floorId);
                return;
            }

            DeselectAll();
            return;
        }

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
            // Chỉ clear lựa chọn ROOM, giữ nguyên FLOOR nếu đang chọn floor
            if (selectionKind == SelectionKind.Room)
            {
                if (!string.IsNullOrEmpty(highlightedID))
                {
                    SetFloorColor(highlightedID, floorDefaultColor);
                    highlightedID = "";
                }

                HideRoomLabel(selectedRoomID);
                selectedRoomID = "";
                selectionKind = string.IsNullOrEmpty(selectedFloorID) ? SelectionKind.None : SelectionKind.Floor;
                roomToggle.DeSelectect();

                UnhighlightAllVisuals();
                if (popupUI) popupUI.SetActive(false); // popup room
            }
        }

        if (selectionKind == SelectionKind.Furniture) return;

        if (selectionKind != SelectionKind.Floor)
        {
            string currentRoomID = checkpointManager.GetSelectedRoomID();

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
                roomToggle.DeSelectect();

                UnhighlightAllVisuals();

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
        string curId = (selectionKind == SelectionKind.Room) ? selectedRoomID :
                       (selectionKind == SelectionKind.Floor) ? selectedFloorID : "";
        if (string.IsNullOrEmpty(curId)) return;

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

        var targetGO = GetFloorGO(curId);
        float baseY = targetGO ? targetGO.transform.position.y : 0f;
        Vector2 c2 = PolygonCentroid(cps);

        Vector3 lateral = Vector3.zero;
        Vector3 depth = Vector3.zero;

        var cam = Camera.main;
        if (cam)
        {
            Vector3 camRight = cam.transform.right; camRight.y = 0f;
            if (camRight.sqrMagnitude < 1e-6f) camRight = Vector3.right;
            camRight.Normalize();
            lateral = camRight * popupX;

            Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 1e-6f)
                camFwd = Vector3.Cross(Vector3.up, camRight);
            camFwd.Normalize();
            depth = camFwd * popupZ;
        }

        Vector3 worldPos = new Vector3(
            c2.x + lateral.x + depth.x,
            baseY + labelYLift,
            c2.y + lateral.z + depth.z
        );

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

        if (actionSpaceRect && uiCanvas && popupUI && popupUI.activeSelf)
        {
            Camera camWS;
            Camera camLocal;
            if (uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                camWS = Camera.main;
                camLocal = null;
            }
            else
            {
                camWS = uiCanvas.worldCamera ? uiCanvas.worldCamera : Camera.main;
                camLocal = camWS;
            }
            if (!camWS) return;

            var view = camWS.WorldToViewportPoint(worldPos);
            if (view.z <= 0f) { popupUI.SetActive(false); return; }

            Vector3 screen = camWS.WorldToScreenPoint(worldPos) + (Vector3)popupScreenOffset;
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

        if (popupWS && popupWS.activeSelf)
        {
            float camYaw = Camera.main ? Camera.main.transform.eulerAngles.y : 0f;
            // popupWS.transform.localRotation = Quaternion.Euler(0f, camYaw, 0f);
            popupWS.transform.position = worldPos;
            popupWS.transform.rotation = Quaternion.Euler(90f, camYaw, 0f);
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

        UnhighlightAllVisuals();      // clear cũ
        HighlightRoomVisuals(roomId); // tô đỏ point + line

        var room = RoomStorage.GetRoomByID(selectedRoomID);
        ShowOnlyRoomLabel(room);

        var targetGO = GetFloorGO(roomId);
        SpawnOrAttachPopup(targetGO);

        forceSelectFirstRoom = false;
        suppressAutoPick = false;

        roomToggle.SelectRoom(roomId);
        if (midpointEditor)
        {
            Debug.Log($"[RoomInfoDisplay] Show midpoint editor for room {roomId}");
            midpointEditor.ShowForRoomID(roomId);
        }
    }

    private void SelectFloor(string floorId)
    {
        if (selectionKind == SelectionKind.Room && !string.IsNullOrEmpty(selectedRoomID))
        {
            HideRoomLabel(selectedRoomID);
            selectedRoomID = "";
            roomToggle.DeSelectect();
            if (midpointEditor) midpointEditor.Hide();
        }
        if (!string.IsNullOrEmpty(highlightedID) && highlightedID != floorId)
            SetFloorColor(highlightedID, floorDefaultColor);

        selectedFloorID = floorId;
        selectionKind = SelectionKind.Floor;
        highlightedID = floorId;

        SetFloorColor(highlightedID, floorSelectedColor);

        UnhighlightAllVisuals();

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
        roomToggle.DeSelectect();

        UnhighlightAllVisuals();

        HideAllLabels();
        if (popupUI) popupUI.SetActive(false);
        if (popupWS) popupWS.SetActive(false);

        suppressAutoPick = true;
        if (midpointEditor) midpointEditor.Hide();
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
        roomToggle.DeSelectect();

        // forceSelectFirstRoom = true;
        suppressAutoPick = false;

        UnhighlightAllVisuals();

        HideAllLabels();
        if (popupUI) popupUI.SetActive(false);
        if (popupWS) popupWS.SetActive(false);
        if (midpointEditor) midpointEditor.Hide();
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
        roomToggle.DeSelectect();
        selectionKind = SelectionKind.None;

        // forceSelectFirstRoom = false;
        suppressAutoPick = true;

        UnhighlightAllVisuals();

        HideAllLabels();
        if (popupUI) popupUI.SetActive(false);
        if (popupWS) popupWS.SetActive(false);
        if (midpointEditor) midpointEditor.Hide();
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
        text.color = Color.red;
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

        float area = Mathf.Abs(SignedArea(cps));
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
        text.color = Color.red;
        text.outlineWidth = 0.15f;
        text.outlineColor = labelOutlineColor;
        if (labelFont) text.font = labelFont;

        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        floorLabels[floorId] = text;
        return text;
    }

    private void UpdateFloorLabel(Floor floor)
    {
        string Fname = string.IsNullOrWhiteSpace(floor.floorName) ? floor.ID : floor.floorName;

        if (floor == null) return;
        if (!floorLabels.TryGetValue(floor.ID, out var tmp) || tmp == null)
        {
            tmp = EnsureFloorLabel(floor.ID);
            if (tmp == null) return;
        }
        var cps = floor.checkpoints;
        if (cps == null || cps.Count < 3) { tmp.text = ""; return; }

        float area = Mathf.Abs(SignedArea(cps));
        Vector2 centroid2 = PolygonCentroid(cps);
        var floorGO = GetFloorGO(floor.ID);
        float baseY = floorGO ? floorGO.transform.position.y : 0f;

        tmp.transform.position = new Vector3(centroid2.x, baseY + labelYLift, centroid2.y);
        tmp.text = $"Floor {Fname}\n{area:F2} m²";

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

    [SerializeField] private bool popupStickToMesh = true;

    private void SpawnOrAttachPopup(GameObject targetGO)
    {
        if (popupStickToMesh || !actionSpaceRect || !uiCanvas)
        {
            if (!PopupRoom || !targetGO) return;
            if (!popupWS) popupWS = Instantiate(PopupRoom);

            var cv = popupWS.GetComponentInChildren<Canvas>(true) ?? popupWS.AddComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            cv.sortingOrder = 100;
            if (!cv.worldCamera) cv.worldCamera = Camera.main;
            if (!popupWS.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>())
                popupWS.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var rt = popupWS.GetComponent<RectTransform>();
            if (!rt) rt = popupWS.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);

            popupWS.transform.SetParent(targetGO.transform, true);
            popupWS.transform.localRotation = Quaternion.identity;
            popupWS.transform.localScale = Vector3.one * 0.01f;
            popupWS.SetActive(true);

            if (popupUI) popupUI.SetActive(false);
            return;
        }

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

    // ===================== HIGHLIGHT =====================

    private IEnumerable<Renderer> EnumerateCheckpointRenderers(string roomId)
    {
        if (checkpointManager == null || checkpointManager.loopMappings == null) yield break;

        for (int i = 0; i < checkpointManager.loopMappings.Count; i++)
        {
            var map = checkpointManager.loopMappings[i];
            if (map == null || map.RoomID != roomId || map.CheckpointsGO == null) continue;

            for (int j = 0; j < map.CheckpointsGO.Count; j++)
            {
                var go = map.CheckpointsGO[j];
                if (!go) continue;

                var r = go.GetComponent<Renderer>();
                if (r) yield return r;

                var childRends = go.GetComponentsInChildren<Renderer>(true);
                for (int k = 0; k < childRends.Length; k++) yield return childRends[k];
            }
            yield break;
        }
    }

    private IEnumerable<LineRenderer> EnumerateRoomLinesUnderFloor(string roomId)
    {
        var floorGO = GetFloorGO(roomId);
        if (!floorGO) yield break;

        var lines = floorGO.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lines.Length; i++) yield return lines[i];
    }

    // Fallback: line ở bất cứ đâu, 2 đầu mút nằm trong/biên polygon room
    private IEnumerable<LineRenderer> EnumerateRoomLinesAnywhere(string roomId)
    {
        var room = RoomStorage.GetRoomByID(roomId);
        if (room == null || room.checkpoints == null || room.checkpoints.Count < 3) yield break;

        bool InPoly(Vector3 p)
        {
            var pp = new Vector2(p.x, p.z);
            return PointIn(room.checkpoints, pp) || OnBoundary(pp, room.checkpoints, 1e-3f);
        }

        // var all = GameObject.FindObjectsOfType<LineRenderer>(true);
            var all = Object.FindObjectsByType<LineRenderer>(
                    FindObjectsInactive.Include,   // lấy cả inactive
                    FindObjectsSortMode.None       // không cần sort để nhanh hơn
                    );
        foreach (var lr in all)
        {
            if (lr.transform.parent != null) continue;
            if (!lr || lr.positionCount < 2) continue;
            Vector3 a = lr.GetPosition(0);
            Vector3 b = lr.GetPosition(lr.positionCount - 1);
            if (InPoly(a) && InPoly(b)) yield return lr;
        }
    }

    private static bool TryGetColorProp(Material m, out int propId)
    {
        propId = 0;
        if (!m) return false;
        if (m.HasProperty(PROP_COLOR))     { propId = PROP_COLOR;     return true; }
        if (m.HasProperty(PROP_BASECOLOR)) { propId = PROP_BASECOLOR; return true; }
        if (m.HasProperty(PROP_TINT))      { propId = PROP_TINT;      return true; }
        return false;
    }

    private void SetRendererColor(Renderer r, Color c)
    {
        if (!r) return;
        if (r is SpriteRenderer sr) { sr.color = c; return; }

        var m = r.material;
        if (!m) return;

        if (m.HasProperty(PROP_COLOR))     { m.color = c; return; }
        if (m.HasProperty(PROP_BASECOLOR)) { m.SetColor(PROP_BASECOLOR, c); return; }
        if (m.HasProperty(PROP_TINT))      { m.SetColor(PROP_TINT, c); return; }
    }

    private void TintLine(LineRenderer lr, Color col)
    {
        if (!lr) return;

        // đổi gradient
        lr.startColor = col;
        lr.endColor   = col;

        // tint material nếu có
        var m = lr.material;
        if (m && TryGetColorProp(m, out int pid))
        {
            if (!_origLineMatColor.ContainsKey(lr))
            {
                // cache màu gốc của material
                Color baseCol = m.GetColor(pid);
                _origLineMatColor[lr] = (pid, baseCol);
            }
            m.SetColor(pid, col);
        }
    }

    private void HighlightRoomVisuals(string roomId)
    {
        // POINTS
        foreach (var r in EnumerateCheckpointRenderers(roomId))
        {
            if (!r) continue;
            if (!_origRendererColor.ContainsKey(r))
            {
                Color baseCol = Color.white;
                if (r is SpriteRenderer sr) baseCol = sr.color;
                else if (r.material && r.material.HasProperty(PROP_COLOR))     baseCol = r.material.color;
                else if (r.material && r.material.HasProperty(PROP_BASECOLOR)) baseCol = r.material.GetColor(PROP_BASECOLOR);
                else if (r.material && r.material.HasProperty(PROP_TINT))      baseCol = r.material.GetColor(PROP_TINT);
                _origRendererColor[r] = baseCol;
            }
            SetRendererColor(r, highlightColor);
        }

        // LINES dưới floor
        foreach (var lr in EnumerateRoomLinesUnderFloor(roomId))
        {
            if (!lr) continue;
            if (!_origLineColor.ContainsKey(lr))
                _origLineColor[lr] = (lr.startColor, lr.endColor);

            TintLine(lr, highlightColor);
        }

        // Fallback: LINES ở bất cứ đâu
        foreach (var lr in EnumerateRoomLinesAnywhere(roomId))
        {
            if (!lr) continue;
            if (!_origLineColor.ContainsKey(lr))
                _origLineColor[lr] = (lr.startColor, lr.endColor);

            TintLine(lr, highlightColor);
        }
    }

    private void UnhighlightAllVisuals()
    {
        // checkpoints
        foreach (var kv in _origRendererColor)
        {
            var r = kv.Key; if (!r) continue;
            SetRendererColor(r, kv.Value);
        }
        _origRendererColor.Clear();

        // line gradient
        foreach (var kv in _origLineColor)
        {
            var lr = kv.Key; if (!lr) continue;
            lr.startColor = kv.Value.start;
            lr.endColor   = kv.Value.end;
        }
        _origLineColor.Clear();

        // line material tint
        foreach (var kv in _origLineMatColor)
        {
            var lr = kv.Key; if (!lr) continue;
            var m = lr.material;
            if (m && m.HasProperty(kv.Value.propId))
                m.SetColor(kv.Value.propId, kv.Value.col);
        }
        _origLineMatColor.Clear();
    }
}
