using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClearAllRoomsButton : MonoBehaviour
{
    [Header("References")]
    public Button clearAllButton;
    public CheckpointManager checkpointManager;

    private RoomInfoDisplay roomInfoDisplay; // UI hiển thị info phòng
    [SerializeField] private ToggleGroupUI toggleGroupUI;
    [SerializeField] private PenManager penManager;
    [SerializeField] private DrawingTool drawingTool;

    private const string CLEAR_ALL_WARNING = "Bạn có chắc chắn muốn xóa tất cả khung đã chọn?";
    private const string CLEAR_ONE_WARNING = "Bạn có chắc chắn muốn xóa khung đã chọn?";


    void Start()
    {
        if (penManager == null)
            penManager = FindFirstObjectByType<PenManager>();

        if (clearAllButton != null)
            clearAllButton.onClick.AddListener(OnClearAllClicked);
        else
            Debug.LogError("Chưa gán ClearAllButton!");

        if (checkpointManager == null)
            Debug.LogError("Chưa gán CheckpointManager!");
    }

    void OnClearAllClicked()
    {
        if (checkpointManager == null) return;

        // Nếu có phòng đang chọn -> xóa ngay phòng đó (không popup)
        string currentRoomID = checkpointManager.GetSelectedRoomID();
        if (!string.IsNullOrEmpty(currentRoomID))
        {
            var room = RoomStorage.GetRoomByID(currentRoomID);
            string displayName = !string.IsNullOrEmpty(room?.roomName) ? room.roomName : currentRoomID;

            var popupOne = Instantiate(ModularPopup.PopupAsset.modularPopupWarningDelete).GetComponent<ModularPopup>();
            popupOne.AutoFindCanvasAndSetup();
            popupOne.Header = string.Format(CLEAR_ONE_WARNING, displayName);
            popupOne.ClickYesEvent = () =>
            {
                Debug.Log($"Người dùng xác nhận: Xóa phòng {displayName} ({currentRoomID})");
                ClearRoomById(currentRoomID);
                checkpointManager?.ClearSelectedRoom();

                // Reset UI
                if (toggleGroupUI != null) toggleGroupUI.ToggleOffAll();
                if (penManager != null) penManager.ChangeState(true);
                if (roomInfoDisplay != null) roomInfoDisplay.ResetState();
            };
            popupOne.autoClearWhenClick = true;
            return;
        }

        // Không có phòng được chọn -> hỏi xác nhận xóa tất cả
        var popup = Instantiate(ModularPopup.PopupAsset.modularPopupWarningDelete).GetComponent<ModularPopup>();
        popup.AutoFindCanvasAndSetup();
        popup.Header = CLEAR_ALL_WARNING;
        popup.ClickYesEvent = () =>
        {
            Debug.Log("Người dùng xác nhận: Xóa tất cả!");
            ClearEverything();

            if (toggleGroupUI != null) toggleGroupUI.ToggleOffAll();
            if (penManager != null) penManager.ChangeState(true);
            if (roomInfoDisplay != null) roomInfoDisplay.ResetState();
        };
        popup.autoClearWhenClick = true;
    }

    /// <summary>
    /// Xóa DUY NHẤT 1 phòng theo roomID.
    /// </summary>
    public void ClearRoomById(string roomID)
    {
        if (string.IsNullOrEmpty(roomID))
        {
            Debug.LogWarning("[ClearRoomById] roomID rỗng.");
            return;
        }

        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null)
        {
            Debug.LogWarning($"[ClearRoomById] Không tìm thấy phòng: {roomID}");
            return;
        }

        // 1. Xóa floor mesh của phòng này
        var floors = GameObject.FindObjectsByType<RoomMeshController>(FindObjectsSortMode.None);
        foreach (var floor in floors)
        {
            if (floor.RoomID == roomID)
                Destroy(floor.gameObject);
        }

        // 2. Xóa checkpoints của phòng này
        var loop = GetLoopByRoomID(roomID);
        if (loop != null)
        {
            // Nếu đang chọn checkpoint thuộc phòng này -> hủy chọn
            if (checkpointManager != null &&
                checkpointManager.selectedCheckpoint != null &&
                loop.Contains(checkpointManager.selectedCheckpoint))
            {
                checkpointManager.DeselectCheckpoint();
                checkpointManager.isDragging = false;
                checkpointManager.isMovingCheckpoint = false;
            }

            foreach (var cp in loop)
                if (cp != null) Destroy(cp);

            if (checkpointManager != null)
                checkpointManager.AllCheckpoints.Remove(loop);
        }

        // 3. Xóa cửa/cửa sổ tạm của phòng này (nếu có)
        if (checkpointManager != null &&
            checkpointManager.tempDoorWindowPoints != null &&
            checkpointManager.tempDoorWindowPoints.TryGetValue(roomID, out var doorPts))
        {
            foreach (var (_, p1GO, p2GO) in doorPts)
            {
                if (p1GO) Destroy(p1GO);
                if (p2GO) Destroy(p2GO);
            }
            checkpointManager.tempDoorWindowPoints.Remove(roomID);
        }

        // 4. Xóa dữ liệu phòng trong RoomStorage
        RoomStorage.rooms.RemoveAll(r => r.ID == roomID);

        // 5. Vẽ lại
        if (checkpointManager != null)
        {
            checkpointManager.ClearAllLines();
            checkpointManager.RedrawAllRooms();
        }

        if (checkpointManager != null)
        {
            checkpointManager.ClearSelectedRoom(); // ← đảm bảo GetSelectedRoomID() trả về null sau khi xóa
        }

        Debug.Log($"Đã xóa phòng: {roomID}");
    }

    /// <summary>
    /// Xóa TẤT CẢ phòng + checkpoint + mesh + dữ liệu tạm (có Undo tổng nếu hệ thống có).
    /// </summary>
    public void ClearEverything(bool isCreateCommand = true)
    {
        if (!roomInfoDisplay)
            roomInfoDisplay = FindFirstObjectByType<RoomInfoDisplay>();

        // Không tạo lệnh Undo nếu chẳng có room nào
        if (RoomStorage.rooms.Count == 0)
            isCreateCommand = false;

        // Xóa mesh floor
        List<Delete_RoomData> deleteRoomDataList = new();
        var floors = GameObject.FindObjectsByType<RoomMeshController>(FindObjectsSortMode.None);
        foreach (var floor in floors)
        {
            if (isCreateCommand)
            {
                var deleteRoomData = new Delete_RoomData(new Room(RoomStorage.GetRoomByID(floor.RoomID)), floor.transform.position);
                deleteRoomDataList.Add(deleteRoomData);
            }
            Destroy(floor.gameObject);
        }

        // Xóa Room trong RoomStorage
        RoomStorage.rooms.Clear();

        // Xóa checkpoints prefab
        if (checkpointManager != null)
        {
            foreach (var loop in checkpointManager.AllCheckpoints)
                foreach (var cp in loop)
                    if (cp != null) Destroy(cp);

            checkpointManager.AllCheckpoints.Clear();

            // Xóa dữ liệu tạm
            checkpointManager.DeleteCurrentDrawingData();

            // Clear line trong DrawingTool
            checkpointManager.ClearAllLines();
        }

        Debug.Log("Đã xóa toàn bộ Room, checkpoint, mesh, line!");
        if (drawingTool != null) drawingTool.currentLineType = LineType.Wall;

        if (isCreateCommand)
        {
            DeleteAllRoomCommand deleteAllRoomCommand = new DeleteAllRoomCommand(deleteRoomDataList);
            deleteAllRoomCommand.ClearAllRoom = this;
            UndoRedoController.Instance.AddToUndo(deleteAllRoomCommand);
        }

        FurnitureManager.Instance.ClearAllFurnitures();
    }

    private List<GameObject> GetLoopByRoomID(string roomID)
    {
        if (checkpointManager == null) return null;
        foreach (var lp in checkpointManager.AllCheckpoints)
            if (checkpointManager.FindRoomIDForLoop(lp) == roomID) return lp;
        return null;
    }
}
