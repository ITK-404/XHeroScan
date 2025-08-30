using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ToggleButtonUIBase : MonoBehaviour
{
    public Button btn;
    public State currentState = State.DeActive;
    public Action<State> OnValueChange;
    public UnityEvent OnToggleOn;
    public UnityEvent OnToggleOff;
    protected virtual void Awake()
    {
        btn = GetComponent<Button>();
    }
    
    public enum State
    {
        Active,
        DeActive
    }

    public virtual void ChangeState(State newState)
    {
        currentState = newState;
        OnValueChange?.Invoke(currentState);
    }

    public void Toggle()
    {
        if (currentState == State.Active)
        {
            currentState = State.DeActive;
            OnToggleOff?.Invoke();
        }
        else
        {
            currentState = State.Active;
            OnToggleOn?.Invoke();
        }

        ChangeState(currentState);
    }
}