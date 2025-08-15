using UnityEngine;
using UnityEngine.UI;

public class BackButton : MonoBehaviour
{
    public Button backButton;
    private static Canvas canvas;


    void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(() =>
            {
                if (isShow) return;
                isShow = true;
                Debug.Log("Button Back Clicked!");
                // ShowUnsavedDataPopup();
                ShowPopupPrefab();
            });
        }
        else
        {
            Debug.LogError("BackButton: Chưa gán Button!");
        }
    }

    private bool isShow = false;

    private void ShowPopupPrefab()
    {
        var popup = Instantiate(ModularPopup.PopupAsset.modularPopupYesNo).GetComponent<ModularPopup>();
        popup.AutoFindCanvasAndSetup();
        popup.Header = "Dữ liệu của bạn chưa được lưu!\nNếu thoát ra sẽ mất dữ liệu!";
        popup.ClickYesEvent = OnClickYes;
        popup.EventWhenClickButtons = () =>
        {
            // BackgroundUI.Instance.Hide();
            isShow = false;
        };
        popup.autoClearWhenClick = true;
        // BackgroundUI.Instance.Show(popup.gameObject, null);
    }

    public static void OnClickYes()
    {
        RoomStorage.rooms.Clear();
        FurnitureManager.tempSaveDataFurnitureDatas.Clear();
        SceneHistoryManager.LoadPreviousScene();
    }
}