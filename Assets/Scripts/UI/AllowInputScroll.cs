using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AllowInputScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    public ScrollRect MainScroll;
    private bool isAvaliable = false;
    private bool isHover = false;
    private bool oldInteractive = false;
    private TMP_InputField inputField;
    private GameObject textField;
    private Image inputImage;

    void Update()
    {
        if (oldInteractive != inputField.interactable)
        {
            oldInteractive = inputField.interactable;
        }

        if (inputField.interactable)
        {
            if (Input.GetMouseButton(0))
            {
                if (!isHover)
                {
                    inputField.enabled = false;
                }
            }
        }
    }

    void Awake()
    {
        MainScroll = GetComponentInParent<ScrollRect>();
        //set class reference to input field, text object, and image
        inputField = gameObject.GetComponent<TMP_InputField>();
        inputImage = gameObject.GetComponent<Image>();
        textField = inputField.textComponent.gameObject;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        MainScroll.OnBeginDrag(eventData);

        if (inputField.interactable)
        {
            inputField.enabled = false;
            isAvaliable = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        MainScroll.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        MainScroll.OnEndDrag(eventData);

        if (inputField.interactable)
        {
            inputField.enabled = false;
            isAvaliable = true;
        }
    }

    public void OnScroll(PointerEventData data)
    {
        MainScroll.OnScroll(data);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (inputField.interactable)
        {
            if (isAvaliable)
            {
                inputField.enabled = true;
                inputField.Select();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inputField.interactable)
        {
            if (eventData.pointerEnter == textField)
            {
                isAvaliable = true;
            }
        }

    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        
        isHover = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        
        isHover = false;
    }
}
