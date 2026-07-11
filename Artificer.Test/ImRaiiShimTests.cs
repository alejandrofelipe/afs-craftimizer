// Test/ImRaiiShimTests.cs
// Tests for ImRaiiShim.PushStyle routing through UiServices.
//
// CONSTRAINT: StyleDisposable.Dispose() calls ImGui.PopStyleVar() which requires
// ImGui context (P/Invoke to cimgui.dll). Therefore:
// - We discard the return value with `_` to avoid triggering Dispose
// - FakeUiServices.PushStyleVar is a no-op — no actual ImGui call is made
// - These tests verify ROUTING only, not rendering behavior
//
using Artificer.Utils;
using ImGuiNET;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;

namespace Artificer.Test;

[TestClass]
public class ImRaiiShimTests
{
    private UIServicesTests.FakeUiServices _fake = null!;

    [TestInitialize]
    public void SetUp()
    {
        _fake = new UIServicesTests.FakeUiServices();
        UiServices.Current = _fake;
    }

    [TestCleanup]
    public void TearDown() => UiServices.Reset();

    // --- Routing tests: verify each registered ImGuiStyleVar reaches UiServices ---

    [TestMethod]
    public void PushStyle_FramePadding_Vec2_RoutesToUiServices()
    {
        // Discard return value intentionally — Dispose would call ImGui.PopStyleVar (needs ImGui context)
        _ = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);

        Assert.AreEqual(1, _fake.PushCount);
        Assert.AreEqual(ImGuiStyleVarId.FramePadding, _fake.LastVarId);
    }

    [TestMethod]
    public void PushStyle_ItemSpacing_Vec2_RoutesToUiServices()
    {
        _ = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));

        Assert.AreEqual(1, _fake.PushCount);
        Assert.AreEqual(ImGuiStyleVarId.ItemSpacing, _fake.LastVarId);
    }

    [TestMethod]
    public void PushStyle_FrameRounding_Float_RoutesToUiServices()
    {
        _ = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f);

        Assert.AreEqual(1, _fake.PushCount);
        Assert.AreEqual(ImGuiStyleVarId.FrameRounding, _fake.LastVarId);
    }

    [TestMethod]
    public void PushStyle_ChildRounding_Float_RoutesToUiServices()
    {
        _ = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 6f);

        Assert.AreEqual(1, _fake.PushCount);
        Assert.AreEqual(ImGuiStyleVarId.ChildRounding, _fake.LastVarId);
    }

    [TestMethod]
    public void PushStyle_WindowPadding_Vec2_RoutesToUiServices()
    {
        _ = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(12f, 8f));

        Assert.AreEqual(1, _fake.PushCount);
        Assert.AreEqual(ImGuiStyleVarId.WindowPadding, _fake.LastVarId);
    }

    // --- Error case: unregistered var throws immediately ---

    [TestMethod]
    public void PushStyle_UnregisteredVar_ThrowsArgumentOutOfRange()
    {
        // ImGuiStyleVar.Alpha (0) is not registered in ImGuiStyleVarId because
        // Alpha pushes only happen in Dalamud-owned code (WindowHost), never in Artificer.UI.
        // Passing it should throw immediately with a helpful message.
        var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => _ = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f));

        Assert.IsTrue(ex.Message.Contains("ImGuiStyleVarId", StringComparison.Ordinal),
            $"esperava 'ImGuiStyleVarId' na mensagem: {ex.Message}");
    }

    // --- ImGuiStyleVarId completeness: all 5 values must be present ---

    [TestMethod]
    public void ImGuiStyleVarId_HasExactlyFiveValues()
    {
        // If this fails, a value was added or removed from the enum without updating
        // DalamudUiServices.ToDalamud() and StubUiServices.ToImGuiNET().
        var values = Enum.GetValues<ImGuiStyleVarId>();
        Assert.AreEqual(5, values.Length,
            "ImGuiStyleVarId must have exactly 5 values. " +
            "If you added a new one, also update DalamudUiServices.ToDalamud() and StubUiServices.ToImGuiNET().");
    }

    [TestMethod]
    public void ImGuiStyleVarId_ContainsAllExpectedMembers()
    {
        var values = Enum.GetNames<ImGuiStyleVarId>();
        CollectionAssert.Contains(values, nameof(ImGuiStyleVarId.WindowPadding));
        CollectionAssert.Contains(values, nameof(ImGuiStyleVarId.FrameRounding));
        CollectionAssert.Contains(values, nameof(ImGuiStyleVarId.ChildRounding));
        CollectionAssert.Contains(values, nameof(ImGuiStyleVarId.FramePadding));
        CollectionAssert.Contains(values, nameof(ImGuiStyleVarId.ItemSpacing));
    }
}
