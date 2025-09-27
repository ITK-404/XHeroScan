using System;
using System.Collections.Generic;
using UnityEngine;
public class UndoRedoController : MonoBehaviour
{
    public static bool loadFromScanAR = false;
    public static UndoRedoController Instance;
    public static List<IUndoRedoCommand> scanARTempList = new();

    [SerializeField] private int maxStackCount = 20;
    
    private List<IUndoRedoCommand> undoList;
    private List<IUndoRedoCommand> redoList;

    public static EditRoomCommandCreator EditRoomCommandCreator;
    
    private void Awake()
    {
        Instance = this;

        undoList = new List<IUndoRedoCommand>();
        redoList = new List<IUndoRedoCommand>();
        // dùng biến static để lưu object data khi load sang scene khác
        if(scanARTempList.Count > 0)
        {
            undoList = new List<IUndoRedoCommand>(scanARTempList);
        }
        // sau khi load xong thì clear đi
        scanARTempList.Clear();
        loadFromScanAR = false;
    }

    public void CreateTempUndoListToScanAR()
    {
        scanARTempList = new List<IUndoRedoCommand>(undoList);
        loadFromScanAR = true;
    }
//#if UNITY_EDITOR
//    private void Update()
//    {
//        if (Input.GetKeyDown(KeyCode.F))
//        {
//            Undo();
//        }

//        if (Input.GetKeyDown(KeyCode.G))
//        {
//            Redo();
//        }

//        if (Input.GetKeyDown(KeyCode.H))
//        {
//            RoomStorage.CheckDuplicateRoomID();
//        }
//    }
//#endif

    public bool CanUndo() => undoList.Count > 0;
    public bool CanRedo() => redoList.Count > 0;

    public void AddToUndo(IUndoRedoCommand command)
    {
        Debug.Log($"[UNDO] Added: {command.GetType().Name} | Time: {DateTime.Now:HH:mm:ss}");
        undoList.Add(command);
        
        redoList.Clear(); // Clear redo khi có hành động mới
     
        if (undoList.Count > maxStackCount)
        {
            Debug.Log("Số lượng command vượt quá số lượng tối đa, đã xóa command trễ nhất");
            undoList.RemoveAt(0);
        }
        
    }

    public void Undo()
    {
        if (undoList.Count == 0) return;

        IUndoRedoCommand command = undoList[^1];
        undoList.Remove(command);
        command.Undo();
        redoList.Add(command);
    }

    public void Redo()
    {
        if (redoList.Count == 0) return;

        IUndoRedoCommand command = redoList[^1];
        redoList.Remove(command);
        command.Redo();
        undoList.Add(command);
    }

    public void ClearData()
    {
        undoList.Clear();
        redoList.Clear();
    }

}