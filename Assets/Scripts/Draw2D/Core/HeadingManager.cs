using System.Collections.Generic;
using UnityEngine;

// tạm thời đúng 2D

public static class HeadingManager
{
    public const float EPS = 1e-8f;

    /// <summary>
    /// Tính heading theo mặt phẳng XZ: 
    /// 0° = Nam (Z−), 90° = Đông (X+), 180° = Bắc (Z+), 270° = Tây (X−)
    /// </summary>
    public static float HeadingDeg(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f; // chỉ xét XZ
        if (dir.sqrMagnitude < EPS) return 0f;

        // Atan2(dx, -dz) để 0° = Z−
        float angle = Mathf.Atan2(dir.x, -dir.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    public static float Wrap360(float deg)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }

    /// <summary>
    /// Khoảng cách bình phương từ điểm p đến đoạn ab theo XZ
    /// </summary>
    public static float DistPointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new(p.x, p.z);
        Vector2 A = new(a.x, a.z);
        Vector2 B = new(b.x, b.z);
        Vector2 AB = B - A;
        float ab2 = Vector2.Dot(AB, AB);
        if (ab2 < EPS) return (P - A).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(P - A, AB) / ab2);
        Vector2 H = A + t * AB;
        return (P - H).sqrMagnitude;
    }

    /// <summary>
    /// Cập nhật heading cho 1 WallLine, cộng thêm roomOffsetDeg (nếu có).
    /// </summary>
    public static void UpdateWallHeading(WallLine wl, float roomOffsetDeg = 0f)
    {
        if (wl == null) return;
        float geom = HeadingDeg(wl.start, wl.end);
        wl.headingCompass = Wrap360(geom + roomOffsetDeg);
    }

    /// <summary>
    /// Cập nhật heading cho toàn bộ tường trong room.
    /// room.headingCompass = roomOffsetDeg (đã wrap).
    /// </summary>
    public static void UpdateAllWallHeadings(Room room, float roomOffsetDeg = 0f)
    {
        if (room == null || room.wallLines == null) return;
        foreach (var wl in room.wallLines)
            UpdateWallHeading(wl, roomOffsetDeg);

        room.headingCompass = Wrap360(roomOffsetDeg);

        // Vì 0° bây giờ là Nam (Z−), vector compass = (0, -1)
        // room.Compass = new Vector2(0f, -1f);
    }

    /// <summary>
    /// Tìm index đoạn tường gần pointWorld nhất.
    /// </summary>
    public static int FindClosestWallIndex(Room room, Vector3 pointWorld)
    {
        if (room == null || room.wallLines == null || room.wallLines.Count == 0) return -1;
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < room.wallLines.Count; i++)
        {
            var w = room.wallLines[i];
            float d = DistPointToSegmentXZ(pointWorld, w.start, w.end);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>
    /// Hiệu chỉnh heading cả room dựa vào một đo đạc AR:
    /// - measuredHeadingDeg: hướng la bàn đo được (0°=Nam).
    /// - arPointWorld: điểm tham chiếu (gần tường nào thì dùng tường đó làm chuẩn).
    /// - snap90: nếu true sẽ “bắt” offset về bội số 90° (đẹp cho phòng vuông).
    /// </summary>
    public static void CalibrateRoomHeadingByAR(Room room, Vector3 arPointWorld, float measuredHeadingDeg, bool snap90 = true)
    {
        int idx = FindClosestWallIndex(room, arPointWorld);
        if (idx < 0) return;

        var target = room.wallLines[idx];
        float geom = HeadingDeg(target.start, target.end);
        float offset = Wrap360(measuredHeadingDeg - geom);

        if (snap90)
            offset = Wrap360(Mathf.Round(offset / 90f) * 90f);

        UpdateAllWallHeadings(room, offset);
    }
}
