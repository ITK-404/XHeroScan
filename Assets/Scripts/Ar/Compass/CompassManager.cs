using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class CompassManager : MonoBehaviour
{
    [Header("Object References")]
    public Camera ReferenceCamera;
    public TextMeshProUGUI compassObject;

    [Header("Options")]
    public bool useTrueNorth = true;     // True North (bù declination) hay Magnetic
    public int smoothWindow = 7;         // cửa sổ trung bình góc
    public float maxJumpPerFrame = 45f;  // ngưỡng loại outlier
    public float gravityAlpha = 0.1f;    // low-pass cho gravity fallback (0.05–0.2)
    public bool debugLogs = false;       // bật log chẩn đoán

    public static CompassManager Instance;

    // State
    public float Heading { get; private set; } // 0..360 (hiển thị)
    private readonly Queue<float> headingHistory = new Queue<float>();
    private float declinationDeg = 0f;          // bù từ sai
    private Vector3 gravityLP = Vector3.zero;   // cho fallback raw

    public float GetCurrentHeading() => Heading;

#if UNITY_ANDROID && !UNITY_EDITOR
    // Android native
    AndroidJavaObject sensorManager;
    AndroidJavaObject rotationVectorSensor;
    AndroidJavaObject context;
    AndroidJavaObject windowManager;
    RotationVectorListener listener;
    AndroidJavaClass sensorClass;
    AndroidJavaClass sensorManagerClass;
    bool androidRotationVectorOk = false;

    // Watchdog & buffer
    float lastRvCallbackTime = -1f;
    volatile float rvAzimuthPending = float.NaN; // finalDeg pending từ callback (true/magnetic)

    // Pitch flip state (hysteresis)
    bool isUpsideDown = false;
    const float ENTER_UPSIDE = 75f; // vào trạng thái lật khi |pitch| > 75°
    const float EXIT_UPSIDE  = 65f; // thoát khi |pitch| < 65°

    // SensorEventListener proxy
    public class RotationVectorListener : AndroidJavaProxy
    {
        private readonly System.Action<float[]> onValues;
        public RotationVectorListener(System.Action<float[]> cb)
            : base("android.hardware.SensorEventListener") { onValues = cb; }

        // PHẢI là public để Java gọi được
        public void onSensorChanged(AndroidJavaObject sensorEvent)
        {
            try
            {
                var values = sensorEvent.Get<float[]>("values");
                onValues?.Invoke(values);
            }
            catch { }
        }

        // PHẢI là public
        public void onAccuracyChanged(AndroidJavaObject sensor, int accuracy) { }
    }
#endif

    // void Awake() => Instance = this;
    void Awake()
{
    if (Instance != null && Instance != this)
    {
        Debug.LogWarning($"[Compass] Duplicate manager detected. Destroy {gameObject.name} (id={GetInstanceID()}).");
        Destroy(gameObject);
        return;
    }
    Instance = this;
    // Nếu cần dùng qua nhiều scene:
    // DontDestroyOnLoad(gameObject);
}


    void Start()
    {
#if UNITY_ANDROID
        // Cần quyền Location nếu muốn bù declination chuẩn (GeomagneticField)
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            StartCoroutine(WaitForPermissionThenReload());
            return;
        }
#endif
        StartCoroutine(InitializeAll());
    }

#if UNITY_ANDROID
    private IEnumerator WaitForPermissionThenReload()
    {
        while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            yield return null;

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
#endif

    private IEnumerator InitializeAll()
    {
        // Bật compass/location của Unity để có fallback & iOS trueHeading
        Input.compass.enabled = true;

        if (Input.location.isEnabledByUser)
        {
            Input.location.Start(1f, 0.1f);
            int wait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && wait-- > 0)
                yield return new WaitForSeconds(0.3f);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        InitAndroidRotationVector();
#endif
        UpdateDeclination(); // nếu có location -> declination chính xác
        yield break;
    }

    private void UpdateDeclination()
    {
        declinationDeg = 0f;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                var loc = Input.location.lastData;
                using (var geoObj = new AndroidJavaObject(
                    "android.hardware.GeomagneticField",
                    (float)loc.latitude, (float)loc.longitude, (float)loc.altitude, JavaCurrentTimeMillis()))
                {
                    declinationDeg = geoObj.Call<float>("getDeclination"); // degrees
                }
                if (debugLogs) Debug.Log($"[Compass] Declination={declinationDeg:F2}°");
            }
        }
        catch { declinationDeg = 0f; }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS: Input.compass.trueHeading đã là True North, nên không cần set declinationDeg
        // Giữ 0 để không cộng thêm lần nữa.
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void InitAndroidRotationVector()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            windowManager = context.Call<AndroidJavaObject>("getSystemService", "window");
            sensorManager = context.Call<AndroidJavaObject>("getSystemService", "sensor");
            sensorClass = new AndroidJavaClass("android.hardware.Sensor");
            sensorManagerClass = new AndroidJavaClass("android.hardware.SensorManager");

            int TYPE_ROTATION_VECTOR = sensorClass.GetStatic<int>("TYPE_ROTATION_VECTOR");
            int TYPE_GEOMAGNETIC_ROTATION_VECTOR = 20;

            rotationVectorSensor = sensorManager.Call<AndroidJavaObject>("getDefaultSensor", TYPE_ROTATION_VECTOR);
            if (rotationVectorSensor == null)
                rotationVectorSensor = sensorManager.Call<AndroidJavaObject>("getDefaultSensor", TYPE_GEOMAGNETIC_ROTATION_VECTOR);

            if (rotationVectorSensor != null)
            {
                listener = new RotationVectorListener(OnAndroidRotationVector);
                int SENSOR_DELAY_GAME = 1; // UI=2, GAME=1, FASTEST=0
                bool ok = sensorManager.Call<bool>("registerListener", listener, rotationVectorSensor, SENSOR_DELAY_GAME);
                androidRotationVectorOk = ok;
                if (debugLogs) Debug.Log($"[Compass/Android] listener registered: {ok}");
            }
            else
            {
                androidRotationVectorOk = false;
                if (debugLogs) Debug.LogWarning("[Compass/Android] No rotation-vector sensor.");
            }
        }
        catch (System.Exception e)
        {
            androidRotationVectorOk = false;
            if (debugLogs) Debug.LogWarning("[Compass/Android] Init error: " + e.Message);
        }
    }

    void OnDestroy()
    {
        if (sensorManager != null && listener != null)
            sensorManager.Call("unregisterListener", listener);
        if (Input.location.status == LocationServiceStatus.Running)
            Input.location.Stop();
    }

    private void OnAndroidRotationVector(float[] vec)
    {
        lastRvCallbackTime = Time.realtimeSinceStartup;

        // 1) RotationMatrix từ rotation-vector
        float[] R = new float[9];
        sensorManagerClass.CallStatic("getRotationMatrixFromVector", R, vec);

        // --- Constants đúng cho remap ---
        const int AXIS_X = 1;         // SensorManager.AXIS_X
        const int AXIS_Y = 2;         // SensorManager.AXIS_Y
        const int AXIS_MINUS_X = 129; // SensorManager.AXIS_MINUS_X (0x81)
        const int AXIS_MINUS_Y = 130; // SensorManager.AXIS_MINUS_Y (0x82)

        // 2) Remap theo xoay màn hình (giống app hệ thống)
        int rotation = windowManager.Call<AndroidJavaObject>("getDefaultDisplay").Call<int>("getRotation");
        int outX = AXIS_X;
        int outY = AXIS_Y;
        switch (rotation)
        {
            case 0: outX = AXIS_X;       outY = AXIS_Y;        break; // ROTATION_0
            case 1: outX = AXIS_Y;       outY = AXIS_MINUS_X;  break; // ROTATION_90
            case 2: outX = AXIS_MINUS_X; outY = AXIS_MINUS_Y;  break; // ROTATION_180
            case 3: outX = AXIS_MINUS_Y; outY = AXIS_X;        break; // ROTATION_270
        }

        float[] Rremap = new float[9];
        bool remapped = sensorManagerClass.CallStatic<bool>("remapCoordinateSystem", R, outX, outY, Rremap);
        if (!remapped && debugLogs) Debug.LogWarning("[Compass/Android] remapCoordinateSystem failed, using raw R");
        float[] Ruse = remapped ? Rremap : R;

        // 3) Orientation → azimuth/pitch
        float[] ori = new float[3];
        sensorManagerClass.CallStatic("getOrientation", Ruse, ori);
        float azimuthDeg = ori[0] * Mathf.Rad2Deg;
        float pitchDeg   = ori[1] * Mathf.Rad2Deg;

        if (azimuthDeg < 0) azimuthDeg += 360f;

        // Flip 180° khi pitch vượt ngưỡng (hysteresis)
        if (!isUpsideDown && Mathf.Abs(pitchDeg) > ENTER_UPSIDE) isUpsideDown = true;
        else if (isUpsideDown && Mathf.Abs(pitchDeg) < EXIT_UPSIDE) isUpsideDown = false;
        if (isUpsideDown) azimuthDeg = (azimuthDeg + 180f) % 360f;

        // 4) True/Magnetic
        float finalDeg = useTrueNorth ? (azimuthDeg + declinationDeg + 360f) % 360f
                                      : azimuthDeg;

        if (debugLogs) Debug.Log($"[Compass/Android] az={azimuthDeg:F1}°, pitch={pitchDeg:F1}°, flip={(isUpsideDown?1:0)}, decl={declinationDeg:F1}°, final={finalDeg:F1}°");

        // Đưa vào buffer, để Update() (main thread) lọc & cập nhật Heading
        rvAzimuthPending = finalDeg;
    }

    private long JavaCurrentTimeMillis()
    {
        using (var sys = new AndroidJavaClass("java.lang.System"))
            return sys.CallStatic<long>("currentTimeMillis");
    }
#endif

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Watchdog: nếu listener không callback >1s -> bật fallback
        if (androidRotationVectorOk && (Time.realtimeSinceStartup - lastRvCallbackTime) > 1.0f)
        {
            androidRotationVectorOk = false;
            if (debugLogs) Debug.LogWarning("[Compass/Android] No sensor callback >1s -> fallback to raw.");
        }

        if (androidRotationVectorOk)
        {
            // Áp dụng dữ liệu mới nếu callback đã ghi buffer
            if (!float.IsNaN(rvAzimuthPending))
            {
                ApplyFiltered(rvAzimuthPending);
                rvAzimuthPending = float.NaN;
            }
        }
        else
        {
            // Android: nếu rotation vector không có/đứt, fallback
            FallbackUpdateFromRaw();
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS: dùng trueHeading (đã tilt-compensated + declination khi location on)
        float deg = Input.compass.trueHeading; // 0..360
        ApplyFiltered(deg);
#else
        // Editor hoặc platform khác: fallback
        FallbackUpdateFromRaw();
#endif

if (compassObject != null)
{
    #if UNITY_EDITOR
    compassObject.text = $"{DirVN(Heading)}:{Heading:F1}° (id:{GetInstanceID()})";
    #else
    compassObject.text = $"{DirVN(Heading)}:{Heading:F1}°";
    #endif
}

    }

    // ==== Fallback: tilt compensation từ rawVector + accelerometer ====
    private void FallbackUpdateFromRaw()
    {
        Vector3 m = Input.compass.rawVector;
        if (m.sqrMagnitude < 1e-6f) return;

        gravityLP = Vector3.Lerp(gravityLP, Input.acceleration, Mathf.Clamp01(gravityAlpha));
        if (gravityLP.sqrMagnitude < 1e-6f) return;

        Vector3 a = gravityLP.normalized;         // "down"
        Vector3 mNorm = m.normalized;
        Vector3 h = (mNorm - Vector3.Dot(mNorm, a) * a).normalized;  // horizontal magnetic

        Vector3 east = Vector3.Cross(a, h).normalized;
        Vector3 north = Vector3.Cross(east, a).normalized;

        // Ở portrait, trục "trước" là +Y màn hình
        float azimuthDeg = Mathf.Atan2(east.y, north.y) * Mathf.Rad2Deg;
        azimuthDeg = AdjustForScreenOrientation(azimuthDeg);
        azimuthDeg = (azimuthDeg + 360f) % 360f;

        float finalDeg = useTrueNorth ? (azimuthDeg + declinationDeg + 360f) % 360f
                                      : azimuthDeg;

        if (debugLogs) Debug.Log($"[Compass/Fallback] azimuth={azimuthDeg:F1}°, decl={declinationDeg:F1}°, final={finalDeg:F1}°");
        ApplyFiltered(finalDeg);
    }

    // ==== Filtering ====
    private void ApplyFiltered(float newDeg)
    {
        if (headingHistory.Count > 0)
        {
            float last = Last(headingHistory);
            float delta = ShortestDeltaAngle(last, newDeg);
            if (Mathf.Abs(delta) > maxJumpPerFrame)
                newDeg = (last + Mathf.Sign(delta) * maxJumpPerFrame + 360f) % 360f;
        }
        while (headingHistory.Count >= Mathf.Max(3, smoothWindow)) headingHistory.Dequeue();
        headingHistory.Enqueue(newDeg);
        Heading = AverageAngles(headingHistory);
    }

    private static float Last(Queue<float> q) { float v = 0; foreach (var x in q) v = x; return v; }
    private static float ShortestDeltaAngle(float a, float b) => ((b - a + 540f) % 360f) - 180f;
    private static float AverageAngles(IEnumerable<float> angles)
    {
        float x = 0f, y = 0f;
        foreach (var angle in angles) { float r = angle * Mathf.Deg2Rad; x += Mathf.Cos(r); y += Mathf.Sin(r); }
        return (Mathf.Atan2(y, x) * Mathf.Rad2Deg + 360f) % 360f;
    }

    // ==== Utils ====
    private static float AdjustForScreenOrientation(float deg)
    {
        switch (Screen.orientation)
        {
            case ScreenOrientation.Portrait:             return (deg + 360f) % 360f;
            case ScreenOrientation.PortraitUpsideDown:   return (deg + 180f) % 360f;
            case ScreenOrientation.LandscapeLeft:        return (deg + 90f) % 360f;
            case ScreenOrientation.LandscapeRight:       return (deg + 270f) % 360f;
            case ScreenOrientation.AutoRotation:
            default:
                return (deg + 360f) % 360f;
        }
    }

    private string DirVN(float degree)
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
}
