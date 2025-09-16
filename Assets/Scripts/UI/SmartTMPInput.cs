using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class SmartTMPInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private CanvasGroup cg;
    private TMP_InputField input;
    private bool isDragging = false;
    private Vector2 downPos;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        input = GetComponent<TMP_InputField>();
        cg.blocksRaycasts = false; // mặc định cho raycast đi qua
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        downPos = eventData.position;
        isDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Vector2.Distance(eventData.position, downPos) > 10f)
            isDragging = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
        {
            // Chỉ tap: bật lại raycast + focus input
            cg.blocksRaycasts = true;
            input.ActivateInputField();
        }
    }

}
