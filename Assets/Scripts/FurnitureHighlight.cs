using System.Collections.Generic;
using UnityEngine;

public class FurnitureHighlight : HighlightTarget
{
    [SerializeField] private LineRenderer[] renderers;
    public override void HighLight()
    {
        SetMaterials(HighlightHandler.HighlightMaterial);
    }

    public override void UnHighlight()
    {
        SetMaterials(HighlightHandler.NormalMaterial);
    }

    private void SetMaterials(Material newMaterial)
    {
        if(renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<LineRenderer>();
        }

        foreach(var line in renderers)
        {
            line.sharedMaterial = newMaterial;
        }
    }
}
