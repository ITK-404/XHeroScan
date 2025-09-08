using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class BottomSheetUI : BaseAnimUI
{
    [SerializeField] private float yOffset;

    protected Vector2 openPos;
    protected Vector2 closedPos;
    protected Vector2 keyboardOpenPos;

    private bool isTweenDone = true;

    private void Start()
    {
        float height = rectContainer.rect.height;

        // Vị trí mở (ngay vị trí hiện tại)
        openPos = rectContainer.anchoredPosition;

        // Vị trí đóng (ẩn xuống dưới)
        closedPos = openPos + new Vector2(0, -(height + yOffset));

        // Khởi tạo ở trạng thái đóng
        rectContainer.anchoredPosition = closedPos;

        rectContainer.gameObject.SetActive(false);
    }

    public override void Open()
    {
        if (container.gameObject.activeSelf) return;
        if (isTweenDone == false) return;
        container.gameObject.SetActive(true);
        PlayAnim(openPos, openDuration, showEase, () =>
        {
            isTweenDone = true;
        });
        OnStartShowAnim?.Invoke();
    }

    public override void Close()
    {
        if (!container.gameObject.activeSelf) return;
        if (isTweenDone == false) return;
        OnEndHideAnim?.Invoke();
        PlayAnim(closedPos, hideDuration, hideEase, () =>
        {
            container.gameObject.SetActive(false);
            OnEndHideAnim?.Invoke();
            isTweenDone = true;
        });
    }

    protected void PlayAnim(Vector2 targetPos, float animDuration, Ease animEase, Action endCallback = null)
    {
        // Hủy tween cũ nếu còn đang chạy
        currentTween?.Kill();

        currentTween = rectContainer.DOAnchorPos(targetPos, animDuration)
            .SetEase(animEase).OnComplete(() => { endCallback?.Invoke(); });
        Debug.Log("Anchored position : " + targetPos);
    }


    protected virtual void Update()
    {
    }
}