using System.IO;
using System.Windows;
using AVLStudentManagement.App.ViewModels;
using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Services;

namespace AVLStudentManagement.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            // Ráp các đối tượng: Repository -> Service -> ViewModel
            // "Cơ sở dữ liệu" là data\HoSoSinhVien.xlsx trong thư mục project (đi ngược từ bin\... lên tới chỗ có .csproj).
            // Chạy ngoài project (đã publish) thì dùng data\ cạnh file exe.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AVLStudentManagement.App.csproj"))) dir = dir.Parent;
            var path = Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "data", "HoSoSinhVien.xlsx");
            var service = new StudentService(new ExcelStudentRepository(path));
            service.Load();
            new MainWindow { DataContext = new MainViewModel(service, path) }.Show();
        }
        catch (Exception ex)
        {
            string msg = ex is DataFormatException d
                ? $"File dữ liệu bị lỗi tại dòng {d.Row}, cột {d.Column}:\n{d.Message}"
                : "Không khởi động được:\n" + ex.Message;
            MessageBox.Show(msg, "Lỗi khởi động", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    // Lỗi không bắt được: ghi log.txt cạnh file exe rồi báo người dùng
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "log.txt"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
        }
        catch { /* không ghi được log thì bỏ qua */ }

        MessageBox.Show("Có lỗi không mong muốn:\n" + e.Exception.Message,
            "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
