using AVLStudentManagement.Core.DataStructures;

namespace AVLStudentManagement.Tests;

[TestClass]
public class AvlTreeTests
{
    // ---------- Hàm hỗ trợ ----------

    private static void CheckNode<TK, TV>(AvlNode<TK, TV>? node)
    {
        if (node == null) return;
        int hl = RealHeightGeneric(node.Left), hr = RealHeightGeneric(node.Right);
        Assert.AreEqual(1 + Math.Max(hl, hr), node.Height, "Height lưu trong nút sai");
        Assert.IsTrue(Math.Abs(hl - hr) <= 1, "Nút bị lệch quá 1");
        Assert.AreEqual(hl - hr, node.BalanceFactor, "BalanceFactor sai");
        CheckNode(node.Left);
        CheckNode(node.Right);
    }

    private static int RealHeightGeneric<TK, TV>(AvlNode<TK, TV>? node) =>
        node == null ? 0 : 1 + Math.Max(RealHeightGeneric(node.Left), RealHeightGeneric(node.Right));

    /// <summary>Kiểm tra mọi tính chất của cây AVL. Gọi sau mỗi thao tác.</summary>
    private static void AssertInvariants<TK, TV>(AvlTree<TK, TV> tree, IComparer<TK>? comparer = null)
    {
        comparer ??= Comparer<TK>.Default;
        CheckNode(tree.Root);
        Assert.AreEqual(RealHeightGeneric(tree.Root), tree.Height);

        var keys = tree.InOrder().Select(p => p.Key).ToList();
        Assert.AreEqual(tree.Count, keys.Count, "Count không khớp số nút");
        for (int i = 1; i < keys.Count; i++)
            Assert.IsTrue(comparer.Compare(keys[i - 1], keys[i]) < 0, "InOrder không tăng dần");
    }

    private static AvlTree<int, string> Build(params int[] keys)
    {
        var tree = new AvlTree<int, string>();
        foreach (var k in keys)
        {
            tree.Insert(k, "v" + k);
            AssertInvariants(tree);
        }
        return tree;
    }

    private static int[] Keys(IEnumerable<KeyValuePair<int, string>> items) => items.Select(p => p.Key).ToArray();

    // ---------- 4 ca xoay ----------

    private static void AssertRotation(int[] order, int expectedRotations)
    {
        var tree = Build(order);
        Assert.AreEqual(2, tree.Root!.Key);
        Assert.AreEqual(1, tree.Root.Left!.Key);
        Assert.AreEqual(3, tree.Root.Right!.Key);
        Assert.AreEqual(expectedRotations, tree.RotationCount);
    }

    [TestMethod] public void Rotation_LL() => AssertRotation([3, 2, 1], 1);
    [TestMethod] public void Rotation_RR() => AssertRotation([1, 2, 3], 1);
    [TestMethod] public void Rotation_LR() => AssertRotation([3, 1, 2], 2);
    [TestMethod] public void Rotation_RL() => AssertRotation([1, 3, 2], 2);

    // ---------- Xóa ----------

    [TestMethod]
    public void Delete_Leaf()
    {
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        Assert.IsTrue(tree.Delete(1));
        AssertInvariants(tree);
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6, 7 }, Keys(tree.InOrder()));
    }

    [TestMethod]
    public void Delete_NodeWithOneChild()
    {
        // 2 chỉ có con phải là 3 (sau khi xóa 1)
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        tree.Delete(1);
        Assert.IsTrue(tree.Delete(2));
        AssertInvariants(tree);
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7 }, Keys(tree.InOrder()));
    }

    [TestMethod]
    public void Delete_NodeWithTwoChildren_KeepsValue()
    {
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        Assert.IsTrue(tree.Delete(6)); // 6 có 2 con 5 và 7, nút 7 lên thay
        AssertInvariants(tree);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 7 }, Keys(tree.InOrder()));
        Assert.IsTrue(tree.TryGet(7, out var v));
        Assert.AreEqual("v7", v); // Value phải đi theo Key
    }

    [TestMethod]
    public void Delete_Root()
    {
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        Assert.IsTrue(tree.Delete(4));
        AssertInvariants(tree);
        Assert.AreEqual(5, tree.Root!.Key); // nút nhỏ nhất bên phải
        Assert.AreEqual("v5", tree.Root.Value);
        Assert.AreEqual(6, tree.Count);
    }

    [TestMethod]
    public void Delete_UntilEmpty()
    {
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        foreach (var k in new[] { 4, 1, 7, 2, 6, 3, 5 })
        {
            Assert.IsTrue(tree.Delete(k));
            AssertInvariants(tree);
        }
        Assert.AreEqual(0, tree.Count);
        Assert.IsNull(tree.Root);
        Assert.AreEqual(0, tree.Height);
    }

    [TestMethod]
    public void Delete_SingleNodeTree()
    {
        var tree = Build(5);
        Assert.IsTrue(tree.Delete(5));
        Assert.IsNull(tree.Root);
        Assert.AreEqual(0, tree.Count);
    }

    [TestMethod]
    public void Delete_MissingKey_ReturnsFalse()
    {
        var tree = Build(1, 2, 3);
        Assert.IsFalse(tree.Delete(99));
        Assert.AreEqual(3, tree.Count);
        Assert.IsFalse(new AvlTree<int, string>().Delete(1)); // cây rỗng
        AssertInvariants(tree);
    }

    [TestMethod]
    public void Delete_CausesRotation()
    {
        // Xóa 1 làm nhánh trái thấp đi, nút 2 bị lệch phải => phải xoay
        var tree = Build(2, 1, 3, 4);
        int before = tree.RotationCount;
        tree.Delete(1);
        AssertInvariants(tree);
        Assert.IsTrue(tree.RotationCount > before);
        Assert.AreEqual(3, tree.Root!.Key);
    }

    // ---------- Khóa trùng, TryGet, TryUpdate, Min, Max ----------

    [TestMethod]
    public void Insert_Duplicate_ReturnsFalseAndKeepsOldValue()
    {
        var tree = Build(1, 2, 3);
        Assert.IsFalse(tree.Insert(2, "moi"));
        Assert.AreEqual(3, tree.Count);
        tree.TryGet(2, out var v);
        Assert.AreEqual("v2", v);
        AssertInvariants(tree);
    }

    [TestMethod]
    public void TryGet_FoundAndNotFound()
    {
        var tree = Build(1, 2, 3);
        Assert.IsTrue(tree.TryGet(3, out var v));
        Assert.AreEqual("v3", v);
        Assert.IsFalse(tree.TryGet(10, out _));
    }

    [TestMethod]
    public void TryUpdate_ExistingKey_ChangesValueOnly()
    {
        var tree = Build(1, 2, 3);
        Assert.IsTrue(tree.TryUpdate(1, "moi"));
        tree.TryGet(1, out var v);
        Assert.AreEqual("moi", v);
        Assert.AreEqual(3, tree.Count);
        Assert.AreEqual(2, tree.Root!.Key);
    }

    [TestMethod]
    public void TryUpdate_MissingKey_ReturnsFalse()
    {
        var tree = Build(1, 2, 3);
        Assert.IsFalse(tree.TryUpdate(9, "x"));
        Assert.AreEqual(3, tree.Count);
    }

    [TestMethod]
    public void MinMax_Works()
    {
        var tree = Build(5, 3, 8, 1, 9);
        Assert.AreEqual(1, tree.Min().Key);
        Assert.AreEqual(9, tree.Max().Key);
    }

    [TestMethod]
    public void MinMax_EmptyTree_Throws()
    {
        var tree = new AvlTree<int, string>();
        Assert.ThrowsExactly<InvalidOperationException>(() => tree.Min());
        Assert.ThrowsExactly<InvalidOperationException>(() => tree.Max());
    }

    // ---------- Range ----------

    [TestMethod]
    public void Range_MatchesSortedDictionary()
    {
        var rnd = new Random(1);
        var tree = new AvlTree<int, string>();
        var dict = new SortedDictionary<int, string>();
        for (int i = 0; i < 300; i++)
        {
            int k = rnd.Next(0, 500);
            if (tree.Insert(k, "v" + k)) dict[k] = "v" + k;
        }

        for (int i = 0; i < 200; i++)
        {
            int a = rnd.Next(-20, 520), b = rnd.Next(-20, 520);
            var expected = dict.Where(p => p.Key >= a && p.Key <= b).Select(p => p.Key).ToArray();
            CollectionAssert.AreEqual(expected, Keys(tree.Range(a, b)), $"Range({a},{b})");
        }
    }

    [TestMethod]
    public void Range_FromGreaterThanTo_IsEmpty()
    {
        var tree = Build(1, 2, 3, 4, 5);
        Assert.AreEqual(0, tree.Range(4, 2).Count());
    }

    [TestMethod]
    public void Range_OutsideData()
    {
        var tree = Build(10, 20, 30);
        Assert.AreEqual(0, tree.Range(1, 5).Count());     // toàn bộ nhỏ hơn dữ liệu
        Assert.AreEqual(0, tree.Range(40, 50).Count());   // toàn bộ lớn hơn dữ liệu
        Assert.AreEqual(3, tree.Range(-100, 100).Count()); // bao trùm hết
        Assert.AreEqual(0, new AvlTree<int, string>().Range(1, 2).Count()); // cây rỗng
    }

    // ---------- Thứ tự duyệt ----------
    // Cây cố định (vẽ tay), chèn 4,2,6,1,3,5,7:
    //          4
    //        /   \
    //       2     6
    //      / \   / \
    //     1   3 5   7

    [TestMethod]
    public void PreOrder_Correct()
    {
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        CollectionAssert.AreEqual(new[] { 4, 2, 1, 3, 6, 5, 7 }, Keys(tree.PreOrder()));
    }

    [TestMethod]
    public void PostOrder_Correct()
    {
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        CollectionAssert.AreEqual(new[] { 1, 3, 2, 5, 7, 6, 4 }, Keys(tree.PostOrder()));
    }

    [TestMethod]
    public void InOrderDescending_Correct()
    {
        var tree = Build(4, 2, 6, 1, 3, 5, 7);
        CollectionAssert.AreEqual(new[] { 7, 6, 5, 4, 3, 2, 1 }, Keys(tree.InOrderDescending()));
    }

    [TestMethod]
    public void Traversals_EmptyTree()
    {
        var tree = new AvlTree<int, string>();
        Assert.AreEqual(0, tree.PreOrder().Count());
        Assert.AreEqual(0, tree.PostOrder().Count());
        Assert.AreEqual(0, tree.InOrder().Count());
        Assert.AreEqual(0, tree.InOrderDescending().Count());
    }

    // ---------- Fuzz và hiệu năng ----------

    [TestMethod]
    public void Fuzz_MatchesSortedDictionary()
    {
        var rnd = new Random(12345);
        var tree = new AvlTree<int, string>();
        var dict = new SortedDictionary<int, string>();

        for (int i = 0; i < 10_000; i++)
        {
            int k = rnd.Next(0, 400);
            if (rnd.Next(2) == 0)
            {
                bool added = !dict.ContainsKey(k);
                if (added) dict[k] = "v" + k;
                Assert.AreEqual(added, tree.Insert(k, "v" + k));
            }
            else
            {
                Assert.AreEqual(dict.Remove(k), tree.Delete(k));
            }

            AssertInvariants(tree);
            Assert.AreEqual(dict.Count, tree.Count);
            CollectionAssert.AreEqual(dict.Keys.ToArray(), Keys(tree.InOrder()));
        }
    }

    [TestMethod]
    public void Insert100kAscending_HeightIsLogarithmic()
    {
        var tree = new AvlTree<int, int>();
        const int n = 100_000;
        for (int i = 0; i < n; i++) tree.Insert(i, i);

        Assert.AreEqual(n, tree.Count);
        Assert.IsTrue(tree.Height <= 1.44 * Math.Log2(n + 2), $"Height = {tree.Height}");
        CheckNode(tree.Root);
    }

    // ---------- Comparer tùy chỉnh ----------

    private sealed class ScoreThenNameComparer : IComparer<(double Score, string Name)>
    {
        // Điểm giảm dần, cùng điểm thì tên tăng dần
        public int Compare((double Score, string Name) x, (double Score, string Name) y)
        {
            int c = y.Score.CompareTo(x.Score);
            return c != 0 ? c : string.CompareOrdinal(x.Name, y.Name);
        }
    }

    [TestMethod]
    public void CustomComparer_TupleKey()
    {
        var cmp = new ScoreThenNameComparer();
        var tree = new AvlTree<(double, string), int>(cmp);
        var items = new (double, string)[]
        {
            (8.0, "An"), (9.5, "Binh"), (8.0, "Chi"), (7.0, "Dung"), (9.5, "Anh"), (6.5, "Em")
        };
        foreach (var it in items)
        {
            Assert.IsTrue(tree.Insert(it, 1));
            AssertInvariants(tree, cmp);
        }

        Assert.IsFalse(tree.Insert((8.0, "An"), 2)); // trùng cả điểm lẫn tên

        var expected = new (double, string)[]
        {
            (9.5, "Anh"), (9.5, "Binh"), (8.0, "An"), (8.0, "Chi"), (7.0, "Dung"), (6.5, "Em")
        };
        CollectionAssert.AreEqual(expected, tree.InOrder().Select(p => p.Key).ToArray());

        Assert.IsTrue(tree.Delete((8.0, "An")));
        AssertInvariants(tree, cmp);
        Assert.AreEqual((9.5, "Anh"), tree.Min().Key);
    }
}
