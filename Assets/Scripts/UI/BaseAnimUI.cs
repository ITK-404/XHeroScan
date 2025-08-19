using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class BaseAnimUI : MonoBehaviour
{
    [SerializeField] protected GameObject container;
    protected RectTransform rectContainer;
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
        rectContainer = container.gameObject.GetComponent<RectTransform>();
    }

    private void OnValidate()
    {
        if (container == null)
        {
            container = transform.GetChild(0).gameObject;
        }
    }

    public abstract void Open();
    public abstract void Close();
}