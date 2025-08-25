using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Default visuals")]
    public Material floorMat;

    // Track tất cả floor đã spawn trong phiên Play
    private readonly List<GameObject> _spawnedFloors = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ====== Shader helpers ======
    private static Shader FindLitShader()
    {
        if (GraphicsSettings.currentRenderPipeline != null &&
            GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"))
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null) return sh;
        }
        if (GraphicsSettings.currentRenderPipeline != null &&
            GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("HD"))
        {
            var sh = Shader.Find("HDRP/Lit");
            if (sh != null) return sh;
        }
        return Shader.Find("Standard");
    }

    private static Shader FindUnlitColorShader()
    {
        if (GraphicsSettings.currentRenderPipeline != null &&
            GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"))
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null) return sh;
        }
        var s1 = Shader.Find("Unlit/Color");
        if (s1 != null) return s1;
        return Shader.Find("Sprites/Default");
    }

    /// <summary>
    /// Sinh floor và TRẢ VỀ root GameObject (để caller giữ reference).
    /// </summary>
    public GameObject PlaceRectFloor(
        Vector3 center,
        float width,
        float depth,
        float yawDeg,
        GameObject checkpointPrefab,
        Material lineMaterial,
        float lineWidth = 0.03f)
    {
        var floorRoot = new GameObject($"Floor_{DateTime.Now:HHmmssfff}");
        floorRoot.transform.position = center;

        Vector3[] localCorners =
        {
            new Vector3(-width/2f, 0f, -depth/2f),
            new Vector3( width/2f, 0f, -depth/2f),
            new Vector3( width/2f, 0f,  depth/2f),
            new Vector3(-width/2f, 0f,  depth/2f),
        };

        Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);

        Vector3[] corners = new Vector3[4];
        for (int i = 0; i < 4; i++)
            corners[i] = center + rot * localCorners[i];

        Vector3[] mids = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[(i + 1) % 4];
            mids[i] = (a + b) * 0.5f;
        }

        var allPts = new List<Vector3>(8);
        allPts.AddRange(corners);
        allPts.AddRange(mids);

        var cpsParent = new GameObject("Checkpoints");
        cpsParent.transform.SetParent(floorRoot.transform, true);

        foreach (var p in allPts)
        {
            GameObject cp = null;
            if (checkpointPrefab != null)
            {
                cp = Instantiate(checkpointPrefab, p, Quaternion.identity, cpsParent.transform);
                cp.SetActive(true);
            }
            else
            {
                cp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cp.transform.SetParent(cpsParent.transform, true);
                cp.transform.position = p;
                cp.transform.localScale = Vector3.one * 0.2f;
                var col = cp.GetComponent<Collider>(); if (col) Destroy(col);

                var mr = cp.GetComponent<MeshRenderer>();
                if (mr)
                {
                    var lit = FindLitShader();
                    var m = new Material(lit != null ? lit : Shader.Find("Standard"));
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.2f, 0.6f, 1f, 1f));
                    if (m.HasProperty("_Color"))     m.SetColor("_Color",     new Color(0.2f, 0.6f, 1f, 1f));
                    mr.sharedMaterial = m;
                }
            }
            cp.name = "Checkpoint";
        }

        // LineRenderer viền
        var lineGO = new GameObject("Outline");
        lineGO.transform.SetParent(floorRoot.transform, true);
        var lr = lineGO.AddComponent<LineRenderer>();
        lr.positionCount = 5;
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;

        if (lineMaterial == null)
        {
            var unlit = FindUnlitColorShader();
            lineMaterial = new Material(unlit != null ? unlit : Shader.Find("Unlit/Color"));
            if (lineMaterial.HasProperty("_BaseColor")) lineMaterial.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 1f));
            if (lineMaterial.HasProperty("_Color"))     lineMaterial.SetColor("_Color",     new Color(0.1f, 0.1f, 0.1f, 1f));
        }
        lr.sharedMaterial = lineMaterial;

        lr.SetPosition(0, corners[0]);
        lr.SetPosition(1, corners[1]);
        lr.SetPosition(2, corners[2]);
        lr.SetPosition(3, corners[3]);
        lr.SetPosition(4, corners[0]);

        // Sàn (Quad)
        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Floor";
        floor.transform.SetParent(floorRoot.transform, true);
        floor.transform.position = center;
        floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f) * rot;
        floor.transform.localScale = new Vector3(width, depth, 1f);
        var floorMr = floor.GetComponent<MeshRenderer>();
        if (floorMr)
        {
            if (floorMat == null)
            {
                var lit = FindLitShader();
                floorMat = new Material(lit != null ? lit : Shader.Find("Standard"));
                if (floorMat.HasProperty("_BaseColor")) floorMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.9f, 1f));
                if (floorMat.HasProperty("_Color"))     floorMat.SetColor("_Color",     new Color(0.85f, 0.85f, 0.9f, 1f));
            }
            floorMr.sharedMaterial = floorMat;
        }
        var floorCol = floor.GetComponent<Collider>(); if (floorCol) Destroy(floorCol);

        _spawnedFloors.Add(floorRoot);
        return floorRoot;
    }

    public void DestroyFloor(GameObject floor)
    {
        if (!floor) return;
        _spawnedFloors.Remove(floor);
        Destroy(floor);
    }

    public void DestroyLastFloor()
    {
        if (_spawnedFloors.Count == 0) return;
        var go = _spawnedFloors[_spawnedFloors.Count - 1];
        _spawnedFloors.RemoveAt(_spawnedFloors.Count - 1);
        if (go) Destroy(go);
    }

    public void DestroyAllFloors()
    {
        for (int i = _spawnedFloors.Count - 1; i >= 0; i--)
        {
            var go = _spawnedFloors[i];
            if (go) Destroy(go);
        }
        _spawnedFloors.Clear();
    }
}
