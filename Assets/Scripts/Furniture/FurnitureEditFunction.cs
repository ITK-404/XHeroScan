using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FurnitureEditFunction : MonoBehaviour
{
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
    private void Start()
    {
        furnitureManager = FurnitureManager.Instance;

        var elementHandler = bottomSheetUI.GetComponent<ElementUIHandler>();
        buttonOk = elementHandler.GetItemByFiler<Button>("okBtn");
        thicknessInputField = elementHandler.GetItemByFiler<TMP_InputField>("thickness");
        widthInputField = elementHandler.GetItemByFiler<TMP_InputField>("width");
        lengthInputField = elementHandler.GetItemByFiler<TMP_InputField>("length");
        
        buttonOk.onClick.AddListener(OnChangeSize);

        var popupEdit = currentPopup.GetComponent<ModularPopupEdit>();
        popupEdit.deleteBtn.onClick.AddListener(DeleteFurniture);
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

            currentFurniture.data.size.width = TryParse(widthInputField, data.size.width);
            currentFurniture.data.size.length = TryParse(lengthInputField, data.size.length);
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
            FurnitureItem item = furnitureManager.CurrentFurnitureItem();
            Vector3 worldPositon = item.GetWorldPosition();

            float heightOffsetZ = item.GetHeightOffset() / 2;
            float finalZPosition = offsetZ + worldPositon.z + heightOffsetZ + heightOffsetZ * 0.2f;
            
            Vector3 standPosition = new Vector3(worldPositon.x + offsetX, currentPopup.transform.position.y, finalZPosition);
            currentPopup.transform.position = standPosition;
            // maybe create world space canvas
        }
        currentPopup.gameObject.SetActive(furnitureManager.CurrentFurnitureItem());
    }
}