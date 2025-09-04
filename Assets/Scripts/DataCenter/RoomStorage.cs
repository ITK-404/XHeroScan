using System;
using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public static class RoomStorage
    {
        public static List<Room> rooms = new List<Room>();

    public static void UpdateOrAddRoomForAR(Room updatedRoom)
    {
        if (updatedRoom == null || string.IsNullOrEmpty(updatedRoom.ID))
        {
            Debug.LogWarning("[RoomStorage] UpdateOrAddRoomForAR: updatedRoom null/ID rỗng.");
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].ID == updatedRoom.ID)
            {
                var dst = rooms[i]; // phòng gốc đang lưu (có center neo chuẩn)

                // Ép polygon mới “bám” lại center gốc của dst
                var tmp = new Room(updatedRoom); // clone để an toàn
                tmp.center = dst.center;         // neo mong muốn
                RoomGeom.ReanchorToCenter(tmp);  // đưa polygon về đúng neo

                // Copy dữ liệu (NHƯNG giữ center gốc)
                string oldFloorId = dst.floorID;
                string newFloorId = tmp.floorID;

                dst.groupID = tmp.groupID;
                dst.roomName = tmp.roomName;
                dst.floorMaterial = tmp.floorMaterial;
                dst.floorID = newFloorId;

                dst.checkpoints = new List<Vector2>(tmp.checkpoints);
                dst.extraCheckpoints = new List<Vector2>(tmp.extraCheckpoints);
                dst.heights = new List<float>(tmp.heights);
                dst.wallLines = tmp.wallLines != null ? tmp.wallLines.Select(w => new WallLine(w)).ToList() : new List<WallLine>();
                dst.Compass = tmp.Compass;
                dst.headingCompass = tmp.headingCompass;

                // Sync Floor linkage
                if (!string.Equals(oldFloorId, newFloorId, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(oldFloorId))
                        FloorStorage.UnregisterRoomIdFromFloor(oldFloorId, dst.ID);
                    if (!string.IsNullOrEmpty(newFloorId))
                        FloorStorage.RegisterRoomIdToFloor(newFloorId, dst.ID);
                }
                else if (!string.IsNullOrEmpty(newFloorId))
                {
                    FloorStorage.RegisterRoomIdToFloor(newFloorId, dst.ID);
                }

                Debug.Log($"[RoomStorage] Updated room {dst.ID} (center anchored at {dst.center})");
                return;
            }
        }

        if ((updatedRoom.center == default(Vector2) || updatedRoom.center == Vector2.zero) &&
                updatedRoom.checkpoints != null && updatedRoom.checkpoints.Count >= 3)
        {
            // phương án 1: neo luôn tại centroid lần đầu
            updatedRoom.center = GeoUtil.Centroid(updatedRoom.checkpoints);
            RoomGeom.ReanchorToCenter(updatedRoom);
        }

        rooms.Add(updatedRoom);
        if (!string.IsNullOrEmpty(updatedRoom.floorID))
            FloorStorage.RegisterRoomIdToFloor(updatedRoom.floorID, updatedRoom.ID);

        Debug.Log($"[RoomStorage] Added room {updatedRoom.ID} (center anchored at {updatedRoom.center})");
    }

    public static void UpdateOrAddRoom(Room updatedRoom)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].ID == updatedRoom.ID)
            {
                Debug.Log("[ROOM_STORAGE] RoomID da bi thay doi" + rooms[i].ID);
                // rooms[i].checkpoints = new List<Vector2>(updatedRoom.checkpoints);
                // rooms[i].extraCheckpoints = new List<Vector2>(updatedRoom.extraCheckpoints);
                // // rooms[i].wallLines = new List<WallLine>(updatedRoom.wallLines);
                // rooms[i].wallLines = new List<WallLine>(updatedRoom.wallLines.Select(w => new WallLine(w)));
                // rooms[i].heights = new List<float>(updatedRoom.heights);
                // rooms[i].Compass = updatedRoom.Compass;
                // rooms[i].headingCompass = updatedRoom.headingCompass;
                // rooms[i].area = updatedRoom.area;
                // rooms[i].ceilingArea = updatedRoom.ceilingArea;
                // rooms[i].perimeter = updatedRoom.perimeter;
                // Debug.Log($"[RoomStorage] Room {updatedRoom.ID} đã được cập nhật.");
                return;
            }
        }

        // Debug.Log("[ROOM_STORAGE] Them room moi" + updatedRoom.ID);
        rooms.Add(updatedRoom);
    }
        public static Room GetRoomByID(string id)
        {
            foreach (var room in rooms)
            {
                if (room.ID == id)
                    return room;
            }

            Debug.LogWarning($"RoomStorage: Không tìm thấy Room với ID: {id}");
            return null;
        }

        public static List<Room> GetRoomsByGroupID(string groupID)
        {
            return rooms.Where(r => r.groupID == groupID).ToList();
        }

        public static void CheckDuplicateRoomID()
        {
            Debug.Log("Room Count: " + rooms.Count);
            HashSet<string> roomIDCheck = new();

            foreach (var item in rooms)
            {
                if (roomIDCheck.Contains(item.ID))
                {
                    Debug.Log("DuplicateRoomID: " + item.ID);
                    continue;
                }

                roomIDCheck.Add(item.ID);
            }
        }

        public static List<Room> GetAllRooms()
        {
            return rooms;
        }
        public static Room GetRoomByWall(WallLine wall)
        {
            foreach (Room room in RoomStorage.rooms)
            {
                if (room.wallLines.Contains(wall))
                    return room;
            }
            return null;
        }
        public static Room GetRoomByWallLine(WallLine wl)
        {
            return rooms.FirstOrDefault(r =>
                r.wallLines.Any(w => w.start == wl.start && w.end == wl.end));
        }
        public static void RemoveRoom(string id)
        {
            rooms.RemoveAll(r => r.ID == id);
        }
        public static void Clear()
        {
            rooms.Clear();
        }
    }
    