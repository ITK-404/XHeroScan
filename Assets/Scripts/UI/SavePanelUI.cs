using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SavePanelUI : MonoBehaviour
{
    // TODO: Thêm tính năng valid input trong tương lai cho file name

    private const string ErrorMessage_FileNameEmpty = "Tên file đang bị để trống";
    private const string ErrorMessage_FileNameExit = "Tên file đã tồn tại, vui lòng chọn tên khác";
    private const string SuccessMessage_ExportFileComplete = "Bạn đã lưu bản vẽ thành công";

    [SerializeField] private Button closeBtn;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private TMP_InputField fileNameInputField;

    [SerializeField] private GameObject savePanelContainer;

    private void Awake()
    {
        closeBtn.onClick.AddListener(Close);
        confirmBtn.onClick.AddListener(() => Show());
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
            ShowPopup(ErrorMessage_FileNameEmpty, ModularPopup.PopupAsset.toastPopupError);
            return;
        }

        if (isFileExit)
        {
            ShowPopup(ErrorMessage_FileNameExit, ModularPopup.PopupAsset.toastPopupError);
            return;
        }

        ShowPopup(SuccessMessage_ExportFileComplete, ModularPopup.PopupAsset.toastPopupComplete);
        SaveLoadManager.Save(fileName);
        Close();
        
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void ShowPopup(string description, GameObject popupPrefab)
    {
        StartCoroutine(Delay(description, popupPrefab));
        // successPopup.GetComponent<ToastUI>().DescriptionText = description;
        // successPopup.gameObject.SetActive(true);
    }

    private IEnumerator Delay(string description, GameObject popupPrefab)
    {
        yield return new WaitForSeconds(0.2f);
        var popup = Instantiate(popupPrefab).GetComponent<ModularPopup>();
        popup.AutoFindCanvasAndSetup();
        popup.SetParent(transform, savePanelContainer.transform.GetSiblingIndex() + 1);
        popup.Description = description;
        popup.AutoDestruct(2f);
    }
}