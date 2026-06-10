// Test/UIServicesTests.cs
using Craftimizer.Utils;
using ImGuiNET;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Craftimizer.Test;

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

    private sealed class FakeUiServices : IUiServices
    {
        public float GlobalScale => 1.0f;
        public ImFontPtr IconFont => default;
    }
}
