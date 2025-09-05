using UnityEngine;

public class FurnitureVisible : MonoBehaviour
{
    private FurnitureItem furnitureItem;
    private bool currentState;
    private void Awake()
    {
        furnitureItem = GetComponent<FurnitureItem>();
    }

    public void Show(bool state)
    {
        currentState = state;
        furnitureItem.modelContainer.gameObject.SetActive(state);
    }

    public string GetRoomID()
    {
        return furnitureItem.data.roomID;
    }

    private void Update()
    {
        if (furnitureItem.lineType == LineType.None)
        {
            if (currentState == false)
            {
                var room = RoomStorage.GetRoomByID(furnitureItem.data.roomID);
                if (room == null) return;
                var worldPosition = new Vector2(furnitureItem.GetWorldPosition().x, furnitureItem.GetWorldPosition().z);
                if (!CheckpointManager.IsPointInPolygon(worldPosition, room.checkpoints))
                {
                    Show(!currentState);
                    furnitureItem.data.roomID = "";
                }
            }
        }

    }

}

