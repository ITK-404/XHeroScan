using UnityEngine;

public class FurnitureVisible : MonoBehaviour
{
    private FurnitureItem furnitureItem;

    private void Awake()
    {
        furnitureItem = GetComponent<FurnitureItem>();
    }

    public void Show(bool state)
    {
        furnitureItem.modelContainer.gameObject.SetActive(state);
    }

    public string GetRoomID()
    {
        return furnitureItem.data.roomID;
    }

}

