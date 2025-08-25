using UnityEngine;

public class Test : MonoBehaviour
{
    private void Awake()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();
        Debug.Log($"SR Size {spriteRenderer.size}", gameObject);
        Debug.Log($"SR bound Size {spriteRenderer.bounds.size}", gameObject);
        Debug.Log($"SR local bound Size {spriteRenderer.localBounds.size}", gameObject);
        Debug.Log($"Sr sprite bound Size {spriteRenderer.sprite.bounds.size},gameObject");
    }
}