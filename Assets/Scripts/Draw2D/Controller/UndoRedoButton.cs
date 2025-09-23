using UnityEngine;
using UnityEngine.UI;

public class UndoRedoButton : MonoBehaviour
{
    public bool isUndo = true;
    private Button btn;

    private UndoRedoController controller;
    
    private void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClicked);
    }

    private void Start()
    {
        controller = UndoRedoController.Instance;
    }

    private void OnDestroy()
    {
        btn.onClick.RemoveListener(OnClicked);
    }

    private void Update()
    {
        if (controller == null) return;

        btn.interactable =  isUndo ? controller.CanUndo() : controller.CanRedo();
    }
    
    private void OnClicked()
    {
        if (isUndo)
        {
            controller.Undo();
        }
        else
        {
            controller.Redo();
        }
    }
}