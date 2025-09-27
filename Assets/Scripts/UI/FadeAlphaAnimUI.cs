using DG.Tweening;
using UnityEngine;

public class FadeAlphaAnimUI : BaseAnimUI
{
    protected override void OnValidate()
    {
        base.OnValidate();
        if(canvasGroup == null)
        {
            canvasGroup = container.AddComponent<CanvasGroup>();
        }
    }
    public override void Close()
    {
        canvasGroup.blocksRaycasts = false;

        canvasGroup.DOKill();
        canvasGroup.DOFade(0, hideDuration).SetEase(hideEase).OnComplete(() =>
        {
            canvasGroup.blocksRaycasts = true;
            container.gameObject.SetActive(false);
        });
    }

    public override void Open()
    {
        container.gameObject.SetActive(true);
        canvasGroup.DOKill();
        canvasGroup.DOFade(1, openDuration).SetEase(showEase);
    }
}