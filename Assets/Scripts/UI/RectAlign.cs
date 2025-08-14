using UnityEngine;

public class RectAligner : MonoBehaviour
{
    public RectTransform leftRect;
    public RectTransform rightRect;
    public RectTransform targetRect;

    [Header("Offsets (%) - Tính theo khoảng cách giữa Left và Right")]
    [Range(-1f, 1f)] public float widthPercentOffset = 0f;
    [Range(-1f, 1f)] public float centerPercentOffset = 0f;
    [Range(-1f, 1f)] public float leftPercentOffset = 0f;
    [Range(-1f, 1f)] public float rightPercentOffset = 0f;

    void Update()
    {
        AlignTarget();
    }

    void AlignTarget()
    {
        if (leftRect == null || rightRect == null || targetRect == null) return;

        RectTransform parent = targetRect.parent as RectTransform;

        Vector3[] leftWorldCorners = new Vector3[4];
        Vector3[] rightWorldCorners = new Vector3[4];

        leftRect.GetWorldCorners(leftWorldCorners);
        rightRect.GetWorldCorners(rightWorldCorners);

        Vector3 worldLeft = leftWorldCorners[0];   // bottom-left
        Vector3 worldRight = rightWorldCorners[3]; // top-right

        Vector2 localLeft, localRight;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, worldLeft, null, out localLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, worldRight, null, out localRight);

        float baseWidth = (localRight.x - localLeft.x);
        float leftOffset = leftPercentOffset * baseWidth;
        float rightOffset = rightPercentOffset * baseWidth;
        float widthOffset = widthPercentOffset * baseWidth;
        float centerOffset = centerPercentOffset * baseWidth;

        float finalWidth = baseWidth + leftOffset + rightOffset + widthOffset;
        float center = (localRight.x + localLeft.x) / 2f + (rightOffset - leftOffset) / 2f + centerOffset;

        // Set anchor/pivot để canh giữa
        targetRect.anchorMin = new Vector2(0.5f, targetRect.anchorMin.y);
        targetRect.anchorMax = new Vector2(0.5f, targetRect.anchorMax.y);
        targetRect.pivot = new Vector2(0.5f, targetRect.pivot.y);

        // Gán giá trị mới
        targetRect.anchoredPosition = new Vector2(center, targetRect.anchoredPosition.y);
        targetRect.sizeDelta = new Vector2(finalWidth, targetRect.sizeDelta.y);
    }
}
