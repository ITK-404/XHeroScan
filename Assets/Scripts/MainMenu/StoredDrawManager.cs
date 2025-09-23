using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoredDrawManager : MonoBehaviour
{
    public static StoredDrawManager Instance;
    [SerializeField] private TMP_InputField fileNameInputField;
    [SerializeField] private Transform changeNamePanel;
    [SerializeField] private Transform deletePanel;

    [SerializeField] private Button changeNameConfirmBtn;
    [SerializeField] private Button deleteButton;

    [Header("Test delete file ")]
    [SerializeField] private LoadFile loadFile;

    [SerializeField] private StoredDrawEmptyPanel emptyPanel;

    private string currentFileName;

    private void Awake()
    {
        Instance = this;
        changeNameConfirmBtn.onClick.AddListener(OnConfirmChangeFileName);
        deleteButton.onClick.AddListener(OnConfirmDeleteFile);
    }

    private void OnDestroy()
    {
        changeNameConfirmBtn.onClick.RemoveListener(OnConfirmChangeFileName);
        deleteButton.onClick.RemoveListener(OnConfirmDeleteFile);
    }

    public void ShowChangeNamePanel(StoredDrawUI storedDrawUI)
    {
        currentFileName = storedDrawUI.fileName;

        fileNameInputField.text = storedDrawUI.fileName;
        changeNamePanel.gameObject.SetActive(true);
        fileNameInputField.Select();
    }

    private void OnConfirmChangeFileName()
    {
        string newFileName = fileNameInputField.text;
        bool isFileNameEmpty = string.IsNullOrEmpty(newFileName);
        bool isFileExist = SaveLoadManager.DoesNameExist(newFileName);
        if (isFileNameEmpty)
        {
            ShowPopup(MessageLog.ErrorMessage_FileNameEmpty, ModularPopup.PopupAsset.toastPopupError);
            return;
        }

        if (isFileExist)
        {
            ShowPopup(MessageLog.ErrorMessage_FileNameExit, ModularPopup.PopupAsset.toastPopupError);
            return;
        }

        if (!SaveLoadManager.ChangeFileName(currentFileName, newFileName))
        {
            ShowPopup(MessageLog.ErrorMessage_UnknowError, ModularPopup.PopupAsset.toastPopupError);
        }

        loadFile.LoadAllSavedFiles();
        changeNamePanel.gameObject.SetActive(false);
        fileNameInputField.text = "";
        ShowPopup(MessageLog.SuccessMessage_ChangeFileNameComplete, ModularPopup.PopupAsset.toastPopupComplete);
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
        popup.SetParent(transform, transform.GetSiblingIndex() + 1);


    }

    private void OnConfirmDeleteFile()
    {
        if (SaveLoadManager.TryDeleteFile(currentFileName))
        {
            loadFile.LoadAllSavedFiles();
            deletePanel.gameObject.SetActive(false);
            emptyPanel.Refresh();
            ShowPopup(MessageLog.SuccessMessage_DeleteFileComplete, ModularPopup.PopupAsset.toastPopupComplete);

        }
    }

    public void ShowDeletePanel(StoredDrawUI storedDrawUI)
    {
        currentFileName = storedDrawUI.fileName;
        deletePanel.gameObject.SetActive(true);
    }

// #if UNITY_EDITOR
//     [Header("Test change file Name")]
//     [SerializeField] private string testChangeFileName;
//     [SerializeField] private string testNewFileName;
//     [SerializeField] private string fileNameTest;
//     private void Update()
//     {
//         if (Input.GetKeyDown(KeyCode.B))
//         {
//             if (SaveLoadManager.TryDeleteFile(fileNameTest))
//             {
//                 loadFile.LoadAllSavedFiles();
//             }
//         }
//
//         if (Input.GetKeyDown(KeyCode.K))
//         {
//             SaveLoadManager.ChangeFileName(testChangeFileName,testNewFileName);
//             loadFile.LoadAllSavedFiles();
//         }
//     }
//     #endif
}