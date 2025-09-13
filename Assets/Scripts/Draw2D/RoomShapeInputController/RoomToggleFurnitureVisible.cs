using UnityEngine;

public class RoomToggleFurnitureVisible : MonoBehaviour
{
    private ToggleSwitch toggleSwitch;
    private string roomID;

    private void Awake()
    {
        toggleSwitch = GetComponent<ToggleSwitch>();
        toggleSwitch.OnToggleChanged.AddListener(ToggleChanged);
    }

    private void ToggleChanged(bool state)
    {
        if (string.IsNullOrEmpty(roomID)) return;

        var room = RoomStorage.GetRoomByID(roomID);
        if (room == null) return;
        room.isShowFurniture = state;

        FurnitureManager.Instance.SetVisibleObjects(roomID, room.isShowFurniture);
    }

    public void SelectRoom(string roomID)
    {
        var room = RoomStorage.GetRoomByID(roomID);
        this.roomID = "";
        toggleSwitch.ToggleWithoutAnimation(room.isShowFurniture);
        this.roomID = roomID;
        toggleSwitch.gameObject.SetActive(true);
    }

    public void DeSelectect()
    {
        this.roomID = "";
        toggleSwitch.gameObject.SetActive(false);
    }

}
