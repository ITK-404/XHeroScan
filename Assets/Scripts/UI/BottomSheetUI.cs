using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class BottomSheetUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] protected RectTransform sheet;

    [SerializeField] private float animDuration = 0.3f;
    [SerializeField] private float yOffset;

    [Header("Ease Settings")]
    [SerializeField] protected Ease openEase = Ease.OutQuart;

    [SerializeField] protected Ease closeEase = Ease.InOutQuad;

    protected Vector2 openPos;
    protected Vector2 closedPos;
    protected Vector2 keyboardOpenPos;

    protected Tweener currentTween;

    public UnityAction OnOpenEvent;
    public UnityAction OnCloseEvent;

    private void Start()
    {
        float height = sheet.rect.height;

        // Vị trí mở (ngay vị trí hiện tại)
        openPos = sheet.anchoredPosition;

        // Vị trí đóng (ẩn xuống dưới)
        closedPos = openPos + new Vector2(0, -(height + yOffset));

        // Khởi tạo ở trạng thái đóng
        sheet.anchoredPosition = closedPos;

        sheet.gameObject.SetActive(false);
    }

    public void Open()
    {
        sheet.gameObject.SetActive(true);
        PlayAnim(openPos, openEase);
        OnOpenEvent?.Invoke();
    }

    public void Close()
    {
        PlayAnim(closedPos, closeEase, () => { sheet.gameObject.SetActive(false); });
        OnCloseEvent?.Invoke();
    }

    protected void PlayAnim(Vector2 targetPos, Ease animEase, Action endCallback = null)
    {
        // Hủy tween cũ nếu còn đang chạy
        currentTween?.Kill();

        currentTween = sheet.DOAnchorPos(targetPos, animDuration)
            .SetEase(animEase).OnComplete(() => { endCallback?.Invoke(); });
        Debug.Log("Anchored position : " + targetPos);
    }


    protected virtual void Update()
    {
    }
}