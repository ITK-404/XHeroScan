using System.Collections.Generic;
using System.Linq;
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

    public void OnClearAllClicked()
    {
        if (checkpointManager == null) return;
        
        SaveLoadManager.MakeDirty();
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
            if (checkpointManager != null &&
                checkpointManager.selectedCheckpoint != null &&
                loop.Contains(checkpointManager.selectedCheckpoint))
            {
                checkpointManager.DeselectCheckpoint();
                checkpointManager.isDragging = false;
                checkpointManager.isMovingCheckpoint = false;
            }

            foreach (var cp in loop) if (cp) Destroy(cp);
            checkpointManager?.AllCheckpoints.Remove(loop);
        }

        // Gỡ mapping khác
        checkpointManager?.RoomFloorMap.Remove(roomID);
        if (checkpointManager?.currentCheckpoints != null)
            checkpointManager.currentCheckpoints.RemoveAll(go => !go); // dọn null

        // Xóa cửa/cửa sổ tạm
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

        // Xóa dữ liệu phòng trong RoomStorage
        RoomStorage.rooms.RemoveAll(r => r.ID == roomID);

        // Vẽ lại
        if (checkpointManager != null)
        {
            checkpointManager.ClearAllLines();
            checkpointManager.RedrawAllRooms();
            checkpointManager.ClearSelectedRoom();
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

        // === TRƯỜNG HỢP: Xoá 1 floor cụ thể ===
        if (roomInfoDisplay != null &&
                roomInfoDisplay.TryGetSelection(out RoomInfoDisplay.SelType kind, out string floorId) &&
                kind == RoomInfoDisplay.SelType.Floor &&
                !string.IsNullOrEmpty(floorId))
            {
                // Lấy danh sách room trên floor này
                var roomsOnFloor = new List<Room>();
                for (int i = 0; i < RoomStorage.rooms.Count; i++)
                {
                    var r = RoomStorage.rooms[i];
                    if (r != null && r.floorID == floorId)
                        roomsOnFloor.Add(r);
                }

                // Undo data
                List<Delete_RoomData> deleteRoomDataList = new();
                if (isCreateCommand)
                {
                    var meshes = GameObject.FindObjectsByType<RoomMeshController>(FindObjectsSortMode.None);
                    foreach (var r in roomsOnFloor)
                    {
                        Vector3 meshPos = Vector3.zero;
                        foreach (var m in meshes)
                        {
                            if (m != null && m.RoomID == r.ID)
                            {
                                meshPos = m.transform.position;
                                break;
                            }
                        }
                        deleteRoomDataList.Add(new Delete_RoomData(new Room(r), meshPos));
                    }
                }

                // Xoá room
                for (int i = 0; i < roomsOnFloor.Count; i++)
                    ClearRoomById(roomsOnFloor[i].ID);

                // Xoá floor trong FloorStorage
                Floor floorData = null;
                for (int i = 0; i < FloorStorage.floors.Count; i++)
                {
                    var f = FloorStorage.floors[i];
                    if (f != null && f.ID == floorId)
                    {
                        floorData = f;
                        break;
                    }
                }
                if (floorData != null)
                {
                    floorData.checkpoints.Clear();
                    floorData.floorLine.Clear();
                    floorData.heights.Clear();
                    floorData.roomIDs.Clear();
                    FloorStorage.floors.Remove(floorData);
                }

                // Xoá GameObject floor mesh
                GameObject floorGO = GameObject.Find($"Floor_{floorId}");
                if (!floorGO) floorGO = GameObject.Find($"RoomFloor_{floorId}");
                if (floorGO) Destroy(floorGO);

                // 6. Xoá FloorVis (visuals: point, line, label…)
                var visGo = GameObject.Find($"FloorVis_{floorId}");
                if (visGo) Destroy(visGo);

                // Reset
                if (checkpointManager != null)
                {
                    checkpointManager.ClearAllLines();
                    checkpointManager.RedrawAllRooms();
                    checkpointManager.ClearSelectedRoom();
                }
                if (drawingTool != null) drawingTool.currentLineType = LineType.Wall;

                // Undo
                if (isCreateCommand && deleteRoomDataList.Count > 0)
                {
                    var deleteAllRoomCommand = new DeleteAllRoomCommand(deleteRoomDataList);
                    deleteAllRoomCommand.ClearAllRoom = this;
                    //UndoRedoController.Instance.AddToUndo(deleteAllRoomCommand);
                }

                FurnitureManager.Instance?.ClearAllFurnitures();
                Debug.Log($"Đã xoá floor {floorId} và {roomsOnFloor.Count} phòng trên sàn.");
                return;
            }

        // === TRƯỜNG HỢP: Không chọn gì -> Xoá TẤT CẢ floors & rooms ===

        if (RoomStorage.rooms.Count == 0 && (FloorStorage.floors == null || FloorStorage.floors.Count == 0))
            isCreateCommand = false;

        // Gom Undo cho rooms hiện có
        List<Delete_RoomData> deleteAllRoomDataList = new();
        var roomMeshes = GameObject.FindObjectsByType<RoomMeshController>(FindObjectsSortMode.None);
        foreach (var rm in roomMeshes)
        {
            if (isCreateCommand)
            {
                var srcRoom = RoomStorage.GetRoomByID(rm.RoomID);
                var data = new Delete_RoomData(new Room(srcRoom), rm.transform.position);
                deleteAllRoomDataList.Add(data);
            }
            Destroy(rm.gameObject); // xoá RoomFloor_<roomId> (mesh phòng)
        }

        // Xoá TẤT CẢ floor mesh GO (Floor_<id>) — do FloorMeshController tạo, tag = "RoomFloor"
        var allFloorGOs = GameObject.FindGameObjectsWithTag("RoomFloor");
        foreach (var go in allFloorGOs)
            if (go) Destroy(go);

        // Xoá mọi FloorVis_<id> (preview/handles/labels)
        var allTransforms = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var tr = allTransforms[i];
            if (tr && tr.name.StartsWith("FloorVis_"))
                Destroy(tr.gameObject);
        }

        // Xoá dữ liệu
        RoomStorage.rooms.Clear();
        FloorStorage.floors?.Clear();

        // Dọn checkpoints - line
        if (checkpointManager != null)
        {
            foreach (var loop in checkpointManager.AllCheckpoints)
                foreach (var cp in loop)
                    if (cp) Destroy(cp);

            checkpointManager.AllCheckpoints.Clear();
            checkpointManager.DeleteCurrentDrawingData();
            checkpointManager.ClearAllLines();
        }

        //Reset và Undo
        Debug.Log("Đã xoá toàn bộ Floor, Room, checkpoint, mesh, line!");
        if (drawingTool != null) drawingTool.currentLineType = LineType.Wall;

        if (isCreateCommand)
        {
            var cmd = new DeleteAllRoomCommand(deleteAllRoomDataList);
            cmd.ClearAllRoom = this;
            //UndoRedoController.Instance.AddToUndo(cmd);
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
