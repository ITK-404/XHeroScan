using UnityEngine;
using UnityEngine.UI;

public class CreateRoomBtn : MonoBehaviour
{
    [SerializeField] private Button btn;
    private void Awake()
    {
        
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        CreateRoomOnFloor.OnClickCreateRoomEvent?.Invoke();
    }
}
