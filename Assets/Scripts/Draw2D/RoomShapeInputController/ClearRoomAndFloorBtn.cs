using UnityEngine;
using UnityEngine.UI;

public class ClearRoomAndFloorBtn : MonoBehaviour
{
    [SerializeField] private Button btn;
    ClearAllRoomsButton clearAllRoomsButton;
    private void Awake()
    {
        clearAllRoomsButton = FindFirstObjectByType<ClearAllRoomsButton>();
        if (btn) btn.onClick.AddListener(OnClearAllClicked);
    }

    private void OnClearAllClicked()
    {
        clearAllRoomsButton.OnClearAllClicked();
    }
}
