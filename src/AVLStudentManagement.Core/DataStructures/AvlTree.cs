namespace AVLStudentManagement.Core.DataStructures;

/// <summary>Một nút trong cây AVL.</summary>
public sealed class AvlNode<TKey, TValue>
{
    public TKey Key { get; internal set; }
    public TValue Value { get; internal set; }
    public AvlNode<TKey, TValue>? Left { get; internal set; }
    public AvlNode<TKey, TValue>? Right { get; internal set; }

    /// <summary>Chiều cao của nút. Nút lá = 1, nút rỗng (null) = 0.</summary>
    public int Height { get; internal set; } = 1;

    /// <summary>Hệ số cân bằng = cao(trái) - cao(phải). Cây AVL luôn nằm trong khoảng -1..1.</summary>
    public int BalanceFactor => (Left?.Height ?? 0) - (Right?.Height ?? 0);

    public AvlNode(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>Cây nhị phân tìm kiếm tự cân bằng AVL. Khóa không được trùng.</summary>
public sealed class AvlTree<TKey, TValue>
{
    private readonly IComparer<TKey> comparer;

    public AvlNode<TKey, TValue>? Root { get; private set; }
    public int Count { get; private set; }
    public int Height => Root?.Height ?? 0;

    /// <summary>Tổng số phép xoay đã thực hiện (dùng để đo hiệu năng).</summary>
    public int RotationCount { get; private set; }

    public AvlTree(IComparer<TKey>? comparer = null)
    {
        this.comparer = comparer ?? Comparer<TKey>.Default;
    }

    // ---------- Thêm / Xóa / Sửa ----------

    /// <summary>Thêm một phần tử. Trả về false nếu khóa đã tồn tại.</summary>
    public bool Insert(TKey key, TValue value)
    {
        bool added = false;
        Root = Insert(Root, key, value, ref added);
        if (added) Count++;
        return added;
    }

    /// <summary>Xóa phần tử theo khóa. Trả về false nếu không tìm thấy.</summary>
    public bool Delete(TKey key)
    {
        bool removed = false;
        Root = Delete(Root, key, ref removed);
        if (removed) Count--;
        return removed;
    }

    /// <summary>Thay giá trị của một khóa đã có. Cấu trúc cây không đổi.</summary>
    public bool TryUpdate(TKey key, TValue value)
    {
        var node = FindNode(key);
        if (node == null) return false;
        node.Value = value;
        return true;
    }

    // ---------- Tìm kiếm ----------

    public bool TryGet(TKey key, out TValue value)
    {
        var node = FindNode(key);
        value = node != null ? node.Value : default!;
        return node != null;
    }

    /// <summary>Phần tử nhỏ nhất (đi hết về bên trái).</summary>
    public KeyValuePair<TKey, TValue> Min()
    {
        if (Root == null) throw new InvalidOperationException("Cây đang rỗng.");
        return ToPair(MinNode(Root));
    }

    /// <summary>Phần tử lớn nhất (đi hết về bên phải).</summary>
    public KeyValuePair<TKey, TValue> Max()
    {
        if (Root == null) throw new InvalidOperationException("Cây đang rỗng.");
        var node = Root;
        while (node.Right != null) node = node.Right;
        return ToPair(node);
    }

    /// <summary>Các phần tử có khóa trong đoạn [from, to], theo thứ tự tăng dần.</summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> Range(TKey from, TKey to)
    {
        var stack = new Stack<AvlNode<TKey, TValue>>();
        var node = Root;

        while (node != null || stack.Count > 0)
        {
            // Đi xuống trái, nhưng bỏ qua những nút nhỏ hơn "from"
            while (node != null)
            {
                if (comparer.Compare(node.Key, from) >= 0)
                {
                    stack.Push(node);
                    node = node.Left;
                }
                else
                {
                    node = node.Right;
                }
            }

            if (stack.Count == 0) yield break;

            node = stack.Pop();
            if (comparer.Compare(node.Key, to) > 0) yield break; // đã vượt quá "to" thì dừng hẳn
            yield return ToPair(node);
            node = node.Right;
        }
    }

    // ---------- Duyệt cây ----------

    /// <summary>Trái - Gốc - Phải: kết quả tăng dần theo khóa.</summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> InOrder() => Walk(ascending: true);

    /// <summary>Phải - Gốc - Trái: kết quả giảm dần theo khóa.</summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> InOrderDescending() => Walk(ascending: false);

    // Duyệt giữa bằng stack. ascending = true thì đi trái trước, false thì đi phải trước
    private IEnumerable<KeyValuePair<TKey, TValue>> Walk(bool ascending)
    {
        var stack = new Stack<AvlNode<TKey, TValue>>();
        var node = Root;

        while (node != null || stack.Count > 0)
        {
            while (node != null)
            {
                stack.Push(node);
                node = ascending ? node.Left : node.Right;
            }
            node = stack.Pop();
            yield return ToPair(node);
            node = ascending ? node.Right : node.Left;
        }
    }

    /// <summary>Gốc - Trái - Phải.</summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> PreOrder()
    {
        var stack = new Stack<AvlNode<TKey, TValue>>();
        if (Root != null) stack.Push(Root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return ToPair(node);
            // Đẩy phải trước để trái được lấy ra trước
            if (node.Right != null) stack.Push(node.Right);
            if (node.Left != null) stack.Push(node.Left);
        }
    }

    /// <summary>Trái - Phải - Gốc.</summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> PostOrder()
    {
        // Lấy thứ tự Gốc - Phải - Trái rồi đảo ngược lại sẽ ra Trái - Phải - Gốc
        var work = new Stack<AvlNode<TKey, TValue>>();
        var reversed = new Stack<AvlNode<TKey, TValue>>();
        if (Root != null) work.Push(Root);

        while (work.Count > 0)
        {
            var node = work.Pop();
            reversed.Push(node);
            if (node.Left != null) work.Push(node.Left);
            if (node.Right != null) work.Push(node.Right);
        }

        foreach (var node in reversed) yield return ToPair(node);
    }

    // ---------- Hàm nội bộ ----------

    private AvlNode<TKey, TValue> Insert(AvlNode<TKey, TValue>? node, TKey key, TValue value, ref bool added)
    {
        if (node == null)
        {
            added = true;
            return new AvlNode<TKey, TValue>(key, value);
        }

        int cmp = comparer.Compare(key, node.Key);
        if (cmp < 0) node.Left = Insert(node.Left, key, value, ref added);
        else if (cmp > 0) node.Right = Insert(node.Right, key, value, ref added);
        else return node; // khóa trùng: không làm gì

        return Rebalance(node);
    }

    private AvlNode<TKey, TValue>? Delete(AvlNode<TKey, TValue>? node, TKey key, ref bool removed)
    {
        if (node == null) return null;

        int cmp = comparer.Compare(key, node.Key);
        if (cmp < 0) node.Left = Delete(node.Left, key, ref removed);
        else if (cmp > 0) node.Right = Delete(node.Right, key, ref removed);
        else
        {
            removed = true;

            // Có 0 hoặc 1 con: lấy con lên thay chỗ
            if (node.Left == null) return node.Right;
            if (node.Right == null) return node.Left;

            // Có 2 con: lấy nút nhỏ nhất bên phải lên thay, rồi xóa nút đó đi
            var next = MinNode(node.Right);
            node.Key = next.Key;
            node.Value = next.Value;
            bool ignored = false;
            node.Right = Delete(node.Right, next.Key, ref ignored);
        }

        return Rebalance(node);
    }

    /// <summary>Cập nhật chiều cao, nếu lệch quá 1 thì xoay để cân bằng lại.</summary>
    private AvlNode<TKey, TValue> Rebalance(AvlNode<TKey, TValue> node)
    {
        UpdateHeight(node);
        int balance = node.BalanceFactor;

        if (balance > 1) // lệch trái
        {
            if (node.Left!.BalanceFactor < 0) node.Left = RotateLeft(node.Left); // ca Trái-Phải
            return RotateRight(node);
        }

        if (balance < -1) // lệch phải
        {
            if (node.Right!.BalanceFactor > 0) node.Right = RotateRight(node.Right); // ca Phải-Trái
            return RotateLeft(node);
        }

        return node;
    }

    private AvlNode<TKey, TValue> RotateRight(AvlNode<TKey, TValue> node)
    {
        var newTop = node.Left!;
        node.Left = newTop.Right;
        newTop.Right = node;

        UpdateHeight(node);
        UpdateHeight(newTop);
        RotationCount++;
        return newTop;
    }

    private AvlNode<TKey, TValue> RotateLeft(AvlNode<TKey, TValue> node)
    {
        var newTop = node.Right!;
        node.Right = newTop.Left;
        newTop.Left = node;

        UpdateHeight(node);
        UpdateHeight(newTop);
        RotationCount++;
        return newTop;
    }

    private static void UpdateHeight(AvlNode<TKey, TValue> node)
    {
        node.Height = Math.Max(node.Left?.Height ?? 0, node.Right?.Height ?? 0) + 1;
    }

    private AvlNode<TKey, TValue>? FindNode(TKey key)
    {
        var node = Root;
        while (node != null)
        {
            int cmp = comparer.Compare(key, node.Key);
            if (cmp == 0) return node;
            node = cmp < 0 ? node.Left : node.Right;
        }
        return null;
    }

    private static AvlNode<TKey, TValue> MinNode(AvlNode<TKey, TValue> node)
    {
        while (node.Left != null) node = node.Left;
        return node;
    }

    private static KeyValuePair<TKey, TValue> ToPair(AvlNode<TKey, TValue> node) => new(node.Key, node.Value);
}
