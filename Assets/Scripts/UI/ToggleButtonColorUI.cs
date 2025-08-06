using System.Collections.Generic;

public class ToggleButtonColorUI : ToggleButtonUIBase
{
    private ToggleColorBase[] toggleColorList;
    protected override void Awake()
    {
        base.Awake();
        toggleColorList = GetComponentsInChildren<ToggleColorBase>();
    }

    public override void ChangeState(State newState)
    {
        base.ChangeState(newState);
        if (toggleColorList == null)
        {
            toggleColorList = GetComponentsInChildren<ToggleColorBase>();
        }
        foreach (var toggleColor in toggleColorList)
        {
            toggleColor.Toggle(newState == State.Active);
        }
    }
}