using UnityEngine;

public class HighlightHandler : MonoBehaviour
{
    public static HighlightHandler Instance;

    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material highlightMaterial;
    public static Material HighlightMaterial;
    public static Material NormalMaterial;

    private HighlightTarget currentTarget;

    private void Awake()
    {
        Instance = this;

        HighlightMaterial = highlightMaterial;
        NormalMaterial = normalMaterial;
    }

    public void Select(HighlightTarget target)
    {
        currentTarget?.UnHighlight();
        currentTarget = target;
        currentTarget?.HighLight();
    }
}