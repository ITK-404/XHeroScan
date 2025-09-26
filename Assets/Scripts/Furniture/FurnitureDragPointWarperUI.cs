using System;
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
    FurniturePoint point;

    public Vector3 correctPosition;

    [SerializeField] private float pointOffsetFixed = 200;
    [SerializeField] private float rotateOffsetFixed = 200;
    private Vector3 startMousePos;
    private Vector3 startOffset;

    [SerializeField] private FurnitureRotateUIWrapper furnitureRotateUIWrapper;
    private void Awake()
    {
        pointPrefab.gameObject.SetActive(false);
        Instance = this;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        furnitureManager = FurnitureManager.Instance;
        list = new List<CheckpointType>
        {
            CheckpointType.Left,
            CheckpointType.Right,
            CheckpointType.Top,
            CheckpointType.Bottom,
            CheckpointType.TopLeft,
            CheckpointType.TopRight,
            CheckpointType.BottomLeft,
            CheckpointType.BottomRight
        };
        //list = new List<CheckpointType> { CheckpointType.Top };
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
            point.gameObject.SetActive(true);
        }
    }
    private Vector3 previousPosition;
    private void Update()
    {
        if (Input.touchCount > 1)
        {
            rotatePoint?.OnEndDrag();
            point?.EndDrag();
            rotatePoint = null;
            point = null;

            pointUIContainer.gameObject.SetActive(false);
            return;
        }

        pointUIContainer.gameObject.SetActive(currentItem != null);

        DraggingPointHandle();
        DragginRotatePointHandle();

        RefreshPoint();
        //DetectMouseIsStatic();
    }
    private void DragginRotatePointHandle()
    {
        if (rotatePoint != null)
        {
            var worldMousePos = GetWorldMousePos();

            // Khử y
            startMousePos.y = rotatePoint.transform.position.y;
            worldMousePos.y = rotatePoint.transform.position.y;

            this.correctPosition = worldMousePos - startOffset;

            rotatePoint.Dragging();
        }
    }

    private void DraggingPointHandle()
    {
        if (point == null) return;

        var worldMousePos = GetWorldMousePos();

        // Khử y
        startMousePos.y = point.transform.position.y;
        worldMousePos.y = point.transform.position.y;

        this.correctPosition = worldMousePos - startOffset;

        point.Dragging();
    }

    private float timer;
    private void DetectMouseIsStatic()
    {
        if (previousPosition == Input.mousePosition)
        {
            // mouse is static, wait for 0.1s
            timer -= Time.deltaTime;
            if (timer < 0)
            {
                // show check point;
                foreach (var item in points)
                {
                    item.Value.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            previousPosition = Input.mousePosition;
            timer = 0.1f;
            foreach (var item in points)
            {
                item.Value.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshPoint()
    {
        if (currentItem == false) return;

        var currentFurniture = furnitureManager.CurrentFurnitureItem();
        var worldCenterPosition = currentFurniture.GetWorldPosition();
        var screenCenterPosition = GetLocalScreenPosition(worldCenterPosition);
        furnitureRotateUIWrapper.gameObject.SetActive(currentFurniture.rotatePoint.gameObject.activeSelf);

        foreach (var item in points)
        {
            foreach (var point in currentFurniture.PointArray)
            {

                var worldMousePosition = currentFurniture.GetWorldMousePosition();
                var worldPointPosition = point.transform.position;
                var pointLocalPosition = GetLocalScreenPosition(worldPointPosition);
                var direction = pointLocalPosition - screenCenterPosition;
                direction.Normalize();

                if (point.checkpointType == item.Key)
                {
                    item.Value.gameObject.SetActive(point.gameObject.activeSelf);
                    float distance = Vector3.Distance(pointLocalPosition, screenCenterPosition);
                    //Debug.Log($"Check distance: {distance}");

                    //Debug.Log($"Direction: " + direction);
                    if (distance < threadShold)
                    {
                        item.Value.anchoredPosition = pointLocalPosition + (pointOffsetFixed * direction);
                    }
                    else
                    {
                        item.Value.anchoredPosition = pointLocalPosition;
                    }
                    //Debug.Log($"Direction: " + direction);
                }

                if (point.checkpointType == CheckpointType.Bottom)
                {
                    furnitureRotateUIWrapper.Rect.anchoredPosition = pointLocalPosition + (rotateOffsetFixed * direction);
                }
            }
        }
    }


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

    private Vector3 GetWorldMousePos()
    {
        Vector3 mouseScreenPos = Input.mousePosition; // (x,y) pixel trên màn hình
        var mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        return mouseWorldPos;
    }

    public void StartDragResize(CheckpointType checkpoint)
    {
        startMousePos = GetWorldMousePos();
        foreach (var point in currentItem.PointArray)
        {
            if (point.checkpointType == checkpoint)
            {
                point.StartDragPoint();
                this.point = point;
                startOffset = startMousePos - point.transform.position;
            }
        }
    }
    public void DraggingResize(CheckpointType checkpoint)
    {

    }

    public void EndDragResize(CheckpointType checkpoint)
    {
        point?.EndDrag();
        point = null;
    }
    private FurnitureRotate rotatePoint;

    internal void OnBeginRotateDrag()
    {
        if (furnitureManager.CurrentFurnitureItem() == null) return;
        rotatePoint = furnitureManager.CurrentFurnitureItem().rotatePoint;
        rotatePoint.StartRotate();

        startMousePos = GetWorldMousePos();
        startOffset = startMousePos - rotatePoint.transform.position;
    }

    internal void OnEndRotateDrag()
    {
        rotatePoint?.OnEndDrag();
        rotatePoint = null;
    }

    public bool IsDragPoint()
    {
        return rotatePoint != null || point != null;
    }
}
