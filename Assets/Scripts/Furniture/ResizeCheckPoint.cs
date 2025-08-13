using System.Collections.Generic;
using UnityEngine;

public class ResizeCheckPoint : MonoBehaviour
{
    [SerializeField] private List<Transform> checkPointList = new();
    [SerializeField] private float size = 1;
    private float previousSize;
    private Vector3 originalSize;
    private void Awake()
    {
        if (checkPointList.Count == 0)
        {
            return;
        }
            originalSize = checkPointList[0].localScale;
    }

    private void Update()
    {
        
        if (previousSize != size)
        {
            foreach (var item in checkPointList)
            {
                item.transform.localScale = originalSize * size;
            }

            previousSize = size;
        }
    }
}