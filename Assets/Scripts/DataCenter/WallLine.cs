using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;

#region Data Room & WallLine
/// <summary>
/// Một đoạn đường thẳng đại diện cho tường, cửa hoặc cửa sổ
/// </summary>
[System.Serializable]
public class WallLine
{
    public Vector3 start;
    public Vector3 end;
    public LineType type; // Wall, Door, Window

    public WallLine()
    {
        Debug.Log("[WallLine] Init no parameter");
    }
    public bool isVisible = true;

    public bool isManualConnection = false;// line phụ 

    public float headingCompass; // hướng thực địa của tường (tự động tính hoặc mặc định ở Draw2D)

    // Dùng cho toàn bộ Line
    public float distanceHeight = 0f;   // độ cao bắt đầu từ mặt đất = 0.5m
    public float Height = 0f; // Chiều cao tường = 2m, chiều cao của cửa / cửa sổ = 1m

    // Thêm: Vật liệu mặt trước / sau tường
    public string materialFront = "Default";
    public string materialBack = "Default";

    public WallLine(Vector3 start, Vector3 end, LineType type, float baseHeight = 0f, float height = 0f,
                    string frontMat = "Default", string backMat = "Default")
    {
        this.start = start;
        this.end = end;
        this.type = type;
        this.distanceHeight = baseHeight;
        this.Height = height;
        this.materialFront = frontMat;
        this.materialBack = backMat;
    }
    // Constructor clone
    public WallLine(WallLine other)
    {
        this.start = other.start;
        this.end = other.end;
        this.type = other.type;
        this.distanceHeight = other.distanceHeight;
        this.Height = other.Height;
        this.isVisible = other.isVisible;
        this.materialFront = other.materialFront;
        this.materialBack = other.materialBack;
        this.isManualConnection = other.isManualConnection;
    }
}

/// <summary> 
/// Một phòng hoặc khu vực được xác định bởi đa giác và các đoạn tường tương ứng
/// </summary>
[System.Serializable]
public class Room
{
    public static float Thickness = 0.1f; 
    private static int roomCounter = 0; // Biến đếm số lượng phòng

    public string ID { get; private set; }  // ID chỉ đọc từ bên ngoài
    public string groupID;
    public string roomName;
    public string floorID; // ID sàn liên kết (nếu có)
    public float thickness = 0.1f; // kích thước mặc định là 20 cm

    public Vector2 center; // tọa độ trung tâm phòng

    public List<Vector2> checkpoints = new List<Vector2>(); // polygon chính
    public List<Vector2> extraCheckpoints = new List<Vector2>(); // điểm lẻ trong phòng
    public List<WallLine> wallLines = new List<WallLine>();
    public List<float> heights = new List<float>();

    public Vector2 Compass = new Vector2();
    public float headingCompass; // hướng thực địa của phòng (theo la bàn)

    // Thêm: vật liệu sàn
    public string floorMaterial = "Default";
    public bool isShowFurniture = true;
    public Room()
    {
        ID = GenerateID(); // Tự tạo ID khi khởi tạo
        groupID = ID;

        roomName = "Room" + (++roomCounter);
    }

    private string GenerateID()
    {
        return Guid.NewGuid().ToString(); // ID ngẫu nhiên toàn cục (UUID)
    }

    // Constructor tạo phòng TRÊN một sàn cụ thể
    public Room(Floor floor) : this()
    {
        if (floor == null) throw new ArgumentNullException(nameof(floor));
        floorID = floor.ID;
    }

    public void SetID(string newID)
    {
        ID = newID;
    }

    // Constructor clone
    public Room(Room other)
    {
        ID = other.ID;
        groupID = other.groupID;
        roomName = other.roomName;
        headingCompass = other.headingCompass;
        Compass = other.Compass;
        floorMaterial = other.floorMaterial;
        floorID = other.floorID;

        checkpoints = new List<Vector2>(other.checkpoints);
        wallLines = new List<WallLine>(other.wallLines.Select(w => new WallLine(w)));
        extraCheckpoints = new List<Vector2>(other.extraCheckpoints);
        heights = new List<float>(other.heights);
    }
}

/// <summary>
/// Loại đường thẳng đại diện cho tường, cửa, hoặc cửa sổ
/// </summary>
public enum LineType
{
    Wall,
    Door,
    Window,
    None
}
#endregion

#region Floor
[System.Serializable]
public class FloorLine
{
    public Vector3 start;
    public Vector3 end;

    public FloorLine() { }

    public FloorLine(Vector3 start, Vector3 end)
    {
        this.start = start;
        this.end = end;
    }
    public FloorLine(FloorLine floorLine)
    {
        this.start = floorLine.start;
        this.end = floorLine.end;
    }
}
[System.Serializable]
public class Floor
{
    private static int floorCounter = 0; // Biến đếm số lượng phòng

    public string ID { get; private set; }  // ID chỉ đọc từ bên ngoài
    public string floorName;

    public Vector2 center; // tọa độ trung tâm sàn

    public List<Vector2> checkpoints = new List<Vector2>(); // polygon chính
    public List<FloorLine> floorLine = new List<FloorLine>();
    public List<float> heights = new List<float>();

    // Liên kết nhiều phòng với sàn này
    public List<string> roomIDs = new();

    public float width, length;

    public Floor()
    {
        ID = GenerateID(); // Tự tạo ID khi khởi tạo

        floorName = "Floor" + (++floorCounter);
    }

    // Tiện dùng: tạo Room mới trên chính sàn này
    public Room CreateRoom()
    {
        var r = new Room(this);        // gắn floorID = this.ID
        if (!roomIDs.Contains(r.ID)) 
            roomIDs.Add(r.ID);
        return r;
    }
    // Hoặc đăng ký Room có sẵn vừa tạo ở ngoài
    public void RegisterRoom(Room r)
    {
        if (r == null) return;
        if (r.floorID != ID) r.floorID = ID;
        if (!roomIDs.Contains(r.ID)) roomIDs.Add(r.ID);
    }

    private string GenerateID()
    {
        return Guid.NewGuid().ToString(); // ID ngẫu nhiên toàn cục (UUID)
    }

    public void SetID(string newID)
    {
        ID = newID;
    }

    public static Floor Clone(Floor other)
    {
        if(other == null )
        {
            Debug.Log("Bạn đang clone 1 object null");
            return null; 
        }
        Floor floor = new Floor();
        floor.ID = other.ID;
        floor.floorName = other.floorName;
        floor.center = other.center;
        floor.checkpoints = new List<Vector2>(other.checkpoints);
        floor.floorLine = new List<FloorLine>(other.floorLine.Select(w => new FloorLine(w)));
        floor.heights = new List<float>(other.heights);
        floor.roomIDs = new List<string>(other.roomIDs);
        floor.width = other.width;
        floor.length = other.length;
        return floor;
    }
}
#endregion