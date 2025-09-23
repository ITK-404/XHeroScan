using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

public class LoadDraftingSceneButton : MonoBehaviour
{
    private Button btn;
    private bool protectClick = false;
    private void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClickChangeScene);
    }

    private void OnDestroy()
    {
        btn.onClick.RemoveListener(OnClickChangeScene);
    }

    private void OnClickChangeScene()
    {
        if (protectClick) return;
        protectClick = true;
        var canvas = GameObject.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var item in canvas)
        {
            if (!item.TryGetComponent(out CanvasGroup canvasGroup))
            {
                canvasGroup = item.AddComponent<CanvasGroup>();
                canvasGroup.blocksRaycasts = false;
            }
        }
        SceneManager.LoadScene("DraftingScene");
    }
}