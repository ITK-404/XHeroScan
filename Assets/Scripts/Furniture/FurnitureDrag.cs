using UnityEngine;
using UnityEngine.EventSystems;

public class FurnitureDrag : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;
    private Vector3 startPosition;
    private Vector3 touchPosition;
    public Vector3 offsetPosition;

    private bool canMove = false;
    private bool canCreateCommand = false;
    private void OnMouseDown()
    {
        canCreateCommand = true;

        StartMoveSetup();
    }

    public void StartMoveSetup()
    {
        furnitureItem.StartDrag();
        FurnitureItem.SnapShotTemp = furnitureItem.data;
        startPosition = furnitureItem.GetWorldPosition();
        touchPosition = furnitureItem.GetWorldMousePosition();
        offsetPosition = touchPosition - startPosition;

        //Debug.Log("Start position: " + startPosition);
        //Debug.Log("Touch position: " + touchPosition);
        //Debug.Log("Offset position: " + offsetPosition);
        canMove = true;
    }

    private void LateUpdate()
    {
        if (canMove == false) return;
        if (Input.touchCount > 1)
        {
            return;
        }
        if (FurnitureManager.Instance.IsSelectFurniture(furnitureItem))
        {
            //if(FurnitureDragPointWarperUI.Instance.IsDragPoint())
            //{
            //    return;
            //}

            if(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            if(FurnitureDragPointWarperUI.Instance.IsDragPoint())
            {
                return;
            }
            DraggingByPosition(furnitureItem.GetWorldMousePosition() - offsetPosition);
        }
    }

    public void DraggingByPosition(Vector3 position)
    {
        var correctPosition = position;
        //Debug.Log($"Correct position {correctPosition} {furnitureItem.GetWorldPosition()}");

        furnitureItem.Dragging(correctPosition);
    }

    private void OnMouseUp()
    {
        Dragging();
    }

    private void Dragging()
    {
        furnitureItem.DeActiveDrag();

        if (startPosition != furnitureItem.GetWorldPosition() && canCreateCommand)
        {
            furnitureItem.CreareEditCommandBySnapShot();
        }

        canMove = false;
        canCreateCommand = false;
    }

    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
    }
}
