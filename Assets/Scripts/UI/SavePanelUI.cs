using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SavePanelUI : MonoBehaviour
{
    [SerializeField] private Button closeBtn;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private TMP_InputField fileNameInputField;

    [SerializeField] private GameObject savePanelContainer;
    private void Awake()
    {
        closeBtn.onClick.AddListener(Close);
        // confirmBtn.onClick.AddListener(() => Show());
        confirmBtn.onClick.AddListener(() => Confirm());
        Close();
        // successPopup.gameObject.SetActive(false);
        // failedPopup.gameObject.SetActive(false);
    }

    private void Close()
    {
        // savePanelContainer.gameObject.SetActive(false);
        BackgroundUI.Instance.Hide();
        savePanelContainer.GetComponent<BottomSheetUI>().Close();
        
    }

    public void Show()
    {
        Debug.Log("Is File loaded: "+SaveLoadManager.IsFileLoaded());
        if (SaveLoadManager.IsFileLoaded())
        {
            ShowPopup(MessageLog.SuccessMessage_ExportFileComplete, ModularPopup.PopupAsset.toastPopupComplete);
            SaveLoadManager.Save();
            return;
        }
        
        EventSystem.current.SetSelectedGameObject(fileNameInputField.gameObject);
        fileNameInputField.OnPointerClick(new PointerEventData(EventSystem.current));
        // BackgroundUI.Instance.Show(transform.gameObject, Close);
        // savePanelContainer.gameObject.SetActive(true);
        savePanelContainer.GetComponent<BottomSheetUI>().Open();
    }


    private void Confirm()
    {
        string fileName = fileNameInputField.text;

        bool isFileNameEmpty = string.IsNullOrEmpty(fileName);
        bool isFileExit = SaveLoadManager.DoesNameExist(fileName);

        if (isFileNameEmpty)
        {
            ShowPopup(MessageLog.ErrorMessage_FileNameEmpty, ModularPopup.PopupAsset.toastPopupError);
            return;
        }

        if (isFileExit)
        {
            ShowPopup(MessageLog.ErrorMessage_FileNameExit, ModularPopup.PopupAsset.toastPopupError);
            return;
        }

        ShowPopup(MessageLog.SuccessMessage_ExportFileComplete, ModularPopup.PopupAsset.toastPopupComplete);
        Close();
        
        EventSystem.current.SetSelectedGameObject(null);
        SaveLoadManager.Save(fileName);
    }

    private void ShowPopup(string description, GameObject popupPrefab)
    {
        StartCoroutine(Delay(description, popupPrefab));
        // successPopup.GetComponent<ToastUI>().DescriptionText = description;
        // successPopup.gameObject.SetActive(true);
    }

    private IEnumerator Delay(string description, GameObject popupPrefab)
    {
        yield return new WaitForSeconds(0.1f);

        var popup = ToastManager.Spawn(description, popupPrefab);
        popup.SetParent(transform, savePanelContainer.transform.GetSiblingIndex() + 1);


        // var popup = Instantiate(popupPrefab).GetComponent<ModularPopup>();
        // popup.AutoFindCanvasAndSetup();
        // popup.SetParent(transform, savePanelContainer.transform.GetSiblingIndex() + 1);
        // popup.Description = description;
        // popup.AutoDestruct();
    }
}

public class MessageLog
{
    public const string ErrorMessage_FileNameEmpty = "Tên file đang bị để trống";
    public const string ErrorMessage_FileNameExit = "Tên file đã tồn tại, vui lòng chọn tên khác";
    public const string SuccessMessage_ExportFileComplete = "Bạn đã lưu bản vẽ thành công";
}