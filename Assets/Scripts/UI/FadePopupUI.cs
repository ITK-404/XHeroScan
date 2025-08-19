using System;
using DG.Tweening;
using UnityEngine;

public class FadePopupUI : MonoBehaviour
{
    [SerializeField] private GameObject container;
    [SerializeField] private CanvasGroup canvasGroup;

    private Ease fadeIn = Ease.OutCubic;
    private Ease fadeOut = Ease.InCubic;

    private Tween currentTween;
    
    private void Awake()
    {
        canvasGroup = container.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = container.AddComponent<CanvasGroup>();
        }
        container.gameObject.SetActive(false);
        canvasGroup.alpha = 0;
    }

    public void Open()
    {
        container.gameObject.SetActive(true);
        Fade(1, 0.15f, fadeIn);
    }

    public void Close()
    {
        Fade(0, 0.15f, fadeOut, () => { container.gameObject.SetActive(false); });
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

public class BaseAnimUI : MonoBehaviour
{
    [SerializeField] protected GameObject container;
    [Header("Animation Settings")]
    [SerializeField] protected Ease showEase = Ease.InOutSine;
    [SerializeField] protected Ease hideEase = Ease.InOutSine;
    [SerializeField] protected float openDuration = 0.1f;
    [SerializeField] protected float hideDuration = 0.1f;

    protected Tween currentTween;
    protected CanvasGroup canvasGroup;
    

    public Action OnStartShowAnim;
    public Action OnEndHideAnim;

    protected virtual void Awake()
    {
        canvasGroup = container.gameObject.GetComponent<CanvasGroup>();
    }
}