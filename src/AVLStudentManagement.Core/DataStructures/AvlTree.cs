using AVLStudentManagement.Core.Models;

namespace AVLStudentManagement.Core.DataStructures;

public class AvlNode
{
    public Student Data { get; set; }
    public AvlNode? Left { get; set; }
    public AvlNode? Right { get; set; }
    public int Height { get; set; }

    public AvlNode(Student data)
    {
        Data = data;
        Left = null;
        Right = null;
        Height = 1;
    }

    // Hệ số cân bằng = cao(trái) - cao(phải). Cây AVL luôn nằm trong khoảng -1..1.
    public int BalanceFactor
    {
        get { return GetHeight(Left) - GetHeight(Right); }
    }

    public static int GetHeight(AvlNode? node)
    {
        if (node == null)
        {
            return 0;
        }
        return node.Height;
    }
}

// Cây AVL chứa sinh viên, khóa là mã sinh viên (StudentId), không cho trùng khóa.
public class AvlTree
{
    public AvlNode? Root { get; private set; }
    public int Count { get; private set; }

    public int RotationCount { get; private set; }

    public AvlTree()
    {
        Root = null;
        Count = 0;
        RotationCount = 0;
    }

    public int Height
    {
        get { return AvlNode.GetHeight(Root); }
    }


    // Thêm một sinh viên. Trả về false nếu khóa đã tồn tại.
    public bool Insert(Student student)
    {
        if (Find(student) != null)
        {
            return false;
        }

        Root = InsertNode(Root, student);
        Count++;
        return true;
    }

    public bool Delete(Student sample)
    {
        if (Find(sample) == null)
        {
            return false;
        }

        Root = DeleteNode(Root, sample);
        Count--;
        return true;
    }

    // Thay dữ liệu của sinh viên cùng khóa, cấu trúc cây không đổi.
    public bool Update(Student student)
    {
        AvlNode? node = FindNode(student);
        if (node == null)
        {
            return false;
        }

        node.Data = student;
        return true;
    }


    // Tìm sinh viên có cùng khóa với sample, không có thì trả về null.
    public Student? Find(Student sample)
    {
        AvlNode? node = FindNode(sample);
        if (node == null)
        {
            return null;
        }
        return node.Data;
    }

    public Student? Min()
    {
        if (Root == null)
        {
            return null;
        }
        return MinNode(Root).Data;
    }

    public Student? Max()
    {
        if (Root == null)
        {
            return null;
        }

        AvlNode node = Root;
        while (node.Right != null)
        {
            node = node.Right;
        }
        return node.Data;
    }

    // Các sinh viên có khóa trong đoạn [from, to], theo thứ tự tăng dần.
    public List<Student> Range(Student from, Student to)
    {
        List<Student> result = new List<Student>();
        CollectRange(Root, from, to, result);
        return result;
    }

    // Đệ quy an toàn vì cây AVL luôn thấp (khoảng 45 tầng cho 1 triệu sinh viên).

    // Trái - Gốc - Phải: kết quả tăng dần theo khóa.
    public List<Student> InOrder()
    {
        List<Student> result = new List<Student>();
        CollectInOrder(Root, result);
        return result;
    }

    // Gốc - Trái - Phải.
    public List<Student> PreOrder()
    {
        List<Student> result = new List<Student>();
        CollectPreOrder(Root, result);
        return result;
    }

    // Trái - Phải - Gốc.
    public List<Student> PostOrder()
    {
        List<Student> result = new List<Student>();
        CollectPostOrder(Root, result);
        return result;
    }

    private void CollectInOrder(AvlNode? node, List<Student> result)
    {
        if (node == null)
        {
            return;
        }
        CollectInOrder(node.Left, result);
        result.Add(node.Data);
        CollectInOrder(node.Right, result);
    }

    private void CollectPreOrder(AvlNode? node, List<Student> result)
    {
        if (node == null)
        {
            return;
        }
        result.Add(node.Data);
        CollectPreOrder(node.Left, result);
        CollectPreOrder(node.Right, result);
    }

    private void CollectPostOrder(AvlNode? node, List<Student> result)
    {
        if (node == null)
        {
            return;
        }
        CollectPostOrder(node.Left, result);
        CollectPostOrder(node.Right, result);
        result.Add(node.Data);
    }

    // Chỉ đi vào nhánh có thể chứa kết quả, nhờ vậy không phải duyệt hết cây.
    private void CollectRange(AvlNode? node, Student from, Student to, List<Student> result)
    {
        if (node == null)
        {
            return;
        }

        // Nút hiện tại lớn hơn "from" thì bên trái mới có thể có kết quả
        if (Compare(from, node.Data) < 0)
        {
            CollectRange(node.Left, from, to, result);
        }

        if (Compare(from, node.Data) <= 0 && Compare(node.Data, to) <= 0)
        {
            result.Add(node.Data);
        }

        // Nút hiện tại nhỏ hơn "to" thì bên phải mới có thể có kết quả
        if (Compare(node.Data, to) < 0)
        {
            CollectRange(node.Right, from, to, result);
        }
    }


    // Âm nếu a đứng trước b, 0 nếu cùng khóa, dương nếu a đứng sau b.
    private int Compare(Student a, Student b)
    {
        return CompareIds(a.StudentId, b.StudentId);
    }

    // So sánh hai mã SV theo giá trị số: ít chữ số hơn thì nhỏ hơn (2 < 10), cùng số chữ số thì so từng chữ số.
    public static int CompareIds(string a, string b)
    {
        string x = a.TrimStart('0');
        string y = b.TrimStart('0');

        if (x.Length != y.Length)
        {
            return x.Length.CompareTo(y.Length);
        }

        int result = string.CompareOrdinal(x, y);
        if (result != 0)
        {
            return result;
        }
        return string.CompareOrdinal(a, b);
    }

    // Thêm đệ quy: đi xuống đúng chỗ, chèn nút mới, rồi cân bằng lại trên đường quay về.
    private AvlNode InsertNode(AvlNode? node, Student student)
    {
        if (node == null)
        {
            return new AvlNode(student);
        }

        if (Compare(student, node.Data) < 0)
        {
            node.Left = InsertNode(node.Left, student);
        }
        else
        {
            node.Right = InsertNode(node.Right, student);
        }

        return Rebalance(node);
    }

    private AvlNode? DeleteNode(AvlNode? node, Student sample)
    {
        if (node == null)
        {
            return null;
        }

        int compare = Compare(sample, node.Data);
        if (compare < 0)
        {
            node.Left = DeleteNode(node.Left, sample);
        }
        else if (compare > 0)
        {
            node.Right = DeleteNode(node.Right, sample);
        }
        else
        {
            // Có 0 hoặc 1 con: lấy con lên thay chỗ
            if (node.Left == null)
            {
                return node.Right;
            }
            if (node.Right == null)
            {
                return node.Left;
            }

            // Có 2 con: lấy nút nhỏ nhất bên phải lên thay, rồi xóa nút đó đi
            AvlNode next = MinNode(node.Right);
            node.Data = next.Data;
            node.Right = DeleteNode(node.Right, next.Data);
        }

        return Rebalance(node);
    }

    // Cập nhật chiều cao, nếu lệch quá 1 thì xoay để cân bằng lại.
    private AvlNode Rebalance(AvlNode node)
    {
        UpdateHeight(node);
        int balance = node.BalanceFactor;

        if (balance > 1)
        {
            // Ca Trái-Phải: xoay trái con trái trước
            if (node.Left!.BalanceFactor < 0)
            {
                node.Left = RotateLeft(node.Left);
            }
            return RotateRight(node);
        }

        if (balance < -1)
        {
            // Ca Phải-Trái: xoay phải con phải trước
            if (node.Right!.BalanceFactor > 0)
            {
                node.Right = RotateRight(node.Right);
            }
            return RotateLeft(node);
        }

        return node;
    }

    private AvlNode RotateRight(AvlNode node)
    {
        AvlNode newTop = node.Left!;
        node.Left = newTop.Right;
        newTop.Right = node;

        UpdateHeight(node);
        UpdateHeight(newTop);
        RotationCount++;
        return newTop;
    }

    private AvlNode RotateLeft(AvlNode node)
    {
        AvlNode newTop = node.Right!;
        node.Right = newTop.Left;
        newTop.Left = node;

        UpdateHeight(node);
        UpdateHeight(newTop);
        RotationCount++;
        return newTop;
    }

    private void UpdateHeight(AvlNode node)
    {
        int leftHeight = AvlNode.GetHeight(node.Left);
        int rightHeight = AvlNode.GetHeight(node.Right);
        node.Height = Math.Max(leftHeight, rightHeight) + 1;
    }

    private AvlNode? FindNode(Student sample)
    {
        AvlNode? node = Root;
        while (node != null)
        {
            int compare = Compare(sample, node.Data);
            if (compare == 0)
            {
                return node;
            }

            if (compare < 0)
            {
                node = node.Left;
            }
            else
            {
                node = node.Right;
            }
        }
        return null;
    }

    private AvlNode MinNode(AvlNode node)
    {
        while (node.Left != null)
        {
            node = node.Left;
        }
        return node;
    }
}
