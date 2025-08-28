using UnityEngine;

public class FurnitureMergeToWall
{
    private FurnitureItem furnitureItem;

    private FurniturePoint leftPoint;
    private FurniturePoint rightPoint;
    private FurniturePoint centerPoint;
    public FurnitureMergeToWall(FurnitureItem furnitureItem)
    {
        this.furnitureItem = furnitureItem;
    }

    public void SetupAnchor()
    {
        this.leftPoint = furnitureItem.GetFurniturePoint(CheckpointType.BottomLeft);
        this.rightPoint = furnitureItem.GetFurniturePoint(CheckpointType.BottomRight);
        this.centerPoint = furnitureItem.GetFurniturePoint(CheckpointType.Bottom);
    }

    public void TryToMerge()
    {
        Debug.Log("bắt đầu check để snap wall line");
        var centerPosition = furnitureItem.transform.TransformPoint(furnitureItem.GetWorldPosition());
        float minDist = float.MaxValue;

        WallLine wallLine = null;
        Vector3 firstDoorPoint = Vector3.zero;
        Vector3 secondDoorPoint = Vector3.zero;
        foreach (Room room in RoomStorage.rooms)
        {
            foreach (var wl in room.wallLines)
            {
                if (wl.type != LineType.Wall) continue; // chỉ chọn từ tường thường

                Vector3 projected = CheckpointManager.Instance.ProjectPointOnLineSegment(wl.start, wl.end, centerPosition);
                float dist = Vector3.Distance(centerPosition, projected);
                Debug.Log("Distance: " + dist);
                if (dist < minDist && dist < 0.2f)
                {
                    minDist = dist;
                    wallLine = wl;
                    firstDoorPoint = projected;
                }
            }
        }

        if (wallLine == null)
        {
            Debug.Log("không kiếm được wallline để snap vào");
            return;
        }
        //Debug.Log("Kiếm được wall line để snap vào");
        //Debug.Log($"Thông số {wallLine.start} {wallLine.end}");
        furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, firstDoorPoint);
    }
}