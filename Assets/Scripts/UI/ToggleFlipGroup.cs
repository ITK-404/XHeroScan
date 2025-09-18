
using System;
using UnityEngine;
using UnityEngine.UI;

public class ToggleFlipGroup : MonoBehaviour
{
    public Button toggleFlipHorizontal;
    public Button toggleFlipVertical;

    FurnitureItem currentFurniture => FurnitureManager.Instance.CurrentFurnitureItem();

    public void SetInteractable(bool isActive)
    {
        toggleFlipHorizontal.interactable = isActive;
        toggleFlipVertical.interactable = isActive;
    }

    private void Awake()
    {
        toggleFlipHorizontal.onClick.AddListener(FlipHorizonntal);
        toggleFlipVertical.onClick.AddListener(FlipVertical);
    }

    private void FlipHorizonntal()
    {
        if (currentFurniture != null)
        {
            FurnitureItem.SnapShotTemp = currentFurniture.data;
            currentFurniture.data.isFlipVertical = !currentFurniture.data.isFlipVertical;
            UpdateByData();
        }
    }

    private void FlipVertical()
    {
        if (currentFurniture != null)
        {
            FurnitureItem.SnapShotTemp = currentFurniture.data;
            currentFurniture.data.isFlipHorizontal = !currentFurniture.data.isFlipHorizontal;
            UpdateByData();

        }
    }

    private void UpdateByData()
    {
        currentFurniture.RefreshRotation();
        currentFurniture.RefreshCheckPointsByBounds();
        currentFurniture.CreareEditCommandBySnapShot();
    }
}