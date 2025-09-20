using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Heading/hướng cho vẽ 2D (mặc định scene phẳng XZ):
/// - 0° = Nam (Z+)  -> TRÊN
/// - 90° = Đông (X-) -> TRÁI
/// - 180° = Bắc (Z-) -> DƯỚI
/// - 270° = Tây (X+) -> PHẢI
/// </summary>
public static class HeadingManager
{
    public const float EPS = 1e-8f;

    /// <summary>
    /// 0° = Nam (Z+), 90° = Đông (X−), 180° = Bắc (Z−), 270° = Tây (X+).
    /// Tính heading có CHIỀU từ 'from' -> 'to' (bearing đầy đủ 0..360).
    /// </summary>
    public static float HeadingDeg(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f; // chỉ xét XZ
        if (dir.sqrMagnitude < EPS) return 0f;

        // Đặt 0° trùng Z+:
        // Atan2(-dx, dz) -> 0° = Z+, 90° = X-, 180° = Z-, 270° = X+
        // float angle = Mathf.Atan2(-dir.x, dir.z) * Mathf.Rad2Deg;
        // if (angle < 0f) angle += 360f;
        // 0° = Đông (X+), 90° = Nam (Z+), 180° = Tây (X-), 270° = Bắc (Z-)
float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg; 
if (angle < 0f) angle += 360f;
        return angle;
    }

    /// <summary>Đưa góc về [0,360).</summary>
    public static float Wrap360(float deg)
    {
        deg = Mathf.Repeat(deg, 360f);
        return deg;
    }

    /// <summary>Đưa góc về [-180,180).</summary>
    public static float Wrap180(float deg)
    {
        deg = Mathf.Repeat(deg + 180f, 360f) - 180f;
        return deg;
    }

    /// <summary>
    /// Vector đơn vị trên mặt phẳng XZ tương ứng với heading theo quy ước:
    /// 0° -> (0,0, +1), 90° -> (-1,0,0), 180° -> (0,0,-1), 270° -> (1,0,0)
    /// </summary>
    public static Vector3 DirFromHeading(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    /// <summary>
    /// Khoảng cách bình phương từ điểm p đến đoạn thẳng ab trong XZ.
    /// Dùng nhanh & ổn định để tìm tường gần nhất.
    /// </summary>
    public static float DistPointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z);
        Vector2 A = new Vector2(a.x, a.z);
        Vector2 B = new Vector2(b.x, b.z);
        Vector2 AB = B - A;

        float ab2 = Vector2.Dot(AB, AB);
        if (ab2 < EPS) return (P - A).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(P - A, AB) / ab2);
        Vector2 H = A + t * AB;
        return (P - H).sqrMagnitude;
    }

    /// <summary>
    /// Cập nhật heading cho 1 WallLine: heading = headingGeom(from->to) + roomOffsetDeg.
    /// Kết quả lưu vào wl.headingCompass (đã wrap 0..360).
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

        for (int i = 0; i < room.wallLines.Count; i++)
        {
            var wl = room.wallLines[i];
            if (wl == null) continue;
            UpdateWallHeading(wl, roomOffsetDeg);
        }

        room.headingCompass = Wrap360(roomOffsetDeg);
    }

    /// <summary>
    /// Tìm index đoạn tường gần pointWorld nhất (trong XZ).
    /// Có lọc cơ bản: chỉ Wall, bỏ manual/ẩn/đoạn quá ngắn (tùy chọn).
    /// </summary>
    public static int FindClosestWallIndex(
        Room room,
        Vector3 pointWorld,
        bool wallsOnly = true,
        bool ignoreManual = true,
        bool ignoreInvisible = true,
        float minLen = 0.05f
    )
    {
        if (room?.wallLines == null || room.wallLines.Count == 0) return -1;

        int best = -1;
        float bestD = float.MaxValue;

        for (int i = 0; i < room.wallLines.Count; i++)
        {
            var w = room.wallLines[i];
            if (w == null) continue;

            if (ignoreInvisible && !w.isVisible) continue;
            if (wallsOnly && w.type != LineType.Wall) continue;
            if (ignoreManual && w.isManualConnection) continue;

            if ((w.end - w.start).sqrMagnitude < minLen * minLen) continue;

            float d = DistPointToSegmentXZ(pointWorld, w.start, w.end);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// Gán nhãn 8 hướng chính theo quy ước 0°=Nam (Z+).
    /// S, SW, W, NW, N, NE, E, SE. (S=0°, W=270°,...)
    /// </summary>
    public static string ToCardinalLabel(float deg, float step = 45f)
    {
        float d = Mathf.Round(Wrap360(deg) / step) * step;
        d = Wrap360(d);

        // Lưu ý: vì 0°=Nam, thứ tự nhãn "xoay" khác với chuẩn 0°=Bắc
        if (Mathf.Approximately(d,   0f)) return "S";
        if (Mathf.Approximately(d,  45f)) return "SW";
        if (Mathf.Approximately(d,  90f)) return "E";   // 90° = Đông (X-), nhưng nhãn vẫn "E"
        if (Mathf.Approximately(d, 135f)) return "SE";
        if (Mathf.Approximately(d, 180f)) return "N";
        if (Mathf.Approximately(d, 225f)) return "NE";
        if (Mathf.Approximately(d, 270f)) return "W";
        if (Mathf.Approximately(d, 315f)) return "NW";

        return $"{d:0}°";
    }
}
