using UnityEngine;

public class FurnitureHandler : MonoBehaviour
{
    public static FurnitureHandler Instance;
    public FurnitureItem currentItem;

    private void Awake()
    {
        Instance = this;
    }

    public void Select(FurnitureItem item)
    {
        currentItem = item;
    }
}