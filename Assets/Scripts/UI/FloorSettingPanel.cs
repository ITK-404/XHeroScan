
using UnityEngine;

public class FloorSettingPanel : SettingPanel
{
    protected override void Awake()
    {
        base.Awake();

        foreach (var parameterType in parameterTypes)
        {
            var parameterInputField = Instantiate(parameterInputFieldPrefab, contentParent);
            switch (parameterType)
            {
                case IntParameterType.Thickness:
                    parameterInputField.Initialize("Độ dày", "0,0" );
                    break;
                case IntParameterType.Height:
                    parameterInputField.Initialize("Chiều cao", "Nhập chiều cao" );
                    break;
                case IntParameterType.Width:
                    parameterInputField.Initialize("Chiều rộng", "Nhập chiều rộng");
                    break;
                //case ParameterType.Length:
                //    parameterInputField.Initialize("Chiều dài", "Nhập chiều dài");
                //break;
                case IntParameterType.DistanceFromGround:
                    parameterInputField.Initialize("Khoảng cách từ mặt đất", "Nhập khoảng cách" );
                    break;
                default:
                    parameterInputField.Initialize("Parameter", "Enter value");
                    break;
            }
            parameterInputField.parameterType = parameterType;
            Debug.Log($"[SettingPanel] Added parameter input field: {parameterType}");
        }
    }
}
