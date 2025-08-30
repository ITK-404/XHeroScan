using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CompassMenu : MonoBehaviour
{
    [SerializeField] private DirectionalItem directionalItemPrefab;
    [SerializeField] private Button turnOnBtn;
    [SerializeField] private Button turnOffCompassBtn;
    [SerializeField] private GameObject directionalsObject;
    private bool isActive = false;

    private DirectionRotationCalculator DirectionRotationCalculator;
    private void Awake()
    {
        turnOnBtn.gameObject.SetActive(true);
        turnOffCompassBtn.gameObject.SetActive(false);
        directionalsObject.gameObject.SetActive(false);
        
        InitRotationCalculator();

        SetupToggleButton();

        InitIconDirections();
        
        Refresh();
        
    }

    private void SetupToggleButton()
    {
        turnOnBtn.onClick.AddListener(() => { Show(true); });
        turnOffCompassBtn.onClick.AddListener(() => { Show(false); });
    }

    public void Show(bool iShow)
    {
        directionalsObject.gameObject.SetActive(iShow);
        turnOnBtn.gameObject.SetActive(!iShow);
        turnOffCompassBtn.gameObject.SetActive(iShow);
    }

    List<Direction> directions = new()
    {
        Direction.East, // icon is look to this direction so this is default rotation
        Direction.North,
        Direction.South,
        Direction.West
    };
    private void InitIconDirections()
    {
        for (int i = 0; i < directions.Count; i++)
        {
            
            var item = Instantiate(directionalItemPrefab, directionalsObject.transform);
            var direction = directions[i];
            if(direction == Direction.East || directions[i] == Direction.West)
            {
                item.anchorIcon = AnchorPosition.MiddleBottom; // icon luôn nằm dưới text
            }
            else if(direction == Direction.North )
            {
                // item.anchorIcon = AnchorPosition.MiddleBottom;
                item.anchorIcon = AnchorPosition.MiddleTop; // icon nằm trên text
            }
            else if(direction == Direction.South)
            {
                // item.anchorIcon = AnchorPosition.MiddleTop;
                item.anchorIcon = AnchorPosition.MiddleBottom; // icon nằm dưới text
            }
            
            itemList.Add(item);
        }

    }

    public void Refresh()
    {
        for (int i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            var itemRect = item.GetComponent<RectTransform>();
            var direction = directions[i];
            var anchor = direction.ToAnchor();

            item.Set(directions[i]);
            item.SetAnchor(itemRect, anchor); // chỉnh anchor của item
            item.SetAnchor(item.Icon, item.anchorIcon); // chỉnh anchor của icon item

            DirectionRotationCalculator.SetZRotation(item.Icon, direction);
        }
    }

    private List<DirectionalItem> itemList = new();

    private void InitRotationCalculator()
    {
        // góc mà UI nằm
        DirectionRotationCalculator = new();
        DirectionRotationCalculator.circleDirection = new()
        {
            Direction.South, // 0
            Direction.East, // 90
            Direction.North, // 180
            Direction.West // 270
        };
        DirectionRotationCalculator.Init();
    }

    private void Toggle()
    {
        isActive = !isActive;
        directionalsObject.gameObject.SetActive(isActive);
    }
}