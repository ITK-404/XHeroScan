using UnityEngine;

public class AutoToggleBackground : MonoBehaviour
{
    // Easy conflict with each other, using carefully
    [SerializeField] private float backgroundAlpha = 0.7f;
    private void OnEnable()
    {
        BackgroundUI.Instance.Show(gameObject, null);
        BackgroundUI.Instance.SetBackgroundAlpha(backgroundAlpha);
    }

    private void OnDisable()
    {
        BackgroundUI.Instance.Hide();
    }
    
}