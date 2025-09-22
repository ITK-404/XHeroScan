using Org.BouncyCastle.Ocsp;
using UnityEngine;

public class FurnitureDrag : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;
    private Vector3 startPosition;
    private Vector3 touchPosition;
    private Vector3 offsetPosition;

    private bool canMove = false;

    private void OnMouseDown()
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

    private void Update()
    {
        if (canMove == false) return;
        if (Input.touchCount > 1)
        {
            return;
        }

        if (FurnitureManager.Instance.IsSelectFurniture(furnitureItem))
        {
            var correctPosition = furnitureItem.GetWorldMousePosition() - offsetPosition;
            //Debug.Log($"Correct position {correctPosition} {furnitureItem.GetWorldPosition()}");
            furnitureItem.Dragging(correctPosition);
        }
    }

    private void OnMouseUp()
    {
        canMove = false;
        furnitureItem.DeActiveDrag();

        if(startPosition != furnitureItem.GetWorldPosition())
        {
            furnitureItem.CreareEditCommandBySnapShot();
        }
    }

    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
    }
}