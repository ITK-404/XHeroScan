using UnityEngine;

public class ToggleFlipWindowDoor : MonoBehaviour
{
    private ToggleSwitch toggleSwitch;
    private FurnitureItem furnitureItem => FurnitureManager.Instance.CurrentFurnitureItem();
    private void Awake()
    {
        toggleSwitch = GetComponent<ToggleSwitch>();
        toggleSwitch.OnToggleChanged.AddListener(ToggleChanged);
    }

    private void ToggleChanged(bool state)
    {
        if (furnitureItem == null) return;
        furnitureItem.furnitureMergeToWall.isFlip = state;
    }

    public void SelectFurniture()
    {
        toggleSwitch.ToggleWithoutAnimation(furnitureItem.furnitureMergeToWall.isFlip);
        toggleSwitch.gameObject.SetActive(true);
    }

    public void DeSelectect()
    {
        toggleSwitch.gameObject.SetActive(false);
    }
}