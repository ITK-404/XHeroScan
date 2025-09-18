using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class FurnitureEditFunction : MonoBehaviour
{
    public ToggleFlipWindowDoor flipToggle;

    [SerializeField] private TextMeshProUGUI popupName;
    [SerializeField] private GameObject currentPopup;
    [SerializeField] private BottomSheetUI bottomSheetUI;
    [SerializeField] private float offsetX;
    [SerializeField] private float offsetZ;
    private FurnitureManager furnitureManager;

    private FurnitureItem currentFurniture => furnitureManager.CurrentFurnitureItem();

    private ParameterInputField thicknessInputField;
    private ParameterInputField widthInputField;
    private ParameterInputField lengthInputField;
    private ParameterInputField distanceFromGroundInputField;

    private ModularPopupEdit popupEdit;
    private FloorSettingPanel settingPanel;
    private BottomSheetInputUI BottomSheetInputUI;

    private ToggleFlipGroup toggleFlipGroup;

    private void Start()
    {
        furnitureManager = FurnitureManager.Instance;

        settingPanel = bottomSheetUI.GetComponent<FloorSettingPanel>();
        BottomSheetInputUI = bottomSheetUI.GetComponent<BottomSheetInputUI>();
        toggleFlipGroup = settingPanel.toggleFlipGroup;
        
        thicknessInputField = settingPanel.GetParameterInputField(IntParameterType.Thickness);
        widthInputField = settingPanel.GetParameterInputField(IntParameterType.Width);
        lengthInputField = settingPanel.GetParameterInputField(IntParameterType.Height);
        distanceFromGroundInputField = settingPanel.GetParameterInputField(IntParameterType.DistanceFromGround);

        settingPanel.OnApplyAction += OnChangeSize;

        popupEdit = currentPopup.GetComponent<ModularPopupEdit>();
        popupEdit.editBtn.onClick.AddListener(() => BottomSheetInputUI.Open());
        popupEdit.deleteBtn.onClick.AddListener(DeleteFurniture);
        popupEdit.doubleBtn.onClick.AddListener(DoubleFurniture);

        bottomSheetUI.OnStartShowAnim.AddListener(OnRefreshValue);

    }

    private void OnDestroy()
    {
        if (settingPanel == null) return;
        settingPanel.OnApplyAction -= OnChangeSize;
        bottomSheetUI.OnStartShowAnim.RemoveListener(OnRefreshValue);
    }

    private void FlipToggle()
    {
        if(currentFurniture != null)
        {
            FurnitureItem.SnapShotTemp = currentFurniture.data;
            currentFurniture.data.isFlipVertical = !currentFurniture.data.isFlipVertical;
            currentFurniture.CreareEditCommandBySnapShot();
        }
    }

    private void DoubleFurniture()
    {
        if(currentFurniture != null)
        {
            var furniture = currentFurniture.InitClone();
            UndoRedoController.Instance.AddToUndo(new CreateItemCommand(furniture.data.instanceID));

        }
    }

    private void DeleteFurniture()
    {
        if(currentFurniture != null)
        {
            UndoRedoController.Instance.AddToUndo(new DeleteItemCommand(currentFurniture.data));
            currentFurniture.Destroy();
        }
    }

    private void OnChangeSize()
    {
        if (currentFurniture)
        {
            Debug.Log("On Change size of furniture item");
            // change size here
            var data = currentFurniture.data;
            FurnitureItem.SnapShotTemp = data;
            currentFurniture.CreareEditCommandBySnapShot();

            float width = TryParse(widthInputField.InputField);
            float length = TryParse(lengthInputField.InputField);
            float distanceFromGround = TryParse(distanceFromGroundInputField.InputField);
            float higherValue = 0;
            // Xử lý để furniture tạo thành hình vuông nếu
            Debug.Log($"Giá trị từ input field {width} {length}");

            currentFurniture.data.size.distanceFromGround = distanceFromGround;

            if (currentFurniture.alwayMakeSquare)
            {
                higherValue = Mathf.Max(width, length);
                width = length = higherValue;
            }
            Debug.Log($"Giá trị mới {width} {length}");
            currentFurniture.data.size.width = width;
            currentFurniture.data.size.length = length;
            currentFurniture.SyncWithBounds();
            currentFurniture.data.size.ClampSize();

            currentFurniture.RefreshCheckPointsByBounds();
            
            widthInputField.ResetValue();
            lengthInputField.ResetValue();
        }
    }

    private float TryParse(TMP_InputField inputField)
    {
        if(float.TryParse(inputField.text,out var result))
        {
            return result;
        }
        return default;
    }

    private void Update()
    {
        if (currentFurniture)
        {
            // show on it
            FurnitureItem item = currentFurniture;
            Vector3 worldPositon = item.GetWorldPosition();

            float heightOffsetZ = item.GetHeightOffset() / 2;
            float finalZPosition = offsetZ + worldPositon.z + heightOffsetZ + heightOffsetZ * 0.2f;
            
            Vector3 standPosition = new Vector3(worldPositon.x + offsetX, currentPopup.transform.position.y, finalZPosition);
            currentPopup.transform.position = standPosition;


            // handle when furniture is door

            // maybe create world space canvas
        }
        currentPopup.gameObject.SetActive(currentFurniture);

        //if (currentFurniture != null)
        //{
        //    var lineType = currentFurniture.lineType;
        //    if (lineType == LineType.Window || lineType == LineType.Door)
        //    {
        //        flipToggle.SelectFurniture();
        //    }
        //}
        //else
        //{
        //    flipToggle.DeSelectect();
        //}
        HandleInputFields();
    }

    private void HandleInputFields()
    {
        if(currentFurniture != null)
        {

            var lineType = currentFurniture.lineType;
            bool isNormalFurniture = currentFurniture.lineType == LineType.None;
            widthInputField.gameObject.SetActive(true);
            lengthInputField.gameObject.SetActive(isNormalFurniture);

            toggleFlipGroup.gameObject.SetActive(lineType == LineType.Door);
            //distanceFromGroundInputField.gameObject.SetActive(lineType == LineType.Window);

            //toggleFlipGroup.SetInteractable(currentFurniture.furnitureMergeToWall.IsInWall());
        
            if(currentFurniture.lineType == LineType.Door)
            {
                popupName.text = "thông số cửa";
            }
            else
            {
                popupName.text = "thông số cửa sổ";
            }
        }
    }

    private void OnRefreshValue()
    {
        Debug.Log("On Refresh Value");
        widthInputField.SetValue(currentFurniture.width);
        lengthInputField.SetValue(currentFurniture.length);
        distanceFromGroundInputField.SetValue(currentFurniture.data.size.distanceFromGround);
    }
}