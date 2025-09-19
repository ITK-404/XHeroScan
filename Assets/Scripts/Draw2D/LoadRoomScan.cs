using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class LoadRoomScan : MonoBehaviour
{
    [Header("UI Setup")]
    public Transform contentParent;
    public GameObject dataItemPrefab;
    public GameObject panelLoadRooom;
    public GameObject popupErr;

    public void OnEnable()
    {
        if (panelLoadRooom != null)
        {
            panelLoadRooom.SetActive(true);
            LoadAllRooms();
        }
    }

    public void LoadAllRooms()
    {
        // Xóa UI cũ
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        for (int index = 0; index < RoomStorage.rooms.Count; index++)
        {
            Room room = RoomStorage.rooms[index];

            GameObject item = Instantiate(dataItemPrefab, contentParent);

            string displayName = room.roomName;
            string areaText = "Diện tích: " + GetRoomAreaString(room);

            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var txt in texts)
            {
                if (txt.name.Contains("Name"))
                    txt.text = displayName;
                else if (txt.name.Contains("Area") || txt.name.Contains("Date"))
                    txt.text = areaText;
            }

            Button btn = item.GetComponentInChildren<Button>();
            if (btn != null)
            {
                string capturedID = room.ID; // tránh closure
                btn.onClick.AddListener(() =>
                {
                    StartCoroutine(HandleRoomSelection(capturedID));
                });
            }
        }
    }

    private IEnumerator HandleRoomSelection(string capturedID)
    {
        // Kiểm tra AR khả dụng
        yield return ARSession.CheckAvailability();

        if (ARSession.state == ARSessionState.Unsupported ||
            ARSession.state == ARSessionState.None ||
            ARSession.state == ARSessionState.NeedsInstall)
        {
            Debug.LogWarning("Thiết bị không hỗ trợ AR hoặc cần cài đặt ARCore/ARKit.");
            if (popupErr != null) popupErr.SetActive(true);
            yield break;
        }

        // Nếu ok thì tiếp tục vào AR scene
        FurnitureManager.Instance.SaveRuntimesToTemp();
        UndoRedoController.EditRoomCommandCreator = new();
        UndoRedoController.EditRoomCommandCreator.TryAddChangedRoomID(capturedID);
        UndoRedoController.Instance.CreateTempUndoListToScanAR();
        PlayerPrefs.SetString("SelectedRoomID", capturedID);

        SceneManager.LoadScene("AR");
    }

    private string GetRoomAreaString(Room room)
    {
        float area = CalculatePolygonArea(room.checkpoints);
        return area.ToString("F2") + " m²";
    }

    private float CalculatePolygonArea(List<Vector2> points)
    {
        float area = 0f;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p1 = points[i];
            Vector2 p2 = points[(i + 1) % n];
            area += (p1.x * p2.y) - (p2.x * p1.y);
        }
        return Mathf.Abs(area * 0.5f);
    }
}
