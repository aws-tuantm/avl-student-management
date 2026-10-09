using AVLStudentManagement.Core.DataStructures;
using AVLStudentManagement.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;

namespace AVLStudentManagement.App.ViewModels;

// Một nút để vẽ: X, Y là góc trên trái trên Canvas.
public class TreeNodeItem
{
    public Student Student { get; }

    public string StudentId
    {
        get { return Student.StudentId; }
    }

    public string FullName
    {
        get { return Student.FullName; }
    }

    public int Height { get; }
    public int BalanceFactor { get; }
    public double X { get; }
    public double Y { get; }

    // Thông tin chính của sinh viên, hiện khi rê chuột vào nút
    public string Tip { get; }

    // Nút nằm trong kết quả tìm kiếm/lọc đang hiện ở bảng (tô màu xanh dương)
    public bool IsMatch { get; set; }

    // Nút nằm trên đường đi tìm mã SV từ gốc xuống (viền đậm)
    public bool IsOnPath { get; set; }

    // Đang lọc mà nút không nằm trong kết quả (làm mờ)
    public bool IsDimmed { get; set; }

    // Nút lệch (|bf| = 1) thì tô màu khác.
    public bool IsLeaning
    {
        get { return BalanceFactor != 0; }
    }

    public TreeNodeItem(Student student, int height, int balanceFactor, double x, double y)
    {
        Student = student;
        Height = height;
        BalanceFactor = balanceFactor;
        X = x;
        Y = y;

        Tip = student.StudentId + " - " + student.FullName + "\n" +
              student.Gender.ToDisplay() + " | Lớp " + student.ClassName + " | " + student.Status.ToDisplay() + "\n" +
              "Điểm TB: " + student.Gpa.ToString("0.00") + " (" + student.Grade.ToDisplay() + ")\n" +
              "Nút này: h = " + height + ", bf = " + balanceFactor;
    }
}

public class TreeEdgeItem
{
    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }

    public TreeEdgeItem(double x1, double y1, double x2, double y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }
}

// Tính tọa độ cây AVL: x = thứ tự in-order, y = độ sâu.
public partial class TreeVisualizerViewModel : ObservableObject
{
    // Cây nhiều hơn số nút này thì không vẽ (vẽ hàng nghìn nút sẽ rất chậm)
    private const int MaxDrawNodes = 300;

    private const int StepX = 124;
    private const int StepY = 90;

    private readonly AvlTree tree;

    public int NodeWidth { get; } = 112;
    public int NodeHeight { get; } = 58;

    public ObservableCollection<TreeNodeItem> Nodes { get; } = new ObservableCollection<TreeNodeItem>();
    public ObservableCollection<TreeEdgeItem> Edges { get; } = new ObservableCollection<TreeEdgeItem>();
    public string[] Orders { get; } = new string[] { "Pre-order", "In-order", "Post-order" };

    [ObservableProperty] private double canvasWidth;
    [ObservableProperty] private double canvasHeight;
    [ObservableProperty] private int orderIndex = 1;
    [ObservableProperty] private string traversal = "";
    [ObservableProperty] private string treeNotice = "";

    // Số liệu của phần cây đang vẽ (đổi theo ô "Số nút vẽ"), hiện trên thanh công cụ của tab
    [ObservableProperty] private int drawnCount;
    [ObservableProperty] private int drawnHeight;
    [ObservableProperty] private string drawnLog2Text = "0.00";
    [ObservableProperty] private string drawnCountTip = "";
    [ObservableProperty] private string drawnHeightTip = "";
    [ObservableProperty] private string drawnLog2Tip = "";

    // Ô nhập số nút muốn vẽ, để trống = vẽ tất cả (tối đa MaxDrawNodes)
    [ObservableProperty] private string nodeLimitText = "";

    // Mức phóng to (1 = 100%), giữ trong khoảng 20% - 300%
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ZoomText))] private double zoom = 1.0;

    private int drawnLevels;

    // Số nút muốn vẽ (người dùng nhập), mặc định là tối đa
    private int nodeLimit = MaxDrawNodes;

    // Mã SV của các nút được vẽ: nodeLimit nút đầu tiên theo từng tầng, tính từ gốc
    private HashSet<string> drawIds = new HashSet<string>();

    private HashSet<string> matchIds = new HashSet<string>();
    private HashSet<string> pathIds = new HashSet<string>();
    private bool isFiltering;

    public TreeVisualizerViewModel(AvlTree tree)
    {
        this.tree = tree;
        Refresh();
    }

    public string ZoomText
    {
        get { return Math.Round(Zoom * 100) + "%"; }
    }

    // Nhân mức phóng to với factor (> 1 phóng to, < 1 thu nhỏ)
    public void ChangeZoom(double factor)
    {
        double newZoom = Zoom * factor;
        if (newZoom < 0.2)
        {
            newZoom = 0.2;
        }
        if (newZoom > 3.0)
        {
            newZoom = 3.0;
        }
        Zoom = newZoom;
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ChangeZoom(1.25);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ChangeZoom(0.8);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        Zoom = 1.0;
    }

    partial void OnOrderIndexChanged(int value)
    {
        UpdateTraversal();
    }

    // Tô sáng các nút nằm trong kết quả đang hiện ở bảng (bảng hiện đủ mọi sinh viên thì không tô).
    public void Highlight(List<Student> shown)
    {
        matchIds.Clear();
        pathIds.Clear();
        isFiltering = shown.Count < tree.Count;

        if (isFiltering)
        {
            foreach (Student student in shown)
            {
                matchIds.Add(student.StudentId);
            }
        }

        Refresh();
    }

    // Tô đường đi tìm mã SV từ gốc xuống, trả về số nút đã ghé qua.
    public int ShowPath(string studentId)
    {
        pathIds.Clear();

        AvlNode? node = tree.Root;
        while (node != null)
        {
            pathIds.Add(node.Data.StudentId);

            int compare = AvlTree.CompareIds(studentId, node.Data.StudentId);
            if (compare == 0)
            {
                break;
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

        Refresh();
        return pathIds.Count;
    }

    // Đọc số nút từ ô nhập rồi vẽ lại. Để trống hoặc nhập sai thì vẽ tất cả (tối đa MaxDrawNodes nút).
    [RelayCommand]
    private void ApplyLimit()
    {
        int n;
        if (int.TryParse(NodeLimitText.Trim(), out n) && n > 0)
        {
            nodeLimit = Math.Min(n, MaxDrawNodes);
            NodeLimitText = nodeLimit.ToString();
        }
        else
        {
            nodeLimit = MaxDrawNodes;
            NodeLimitText = "";
        }
        Refresh();
    }

    public void Refresh()
    {
        Nodes.Clear();
        Edges.Clear();

        // Chọn nodeLimit nút đầu tiên theo từng tầng (từ gốc xuống), nên các nút được vẽ luôn nối liền với gốc
        drawIds.Clear();
        Queue<AvlNode> queue = new Queue<AvlNode>();
        if (tree.Root != null)
        {
            queue.Enqueue(tree.Root);
        }
        while (queue.Count > 0 && drawIds.Count < nodeLimit)
        {
            AvlNode current = queue.Dequeue();
            drawIds.Add(current.Data.StudentId);
            if (current.Left != null)
            {
                queue.Enqueue(current.Left);
            }
            if (current.Right != null)
            {
                queue.Enqueue(current.Right);
            }
        }
        drawnLevels = 0;

        int counter = 0;
        Place(tree.Root, 0, ref counter);

        TreeNotice = "";
        if (Nodes.Count < tree.Count)
        {
            TreeNotice = $"Đang vẽ {Nodes.Count} / {tree.Count} nút đầu tiên theo từng tầng (từ gốc xuống). Tìm theo mã SV để hiện đường đi tới nút khác.";
        }

        // Số liệu của phần cây đang vẽ
        DrawnCount = Nodes.Count;
        DrawnHeight = drawnLevels;
        double log = 0;
        if (DrawnCount > 0)
        {
            log = Math.Log2(DrawnCount);
        }
        DrawnLog2Text = log.ToString("F2");

        int lowerPower = (int)Math.Floor(log);
        DrawnCountTip = $"n = {DrawnCount}\nLà số nút đang vẽ, trên tổng {tree.Count} sinh viên của cả cây.\nĐổi theo ô \"Số nút vẽ\".";
        DrawnHeightTip = $"h = {DrawnHeight}\nLà số tầng của phần cây đang vẽ (đường dài nhất từ gốc xuống nút đang vẽ).\nCả cây thật cao {tree.Height} tầng.";
        DrawnLog2Tip = $"log2(n) = log2({DrawnCount}) = {log:F2}\nVì 2^{lowerPower} = {1L << lowerPower} <= {DrawnCount} < 2^{lowerPower + 1} = {1L << (lowerPower + 1)}.";

        CanvasWidth = counter * StepX + 20;
        CanvasHeight = drawnLevels * StepY + 20;
        UpdateTraversal();
    }

    // Duyệt in-order: trái -> nút -> phải, đếm thứ tự để ra X
    private TreeNodeItem? Place(AvlNode? node, int depth, ref int counter)
    {
        if (node == null)
        {
            return null;
        }

        // Nút ngoài số nút muốn vẽ thì bỏ qua, trừ khi nằm trên đường đi tìm kiếm
        if (!drawIds.Contains(node.Data.StudentId) && !pathIds.Contains(node.Data.StudentId))
        {
            return null;
        }

        drawnLevels = Math.Max(drawnLevels, depth + 1);
        TreeNodeItem? left = Place(node.Left, depth + 1, ref counter);

        double x = counter * StepX + 10;
        double y = depth * StepY + 10;
        TreeNodeItem me = new TreeNodeItem(node.Data, node.Height, node.BalanceFactor, x, y);
        me.IsMatch = isFiltering && matchIds.Contains(me.StudentId);
        me.IsOnPath = pathIds.Contains(me.StudentId);
        me.IsDimmed = isFiltering && !me.IsMatch && !me.IsOnPath;
        counter++;

        TreeNodeItem? right = Place(node.Right, depth + 1, ref counter);

        Nodes.Add(me);
        if (left != null)
        {
            Edges.Add(new TreeEdgeItem(me.X + NodeWidth / 2, me.Y + NodeHeight, left.X + NodeWidth / 2, left.Y));
        }
        if (right != null)
        {
            Edges.Add(new TreeEdgeItem(me.X + NodeWidth / 2, me.Y + NodeHeight, right.X + NodeWidth / 2, right.Y));
        }
        return me;
    }

    private void UpdateTraversal()
    {
        // Cây lớn: danh sách duyệt dài hàng nghìn mã, không hiện
        if (tree.Count > MaxDrawNodes)
        {
            Traversal = "(quá dài để hiện, cây có " + tree.Count + " sinh viên)";
            return;
        }

        List<Student> sequence;
        if (OrderIndex == 0)
        {
            sequence = tree.PreOrder();
        }
        else if (OrderIndex == 2)
        {
            sequence = tree.PostOrder();
        }
        else
        {
            sequence = tree.InOrder();
        }

        StringBuilder text = new StringBuilder();
        foreach (Student student in sequence)
        {
            if (text.Length > 0)
            {
                text.Append(", ");
            }
            text.Append(student.StudentId);
        }
        Traversal = text.ToString();
    }
}
