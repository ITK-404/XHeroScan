using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class RoomShapeInputController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField sidesInputField;   // Số cạnh
    public TMP_InputField lengthInputField;  // Chiều dài cạnh (m)
    public Button createButton;          // Nút "Tạo Room"
    public GameObject targetPanel; // Panel đóng

    [Header("References")]
    public CheckpointManager checkpointManager; // Script vẽ

    void Start()
    {
        if (createButton != null)
            createButton.onClick.AddListener(OnCreateRoomClicked);
        else
            Debug.LogError("Chưa gán CreateButton!");
    }

    void OnCreateRoomClicked()
    {
        if (checkpointManager == null)
        {
            Debug.LogError("CheckpointManager chưa gán!");
            return;
        }

        // === Lấy số cạnh ===
        int sides = 0;
        if (!int.TryParse(sidesInputField.text, out sides) || sides < 3)
        {
            Debug.LogWarning("Số cạnh không hợp lệ! (>=3)");
            PopupController.Show("Số cạnh không hợp lệ! (>=3)", null);
            return;
        }

        // === Lấy chiều dài cạnh ===
        float length = 0f;
        if (!float.TryParse(lengthInputField.text, out length) || length <= 0)
        {
            Debug.LogWarning("Chiều dài cạnh không hợp lệ! (>0)");
            PopupController.Show("Chiều dài cạnh không hợp lệ! (>0)", null);
            return;
        }

        // === Truyền camera (nếu chưa gán sẵn) ===
        if (checkpointManager.drawingCamera == null)
            checkpointManager.drawingCamera = Camera.main;

        // === Tạo Room tự động ===
        CreateRegularPolygonRoom(sides, length);

        Debug.Log($"[RoomShapeInputController] Gửi yêu cầu tạo Room {sides} cạnh, cạnh dài {length}m");
        targetPanel.SetActive(false);
    }
    public void CreateRegularPolygonRoom(int sides, float edgeLength)
    {
        if (sides < 3)
        {
            Debug.LogError("Số cạnh phải >= 3");
            return;
        }

        if (edgeLength <= 0)
        {
            Debug.LogError("Chiều dài cạnh phải > 0");
            return;
        }

        // Tìm center trên mặt phẳng y=0 theo camera
        Camera cam = checkpointManager.drawingCamera != null ? checkpointManager.drawingCamera : Camera.main;
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float enter = 0;
        Vector3 center = Vector3.zero;

        if (groundPlane.Raycast(ray, out enter))
        {
            center = ray.GetPoint(enter);
        }
        else
        {
            Debug.LogError("Không raycast được xuống mặt phẳng y=0");
            return;
        }

        Debug.Log($"Tâm room tại: {center}");

        // Tính bán kính từ chiều dài cạnh
        float radius = edgeLength / (2 * Mathf.Sin(Mathf.PI / sides));
        Debug.Log($"Bán kính: {radius}");

        // Tạo các checkpoint prefab
        float angleOffset = Mathf.PI / 2; // Quay để cạnh đầu hướng lên
        for (int i = 0; i < sides; i++)
        {
            float angle = 2 * Mathf.PI * i / sides + angleOffset;
            float x = center.x + radius * Mathf.Cos(angle);
            float z = center.z + radius * Mathf.Sin(angle);
            Vector3 pos = new Vector3(x, 0, z);

            var cp = Instantiate(checkpointManager.checkpointPrefab, pos, Quaternion.identity);
            checkpointManager.currentCheckpoints.Add(cp);
        }

        // Tạo wallLines & vẽ line
        for (int i = 0; i < checkpointManager.currentCheckpoints.Count; i++)
        {
            Vector3 p1 = checkpointManager.currentCheckpoints[i].transform.position;
            Vector3 p2 = (i == checkpointManager.currentCheckpoints.Count - 1)
                ? checkpointManager.currentCheckpoints[0].transform.position
                : checkpointManager.currentCheckpoints[i + 1].transform.position;

            checkpointManager.DrawLineAndDistance(p1, p2);
            checkpointManager.wallLines.Add(new WallLine(p1, p2, LineType.Wall));
        }

        // Tạo Room & lưu
        Room newRoom = new Room();
        foreach (GameObject cp in checkpointManager.currentCheckpoints)
        {
            Vector3 pos = cp.transform.position;
            pos.y = 0f;
            newRoom.checkpoints.Add(new Vector2(pos.x, pos.z));
        }

        if (MeshGenerator.CalculateArea(newRoom.checkpoints) > 0)
        {
            newRoom.checkpoints.Reverse();
            Debug.Log("Đã đảo chiều polygon để mesh đúng mặt.");
        }

        newRoom.wallLines.AddRange(checkpointManager.wallLines);

        RoomStorage.rooms.Add(newRoom);

        // Tạo mesh sàn
        GameObject floorGO = new GameObject($"RoomFloor_{newRoom.ID}");
        RoomMeshController meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(newRoom.ID);

        // Ánh xạ loop
        List<GameObject> loopRef = new List<GameObject>(checkpointManager.currentCheckpoints);
        checkpointManager.AllCheckpoints.Add(loopRef);
        checkpointManager.loopMappings.Add(new LoopMap(newRoom.ID, loopRef));

        checkpointManager.currentCheckpoints.Clear();
        checkpointManager.wallLines.Clear();

        Debug.Log($"Đã tạo Room tự động: {sides} cạnh, cạnh dài ~{edgeLength}m, RoomID: {newRoom.ID}");
    }
}
