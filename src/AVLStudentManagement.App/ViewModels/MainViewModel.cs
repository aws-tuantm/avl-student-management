using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Services;
using AVLStudentManagement.Core.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVLStudentManagement.App.ViewModels;

public partial class MainViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly StudentService service;
    private double lastMs;
    private bool isLoadingTable; // true khi đang nạp lại danh sách vào bảng

    private readonly string dataPath; // file Excel đang làm cơ sở dữ liệu

    public MainViewModel(StudentService service, string dataPath)
    {
        this.service = service;
        this.dataPath = dataPath;
        Faculties = service.Catalog.Faculties.ToList();
        Tree = new TreeVisualizerViewModel(service.Tree);
        filterStatus = StatusOptions[0];
        filterGrade = GradeOptions[0];
        ShowAll();
    }

    // ---------- Dữ liệu cho giao diện ----------

    public ObservableCollection<Student> Items { get; } = new();
    public ObservableCollection<string> ClassNames { get; } = new();
    public List<string> Faculties { get; }
    public TreeVisualizerViewModel Tree { get; }

    public Gender[] Genders { get; } = Enum.GetValues<Gender>();
    public Status[] Statuses { get; } = Enum.GetValues<Status>();

    // Lựa chọn cho ComboBox lọc: phần tử đầu là "(Tất cả)" (Value = null)
    public List<Option<Status>> StatusOptions { get; } =
        new[] { new Option<Status>(null, "(Tất cả)") }
            .Concat(Enum.GetValues<Status>().Select(s => new Option<Status>(s, s.ToDisplay()))).ToList();
    public List<Option<Grade>> GradeOptions { get; } =
        new[] { new Option<Grade>(null, "(Tất cả)") }
            .Concat(Enum.GetValues<Grade>().Select(g => new Option<Grade>(g, g.ToDisplay()))).ToList();

    // ---------- Form nhập (tên trùng tên trường để khớp lỗi từ Validator) ----------

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNew))] private Student? selectedItem;
    [ObservableProperty] private string studentId = "";
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private DateTime? birthDate;
    [ObservableProperty] private Gender gender;
    [ObservableProperty] private string nationalId = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string address = "";
    [ObservableProperty] private string faculty = "";
    [ObservableProperty] private string className = "";
    [ObservableProperty] private Status status;
    [ObservableProperty] private string gpa = "";

    /// <summary>true khi đang thêm mới; false khi đang sửa (khóa ô StudentId).</summary>
    public bool IsNew => SelectedItem == null;

    // ---------- Bộ lọc ----------

    [ObservableProperty] private string searchId = "";
    [ObservableProperty] private string filterName = "";
    [ObservableProperty] private string filterClass = "";
    [ObservableProperty] private string filterFaculty = "";
    [ObservableProperty] private Option<Status> filterStatus;
    [ObservableProperty] private Option<Grade> filterGrade;
    [ObservableProperty] private string fromId = "";
    [ObservableProperty] private string toId = "";
    [ObservableProperty] private string fromGpa = "";
    [ObservableProperty] private string toGpa = "";
    [ObservableProperty] private string topCount = "5";

    // Các ô trên thanh trạng thái
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private int visibleCount;
    [ObservableProperty] private int treeHeight;
    [ObservableProperty] private string log2Text = "0.00";
    [ObservableProperty] private int rotationCount; // số lần xoay của cây MaSV từ lúc nạp
    [ObservableProperty] private string lastTimeText = "0 ms";
    [ObservableProperty] private string message = "Sẵn sàng";

    // ---------- Chọn dòng / đổi khoa ----------

    partial void OnSelectedItemChanged(Student? value)
    {
        if (isLoadingTable) return; // đang làm mới danh sách: giữ nguyên form
        if (value == null) { ResetForm(); return; }

        SetErrors(new());
        StudentId = value.StudentId;
        FullName = value.FullName;
        BirthDate = value.BirthDate;
        Gender = value.Gender;
        NationalId = value.NationalId;
        Email = value.Email;
        Phone = value.Phone;
        Address = value.Address;
        Faculty = value.Faculty;   // phải gán Faculty trước để danh sách Lớp được nạp
        ClassName = value.ClassName;
        Status = value.Status;
        Gpa = value.Gpa.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // Lớp phụ thuộc Khoa: đổi khoa thì nạp lại danh sách lớp
    partial void OnFacultyChanged(string value)
    {
        // ComboBox có thể gán null khi danh sách đổi, đổi lại thành ""
        if (value is null) { Faculty = ""; return; }
        ClassNames.Clear();
        foreach (var name in service.Catalog.ClassesOf(value)) ClassNames.Add(name);
        if (!ClassNames.Contains(ClassName)) ClassName = "";
    }

    partial void OnClassNameChanged(string value)
    {
        if (value is null) ClassName = "";
    }

    private void ResetForm()
    {
        StudentId = FullName = NationalId = Email = Phone = Address = Faculty = ClassName = Gpa = "";
        BirthDate = null;
        Gender = Gender.Male;
        Status = Status.Studying;
    }

    // ---------- Thêm / Sửa / Xóa / Làm mới ----------

    [RelayCommand]
    private void Add()
    {
        if (Run(() => service.Add(BuildStudent()))) AfterWrite();
    }

    [RelayCommand]
    private void Update()
    {
        if (SelectedItem == null) { Info("Hãy chọn một sinh viên trong bảng để cập nhật."); return; }
        if (Run(() => service.Update(BuildStudent()))) AfterWrite();
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem == null) { Info("Hãy chọn một sinh viên trong bảng để xóa."); return; }
        string id = SelectedItem.StudentId;
        if (!Confirm($"Xóa sinh viên {id} - {SelectedItem.FullName}?")) return;
        if (Run(() => service.Delete(id))) AfterWrite();
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedItem = null;
        SetErrors(new());
        ResetForm();
    }

    private Student BuildStudent()
    {
        // Điểm không đọc được -> NaN, Validator sẽ báo lỗi "Điểm TB từ 0 đến 4"
        double gpaValue = TryParseGpa(Gpa, out var parsed) ? parsed : double.NaN;
        return new Student
        {
            StudentId = StudentId, FullName = FullName, BirthDate = BirthDate ?? default, Gender = Gender,
            NationalId = NationalId, Email = Email, Phone = Phone, Address = Address,
            Faculty = Faculty, ClassName = ClassName, Status = Status, Gpa = gpaValue,
        };
    }

    // ---------- Phím tắt ----------

    [ObservableProperty] private bool isAdvancedOpen;   // phần "Nâng cao" đang mở
    [ObservableProperty] private int selectedTabIndex;  // 0 = Hồ sơ, 1 = Cấu trúc cây

    // Ctrl+S: đang thêm mới thì Thêm, đang sửa thì Cập nhật
    [RelayCommand]
    private void Save()
    {
        if (IsNew) Add(); else Update();
    }

    // Esc: đóng phần Nâng cao, bỏ chọn và xóa form
    [RelayCommand]
    private void Escape()
    {
        IsAdvancedOpen = false;
        Clear();
    }

    [RelayCommand]
    private void ShowProfileTab() => SelectedTabIndex = 0;

    [RelayCommand]
    private void ShowTreeTab() => SelectedTabIndex = 1;

    [RelayCommand]
    private void ShowShortcuts() => Info(
        "Ctrl+N: Làm mới form để thêm mới\n" +
        "Ctrl+S: Lưu (Thêm hoặc Cập nhật)\n" +
        "Delete (khi bảng có focus) hoặc Ctrl+D: Xóa dòng đang chọn\n" +
        "F5: Hiện tất cả\n" +
        "Ctrl+F hoặc F3: Đến ô tìm nhanh (Enter để tìm)\n" +
        "Esc: Bỏ chọn, xóa form, đóng phần Nâng cao\n" +
        "Ctrl+1 / Ctrl+2: Chuyển tab Hồ sơ / Cấu trúc cây\n" +
        "F4: Ẩn/hiện panel chi tiết\n" +
        "Ctrl+Shift+F: Bộ lọc nâng cao\n" +
        "F1: Bảng phím tắt\n" +
        "Ctrl+I / Ctrl+E: Nhập / Xuất Excel\n" +
        "Ctrl+Q hoặc Alt+F4: Thoát");

    [RelayCommand]
    private void Exit() => Application.Current.Shutdown();

    // ---------- Nhập / Xuất Excel ----------

    private const string ExcelFilter = "Excel (*.xlsx)|*.xlsx";

    // Xuất: file CSDL luôn được lưu sau mỗi thao tác nên chỉ cần chép ra chỗ người dùng chọn
    [RelayCommand]
    private void ExportExcel()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = ExcelFilter, FileName = "HoSoSinhVien.xlsx" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            File.Copy(dataPath, dialog.FileName, overwrite: true);
            Message = $"Đã xuất ra {dialog.FileName}";
        }
        catch (IOException ex) { Info("Không xuất được file (file đích có đang mở không?).\n" + ex.Message); }
    }

    // Nhập: đọc file được chọn rồi gộp vào dữ liệu hiện có (bỏ qua SV trùng hoặc sai)
    [RelayCommand]
    private void ImportExcel()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = ExcelFilter };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var (students, _) = new ExcelStudentRepository(dialog.FileName).Load();
            var (added, skipped) = Time(() => service.Import(students));
            AfterWrite();
            Info($"Đã nhập {added} sinh viên, bỏ qua {skipped} (trùng khóa hoặc dữ liệu không hợp lệ).");
        }
        catch (DataFormatException ex) { Info($"File nhập bị lỗi: {ex.Message}"); }
        catch (IOException ex) { Info("Không nhập được file (file có đang mở trong Excel không?).\n" + ex.Message); }
    }

    // ---------- Bố cục kiểu desktop ----------

    // Độ rộng cột panel chi tiết (GridSplitter kéo đổi được, 0 = thu gọn)
    [ObservableProperty] private GridLength detailWidth = new(320);
    private double lastDetailWidth = 320;

    // F4: ẩn/hiện panel chi tiết, nhớ độ rộng lần trước
    [RelayCommand]
    private void ToggleDetailPane()
    {
        if (DetailWidth.Value > 0)
        {
            lastDetailWidth = DetailWidth.Value;
            DetailWidth = new GridLength(0);
        }
        else
        {
            DetailWidth = new GridLength(lastDetailWidth);
        }
    }

    // Menu chuột phải "Sửa / Chọn": mở panel chi tiết (dòng đã được chọn nên form đã có dữ liệu)
    [RelayCommand]
    private void ShowDetailPane()
    {
        if (DetailWidth.Value == 0) ToggleDetailPane();
    }
    // Ctrl+Shift+F: đóng/mở bộ lọc nâng cao
    [RelayCommand]
    private void ToggleAdvanced() => IsAdvancedOpen = !IsAdvancedOpen;

    // Menu Công cụ: mở bộ lọc nâng cao để nhập Top N, khoảng...
    [RelayCommand]
    private void OpenAdvanced()
    {
        SelectedTabIndex = 0;
        IsAdvancedOpen = true;
    }

    // Menu chuột phải: sao chép mã SV của dòng đang chọn
    [RelayCommand]
    private void CopyId()
    {
        if (SelectedItem != null) Clipboard.SetText(SelectedItem.StudentId);
    }

    // ---------- Tìm kiếm / lọc ----------

    [RelayCommand]
    private void Search()
    {
        var student = Time(() => service.Find(SearchId.Trim()));
        if (student == null) { Info("Không tìm thấy mã sinh viên này."); return; }
        ShowList(new[] { student });
        SelectedItem = student;
    }

    [RelayCommand]
    private void SearchRange()
    {
        if (!CheckIdRange()) return;
        ShowList(Time(() => service.FindByIdRange(FromId.Trim(), ToId.Trim())));
    }

    [RelayCommand]
    private void DeleteRange()
    {
        if (!CheckIdRange()) return;
        string from = FromId.Trim(), to = ToId.Trim();
        if (from == "" || to == "") { Info("Nhập đủ cả hai đầu của khoảng mã SV trước khi xóa."); return; }
        int count = service.FindByIdRange(from, to).Count;
        if (count == 0) { Info("Không có sinh viên nào trong khoảng này."); return; }
        if (!Confirm($"Xóa {count} sinh viên có mã từ {from} đến {to}?")) return;
        if (Run(() => service.DeleteByIdRange(from, to))) AfterWrite();
    }

    [RelayCommand]
    private void GpaRange()
    {
        if (!TryParseGpa(FromGpa, out var min) || !TryParseGpa(ToGpa, out var max))
        {
            Info("Điểm TB phải là số.");
            return;
        }
        if (min > max) { Info("Điểm bắt đầu phải nhỏ hơn hoặc bằng điểm kết thúc."); return; }
        ShowList(Time(() => service.FindByGpaRange(min, max)));
    }

    [RelayCommand]
    private void TopStudent() => ShowOne(Time(() => service.TopStudent()));

    [RelayCommand]
    private void LowestGpaStudent() => ShowOne(Time(() => service.LowestGpaStudent()));

    [RelayCommand]
    private void TopN()
    {
        if (!int.TryParse(TopCount.Trim(), out var n) || n <= 0)
        {
            Info("Top N phải là số nguyên lớn hơn 0.");
            return;
        }
        ShowList(Time(() => service.TopN(n)));
    }

    [RelayCommand]
    private void Filter() =>
        ShowList(Time(() => service.Filter(FilterName, FilterClass, FilterFaculty, FilterStatus.Value, FilterGrade.Value)));

    [RelayCommand]
    private void ShowAll()
    {
        // Bỏ sắp xếp cột của DataGrid để bảng về lại thứ tự in-order (StudentId tăng dần)
        CollectionViewSource.GetDefaultView(Items).SortDescriptions.Clear();
        ShowList(Time(() => service.GetAll().ToList()));
    }

    private bool CheckIdRange()
    {
        if (string.CompareOrdinal(FromId.Trim(), ToId.Trim()) > 0)
        {
            Info("Mã bắt đầu phải nhỏ hơn hoặc bằng mã kết thúc.");
            return false;
        }
        return true;
    }

    // Chỉ nhận số thập phân thường: không số mũ, không NaN/Infinity
    private static bool TryParseGpa(string text, out double value) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private void ShowOne(Student? student)
    {
        if (student == null) { Info("Chưa có sinh viên nào."); return; }
        ShowList(new[] { student });
    }

    // ---------- Hàm dùng chung ----------

    // Làm mới bảng mà không reset form đang nhập; dòng đang chọn còn trong danh sách thì chọn lại
    private void ShowList(IEnumerable<Student> list)
    {
        var selected = SelectedItem;
        isLoadingTable = true;
        Items.Clear();
        foreach (var student in list) Items.Add(student);
        if (selected != null && Items.Contains(selected)) SelectedItem = selected;
        isLoadingTable = false;
        RefreshStatus();
        Message = $"Hiển thị {Items.Count} kết quả.";
    }

    // Sau khi ghi thành công: làm mới form, bảng, cây (giữ thời gian của thao tác ghi)
    private void AfterWrite()
    {
        Clear();
        CollectionViewSource.GetDefaultView(Items).SortDescriptions.Clear();
        ShowList(service.GetAll().ToList());
        Tree.Refresh();
        Message = "Đã lưu thay đổi vào file Excel.";
    }

    private void RefreshStatus()
    {
        int count = service.Tree.Count;
        double log = count > 0 ? Math.Log2(count) : 0;
        TotalCount = count;
        VisibleCount = Items.Count;
        TreeHeight = service.Tree.Height;
        Log2Text = log.ToString("F2");
        RotationCount = service.Tree.RotationCount;
        LastTimeText = $"{lastMs:F3} ms";
    }

    // Đo thời gian chạy của thao tác
    private T Time<T>(Func<T> func)
    {
        var watch = Stopwatch.StartNew();
        try { return func(); }
        finally { watch.Stop(); lastMs = watch.Elapsed.TotalMilliseconds; }
    }

    private void Time(Action action) => Time(() => { action(); return 0; });

    // Chạy thao tác ghi, bắt lỗi và báo tiếng Việt. Trả về true nếu thành công
    private bool Run(Action action)
    {
        try
        {
            Time(action);
            return true;
        }
        catch (ValidationException ex)
        {
            SetErrors(ex.Errors);
        }
        catch (DuplicateKeyException ex)
        {
            SetErrors(new Dictionary<string, string> { [ex.Field] = ex.Message });
            Info(ex.Message);
        }
        catch (IOException ex)
        {
            Info("Không lưu được file Excel (file có đang mở trong Excel không?). Thao tác đã được hoàn tác.\n" + ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            Info(ex.Message);
        }
        return false;
    }

    private static void Info(string message) => MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

    private static bool Confirm(string message) =>
        MessageBox.Show(message, "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    // ---------- INotifyDataErrorInfo: lỗi theo từng trường ----------

    private Dictionary<string, string> errors = new();
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    public bool HasErrors => errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName != null && errors.TryGetValue(propertyName, out var error) ? new[] { error } : Array.Empty<string>();

    /// <summary>Lấy lỗi của một trường để hiện cạnh ô nhập: {Binding [StudentId]}.</summary>
    public string this[string field] => errors.TryGetValue(field, out var error) ? error : "";

    private void SetErrors(Dictionary<string, string> newErrors)
    {
        var fields = errors.Keys.Union(newErrors.Keys).ToList();
        errors = newErrors;
        foreach (var field in fields) ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));
        OnPropertyChanged("Item[]");
    }

    // Người dùng sửa ô nào thì xóa lỗi của ô đó
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != null && errors.ContainsKey(e.PropertyName))
        {
            var copy = new Dictionary<string, string>(errors);
            copy.Remove(e.PropertyName);
            SetErrors(copy);
        }
    }
}

/// <summary>Một lựa chọn trong ComboBox lọc: Value = null nghĩa là "(Tất cả)".</summary>
public record Option<T>(T? Value, string Label) where T : struct;
