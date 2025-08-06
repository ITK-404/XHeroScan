using TMPro;
using UnityEngine;

public class GridInformationText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI informationText;
    [SerializeField] private string template;
    private void Awake()
    {
        GPUInstancedGrid.OnChangedLimitSize += GPUInstancedGridOnOnChangedLimitSize;
    }

    private void OnDestroy()
    {
        GPUInstancedGrid.OnChangedLimitSize -= GPUInstancedGridOnOnChangedLimitSize;
    }

    private void GPUInstancedGridOnOnChangedLimitSize(int unitSize)
    {
        informationText.text = string.Format(template, unitSize);   
    }
}
