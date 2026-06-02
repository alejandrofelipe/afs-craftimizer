#if DEBUG
using Craftimizer.Plugin;
using Craftimizer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Craftimizer.Utils.ProgressBarComponent;

namespace Craftimizer.Windows;

/// <summary>
/// Test window for validating ProgressBarComponent functionality.
/// This is a temporary window for Fase 3 validation and should be removed after testing.
/// </summary>
public sealed class ProgressBarTestWindow : Window, IDisposable
{
    private readonly Configuration _config;
    
    // Test state
    private int _testProgress = 50;
    private int _testMax = 100;
    private int _selectedMode = 0;
    private int _selectedState = 0;
    private int _selectedTheme = 0;
    private bool _showPercentage = true;
    private bool _showTooltip = true;
    private string _customTooltip = "";
    private int _snapshotCount = 3;
    
    // Test snapshots for aggregated mode
    private readonly List<ProgressSnapshot> _testSnapshots = new();

    public ProgressBarTestWindow(Configuration config) : base("ProgressBar Test Window###ProgressBarTest")
    {
        _config = config;
        
        Size = new Vector2(600, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        InitializeTestSnapshots();
    }

    private void InitializeTestSnapshots()
    {
        _testSnapshots.Clear();
        _testSnapshots.Add(new ProgressSnapshot(
            Name: "MCTS Algorithm",
            CurrentValue: 1000,
            MaxValue: 1000,
            State: ProgressState.Completed,
            Stage: 3
        ));
        _testSnapshots.Add(new ProgressSnapshot(
            Name: "Raphael Solver",
            CurrentValue: 650,
            MaxValue: 1000,
            State: ProgressState.InProgress,
            Stage: 2
        ));
        _testSnapshots.Add(new ProgressSnapshot(
            Name: "Stepwise Search",
            CurrentValue: 200,
            MaxValue: 500,
            State: ProgressState.InProgress,
            Stage: 1
        ));
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Test window for ProgressBarComponent - Fase 3 validation");
        ImGui.Separator();

        DrawControlPanel();
        ImGui.Separator();

        DrawSingleProgressTests();
        ImGui.Separator();

        DrawAggregatedProgressTests();
        ImGui.Separator();

        DrawEdgeCaseTests();
    }

    private void DrawControlPanel()
    {
        if (ImGui.CollapsingHeader("Control Panel", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Text("Simple Progress Controls:");
            ImGui.SliderInt("Progress", ref _testProgress, 0, _testMax);
            ImGui.SliderInt("Max", ref _testMax, 1, 200);
            
            ImGui.Spacing();
            ImGui.Text("Display Mode:");
            ImGui.RadioButton("Horizontal", ref _selectedMode, 0); ImGui.SameLine();
            ImGui.RadioButton("Arc", ref _selectedMode, 1); ImGui.SameLine();
            ImGui.RadioButton("Compact", ref _selectedMode, 2); ImGui.SameLine();
            ImGui.RadioButton("Stacked", ref _selectedMode, 3);
            
            ImGui.Spacing();
            ImGui.Text("State:");
            ImGui.RadioButton("InProgress", ref _selectedState, 0); ImGui.SameLine();
            ImGui.RadioButton("Completed", ref _selectedState, 1);
            ImGui.RadioButton("Indeterminate", ref _selectedState, 2); ImGui.SameLine();
            ImGui.RadioButton("Cancelled", ref _selectedState, 3); ImGui.SameLine();
            ImGui.RadioButton("Failed", ref _selectedState, 4);
            
            ImGui.Spacing();
            ImGui.Text("Color Theme:");
            ImGui.RadioButton("Colorful", ref _selectedTheme, 0); ImGui.SameLine();
            ImGui.RadioButton("Simple", ref _selectedTheme, 1);
            
            ImGui.Spacing();
            ImGui.Checkbox("Show Percentage", ref _showPercentage);
            ImGui.Checkbox("Show Detailed Tooltip", ref _showTooltip);
            
            ImGui.Spacing();
            ImGui.Text("Custom Tooltip (leave empty for default):");
            ImGui.InputText("##customTooltip", ref _customTooltip, 256);
            
            ImGui.Spacing();
            ImGui.Text("Aggregated Test:");
            if (ImGui.SliderInt("Snapshot Count", ref _snapshotCount, 1, 10))
            {
                UpdateSnapshotCount();
            }
            if (ImGui.Button("Randomize Progress"))
            {
                RandomizeSnapshots();
            }
        }
    }

    private void DrawSingleProgressTests()
    {
        if (ImGui.CollapsingHeader("Single Progress Tests", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var mode = (DisplayMode)_selectedMode;
            var state = (ProgressState)_selectedState;
            var theme = (Configuration.ProgressBarType)_selectedTheme;
            
            var config = new VisualConfig(
                Mode: mode,
                ColorTheme: theme,
                Width: mode == DisplayMode.Arc ? 120f : 400f,
                Height: mode == DisplayMode.Arc ? 120f : 24f,
                ShowPercentage: _showPercentage,
                ShowDetailedTooltip: _showTooltip
            );

            ImGui.Text($"Test: {mode} mode, {state} state, {theme} theme");
            
            var tooltip = string.IsNullOrWhiteSpace(_customTooltip) ? null : _customTooltip;
            
            ProgressBarComponent.DrawSimple(
                currentValue: _testProgress,
                maxValue: _testMax,
                state: state,
                config: config,
                tooltipOverride: tooltip
            );
            
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), 
                $"Fraction: {(float)_testProgress / _testMax:P1}");
        }
    }

    private void DrawAggregatedProgressTests()
    {
        if (ImGui.CollapsingHeader("Aggregated Progress Tests", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var mode = (DisplayMode)_selectedMode;
            var theme = (Configuration.ProgressBarType)_selectedTheme;
            
            var config = new VisualConfig(
                Mode: mode,
                ColorTheme: theme,
                Width: mode == DisplayMode.Arc ? 120f : 400f,
                Height: mode == DisplayMode.Arc ? 120f : 24f,
                ShowPercentage: _showPercentage,
                ShowDetailedTooltip: _showTooltip,
                ShowSummaryWhenAggregated: true
            );

            ImGui.Text($"Aggregated: {_testSnapshots.Count} snapshots");
            
            ProgressBarComponent.DrawAggregated(_testSnapshots, config);
            
            ImGui.Spacing();
            var completedCount = _testSnapshots.Count(s => s.State == ProgressState.Completed);
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), 
                $"Completed: {completedCount}/{_testSnapshots.Count} = {(float)completedCount / (float)_testSnapshots.Count:P0}");
            
            ImGui.Spacing();
            ImGui.Text("Snapshot Details:");
            foreach (var snapshot in _testSnapshots)
            {
                var stateColor = snapshot.State switch
                {
                    ProgressState.Completed => new Vector4(0f, 1f, 0f, 1f),
                    ProgressState.InProgress => new Vector4(1f, 1f, 0f, 1f),
                    ProgressState.Cancelled => new Vector4(0.5f, 0.5f, 0.5f, 1f),
                    ProgressState.Failed => new Vector4(1f, 0f, 0f, 1f),
                    _ => new Vector4(1f, 1f, 1f, 1f)
                };
                ImGui.TextColored(stateColor, 
                    $"  {snapshot.Name}: {snapshot.CurrentValue}/{snapshot.MaxValue} ({snapshot.Fraction:P0}) - {snapshot.State}");
            }
        }
    }

    private void DrawEdgeCaseTests()
    {
        if (ImGui.CollapsingHeader("Edge Case Tests"))
        {
            ImGui.Text("Edge Case 1: Empty snapshot list");
            var emptyConfig = new VisualConfig(
                Mode: DisplayMode.Horizontal,
                Width: 400f
            );
            ProgressBarComponent.DrawAggregated(new List<ProgressSnapshot>(), emptyConfig);
            
            ImGui.Spacing();
            ImGui.Text("Edge Case 2: All completed");
            var allCompleted = new List<ProgressSnapshot>
            {
                new("Task 1", 100, 100, ProgressState.Completed),
                new("Task 2", 100, 100, ProgressState.Completed),
                new("Task 3", 100, 100, ProgressState.Completed)
            };
            ProgressBarComponent.DrawAggregated(allCompleted, emptyConfig);
            
            ImGui.Spacing();
            ImGui.Text("Edge Case 3: All indeterminate");
            var allIndeterminate = new List<ProgressSnapshot>
            {
                new("Loading 1", 0, 100, ProgressState.Indeterminate),
                new("Loading 2", 0, 100, ProgressState.Indeterminate)
            };
            ProgressBarComponent.DrawAggregated(allIndeterminate, emptyConfig);
            
            ImGui.Spacing();
            ImGui.Text("Edge Case 4: Mixed states");
            var mixedStates = new List<ProgressSnapshot>
            {
                new("Completed", 100, 100, ProgressState.Completed),
                new("In Progress", 50, 100, ProgressState.InProgress),
                new("Cancelled", 30, 100, ProgressState.Cancelled),
                new("Failed", 80, 100, ProgressState.Failed),
                new("Indeterminate", 0, 100, ProgressState.Indeterminate)
            };
            ProgressBarComponent.DrawAggregated(mixedStates, emptyConfig);
            
            ImGui.Spacing();
            ImGui.Text("Edge Case 5: Custom colors");
            var customColorSnapshot = new ProgressSnapshot(
                Name: "Custom Color",
                CurrentValue: 75,
                MaxValue: 100,
                State: ProgressState.InProgress,
                CustomColor: new Vector4(1f, 0.5f, 0f, 1f) // Orange
            );
            ProgressBarComponent.DrawSingle(customColorSnapshot, emptyConfig);
        }
    }

    private void UpdateSnapshotCount()
    {
        var random = new Random();
        _testSnapshots.Clear();
        
        for (int i = 0; i < _snapshotCount; i++)
        {
            var progress = random.Next(0, 101);
            var state = progress >= 100 
                ? ProgressState.Completed 
                : (progress > 0 ? ProgressState.InProgress : ProgressState.Indeterminate);
            
            _testSnapshots.Add(new ProgressSnapshot(
                Name: $"Algorithm {i + 1}",
                CurrentValue: progress,
                MaxValue: 100,
                State: state,
                Stage: i
            ));
        }
    }

    private void RandomizeSnapshots()
    {
        var random = new Random();
        _testSnapshots.Clear();
        
        var states = new[] 
        { 
            ProgressState.InProgress, 
            ProgressState.Completed, 
            ProgressState.Indeterminate,
            ProgressState.Cancelled,
            ProgressState.Failed
        };
        
        for (int i = 0; i < _snapshotCount; i++)
        {
            var state = states[random.Next(states.Length)];
            var progress = state == ProgressState.Completed ? 100 :
                          state == ProgressState.Indeterminate ? 0 :
                          random.Next(1, 100);
            
            _testSnapshots.Add(new ProgressSnapshot(
                Name: $"Task {i + 1}",
                CurrentValue: progress,
                MaxValue: 100,
                State: state,
                Stage: i
            ));
        }
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
#endif
