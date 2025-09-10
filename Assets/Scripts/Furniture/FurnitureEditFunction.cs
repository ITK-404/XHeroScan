using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class FurnitureEditFunction : MonoBehaviour
{
    public ToggleFlipWindowDoor flipToggle;


    [SerializeField] private GameObject currentPopup;
    [SerializeField] private BottomSheetUI bottomSheetUI;
    [SerializeField] private float offsetX;
    [SerializeField] private float offsetZ;
    private FurnitureManager furnitureManager;

    private FurnitureItem currentFurniture => furnitureManager.CurrentFurnitureItem();

    private TMP_InputField thicknessInputField;
    private TMP_InputField widthInputField;
    private TMP_InputField lengthInputField;
    private Button buttonOk;

    private ModularPopupEdit popupEdit;
    private void Start()
    {
        furnitureManager = FurnitureManager.Instance;

        var elementHandler = bottomSheetUI.GetComponent<ElementUIHandler>();
        buttonOk = elementHandler.GetItemByFiler<Button>("okBtn");
        thicknessInputField = elementHandler.GetItemByFiler<TMP_InputField>("thickness");
        widthInputField = elementHandler.GetItemByFiler<TMP_InputField>("width");
        lengthInputField = elementHandler.GetItemByFiler<TMP_InputField>("length");
        
        buttonOk.onClick.AddListener(OnChangeSize);

        popupEdit = currentPopup.GetComponent<ModularPopupEdit>();
        popupEdit.deleteBtn.onClick.AddListener(DeleteFurniture);
        popupEdit.doubleBtn.onClick.AddListener(DoubleFurniture);
        popupEdit.flipBtn.onClick.AddListener(FlipToggle);
    }

    private void FlipToggle()
    {
        if(currentFurniture != null)
        {
            currentFurniture.data.isFlip = !currentFurniture.data.isFlip;
        }
    }

    private void DoubleFurniture()
    {
        if(currentFurniture != null)
        {
            currentFurniture.InitClone();
        }
    }

    private void OnDestroy()
    {
        if (buttonOk)
        {
            buttonOk.onClick.RemoveListener(OnChangeSize);
        }
    }

    private void DeleteFurniture()
    {
        if(currentFurniture != null)
        {
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

            float width = TryParse(widthInputField, data.size.width);
            float length = TryParse(lengthInputField, data.size.length);
            float higherValue = 0;
            // Xử lý để furniture tạo thành hình vuông nếu
            // if (currentFurniture.alwayMakeSquare)
            {
                higherValue = Mathf.Max(width, length);
                width = length = higherValue;
            }

            currentFurniture.data.size.width = width;
            currentFurniture.data.size.length = length;
            currentFurniture.SyncWithBounds();
            currentFurniture.data.size.ClampSize();

            currentFurniture.RefreshCheckPointsByBounds();
        }
    }

    private float TryParse(TMP_InputField inputField, float defaultValue)
    {
        if(float.TryParse(inputField.text,out var result))
        {
            return result;
        }
        return defaultValue;
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
            
            popupEdit.flipBtn.gameObject.SetActive(currentFurniture.lineType == LineType.Door);
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
    }
}