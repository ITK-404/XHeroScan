using System;
using UnityEngine;

public class DropdownAttribute : PropertyAttribute
{
    public Type staticType;

    public DropdownAttribute(Type staticType)
    {
        this.staticType = staticType;
    }
}
