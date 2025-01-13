using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeSystemLogic : MonoBehaviour
{
    public static TimeSystemLogic Instance { get; private set; }

    [SerializeField] private float secondsPerSeventh = 180f; // 1 Seventh = 3 minutes
    private float timeSinceLastSeventh = 0f;

    [SerializeField] private TMP_Text cycleText, echoText, seventhText, phaseText;
    [SerializeField] private Image phaseImage;

    [SerializeField] private TimeUnit[] echoes, phases;

    public int CurrentCycle { get; private set; } = 1;
    public int CurrentEcho { get; private set; } = 1;
    public int CurrentPhase { get; private set; } = 1;
    public int CurrentSeventh { get; private set; } = 1;

    public event Action<int> OnPhaseChange, OnEchoChange, OnCycleChange, OnRitualSeventh;

    private int totalPhaseIndex;

    [SerializeField] private List<string> cycleNames;

    private string currentCycleName;

    private Color originalColor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate TimeSystemLogic found, destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        currentCycleName = "Overture"; // The first cycle name

        originalColor = seventhText.color;

        UpdateUI();
    }

    private void Update()
    {
        timeSinceLastSeventh += Time.deltaTime;

        if (timeSinceLastSeventh >= secondsPerSeventh)
        {
            timeSinceLastSeventh = 0f;
            IncrementSeventh();
        }
    }

    private void IncrementSeventh()
    {
        CurrentSeventh++;

        if (CurrentSeventh > 21) // Transition to next phase
        {
            CurrentSeventh = 1;
            IncrementPhase();
        }
        else if (CurrentSeventh == 21) // Ritual Seventh
        {
            OnRitualSeventh?.Invoke(CurrentSeventh);
        }

        UpdateUI();
    }

    private void IncrementPhase()
    {
        CurrentPhase++;
        totalPhaseIndex++;

        if (CurrentPhase > 3) // Transition to next echo
        {
            CurrentPhase = 1;
            IncrementEcho();
        }

        if (totalPhaseIndex >= phases.Length) // Reset the totalPhaseIndex at the end of the cycle
            totalPhaseIndex = 0;

        OnPhaseChange?.Invoke(CurrentPhase);
    }

    private void IncrementEcho()
    {
        CurrentEcho++;

        if (CurrentEcho > 4) // Transition to next cycle
        {
            CurrentEcho = 1;
            IncrementCycle();
        }

        OnEchoChange?.Invoke(CurrentEcho);
    }

    private void IncrementCycle()
    {
        CurrentCycle++;
        currentCycleName = CurrentCycle == 1 ? "Overture" : cycleNames[UnityEngine.Random.Range(0, cycleNames.Count)];
        OnCycleChange?.Invoke(CurrentCycle);
    }

    private void UpdateUI()
    {
        // Update Cycle Text
        cycleText.text = $"Cycle {CurrentCycle} ◦ {currentCycleName}";

        // Update Echo Text
        if (CurrentEcho - 1 < echoes.Length)
        {
            echoText.text = echoes[CurrentEcho - 1].unitName;
        }

        // Update Phase Image
        if (totalPhaseIndex < phases.Length)
        {
            phaseImage.sprite = phases[totalPhaseIndex].icon;
            phaseText.text = phases[totalPhaseIndex].unitName;
        }

        // Update Seventh Text and Color
        seventhText.text = CurrentSeventh.ToString();
        seventhText.color = CurrentSeventh == 21 ? Color.red : originalColor;
    }
}

