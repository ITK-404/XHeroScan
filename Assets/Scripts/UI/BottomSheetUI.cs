using DG.Tweening;
using UnityEngine;

public class BottomSheetUI : MonoBehaviour
{
    [SerializeField] private float animDuration = 0.3f;
    [SerializeField] private Ease animEase = Ease.OutCubic;

    private RectTransform sheet;
    private Vector2 openPos;
    private Vector2 closedPos;
    private Tweener currentTween;

    private void Awake()
    {
        sheet = GetComponent<RectTransform>();

        float height = sheet.rect.height;

        // Vị trí mở (ngay vị trí hiện tại)
        openPos = sheet.anchoredPosition;

        // Vị trí đóng (ẩn xuống dưới)
        closedPos = openPos + new Vector2(0, -height);

        // Khởi tạo ở trạng thái đóng
        sheet.anchoredPosition = closedPos;
    }

    public void Open()
    {
        PlayAnim(openPos);
    }

    public void Close()
    {
        PlayAnim(closedPos);
    }

    private void PlayAnim(Vector2 targetPos)
    {
        // Hủy tween cũ nếu còn đang chạy
        currentTween?.Kill();

        currentTween = sheet.DOAnchorPos(targetPos, animDuration)
            .SetEase(animEase);
    }

    // Test nhanh bằng phím O và C
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O)) Open();
        if (Input.GetKeyDown(KeyCode.C)) Close();
    }
}