using Artificer.Utils;

namespace Artificer.Test.Utils;

[TestClass]
public class CancellationGenerationTests
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public void Begin_CancelsPreviousTokenAndMakesNewGenerationCurrent()
    {
        using var coordinator = new CancellationGeneration();
        var first = coordinator.Begin();

        var second = coordinator.Begin();

        Assert.IsTrue(first.Token.IsCancellationRequested);
        Assert.IsFalse(coordinator.IsCurrent(first.Generation));
        Assert.IsTrue(coordinator.IsCurrent(second.Generation));
        Assert.IsFalse(second.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void IsCurrent_WithoutActiveGeneration_ReturnsFalse()
    {
        using var coordinator = new CancellationGeneration();

        Assert.IsFalse(coordinator.IsCurrent(0));
    }

    [TestMethod]
    public void Cancel_CurrentGeneration_CancelsTokenAndInvalidatesGeneration()
    {
        using var coordinator = new CancellationGeneration();
        var current = coordinator.Begin();

        coordinator.Cancel(current.Generation);

        Assert.IsTrue(current.Token.IsCancellationRequested);
        Assert.IsFalse(coordinator.IsCurrent(current.Generation));
    }

    [TestMethod]
    public void Cancel_PreviousGeneration_DoesNotCancelCurrentGeneration()
    {
        using var coordinator = new CancellationGeneration();
        var previous = coordinator.Begin();
        var current = coordinator.Begin();

        coordinator.Cancel(previous.Generation);

        Assert.IsTrue(coordinator.IsCurrent(current.Generation));
        Assert.IsFalse(current.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_CancelsActiveTokenAndInvalidatesGeneration()
    {
        var coordinator = new CancellationGeneration();
        var current = coordinator.Begin();

        coordinator.Dispose();

        Assert.IsTrue(current.Token.IsCancellationRequested);
        Assert.IsFalse(coordinator.IsCurrent(current.Generation));
    }

    [TestMethod]
    public void Dispose_MultipleCallsAndLaterCancel_AreSafe()
    {
        var coordinator = new CancellationGeneration();
        var current = coordinator.Begin();

        coordinator.Dispose();

        coordinator.Dispose();
        coordinator.Cancel(current.Generation);
    }

    [TestMethod]
    public void Begin_AfterDispose_ThrowsAndNoGenerationIsCurrent()
    {
        var coordinator = new CancellationGeneration();
        var previous = coordinator.Begin();
        coordinator.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => coordinator.Begin());
        Assert.IsFalse(coordinator.IsCurrent(previous.Generation));
    }

    [TestMethod]
    public void Begin_CancellationCallbackReentersCoordinatorWithoutDeadlockOrCancelingReentrantGeneration()
    {
        using var coordinator = new CancellationGeneration();
        var previous = coordinator.Begin();
        using var reentrantCompleted = new ManualResetEventSlim();
        var previousWasCurrentDuringCancellation = true;
        (long Generation, CancellationToken Token) reentrant = default;
        Task? reentrantTask = null;

        using var registration = previous.Token.Register(() =>
        {
            reentrantTask = Task.Run(() =>
            {
                previousWasCurrentDuringCancellation = coordinator.IsCurrent(previous.Generation);
                reentrant = coordinator.Begin();
                reentrantCompleted.Set();
            });

            Assert.IsTrue(reentrantCompleted.Wait(GateTimeout), "Cancellation ran while holding the coordinator lock.");
            reentrantTask.GetAwaiter().GetResult();
        });

        var outerBeginTask = Task.Run(coordinator.Begin);

        Assert.IsTrue(outerBeginTask.Wait(GateTimeout), "Begin deadlocked while invoking a cancellation callback.");
        var superseded = outerBeginTask.GetAwaiter().GetResult();
        Assert.IsFalse(previousWasCurrentDuringCancellation);
        Assert.IsTrue(superseded.Token.IsCancellationRequested);
        Assert.IsFalse(coordinator.IsCurrent(superseded.Generation));
        Assert.IsTrue(coordinator.IsCurrent(reentrant.Generation));
        Assert.IsFalse(reentrant.Token.IsCancellationRequested);
    }
}
