using UnityEngine;

public class ExampleDropDown : MonoBehaviour
{
    [SerializeField,Dropdown(typeof(FurnitureName))] private string dropDownName;
}