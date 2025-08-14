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

    private string selectedRoomID = "";
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
            ResetAfterDelete();
        }
        lastRoomsCount = curCount;

        // Nếu không còn room nào -> reset cứng
        if (RoomStorage.rooms.Count == 0)
        {
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
            selectedRoomID = currentRoomID;
            forceSelectFirstRoom = false;
            suppressAutoPick = false; // user đã chọn lại → bỏ khóa auto-pick
        }

        // 1.5. Nếu vừa reset thủ công và muốn ép lấy room đầu tiên
        if (forceSelectFirstRoom && RoomStorage.rooms.Count > 0 && string.IsNullOrEmpty(selectedRoomID))
        {
            selectedRoomID = RoomStorage.rooms[0].ID;
            Room room = RoomStorage.rooms[0];
            if (room != null) UpdateRoomInfo(room);
            forceSelectFirstRoom = false;
            return;
        }

        // 2. Chỉ auto-pick room đầu tiên nếu KHÔNG bị khóa
        if (!suppressAutoPick && string.IsNullOrEmpty(currentRoomID) && string.IsNullOrEmpty(selectedRoomID) && RoomStorage.rooms.Count > 0)
        {
            selectedRoomID = RoomStorage.rooms[0].ID;
        }

        // 3. Nếu room hiện tại bị xoá
        if (!string.IsNullOrEmpty(selectedRoomID) && RoomStorage.GetRoomByID(selectedRoomID) == null)
        {
            selectedRoomID = "";
            ClearText();
            // giữ suppressAutoPick = true nếu vừa xóa để không auto-pick lại
            return;
        }

        // 4. Nếu đã có room được chọn → luôn cập nhật realtime
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
        widthText.text = $"Chiều rộng: {minLength:F2} m";
        perimeterText.text = $"Chu vi: {perimeter:F2} m";
        areaText.text = $"Diện tích: {area:F2} m²";
    }

    public void ClearText()
    {
        lengthText.text = "Chiều dài: -";
        widthText.text = "Chiều rộng: -";
        perimeterText.text = "Chu vi: -";
        areaText.text = "Diện tích: -";
    }

    public void ResetState()
    {
        selectedRoomID = "";
        forceSelectFirstRoom = true;   // khác với ResetAfterDelete
        suppressAutoPick = false;      // cho phép auto-pick
        ClearText();
    }

    public void ResetAfterDelete()
    {
        selectedRoomID = "";
        forceSelectFirstRoom = false;
        suppressAutoPick = true; // khóa auto-pick cho đến khi user chọn lại
        ClearText();
    }
}
