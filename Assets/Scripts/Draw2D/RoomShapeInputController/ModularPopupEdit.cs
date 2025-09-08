using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class ModularPopupEdit : MonoBehaviour
{
    [Header("Buttons")]
    public Button editBtn;
    public Button splitBtn;
    public Button doubleBtn;
    public Button deleteBtn;

    [Header("Contents (chỉ 1 hiển thị tại 1 thời điểm)")]
    [SerializeField] private GameObject objectEdit;
    [SerializeField] private GameObject objectSplit;
    [SerializeField] private GameObject objectDouble;
    [SerializeField] private GameObject objectDelete;
    [Header("Open behavior")]
    [SerializeField] private bool isShowSplit = true;
    [SerializeField] private bool deferOpenOneFrame = true;

    void Awake()
    {
        gameObject.SetActive(false);
        if (editBtn)   editBtn.onClick.AddListener(() => ShowOnly(objectEdit));
        if (splitBtn)  splitBtn.onClick.AddListener(() => ShowOnly(objectSplit));
        if (doubleBtn) doubleBtn.onClick.AddListener(() => ShowOnly(objectDouble));
        if (deleteBtn) deleteBtn.onClick.AddListener(() => ShowOnly(objectDelete));

        if (Application.isPlaying && EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        splitBtn.gameObject.SetActive(isShowSplit);
    }

    private void ShowOnly(GameObject target)
    {
        if (!target) return;

        // Tắt tất cả
        if (objectEdit)   objectEdit.SetActive(false);
        if (objectSplit)  objectSplit.SetActive(false);
        if (objectDouble) objectDouble.SetActive(false);
        if (objectDelete) objectDelete.SetActive(false);

        // Bật panel mục tiêu
        target.SetActive(true);

        // Nếu là tab Edit thì mở ngay BottomSheetUI
        if (target == objectEdit)
        {
            var bs = objectEdit.GetComponent<BottomSheetUI>();
            if (bs != null)
            {
                if (deferOpenOneFrame) StartCoroutine(Defer(() => bs.Open()));
                else bs.Open();
            }
        }
        else
        {
            // Tab khác, nếu có BottomSheetUI thì cũng mở
            var bs = target.GetComponent<BottomSheetUI>();
            if (bs != null)
            {
                if (deferOpenOneFrame) StartCoroutine(Defer(() => bs.Open()));
                else bs.Open();
            }
        }
    }

    private IEnumerator Defer(System.Action action)
    {
        yield return null;
        
        action?.Invoke();
    }
}
