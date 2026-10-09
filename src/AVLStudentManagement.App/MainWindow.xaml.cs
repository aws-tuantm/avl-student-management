using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AVLStudentManagement.App.ViewModels;

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
        if (DataContext is MainViewModel vm) vm.SelectedTabIndex = 0;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    // Nhấp đúp hàng: mở panel chi tiết (nếu đang thu gọn) và đưa con trỏ vào form
    private void StudentGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(StudentGrid, source) is DataGridRow)
            FocusForm();
    }

    // Chuột phải: chọn luôn dòng được bấm để menu áp dụng đúng dòng
    private void Row_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row) row.IsSelected = true;
    }

    private void FocusForm()
    {
        if (DataContext is MainViewModel vm && vm.DetailWidth.Value == 0) vm.ToggleDetailPaneCommand.Execute(null);
        // Chờ giao diện cập nhật xong rồi mới focus
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            FullNameBox.Focus();
            FullNameBox.SelectAll();
        });
    }
}
