using UnityEngine;

public class OpenBackground : MonoBehaviour
{
    [SerializeField] private float backgroundAlpha = 0.7f;
    public void Open(GameObject target)
    {
        BackgroundUI.Instance.Show(target, null);
        BackgroundUI.Instance.SetBackgroundAlpha(backgroundAlpha);
    }
}