using System.Numerics;
using Artificer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test;

[TestClass]
public class ContrastTextTests
{
    // ContrastText retorna escuro (~0.08) para fundo claro, claro (~0.96) para fundo escuro.
    private static bool IsDark(Vector4 c) => c.X < 0.5f;

    [TestMethod]
    public void White_ReturnsDarkText()
        => Assert.IsTrue(IsDark(Colors.ContrastText(new Vector4(1f, 1f, 1f, 1f))));

    [TestMethod]
    public void Black_ReturnsLightText()
        => Assert.IsFalse(IsDark(Colors.ContrastText(new Vector4(0f, 0f, 0f, 1f))));

    [TestMethod]
    public void BrightStatFill_ReturnsDarkText()
        => Assert.IsTrue(IsDark(Colors.ContrastText(Colors.Progress)));   // teal-green claro

    [TestMethod]
    public void DarkColor_ReturnsLightText()
        => Assert.IsFalse(IsDark(Colors.ContrastText(new Vector4(0.10f, 0.10f, 0.40f, 1f))));

    [TestMethod]
    public void LowAlphaBright_ReturnsLightText()
        // branco com alpha baixo ≈ efetivamente escuro sobre o FrameBg → texto claro
        => Assert.IsFalse(IsDark(Colors.ContrastText(new Vector4(1f, 1f, 1f, 0.2f))));
}
