using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ForwardScrollEvents : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] ScrollRect _ScrollRect;
    private void Awake()
    {
        _ScrollRect = GetComponentInParent<ScrollRect>();
    }
    public void OnBeginDrag(PointerEventData eventData) => _ScrollRect.OnBeginDrag(eventData);
    public void OnDrag(PointerEventData eventData) => _ScrollRect.OnDrag(eventData);
    public void OnEndDrag(PointerEventData eventData) => _ScrollRect.OnEndDrag(eventData);
}