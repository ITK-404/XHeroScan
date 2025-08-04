using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    [Header("Materials")]
    public Material defaultMaterial;
    public Material selectedMaterial;

    [HideInInspector]
    public GameObject checkpointPrefab;  // Tự động gán từ chính object này

    private Renderer rend;
    private bool isSelected = false;

    void Start()
    {
        checkpointPrefab = gameObject; // Tự gán prefab = chính object này

        rend = GetComponentInChildren<Renderer>();
        if (rend == null)
            Debug.LogWarning($"Renderer not found on {gameObject.name}");

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        Debug.Log($"{gameObject.name} SetSelected({selected})");

        if (rend != null)
            rend.material = selected ? selectedMaterial : defaultMaterial;

        if (selected) OnSelected();
        else OnDeselected();
    }

    public bool IsSelected() => isSelected;

    private void OnSelected()
    {
        // (Tuỳ ý thêm hiệu ứng glow, outline ở đây)
        Debug.Log($"{gameObject.name} đã được chọn.");
    }

    private void OnDeselected()
    {
        transform.localScale = Vector3.one;
    }
}
