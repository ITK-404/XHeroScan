using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModularPopup : MonoBehaviour
{
    public static PopupAsset PopupAsset
    {
        get
        {
            if (popupAsset == null)
            {
                popupAsset = Resources.Load<PopupAsset>("Popup Asset");
            }

            return popupAsset;
        }
    }

    private static PopupAsset popupAsset;

    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI yesBtnText;
    [SerializeField] private TextMeshProUGUI noBtnText;

    [SerializeField] private Button yesBtn;
    [SerializeField] private Button noBtn;

    [SerializeField] private bool canAnimate = false;

    public Button NoBtn
    {
        get => noBtn;
    }

    public Action ClickYesEvent;
    public Action ClickNoEvent;
    public Action EventWhenClickButtons;

    public bool autoClearWhenClick = false;

    public string Header
    {
        get => headerText.text;
        set => headerText.text = value;
    }

    public string Description
    {
        get => descriptionText.text;
        set => descriptionText.text = value;
    }


    public string YesText
    {
        get => yesBtnText.text;
        set => yesBtnText.text = value;
    }

    public string NoText
    {
        get => noBtnText.text;
        set => noBtnText.text = value;
    }

    private void Awake()
    {
        if (yesBtn)
            yesBtn.onClick.AddListener(OnYesClicked);
        if (noBtn)
            noBtn.onClick.AddListener(OnNoClicked);
    }

    private void Start()
    {
        if (canAnimate)
        {
            UIAnimationUltils.PopupScaleAnimation(gameObject,0.2f);
        }
    }

    private void OnYesClicked()
    {
        ClickYesEvent?.Invoke();
        EventWhenClickButtons?.Invoke();
        TryToClear();
    }

    private void OnNoClicked()
    {
        ClickNoEvent?.Invoke();
        EventWhenClickButtons?.Invoke();
        TryToClear();
    }

    private void TryToClear()
    {
        if (autoClearWhenClick)
        {
            Destroy(gameObject);
        }
    }

    public void ResetAnchorOffsetAndScale()
    {
        var popupRect = GetComponent<RectTransform>();
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;
        popupRect.transform.localScale = Vector3.one;
    }

    public void AutoFindCanvasAndSetup()
    {
        var canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);
        transform.SetParent(canvas.transform, false);
        ResetAnchorOffsetAndScale();
    }

    public void SetParent(Transform parent, int childIndex = 0)
    {
        transform.parent = parent;
        transform.SetSiblingIndex(childIndex);
    }

    public void AutoDestruct(float delay = 0.5f)
    {
        StartCoroutine(PlayDelayDestroy(delay));
    }
    
    private IEnumerator PlayDelayDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (canAnimate)
        {
            UIAnimationUltils.PopoutScaleAnimation(gameObject, 0.2f, true);
        }
        else
        {
            DestroySelf();
        }
        
    }
    
    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}