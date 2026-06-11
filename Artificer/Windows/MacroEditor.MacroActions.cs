using Artificer.Plugin;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;

namespace Artificer.Windows;

public sealed partial class MacroEditor
{
    private void DrawMacroActions(float availWidth)
    {
        var height = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var hasMacro = Macro.Count > 0;
        var hasDefault = DefaultActions.Length > 0;

        var smallCount = 2 + (hasDefault ? 1 : 0) + (hasMacro ? 1 : 0);
        var width = availWidth - ((spacing + height) * smallCount);
        var halfWidth = (width - spacing) / 2f;

        using (var _disabled = ImRaii.Disabled(SolverRunning || !hasMacro))
        {
            if (ImGui.Button("Save Macro", new(halfWidth, height)))
            {
                if (MacroSetter != null)
                    SaveMacro();
                else
                    ShowSaveAsPopup();
            }
        }
        DrawSaveAsPopup();

        ImGui.SameLine();

        if (SolverRunning)
        {
            if (SolverTask?.Cancelling ?? false)
            {
                using var _disabled = ImRaii.Disabled();
                ImGui.Button("Stopping...", new(halfWidth, height));
            }
            else
            {
                Theme.PushPrimaryButton();
                if (ImGui.Button("Stop", new(halfWidth, height)))
                    SolverTask?.Cancel();
                Theme.PopPrimaryButton();
            }
        }
        else
        {
            if (!hasMacro)
                Theme.PushPrimaryButton();
            if (ImGui.Button("Solve & Replace", new(halfWidth, height)))
                CalculateBestMacro();
            if (!hasMacro)
                Theme.PopPrimaryButton();
        }

        ImGui.SameLine();
        if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Paste))
            MacroCopy.Copy(Macro.Actions.ToArray(), _plugin);
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("Copy to Clipboard");

        ImGui.SameLine();
        using (var _disabled = ImRaii.Disabled(SolverRunning))
        {
            if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.FileImport))
                ShowImportPopup();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.Tooltip("Import Macro");
        DrawImportPopup();

        if (hasDefault)
        {
            ImGui.SameLine();
            using (var _disabled = ImRaii.Disabled(SolverRunning))
            {
                if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Undo))
                {
                    SolverStartStepCount = null;
                    Macro.Clear();
                    foreach (var action in DefaultActions)
                        AddStep(action);
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGuiUtils.Tooltip("Reset");
        }

        if (hasMacro)
        {
            ImGui.SameLine();
            Theme.PushDangerButton();
            using (var _disabled = ImRaii.Disabled(SolverRunning))
            {
                if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Trash))
                {
                    SolverStartStepCount = null;
                    Macro.Clear();
                }
            }
            Theme.PopDangerButton();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGuiUtils.Tooltip("Clear");
        }
    }

    private void ShowSaveAsPopup()
    {
        ImGui.OpenPopup($"##saveAsPopup");
        popupSaveAsMacroName = string.Empty;
        ImGui.SetNextWindowPos(ImGui.GetMousePos() - new Vector2(ImGui.CalcItemWidth() * .25f, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
    }

    private void DrawSaveAsPopup()
    {
        using var popup = ImRaii.Popup($"##saveAsPopup");
        if (popup)
        {
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(ImGui.CalcItemWidth());
            if (ImGui.InputTextWithHint($"##setName", "Name", ref popupSaveAsMacroName, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrWhiteSpace(popupSaveAsMacroName))
                {
                    var newMacro = new Macro() { Name = popupSaveAsMacroName, Actions = Macro.Actions.ToArray() };
                    _plugin.MacroRepository.Add(newMacro);
                    MacroSetter = actions =>
                    {
                        newMacro.ActionEnumerable = actions;
                        _plugin.MacroRepository.Update(newMacro);
                    };
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    private void ShowImportPopup()
    {
        ImGui.OpenPopup($"##importPopup");
        popupImportText = string.Empty;
        popupImportUrl = string.Empty;
        popupImportError = string.Empty;
        popupImportUrlMacro = null;
        popupImportUrlTokenSource = null;
    }

    private void DrawImportPopup()
    {
        const string ExampleMacro = "/mlock\n/ac \"Muscle Memory\" <wait.3>\n/ac Manipulation <wait.2>\n/ac Veneration <wait.2>\n/ac \"Waste Not II\" <wait.2>\n/ac Groundwork <wait.3>\n/ac Innovation <wait.2>\n/ac \"Preparatory Touch\" <wait.3>\n/ac \"Preparatory Touch\" <wait.3>\n/ac \"Preparatory Touch\" <wait.3>\n/ac \"Preparatory Touch\" <wait.3>\n/ac \"Great Strides\" <wait.2>\n/ac \"Byregot's Blessing\" <wait.3>\n/ac \"Careful Synthesis\" <wait.3>";
        const string ExampleUrl = "https://ffxivteamcraft.com/simulator/39630/35499/9XOZDZKhbVXJUIPXjM63";

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new Vector2(0.5f));
        ImGui.SetNextWindowSizeConstraints(new(400, 0), new(float.PositiveInfinity));
        using var popup = ImRaii.Popup($"##importPopup", ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoMove);
        if (popup)
        {
            bool submittedText, submittedUrl;

            using (var panel = ImRaii2.GroupPanel("##text", -1, out var availWidth))
            {
                ImGui.AlignTextToFramePadding();
                ImGuiUtils.TextCentered("Paste your macro here");
                {
                    using var font = ImRaii.PushFont(UiBuilder.MonoFont);
                    ImGuiUtils.InputTextMultilineWithHint("", ExampleMacro, ref popupImportText, 2048, new(availWidth, ImGui.GetTextLineHeight() * 15 + ImGui.GetStyle().FramePadding.Y), (int)ImGuiInputTextFlags.AutoSelectAll);
                }
                using (var _disabled = ImRaii.Disabled(popupImportUrlTokenSource != null))
                    submittedText = ImGui.Button("Import", new(availWidth, 0));
            }

            using (var panel = ImRaii2.GroupPanel("##url", -1, out var availWidth))
            {
                var availOffset = ImGui.GetContentRegionAvail().X - availWidth;

                ImGui.AlignTextToFramePadding();
                ImGuiUtils.TextCentered("or provide a url to it");
                ImGui.SameLine();
                using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey))
                {
                    using var font = ImRaii.PushFont(UiBuilder.IconFont);
                    ImGuiUtils.TextRight(FontAwesomeIcon.InfoCircle.ToIconString(), ImGui.GetContentRegionAvail().X - availOffset);
                }
                if (ImGui.IsItemHovered())
                {
                    using var t = ImRaii.Tooltip();
                    ImGui.TextUnformatted("Supported sites:");
                    ImGui.BulletText("ffxivteamcraft.com");
                    ImGui.BulletText("craftingway.app");
                    ImGui.TextUnformatted("More suggestions are appreciated!");
                }
                ImGui.SetNextItemWidth(availWidth);
                submittedUrl = ImGui.InputTextWithHint("", ExampleUrl, ref popupImportUrl, 2048, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue);
                using (var _disabled = ImRaii.Disabled(popupImportUrlTokenSource != null))
                    submittedUrl = ImGui.Button("Import", new(availWidth, 0)) || submittedUrl;
            }

            ImGui.Dummy(default);

            if (!string.IsNullOrWhiteSpace(popupImportError))
            {
                using (var c = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                    ImGui.TextWrapped(popupImportError);
                ImGui.Dummy(default);
            }

            if (ImGuiUtils.ButtonCentered("Nevermind", new(ImGui.GetContentRegionAvail().X / 2f, 0)))
            {
                popupImportUrlTokenSource?.Cancel();
                ImGui.CloseCurrentPopup();
            }

            if (popupImportUrlTokenSource == null)
            {
                if (submittedText)
                {
                    if (MacroImport.TryParseMacro(popupImportText) is { } parsedActions)
                    {
                        popupImportUrlTokenSource?.Cancel();
                        Macro.Clear();
                        foreach (var action in parsedActions)
                            AddStep(action);

                        Plugin.Plugin.DisplayNotification(new()
                        {
                            Content = $"Imported macro with {parsedActions.Count} step{(parsedActions.Count != 1 ? "s" : "")}",
                            MinimizedText = $"Imported {parsedActions.Count} step macro",
                            Title = "Macro Imported",
                            Type = NotificationType.Success
                        });
                        popupImportUrlTokenSource?.Cancel();
                        ImGui.CloseCurrentPopup();
                    }
                    else
                        popupImportError = "Could not find any actions to import. Is it a valid macro?";
                }
                if (submittedUrl)
                {
                    if (MacroImport.TryParseUrl(popupImportUrl, out _))
                    {
                        popupImportUrlTokenSource = new();
                        popupImportUrlMacro = null;
                        var token = popupImportUrlTokenSource.Token;
                        var url = popupImportUrl;

                        var task = Task.Run(() => MacroImport.RetrieveUrl(url, token), token);
                        _ = task.ContinueWith(t =>
                        {
                            if (token == popupImportUrlTokenSource.Token)
                                popupImportUrlTokenSource = null;
                        });
                        _ = task.ContinueWith(t =>
                        {
                            if (token.IsCancellationRequested)
                                return;

                            try
                            {
                                t.Exception!.Flatten().Handle(ex => ex is TaskCanceledException or OperationCanceledException);
                            }
                            catch (AggregateException e)
                            {
                                if (e.InnerExceptions.Count == 1)
                                    popupImportError = e.InnerExceptions[0].Message;
                                else
                                    popupImportError = e.Message;
                                Log.Error(e, "Retrieving macro failed");
                            }
                        }, TaskContinuationOptions.OnlyOnFaulted);
                        _ = task.ContinueWith(t => popupImportUrlMacro = t.Result, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    else
                        popupImportError = "The url is not in the right format for any supported sites.";
                }
                if (popupImportUrlMacro is { Name: var name, Actions: var actions })
                {
                    Macro.Clear();
                    foreach (var action in actions)
                        AddStep(action);
                    Plugin.Plugin.DisplayNotification(new()
                    {
                        Content = $"Imported macro \"{name}\"",
                        Title = "Macro Imported",
                        Type = NotificationType.Success
                    });

                    popupImportUrlTokenSource?.Cancel();
                    ImGui.CloseCurrentPopup();
                }
            }
        }
        else
        {
            popupImportUrlTokenSource?.Cancel();
            popupImportUrlTokenSource = null;
        }
    }
}
