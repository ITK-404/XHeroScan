using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TMP_InputFieldCustom : TMP_InputField
{
    protected bool isDragging = false;
    private Vector2 pointerDownPos;
    public override void OnPointerDown(PointerEventData eventData)
    {
        // Ghi lại vị trí khi bắt đầu chạm
        pointerDownPos = eventData.position;
        isDragging = false;
        // KHÔNG gọi base.OnPointerDown ở đây
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
        {
            // Nếu không kéo, coi như click
            base.OnPointerDown(eventData); // chuẩn bị caret
            base.OnPointerClick(eventData); // focus và bật bàn phím
        }
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);
        if (Vector2.Distance(eventData.position, pointerDownPos) > 10f)
        {
            isDragging = true; // vượt ngưỡng, coi là scroll
        }
    }
}
