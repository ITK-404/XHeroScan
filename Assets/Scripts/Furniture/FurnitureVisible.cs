using UnityEngine;

public class FurnitureVisible : MonoBehaviour
{
    private FurnitureItem furnitureItem;
    private bool currentState;
    private Transform textContainer;
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
        if(currentState == false)
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

        textContainer.gameObject.SetActive(currentState);
    }

    private void ResetState()
    {
        Show(!currentState);
        furnitureItem.data.roomID = "";
    }
}

