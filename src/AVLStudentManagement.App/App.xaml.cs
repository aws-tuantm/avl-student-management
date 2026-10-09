using AVLStudentManagement.App.ViewModels;
using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Services;
using System.IO;
using System.Windows;

namespace AVLStudentManagement.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            string path = FindDataFilePath();

            // Ráp các đối tượng: Repository -> Service -> ViewModel -> cửa sổ chính
            ExcelStudentRepository repository = new ExcelStudentRepository(path);
            StudentService service = new StudentService(repository);
            service.Load();

            MainWindow window = new MainWindow();
            window.DataContext = new MainViewModel(service, path);
            window.Show();
        }
        catch (Exception ex)
        {
            string message;
            if (ex is StudentException)
            {
                message = "File dữ liệu bị lỗi:\n" + ex.Message;
            }
            else
            {
                message = "Không khởi động được:\n" + ex.Message;
            }

            MessageBox.Show(message, "Lỗi khởi động", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    // File dữ liệu: dataHoSoSinhVien.xlsx trong thư mục project (tìm ngược từ bin lên chỗ có .csproj), bản đóng gói thì cạnh file exe.
    private static string FindDataFilePath()
    {
        string folder = AppContext.BaseDirectory;

        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string projectFile = Path.Combine(current.FullName, "AVLStudentManagement.App.csproj");
            if (File.Exists(projectFile))
            {
                folder = current.FullName;
                break;
            }
            current = current.Parent;
        }

        return Path.Combine(folder, "data", "HoSoSinhVien.xlsx");
    }

    // Lỗi không bắt được: ghi log.txt cạnh file exe rồi báo người dùng
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n";
            File.AppendAllText(logPath, line);
        }
        catch
        {
        }

        MessageBox.Show("Có lỗi không mong muốn:\n" + e.Exception.Message,
            "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
