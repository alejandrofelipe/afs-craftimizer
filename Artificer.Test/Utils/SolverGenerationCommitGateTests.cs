using Artificer.Utils;

namespace Artificer.Test.Utils;

[TestClass]
public class SolverGenerationCommitGateTests
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public void Begin_ResetsStateAndRejectsPreviousGenerationCommits()
    {
        var gate = new SolverGenerationCommitGate();
        var state = 0;
        var firstGeneration = gate.Begin(
            reset: () => state = 0,
            beginGeneration: () => 1);
        Assert.IsTrue(gate.TryCommit(firstGeneration, () =>
        {
            state = 1;
            return true;
        }));

        var secondGeneration = gate.Begin(
            reset: () => state = 0,
            beginGeneration: () => 2);

        Assert.AreEqual(0, state);
        Assert.IsFalse(gate.TryCommit(firstGeneration, () => true));
        Assert.IsTrue(gate.TryCommit(secondGeneration, () => true));
    }

    [TestMethod]
    public void Invalidate_ClosesGenerationBeforeRunningCancellationSideEffect()
    {
        var gate = new SolverGenerationCommitGate();
        var generation = gate.Begin(
            reset: () => { },
            beginGeneration: () => 1);
        var staleCommitDuringCancellation = true;

        gate.Invalidate(() =>
        {
            staleCommitDuringCancellation = gate.TryCommit(generation, () => true);
        });

        Assert.IsFalse(staleCommitDuringCancellation);
    }

    [TestMethod]
    public void Begin_WaitsForAuthorizedCommitBeforeResettingState()
    {
        using var commitAuthorized = new ManualResetEventSlim();
        using var beginReachedGate = new ManualResetEventSlim();
        using var resetEntered = new ManualResetEventSlim();
        using var releaseCommit = new ManualResetEventSlim();
        var gate = new SolverGenerationCommitGate();
        var state = 0;
        var generation = gate.Begin(
            reset: () => { },
            beginGeneration: () => 1);
        Task? commitTask = null;
        Task? beginTask = null;

        try
        {
            commitTask = Task.Run(() => gate.TryCommit(generation, () =>
            {
                commitAuthorized.Set();
                Assert.IsTrue(releaseCommit.Wait(GateTimeout));
                Volatile.Write(ref state, 1);
            }));
            Assert.IsTrue(commitAuthorized.Wait(GateTimeout));

            beginTask = Task.Run(() => gate.Begin(
                reset: () =>
                {
                    Volatile.Write(ref state, 0);
                    resetEntered.Set();
                },
                beginGeneration: () =>
                {
                    beginReachedGate.Set();
                    return 2;
                }));
            Assert.IsTrue(beginReachedGate.Wait(GateTimeout));
            Assert.IsFalse(resetEntered.Wait(TimeSpan.FromMilliseconds(500)));

            releaseCommit.Set();
            commitTask.GetAwaiter().GetResult();
            beginTask.GetAwaiter().GetResult();

            Assert.AreEqual(0, Volatile.Read(ref state));
        }
        finally
        {
            releaseCommit.Set();
            WaitForCleanup(commitTask);
            WaitForCleanup(beginTask);
        }
    }

    private static void WaitForCleanup(Task? task)
    {
        if (task is null || task.IsCompleted)
            return;

        try { _ = task.Wait(GateTimeout); }
        catch (AggregateException) { }
    }
}
