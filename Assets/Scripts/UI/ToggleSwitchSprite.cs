using UnityEngine;
using UnityEngine.UI;

public class ToggleSwitchSprite : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Sprite toggleOnSprite;
    [SerializeField] private Sprite toggleOffSprite;
    
    private ToggleSwitch toggle;
    
    private void Awake()
    {
        toggle = GetComponent<ToggleSwitch>();
        toggle.OnToggleChanged.AddListener(SetState);
    }

    private void OnDestroy()
    {
        toggle.OnToggleChanged.RemoveListener(SetState);
    }

    private void SetState(bool isActive)
    {
        if (image == null)
        {
            Debug.Log("Image is null",gameObject);
            return;
        }

        if (!toggleOnSprite || !toggleOffSprite)
        {
            Debug.Log("Toggle Sprite is null");
            return;
        }
        
        var sprite = isActive ? toggleOnSprite : toggleOffSprite;
        image.sprite = sprite;
    }
}