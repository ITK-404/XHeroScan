public class ToggleButtonUI : ToggleButtonUIBase
{
    protected override void Awake()
    {
        base.Awake();
        btn.onClick.AddListener(Toggle);
    }

    private void OnDestroy()
    {
        if (btn != null)
        {
            btn.onClick.RemoveListener(Toggle);
        }
    }
}