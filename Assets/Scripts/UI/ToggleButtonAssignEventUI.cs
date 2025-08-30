using System;
using UnityEngine.Events;

public class ToggleButtonAssignEventUI : ToggleButtonColorUI
{
    public Action ActiveEvent;
    protected override void Awake()
    {
        base.Awake();
        ChangeState(currentState);
    }
    
    public void OnActive()
    {
        ActiveEvent?.Invoke();
    }
}