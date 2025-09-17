using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class FurnitureMergeToWall
{
    public float ratio;
    private FurnitureItem furnitureItem;

    private FurniturePoint leftPoint;
    private FurniturePoint rightPoint;
    private FurniturePoint bottomPoint;

    private WallLine attachedWallLine;
    private Room attachedRoom;

    private WallLine typedWallLine;
    public WallLine PDFWallLine => typedWallLine;
    public FurnitureMergeToWall(FurnitureItem furnitureItem)
    {
        this.furnitureItem = furnitureItem;

        typedWallLine = new WallLine();
    }

    public void SetupAnchor(CheckpointType leftPoint, CheckpointType rightPoint)
    {
        this.leftPoint = furnitureItem.GetFurniturePoint(leftPoint);
        this.rightPoint = furnitureItem.GetFurniturePoint(rightPoint);
        this.bottomPoint = furnitureItem.GetFurniturePoint(CheckpointType.Bottom);
    }

    private void UpdateOwnWallLine()
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

    public void TryToMergeAndSnapInAllWall()
    {
        SnapTemp(allowSnap);
    }

    public void ForceSnapToWall()
    {
        SnapTemp(true);
    }

    private void SnapTemp(bool allowSnap)
    {
        if (allowSnap == false) return;

        //Debug.Log("bắt đầu check để snap wall line");

        SnapToNearestWallLine(RoomStorage.rooms, 0.2f, out var wallLine, out var firstDoorPoint);
        SetAttachedWallLine(wallLine);

        if (wallLine == null)
        {
            //Debug.Log("không kiếm được wallline để snap vào");
            ratio = 0;
            return;
        }

        //Debug.Log("Kiếm được wall line để snap vào");
        //Debug.Log($"Thông số {wallLine.start} {wallLine.end}");
        furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, firstDoorPoint);

        ratio = GetPointRatio(wallLine.start, wallLine.end, firstDoorPoint);
    }

    public void SnapToNearestWallOfCurrentRoom()
    {
        if (attachedRoom == null) return;

        SnapToNearestWallLine(new[] { attachedRoom }, float.MaxValue, out var wallLine, out var firstDoorPoint);

        SetAttachedWallLine(wallLine);

        if (wallLine == null)
        {
            Debug.Log("không kiếm được wallline để snap vào");
            ratio = 0;
            return;
        }

        furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, firstDoorPoint);

    }

    private void SnapToNearestWallLine(IEnumerable<Room> rooms, float minDistance, out WallLine foundWallLine, out Vector3 foundPoint)
    {
        Vector3 centerPosition = GetCenterPosition();
        float minDist = float.MaxValue;
        WallLine wallLine = null;
        Vector3 firstDoorPoint = Vector3.zero;

        foreach (Room room in rooms)
        {
            FindNearestWallLine(room, centerPosition, minDistance, ref minDist, ref wallLine, ref firstDoorPoint);
        }

        foundWallLine = wallLine;
        foundPoint = firstDoorPoint;
    }

    public void FindNearestWallLine(Room room, Vector3 centerPosition, float minDistanceValid, ref float minDist, ref WallLine wallLine, ref Vector3 firstDoorPoint)
    {
        foreach (var wl in room.wallLines)
        {
            if (wl.type != LineType.Wall) continue; // chỉ chọn từ tường thường

            Vector3 projected =
                CheckpointManager.Instance.ProjectPointOnLineSegment(wl.start, wl.end, centerPosition);
            centerPosition.y = projected.y;
            float dist = Vector3.Distance(centerPosition, projected);
            Debug.Log($"Distance from center to line: " + dist);
            bool isObjectNearLine = IsWithinDistance(centerPosition, projected, minDistanceValid);

            if (dist < minDist && isObjectNearLine)
            {
                Debug.Log("Đã tìm thấy wall line ngắn");
                minDist = dist;
                wallLine = wl;
                firstDoorPoint = projected;
            }
            //Debug.Log("Distance: " + dist);
        }
    }

    private void SetAttachedWallLine(WallLine wallLine)
    {
        // Thoát sớm nếu không có thay đổi
        if (attachedWallLine == wallLine) return;

        attachedWallLine = wallLine;
        attachedRoom = wallLine != null ? RoomStorage.GetRoomByWall(attachedWallLine) : null;
    }

    public void CheckWallLineIsValidInRoom()
    {
        if (attachedWallLine != null)
        {
            if (attachedRoom.wallLines.Contains(attachedWallLine) == false)
            {
                attachedWallLine = null;
            }
        }
    }


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
            TryToMergeAndSnapInAllWall();
        }

        else
        {
            if (attachedWallLine != null)
            {
                // moving but using center of wall line
                // Debug.Log("Wall line is not null, try to align with them");
                Vector3 centerPosition = Vector3.Lerp(attachedWallLine.start, attachedWallLine.end, ratio);
                // sync y position from model 2D
                centerPosition.y = furnitureItem.GetWorldPosition().y;

                furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, centerPosition);
                furnitureItem.RefreshCheckPointsByBounds();

                RotationToWallLine();
            }
        }

        UpdateOwnWallLine();
        UpdateRoomAttaced();
        ProcessWidthForWindow();
    }
    private void ProcessWidthForWindow()
    {
        // xử lý độ dày cho cửa sổ
        if (furnitureItem.lineType != LineType.Window)
            return;
        if (attachedRoom == null)
            return;

        furnitureItem.data.size.length = attachedRoom.thickness;
    }
    private void UpdateRoomAttaced()
    {
        // cập nhật rằng furniture đang được gắn trong room hay không
        if (attachedRoom == null)
        {
            furnitureItem.data.roomID = "";
            return;
        }
        furnitureItem.data.roomID = attachedRoom.ID;
    }


    private void RotationToWallLine()
    {
        var flipOffset = 0;
        //flipOffset = furnitureItem.data.isFlipVertical ? 180 : 0;
        Vector3 dir = attachedWallLine.end - attachedWallLine.start;
        dir.y = 0;
        float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        furnitureItem.SetRotation(-angle + flipOffset);
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

    public void ResetAttached()
    {
        attachedRoom = null;
        attachedWallLine = null;
    }
}

