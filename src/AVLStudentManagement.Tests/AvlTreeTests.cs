using AVLStudentManagement.Core.DataStructures;
using AVLStudentManagement.Core.Models;

namespace AVLStudentManagement.Tests;

[TestClass]
public class AvlTreeTests
{
    // ---------- Hàm hỗ trợ ----------

    // Sinh viên chỉ có mã (số n, đệm 8 chữ số) và điểm
    private static Student Make(int n, double gpa = 3.0)
    {
        return new Student { StudentId = n.ToString("D8"), Gpa = gpa };
    }

    private static int RealHeight(AvlNode? node)
    {
        if (node == null)
        {
            return 0;
        }
        return 1 + Math.Max(RealHeight(node.Left), RealHeight(node.Right));
    }

    private static void CheckNode(AvlNode? node)
    {
        if (node == null)
        {
            return;
        }

        int leftHeight = RealHeight(node.Left);
        int rightHeight = RealHeight(node.Right);
        Assert.AreEqual(1 + Math.Max(leftHeight, rightHeight), node.Height, "Height lưu trong nút sai");
        Assert.IsTrue(Math.Abs(leftHeight - rightHeight) <= 1, "Nút bị lệch quá 1");
        Assert.AreEqual(leftHeight - rightHeight, node.BalanceFactor, "BalanceFactor sai");
        CheckNode(node.Left);
        CheckNode(node.Right);
    }

    // Kiểm tra mọi tính chất của cây AVL. Gọi sau mỗi thao tác.
    private static void AssertInvariants(AvlTree tree)
    {
        CheckNode(tree.Root);
        Assert.AreEqual(RealHeight(tree.Root), tree.Height);

        List<Student> students = tree.InOrder();
        Assert.AreEqual(tree.Count, students.Count, "Count không khớp số nút");
        for (int i = 1; i < students.Count; i++)
        {
            bool increasing = string.CompareOrdinal(students[i - 1].StudentId, students[i].StudentId) < 0;
            Assert.IsTrue(increasing, "InOrder không tăng dần");
        }
    }

    private static AvlTree Build(params int[] ids)
    {
        AvlTree tree = new AvlTree();
        foreach (int id in ids)
        {
            tree.Insert(Make(id));
            AssertInvariants(tree);
        }
        return tree;
    }

    // Danh sách sinh viên -> mảng số mã (dễ so sánh)
    private static int[] Ids(List<Student> students)
    {
        return students.Select(student => int.Parse(student.StudentId)).ToArray();
    }

    // ---------- 4 ca xoay ----------

    private static void AssertRotation(int[] order, int expectedRotations)
    {
        AvlTree tree = Build(order);
        Assert.AreEqual(Make(2).StudentId, tree.Root!.Data.StudentId);
        Assert.AreEqual(Make(1).StudentId, tree.Root.Left!.Data.StudentId);
        Assert.AreEqual(Make(3).StudentId, tree.Root.Right!.Data.StudentId);
        Assert.AreEqual(expectedRotations, tree.RotationCount);
    }

    [TestMethod] public void Rotation_LL() => AssertRotation([3, 2, 1], 1);
    [TestMethod] public void Rotation_RR() => AssertRotation([1, 2, 3], 1);
    [TestMethod] public void Rotation_LR() => AssertRotation([3, 1, 2], 2);
    [TestMethod] public void Rotation_RL() => AssertRotation([1, 3, 2], 2);

    [TestMethod]
    public void Ids_AreOrderedByNumericValue()
    {
        AvlTree tree = new AvlTree();
        foreach (string id in new[] { "10", "2", "9", "100", "007", "7" })
        {
            Assert.IsTrue(tree.Insert(new Student { StudentId = id }), id);
        }

        // 2 < 007 < 7 (cùng giá trị 7 thì so chuỗi) < 9 < 10 < 100
        string[] ids = tree.InOrder().Select(s => s.StudentId).ToArray();
        CollectionAssert.AreEqual(new[] { "2", "007", "7", "9", "10", "100" }, ids);
    }

    // ---------- Xóa ----------

    [TestMethod]
    public void Delete_Leaf()
    {
        AvlTree tree = Build(4, 2, 6, 1, 3, 5, 7);
        Assert.IsTrue(tree.Delete(Make(1)));
        AssertInvariants(tree);
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6, 7 }, Ids(tree.InOrder()));
    }

    [TestMethod]
    public void Delete_NodeWithOneChild()
    {
        // 2 chỉ có con phải là 3 (sau khi xóa 1)
        AvlTree tree = Build(4, 2, 6, 1, 3, 5, 7);
        tree.Delete(Make(1));
        Assert.IsTrue(tree.Delete(Make(2)));
        AssertInvariants(tree);
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7 }, Ids(tree.InOrder()));
    }

    [TestMethod]
    public void Delete_NodeWithTwoChildren()
    {
        AvlTree tree = Build(4, 2, 6, 1, 3, 5, 7);
        Assert.IsTrue(tree.Delete(Make(6))); // 6 có 2 con 5 và 7, nút 7 lên thay
        AssertInvariants(tree);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 7 }, Ids(tree.InOrder()));
        Assert.IsNotNull(tree.Find(Make(7)));
    }

    [TestMethod]
    public void Delete_Root()
    {
        AvlTree tree = Build(4, 2, 6, 1, 3, 5, 7);
        Assert.IsTrue(tree.Delete(Make(4)));
        AssertInvariants(tree);
        Assert.AreEqual(Make(5).StudentId, tree.Root!.Data.StudentId); // nút nhỏ nhất bên phải
        Assert.AreEqual(6, tree.Count);
    }

    [TestMethod]
    public void Delete_UntilEmpty()
    {
        AvlTree tree = Build(4, 2, 6, 1, 3, 5, 7);
        foreach (int id in new[] { 4, 1, 7, 2, 6, 3, 5 })
        {
            Assert.IsTrue(tree.Delete(Make(id)));
            AssertInvariants(tree);
        }
        Assert.AreEqual(0, tree.Count);
        Assert.IsNull(tree.Root);
        Assert.AreEqual(0, tree.Height);
    }

    [TestMethod]
    public void Delete_SingleNodeTree()
    {
        AvlTree tree = Build(5);
        Assert.IsTrue(tree.Delete(Make(5)));
        Assert.IsNull(tree.Root);
        Assert.AreEqual(0, tree.Count);
    }

    [TestMethod]
    public void Delete_MissingKey_ReturnsFalse()
    {
        AvlTree tree = Build(1, 2, 3);
        Assert.IsFalse(tree.Delete(Make(99)));
        Assert.AreEqual(3, tree.Count);
        Assert.IsFalse(new AvlTree().Delete(Make(1))); // cây rỗng
        AssertInvariants(tree);
    }

    [TestMethod]
    public void Delete_CausesRotation()
    {
        // Xóa 1 làm nhánh trái thấp đi, nút 2 bị lệch phải => phải xoay
        AvlTree tree = Build(2, 1, 3, 4);
        int before = tree.RotationCount;
        tree.Delete(Make(1));
        AssertInvariants(tree);
        Assert.IsGreaterThan(before, tree.RotationCount);
        Assert.AreEqual(Make(3).StudentId, tree.Root!.Data.StudentId);
    }

    // ---------- Khóa trùng, Find, Update, Min, Max ----------

    [TestMethod]
    public void Insert_Duplicate_ReturnsFalseAndKeepsOldData()
    {
        AvlTree tree = Build(1, 2, 3);
        Assert.IsFalse(tree.Insert(Make(2, 4.0)));
        Assert.AreEqual(3, tree.Count);
        Assert.AreEqual(3.0, tree.Find(Make(2))!.Gpa);
        AssertInvariants(tree);
    }

    [TestMethod]
    public void Find_FoundAndNotFound()
    {
        AvlTree tree = Build(1, 2, 3);
        Assert.IsNotNull(tree.Find(Make(3)));
        Assert.IsNull(tree.Find(Make(10)));
    }

    [TestMethod]
    public void Update_ExistingKey_ChangesDataOnly()
    {
        AvlTree tree = Build(1, 2, 3);
        Assert.IsTrue(tree.Update(Make(1, 3.9)));
        Assert.AreEqual(3.9, tree.Find(Make(1))!.Gpa);
        Assert.AreEqual(3, tree.Count);
        Assert.AreEqual(Make(2).StudentId, tree.Root!.Data.StudentId);
    }

    [TestMethod]
    public void Update_MissingKey_ReturnsFalse()
    {
        AvlTree tree = Build(1, 2, 3);
        Assert.IsFalse(tree.Update(Make(9)));
        Assert.AreEqual(3, tree.Count);
    }

    [TestMethod]
    public void MinMax_Works()
    {
        AvlTree tree = Build(5, 3, 8, 1, 9);
        Assert.AreEqual(Make(1).StudentId, tree.Min()!.StudentId);
        Assert.AreEqual(Make(9).StudentId, tree.Max()!.StudentId);
    }

    [TestMethod]
    public void MinMax_EmptyTree_ReturnsNull()
    {
        AvlTree tree = new AvlTree();
        Assert.IsNull(tree.Min());
        Assert.IsNull(tree.Max());
    }

    // ---------- Range ----------

    [TestMethod]
    public void Range_MatchesSortedSet()
    {
        Random random = new Random(1);
        AvlTree tree = new AvlTree();
        SortedSet<int> expectedSet = new SortedSet<int>();
        for (int i = 0; i < 300; i++)
        {
            int id = random.Next(0, 500);
            if (expectedSet.Add(id))
            {
                tree.Insert(Make(id));
            }
        }

        for (int i = 0; i < 200; i++)
        {
            int a = random.Next(0, 520);
            int b = random.Next(0, 520);
            int[] expected = expectedSet.Where(id => id >= a && id <= b).ToArray();
            CollectionAssert.AreEqual(expected, Ids(tree.Range(Make(a), Make(b))), $"Range({a},{b})");
        }
    }

    [TestMethod]
    public void Range_FromGreaterThanTo_IsEmpty()
    {
        AvlTree tree = Build(1, 2, 3, 4, 5);
        Assert.AreEqual(0, tree.Range(Make(4), Make(2)).Count);
    }

    [TestMethod]
    public void Range_OutsideData()
    {
        AvlTree tree = Build(10, 20, 30);
        Assert.AreEqual(0, tree.Range(Make(1), Make(5)).Count);     // toàn bộ nhỏ hơn dữ liệu
        Assert.AreEqual(0, tree.Range(Make(40), Make(50)).Count);   // toàn bộ lớn hơn dữ liệu
        Assert.AreEqual(3, tree.Range(Make(0), Make(100)).Count);   // bao trùm hết
        Assert.AreEqual(0, new AvlTree().Range(Make(1), Make(2)).Count); // cây rỗng
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
        AvlTree tree = Build(4, 2, 6, 1, 3, 5, 7);
        CollectionAssert.AreEqual(new[] { 4, 2, 1, 3, 6, 5, 7 }, Ids(tree.PreOrder()));
    }

    [TestMethod]
    public void PostOrder_Correct()
    {
        AvlTree tree = Build(4, 2, 6, 1, 3, 5, 7);
        CollectionAssert.AreEqual(new[] { 1, 3, 2, 5, 7, 6, 4 }, Ids(tree.PostOrder()));
    }

    [TestMethod]
    public void Traversals_EmptyTree()
    {
        AvlTree tree = new AvlTree();
        Assert.AreEqual(0, tree.PreOrder().Count);
        Assert.AreEqual(0, tree.PostOrder().Count);
        Assert.AreEqual(0, tree.InOrder().Count);
    }

    // ---------- Fuzz và hiệu năng ----------

    [TestMethod]
    public void Fuzz_MatchesSortedSet()
    {
        Random random = new Random(12345);
        AvlTree tree = new AvlTree();
        SortedSet<int> expectedSet = new SortedSet<int>();

        for (int i = 0; i < 10_000; i++)
        {
            int id = random.Next(0, 400);
            if (random.Next(2) == 0)
            {
                bool added = expectedSet.Add(id);
                Assert.AreEqual(added, tree.Insert(Make(id)));
            }
            else
            {
                bool removed = expectedSet.Remove(id);
                Assert.AreEqual(removed, tree.Delete(Make(id)));
            }

            AssertInvariants(tree);
            Assert.AreEqual(expectedSet.Count, tree.Count);
            CollectionAssert.AreEqual(expectedSet.ToArray(), Ids(tree.InOrder()));
        }
    }

    [TestMethod]
    public void Insert100kAscending_HeightIsLogarithmic()
    {
        AvlTree tree = new AvlTree();
        const int n = 100_000;
        for (int i = 0; i < n; i++)
        {
            tree.Insert(Make(i));
        }

        Assert.AreEqual(n, tree.Count);
        Assert.IsLessThanOrEqualTo(1.44 * Math.Log2(n + 2), tree.Height);
        CheckNode(tree.Root);
    }
}
