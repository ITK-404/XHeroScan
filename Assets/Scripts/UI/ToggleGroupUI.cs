using System.Collections.Generic;
using UnityEngine;

public class ToggleGroupUI : MonoBehaviour
{
    [SerializeField] private List<ToggleButtonLineType> list = new();
    public DrawingTool drawingTool;
    public CheckpointManager checkpointManager;
    public PenManager penManager;
    public FocusFunctionFieldUI focusFunctionFieldUI;
    private void Start()
    {
        Setup();
    }

    private void Setup()
    {
        foreach (ToggleButtonLineType item in list)
        {
            if (item == null) continue;
            if (item.btn == null) continue;
            item.btn.onClick.AddListener(() =>
            {
                Active(item);
            });
        }

    }

    public void ShowFirstButton()
    {
    }

    public void ToggleOffAll()
    {
        foreach (var item in list)
        {
            item.ChangeState(ToggleButtonUIBase.State.DeActive);
        }

    }

    [SerializeField] private GameObject toastUI;

    public void Active(ToggleButtonLineType btn)
    {
        if (RoomStorage.rooms.Count == 0)
        {
            ModularPopup.CreatePopup("Không thể kích hoạt tạo tường", "Cần ít nhất một phòng", ModularPopup.PopupAsset.toastPopupError);
            return;
        }

        if (btn.currentState == ToggleButtonUIBase.State.DeActive)
        {
            btn.ChangeState(ToggleButtonUIBase.State.Active);
            // lock pen
            penManager.ChangeState(false);
        }

        drawingTool.currentLineType = btn.lineType;
        checkpointManager.currentLineType = btn.lineType;

        focusFunctionFieldUI.Open(() =>
        {
            DeActive(btn);
        });
    }

    public void DeActive(ToggleButtonLineType btn)
    {
        btn.ChangeState(ToggleButtonUIBase.State.DeActive);
        penManager.ChangeState(true);
        return;
    }

}