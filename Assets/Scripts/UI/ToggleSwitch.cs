using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ToggleSwitch : MonoBehaviour, IPointerClickHandler
{
    [Range(0f, 1f)] [SerializeField] private float currentValue = 0f;

    public bool CurrentValue;
    private Slider slider;
    [SerializeField, Range(0, 1f)] private float animationDuration = 0.2f;

    [SerializeField] private AnimationCurve easeCurve;

    [SerializeField] private UnityEvent OnToggleOn;
    [SerializeField] private UnityEvent OnToggleOff;

    private ToggleSwitchGroupManager toggleSwitchGroupManager;

    private bool previousValue;
    private Coroutine animatedCoroutine;
    
    private void OnValidate()
    {
        SetupComponents();
    }

    private void Awake()
    {
        SetupComponents();
    }

    private void SetupComponents()
    {
        slider = GetComponent<Slider>();
        if (slider == null)
        {
            return;
        }

        slider.value = currentValue;
        slider.interactable = false;
        var slidersColors = slider.colors;

        slidersColors.disabledColor = Color.white;
        slider.colors = slidersColors;
        slider.transition = Selectable.Transition.None;
    }

    private void Toggle()
    {
        if (toggleSwitchGroupManager != null)
        {
            toggleSwitchGroupManager.ToggleGroup(this);
        }
        else
        {
            SetStateAndStartAnimation(!CurrentValue);
        }
    }

    private void SetStateAndStartAnimation(bool state)
    {
        CurrentValue = state;
        if (previousValue != CurrentValue)
        {
            if (CurrentValue)
            {
                OnToggleOn?.Invoke();
            }
            else
            {
                OnToggleOff?.Invoke();
            }

            previousValue = CurrentValue;
        }

        if (animatedCoroutine != null)
        {
            StopCoroutine(animatedCoroutine);
        }
        
        animatedCoroutine = StartCoroutine(DisableCoroutine());
    }
    
    private IEnumerator DisableCoroutine()
    {
        float startValue = slider.value;
        float endValue = CurrentValue ? 1f : 0f;
        float timer = 0;

        if (animationDuration > 0)
        {
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float lerpFactor = Mathf.Clamp01(timer / animationDuration);
                slider.value = Mathf.Lerp(startValue, endValue, easeCurve.Evaluate(lerpFactor));
                yield return null;
            }
        }

        slider.value = endValue;
    }

    public void ToggleByGroupManager(bool state)
    {
        SetStateAndStartAnimation(state);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Toggle();
    }
}

public class ToggleSwitchGroupManager : MonoBehaviour
{

    private void Awake()
    {
        
    }

    private void OnToggleChanged(ToggleSwitch toggledSwitch)
    {
    }

    public void ToggleGroup(ToggleSwitch toggleSwitch)
    {
    }
}