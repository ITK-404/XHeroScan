using iTextSharp.text.pdf.parser.clipper;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureDragPointWarperUI : MonoBehaviour
{
    [SerializeField] private RectTransform pointPrefab;
    [SerializeField] private RectTransform pointUIContainer;
    private FurnitureManager furnitureManager;
    private Camera mainCamera;

    private List<CheckpointType> list = new();
    private Dictionary<CheckpointType, RectTransform> points = new();

    public float threadShold = 100;
    FurnitureItem currentItem => furnitureManager.CurrentFurnitureItem();
    private void Start()
    {
        mainCamera = Camera.main;
        furnitureManager = FurnitureManager.Instance;
        list = new List<CheckpointType> { CheckpointType.Left, CheckpointType.Right, CheckpointType.Top, CheckpointType.Bottom };
        //list = new List<CheckpointType> { CheckpointType.Top};
        Inititialize();
    }

    private void Inititialize()
    {
        foreach (var item in list)
        {
            var point = Instantiate(pointPrefab, pointUIContainer);
            var pointWrapper = point.GetComponent<FurniturePointUIWrapper>();
            pointWrapper.warperUI = this;
            pointWrapper.checkpointType = item;
            points.Add(item, point);
        }
    }

    private void Update()
    {
        RefreshPoint();
    }

    private void RefreshPoint()
    {
        if (currentItem == false) return;

        var currentFurniture = furnitureManager.CurrentFurnitureItem();
        var worldCenterPosition = currentFurniture.GetWorldPosition();
        var screenCenterPosition = GetLocalScreenPosition(worldCenterPosition);


        foreach (var item in points)
        {
            foreach (var point in currentFurniture.PointArray)
            {
                if (point.checkpointType == item.Key)
                {
                    var screenPosition = mainCamera.WorldToScreenPoint(point.transform.position);
                    var pointLocalPosition = GetLocalScreenPosition(screenPosition);
                    float distance = Vector3.Distance(pointLocalPosition, screenCenterPosition);
                    Debug.Log($"Local position: {point.transform.position}");
                    Debug.Log($"Local position: {mainCamera.ScreenToWorldPoint(screenPosition)}");

                    var direction = pointLocalPosition - screenCenterPosition;
                    direction.Normalize();
                    Debug.Log($"Direction: " + direction);
                    if(distance < threadShold)
                    {
                        item.Value.anchoredPosition = pointLocalPosition + (pointOffsetFixed * direction);
                    }
                    else
                    {
                        item.Value.anchoredPosition = pointLocalPosition;
                    }
                }
            }
        }
    }


    [SerializeField] private float pointOffsetFixed = 200;

    private Vector2 GetLocalScreenPosition(Vector3 screenPosition)
    {
        
        Vector2 uiPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            pointUIContainer as RectTransform,
            screenPosition,
            null,
            out uiPosition
        );

        return uiPosition;
    }

    public void StartDrag(CheckpointType checkpoint)
    {
        foreach(var point in currentItem.PointArray)
        {
            if(point.checkpointType == checkpoint)
            {
                point.StartDragPoint();
            }
        }
    }
    public void Dragging(CheckpointType checkpoint)
    {
        foreach (var point in currentItem.PointArray)
        {
            if (point.checkpointType == checkpoint)
            {
                point.Dragging();
            }
        }
    }

    public void EndDrag(CheckpointType checkpoint)
    {
        foreach (var point in currentItem.PointArray)
        {
            if (point.checkpointType == checkpoint)
            {
                point.EndDrag();
            }
        }
    }
}
