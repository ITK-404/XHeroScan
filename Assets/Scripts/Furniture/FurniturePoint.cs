using UnityEngine;

public class FurniturePoint : MonoBehaviour
{
    public static Camera mainCam;

    private void Awake()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }
    }
    
    private void OnMouseDrag()
    {
        // Lấy khoảng cách từ camera đến object
        float distance = Vector3.Distance(mainCam.transform.position, transform.position);

        // Chuyển vị trí chuột sang tọa độ thế giới
        Vector3 worldMousePosition = mainCam.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance)
        );

        // Di chuyển object theo X-Z, giữ nguyên Y
        transform.position = new Vector3(worldMousePosition.x, transform.position.y, worldMousePosition.z);
    }

}