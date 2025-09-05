using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FloorStorage
{
    public static List<Floor> floors = new List<Floor>();

    public static Floor GetFloorByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < floors.Count; i++)
            if (floors[i] != null && floors[i].ID == id)
                return floors[i];
        return null;
    }

    public static void UpdateOrAddFloor(Floor updated)
    {
        if (updated == null || string.IsNullOrEmpty(updated.ID)) return;
        for (int i = 0; i < floors.Count; i++)
        {
            if (floors[i].ID == updated.ID)
            {
                // cập nhật tối thiểu, bạn có thể mở rộng tùy ý
                floors[i].floorName = updated.floorName;
                floors[i].checkpoints = new List<Vector2>(updated.checkpoints ?? new List<Vector2>());
                floors[i].heights = new List<float>(updated.heights ?? new List<float>());
                floors[i].floorLine = new List<FloorLine>(updated.floorLine ?? new List<FloorLine>());
                floors[i].center = updated.center;
                // roomIDs giữ theo trạng thái hiện có
                return;
            }
        }
        floors.Add(updated);
    }

    public static void RegisterRoomIdToFloor(string floorId, string roomId)
    {
        if (string.IsNullOrEmpty(floorId) || string.IsNullOrEmpty(roomId)) return;
        var f = GetFloorByID(floorId);
        if (f == null) return;
        if (!f.roomIDs.Contains(roomId)) f.roomIDs.Add(roomId);
    }

    public static void UnregisterRoomIdFromFloor(string floorId, string roomId)
    {
        if (string.IsNullOrEmpty(floorId) || string.IsNullOrEmpty(roomId)) return;
        var f = GetFloorByID(floorId);
        if (f == null) return;
        f.roomIDs.Remove(roomId);
    }
}
public static class RoomGeom
{
    public static void ReanchorToCenter(Room r)
    {
        if (r == null || r.checkpoints == null || r.checkpoints.Count < 3) return;

        // centroid của polygon nhận từ AR (theo world-space)
        Vector2 cNow = GeoUtil.Centroid(r.checkpoints);

        // delta để đưa centroid về đúng neo (center) đang có trong Room
        Vector2 delta = r.center - cNow;

        // dịch polygon chính
        for (int i = 0; i < r.checkpoints.Count; i++)
            r.checkpoints[i] = r.checkpoints[i] + delta;

        // dịch điểm extra (nếu có)
        for (int i = 0; i < r.extraCheckpoints.Count; i++)
            r.extraCheckpoints[i] = r.extraCheckpoints[i] + delta;

        // dịch tất cả wallLines theo XZ
        foreach (var w in r.wallLines)
        {
            w.start += new Vector3(delta.x, 0f, delta.y);
            w.end   += new Vector3(delta.x, 0f, delta.y);
        }
    }
}
