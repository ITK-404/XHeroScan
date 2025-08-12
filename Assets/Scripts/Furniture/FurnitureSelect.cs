using UnityEngine;

public class FurnitureSelect : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;

    private void OnMouseDown()
    {
        FurnitureManager.Instance.SelectFurniture(furnitureItem);
    }
}