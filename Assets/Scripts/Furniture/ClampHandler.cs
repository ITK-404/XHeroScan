using UnityEngine;

public class ClampHandler
{
    public static Vector3 ClampPosition(Vector3 currentPos, Vector3 center, float limitSize, CheckpointType type)
    {
        Debug.Log("Center: Position: " + center);
        Vector3 clampedPos = currentPos;

        // Clamp ở trái - CHỈ áp dụng cho các type Left
        if (type == CheckpointType.Left || type == CheckpointType.TopLeft || type == CheckpointType.BottomLeft)
        {
            if (currentPos.x > center.x - limitSize)
            {
                Debug.Log("Đã clamp trục trái");
                clampedPos.x = center.x - limitSize;
            }
        }

        // Clamp phải - CHỈ áp dụng cho các type Right  
        if (type == CheckpointType.Right || type == CheckpointType.TopRight || type == CheckpointType.BottomRight)
        {
            if (currentPos.x < center.x + limitSize)
            {
                Debug.Log("Đã clamp trục phải");
                clampedPos.x = center.x + limitSize;
            }
        }

        // Clamp top - CHỈ áp dụng cho các type Top
        if (type == CheckpointType.Top || type == CheckpointType.TopLeft || type == CheckpointType.TopRight)
        {
            if (currentPos.z < center.z + limitSize)
            {
                Debug.Log("Đã clamp trục trên");
                clampedPos.z = center.z + limitSize;
            }
        }

        // Clamp bottom - CHỈ áp dụng cho các type Bottom
        if (type == CheckpointType.Bottom || type == CheckpointType.BottomLeft || type == CheckpointType.BottomRight)
        {
            if (currentPos.z > center.z - limitSize)
            {
                Debug.Log("Đã clamp trục dưới");
                clampedPos.z = center.z - limitSize;
            }
        }

        return clampedPos;
    }
}