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
            backButton.onClick.AddListener(ShowPopupPrefab);
        }
    }
    
    private void ShowPopupPrefab()
    {
        if(RoomStorage.rooms.Count == 0)
        {
            BackButton.OnClickYes();
            SceneHistoryManager.LoadPreviousScene();
            return;
        }
        
        var popup = Instantiate(ModularPopup.PopupAsset.saveBeforeLeftPopupWarning).GetComponent<ModularPopup>();
        popup.AutoFindCanvasAndSetup();
        popup.Header = "Bạn có muốn thoát khỏi chế độ vẽ 2D?";
        popup.ClickYesEvent = OpenSavePanel;
   
        popup.autoClearWhenClick = true;
    }

    private void OpenSavePanel()
    {
        savePanel.Show();
    }
}