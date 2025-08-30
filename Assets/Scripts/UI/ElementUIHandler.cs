using System;
using System.Collections.Generic;
using UnityEngine;

public class ElementUIHandler : MonoBehaviour
{
    private ElementFiler[] elementList;

    // Key giờ chứa luôn Type + ID + UIType để không bị nhầm
    private readonly Dictionary<(Type, string, UIType), object> cachedList = new();

    private void Awake()
    {
        // Nếu muốn lấy cả inactive thì thêm true
        elementList = GetComponentsInChildren<ElementFiler>(true);
    }

    public ElementFiler GetItemByFiler(string id, UIType type = UIType.None)
    {
        foreach (var item in elementList)
        {
            if (string.Equals(item.ID, id, StringComparison.OrdinalIgnoreCase)
                && (type == UIType.None || type == item.Type))
            {
                return item;
            }
        }
        return null;
    }

    public T GetItemByFiler<T>(string id, UIType elementType = UIType.None) where T : MonoBehaviour
    {
        var key = (typeof(T), id, elementType);
        if (cachedList.TryGetValue(key, out var value))
        {
            return (T)value;
        }

        value = GetItemByFiler(id, elementType)?.GetComponent<T>();
        if (value == null)
        {
            return null;
        }

        // Gán trực tiếp, nếu key trùng thì overwrite luôn thay vì nổ exception
        cachedList[key] = value;
        return (T)value;
    }
}
