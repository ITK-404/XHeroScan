using System;

public class ToggleButtonHeaderUI : ToggleButtonColorUI
{
    public Action<int> OnClickCallback;
    public StuctureHeaderType localType;
    public int index = 0;

    protected override void Awake()
    {
        base.Awake();
        btn.onClick.AddListener(() =>
        {
            {
                OnClickCallback?.Invoke(index);
            }
        });
    }
}