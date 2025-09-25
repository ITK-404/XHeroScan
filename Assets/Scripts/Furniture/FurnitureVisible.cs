using UnityEngine;

public class FurnitureVisible : MonoBehaviour
{
    private FurnitureItem furnitureItem;
    private GameObject textContainer;
    private bool currentState;
    private void Awake()
    {
        furnitureItem = GetComponent<FurnitureItem>();
        textContainer = furnitureItem.textContainer;
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
        if(currentState == false || string.IsNullOrWhiteSpace(furnitureItem.data.roomID) == false)
        {
            var room = RoomStorage.GetRoomByID(furnitureItem.data.roomID);
            if (room == null)
            {
                ResetState();
                return;
            }
            if (furnitureItem.lineType == LineType.None)
            {
                var worldPosition = new Vector2(furnitureItem.GetWorldPosition().x, furnitureItem.GetWorldPosition().z);

                if (!CheckpointManager.IsPointInPolygon(worldPosition, room.checkpoints))
                {
                    ResetState();
                }

            }
        }

        if (textContainer == null) return;

        textContainer.gameObject.SetActive(currentState);
    }

    private void ResetState()
    {
        Show(!currentState);
        furnitureItem.data.roomID = "";
    }
}

