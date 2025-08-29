using System;
using UnityEngine;
[Serializable]
public class FurnitureMergeToWall
{
    private FurnitureItem furnitureItem;

    private FurniturePoint leftPoint;
    private FurniturePoint rightPoint;
    private FurniturePoint centerPoint;

    private WallLine currentWallLine;
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

    public void TryToMergeAndSnapInWall()
    {
        Debug.Log("bắt đầu check để snap wall line");
        var anchorPoint = centerPoint;
        Vector3 centerPosition = anchorPoint.transform.position;

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
                centerPosition.y = projected.y;
                float dist = Vector3.Distance(centerPosition, projected);

                bool isObjectNearLine = IsWithinDistance(centerPosition, projected, 0.2f);

                if (dist < minDist && isObjectNearLine)
                {
                    minDist = dist;
                    wallLine = wl;
                    firstDoorPoint = projected;
                }
                //Debug.Log("Distance: " + dist);
            }
        }

        if (wallLine == null)
        {
            Debug.Log("không kiếm được wallline để snap vào");
            return;
        }
        currentWallLine = wallLine;

        //Debug.Log("Kiếm được wall line để snap vào");
        //Debug.Log($"Thông số {wallLine.start} {wallLine.end}");
        furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, firstDoorPoint);


    }

    public void Update()
    {
        if (currentWallLine != null)
        {
            Debug.Log("Wall line is not null, try to align with them");
            Vector3 centerPosition = (currentWallLine.start + currentWallLine.end) / 2;
            FurnitureManager.Instance.debugPoint.transform.position = centerPosition;
        }
    }
    
    private bool IsWithinDistance(Vector3 point1, Vector3 point2, float distance)
    {
        point1.y = 0;
        point2.y = 0;
        return Vector3.Distance(point1, point2) < distance;
    }
}