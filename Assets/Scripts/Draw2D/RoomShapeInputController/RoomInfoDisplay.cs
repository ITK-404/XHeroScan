using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class RoomInfoDisplay : MonoBehaviour
{
    [Header("Text Fields")]
    public TMP_Text lengthText;
    public TMP_Text widthText;
    public TMP_Text perimeterText;
    public TMP_Text areaText;

    [Header("Reference")]
    private CheckpointManager checkpointManager;

    [Header("Floor Highlight")]
    [SerializeField] private Color floorDefaultColor  = Color.white;
    [SerializeField] private Color floorSelectedColor = Color.yellow;

    private string selectedRoomID = "";
    private string highlightedRoomID = "";   // room sàn đang được tô màu

    private bool forceSelectFirstRoom = false;

    // NEW: chặn tự chọn room ngay sau khi xóa
    private bool suppressAutoPick = false;
    private int lastRoomsCount = 0;

    void Awake()
    {
        ClearText();
    }

    void Start()
    {
        checkpointManager = FindFirstObjectByType<CheckpointManager>();
        lastRoomsCount = RoomStorage.rooms.Count;
    }

    void Update()
    {
        // Phát hiện có xóa room (số lượng giảm)
        int curCount = RoomStorage.rooms.Count;
        if (curCount < lastRoomsCount)
        {
            // reset state lựa chọn & highlight
            if (!string.IsNullOrEmpty(highlightedRoomID))
            {
                SetRoomFloorColor(highlightedRoomID, floorDefaultColor);
                highlightedRoomID = "";
            }
            ResetAfterDelete();
        }
        lastRoomsCount = curCount;

        // Nếu không còn room nào -> reset cứng
        if (RoomStorage.rooms.Count == 0)
        {
            if (!string.IsNullOrEmpty(highlightedRoomID))
            {
                SetRoomFloorColor(highlightedRoomID, floorDefaultColor);
                highlightedRoomID = "";
            }
            selectedRoomID = "";
            suppressAutoPick = false;   // hết room rồi thì bỏ khóa
            forceSelectFirstRoom = false;
            ClearText();
            return;
        }

        string currentRoomID = checkpointManager.GetSelectedRoomID();

        // 1. Nếu có chọn mới → cập nhật nếu khác với room trước đó
        if (!string.IsNullOrEmpty(currentRoomID) && currentRoomID != selectedRoomID)
        {
            // reset sàn cũ
            if (!string.IsNullOrEmpty(highlightedRoomID) && highlightedRoomID != currentRoomID)
                SetRoomFloorColor(highlightedRoomID, floorDefaultColor);

            selectedRoomID = currentRoomID;
            highlightedRoomID = currentRoomID;
            SetRoomFloorColor(highlightedRoomID, floorSelectedColor);

            forceSelectFirstRoom = false;
            suppressAutoPick = false; // user đã chọn lại → bỏ khóa auto-pick
        }

        // 1.5. Nếu vừa reset thủ công và muốn ép lấy room đầu tiên
        if (forceSelectFirstRoom && RoomStorage.rooms.Count > 0 && string.IsNullOrEmpty(selectedRoomID))
        {
            selectedRoomID = RoomStorage.rooms[0].ID;

            // reset sàn cũ rồi highlight sàn mới
            if (!string.IsNullOrEmpty(highlightedRoomID) && highlightedRoomID != selectedRoomID)
                SetRoomFloorColor(highlightedRoomID, floorDefaultColor);

            highlightedRoomID = selectedRoomID;
            SetRoomFloorColor(highlightedRoomID, floorSelectedColor);

            Room room = RoomStorage.rooms[0];
            if (room != null) UpdateRoomInfo(room);
            forceSelectFirstRoom = false;
            return;
        }

        // 2. Chỉ auto-pick room đầu tiên nếu KHÔNG bị khóa
        if (!suppressAutoPick && string.IsNullOrEmpty(currentRoomID) && string.IsNullOrEmpty(selectedRoomID) && RoomStorage.rooms.Count > 0)
        {
            selectedRoomID = RoomStorage.rooms[0].ID;

            if (!string.IsNullOrEmpty(highlightedRoomID) && highlightedRoomID != selectedRoomID)
                SetRoomFloorColor(highlightedRoomID, floorDefaultColor);

            highlightedRoomID = selectedRoomID;
            SetRoomFloorColor(highlightedRoomID, floorSelectedColor);
        }

        // 3. Nếu room hiện tại bị xoá
        if (!string.IsNullOrEmpty(selectedRoomID) && RoomStorage.GetRoomByID(selectedRoomID) == null)
        {
            // reset highlight của room vừa bị xóa
            if (!string.IsNullOrEmpty(highlightedRoomID))
            {
                SetRoomFloorColor(highlightedRoomID, floorDefaultColor);
                highlightedRoomID = "";
            }

            selectedRoomID = "";
            ClearText();
            // giữ suppressAutoPick = true nếu vừa xóa để không auto-pick lại
            return;
        }

        // 4. Nếu đã có room được chọn -> luôn cập nhật realtime
        if (!string.IsNullOrEmpty(selectedRoomID))
        {
            Room room = RoomStorage.GetRoomByID(selectedRoomID);
            if (room != null)
            {
                UpdateRoomInfo(room);
            }
        }
        else
        {
            // Không có room được chọn (do vừa xóa hoặc click ra ngoài)
            // reset highlight nếu còn
            if (!string.IsNullOrEmpty(highlightedRoomID))
            {
                SetRoomFloorColor(highlightedRoomID, floorDefaultColor);
                highlightedRoomID = "";
            }
            ClearText();
        }
    }

    void UpdateRoomInfo(Room room)
    {
        List<Vector2> points = room.checkpoints;
        if (points == null || points.Count < 3)
        {
            ClearText();
            return;
        }

        float perimeter = 0f;
        float maxLength = 0f;
        float minLength = float.MaxValue;
        float area = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            float dist = Vector2.Distance(a, b);
            perimeter += dist;
            maxLength = Mathf.Max(maxLength, dist);
            minLength = Mathf.Min(minLength, dist);
            area += (a.x * b.y - b.x * a.y);
        }

        area = Mathf.Abs(area) * 0.5f;

        lengthText.text = $"Chiều dài: {maxLength:F2} m";
        widthText.text  = $"Chiều rộng: {minLength:F2} m";
        perimeterText.text = $"Chu vi: {perimeter:F2} m";
        areaText.text   = $"Diện tích: {area:F2} m²";
    }

    public void ClearText()
    {
        lengthText.text = "Chiều dài: -";
        widthText.text  = "Chiều rộng: -";
        perimeterText.text = "Chu vi: -";
        areaText.text   = "Diện tích: -";
    }

    public void ResetState()
    {
        // reset highlight hiện tại (nếu có)
        if (!string.IsNullOrEmpty(highlightedRoomID))
        {
            SetRoomFloorColor(highlightedRoomID, floorDefaultColor);
            highlightedRoomID = "";
        }

        selectedRoomID = "";
        forceSelectFirstRoom = true;   // khác với ResetAfterDelete
        suppressAutoPick = false;      // cho phép auto-pick
        ClearText();
    }

    public void ResetAfterDelete()
    {
        // reset highlight hiện tại (nếu có)
        if (!string.IsNullOrEmpty(highlightedRoomID))
        {
            SetRoomFloorColor(highlightedRoomID, floorDefaultColor);
            highlightedRoomID = "";
        }

        selectedRoomID = "";
        forceSelectFirstRoom = false;
        suppressAutoPick = true; // khóa auto-pick cho đến khi user chọn lại
        ClearText();
    }

    // === đặt màu lên sàn của phòng có id ===
    private void SetRoomFloorColor(string roomId, Color color)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        // Ưu tiên tìm theo tên "RoomFloor_{roomId}"
        GameObject floorGO = GameObject.Find($"RoomFloor_{roomId}");

        if (floorGO == null) return;

        // Đổi màu vật liệu (Renderer có thể nằm ở chính nó hoặc con)
        var rend = floorGO.GetComponent<MeshRenderer>() ?? floorGO.GetComponentInChildren<MeshRenderer>();
        if (rend != null)
        {
            // renderer.material sẽ tạo instance riêng
            rend.material.color = color;
        }
    }
}
