using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PencilLine : MonoBehaviour
{
    public enum CommitMode { OnRelease, Live }

    [Header("Basic")]
    public bool drawingEnabled = false;
    public Camera targetCamera;

    [Header("Layering")]
    public int index = 2;
    public float layerStepY = 0.002f;
    public bool useIndexPlane = true;
    public float planeY = 0f;

    [Header("Line Settings")]
    public float lineWidth = 0.02f;
    public bool ignoreWhenPointerOverUI = true;

    [Header("Freehand Sampling")]
    public float minPointDistance = 0.02f; // giảm nhiễu
    public int   maxPoints = 4096;

    [Header("Commit Strategy")]
    public CommitMode commitMode = CommitMode.OnRelease;

    [Tooltip("Sai số RDP (m): càng lớn càng ít điểm (khuyên 0.15–0.30).")]
    public float rdpTolerance = 0.20f;

    // — Các tham số live (chỉ dùng khi commitMode = Live) —
    [Header("Live Commit (optional)")]
    public bool  commitToWalls = true;
    public float minSegmentLength = 0.12f;
    public float cornerAngleDeg  = 22f;
    public int   curvatureWindow = 7;
    public float commitMinSpacing = 0.25f;
    public int   commitCooldownMs = 150;
    public float minProminenceDeg = 8f;

    private LineRenderer line;
    private Plane drawPlane;
    private readonly List<Vector3> points = new();
    private readonly List<Vector3> committed = new();
    public static bool isDragging = false;

    private float cooldownUntilTime = 0f;
    private HandleCheckpointManger handler;
    private CheckpointManager cm;
    private bool hasOpenSegment = false;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        CreateLineRendererIfNeeded();
        ClearPreview();
    }

    void Start()
    {
        handler = FindFirstObjectByType<HandleCheckpointManger>();
        cm      = FindFirstObjectByType<CheckpointManager>();
        if (handler == null) Debug.LogError("[Pencil] Không thấy HandleCheckpointManger.");
        if (cm == null)      Debug.LogError("[Pencil] Không thấy CheckpointManager.");
    }

    void Update()
    {
        float y = useIndexPlane ? (index * layerStepY) : planeY;
        drawPlane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (!drawingEnabled || targetCamera == null) return;

        bool pointerDown=false, pointerHeld=false, pointerUp=false;
        Vector2 pointerPos = Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        if (Input.touchCount == 0)
        {
            pointerDown = Input.GetMouseButtonDown(0);
            pointerHeld = Input.GetMouseButton(0);
            pointerUp   = Input.GetMouseButtonUp(0);
            pointerPos  = Input.mousePosition;
        }
#endif
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            pointerPos = t.position;
            if (t.phase == TouchPhase.Began) pointerDown = true;
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) pointerHeld = true;
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) pointerUp = true;
        }

        if (ignoreWhenPointerOverUI && pointerDown)
        {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
#else
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject(0)) return;
#endif
        }

        if (pointerDown && TryScreenToWorldOnPlane(pointerPos, out var w0))
        {
            isDragging = true;
            points.Clear();
            committed.Clear();
            cooldownUntilTime = 0f;

if (handler != null) handler.ResumePlacing();

            AddPoint(w0, true);
            if (commitMode == CommitMode.Live) BeginOpenSegmentAt(w0);
            UpdateLineRenderer();
        }

        if (isDragging && pointerHeld && TryScreenToWorldOnPlane(pointerPos, out var w))
        {
            if (AddPoint(w))
            {
                if (commitMode == CommitMode.Live) TryCommitOnCurvaturePeak();
                UpdateLineRenderer();
            }
        }

        if (isDragging && pointerUp)
        {
            if (commitMode == CommitMode.OnRelease)
            {
                CommitStrokeOnRelease(); // <<<<<< quan trọng
            }
            else
            {
                TryFinalizeTailLive();
                EndOpenSegmentIfAny();
                handler?.StopPlacing();
            }

            isDragging = false;
            ClearPreview();
        }
    }

    // ===================== ON-RELEASE (RDP) =====================
    private void CommitStrokeOnRelease()
    {
        if (points.Count < 2) return;
        if (handler == null) return;

        // 1) Đơn giản hoá đường vẽ bằng RDP
        var simplified = RdpSimplify(points, rdpTolerance);
        if (simplified.Count < 2) return;

        // 2) Gọi HandleCheckpointManger theo polyline rút gọn
        //    -> rất ít điểm cho 1 cung
        handler.HandleSingleWallPlacement(simplified[0]); // set firstPoint
        for (int i = 1; i < simplified.Count; i++)
            handler.HandleSingleWallPlacement(simplified[i]); // chốt đoạn

        handler.StopPlacing();
    }

    // Ramer–Douglas–Peucker 3D (dùng khoảng cách vuông góc tới đoạn)
    private static List<Vector3> RdpSimplify(List<Vector3> input, float epsilon)
    {
        if (input == null || input.Count < 3) return new List<Vector3>(input);
        var idxKeep = new List<int> { 0, input.Count - 1 };

        Stack<(int, int)> st = new();
        st.Push((0, input.Count - 1));
        float eps2 = epsilon * epsilon;

        while (st.Count > 0)
        {
            var (start, end) = st.Pop();
            float maxDist2 = -1f; int maxIdx = -1;
            Vector3 A = input[start], B = input[end];

            // tiền tính cho khoảng cách điểm–đoạn
            Vector3 AB = B - A; float ab2 = AB.sqrMagnitude;

            for (int i = start + 1; i < end; i++)
            {
                float d2 = PointToSegmentDist2(input[i], A, B, ab2);
                if (d2 > maxDist2) { maxDist2 = d2; maxIdx = i; }
            }

            if (maxDist2 > eps2 && maxIdx >= 0)
            {
                st.Push((start, maxIdx));
                st.Push((maxIdx, end));
                idxKeep.Add(maxIdx);
            }
        }

        idxKeep.Sort();
        var outList = new List<Vector3>(idxKeep.Count);
        foreach (var idx in idxKeep) outList.Add(input[idx]);
        return outList;
    }

    private static float PointToSegmentDist2(Vector3 P, Vector3 A, Vector3 B, float ab2Pre)
    {
        float ab2 = ab2Pre > 1e-12f ? ab2Pre : (B - A).sqrMagnitude + 1e-12f;
        float t = Vector3.Dot(P - A, B - A) / ab2;
        t = Mathf.Clamp01(t);
        Vector3 H = A + t * (B - A);
        return (P - H).sqrMagnitude;
    }

    // ===================== LIVE (giữ khi bạn cần) =====================
    private void TryCommitOnCurvaturePeak()
    {
        int n = points.Count;
        int W = Mathf.Max(3, curvatureWindow | 1);
        if (n < W) return;

        int start = n - W, end = n - 1;

        float totalAngle = 0f;
        for (int i = start + 1; i <= end - 1; i++)
        {
            Vector3 a = points[i] - points[i - 1]; a.y = 0;
            Vector3 b = points[i + 1] - points[i]; b.y = 0;
            if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f) continue;
            totalAngle += Vector3.Angle(a, b);
        }

        Vector3 lastCommit = committed.Count > 0 ? committed[^1] : points[0];
        float lenFromCommit = Vector3.Distance(lastCommit, points[end]);
        if (totalAngle < cornerAngleDeg || lenFromCommit < minSegmentLength) return;

        int bestI = -1; float bestAng = -1f;
        float sideAvg = 0f; int sideCnt = 0;
        for (int i = start + 1; i <= end - 1; i++)
        {
            Vector3 a = points[i] - points[i - 1]; a.y = 0;
            Vector3 b = points[i + 1] - points[i]; b.y = 0;
            if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f) continue;
            float ang = Vector3.Angle(a, b);
            if (ang > bestAng) { bestAng = ang; bestI = i; }
            if (i <= start + 2 || i >= end - 2) { sideAvg += ang; sideCnt++; }
        }
        float baseAng = sideCnt > 0 ? sideAvg / sideCnt : 0f;
        float prominence = bestAng - baseAng;

        if (Time.unscaledTime < cooldownUntilTime) return;
        Vector3 peak = points[bestI];
        if (Vector3.Distance(lastCommit, peak) < commitMinSpacing) return;
        if (prominence < minProminenceDeg) return;

        CommitTo(peak);
        cooldownUntilTime = Time.unscaledTime + (commitCooldownMs / 1000f);
    }

    private void TryFinalizeTailLive()
    {
        if (!commitToWalls || !hasOpenSegment || points.Count < 2) return;
        Vector3 lastCommit = committed.Count > 0 ? committed[^1] : points[0];
        Vector3 end = points[^1];
        float len = Vector3.Distance(lastCommit, end);
        if (len < minSegmentLength || Vector3.Distance(lastCommit, end) < commitMinSpacing) return;
        CommitTo(end);
    }

    private void CommitTo(Vector3 worldPoint)
    {
        if (!commitToWalls) return;
        handler.HandleSingleWallPlacement(worldPoint);    // chốt đoạn
        committed.Add(worldPoint);
        BeginOpenSegmentAt(worldPoint);                   // mở đoạn mới
    }

    private void BeginOpenSegmentAt(Vector3 start)
    {
        if (!commitToWalls) return;
        handler.HandleSingleWallPlacement(start); // đặt firstPoint
        hasOpenSegment = true;
        committed.Add(start);
    }

    private void EndOpenSegmentIfAny() { hasOpenSegment = false; }

    private bool AddPoint(Vector3 p, bool force = false)
    {
        if (points.Count >= maxPoints) return false;
        if (force || points.Count == 0) { points.Add(p); return true; }
        if (Vector3.Distance(points[^1], p) >= minPointDistance) { points.Add(p); return true; }
        return false;
    }

    private void UpdateLineRenderer()
    {
        if (points.Count == 0) { line.positionCount = 0; return; }
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
    }

    private void ClearPreview()
    {
        points.Clear();
        committed.Clear();
        if (line != null) line.positionCount = 0;
    }

    private bool TryScreenToWorldOnPlane(Vector2 screenPos, out Vector3 world)
    {
        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        if (drawPlane.Raycast(ray, out float dist)) { world = ray.GetPoint(dist); return true; }
        world = Vector3.zero; return false;
    }

    public void ToggleDrawingMode()
    {
        drawingEnabled = !drawingEnabled;
        if (!drawingEnabled)
        {
            isDragging = false;
            EndOpenSegmentIfAny();
            ClearPreview();
        }
    }

    private void CreateLineRendererIfNeeded()
    {
        if (line != null) return;
        line = gameObject.GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();

        var mat = new Material(Shader.Find("Sprites/Default"));
        line.material = mat;
        line.startColor = Color.black;
        line.endColor   = Color.black;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.startWidth = lineWidth;
        line.endWidth   = lineWidth;
        line.sortingOrder = short.MaxValue;
    }
}
