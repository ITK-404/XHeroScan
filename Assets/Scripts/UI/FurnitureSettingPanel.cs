
using UnityEngine;

public class FurnitureSettingPanel : SettingPanel
{
    [SerializeField] private ToggleFlipGroup toggleFlipGroup;
    private void OnEnable()
    {
        // checking right here
        OnChecking();
    }

    private void OnChecking()
    {
        if(parameterInputFields.Count == 0)
        {
            return;
        }

        var currentFurniture = FurnitureManager.Instance.CurrentFurnitureItem();

        if(currentFurniture == null)
        {
            return;
        }

        var inputHeight = GetParameterInputField(IntParameterType.Height);
        inputHeight.gameObject.SetActive(false);

        bool canShowFlipBtn = currentFurniture.lineType == LineType.Door &&
                              currentFurniture.furnitureMergeToWall.IsInWall();
        toggleFlipGroup.gameObject.SetActive(canShowFlipBtn);
        
        var heightFromGround = GetParameterInputField(IntParameterType.DistanceFromGround);
        heightFromGround.gameObject.SetActive( currentFurniture.lineType != LineType.Door);

        int childCount = contentParent.transform.childCount;
        toggleFlipGroup.transform.SetSiblingIndex(childCount);
    }
}
