// đặt file riêng: PopupInteractionControl.cs
using UnityEngine;

public class PopupInteractionControl : MonoBehaviour
{

    public bool IsFloorHandleDragging = false;

    public bool IsRoomFloorDragging = false;

    private void Update()
    {
        if (gameObject.activeSelf)
        {
            InteractionFlags.IsFloorHandleDragging = IsFloorHandleDragging;
            InteractionFlags.IsRoomFloorDragging = IsRoomFloorDragging;
        }
    }
  
}