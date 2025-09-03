using System;
using UnityEngine;

[Serializable]
public class FurnitureMergeToWall
{
    private float offset ;

    private FurnitureItem furnitureItem;

    private FurniturePoint leftPoint;
    private FurniturePoint rightPoint;
    private FurniturePoint bottomPoint;

    WallLine attachedWallLine;

    private WallLine typedWallLine;

    public FurnitureMergeToWall(FurnitureItem furnitureItem)
    {
        this.furnitureItem = furnitureItem;

        typedWallLine = new WallLine();
        typedWallLine.type = furnitureItem.lineType;
    }

    public void SetupAnchor(CheckpointType leftPoint, CheckpointType rightPoint)
    {
        this.leftPoint = furnitureItem.GetFurniturePoint(leftPoint);
        this.rightPoint = furnitureItem.GetFurniturePoint(rightPoint);
        this.bottomPoint = furnitureItem.GetFurniturePoint(CheckpointType.Bottom);
    }

    private void UpdateWallLine()
    {
        typedWallLine.start = leftPoint.transform.position;
        typedWallLine.end = rightPoint.transform.position;
    }

    private bool allowSnap = false;
    public void StartSnap() => allowSnap = true;
    public void EndSnap() => allowSnap = false;

    private Vector3 GetCenterPosition()
    {
        return furnitureItem.isUsingCenterPosToSnap ? furnitureItem.GetWorldPosition() : bottomPoint.transform.position;
    }

    public void TryToMergeAndSnapInWall(bool isActiveRange = true)
    {
        if (allowSnap == false) return;
        Debug.Log("bắt đầu check để snap wall line");
        // var anchorPoint = centerPoint;
        // Vector3 centerPosition = anchorPoint.transform.position;
        Vector3 centerPosition = GetCenterPosition();
        float minDist = float.MaxValue;
        WallLine wallLine = null;

        Vector3 firstDoorPoint = Vector3.zero;
        Vector3 secondDoorPoint = Vector3.zero;

        foreach (Room room in RoomStorage.rooms)
        {
            foreach (var wl in room.wallLines)
            {
                if (wl.type != LineType.Wall) continue; // chỉ chọn từ tường thường

                Vector3 projected =
                    CheckpointManager.Instance.ProjectPointOnLineSegment(wl.start, wl.end, centerPosition);
                centerPosition.y = projected.y;
                float dist = Vector3.Distance(centerPosition, projected);
                float distance = isActiveRange ? 0.2f : 100;
                bool isObjectNearLine = IsWithinDistance(centerPosition, projected, distance);

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
            ratio = 0;
            return;
        }

        SetAttachedWallLine(wallLine);

        //Debug.Log("Kiếm được wall line để snap vào");
        //Debug.Log($"Thông số {wallLine.start} {wallLine.end}");
        furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, firstDoorPoint);

        ratio = GetPointRatio(wallLine.start, wallLine.end, firstDoorPoint);
        // cách xoay này chưa được hoàn hảo
        // RotationToWallLine();
    }

    private void SetAttachedWallLine(WallLine wallLine)
    {
        // Thoát sớm nếu không có thay đổi
        if (attachedWallLine == wallLine) return;

        if (wallLine == null) return;

        if (attachedWallLine != null)
        {
            var _room = RoomStorage.GetRoomByWall(attachedWallLine);
            if (_room == null) return;
            if (_room.wallLines.Contains(attachedWallLine) == false)
            {
                _room.wallLines.Remove(typedWallLine);
            }
        }

        attachedWallLine = wallLine;
        var room = RoomStorage.GetRoomByWall(attachedWallLine);
        if (room == null) return;
        if (room.wallLines.Contains(attachedWallLine))
        {
            room.wallLines.Add(typedWallLine);
        }
    }

    public float ratio;

    float GetPointRatio(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 ab = end - start;
        Vector3 ap = point - start;
        float t = Vector3.Dot(ap, ab) / ab.sqrMagnitude;
        return Mathf.Clamp01(t);
    }

    public void Update()
    {
        if (allowSnap)
        {
            TryToMergeAndSnapInWall();
        }

        else
        {
            if (attachedWallLine != null)
            {
                // moving but using center of wall line
                // Debug.Log("Wall line is not null, try to align with them");
                Vector3 centerPosition = Vector3.Lerp(attachedWallLine.start, attachedWallLine.end, ratio);
                FurnitureManager.Instance.debugPoint.transform.position = centerPosition;
                // sync y position from model 2D
                centerPosition.y = furnitureItem.GetWorldPosition().y;

                furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, centerPosition);
                furnitureItem.RefreshCheckPointsByBounds();

                RotationToWallLine();
            }
        }

        UpdateWallLine();
    }

    private void RotationToWallLine()
    {
        Vector3 dir = attachedWallLine.end - attachedWallLine.start;
        dir.y = 0;
        float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        furnitureItem.SetRotation(-angle + offset);
    }

    private bool IsWithinDistance(Vector3 point1, Vector3 point2, float distance)
    {
        point1.y = 0;
        point2.y = 0;
        return Vector3.Distance(point1, point2) < distance;
    }

    public bool IsInWall()
    {
        return attachedWallLine != null;
    }

    public void TryRemoveWallLine()
    {
        if (attachedWallLine != null)
        {
            var room = RoomStorage.GetRoomByWallLine(attachedWallLine);
            if (room == null) return;   
            if (room.wallLines.Contains(typedWallLine))
            {
                room.wallLines.Remove(typedWallLine);
            }
        }

    }

    public void ResetAttachedWallLine()
    {
        TryRemoveWallLine();
        attachedWallLine = null;
    }
}