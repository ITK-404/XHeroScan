using UnityEngine;
using UnityEngine.UI;

public class ToggleGroupHeaderUI : MonoBehaviour
{
    private static readonly int ColorA = Shader.PropertyToID("_ColorA");
    private static readonly int ColorC = Shader.PropertyToID("_ColorB");
    private static readonly int Split2 = Shader.PropertyToID("_Split2");
    private static readonly int Split1 = Shader.PropertyToID("_Split1");
    
    [SerializeField] private ToggleButtonHeaderUI[] toggleButtons;
    [SerializeField] private Material[] materials;
    [SerializeField] private Image[] buttonLineImages;
    
    [SerializeField] private Color leftColor;
    [SerializeField] private Color middleColor;
    
    [SerializeField] private float fullWidth;
    [SerializeField] private float halfWidth;
    
    [SerializeField] private Material sharedMaterial;
    
    private StuctureHeaderType globalType;

    private void Awake()
    {
        toggleButtons = GetComponentsInChildren<ToggleButtonHeaderUI>();

        for (int i = 0; i < toggleButtons.Length; i++)
        {
            toggleButtons[i].index = i;
            toggleButtons[i].OnClickCallback = OnSelectThis;
        }
        
        materials = new Material[buttonLineImages.Length];
        var shader = Shader.Find("Unlit/HorizontalThreeColorGradientPreciseFade");
        for (int i = 0; i < buttonLineImages.Length; i++)
        {
            var item = buttonLineImages[i];
            Material uniqueMat = new Material(shader);
            item.material = uniqueMat;
            materials[i] = uniqueMat;
        }

        OnSelectThis(0);
    }

    private int currentIndex;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            OnSelectThis(currentIndex++);
            if (currentIndex >= toggleButtons.Length)
            {
                currentIndex = 0;
            }
        }
    }

    private void OnSelectThis(int index)
    {
        for (int i = 0; i < toggleButtons.Length; i++)
        {
            var isActive = index >= i;
            toggleButtons[i]
                .ChangeState(isActive ? ToggleButtonUIBase.State.Active : ToggleButtonUIBase.State.DeActive);
            if (isActive)
            {
                globalType = toggleButtons[i].localType;
            }
        }

        for (int i = 0; i < buttonLineImages.Length; i++)
        {
            if (i == index)
            {
                materials[i].SetColor(ColorA, leftColor);
                materials[i].SetColor(ColorC, middleColor);
                materials[i].SetFloat(Split1, 0.5f);
                materials[i].SetFloat(Split2, 1);
            }
            else if( i < index)
            {
                materials[i].SetColor(ColorA, leftColor);
                materials[i].SetColor(ColorC, middleColor);
                materials[i].SetFloat(Split1, 1);
                materials[i].SetFloat(Split2, 1);
            }
            else
            {
                materials[i].SetColor(ColorA, middleColor);
                materials[i].SetColor(ColorC, middleColor);
                materials[i].SetFloat(Split1, 0);
                materials[i].SetFloat(Split2, 1);
            }
         
        }
    }
}

public enum StuctureHeaderType
{
    Cong_Trinh,
    Phan_Cung,
    Luan_Giai,
    Hoa_Giai
}