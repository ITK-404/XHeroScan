using UnityEngine;
using UnityEngine.UI;

public class ModularPopupEdit : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button editBtn;
    [SerializeField] private Button splitBtn;
    [SerializeField] private Button doubleBtn;
    [SerializeField] private Button deleteBtn;

    [Header("Contents (1 cái hiển thị tại 1 thời điểm)")]
    [SerializeField] private GameObject objectEdit;
    [SerializeField] private GameObject objectSplit;
    [SerializeField] private GameObject objectDouble;
    [SerializeField] private GameObject objectDelete;

    [Header("Default")]
    [SerializeField] private GameObject defaultContent; // kéo object muốn hiển thị mặc định ở đây

    private void Awake()
    {
        if (editBtn)   editBtn.onClick.AddListener(() => ShowOnly(objectEdit));
        if (splitBtn)  splitBtn.onClick.AddListener(() => ShowOnly(objectSplit));
        if (doubleBtn) doubleBtn.onClick.AddListener(() => ShowOnly(objectDouble));
        if (deleteBtn) deleteBtn.onClick.AddListener(() => ShowOnly(objectDelete));
    }

    private void OnEnable()
    {
        // hiển thị mặc định khi popup bật
        if (defaultContent != null) ShowOnly(defaultContent);
        else ShowOnly(objectEdit); // fallback
    }

    private void ShowOnly(GameObject target)
    {
        if (objectEdit)   objectEdit.SetActive(target == objectEdit);
        if (objectSplit)  objectSplit.SetActive(target == objectSplit);
        if (objectDouble) objectDouble.SetActive(target == objectDouble);
        if (objectDelete) objectDelete.SetActive(target == objectDelete);

        // (tuỳ chọn) đổi trạng thái interactable của nút đang active
        if (editBtn)   editBtn.interactable   = (target != objectEdit);
        if (splitBtn)  splitBtn.interactable  = (target != objectSplit);
        if (doubleBtn) doubleBtn.interactable = (target != objectDouble);
        if (deleteBtn) deleteBtn.interactable = (target != objectDelete);
    }
}
