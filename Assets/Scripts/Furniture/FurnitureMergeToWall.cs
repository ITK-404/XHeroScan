using System;
using iTextSharp.text.pdf.parser;
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

    private bool allowSnap = false;
    public void StartSnap() => allowSnap = true;
    public void EndSnap() => allowSnap = false;

    public void TryToMergeAndSnapInWall()
    {
        if (allowSnap == false) return;
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

                Vector3 projected =
                    CheckpointManager.Instance.ProjectPointOnLineSegment(wl.start, wl.end, centerPosition);
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
            ratio = 0;
            return;
        }

        currentWallLine = wallLine;

        //Debug.Log("Kiếm được wall line để snap vào");
        //Debug.Log($"Thông số {wallLine.start} {wallLine.end}");
        furnitureItem.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, firstDoorPoint);

        ratio = GetPointRatio(wallLine.start, wallLine.end, firstDoorPoint);
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
            if (currentWallLine != null)
            {
                Debug.Log("Wall line is not null, try to align with them");
                Vector3 centerPosition = Vector3.Lerp(currentWallLine.start, currentWallLine.end, ratio);
                FurnitureManager.Instance.debugPoint.transform.position = centerPosition;
                furnitureItem.SetWorldPosition(centerPosition);
            }
        }
    }

    private bool IsWithinDistance(Vector3 point1, Vector3 point2, float distance)
    {
        point1.y = 0;
        point2.y = 0;
        return Vector3.Distance(point1, point2) < distance;
    }
}