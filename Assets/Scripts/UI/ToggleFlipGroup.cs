
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
            currentFurniture.data.isFlipVertical = !currentFurniture.data.isFlipVertical;
            currentFurniture.RefreshRotation();
        }
    }

    private void FlipVertical()
    {
        if (currentFurniture != null)
        {
            currentFurniture.data.isFlipHorizontal = !currentFurniture.data.isFlipHorizontal;
            currentFurniture.RefreshRotation();
        }
    }
}