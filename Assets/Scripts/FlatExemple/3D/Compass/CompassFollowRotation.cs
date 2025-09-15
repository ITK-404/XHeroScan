using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CompassFollowRotation : MonoBehaviour
{
    public Transform roomModel;
    public Image compassImage;
    public TextMeshProUGUI compassText;

    private Room currentRoom;

    private bool hasSetInitialOffset = false;
    private float initialOffsetAngle = 0f;
    private Quaternion originalRotation;

    void Start()
    {
        List<Room> rooms = RoomStorage.rooms;

        // ✅ Sửa thứ tự kiểm tra để tránh rooms[0] khi rỗng
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("Không có Room nào trong RoomStorage.");
            return;
        }

        currentRoom = rooms[0];
        if (compassImage != null)
            originalRotation = compassImage.rectTransform.rotation;

        // ✅ Tính và LƯU hướng thực địa cho tất cả tường ngay lúc khởi động
        UpdateWallDirections(currentRoom);
    }

    void Update()
    {
        if (roomModel == null || compassImage == null || currentRoom == null)
            return;

        WallLine facingWall = GetMostFacingWall(currentRoom);
        if (facingWall != null)
        {
            // ——— Tính hướng thực địa của tường đang "đối diện" (và LƯU lại) ———
            Vector3 dir = (facingWall.end - facingWall.start);
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-8f)
            {
                float angleToNorth = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; // yaw world
                float realWorldAngle = (angleToNorth + currentRoom.headingCompass + 360f) % 360f;

                // ✅ LƯU vào wallLine tương ứng
                facingWall.headingCompass = realWorldAngle;
            }

            // ——— Xoay kim la bàn dựa trên offset ban đầu và yaw của model ———
            float yRotation = NormalizeAngle(roomModel.eulerAngles.y);

            if (!hasSetInitialOffset)
            {
                // Lưu offset ban đầu giữa yRotation và realWorldAngle của tường facing (nếu cần)
                // Nếu muốn kim độc lập tường, bạn có thể thay bằng công thức northYawWorld - yRotation
                // nhưng ở đây giữ nguyên logic sẵn có của bạn.
                Vector3 dir0 = (facingWall.end - facingWall.start); dir0.y = 0f;
                float angleToNorth0 = Mathf.Atan2(dir0.x, dir0.z) * Mathf.Rad2Deg;
                float realWorldAngle0 = (angleToNorth0 + currentRoom.headingCompass + 360f) % 360f;

                initialOffsetAngle = yRotation - realWorldAngle0;
                hasSetInitialOffset = true;
            }

            float compassAngle = yRotation - initialOffsetAngle;

            if (compassImage != null)
                compassImage.rectTransform.localRotation = Quaternion.AngleAxis(-compassAngle, Vector3.forward);

            float normalized = (360f - compassAngle + 360f) % 360f; // đảo vì UI Z ngược
            string label = AngleToDirectionLabel(normalized);
            if (compassText != null)
                compassText.text = $"{normalized:F1}° ({label})";
        }
        else
        {
            if (compassText != null)
                compassText.text = "N/A";
        }
    }

    /// <summary>
    /// ✅ Tính và LƯU bearing thực địa cho TẤT CẢ wallLines của room.
    /// Gọi hàm này sau khi hiệu chuẩn heading phòng hoặc sau khi thay đổi hình học tường.
    /// </summary>
    private void UpdateWallDirections(Room room)
    {
        if (room == null || room.wallLines == null) return;

        foreach (WallLine line in room.wallLines)
        {
            Vector3 dir = (line.end - line.start);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-8f)
            {
                line.headingCompass = 0f; // hoặc giữ nguyên nếu muốn
                continue;
            }

            // Góc hình học so với trục Z+ của thế giới (0°=Z+)
            float angleToNorth = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            // Cộng offset phòng để ra "hướng thực địa" (0°=Bắc, 90°=Đông, ...)
            float realWorldAngle = (angleToNorth + room.headingCompass + 360f) % 360f;

            // ✅ LƯU vào wallLine
            line.headingCompass = realWorldAngle;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string directionLabel = AngleToDirectionLabel(realWorldAngle);
            Debug.Log($"[WallDir] {line.start} -> {line.end} = {realWorldAngle:0.0}° ({directionLabel})");
#endif
        }
    }

    /// <summary>
    /// Tìm tường có "hướng nhìn" gần với hướng camera nhất (xấp xỉ).
    /// (Giữ nguyên logic hiện có, nếu muốn chính xác hơn dùng pháp tuyến tường.)
    /// </summary>
    private WallLine GetMostFacingWall(Room room)
    {
        if (Camera.main == null || room.wallLines == null || room.wallLines.Count == 0)
            return null;

        Vector3 cameraForward = Camera.main.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        float maxDot = -1f;
        WallLine bestWall = null;

        foreach (var wall in room.wallLines)
        {
            Vector3 wallDir = (wall.end - wall.start);
            wallDir.y = 0;
            if (wallDir.sqrMagnitude < 1e-8f) continue;

            wallDir.Normalize();
            float dot = Vector3.Dot(-wallDir, cameraForward); // gần "đối diện"

            if (dot > maxDot)
            {
                maxDot = dot;
                bestWall = wall;
            }
        }
        return bestWall;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    private string AngleToDirectionLabel(float degree)
    {
        if (degree < 0) degree += 360;

        if ((degree >= 0 && degree < 7.5f) || degree >= 352.5f) return "Bắc";
        if (degree < 22.5f) return "Bắc";
        if (degree < 37.5f) return "Đông Bắc";
        if (degree < 52.5f) return "Đông Bắc";
        if (degree < 67.5f) return "Đông Bắc";
        if (degree < 82.5f) return "Đông";
        if (degree < 97.5f) return "Đông";
        if (degree < 112.5f) return "Đông";
        if (degree < 127.5f) return "Đông Nam";
        if (degree < 142.5f) return "Đông Nam";
        if (degree < 157.5f) return "Đông Nam";
        if (degree < 172.5f) return "Nam";
        if (degree < 187.5f) return "Nam";
        if (degree < 202.5f) return "Nam";
        if (degree < 217.5f) return "Tây Nam";
        if (degree < 232.5f) return "Tây Nam";
        if (degree < 247.5f) return "Tây Nam";
        if (degree < 262.5f) return "Tây";
        if (degree < 277.5f) return "Tây";
        if (degree < 292.5f) return "Tây";
        if (degree < 307.5f) return "Tây Bắc";
        if (degree < 322.5f) return "Tây Bắc";
        if (degree < 337.5f) return "Tây Bắc";
        return "Bắc";
    }

    // —————— (tùy chọn) ——————
    // Nếu bạn muốn bên ngoài gọi lại để cập nhật toàn bộ khi offset phòng thay đổi:
    public void RefreshAllWallHeadings()
    {
        if (currentRoom != null) UpdateWallDirections(currentRoom);
    }
}
