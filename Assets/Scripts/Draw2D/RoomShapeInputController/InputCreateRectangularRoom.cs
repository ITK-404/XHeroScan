using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InputCreateRectangularRoom : MonoBehaviour
{
    private const string WidthErrorLog = "Chiều rộng cạnh không hợp lệ! (>0)";
    private const string HeightErrorLog = "Chiều dài cạnh không hợp lệ! (>0)";
    
    [Header("UI References")]
    [SerializeField] private GameObject lengthInputField;   // Chiều dài cạnh (m)
    [SerializeField] private GameObject widthInputField;  // Chiều rộng cạnh (m)
    [SerializeField] private GameObject targetPanel; // Panel đóng
    [SerializeField] private Button createButton;
    [SerializeField] private Toggle deleteDataToggle;
    [Header("References")]
    [SerializeField] private CheckpointManager checkpointManager; // Script vẽ
    [SerializeField] private PanelToggleController panelToggleController;


    private TMP_InputField _lengthInputField;
    private TMP_InputField _widthInputField;

    private const int LIMIT_CHARACTER_COUNT = 9;
    private bool saveInputForNextTime = false;

    private void Awake()
    {
        _lengthInputField = lengthInputField.GetComponentInChildren<TMP_InputField>();
        _widthInputField = widthInputField.GetComponentInChildren<TMP_InputField>();

        _lengthInputField.characterLimit = LIMIT_CHARACTER_COUNT;
        _widthInputField.characterLimit = LIMIT_CHARACTER_COUNT;
        
        deleteDataToggle.SetIsOnWithoutNotify(saveInputForNextTime);
        deleteDataToggle.onValueChanged.AddListener((state) =>
        {
            saveInputForNextTime = state;
        });
    }

    void Start()
    {
        if (createButton != null)
            createButton.onClick.AddListener(OnCreateRoomClicked);
        else
            Debug.LogError("Chưa gán CreateButton!");

        panelToggleController = GetComponent<PanelToggleController>();
        // Tự động focus vào chiều dài sau 1 frame
    }



    void OnCreateRoomClicked()
    {
        if (checkpointManager == null)
        {
            Debug.LogError("CheckpointManager chưa gán!");
            return;
        }

        // === Lấy chiều dài ===
        float length = TryGetInput(_lengthInputField, WidthErrorLog);
        // === Lấy chiều rộng ===
        float width = TryGetInput(_widthInputField, HeightErrorLog);
        
        // === Truyền camera (nếu chưa gán sẵn) ===
        if (checkpointManager.drawingCamera == null)
            checkpointManager.drawingCamera = Camera.main;

        // === Tạo Room hình chữ nhật ===
        Debug.Log($"[RoomShapeInputController] Gửi yêu cầu tạo Room hình chữ nhật chiều dài {length}m , cạnh rộng {width}m");
        
        CreateRectangleRoom(length, width);

        if (!saveInputForNextTime)
        {
            _lengthInputField.text = "0";
            _widthInputField.text = "0";
        }
        
        panelToggleController.Show(false);
        SaveLoadManager.MakeDirty();
    }

    private float TryGetInput(TMP_InputField inputField, string errorLog)
    {
        float value = 0;
        if (!inputField || !float.TryParse(inputField.text, out value) || value <= 0)
        {
            Debug.LogWarning(HeightErrorLog);
            ShowInformationToast(errorLog);
        }

        return value;
    }

    private void ShowInformationToast(string descriptionText)
    {
        var popup = Instantiate(ModularPopup.PopupAsset.toastPopupError).GetComponent<ModularPopup>();
        popup.Description = descriptionText;
        popup.AutoFindCanvasAndSetup();
        popup.SetParent(targetPanel.transform.parent,targetPanel.transform.GetSiblingIndex() + 1);
        popup.AutoDestruct(2f);
    }

    public void CreateRectangleRoom(float width, float height)
    {
        if (width <= 0 || height <= 0)
        {
            Debug.LogError("Chiều dài và chiều rộng phải > 0");
            return;
        }

        // Xoá dữ liệu tạm nếu đang vẽ dở

        // Tìm center trên mặt phẳng y=0 theo camera
        Camera cam = checkpointManager.drawingCamera != null ? checkpointManager.drawingCamera : Camera.main;
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float enter = 0;
        Vector3 center = Vector3.zero;

        if (groundPlane.Raycast(ray, out enter))
        {
            center = ray.GetPoint(enter);
        }
        else
        {
            Debug.LogError("Không raycast được xuống mặt phẳng y=0");
            return;
        }

        Debug.Log($"Tâm room (rectangle) tại: {center}");

        // Tính 4 đỉnh hình chữ nhật quanh center
        checkpointManager.CreateRectangleRoom(width, height, center,null,true);
    }
}