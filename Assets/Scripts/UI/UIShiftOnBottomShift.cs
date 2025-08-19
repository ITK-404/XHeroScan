using DG.Tweening;
using UnityEngine;

public class UIShiftOnBottomShift : MonoBehaviour
{
    [SerializeField] private BottomSheetUI bottomSheetUI;
    [SerializeField] private RectTransform itemShift;

    [SerializeField] private RectTransform openPosition;
    [SerializeField] private RectTransform closePosition;

    private Ease openEase = Ease.OutQuart;
    private Ease closeEase = Ease.InOutQuad;

    private Tween currentTween;
    
    private void Awake()
    {
        bottomSheetUI.OnStartShowAnim += OnOpenEvent;
        bottomSheetUI.OnEndHideAnim += OnCloseEvent;

        itemShift.anchoredPosition = closePosition.anchoredPosition;
    }

    private void OnDestroy()
    {
        bottomSheetUI.OnStartShowAnim -= OnOpenEvent;
        bottomSheetUI.OnEndHideAnim -= OnCloseEvent;
    }

    private void OnCloseEvent()
    {
        currentTween?.Kill();
        
        currentTween = itemShift.DOAnchorPos(closePosition.anchoredPosition, 0.2f).SetEase(closeEase);
    }

    private void OnOpenEvent()
    {
        currentTween?.Kill();
 
        currentTween = itemShift.DOAnchorPos(openPosition.anchoredPosition, 0.2f).SetEase(openEase);
    }
}