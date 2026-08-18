using Artificer.Utils;

namespace Artificer.Test.Utils;

[TestClass]
public class ExecutionGenerationTests
{
    [TestMethod]
    public void Next_InvalidatesPreviousGeneration()
    {
        var generations = new ExecutionGeneration();

        var previous = generations.Next();
        var current = generations.Next();

        Assert.AreNotEqual(previous, current);
        Assert.IsFalse(generations.IsCurrent(previous));
        Assert.IsTrue(generations.IsCurrent(current));
    }

    [TestMethod]
    public void TryInvalidate_OldGeneration_DoesNotInvalidateNewGeneration()
    {
        var generations = new ExecutionGeneration();
        var oldGeneration = generations.Next();
        var currentGeneration = generations.Next();

        Assert.IsFalse(generations.TryInvalidate(oldGeneration));
        Assert.IsTrue(generations.IsCurrent(currentGeneration));
    }

    [TestMethod]
    public void Next_ConcurrentCalls_ReturnUniqueValues()
    {
        var generations = new ExecutionGeneration();
        var values = new long[1000];

        Parallel.For(0, values.Length, index => values[index] = generations.Next());

        Assert.AreEqual(values.Length, values.Distinct().Count());
    }
}
