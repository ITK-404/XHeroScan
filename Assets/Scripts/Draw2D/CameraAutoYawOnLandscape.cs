using UnityEngine;
using System.Collections;

public class CameraKeepUpright2D : MonoBehaviour
{
    [Header("Camera để xoay (2D)")]
    public Camera targetCam;

    [Header("Xoay mượt?")]
    public bool smoothRotate = true;
    public float rotateSpeedDegPerSec = 360f;
    public float snapEpsilon = 0.5f;

    [Header("Debug")]
    public bool logDebug = false;

    private DeviceOrientation lastOri = DeviceOrientation.Unknown;
    private Quaternion baseRot;     // góc gốc của camera (giữ nguyên roll/pitch)
    private float baseYaw;          // yaw gốc (theo euler Y)
    private Coroutine rotateCo;

    void Awake()
    {
        if (targetCam == null) targetCam = GetComponent<Camera>();
        if (targetCam == null) targetCam = Camera.main;
    }

    void Start()
    {
        // bật autorotation để HĐH cập nhật orientation
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToLandscapeLeft  = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait       = true;
        Screen.autorotateToPortraitUpsideDown = true;

        baseRot = targetCam.transform.rotation;
        baseYaw = baseRot.eulerAngles.y;

        lastOri = Input.deviceOrientation;
        // set ngay góc tương ứng hiện tại
        ApplyOrientationImmediate(GetTargetYawFor(lastOri));
    }

    void Update()
    {
        if (!targetCam) return;

        var cur = Input.deviceOrientation;
        if (cur == DeviceOrientation.Unknown ||
            cur == DeviceOrientation.FaceUp ||
            cur == DeviceOrientation.FaceDown)
            return;

        if (cur != lastOri)
        {
            float targetYaw = GetTargetYawFor(cur);
            RotateToYaw(targetYaw);
            if (logDebug) Debug.Log($"[2DKeep] {lastOri} -> {cur}, yaw={targetYaw}");
            lastOri = cur;
        }
    }

    // Map trạng thái máy -> yaw mong muốn (tính từ baseYaw)
    private float GetTargetYawFor(DeviceOrientation ori)
    {
        switch (ori)
        {
            case DeviceOrientation.LandscapeLeft:   return baseYaw + 90f;  // máy xoay trái -> xoay cam +90
            case DeviceOrientation.LandscapeRight:  return baseYaw - 90f;  // máy xoay phải -> xoay cam -90
            case DeviceOrientation.PortraitUpsideDown: return baseYaw + 180f;
            case DeviceOrientation.Portrait:
            default:                               return baseYaw;         // đứng dọc -> về góc gốc
        }
    }

    private void ApplyOrientationImmediate(float targetYaw)
    {
        var e = targetCam.transform.eulerAngles;
        targetCam.transform.rotation = Quaternion.Euler(e.x, targetYaw, e.z);
    }

    private void RotateToYaw(float targetYaw)
    {
        if (!smoothRotate)
        {
            ApplyOrientationImmediate(targetYaw);
            return;
        }
        if (rotateCo != null) StopCoroutine(rotateCo);
        rotateCo = StartCoroutine(RotateYawSmooth(targetCam.transform, targetYaw));
    }

    private IEnumerator RotateYawSmooth(Transform t, float targetYaw)
    {
        // quay đường ngắn nhất theo yaw, giữ nguyên x/z (roll/pitch)
        while (true)
        {
            var e = t.eulerAngles;
            float curYaw = e.y;
            float remain = Mathf.DeltaAngle(curYaw, targetYaw);

            if (Mathf.Abs(remain) <= snapEpsilon)
            {
                t.rotation = Quaternion.Euler(e.x, targetYaw, e.z);
                yield break;
            }

            float step = Mathf.Sign(remain) * rotateSpeedDegPerSec * Time.deltaTime;
            if (Mathf.Abs(step) > Mathf.Abs(remain)) step = remain;

            t.rotation = Quaternion.Euler(e.x, curYaw + step, e.z);
            yield return null;
        }
    }
}
