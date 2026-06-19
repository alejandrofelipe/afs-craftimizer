using Artificer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test.UI;

[TestClass]
public class ContentWidthTests
{
    // Garante stack limpo entre testes (estado estático compartilhado).
    [TestInitialize]
    public void SetUp() => ImGuiUtils.DrainContentWidthForTests();

    [TestMethod]
    public void CurrentContentWidth_IsNull_WhenNoPanelActive()
    {
        Assert.IsNull(ImGuiUtils.CurrentContentWidth);
    }

    [TestMethod]
    public void Push_Then_Pop_RestoresNull()
    {
        ImGuiUtils.PushContentWidth(200f);
        Assert.AreEqual(200f, ImGuiUtils.CurrentContentWidth);
        ImGuiUtils.PopContentWidth();
        Assert.IsNull(ImGuiUtils.CurrentContentWidth);
    }

    [TestMethod]
    public void Push_Nested_PeeksInnermost()
    {
        ImGuiUtils.PushContentWidth(300f);
        ImGuiUtils.PushContentWidth(120f);
        Assert.AreEqual(120f, ImGuiUtils.CurrentContentWidth);
        ImGuiUtils.PopContentWidth();
        Assert.AreEqual(300f, ImGuiUtils.CurrentContentWidth);
        ImGuiUtils.PopContentWidth();
    }

    [TestMethod]
    public void Pop_OnEmptyStack_IsSafe()
    {
        // Não deve lançar nem deixar o stack inconsistente.
        ImGuiUtils.PopContentWidth();
        Assert.IsNull(ImGuiUtils.CurrentContentWidth);
    }

    [TestMethod]
    public void NonPositiveWidth_ReadsAsNull()
    {
        // width == 0 (size-to-content) e width negativa não impõem largura: sem constraint.
        ImGuiUtils.PushContentWidth(0f);
        Assert.IsNull(ImGuiUtils.CurrentContentWidth);
        ImGuiUtils.PopContentWidth();

        ImGuiUtils.PushContentWidth(-1f);
        Assert.IsNull(ImGuiUtils.CurrentContentWidth);
        ImGuiUtils.PopContentWidth();
    }

    [TestMethod]
    public void ResolveAvailWidth_ExplicitWins_OverPanelAndFallback()
    {
        ImGuiUtils.PushContentWidth(200f);
        Assert.AreEqual(75f, ImGuiUtils.ResolveAvailWidth(75f, fallback: 9999f));
        ImGuiUtils.PopContentWidth();
    }

    [TestMethod]
    public void ResolveAvailWidth_InsidePanel_ClampsToPanel_NotContentRegion()
    {
        // GARANTIA ANTI-CRESCIMENTO: mesmo com uma "borda da janela" enorme (fallback),
        // um helper dentro do painel dimensiona pela largura FIXA do painel, nunca pelo
        // content region — então não pode realimentar o auto-resize.
        ImGuiUtils.PushContentWidth(200f);
        Assert.AreEqual(200f, ImGuiUtils.ResolveAvailWidth(default, fallback: 9999f));
        ImGuiUtils.PopContentWidth();
    }

    [TestMethod]
    public void ResolveAvailWidth_InsidePanel_PrefersNarrowerFallback()
    {
        // Dentro de uma célula de tabela/child mais estreita que o painel, o fallback
        // (content region da célula) vence — o helper não deve estourar a célula com a
        // largura do painel. ResolveAvailWidth = min(largura do painel, fallback).
        ImGuiUtils.PushContentWidth(320f);
        Assert.AreEqual(160f, ImGuiUtils.ResolveAvailWidth(default, fallback: 160f));
        ImGuiUtils.PopContentWidth();
    }

    [TestMethod]
    public void ResolveAvailWidth_OutsidePanel_UsesFallback()
    {
        Assert.AreEqual(9999f, ImGuiUtils.ResolveAvailWidth(default, fallback: 9999f));
    }

    [TestMethod]
    public void ResolveAvailWidth_SizeToContentPanel_UsesFallback()
    {
        ImGuiUtils.PushContentWidth(0f); // size-to-content => sem constraint
        Assert.AreEqual(9999f, ImGuiUtils.ResolveAvailWidth(default, fallback: 9999f));
        ImGuiUtils.PopContentWidth();
    }
}
