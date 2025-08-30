using UnityEngine;
using UnityEngine.UI;

public class SpawnFurnitureButton : MonoBehaviour
{
    [SerializeField] string ItemID;

    private Button btn;

    private void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(SpawnOnClick);
    }

    private void OnDestroy()
    {
        btn.onClick.RemoveListener(SpawnOnClick);
    }

    private void SpawnOnClick()
    {
        FurnitureManager.Instance.SpawnFurnitureCenterScreen(ItemID);
    }
}