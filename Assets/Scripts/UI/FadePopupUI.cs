using System;
using DG.Tweening;
using UnityEngine;

public class FadePopupUI : BaseAnimUI
{

    protected override void Awake()
    {
        base.Awake();
        container.gameObject.SetActive(false);
        canvasGroup.alpha = 0;
    }

    public override void Open()
    {
        container.gameObject.SetActive(true);
        Fade(1, openDuration,showEase);
    }

    public override void Close()
    {
        Fade(0, hideDuration, hideEase, () => { container.gameObject.SetActive(false); });
    }

    private void Fade(float value, float duration, Ease ease, Action playDoneCallback = null)
    {
        if (canvasGroup == null)
        {
            Debug.Log("Canvas group is null, cannot using fade",gameObject);
            return;
        }
        currentTween?.Kill();
        currentTween = canvasGroup.DOFade(value, duration).SetEase(ease).OnComplete(() => { playDoneCallback?.Invoke(); });
    }
}