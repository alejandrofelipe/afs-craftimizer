using Artificer.Utils;

namespace Artificer.Test.Utils;

[TestClass]
public class GenerationSlotTests
{
    [TestMethod]
    public void Begin_ClearsPublishedValueAndAdvancesGeneration()
    {
        var slot = new GenerationSlot<string>();
        var firstGeneration = slot.Begin();
        slot.TryPublish(firstGeneration, "first");

        var secondGeneration = slot.Begin();

        Assert.AreNotEqual(firstGeneration, secondGeneration);
        Assert.IsNull(slot.Value);
    }

    [TestMethod]
    public void TryPublish_CurrentGeneration_PublishesValue()
    {
        var slot = new GenerationSlot<string>();
        var generation = slot.Begin();

        var published = slot.TryPublish(generation, "current");

        Assert.IsTrue(published);
        Assert.AreEqual("current", slot.Value);
    }

    [TestMethod]
    public void Invalidate_ClearsPublishedValueAndInvalidatesGeneration()
    {
        var slot = new GenerationSlot<string>();
        var generation = slot.Begin();
        slot.TryPublish(generation, "current");

        slot.Invalidate();

        Assert.IsNull(slot.Value);
        Assert.IsFalse(slot.TryPublish(generation, "stale"));
    }

    [TestMethod]
    public async Task TryPublish_PreviousGenerationAfterNewBegin_DoesNotOverwriteCurrentValue()
    {
        var slot = new GenerationSlot<string>();
        var staleGeneration = slot.Begin();
        var allowStalePublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var stalePublication = Task.Run(async () =>
        {
            await allowStalePublication.Task.ConfigureAwait(false);
            return slot.TryPublish(staleGeneration, "stale");
        });

        var currentGeneration = slot.Begin();
        Assert.IsTrue(slot.TryPublish(currentGeneration, "current"));
        allowStalePublication.SetResult();

        Assert.IsFalse(await stalePublication.ConfigureAwait(false));
        Assert.AreEqual("current", slot.Value);
    }
}
