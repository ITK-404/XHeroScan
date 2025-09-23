using UnityEngine;
using UnityEngine.SceneManagement;

public class StoredDrawEmptyPanel : MonoBehaviour
{
    [SerializeField] private GameObject emptyPanel;
    private void Awake()
    {
        emptyPanel.gameObject.SetActive(false);
    }
    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (SaveLoadManager.IsContainSaveFileLocal())
        {
            emptyPanel.gameObject.SetActive(false);
        }
        else
        {
            emptyPanel.gameObject.SetActive(true);
        }
    }
}
