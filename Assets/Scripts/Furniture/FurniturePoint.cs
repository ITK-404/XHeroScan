using UnityEngine;

public class FurniturePoint : MonoBehaviour
{
    public static Camera mainCam;
    public Transform center;
    public ScaleHandle scaleHandle;
    public FurnitureItem furniture;
    private void Awake()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }
    }

    private void OnMouseDrag()
    {
        furniture.OnDragPoint(this);
    }
}