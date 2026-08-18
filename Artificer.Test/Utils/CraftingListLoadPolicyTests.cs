using Artificer.Utils;

namespace Artificer.Test.Utils;

[TestClass]
public sealed class CraftingListLoadPolicyTests
{
    [TestMethod]
    public void TryBeginManualPriceLoad_WhileTreeIsLoading_PreservesActiveGeneration()
    {
        var policy = new CraftingListLoadPolicy();
        var active = policy.TryBeginPriceLoad();

        var blocked = policy.TryBeginManualPriceLoad(
            treeLoading: true,
            showMarketPrices: true,
            hasList: true,
            hasTree: true,
            hasPlayer: true);

        Assert.IsTrue(active.Started);
        Assert.IsFalse(blocked.Started);
        Assert.IsFalse(blocked.ShouldClearState);
        Assert.IsTrue(policy.IsPriceLoadCurrent(active.Generation));
        Assert.IsFalse(active.Token.IsCancellationRequested);

        policy.DisposePriceLoad();
    }

    [DataTestMethod]
    [DataRow(false, true, true, true)]
    [DataRow(true, false, true, true)]
    [DataRow(true, true, false, true)]
    [DataRow(true, true, true, false)]
    public void TryBeginManualPriceLoad_WithMissingPrerequisite_PreservesActiveGeneration(
        bool showMarketPrices,
        bool hasList,
        bool hasTree,
        bool hasPlayer)
    {
        var policy = new CraftingListLoadPolicy();
        var active = policy.TryBeginPriceLoad();

        var blocked = policy.TryBeginManualPriceLoad(
            treeLoading: false,
            showMarketPrices,
            hasList,
            hasTree,
            hasPlayer);

        Assert.IsFalse(blocked.Started);
        Assert.IsTrue(policy.IsPriceLoadCurrent(active.Generation));
        Assert.IsFalse(active.Token.IsCancellationRequested);

        policy.DisposePriceLoad();
    }

    [TestMethod]
    public void CanCompleteInventorySync_AfterCloseAndReopen_RejectsStaleLifecycle()
    {
        var policy = new CraftingListLoadPolicy();
        var listId = Guid.NewGuid();
        var startedLifecycle = policy.CaptureLifecycleEpoch();

        policy.InvalidateLifecycle(); // Close.
        policy.InvalidateLifecycle(); // Reopen the same list.

        Assert.IsFalse(policy.CanCompleteInventorySync(
            startedLifecycle,
            listId,
            currentListId: listId,
            isOpen: true,
            listExists: true));

        policy.DisposePriceLoad();
    }

    [TestMethod]
    public void CanCompleteInventorySync_AfterAutoDelete_RejectsDeletedList()
    {
        var policy = new CraftingListLoadPolicy();
        var listId = Guid.NewGuid();
        var startedLifecycle = policy.CaptureLifecycleEpoch();

        policy.InvalidateLifecycle();

        Assert.IsFalse(policy.CanCompleteInventorySync(
            startedLifecycle,
            listId,
            currentListId: null,
            isOpen: false,
            listExists: false));

        policy.DisposePriceLoad();
    }

    [DataTestMethod]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    public void CanCompleteInventorySync_WithInvalidCurrentContext_ReturnsFalse(
        bool isOpen,
        bool sameList,
        bool listExists)
    {
        var policy = new CraftingListLoadPolicy();
        var listId = Guid.NewGuid();
        var currentListId = sameList ? listId : Guid.NewGuid();
        var startedLifecycle = policy.CaptureLifecycleEpoch();

        Assert.IsFalse(policy.CanCompleteInventorySync(
            startedLifecycle,
            listId,
            currentListId,
            isOpen,
            listExists));

        policy.DisposePriceLoad();
    }

    [TestMethod]
    public void TryBeginPriceLoad_WhenCancellationCallbackThrows_ObservesFailureAndRequestsStateClear()
    {
        var policy = new CraftingListLoadPolicy();
        var active = policy.TryBeginPriceLoad();
        var callbackException = new InvalidOperationException("Expected callback failure.");
        using var registration = active.Token.Register(() => throw callbackException);

        var failed = policy.TryBeginPriceLoad();

        Assert.IsFalse(failed.Started);
        Assert.IsTrue(failed.ShouldClearState);
        Assert.IsInstanceOfType<AggregateException>(failed.Exception);
        var aggregate = (AggregateException)failed.Exception;
        Assert.AreSame(callbackException, aggregate.InnerExceptions.Single());
        Assert.IsTrue(active.Token.IsCancellationRequested);
        Assert.IsFalse(policy.IsPriceLoadCurrent(active.Generation));

        policy.DisposePriceLoad();
    }

    [TestMethod]
    public void TryBeginPriceLoad_WhenThrowingCallbackStartsReentrantOwner_PreservesNewerState()
    {
        var policy = new CraftingListLoadPolicy();
        var active = policy.TryBeginPriceLoad();
        var callbackException = new InvalidOperationException("Expected callback failure.");
        PriceLoadStartResult reentrant = default;
        var publishedState = "active";
        using var registration = active.Token.Register(() =>
        {
            reentrant = policy.TryBeginPriceLoad();
            publishedState = "reentrant";
            throw callbackException;
        });

        var failed = policy.TryBeginPriceLoad();
        if (failed.ShouldClearState)
            publishedState = "cleared";

        Assert.IsFalse(failed.Started);
        Assert.IsFalse(failed.ShouldClearState);
        Assert.IsNotNull(failed.Exception);
        Assert.AreEqual("reentrant", publishedState);
        Assert.IsTrue(reentrant.Started);
        Assert.IsTrue(policy.IsPriceLoadCurrent(reentrant.Generation));
        Assert.IsFalse(reentrant.Token.IsCancellationRequested);

        policy.DisposePriceLoad();
    }

    [TestMethod]
    public void CancelPriceLoad_WhenCallbackThrows_ObservesFailureAndStillRequestsStateClear()
    {
        var policy = new CraftingListLoadPolicy();
        var active = policy.TryBeginPriceLoad();
        var callbackException = new InvalidOperationException("Expected callback failure.");
        using var registration = active.Token.Register(() => throw callbackException);

        var stopped = policy.CancelPriceLoad();

        Assert.IsTrue(stopped.ShouldClearState);
        Assert.IsInstanceOfType<AggregateException>(stopped.Exception);
        var aggregate = (AggregateException)stopped.Exception;
        Assert.AreSame(callbackException, aggregate.InnerExceptions.Single());
        Assert.IsTrue(active.Token.IsCancellationRequested);
        Assert.IsFalse(policy.IsPriceLoadCurrent(active.Generation));

        policy.DisposePriceLoad();
    }

    [TestMethod]
    public void CancelPriceLoad_WhenThrowingCallbackStartsReentrantOwner_PreservesNewerState()
    {
        var policy = new CraftingListLoadPolicy();
        var active = policy.TryBeginPriceLoad();
        PriceLoadStartResult reentrant = default;
        var callbackException = new InvalidOperationException("Expected callback failure.");
        using var registration = active.Token.Register(() =>
        {
            reentrant = policy.TryBeginPriceLoad();
            throw callbackException;
        });

        var stopped = policy.CancelPriceLoad();

        Assert.IsFalse(stopped.ShouldClearState);
        Assert.IsNotNull(stopped.Exception);
        Assert.IsTrue(reentrant.Started);
        Assert.IsTrue(policy.IsPriceLoadCurrent(reentrant.Generation));
        Assert.IsFalse(reentrant.Token.IsCancellationRequested);

        policy.DisposePriceLoad();
    }

    [TestMethod]
    public void DisposePriceLoad_WhenCallbackThrows_ObservesFailureAndInvalidatesLifecycle()
    {
        var policy = new CraftingListLoadPolicy();
        var active = policy.TryBeginPriceLoad();
        var callbackException = new InvalidOperationException("Expected callback failure.");
        using var registration = active.Token.Register(() => throw callbackException);

        var stopped = policy.DisposePriceLoad();
        var currentLifecycle = policy.CaptureLifecycleEpoch();

        Assert.IsTrue(stopped.ShouldClearState);
        Assert.IsInstanceOfType<AggregateException>(stopped.Exception);
        var aggregate = (AggregateException)stopped.Exception;
        Assert.AreSame(callbackException, aggregate.InnerExceptions.Single());
        Assert.IsFalse(policy.CanCompleteInventorySync(
            currentLifecycle,
            Guid.Empty,
            currentListId: Guid.Empty,
            isOpen: true,
            listExists: true));
    }
}
