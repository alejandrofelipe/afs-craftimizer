using Craftimizer.Utils;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;

namespace Craftimizer.UIStudio;

internal sealed class StudioWindow
{
    private readonly IReadOnlyList<IStory> _stories;
    private IStory? _selected;

    public StudioWindow(IReadOnlyList<IStory> stories)
    {
        _stories = stories;
        _selected = stories.Count > 0 ? stories[0] : null;
    }

    public void Draw()
    {
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(io.DisplaySize);

        Theme.Push();
        if (ImGui.Begin("##studio", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove))
        {
            DrawSidebar();
            ImGui.SameLine();
            DrawCanvas();
        }
        ImGui.End();
        Theme.Pop();
    }

    private void DrawSidebar()
    {
        ImGui.BeginChild("##sidebar", new Vector2(220, 0), ImGuiChildFlags.Border);

        ImGui.TextDisabled("STORIES");
        ImGui.Separator();

        string? currentCategory = null;
        foreach (var story in _stories)
        {
            if (story.Category != currentCategory)
            {
                currentCategory = story.Category;
                ImGui.Spacing();
                ImGui.TextDisabled(story.Category.ToUpperInvariant());
            }

            var isSelected = ReferenceEquals(story, _selected);
            if (ImGui.Selectable($"  {story.Name}", isSelected))
                _selected = story;
        }

        ImGui.EndChild();
    }

    private void DrawCanvas()
    {
        ImGui.BeginChild("##canvas", Vector2.Zero, ImGuiChildFlags.None);

        if (_selected is { } story)
        {
            ImGui.TextDisabled($"{story.Category} / {story.Name}");
            ImGui.Separator();
            ImGui.Spacing();

            Theme.Push();
            story.Draw();
            Theme.Pop();
        }
        else
        {
            ImGui.TextDisabled("Selecione uma story na barra lateral.");
        }

        ImGui.EndChild();
    }
}
