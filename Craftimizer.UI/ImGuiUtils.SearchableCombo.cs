using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Craftimizer.Utils;

internal static partial class ImGuiUtils
{
    private sealed class SearchableComboData<T> where T : IEquatable<T>
    {
        public readonly ImmutableArray<T> items;
        public List<T> filteredItems;
        public T selectedItem;
        public string input;
        public bool wasTextActive;
        public bool wasPopupActive;
        public CancellationTokenSource? cts;
        public Task? task;

        private readonly Func<T, string> getString;

        public SearchableComboData(IEnumerable<T> items, T selectedItem, Func<T, string> getString)
        {
            this.items = [.. items];
            filteredItems = [selectedItem];
            this.selectedItem = selectedItem;
            this.getString = getString;
            input = GetString(selectedItem);
        }

        public void SetItem(T selectedItem)
        {
            if (!this.selectedItem.Equals(selectedItem))
            {
                input = GetString(selectedItem);
                this.selectedItem = selectedItem;
            }
        }

        public string GetString(T item) => getString(item);

        public void Filter()
        {
            cts?.Cancel();
            var inp = input;
            cts = new();
            var token = cts.Token;
            task = Task.Run(() => FilterTask(inp, token), token)
                .ContinueWith(t =>
            {
                if (cts.IsCancellationRequested)
                    return;

                try
                {
                    t.Exception!.Flatten().Handle(ex => ex is TaskCanceledException or OperationCanceledException);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Filtering recipes failed: {e}");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void FilterTask(string input, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                filteredItems = [.. items];
                return;
            }
            var matcher = new FuzzyMatcher(input.ToLowerInvariant(), MatchMode.FuzzyParts);
            var query = items.AsParallel().Select(i => (Item: i, Score: matcher.Matches(getString(i).ToLowerInvariant())))
                .Where(t => t.Score > 0)
                .OrderByDescending(t => t.Score)
                .Select(t => t.Item);
            token.ThrowIfCancellationRequested();
            filteredItems = [.. query];
        }
    }

    private static readonly Dictionary<uint, object> ComboData = [];

    private static SearchableComboData<T> GetComboData<T>(uint comboKey, IEnumerable<T> items, T selectedItem, Func<T, string> getString) where T : IEquatable<T> =>
        (SearchableComboData<T>)(
            ComboData.TryGetValue(comboKey, out var data)
            ? data
            : ComboData[comboKey] = new SearchableComboData<T>(items, selectedItem, getString));

    // https://github.com/ocornut/imgui/issues/718#issuecomment-1563162222
    public static bool SearchableCombo<T>(string id, ref T selectedItem, IEnumerable<T> items, ImFontPtr selectableFont, float width, Func<T, string> getString, Func<T, string> getId, Action<T> draw) where T : IEquatable<T>
    {
        var comboKey = ImGui.GetID(id);
        var data = GetComboData(comboKey, items, selectedItem, getString);
        data.SetItem(selectedItem);

        using var pushId = ImRaii.PushId(id);

        width = width == 0 ? ImGui.GetContentRegionAvail().X : width;
        var availableSpace = Math.Min(ImGui.GetContentRegionAvail().X, width);
        ImGui.SetNextItemWidth(availableSpace);
        var isInputTextEnterPressed = ImGui.InputText("##input", ref data.input, 256, ImGuiInputTextFlags.EnterReturnsTrue);
        var min = ImGui.GetItemRectMin();
        var size = ImGui.GetItemRectSize();
        size.X = Math.Min(size.X, availableSpace);

        var isInputTextActivated = ImGui.IsItemActivated();

        if (isInputTextActivated)
        {
            ImGui.SetNextWindowPos(min - ImGui.GetStyle().WindowPadding);
            ImGui.OpenPopup("##popup");
            data.wasTextActive = false;
        }

        using (var popup = ImRaii.Popup("##popup", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            if (popup)
            {
                data.wasPopupActive = true;

                if (isInputTextActivated)
                {
                    ImGui.SetKeyboardFocusHere(0);
                    data.Filter();
                }
                ImGui.SetNextItemWidth(size.X);
                if (ImGui.InputText("##input_popup", ref data.input, 256))
                    data.Filter();
                var isActive = ImGui.IsItemActive();
                if (!isActive && data.wasTextActive && ImGui.IsKeyPressed(ImGuiKey.Enter))
                    isInputTextEnterPressed = true;
                data.wasTextActive = isActive;

                using (var scrollingRegion = ImRaii.Child("scrollingRegion", new Vector2(size.X, size.Y * 10), false, ImGuiWindowFlags.HorizontalScrollbar))
                {
                    T? _selectedItem = default;
                    var height = ImGui.GetTextLineHeight();
                    var r = ListClip(data.filteredItems, height, t =>
                    {
                        var name = getString(t);
                        using (var selectFont = ImRaii.PushFont(selectableFont))
                        {
                            if (ImGui.Selectable($"##{getId(t)}"))
                            {
                                _selectedItem = t;
                                return true;
                            }
                        }
                        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X / 2f);
                        draw(t);
                        return false;
                    });
                    if (r)
                    {
                        selectedItem = _selectedItem!;
                        data.SetItem(selectedItem);
                        ImGui.CloseCurrentPopup();
                        return true;
                    }
                }

                if (isInputTextEnterPressed || ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    if (isInputTextEnterPressed && data.filteredItems.Count > 0)
                    {
                        selectedItem = data.filteredItems[0];
                        data.SetItem(selectedItem);
                    }
                    ImGui.CloseCurrentPopup();
                    return true;
                }
            }
            else
            {
                if (data.wasPopupActive)
                {
                    data.wasPopupActive = false;
                    data.input = getString(selectedItem);
                }
            }
        }

        return false;
    }

    private static bool ListClip<T>(List<T> data, float lineHeight, Predicate<T> func)
    {
        ImGuiListClipperPtr imGuiListClipperPtr;
        unsafe
        {
            imGuiListClipperPtr = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
        }
        try
        {
            imGuiListClipperPtr.Begin(data.Count, lineHeight);
            while (imGuiListClipperPtr.Step())
            {
                for (var i = imGuiListClipperPtr.DisplayStart; i <= imGuiListClipperPtr.DisplayEnd; i++)
                {
                    if (i >= data.Count)
                        return false;

                    if (i >= 0)
                    {
                        if (func(data[i]))
                            return true;
                    }
                }
            }
            return false;
        }
        finally
        {
            imGuiListClipperPtr.End();
            imGuiListClipperPtr.Destroy();
        }
    }
}
