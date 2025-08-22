using UnityEngine;

public class MappingModelFurniture : MonoBehaviour
{
    public string ItemID;
    [SerializeField] private FurnitureData Data;
    [SerializeField] private GameObject model2D;
    [SerializeField] private GameObject model3D;
    [SerializeField] private FurnitureKeyStorage storage;

    private void Awake()
    {

        // Subscribe to view change events
        ViewChanger.ViewChanged += OnViewChanged;

        // Initialize the model based on the current view type
    }

    private void OnDestroy()
    {
        ViewChanger.ViewChanged -= OnViewChanged;
    }

    private void InitModel()
    {
        var furnitureKey = storage.GetFurnitureKeyByID(ItemID);
        var spriteRenderer2D = model2D.GetComponent<SpriteRenderer>();
      
        spriteRenderer2D.sprite = furnitureKey.sprite2D;
        SetSpriteSize(spriteRenderer2D, Data.Width, Data.Height);
        
        model3D = Instantiate(furnitureKey.model3D, transform);
        model3D.transform.localPosition = Vector3.zero;
        SetModelSize(model3D.gameObject, new Vector3(Data.Width, Data.ObjectHeight, Data.Height));
    }

    void SetModelSize(GameObject model, Vector3 targetSize)
    {
        MeshFilter mf = model.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("No MeshFilter or Mesh found.");
            return;
        }

        Vector3 meshSize = mf.sharedMesh.bounds.size; // Local space size
        Vector3 currentScale = model.transform.localScale;

        currentScale.x = targetSize.x / meshSize.x;
        currentScale.y = targetSize.y / meshSize.y;
        currentScale.z = targetSize.z / meshSize.z;

        model.transform.localScale = currentScale;
    }


    void SetSpriteSize(SpriteRenderer sr, float targetWidth, float targetHeight)
    {
        // Sprite sp = sr.sprite;
        // if (sp == null) return;
        //
        // // Lấy kích thước gốc của sprite (pixel → unit)
        // Vector2 spriteSizeUnits = new Vector2(
        //     sp.rect.width / sp.pixelsPerUnit,
        //     sp.rect.height / sp.pixelsPerUnit
        // );
        //
        // // Tính scale để đạt target size
        // Vector3 newScale = sr.transform.localScale;
        // newScale.x = targetWidth / spriteSizeUnits.x;
        // newScale.y = targetHeight / spriteSizeUnits.y;
        //
        // sr.transform.localScale = newScale;
    }

    public void SetData(FurnitureData data)
    {
        Data = data;
        InitModel();
        UpdateModel();
        OnViewChanged(ViewChanger.currentViewType);
    }

    private void UpdateModel()
    {
        if (Data == null)
        {
            Debug.LogWarning("FurnitureData is not set.");
            return;
        }

        // Assuming you have a method to update the model based on the data
        transform.localPosition = new Vector3(Data.worldPosition.x, -2.4f, Data.worldPosition.z);
        transform.localRotation = Quaternion.Euler(0, Data.rotation, 0);
        transform.localScale = new Vector3(Data.Width, Data.ObjectHeight, Data.Height);
    }

    private void OnViewChanged(ViewType viewType)
    {
        if (viewType == ViewType.VIew2D)
        {
            model2D?.SetActive(true);
            model3D?.SetActive(false);
        }
        else
        {
            model2D?.SetActive(false);
            model3D?.SetActive(true);
        }
    }
}