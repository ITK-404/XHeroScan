using UnityEngine;

public class ScaleByCameraZoom : MonoBehaviour
{
    public Vector3 originalScale;
    public float originalRotationPointOffset = 0.25f;
    public float originalSize;
    public float originalWidth;
    public float Offset => offset;
    private float offset;
    public Vector3 Scale => scale;
    private Vector3 scale;
    public float Width => width;
    private float width;
    
    private Camera mainCam;
    private float ratio;
    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        ratio = mainCam.orthographicSize / originalSize;
        ratio = Mathf.Clamp(ratio, 1, 3);
        scale = originalScale * ratio;
        offset = originalRotationPointOffset * ratio;
        width = originalWidth * ratio;
    }
}