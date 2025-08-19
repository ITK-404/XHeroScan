using DG.Tweening;
using UnityEngine;

public class UIAnimationUltils

{
    public static void PopupScaleAnimation(GameObject popup, float duration = 0.2f)
    {
        if (popup == null) return;

        RectTransform rectTransform = popup.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        rectTransform.localScale = Vector3.zero;
        popup.SetActive(true);
        rectTransform.transform.DOScale(Vector3.one, duration).SetEase(Ease.OutBack);
    }
    
    public static void PopoutScaleAnimation(GameObject popup, float duration = 0.2f, bool isDestroyAfter = true)
    {
        if (popup == null) return;

        RectTransform rectTransform = popup.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        rectTransform.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack).OnComplete(() =>
        {
            if (isDestroyAfter)
            {
                GameObject.Destroy(popup.gameObject);
            }
            else
            {
                popup.SetActive(false);
            }
        });
    }
}