using System.Collections.Generic;
using UnityEngine;

public class ResizeCheckPoint : MonoBehaviour
{
    [SerializeField] private List<Transform> checkPointList = new();
    [SerializeField] private float size = 1;

    private void Update()
    {
        foreach (var item in checkPointList)
        {
            item.transform.localScale = FurnitureManager.Instance.ScaleByCameraZoom.Scale;
        }
    }
}