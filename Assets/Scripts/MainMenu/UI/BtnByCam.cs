using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;

public class btnByCam : MonoBehaviour
{
    public Button btnEnter;
    public Button btnMeasure;
    public GameObject unit;      // GameObject sẽ được bật khi nhấn Enter
    public GameObject buttonBar; // GameObject chứa các button cần ẩn
    public GameObject background; // GameObject chứa background
    public GameObject popupErr;   // Hiện popup khi không hỗ trợ AR

    private static bool isMeasure = false;
    public bool IsMeasure { set { isMeasure = value; } get { return isMeasure; } }
    public static btnByCam Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (btnEnter != null)
            btnEnter.onClick.AddListener(OnEnterClicked);
        else
            Debug.LogError("btnEnter chưa được gán!");

        if (btnMeasure != null)
            btnMeasure.onClick.AddListener(() => StartCoroutine(OnMeasureClicked()));
        else
            Debug.LogError("btnMeasure chưa được gán!");

        if (unit != null)
            unit.SetActive(false);
        else
            Debug.LogError("unit chưa được gán!");
        if (background != null)
            background.SetActive(false);
        else
            Debug.LogError("background chưa được gán!");

        if (buttonBar == null)
            Debug.LogError("buttonBar chưa được gán!");

        if (popupErr != null)
            popupErr.SetActive(false);
    }

    void OnEnterClicked()
    {
        if (unit != null)
            unit.SetActive(true);    // Hiện GameObject unit
    }

    private IEnumerator OnMeasureClicked()
    {
        // Kiểm tra AR khả dụng
        yield return ARSession.CheckAvailability();

        if (ARSession.state == ARSessionState.Unsupported ||
            ARSession.state == ARSessionState.None)
        {
            Debug.LogWarning("Thiết bị không hỗ trợ AR");
            if (popupErr != null) popupErr.SetActive(true);
            yield break;
        }

        if (ARSession.state == ARSessionState.NeedsInstall)
        {
            Debug.LogWarning("Thiết bị cần cài thêm AR Core/Kit");
            if (popupErr != null) popupErr.SetActive(true);
            yield break;
        }

        // Nếu ok thì chuyển scene
        isMeasure = true;
        Debug.Log("Measure clicked - To scene ARFoundation\nMeasure = true");
        SceneManager.LoadScene("AR");
    }
}
