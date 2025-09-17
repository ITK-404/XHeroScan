#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public class DropdownAttribute : PropertyAttribute
{
    public Type staticType;

    public DropdownAttribute(Type staticType)
    {
        this.staticType = staticType;
    }
}

[CustomPropertyDrawer(typeof(DropdownAttribute))]
public class DropdownPropertyDrawer : PropertyDrawer
{
    private string[] cachedOptions;
    private Type cachedType;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Chỉ work với string properties
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Dropdown chỉ dùng cho string!");
            return;
        }

        DropdownAttribute dropdownAttribute = (DropdownAttribute)attribute;

        // Cache options để tránh reflection liên tục
        if (cachedOptions == null || cachedType != dropdownAttribute.staticType)
        {
            cachedOptions = GetOptionsFromType(dropdownAttribute.staticType);
            cachedType = dropdownAttribute.staticType;
        }

        if (cachedOptions == null || cachedOptions.Length == 0)
        {
            EditorGUI.LabelField(position, label.text, "Không tìm thấy const string nào!");
            return;
        }

        // Tìm index của giá trị hiện tại
        string currentValue = property.stringValue;
        int selectedIndex = Array.IndexOf(cachedOptions, currentValue);

        // Nếu không tìm thấy, set về 0
        if (selectedIndex == -1)
            selectedIndex = 0;

        // Hiển thị dropdown
        selectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, cachedOptions);

        // Update giá trị
        property.stringValue = cachedOptions[selectedIndex];
    }

    private string[] GetOptionsFromType(Type type)
    {
        if (type == null) return null;

        // Lấy tất cả const string fields từ static class
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string));

        List<string> options = new List<string>();

        foreach (var field in fields)
        {
            string value = (string)field.GetValue(null);
            if (!string.IsNullOrEmpty(value))
                options.Add(value);
        }

        return options.ToArray();
    }
}
#endif
public static class FurnitureName
{
    public const string WindowOneWing = "window_1";
    public const string WindowTwoWing = "window_2";
    public const string WindowFourWing = "window_4";

    public const string DoorOneWing = "door_1";
    public const string DoorTwoWing = "door_2";
    public const string DoorFourWing = "door_4";

    public const string Bed = "bed_1";

}
