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
    public void Begin_WhenPreviousCancellationCallbackThrows_RollsBackUndeliveredGeneration()
    {
        using var coordinator = new CancellationGeneration();
        var previous = coordinator.Begin();
        using var previousWaitHandle = previous.Token.WaitHandle;
        var callbackException = new InvalidOperationException("Expected callback failure.");
        var installedGeneration = previous.Generation + 1;
        var callbackSawInstalledGeneration = false;

        using var registration = previous.Token.Register(() =>
        {
            callbackSawInstalledGeneration = coordinator.IsCurrent(installedGeneration);
            throw callbackException;
        });

        var thrown = Assert.ThrowsException<AggregateException>(() => coordinator.Begin());

        Assert.AreEqual(1, thrown.InnerExceptions.Count);
        Assert.AreSame(callbackException, thrown.InnerExceptions[0]);
        Assert.IsTrue(callbackSawInstalledGeneration);
        Assert.IsTrue(previous.Token.IsCancellationRequested);
        Assert.IsTrue(previousWaitHandle.SafeWaitHandle.IsClosed);
        Assert.IsFalse(coordinator.IsCurrent(installedGeneration));
    }

    [TestMethod]
    public void Begin_WhenThrowingPreviousCallbackCreatesReentrantGeneration_DoesNotRollItBack()
    {
        using var coordinator = new CancellationGeneration();
        var previous = coordinator.Begin();
        var callbackException = new InvalidOperationException("Expected callback failure.");
        (long Generation, CancellationToken Token) reentrant = default;

        using var registration = previous.Token.Register(() =>
        {
            reentrant = coordinator.Begin();
            throw callbackException;
        });

        var outerBeginTask = Task.Run(() => Assert.ThrowsException<AggregateException>(() => coordinator.Begin()));

        Assert.IsTrue(outerBeginTask.Wait(GateTimeout), "Begin deadlocked while invoking a reentrant cancellation callback.");
        var thrown = outerBeginTask.GetAwaiter().GetResult();
        Assert.AreEqual(1, thrown.InnerExceptions.Count);
        Assert.AreSame(callbackException, thrown.InnerExceptions[0]);
        Assert.IsTrue(coordinator.IsCurrent(reentrant.Generation));
        Assert.IsFalse(reentrant.Token.IsCancellationRequested);
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
    public async Task Begin_StaleCompletionCannotReplaceOrMutateCurrentPublishedSnapshot()
    {
        using var coordinator = new CancellationGeneration();
        var published = new Dictionary<uint, int>();
        var staleStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStale = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Dictionary<uint, int>> BuildAndPublishAsync(
            (long Generation, CancellationToken Token) load,
            uint itemId,
            int price,
            TaskCompletionSource<bool>? started,
            Task release)
        {
            var loaded = new Dictionary<uint, int> { [itemId] = price };
            started?.SetResult(true);
            await release.WaitAsync(GateTimeout).ConfigureAwait(false);
            loaded.Add(itemId + 1, price + 1);

            if (coordinator.IsCurrent(load.Generation))
                published = loaded;

            return loaded;
        }

        var stale = coordinator.Begin();
        var staleTask = BuildAndPublishAsync(stale, 10, 100, staleStarted, releaseStale.Task);
        await staleStarted.Task.WaitAsync(GateTimeout).ConfigureAwait(false);

        var current = coordinator.Begin();
        var currentSnapshot = await BuildAndPublishAsync(
            current,
            20,
            200,
            started: null,
            Task.CompletedTask).ConfigureAwait(false);
        var publishedSnapshot = published;

        releaseStale.SetResult(true);
        var staleSnapshot = await staleTask.WaitAsync(GateTimeout).ConfigureAwait(false);
        staleSnapshot.Add(99, 999);

        Assert.IsTrue(stale.Token.IsCancellationRequested);
        Assert.AreSame(currentSnapshot, publishedSnapshot);
        Assert.AreSame(publishedSnapshot, published);
        Assert.AreEqual(2, published.Count);
        Assert.AreEqual(200, published[20]);
        Assert.AreEqual(201, published[21]);
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
