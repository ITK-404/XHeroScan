using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public enum IntParameterType
{
    Thickness,
    Height,
    Width,
    Length,
    DistanceFromGround
}
public class SettingPanel : MonoBehaviour
{
    [SerializeField] protected ParameterInputField parameterInputFieldPrefab;
    [SerializeField] protected Transform contentParent;
    [SerializeField] protected IntParameterType[] parameterTypes;
    [SerializeField] protected Button applyButton;
    protected List<ParameterInputField> parameterInputFields = new List<ParameterInputField>();

    public Action OnApplyAction;

    protected virtual void Awake()
    {
        applyButton.onClick.AddListener(() => OnApplyAction?.Invoke());
    }

    public void ResetAllParameters()
    {
        foreach (var inputField in parameterInputFields)
        {
            inputField.ResetValue();
        }
    }

    public ParameterInputField GetParameterInputField(IntParameterType parameterType)
    {
        foreach(var item in parameterInputFields)
        {
            if (item.parameterType == parameterType)
                return item;
        }
        Debug.Log($"[SettingPanel] Parameter input field not found: {parameterType}");
        return null;
    }

    public void Add(ParameterInputField parameter)
    {
        parameterInputFields.Add(parameter);
    }
}
