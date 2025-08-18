using DG.Tweening;
using UnityEngine;

public class BottomSheetUI : MonoBehaviour
{
    [SerializeField] private float animDuration = 0.3f;
    [SerializeField] private Ease animEase = Ease.OutCubic;

    protected RectTransform sheet;
    protected Vector2 openPos;
    protected Vector2 closedPos;
    protected Vector2 keyboardOpenPos;
    protected Tweener currentTween;

    private void Start()
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

    private void OnDestroy()
    {
        currentTween?.Kill();
    }

    public void Open()
    {
        PlayAnim(openPos);
    }

    public void Close()
    {
        PlayAnim(closedPos);
    }

    protected void PlayAnim(Vector2 targetPos)
    {
        // Hủy tween cũ nếu còn đang chạy
        currentTween?.Kill();

        currentTween = sheet.DOAnchorPos(targetPos, animDuration)
            .SetEase(animEase);
        Debug.Log("Anchored position : "+targetPos);
    }

    [SerializeField] protected bool testByKeyboard;
    [SerializeField] protected bool activeWithPanel;


    protected virtual void Update()
    {
        
        if (!testByKeyboard) return;
        if (Input.GetKeyDown(KeyCode.O)) Open();
        if (Input.GetKeyDown(KeyCode.C)) Close();
    }
}