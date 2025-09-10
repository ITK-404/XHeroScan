using UnityEngine;

public class FurnitureHitboxSizeBuffer : MonoBehaviour
{
    [SerializeField] private Vector3 hitBoxSizeBuffer = Vector3.zero;
    private FurnitureItem furnitureItem;
    private BoxCollider boxCollider;

    private void Awake()
    {
        furnitureItem = GetComponent<FurnitureItem>();
        boxCollider = furnitureItem.modelContainer.GetComponent<BoxCollider>();
    }

    private void Start()
    {
        if (boxCollider == null || furnitureItem == null) return;
        var size = boxCollider.size;
        size.x += hitBoxSizeBuffer.x;
        size.y += hitBoxSizeBuffer.y;
        size.z += hitBoxSizeBuffer.z;
        boxCollider.size = size;
    }
}
