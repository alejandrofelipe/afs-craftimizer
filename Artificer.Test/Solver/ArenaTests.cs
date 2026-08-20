using System.Runtime.Intrinsics;

namespace Artificer.Test.Solver;

[TestClass]
public class ArenaTests
{
    private static SimulationInput MakeInput() =>
        new(new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 575, Level = 90, CanUseManipulation = true },
            new RecipeInfo
            {
                ClassJobLevel = 90, MaxDurability = 80, MaxQuality = 7200, MaxProgress = 3500,
                QualityModifier = 80, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130,
            });

    private static SimulationNode MakeNode() =>
        new(new SimulationState(MakeInput()), null, CompletionState.Incomplete, new ActionSet());

    // ---- ArenaBuffer<T> ----

    [TestMethod]
    public void ArenaBuffer_InitialState_EmptyAndNullData()
    {
        var buf = new ArenaBuffer<SimulationNode>();
        Assert.AreEqual(0, buf.Count);
        Assert.IsNull(buf.Data);
    }

    [TestMethod]
    public void ArenaBuffer_Add_AllocatesDataAndSetsChildIdx()
    {
        var buf = new ArenaBuffer<SimulationNode>();
        var node = new ArenaNode<SimulationNode>(MakeNode());
        buf.Add(node);

        Assert.AreEqual(1, buf.Count);
        Assert.IsNotNull(buf.Data);
        Assert.AreEqual((0, 0), node.ChildIdx);
    }

    [TestMethod]
    public void ArenaBuffer_Add_FillsBatch_NoSecondBatch()
    {
        var buf = new ArenaBuffer<SimulationNode>();
        for (var i = 0; i < ArenaBuffer.BatchSize; i++)
        {
            var node = new ArenaNode<SimulationNode>(MakeNode());
            buf.Add(node);
            Assert.AreEqual((0, i), node.ChildIdx);
        }
        Assert.AreEqual(ArenaBuffer.BatchSize, buf.Count);
        Assert.IsNull(buf.Data![1]);
    }

    [TestMethod]
    public void ArenaBuffer_Add_CrossesBatch_AllocatesSecondArray()
    {
        var buf = new ArenaBuffer<SimulationNode>();
        ArenaNode<SimulationNode>? firstOfSecondBatch = null;
        for (var i = 0; i <= ArenaBuffer.BatchSize; i++)
        {
            var node = new ArenaNode<SimulationNode>(MakeNode());
            buf.Add(node);
            if (i == ArenaBuffer.BatchSize)
                firstOfSecondBatch = node;
        }

        Assert.AreEqual(ArenaBuffer.BatchSize + 1, buf.Count);
        Assert.IsNotNull(buf.Data![1]);
        Assert.AreEqual((1, 0), firstOfSecondBatch!.ChildIdx);
    }

    // ---- ArenaNode<T> ----

    [TestMethod]
    public void ArenaNode_NoParent_NullParentAndParentScores()
    {
        var node = new ArenaNode<SimulationNode>(MakeNode());
        Assert.IsNull(node.Parent);
        Assert.IsNull(node.ParentScores);
        Assert.AreEqual(0, node.Children.Count);
    }

    [TestMethod]
    public void ArenaNode_WithParent_ReferencesParentScores()
    {
        var parent = new ArenaNode<SimulationNode>(MakeNode());
        var child = new ArenaNode<SimulationNode>(MakeNode(), parent);

        Assert.AreEqual(parent, child.Parent);
        Assert.IsNotNull(child.ParentScores);
    }

    [TestMethod]
    public void ArenaNode_Add_CreatesChild_IncrementsBoth()
    {
        var parent = new ArenaNode<SimulationNode>(MakeNode());
        var child = parent.Add(MakeNode());

        Assert.AreEqual(parent, child.Parent);
        Assert.AreEqual(1, parent.Children.Count);
        Assert.AreEqual(1, parent.ChildScores.Count);
    }

    [TestMethod]
    public void ArenaNode_ChildAt_ReturnsAddedNode()
    {
        var parent = new ArenaNode<SimulationNode>(MakeNode());
        var child = parent.Add(MakeNode());

        Assert.AreEqual(child, parent.ChildAt(child.ChildIdx));
    }

    [TestMethod]
    public void ArenaNode_ChildAt_NullWhenNoChildren()
    {
        var parent = new ArenaNode<SimulationNode>(MakeNode());
        Assert.IsNull(parent.ChildAt((0, 0)));
    }

    [TestMethod]
    public void ArenaNode_ParentScores_MatchesParentChildScores()
    {
        var parent = new ArenaNode<SimulationNode>(MakeNode());
        parent.Add(MakeNode());
        var child = new ArenaNode<SimulationNode>(MakeNode(), parent);

        Assert.AreEqual(parent.ChildScores.Count, child.ParentScores!.Value.Count);
    }

    [DataTestMethod]
    [DataRow(32)]
    [DataRow(33)]
    [DataRow(40)]
    [DataRow(65)]
    public void ArenaNode_Add_GrowsPastInitialCapacity(int count)
    {
        var parent = new ArenaNode<SimulationNode>(MakeNode());
        var children = new List<ArenaNode<SimulationNode>>();

        for (var i = 0; i < count; i++)
            children.Add(parent.Add(MakeNode()));

        Assert.AreEqual(count, parent.Children.Count);
        Assert.AreEqual(count, parent.ChildScores.Count);
        for (var i = 0; i < count; i++)
        {
            Assert.AreEqual((i >> ArenaBuffer.BatchSizeBits, i & ArenaBuffer.BatchSizeMask), children[i].ChildIdx);
            Assert.AreSame(children[i], parent.ChildAt(children[i].ChildIdx));
        }
    }

    // ---- NodeScoresBuffer ----

    [TestMethod]
    public void NodeScoresBuffer_InitialState_ZeroAndNullData()
    {
        var buf = new NodeScoresBuffer();
        Assert.AreEqual(0, buf.Count);
        Assert.IsNull(buf.Data);
    }

    [TestMethod]
    public void NodeScoresBuffer_Add_AllocatesData()
    {
        var buf = new NodeScoresBuffer();
        buf.Add();
        Assert.AreEqual(1, buf.Count);
        Assert.IsNotNull(buf.Data);
    }

    [TestMethod]
    public void NodeScoresBuffer_Add_Multiple()
    {
        var buf = new NodeScoresBuffer();
        for (var i = 0; i < 3; i++) buf.Add();
        Assert.AreEqual(3, buf.Count);
    }

    [TestMethod]
    public void NodeScoresBuffer_Add_CrossesBatch()
    {
        var buf = new NodeScoresBuffer();
        for (var i = 0; i <= ArenaBuffer.BatchSize; i++) buf.Add();
        Assert.AreEqual(ArenaBuffer.BatchSize + 1, buf.Count);
    }

    [TestMethod]
    public void NodeScoresBuffer_Visit_IncrementsGetVisits()
    {
        var buf = new NodeScoresBuffer();
        buf.Add();
        buf.Visit((0, 0), 0.5f);
        Assert.AreEqual(1, buf.GetVisits((0, 0)));
    }

    [TestMethod]
    public void NodeScoresBuffer_Visit_MultipleVisits_Accumulates()
    {
        var buf = new NodeScoresBuffer();
        buf.Add();
        buf.Visit((0, 0), 0.5f);
        buf.Visit((0, 0), 0.8f);
        buf.Visit((0, 0), 0.3f);
        Assert.AreEqual(3, buf.GetVisits((0, 0)));
    }

    [TestMethod]
    public void NodeScoresBuffer_Visit_IndependentSlots()
    {
        var buf = new NodeScoresBuffer();
        buf.Add(); buf.Add();
        buf.Visit((0, 0), 0.5f);
        buf.Visit((0, 1), 0.8f);
        Assert.AreEqual(1, buf.GetVisits((0, 0)));
        Assert.AreEqual(1, buf.GetVisits((0, 1)));
    }

    [TestMethod]
    public void NodeScoresBuffer_Visit_CrossBatchSlot()
    {
        var buf = new NodeScoresBuffer();
        for (var i = 0; i <= ArenaBuffer.BatchSize; i++) buf.Add();
        buf.Visit((1, 0), 0.7f);
        Assert.AreEqual(1, buf.GetVisits((1, 0)));
    }

    [TestMethod]
    public void NodeScoresBuffer_Add40_VisitEachSlot_TracksPerSlotAcrossBatches()
    {
        var buf = new NodeScoresBuffer();
        for (var i = 0; i < 40; i++) buf.Add();
        Assert.AreEqual(40, buf.Count);

        for (var i = 0; i < 40; i++)
            buf.Visit((i >> ArenaBuffer.BatchSizeBits, i & ArenaBuffer.BatchSizeMask), i + 0.5f);

        for (var i = 0; i < 40; i++)
        {
            var at = (i >> ArenaBuffer.BatchSizeBits, i & ArenaBuffer.BatchSizeMask);
            Assert.AreEqual(1, buf.GetVisits(at));
            Assert.AreEqual(i + 0.5f, buf.Data![at.Item1].ScoreSum[at.Item2], 1e-3f);
            Assert.AreEqual(i + 0.5f, buf.Data![at.Item1].MaxScore[at.Item2], 1e-3f);
        }

        // Re-visitar o slot 32 (2º batch) acumula a soma e mantém o max.
        var at32 = (32 >> ArenaBuffer.BatchSizeBits, 32 & ArenaBuffer.BatchSizeMask);
        buf.Visit(at32, 100f);
        Assert.AreEqual(2, buf.GetVisits(at32));
        Assert.AreEqual(32.5f + 100f, buf.Data![at32.Item1].ScoreSum[at32.Item2], 1e-3f);
        Assert.AreEqual(100f, buf.Data![at32.Item1].MaxScore[at32.Item2], 1e-3f);
    }

    // ---- VectorUtils.At ----

    [TestMethod]
    public void VectorUtils_At_ModifiesArbitraryElement()
    {
        var vec = Vector256<float>.Zero;
        VectorUtils.At(ref vec, 3) = 1.5f;
        Assert.AreEqual(1.5f, vec[3]);
        Assert.AreEqual(0f, vec[0]);
    }

    [TestMethod]
    public void VectorUtils_At_FirstElement()
    {
        var vec = Vector256<float>.Zero;
        VectorUtils.At(ref vec, 0) = 42.0f;
        Assert.AreEqual(42.0f, vec[0]);
        Assert.AreEqual(0f, vec[1]);
    }

    [TestMethod]
    public void VectorUtils_At_LastElement()
    {
        var vec = Vector256<float>.Zero;
        var last = Vector256<float>.Count - 1;
        VectorUtils.At(ref vec, last) = 99.0f;
        Assert.AreEqual(99.0f, vec[last]);
    }
}
