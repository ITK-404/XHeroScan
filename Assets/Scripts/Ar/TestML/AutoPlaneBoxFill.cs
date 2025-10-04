using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARPlaneManager))]
public class AutoPlaneBoxFill : MonoBehaviour
{
    [Header("Managers (leave empty to auto-get)")]
    public ARPlaneManager planeManager; // optional

    [Header("Visual")]
    [Tooltip("Vật liệu tô màu. Nếu để trống sẽ tạo Unlit/Color màu xanh lá, Alpha=0.35.")]
    public Material fillMaterial;
    [Tooltip("Độ nhô (m) để tránh z-fighting với mesh của ARPlaneMeshVisualizer.")]
    public float zLift = 0.0015f;

    [Header("Stability (debounce)")]
    public int   requiredStableFrames = 5;
    public float areaDeltaThreshold   = 0.01f; // m^2
    public int   rmsSampleCount       = 16;
    public float rmsStableThreshold   = 0.01f; // m

    [Header("Simplify & Orthogonality")]
    [Tooltip("Sai số RDP cho boundary (m). 0.02–0.05 hợp lý.")]
    public float simplifyTolerance = 0.03f;

    [Tooltip("Bật ép hướng cạnh về 2 trục trực giao (Manhattan).")]
    public bool  enableManhattanSnap = true;
    [Range(0f,45f)] public float manhattanAngleTolDeg = 15f;
    [Tooltip("Khoảng cách gộp đỉnh sau khi snap (m).")]
    public float cornerMergeTol = 0.02f;

    [Header("Box Fit (rectangle)")]
    [Tooltip("Đóng khung VUÔNG (hình hộp) từ polygon đã simplify/snap.")]
    public bool  enableRectBoxFit = true;
    [Tooltip("Nở/thu hình hộp (m). Dương = nở, âm = co).")]
    public float rectInsetOutset = 0f;

    [Header("Filters")]
    [Tooltip("Chỉ xử lý plane nằm ngang.")]
    public bool filterHorizontalOnly = false;

    // === NEW: Corner/Line spawn ===
    [Header("Corner & Line Emission (Auto Place)")]
    [Tooltip("Prefab cho point (nếu trống sẽ tạo sphere runtime).")]
    public GameObject pointPrefab;
    [Tooltip("Vật liệu cho LineRenderer.")]
    public Material lineMaterial;
    [Tooltip("Kích thước point (đường kính, m).")]
    public float pointSize = 0.04f;
    [Tooltip("Độ dày line (m).")]
    public float lineWidth = 0.008f;
    [Tooltip("Ngưỡng gộp point (m) để tránh trùng khi plane hơi xê dịch).")]
    public float cornerMergeTolWorld = 0.03f;
    [Tooltip("Độ nhô cho point/line so với mặt phẳng (m).")]
    public float cornerZLift = 0.002f;

    private ARPlaneManager _planeMgr;

    private class PlaneState
    {
        public float lastArea;
        public int   stableCount;
        public List<Vector3> lastBoundarySample = new(); // world samples for RMS
        public GameObject fillGO;   // holds MeshFilter + MeshRenderer
        public Mesh fillMesh;

        // === NEW: store spawned corners & edges ===
        public GameObject cornersRoot;  // parent of points
        public GameObject edgesRoot;    // parent of lines
        public readonly List<Transform> cornerPoints = new();
        public readonly List<LineRenderer> edgeLines = new();
        public readonly List<Vector3> lastEmittedCornersWorld = new(); // dedupe
    }
    private readonly Dictionary<TrackableId, PlaneState> _states = new();

    void Awake()
    {
        _planeMgr = planeManager ?? GetComponent<ARPlaneManager>();
        if (!fillMaterial)
        {
            var shader = Shader.Find("Unlit/Color");
            fillMaterial = new Material(shader) { color = new Color(0f, 1f, 0f, 0.35f) };
        }
    }

    void OnEnable()  => _planeMgr.planesChanged += OnPlanesChanged;
    void OnDisable() => _planeMgr.planesChanged -= OnPlanesChanged;

    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        foreach (var p in args.removed) ClearPlane(p.trackableId);
        foreach (var p in args.added)   EnsureState(p);
        foreach (var p in args.updated) TryProcessPlane(p);
    }

    void EnsureState(ARPlane plane)
    {
        if (_states.ContainsKey(plane.trackableId)) return;
        var st = new PlaneState();

        // Tạo GO chứa mesh tô
        var go = new GameObject($"PlaneFill_{plane.trackableId}");
        go.transform.SetParent(plane.transform, false);
        go.transform.localPosition = new Vector3(0f, zLift, 0f);
        st.fillGO = go;
        st.fillMesh = new Mesh { name = "PlaneFillMesh" };
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = st.fillMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = fillMaterial;

        // NEW: roots cho points & lines
        st.cornersRoot = new GameObject("CornersRoot");
        st.cornersRoot.transform.SetParent(plane.transform, false);
        st.cornersRoot.transform.localPosition = new Vector3(0f, cornerZLift, 0f);

        st.edgesRoot = new GameObject("EdgesRoot");
        st.edgesRoot.transform.SetParent(plane.transform, false);
        st.edgesRoot.transform.localPosition = new Vector3(0f, cornerZLift, 0f);

        _states[plane.trackableId] = st;

        // Ẩn ARPlaneMeshVisualizer nếu muốn chỉ thấy “tô xanh”:
        var vis = plane.GetComponent<ARPlaneMeshVisualizer>();
        if (vis) vis.enabled = false;
        var mrPlane = plane.GetComponent<MeshRenderer>();
        if (mrPlane) mrPlane.enabled = false;
    }

    void ClearPlane(TrackableId id)
    {
        if (!_states.TryGetValue(id, out var st)) return;
        if (st.fillGO) Destroy(st.fillGO);
        if (st.cornersRoot) Destroy(st.cornersRoot);
        if (st.edgesRoot) Destroy(st.edgesRoot);
        _states.Remove(id);
    }

    void TryProcessPlane(ARPlane plane)
    {
        if (filterHorizontalOnly)
        {
            var a = plane.alignment;
            if (a != PlaneAlignment.HorizontalUp && a != PlaneAlignment.HorizontalDown) return;
        }

        var boundary = plane.boundary;
        if (!boundary.IsCreated || boundary.Length < 3) return;

        if (!_states.TryGetValue(plane.trackableId, out var st))
            EnsureState(plane);

        st = _states[plane.trackableId];

        // --------- Stability (area + RMS) ----------
        float area = PolygonArea(boundary);
        var sample = SampleBoundaryWorld(plane, boundary, Mathf.Max(4, rmsSampleCount));
        float rms  = (st.lastBoundarySample.Count == sample.Count) ? RmsShift(st.lastBoundarySample, sample) : float.MaxValue;

        bool stableNow = Mathf.Abs(area - st.lastArea) < areaDeltaThreshold && rms < rmsStableThreshold;
        st.stableCount = stableNow ? (st.stableCount + 1) : 0;
        st.lastArea = area;
        st.lastBoundarySample = sample;

        if (st.stableCount < requiredStableFrames) return;

        // --------- Simplify closed ----------
        var simplified = RdpSimplifyClosed(boundary, simplifyTolerance);
        if (simplified.Count < 3) return;

        // --------- Manhattan snap (optional) ----------
        if (enableManhattanSnap)
            simplified = SnapPolygonManhattanClosed(simplified, manhattanAngleTolDeg * Mathf.Deg2Rad, cornerMergeTol);
        if (simplified.Count < 3) return;

        // --------- Box Fit (rectangle) or keep polygon ----------
        List<Vector2> polyLocal;
        if (enableRectBoxFit)
        {
            var rectLocal = FitOrthogonalRectangle(simplified, rectInsetOutset, out _, out _);
            polyLocal = rectLocal; // 4 đỉnh (x,z) trong local plane
        }
        else
        {
            polyLocal = simplified; // đa giác sau snap
        }

        // --------- Build mesh (local coords) ----------
        BuildFillMesh(plane, st.fillMesh, polyLocal);

        // --------- NEW: Emit/Update corners + edges ----------
        EmitCornersAndEdges(plane, st, polyLocal);
    }

    // ================= Mesh building =================
    void BuildFillMesh(ARPlane plane, Mesh mesh, List<Vector2> polygonLocalXZ)
    {
        mesh.Clear();

        int n = polygonLocalXZ.Count;
        var verts = new Vector3[n];
        for (int i = 0; i < n; i++)
            verts[i] = new Vector3(polygonLocalXZ[i].x, 0f, polygonLocalXZ[i].y);

        int[] indices;
        if (n == 4)
        {
            indices = new int[] { 0, 1, 2, 0, 2, 3 };
        }
        else
        {
            indices = TriangulateEarClipping(polygonLocalXZ);
            if (indices == null || indices.Length < 3)
                return;
        }

        var uvs = new Vector2[n];
        for (int i = 0; i < n; i++)
            uvs[i] = polygonLocalXZ[i];

        mesh.vertices  = verts;
        mesh.triangles = indices;
        mesh.uv        = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        mesh.normals = normals;
    }

    // =================== NEW: Corners & Edges ===================
    void EmitCornersAndEdges(ARPlane plane, PlaneState st, List<Vector2> polyLocalXZ)
    {
        // Convert local (x,z) => world
        int n = polyLocalXZ.Count;
        if (n < 2) return;

        var worldCorners = new List<Vector3>(n);
        for (int i = 0; i < n; i++)
        {
            var local = new Vector3(polyLocalXZ[i].x, 0f, polyLocalXZ[i].y);
            var w = plane.transform.TransformPoint(local + Vector3.up * cornerZLift);
            worldCorners.Add(w);
        }

        // Update/Spawn points (one per vertex), de-dupe by distance
        EnsureCornerCount(st, worldCorners.Count);

        for (int i = 0; i < worldCorners.Count; i++)
        {
            var w = worldCorners[i];

            // nếu điểm i chưa có hoặc lệch nhiều => đặt/move
            if (st.cornerPoints[i] == null)
            {
                st.cornerPoints[i] = SpawnCornerPoint(st.cornersRoot.transform, w);
            }
            else
            {
                if (Vector3.Distance(st.cornerPoints[i].position, w) > cornerMergeTolWorld * 0.25f)
                    st.cornerPoints[i].position = w;
            }
        }

        // Dựng edges theo chu vi (i -> i+1)
        EnsureEdgeCount(st, worldCorners.Count);

        for (int i = 0; i < worldCorners.Count; i++)
        {
            int j = (i + 1) % worldCorners.Count;
            var p0 = st.cornerPoints[i].position;
            var p1 = st.cornerPoints[j].position;

            var lr = st.edgeLines[i];
            if (!lr)
            {
                lr = SpawnEdgeLine(st.edgesRoot.transform);
                st.edgeLines[i] = lr;
            }
            lr.positionCount = 2;
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);
        }

        // Cập nhật cache “đã phát”
        st.lastEmittedCornersWorld.Clear();
        st.lastEmittedCornersWorld.AddRange(worldCorners);
    }

    void EnsureCornerCount(PlaneState st, int target)
    {
        // tạo thiếu thì thêm, dư thì xóa cuối
        while (st.cornerPoints.Count < target)
            st.cornerPoints.Add(null);

        if (st.cornerPoints.Count > target)
        {
            for (int i = st.cornerPoints.Count - 1; i >= target; i--)
            {
                if (st.cornerPoints[i] != null)
                    Destroy(st.cornerPoints[i].gameObject);
                st.cornerPoints.RemoveAt(i);
            }
        }
        // đảm bảo root tồn tại
        if (!st.cornersRoot)
        {
            st.cornersRoot = new GameObject("CornersRoot");
        }
    }

    void EnsureEdgeCount(PlaneState st, int target)
    {
        while (st.edgeLines.Count < target)
            st.edgeLines.Add(null);

        if (st.edgeLines.Count > target)
        {
            for (int i = st.edgeLines.Count - 1; i >= target; i--)
            {
                if (st.edgeLines[i] != null)
                    Destroy(st.edgeLines[i].gameObject);
                st.edgeLines.RemoveAt(i);
            }
        }
        if (!st.edgesRoot)
        {
            st.edgesRoot = new GameObject("EdgesRoot");
        }
    }

    Transform SpawnCornerPoint(Transform parent, Vector3 worldPos)
    {
        GameObject go;
        if (pointPrefab)
        {
            go = Instantiate(pointPrefab, worldPos, Quaternion.identity, parent);
            go.transform.localScale = Vector3.one * pointSize;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Corner";
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * pointSize;
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                var shader = Shader.Find("Unlit/Color");
                mr.sharedMaterial = new Material(shader) { color = new Color(1f, 0.5f, 0f, 1f) }; // cam
            }
        }
        return go.transform;
    }

    LineRenderer SpawnEdgeLine(Transform parent)
    {
        var go = new GameObject("Edge");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.useWorldSpace = true;
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        if (lineMaterial)
        {
            lr.sharedMaterial = lineMaterial;
        }
        else
        {
            var shader = Shader.Find("Unlit/Color");
            lr.sharedMaterial = new Material(shader) { color = Color.yellow };
        }
        return lr;
    }

    // =================== Geometry helpers ===================
    static float PolygonArea(NativeArray<Vector2> poly)
    {
        double sum = 0;
        int n = poly.Length;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            sum += a.x * b.y - b.x * a.y;
        }
        return Mathf.Abs((float)(sum * 0.5));
    }

    static List<Vector3> SampleBoundaryWorld(ARPlane plane, NativeArray<Vector2> boundary, int k = 16)
    {
        var outPts = new List<Vector3>(k);
        if (!boundary.IsCreated || boundary.Length == 0) return outPts;

        for (int i = 0; i < k; i++)
        {
            float t = (i / (float)k) * boundary.Length;
            int idx = Mathf.Clamp(Mathf.RoundToInt(t) % boundary.Length, 0, boundary.Length - 1);
            var v = boundary[idx];
            outPts.Add(plane.transform.TransformPoint(new Vector3(v.x, 0f, v.y)));
        }
        return outPts;
    }

    static float RmsShift(List<Vector3> a, List<Vector3> b)
    {
        if (a == null || b == null || a.Count == 0 || a.Count != b.Count) return float.MaxValue;
        double s = 0;
        for (int i = 0; i < a.Count; i++) s += (a[i] - b[i]).sqrMagnitude;
        return Mathf.Sqrt((float)(s / a.Count));
    }

    static List<Vector2> RdpSimplifyClosed(NativeArray<Vector2> input, float tol)
    {
        if (!input.IsCreated || input.Length == 0)
            return new List<Vector2>();

        var pts = new List<Vector2>(input.Length + 1);
        for (int i = 0; i < input.Length; i++) pts.Add(input[i]);
        pts.Add(input[0]); // đóng vòng

        var outOpen = Rdp(pts, 0, pts.Count - 1, tol);
        if (outOpen.Count > 1 && outOpen[0] == outOpen[^1])
            outOpen.RemoveAt(outOpen.Count - 1);

        return outOpen;
    }

    static List<Vector2> Rdp(List<Vector2> pts, int start, int end, float tol)
    {
        if (end <= start + 1) return new List<Vector2> { pts[start], pts[end] };

        float maxDist = 0f; int idx = -1;
        var a = pts[start]; var b = pts[end];
        for (int i = start + 1; i < end; i++)
        {
            float d = PerpDistance(pts[i], a, b);
            if (d > maxDist) { maxDist = d; idx = i; }
        }

        if (maxDist > tol)
        {
            var left = Rdp(pts, start, idx, tol);
            var right = Rdp(pts, idx, end, tol);
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }
        else
        {
            return new List<Vector2> { a, b };
        }
    }

    static float PerpDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        if (a == b) return Vector2.Distance(p, a);
        Vector2 ab = b - a;
        return Mathf.Abs(ab.x * (a.y - p.y) - (a.x - p.x) * ab.y) / ab.magnitude;
    }

    // ======== Manhattan & Box-Fit ========
    static float Angle180(float rad)
    {
        float a = rad % Mathf.PI;
        if (a < 0) a += Mathf.PI;
        return a;
    }
    static float EdgeAngle(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        return Angle180(Mathf.Atan2(d.y, d.x));
    }
    static (float a0, float a1) DominantAxes(List<Vector2> poly)
    {
        if (poly == null || poly.Count < 2) return (0f, Mathf.PI * 0.5f);
        int bins = 36; float binSize = Mathf.PI / bins;
        int[] hist = new int[bins];
        for (int i = 0; i < poly.Count; i++)
        {
            float ang = EdgeAngle(poly[i], poly[(i + 1) % poly.Count]);
            int bin = Mathf.Clamp(Mathf.FloorToInt(ang / binSize), 0, bins - 1);
            hist[bin]++;
        }
        int maxBin = 0; for (int i = 1; i < bins; i++) if (hist[i] > hist[maxBin]) maxBin = i;
        float a0 = (maxBin + 0.5f) * binSize;
        float a1 = Angle180(a0 + Mathf.PI * 0.5f);
        return (a0, a1);
    }
    static float NearestAxis(float ang, float a0, float a1)
    {
        float d0 = Mathf.Min(Mathf.Abs(Angle180(ang - a0)), Mathf.Abs(Angle180(a0 - ang)));
        float d1 = Mathf.Min(Mathf.Abs(Angle180(ang - a1)), Mathf.Abs(Angle180(a1 - ang)));
        return (d0 <= d1) ? a0 : a1;
    }
    static void LineThroughPoint(float angle, Vector2 p, out Vector2 n, out float c)
    {
        n = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
        c = Vector2.Dot(n, p);
    }
    static bool IntersectLines(Vector2 n1, float c1, Vector2 n2, float c2, out Vector2 x)
    {
        float det = n1.x * n2.y - n1.y * n2.x;
        if (Mathf.Abs(det) < 1e-6f) { x = default; return false; }
        x = new Vector2( ( c1 * n2.y - n1.y * c2) / det,
                         (-c1 * n2.x + n1.x * c2) / det );
        return true;
    }
    static List<Vector2> MergeCloseCornersClosed(List<Vector2> poly, float tol)
    {
        if (poly == null || poly.Count == 0) return new List<Vector2>();
        var outPts = new List<Vector2>();
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 curr = poly[i];
            Vector2 prev = (outPts.Count > 0) ? outPts[^1] : poly[(i - 1 + poly.Count) % poly.Count];

            if (outPts.Count == 0) outPts.Add(curr);
            else
            {
                if ((curr - prev).magnitude < tol)
                    outPts[^1] = 0.5f * (prev + curr);
                else
                    outPts.Add(curr);
            }
        }
        if (outPts.Count >= 2 && (outPts[0] - outPts[^1]).magnitude < tol)
        {
            outPts[0] = 0.5f * (outPts[0] + outPts[^1]);
            outPts.RemoveAt(outPts.Count - 1);
        }
        return outPts;
    }
    static List<Vector2> RemoveCollinearClosed(List<Vector2> poly, float eps = 1e-5f)
    {
        if (poly == null || poly.Count < 3) return new List<Vector2>(poly);
        var outPts = new List<Vector2>(poly.Count);
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[(i - 1 + n) % n];
            Vector2 b = poly[i];
            Vector2 c = poly[(i + 1) % n];
            Vector2 ab = (b - a), bc = (c - b);
            float area2 = Mathf.Abs(ab.x * bc.y - ab.y * bc.x);
            if (area2 > eps) outPts.Add(b);
        }
        return outPts;
    }
    static List<Vector2> SnapPolygonManhattanClosed(List<Vector2> poly, float angleTolRad, float mergeTol)
    {
        if (poly == null || poly.Count < 3) return new List<Vector2>(poly);
        var (a0, a1) = DominantAxes(poly);
        int n = poly.Count;
        var snapped = new List<Vector2>(n);

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = poly[(i - 1 + n) % n];
            Vector2 curr = poly[i];
            Vector2 next = poly[(i + 1) % n];

            float angPrev = EdgeAngle(prev, curr);
            float angNext = EdgeAngle(curr, next);

            float sPrev = NearestAxis(angPrev, a0, a1);
            float sNext = NearestAxis(angNext, a0, a1);

            float dPrev = Mathf.Min(Mathf.Abs(Angle180(angPrev - a0)), Mathf.Abs(Angle180(angPrev - a1)));
            float dNext = Mathf.Min(Mathf.Abs(Angle180(angNext - a0)), Mathf.Abs(Angle180(angNext - a1)));
            bool okPrev = dPrev <= angleTolRad;
            bool okNext = dNext <= angleTolRad;

            if (okPrev && okNext && Mathf.Abs(Angle180(sPrev - sNext)) > 1e-3f)
            {
                LineThroughPoint(sPrev, curr, out var n1, out var c1);
                LineThroughPoint(sNext, curr, out var n2, out var c2);
                if (IntersectLines(n1, c1, n2, c2, out var x)) snapped.Add(x);
                else
                {
                    float delta = Vector2.Dot(n1, curr) - c1;
                    snapped.Add(curr - n1 * delta);
                }
            }
            else if (okPrev ^ okNext)
            {
                float s = okPrev ? sPrev : sNext;
                LineThroughPoint(s, curr, out var nUse, out var cUse);
                float delta = Vector2.Dot(nUse, curr) - cUse;
                snapped.Add(curr - nUse * delta);
            }
            else snapped.Add(curr);
        }

        snapped = MergeCloseCornersClosed(snapped, mergeTol);
        snapped = RemoveCollinearClosed(snapped, 1e-6f);
        return snapped;
    }
    static List<Vector2> FitOrthogonalRectangle(List<Vector2> poly, float insetOutset, out Vector2 u, out Vector2 v)
    {
        var (a0, _) = DominantAxes(poly);
        u = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
        v = new Vector2(-Mathf.Sin(a0), Mathf.Cos(a0));

        float uMin = float.PositiveInfinity, uMax = float.NegativeInfinity;
        float vMin = float.PositiveInfinity, vMax = float.NegativeInfinity;

        for (int i = 0; i < poly.Count; i++)
        {
            float pu = Vector2.Dot(poly[i], u);
            float pv = Vector2.Dot(poly[i], v);
            if (pu < uMin) uMin = pu; if (pu > uMax) uMax = pu;
            if (pv < vMin) vMin = pv; if (pv > vMax) vMax = pv;
        }

        if (Mathf.Abs(insetOutset) > 1e-6f)
        {
            uMin -= insetOutset; uMax += insetOutset;
            vMin -= insetOutset; vMax += insetOutset;
        }

        var p0 = u * uMin + v * vMin;
        var p1 = u * uMax + v * vMin;
        var p2 = u * uMax + v * vMax;
        var p3 = u * uMin + v * vMax;
        return new List<Vector2> { p0, p1, p2, p3 };
    }

    // ======== Triangulation (ear clipping) for simple polygons (CCW/CW both ok) ========
    static int[] TriangulateEarClipping(List<Vector2> poly)
    {
        int n = poly.Count;
        if (n < 3) return Array.Empty<int>();

        var V = new List<int>(n);
        for (int i = 0; i < n; i++) V.Add(i);

        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = poly[i], q = poly[(i + 1) % n];
            area += p.x * q.y - q.x * p.y;
        }
        bool ccw = area > 0f;

        var result = new List<int>();
        int guard = 0;

        while (V.Count > 2 && guard++ < 2000)
        {
            bool earFound = false;
            for (int i = 0; i < V.Count; i++)
            {
                int i0 = V[(i - 1 + V.Count) % V.Count];
                int i1 = V[i];
                int i2 = V[(i + 1) % V.Count];

                var a = poly[i0];
                var b = poly[i1];
                var c = poly[i2];

                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (ccw ? cross <= 1e-8f : cross >= -1e-8f) continue;

                bool anyInside = false;
                for (int j = 0; j < V.Count; j++)
                {
                    int vi = V[j];
                    if (vi == i0 || vi == i1 || vi == i2) continue;
                    if (PointInTri(poly[vi], a, b, c))
                    { anyInside = true; break; }
                }
                if (anyInside) continue;

                result.Add(i0); result.Add(i1); result.Add(i2);
                V.RemoveAt(i);
                earFound = true;
                break;
            }
            if (!earFound) break;
        }

        return result.ToArray();
    }

    static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        bool b1 = Sign(p, a, b) < 0.0f;
        bool b2 = Sign(p, b, c) < 0.0f;
        bool b3 = Sign(p, c, a) < 0.0f;
        return ((b1 == b2) && (b2 == b3));
    }
    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
