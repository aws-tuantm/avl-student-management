using System.Collections.ObjectModel;
using AVLStudentManagement.Core.DataStructures;
using AVLStudentManagement.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AVLStudentManagement.App.ViewModels;

/// <summary>Một nút để vẽ: X, Y là góc trên trái trên Canvas.</summary>
public record TreeNodeItem(string StudentId, int Height, int BalanceFactor, double X, double Y)
{
    /// <summary>Nút lệch (|bf| = 1) thì tô màu khác.</summary>
    public bool IsLeaning => BalanceFactor != 0;
}

/// <summary>Một cạnh nối cha với con.</summary>
public record TreeEdgeItem(double X1, double Y1, double X2, double Y2);

/// <summary>Tính tọa độ cây AVL: x = thứ tự in-order, y = độ sâu.</summary>
public partial class TreeVisualizerViewModel : ObservableObject
{
    private const int StepX = 90, StepY = 80;

    private readonly AvlTree<string, Student> tree;

    // Kích thước nút, XAML bind vào đây
    public int NodeWidth { get; } = 84;
    public int NodeHeight { get; } = 44;

    public ObservableCollection<TreeNodeItem> Nodes { get; } = new();
    public ObservableCollection<TreeEdgeItem> Edges { get; } = new();
    public string[] Orders { get; } = { "Pre-order", "In-order", "Post-order" };

    [ObservableProperty] private double canvasWidth;
    [ObservableProperty] private double canvasHeight;
    [ObservableProperty] private int orderIndex = 1;
    [ObservableProperty] private string traversal = "";

    public TreeVisualizerViewModel(AvlTree<string, Student> tree)
    {
        this.tree = tree;
        Refresh();
    }

    partial void OnOrderIndexChanged(int value) => UpdateTraversal();

    /// <summary>Vẽ lại cây (gọi sau mỗi thao tác ghi).</summary>
    public void Refresh()
    {
        Nodes.Clear();
        Edges.Clear();
        int counter = 0;
        Place(tree.Root, 0, ref counter);
        CanvasWidth = counter * StepX + 20;
        CanvasHeight = tree.Height * StepY + 20;
        UpdateTraversal();
    }

    // Duyệt in-order: trái -> nút -> phải, đếm thứ tự để ra X
    private TreeNodeItem? Place(AvlNode<string, Student>? node, int depth, ref int counter)
    {
        if (node == null) return null;

        var left = Place(node.Left, depth + 1, ref counter);
        var me = new TreeNodeItem(node.Key, node.Height, node.BalanceFactor, counter * StepX + 10, depth * StepY + 10);
        counter++;
        var right = Place(node.Right, depth + 1, ref counter);

        Nodes.Add(me);
        if (left != null) Edges.Add(new TreeEdgeItem(me.X + NodeWidth / 2, me.Y + NodeHeight, left.X + NodeWidth / 2, left.Y));
        if (right != null) Edges.Add(new TreeEdgeItem(me.X + NodeWidth / 2, me.Y + NodeHeight, right.X + NodeWidth / 2, right.Y));
        return me;
    }

    private void UpdateTraversal()
    {
        var seq = OrderIndex switch
        {
            0 => tree.PreOrder(),
            2 => tree.PostOrder(),
            _ => tree.InOrder(),
        };
        Traversal = string.Join(", ", seq.Select(p => p.Key));
    }
}
