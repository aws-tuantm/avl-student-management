using AVLStudentManagement.App.ViewModels;
using AVLStudentManagement.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AVLStudentManagement.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Ctrl+F / F3: chuyển về tab Hồ sơ rồi đưa con trỏ vào ô tìm nhanh
    private void FocusSearch(object sender, ExecutedRoutedEventArgs e)
    {
        MainViewModel? viewModel = DataContext as MainViewModel;
        if (viewModel != null)
        {
            viewModel.SelectedTabIndex = 0;
        }

        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    // Nhấp đúp hàng: mở panel chi tiết (nếu đang thu gọn) và đưa con trỏ vào form
    private void StudentGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? clicked = e.OriginalSource as DependencyObject;
        if (clicked == null)
        {
            return;
        }

        // Chỉ xử lý khi bấm vào một hàng dữ liệu (không phải tiêu đề cột hay vùng trống)
        DependencyObject? row = ItemsControl.ContainerFromElement(StudentGrid, clicked);
        if (row is DataGridRow)
        {
            FocusForm();
        }
    }

    // Chuột phải vào dòng chưa được chọn: chọn riêng dòng đó. Dòng đã nằm trong vùng chọn thì giữ nguyên vùng chọn.
    private void Row_RightClick(object sender, MouseButtonEventArgs e)
    {
        DataGridRow? row = sender as DataGridRow;
        if (row != null && !row.IsSelected)
        {
            StudentGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    // Chọn nhiều dòng (Shift + bấm, Ctrl + bấm, Ctrl + A): báo cho ViewModel danh sách đang chọn
    private void StudentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        MainViewModel? viewModel = DataContext as MainViewModel;
        if (viewModel == null)
        {
            return;
        }

        List<Student> selected = new List<Student>();
        foreach (object item in StudentGrid.SelectedItems)
        {
            selected.Add((Student)item);
        }
        viewModel.SetSelectedStudents(selected);
    }

    private void FocusForm()
    {
        MainViewModel? viewModel = DataContext as MainViewModel;
        if (viewModel != null && viewModel.DetailWidth.Value == 0)
        {
            viewModel.ToggleDetailPaneCommand.Execute(null);
        }

        // Chờ giao diện cập nhật xong rồi mới đưa con trỏ vào ô Họ tên
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusFullNameBox));
    }

    // Giữ Ctrl và lăn chuột trên cây: phóng to/thu nhỏ (không giữ Ctrl thì cuộn bình thường)
    private void TreeScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        MainViewModel? viewModel = DataContext as MainViewModel;
        if (Keyboard.Modifiers != ModifierKeys.Control || viewModel == null)
        {
            return;
        }

        if (e.Delta > 0)
        {
            viewModel.Tree.ChangeZoom(1.1);
        }
        else
        {
            viewModel.Tree.ChangeZoom(1.0 / 1.1);
        }
        e.Handled = true;
    }

    private void FocusFullNameBox()
    {
        FullNameBox.Focus();
        FullNameBox.SelectAll();
    }
}
