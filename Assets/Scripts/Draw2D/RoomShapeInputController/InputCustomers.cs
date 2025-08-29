using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DimensionOkHandler : MonoBehaviour
{
    [Header("Refs")]
    private RoomInfoDisplay roomInfoDisplay;
    private CheckpointManager checkpointManager;
    private DragFromButtonSpawnFloor spawnFloor;
    [SerializeField] private TMP_InputField inputLength;
    [SerializeField] private TMP_InputField inputWidth;
    [SerializeField] private Button buttonOk;

    private void Awake()
    {
        if (!roomInfoDisplay)   roomInfoDisplay  = FindFirstObjectByType<RoomInfoDisplay>();
        if (!checkpointManager) checkpointManager = FindFirstObjectByType<CheckpointManager>();
        if (!spawnFloor)        spawnFloor       = FindFirstObjectByType<DragFromButtonSpawnFloor>();
        if (buttonOk) buttonOk.onClick.AddListener(ApplyDimensions);
    }

    private void OnDestroy()
    {
        if (buttonOk) buttonOk.onClick.RemoveListener(ApplyDimensions);
    }

    // Gọi từ nút OK: tự lấy ID đang chọn. (Ưu tiên Room)
    private void ApplyDimensions()
    {
        if (checkpointManager == null) { Debug.LogWarning("[DimOK] checkpointManager NULL"); return; }

        string id = checkpointManager.GetSelectedRoomID(); // ưu tiên Room đang chọn
        if (string.IsNullOrEmpty(id))
        {
            if (roomInfoDisplay != null &&
                roomInfoDisplay.TryGetSelection(out RoomInfoDisplay.SelType kind, out string selId))
            {
                id = selId; // nếu không có Room đang chọn thì lấy từ selection (Floor/Room)
            }
        }

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[DimOK] Không có ID để cập nhật.");
            return;
        }

        ApplyDimensionsForId(id);
    }

    // === Hàm chính: CHỈ CẦN TRUYỀN ID ===
    public void ApplyDimensionsForId(string id)
    {
        // 1) Đọc L & W từ UI
        if (!TryParse(inputLength?.text, out float L) || !TryParse(inputWidth?.text, out float W))
        {
            Debug.LogWarning("[DimOK] Cần nhập đủ Chiều dài & Chiều rộng.");
            return;
        }

        // 2) Tìm đúng đối tượng theo ID (ưu tiên Room)
        Room room = RoomStorage.GetRoomByID(id);
        Floor floor = null;
        if (room == null && FloorStorage.floors != null)
        {
            for (int i = 0; i < FloorStorage.floors.Count; i++)
            {
                var f = FloorStorage.floors[i];
                if (f != null && f.ID == id) { floor = f; break; }
            }
        }
        if (room == null && floor == null)
        {
            Debug.LogWarning($"[DimOK] Không tìm thấy Room/Floor với ID={id}");
            return;
        }

        // 3) Lấy centroid từ hình hiện có (nếu không có thì (0,0))
        List<Vector2> basis = null;
        if (room  != null && room.checkpoints  != null && room.checkpoints.Count  >= 3) basis = room.checkpoints;
        if (floor != null && floor.checkpoints != null && floor.checkpoints.Count >= 3) basis ??= floor.checkpoints;

        Vector2 centroid = Vector2.zero;
        if (basis != null && basis.Count >= 3)
        {
            // centroid polygon (inline)
            float A = 0f, cx = 0f, cy = 0f;
            int n = basis.Count;
            for (int i = 0; i < n; i++)
            {
                var p = basis[i];
                var q = basis[(i + 1) % n];
                float cr = p.x * q.y - q.x * p.y;
                A  += cr;
                cx += (p.x + q.x) * cr;
                cy += (p.y + q.y) * cr;
            }
            A *= 0.5f;
            if (Mathf.Abs(A) > 1e-8f) { cx /= (6f * A); cy /= (6f * A); centroid = new Vector2(cx, cy); }
            else
            {
                for (int i = 0; i < basis.Count; i++) centroid += basis[i];
                centroid /= Mathf.Max(1, basis.Count);
            }
        }

        // 4) Tạo chữ nhật LxW (căn trục thế giới, KHÔNG xoay)
        float hx = L * 0.5f, hy = W * 0.5f;
        var rect = new List<Vector2>(4)
        {
            new Vector2(centroid.x - hx, centroid.y - hy),
            new Vector2(centroid.x + hx, centroid.y - hy),
            new Vector2(centroid.x + hx, centroid.y + hy),
            new Vector2(centroid.x - hx, centroid.y + hy)
        };

        // 5) Gán đúng nơi trùng ID (chỉ một nơi)
        if (room != null)
        {
            room.checkpoints = rect;
            // dọn dữ liệu phát sinh của ROOM
            room.wallLines?.Clear();
            room.extraCheckpoints?.Clear();
            Debug.Log($"[DimOK] Overwrite ROOM {id} -> {L}x{W}, area={L*W}");
        }
        else
        {
            floor.checkpoints = rect;
            Debug.Log($"[DimOK] Overwrite FLOOR {id} -> {L}x{W}, area={L*W}");
        }

        // 6) Redraw: ưu tiên vẽ floor theo state của spawnFloor, sau đó (nếu cần) rebuild toàn bộ
        if (spawnFloor != null)
        {
            spawnFloor.width = Mathf.Abs(L);
            spawnFloor.depth = Mathf.Abs(W);
            try
            {
                spawnFloor.LoadStateFromFloorId(id);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DimOK] spawnFloor.RedrawRectangleFromState() error: {e.Message}");
            }
        }

        // Nếu hệ thống của bạn vẫn cần dựng lại line/label theo RoomStorage thì giữ dòng này.
        checkpointManager?.RedrawAllRooms();
    }

    // parse số với ,/.
    private static bool TryParse(string s, out float v)
    {
        v = 0f;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace(',', '.');
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}
