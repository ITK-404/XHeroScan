using UnityEngine;
using UnityEngine.UI;

public class BottomClosePage : MonoBehaviour
{
    public Button btn;

    private void OnValidate()
    {
        btn = GetComponent<Button>();
    }
}