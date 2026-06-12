// Test/UIServicesTests.cs
using Artificer.Utils;
using ImGuiNET;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;

namespace Artificer.Test;

[TestClass]
public class UIServicesTests
{
    [TestCleanup]
    public void TearDown() => UiServices.Reset();

    [TestMethod]
    public void Current_ThrowsIfNotSet()
    {
        Assert.ThrowsException<InvalidOperationException>(() => _ = UiServices.Current);
    }

    [TestMethod]
    public void Current_ReturnsAssignedInstance()
    {
        var stub = new FakeUiServices();
        UiServices.Current = stub;
        Assert.AreSame(stub, UiServices.Current);
        Assert.AreEqual(1.0f, UiServices.Current.GlobalScale);
    }

    [TestMethod]
    public void PushStyleVar_Float_RecordsCallOnFake()
    {
        var fake = new FakeUiServices();
        UiServices.Current = fake;

        UiServices.Current.PushStyleVar(ImGuiStyleVarId.FrameRounding, 4f);

        Assert.AreEqual(1, fake.PushCount);
        Assert.AreEqual(ImGuiStyleVarId.FrameRounding, fake.LastVarId);
    }

    [TestMethod]
    public void PushStyleVar_Vec2_RecordsCallOnFake()
    {
        var fake = new FakeUiServices();
        UiServices.Current = fake;

        UiServices.Current.PushStyleVar(ImGuiStyleVarId.WindowPadding, new Vector2(12f, 8f));

        Assert.AreEqual(1, fake.PushCount);
        Assert.AreEqual(ImGuiStyleVarId.WindowPadding, fake.LastVarId);
    }

    // Shared fake used across test files via internal visibility.
    // PushStyleVar is a no-op (does NOT call ImGui.PushStyleVar) so tests run without ImGui context.
    internal sealed class FakeUiServices : IUiServices
    {
        public int PushCount { get; private set; }
        public ImGuiStyleVarId? LastVarId { get; private set; }

        public float GlobalScale => 1.0f;
        public ImFontPtr IconFont => default;
        public ImFontPtr DefaultFont => default;
        public void OpenLink(string url) { }

        public void PushStyleVar(ImGuiStyleVarId var, float val)
        {
            PushCount++;
            LastVarId = var;
        }

        public void PushStyleVar(ImGuiStyleVarId var, Vector2 val)
        {
            PushCount++;
            LastVarId = var;
        }
    }
}
