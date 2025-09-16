using UnityEngine;
using TMPro;

public class CompassRoomManager : MonoBehaviour
{
    public GameObject compassLabelPrefab;
    public GameObject compassLabelPrefab2;
    public GameObject distanceTextPrefab;

    public void OnSetCompassDirectionForCurrentRoom()
    {
        if (RoomStorage.rooms == null || RoomStorage.rooms.Count == 0) return;

        int idx = RoomStorage.rooms.Count - 1;
        var room = RoomStorage.rooms[idx];          // <- copy
        float heading = CompassManager.Instance.GetCurrentHeading();

        room.headingCompass = heading;              // sửa trên copy
        RoomStorage.rooms[idx] = room;              // commit ngược vào list

        Debug.Log($"[Set Heading] idx={idx} heading={heading:0.0}°");
        Debug.Log($"[Verify] list value now = {RoomStorage.rooms[idx].headingCompass:0.0}°");
        CreateCompassLabel(heading);
    }


    private void CreateCompassLabel(float heading)
    {
        if (compassLabelPrefab == null)
        {
            Debug.LogWarning("Chưa gán compassLabelPrefab!");
            return;
        }
        Camera cam = Camera.main != null ? Camera.main : (Camera.allCameras.Length > 0 ? Camera.allCameras[0] : null);
        if (cam == null)
        {
            Debug.LogError("Không tìm thấy Camera.main");
            return;
        }
        if (RoomStorage.rooms == null || RoomStorage.rooms.Count == 0)
        {
            Debug.LogWarning("RoomStorage trống.");
            return;
        }

        Room currentRoom = RoomStorage.rooms[RoomStorage.rooms.Count - 1];

        // === Tính yaw của camera theo world và offset quy đổi world→true ===
        float camYaw = Mathf.Atan2(cam.transform.forward.x, cam.transform.forward.z) * Mathf.Rad2Deg;
        camYaw = (camYaw + 360f) % 360f;

        // Mọi góc đo theo world muốn quy về Bắc thật thì + northOffset
        // float northOffset = (heading - camYaw + 360f) % 360f;

        // --- Xác định vị trí spawn ---
        Vector3 camPos = cam.transform.position;
        Ray ray = new Ray(camPos, Vector3.down);
        Vector3 spawnPosition;
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            spawnPosition = hit.point + Vector3.up * 0.1f;
            currentRoom.Compass = new Vector2(spawnPosition.x, spawnPosition.z);
            Debug.Log($"Raycast hit ARPlane: {hit.point}");
        }
        else
        {
            spawnPosition = camPos + cam.transform.forward * 0.5f - Vector3.up * 0.3f;
            currentRoom.Compass = new Vector2(spawnPosition.x, spawnPosition.z);
            Debug.LogWarning("Không raycast được mặt sàn AR, fallback tại vị trí camera.");
        }

        // --- Tìm tường gần nhất ---
        WallLine nearestWall = null;
        Vector3 nearestPointOnWall = Vector3.zero;
        float minDistance = float.MaxValue;

        foreach (var wall in currentRoom.wallLines)
        {
            Vector3 closest = ClosestPointOnLine(wall.start, wall.end, spawnPosition);
            float dist = Vector3.Distance(spawnPosition, closest);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestWall = wall;
                nearestPointOnWall = closest;
            }
        }

        if (nearestWall != null)
        {
            // Góc tường theo WORLD
            Vector3 dir = nearestWall.end - nearestWall.start; dir.y = 0f;
            float angleWorld = 0f;
            if (dir.sqrMagnitude > 1e-9f)
            {
                angleWorld = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                angleWorld = (angleWorld + 360f) % 360f;
            }

            // Quy về BẮC THẬT bằng offset world→true
            // float wallTrueHeading = (angleWorld + northOffset) % 360f;
            // nearestWall.headingCompass = heading;

            // --- Xoá các nhãn cũ ---
            foreach (Transform child in transform)
                if (child.name.Contains("CompassLabel")) Destroy(child.gameObject);
            foreach (Transform child in transform)
                if (child.name.Contains("CompassLabel2")) Destroy(child.gameObject);

            float yawForNorthWorld = (camYaw - heading + 360f) % 360f;

            Quaternion lookRotation = Quaternion.Euler(90f, yawForNorthWorld, 135f);

            GameObject label = Instantiate(
                compassLabelPrefab,
                spawnPosition,
                lookRotation,
                transform
            );
            label.name = "CompassLabel";

            if (compassLabelPrefab2 != null)
            {
                GameObject label2 = Instantiate(
                    compassLabelPrefab2,
                    spawnPosition,
                    lookRotation,
                    transform
                );
                label2.name = "CompassLabel2";
                SetLayerRecursively(label2, LayerMask.NameToLayer("PreviewModel"));
            }
        }
        else
        {
            Debug.LogWarning("Không tìm thấy tường gần nhất để hướng mũi tên.");
        }
    }

    // Hàm bổ trợ tìm điểm gần nhất trên đoạn thẳng
    private Vector3 ClosestPointOnLine(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }
    
    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
