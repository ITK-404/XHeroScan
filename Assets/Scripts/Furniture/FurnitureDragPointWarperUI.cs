using iTextSharp.text.pdf.parser.clipper;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureDragPointWarperUI : MonoBehaviour
{
    public static FurnitureDragPointWarperUI Instance;
    [SerializeField] private RectTransform pointPrefab;
    [SerializeField] private RectTransform pointUIContainer;
    private FurnitureManager furnitureManager;
    private Camera mainCamera;

    private List<CheckpointType> list = new();
    private Dictionary<CheckpointType, RectTransform> points = new();

    public float threadShold = 100;
    FurnitureItem currentItem => furnitureManager.CurrentFurnitureItem();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        furnitureManager = FurnitureManager.Instance;
        //list = new List<CheckpointType> { CheckpointType.Left, CheckpointType.Right, CheckpointType.Top, CheckpointType.Bottom };
        list = new List<CheckpointType> { CheckpointType.Top };
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
                    var worldPointPosition = point.transform.position;
                    var pointLocalPosition = GetLocalScreenPosition(worldPointPosition);
                    var worldMousePosition = currentFurniture.GetWorldMousePosition();

                    float distance = Vector3.Distance(pointLocalPosition, screenCenterPosition);
                    //Debug.Log($"Check distance: {distance}");

                    var direction = pointLocalPosition - screenCenterPosition;
                    direction.Normalize();
                    //Debug.Log($"Direction: " + direction);
                    if(distance < threadShold)
                    {
                        item.Value.anchoredPosition = pointLocalPosition + (pointOffsetFixed * direction);
                    }
                    else
                    {
                        item.Value.anchoredPosition = pointLocalPosition;
                    }

                    worldPointPosition.y = 0;
                    //worldMousePosition.y = 0;

                    var mouseOffsetPosition = worldMousePosition - worldPointPosition;
                    var correctPosition = worldMousePosition - mouseOffsetPosition;

                    Debug.Log($"world mouse pos {worldMousePosition} Convert {correctPosition} {point.transform.position}");
                }
            }
        }
    }
    public Vector3 correctPosition;

    [SerializeField] private float pointOffsetFixed = 200;

    private Vector2 GetLocalScreenPosition(Vector3 worldPosition)
    {
        var screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
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

        var currentFurniture = furnitureManager.CurrentFurnitureItem();
        var worldCenterPosition = currentFurniture.GetWorldPosition();
        var screenCenterPosition = GetLocalScreenPosition(worldCenterPosition);

        foreach (var point in currentItem.PointArray)
        {
            if (point.checkpointType == checkpoint)
            {
                //var worldPointPosition = point.transform.position;
                //var pointLocalPosition = GetLocalScreenPosition(worldPointPosition);
                //var worldMousePosition = currentFurniture.GetWorldMousePosition();

                //float distance = Vector3.Distance(pointLocalPosition, screenCenterPosition);
                ////Debug.Log($"Check distance: {distance}");

                //var direction = pointLocalPosition - screenCenterPosition;
                //direction.Normalize();
                ////Debug.Log($"Direction: " + direction);

                //worldPointPosition.y = 0;
                ////worldMousePosition.y = 0;

                //var mouseOffsetPosition = worldMousePosition - worldPointPosition;
                //var correctPosition = worldMousePosition - mouseOffsetPosition;
                //Debug.Log($"{worldMousePosition - mouseOffsetPosition} {point.transform.position}");

                //this.correctPosition = correctPosition;
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
