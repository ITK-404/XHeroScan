using UnityEngine;

public class ClampHandler
{
    public static Vector3 ClampPosition(Vector3 currentPos, Vector3 center, float limitSize, CheckpointType type, Quaternion currentRotation)
    {
        // B1: Đưa vị trí và tâm về local space
        Vector3 localPos = Quaternion.Inverse(currentRotation) * (currentPos - center);

        // B2: Clamp trong local space (như logic cũ)
        if (type == CheckpointType.Left || type == CheckpointType.TopLeft || type == CheckpointType.BottomLeft)
        {
            if (localPos.x > -limitSize)
                localPos.x = -limitSize;
        }

        if (type == CheckpointType.Right || type == CheckpointType.TopRight || type == CheckpointType.BottomRight)
        {
            if (localPos.x < limitSize)
                localPos.x = limitSize;
        }

        if (type == CheckpointType.Top || type == CheckpointType.TopLeft || type == CheckpointType.TopRight)
        {
            if (localPos.z < limitSize)
                localPos.z = limitSize;
        }

        if (type == CheckpointType.Bottom || type == CheckpointType.BottomLeft || type == CheckpointType.BottomRight)
        {
            if (localPos.z > -limitSize)
                localPos.z = -limitSize;
        }

        // B3: Chuyển kết quả clamp về world space
        return center + currentRotation * localPos;
    }
}