using UnityEngine;

public class SpriteOneUnit : MonoBehaviour
{
    [ContextMenu("Scale Sprite to 1x1 Unit")]
    void ScaleToOne()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
        {
            Debug.LogWarning("Không có SpriteRenderer hoặc Sprite để scale.");
            return;
        }

        Vector2 size = sr.sprite.bounds.size;
        if (size.x == 0 || size.y == 0) return;

        transform.localScale = new Vector3(
            1f / size.x,
            1f / size.y,
            transform.localScale.z
        );
    }
}