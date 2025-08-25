using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CenterUIDotHunter : MonoBehaviour
{
    [Range(0, 20)] public float centerTolerancePx = 4f;
    [Range(0, 20)] public float maxSizePx = 20f;
    [Range(0f, 1f)] public float magentaTolerance = 0.2f; // 0 = đúng #FF00FF

    void Start()
    {
        Debug.Log("[ERR][CenterUIDotHunter] Scan...");
        var canvases = FindObjectsOfType<Canvas>(true);
        foreach (var canvas in canvases)
        {
            var gr = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(); // chỉ để chắc canvas là UI
            foreach (var img in canvas.GetComponentsInChildren<Image>(true))
            {
                if (!img.isActiveAndEnabled) continue;
                var rt = img.rectTransform;

                // gần tâm canvas?
                var anchored = rt.anchoredPosition;
                if (Mathf.Abs(anchored.x) > centerTolerancePx || Mathf.Abs(anchored.y) > centerTolerancePx) continue;

                // rất nhỏ?
                var size = rt.rect.size;
                if (size.x > maxSizePx || size.y > maxSizePx) continue;

                // màu gần magenta?
                var c = img.color;
                var d = Mathf.Abs(c.r - 1f) + Mathf.Abs(c.g - 0f) + Mathf.Abs(c.b - 1f);
                if (d <= 3f * magentaTolerance) // càng nhỏ càng “đúng” magenta
                {
                    Debug.LogWarning($"[ERR][CenterUIDotHunter] UI Image nghi vấn: {GetPath(rt)}  size={size}  color={c}  anchored={anchored}");
                }
            }
        }
        Debug.Log("[ERR][CenterUIDotHunter] Done.");
    }

    private string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
public class WorldCenterDotHunter : MonoBehaviour
{
    public float radius = 0.1f;    // trong vòng bán kính này quanh (0,0,0)
    public float maxExtent = 0.2f; // kích thước rất nhỏ

    void Start()
    {
        Debug.Log("[ERR]WorldCenterDotHunter] Scan renderers near (0,0,0)...");
        var rends = FindObjectsOfType<Renderer>(true);
        foreach (var r in rends)
        {
            var pos = r.transform.position;
            if (pos.sqrMagnitude > radius * radius) continue;

            var b = r.bounds;
            var maxSize = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxSize <= maxExtent)
            {
                var matNames = "";
                foreach (var m in r.sharedMaterials) if (m) matNames += $"[{m.name}|{m.shader?.name}] ";
                Debug.LogWarning($"[ERR][WorldCenterDotHunter] {GetPath(r.transform)}  pos={pos}  size={b.size}  mats={matNames}");
            }
        }
        Debug.Log("[ERR][WorldCenterDotHunter] Done.");
    }

    private string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
public class CenterRayProbe : MonoBehaviour
{
    void Start()
    {
        var cam = Camera.main;
        if (!cam) { Debug.LogWarning("[ERR][CenterRayProbe] No main camera"); return; }

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // UI raycast
        var es = FindFirstObjectByType<EventSystem>();
        var results = new List<RaycastResult>();
        if (es)
        {
            var ped = new PointerEventData(es) { position = center };
            foreach (var gr in FindObjectsOfType<GraphicRaycaster>(true))
            {
                results.Clear();
                gr.Raycast(ped, results);
                foreach (var rr in results)
                {
                    Debug.Log($"[ERR][CenterRayProbe][UI] Hit: {GetPath(rr.gameObject.transform)} (graphic={rr.module.name})");
                }
            }
        }

        // 3D raycast
        var ray = cam.ScreenPointToRay(center);
        if (Physics.Raycast(ray, out var hit, 5000f))
        {
            Debug.Log($"[ERR][CenterRayProbe][3D] Hit: {GetPath(hit.transform)} at {hit.point}");
        }
        else
        {
            Debug.Log("[ERR][CenterRayProbe][3D] No hit");
        }
    }

    private string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}