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
        Debug.Log("Is wall line is null: " + currentWallLine == null);
        if (currentWallLine != null)
        {
            var rotation = RotationFromLine2D(currentWallLine.start, currentWallLine.end);
            furnitureItem.data.size.rotation = Quaternion.Euler(90, rotation.y, 0);
        }
    }

    public static Quaternion RotationFromLine2D(Vector2 start, Vector2 end)
    {
        Vector2 d = end - start;
        if (d.sqrMagnitude < 1e-8f) return Quaternion.identity;

        float angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        // Nếu sprite mặc định “hướng phải” (trục +X) thì dùng thẳng angleDeg
        // Nếu sprite mặc định “hướng lên” (+Y) thì angleDeg -= 90f;
        return Quaternion.AngleAxis(angleDeg, Vector3.forward);
    }
    private bool IsWithinDistance(Vector3 point1, Vector3 point2, float distance)
    {
        point1.y = 0;
        point2.y = 0;
        return Vector3.Distance(point1, point2) < distance;
    }
}