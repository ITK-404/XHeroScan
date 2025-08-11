using UnityEngine;

public class ClampHandler
{
    public Vector3 ClampPosition(Vector3 currnetPos, Bounds bounds, float limitSize, CheckpointType type)
    {
        // clamp ở trái
        switch (type)
        {
            case CheckpointType.Left:
            case CheckpointType.TopLeft:
            case CheckpointType.BottomLeft:
                if (currnetPos.x >= bounds.center.x - limitSize)
                {
                    Debug.Log("Đã clamp trục trái");
                    currnetPos.x = bounds.center.x - limitSize;
                }
                break;
            default:
                break;
        }
        // clamp phải
        switch (type)
        {
            case CheckpointType.Right:
            case CheckpointType.TopRight:
            case CheckpointType.BottomRight:
                if (currnetPos.x <= bounds.center.x + limitSize)
                {
                    Debug.Log("Đã clamp trục phải");
                    currnetPos.x = bounds.center.x + limitSize;
                }
                break;
            default:
                break;
        }
        // clamp top
        switch (type)
        {
            case CheckpointType.Top:
            case CheckpointType.TopLeft:
            case CheckpointType.TopRight:
                if (currnetPos.z <= bounds.center.y + limitSize)
                {
                    Debug.Log("Đã clamp trục trên");
                    currnetPos.z = bounds.center.y + limitSize;
                }
                break;
            default:
                break;
        }
        // clamp bottom
        switch (type)
        {
            case CheckpointType.Bottom:
            case CheckpointType.BottomLeft:
            case CheckpointType.BottomRight:
                if (currnetPos.z >= bounds.center.y - limitSize)
                {
                    Debug.Log("Đã clamp trục dưới");
                    currnetPos.z = bounds.center.y - limitSize;
                }
                break;
            default:
                break;
        }

        return currnetPos;
    }
}