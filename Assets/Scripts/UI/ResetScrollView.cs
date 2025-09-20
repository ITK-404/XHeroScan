using UnityEngine;
using UnityEngine.UI;

public class ResetScrollView : MonoBehaviour
{
    public ScrollRect scrollRect;

    private void OnEnable()
    {
        if(scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
        }
        scrollRect.verticalNormalizedPosition = 1f;
    }
}
