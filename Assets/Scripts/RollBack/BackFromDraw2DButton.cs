using UnityEngine;
using UnityEngine.UI;

public class BackFromDraw2DButton : MonoBehaviour
{
    [SerializeField] private Button backButton;
    [SerializeField] private SavePanelUI savePanel;
    
    private void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(CheckLogicExit);
        }
    }

    private void CheckLogicExit()
    {
        bool isFileLoadedAndIsDirty = SaveLoadManager.IsFileLoaded() && !SaveLoadManager.IsDirty();
        bool isFileNotLoadAndRoomIsEmpty = !SaveLoadManager.IsFileLoaded() && RoomStorage.rooms.Count == 0;
        Debug.Log("is File Loaded And Is Dirty" + isFileLoadedAndIsDirty);
        Debug.Log("is File Not Load And Room Is Empty" + isFileNotLoadAndRoomIsEmpty);
        if (isFileLoadedAndIsDirty || isFileNotLoadAndRoomIsEmpty)
        {
            ExitDraw2D();
            return;
        }

        var popup = Instantiate(ModularPopup.PopupAsset.saveBeforeLeftPopupWarning).GetComponent<ModularPopup>();
        popup.AutoFindCanvasAndSetup();
        popup.Header = "Bạn có muốn thoát khỏi chế độ vẽ 2D?";
        popup.ClickYesEvent = OpenSavePanel;
        popup.ClickNoEvent = ExitDraw2D;
        popup.autoClearWhenClickYes = true;

        BackgroundUI.Instance.Show(popup.gameObject, () =>
        {
            popup.AutoDestruct(0);
        });

    }

    private void ExitDraw2D()
    {
        BackButton.OnClickYes();
        SceneHistoryManager.LoadPreviousScene();
        SaveLoadManager.Clear();
    }

    private void OpenSavePanel()
    {
        savePanel.Show();
    }
}