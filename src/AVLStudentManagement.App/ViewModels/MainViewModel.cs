using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace AVLStudentManagement.App.ViewModels;
public partial class MainViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly StudentService service;

    private readonly string dataPath;

    private Stopwatch watch = new Stopwatch();
    private double lastMs;

    private bool isLoadingTable;

    public MainViewModel(StudentService service, string dataPath)
    {
        this.service = service;
        this.dataPath = dataPath;
        Faculties = new List<string>(service.Catalog.Faculties);
        Tree = new TreeVisualizerViewModel(service.Tree);

        StatusOptions.Add("Tất cả");
        foreach (Status status in Enum.GetValues<Status>())
        {
            StatusOptions.Add(status.ToDisplay());
        }

        GradeOptions.Add("Tất cả");
        foreach (Grade grade in Enum.GetValues<Grade>())
        {
            GradeOptions.Add(grade.ToDisplay());
        }

        ClassOptions.Add("Tất cả");
        foreach (string faculty in service.Catalog.Faculties)
        {
            foreach (string className in service.Catalog.ClassesOf(faculty))
            {
                ClassOptions.Add(className);
            }
        }

        ShowAll();
    }


    public ObservableCollection<Student> Items { get; } = new ObservableCollection<Student>();
    public ObservableCollection<string> ClassNames { get; } = new ObservableCollection<string>();
    public List<string> Faculties { get; }
    public TreeVisualizerViewModel Tree { get; }

    public Gender[] Genders { get; } = Enum.GetValues<Gender>();
    public Status[] Statuses { get; } = Enum.GetValues<Status>();

    public List<string> StatusOptions { get; } = new List<string>();
    public List<string> GradeOptions { get; } = new List<string>();
    public List<string> ClassOptions { get; } = new List<string>();

    // Form nhập (tên thuộc tính trùng tên trường của Student để khớp lỗi kiểm tra)

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

    public bool IsNew
    {
        get { return SelectedItem == null; }
    }


    [ObservableProperty] private string searchId = "";
    [ObservableProperty] private int filterClassIndex;
    [ObservableProperty] private string topCount = "10";
    [ObservableProperty] private int filterStatusIndex;
    [ObservableProperty] private int filterGradeIndex;

    [ObservableProperty] private int totalCount;
    [ObservableProperty] private int visibleCount;
    [ObservableProperty] private int treeHeight;
    [ObservableProperty] private string log2Text = "0.00";
    [ObservableProperty] private int rotationCount;
    [ObservableProperty] private string lastTimeText = "0 ms";

    [ObservableProperty] private string totalTip = "";
    [ObservableProperty] private string visibleTip = "";
    [ObservableProperty] private string heightTip = "";
    [ObservableProperty] private string log2Tip = "";
    [ObservableProperty] private string rotationTip = "";
    [ObservableProperty] private string timeTip = "";
    [ObservableProperty] private string message = "Sẵn sàng";

    // Các dòng đang chọn trong bảng (chọn nhiều bằng Shift hoặc Ctrl)
    private List<Student> selectedStudents = new List<Student>();


    partial void OnSelectedItemChanged(Student? value)
    {
        if (isLoadingTable)
        {
            return;
        }

        if (value == null)
        {
            ResetForm();
            return;
        }

        SetErrors(new Dictionary<string, string>());
        StudentId = value.StudentId;
        FullName = value.FullName;
        BirthDate = value.BirthDate;
        Gender = value.Gender;
        NationalId = value.NationalId;
        Email = value.Email;
        Phone = value.Phone;
        Address = value.Address;

        // Phải gán Faculty trước để danh sách Lớp được nạp
        Faculty = value.Faculty;
        ClassName = value.ClassName;
        Status = value.Status;
        Gpa = value.Gpa.ToString("0.##", CultureInfo.InvariantCulture);
    }

    partial void OnFacultyChanged(string value)
    {
        // ComboBox có thể gán null khi danh sách đổi, khi đó đổi lại thành chuỗi rỗng
        if (value == null)
        {
            Faculty = "";
            return;
        }

        ClassNames.Clear();
        foreach (string name in service.Catalog.ClassesOf(value))
        {
            ClassNames.Add(name);
        }

        if (!ClassNames.Contains(ClassName))
        {
            ClassName = "";
        }
    }

    partial void OnClassNameChanged(string value)
    {
        if (value == null)
        {
            ClassName = "";
        }
    }

    private void ResetForm()
    {
        StudentId = "";
        FullName = "";
        BirthDate = null;
        Gender = Gender.Male;
        NationalId = "";
        Email = "";
        Phone = "";
        Address = "";
        Faculty = "";
        ClassName = "";
        Status = Status.Studying;
        Gpa = "";
    }


    [RelayCommand]
    private void Add()
    {
        try
        {
            StartTimer();
            service.Add(BuildStudent());
            StopTimer();
            AfterWrite();
        }
        catch (Exception ex)
        {
            StopTimer();
            if (!ShowWriteError(ex))
            {
                throw;
            }
        }
    }

    // Được MainWindow gọi mỗi khi vùng chọn của bảng đổi
    public void SetSelectedStudents(List<Student> students)
    {
        selectedStudents = students;
        if (students.Count > 1)
        {
            Message = $"Đã chọn {students.Count} sinh viên. Bấm Xóa để xóa tất cả các dòng đã chọn.";
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (selectedStudents.Count == 0)
        {
            Info("Hãy chọn một hoặc nhiều sinh viên trong bảng để xóa.");
            return;
        }

        string question = $"Xóa {selectedStudents.Count} sinh viên đã chọn?";
        if (selectedStudents.Count == 1)
        {
            question = $"Xóa sinh viên {selectedStudents[0].StudentId} - {selectedStudents[0].FullName}?";
        }
        if (!Confirm(question))
        {
            return;
        }

        try
        {
            StartTimer();
            service.DeleteMany(selectedStudents);
            StopTimer();
            AfterWrite();
        }
        catch (Exception ex)
        {
            StopTimer();
            if (!ShowWriteError(ex))
            {
                throw;
            }
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedItem = null;
        SetErrors(new Dictionary<string, string>());
        ResetForm();
    }

    private Student BuildStudent()
    {
        // Điểm không đọc được thì để NaN, kiểm tra dữ liệu sẽ báo lỗi "Điểm TB phải lớn hơn 0"
        double gpaValue = double.NaN;
        double parsed;
        if (TryParseGpa(Gpa, out parsed))
        {
            gpaValue = parsed;
        }

        // Chưa chọn ngày sinh thì để ngày mặc định, Validator sẽ báo lỗi tuổi
        DateTime birth = default(DateTime);
        if (BirthDate != null)
        {
            birth = BirthDate.Value;
        }

        Student student = new Student
        {
            StudentId = StudentId,
            FullName = FullName,
            BirthDate = birth,
            Gender = Gender,
            NationalId = NationalId,
            Email = Email,
            Phone = Phone,
            Address = Address,
            Faculty = Faculty,
            ClassName = ClassName,
            Status = Status,
            Gpa = gpaValue
        };
        return student;
    }



    // 0 = tab Hồ sơ, 1 = tab Cấu trúc cây
    [ObservableProperty] private int selectedTabIndex;

    // Ctrl+S: đang thêm mới thì Thêm, đang sửa thì Cập nhật
    [RelayCommand]
    private void Save()
    {
        if (IsNew)
        {
            Add();
            return;
        }

        try
        {
            StartTimer();
            service.Update(BuildStudent());
            StopTimer();
            AfterWrite();
        }
        catch (Exception ex)
        {
            StopTimer();
            if (!ShowWriteError(ex))
            {
                throw;
            }
        }
    }

    // Esc: đóng phần Nâng cao, bỏ chọn và xóa form
    [RelayCommand]
    private void Escape()
    {
        Clear();
    }

    [RelayCommand]
    private void ShowProfileTab()
    {
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    private void ShowTreeTab()
    {
        SelectedTabIndex = 1;
    }

    [RelayCommand]
    private void Exit()
    {
        Application.Current.Shutdown();
    }

    // F1: bảng liệt kê các phím tắt
    [RelayCommand]
    private void ShowShortcuts()
    {
        Info("Ctrl+N: Làm mới form để thêm mới\n" +
             "Ctrl+S: Lưu (thêm mới, hoặc cập nhật nếu đang chọn một sinh viên)\n" +
             "Ctrl+D hoặc Delete (khi bảng có focus): Xóa các dòng đang chọn\n" +
             "Shift + bấm, Ctrl + bấm, Ctrl+A: Chọn nhiều dòng trong bảng\n" +
             "F5: Bỏ lọc, hiện tất cả\n" +
             "Ctrl+F hoặc F3: Đến ô tìm theo mã SV (Enter để tìm)\n" +
             "Esc: Bỏ chọn, xóa form\n" +
             "Ctrl+1 / Ctrl+2: Chuyển sang Hồ sơ / Cấu trúc cây\n" +
             "Ctrl + lăn chuột (trên cây): Phóng to / thu nhỏ\n" +
             "F4: Ẩn/hiện panel chi tiết\n" +
             "Ctrl+I / Ctrl+E: Nhập / Xuất Excel\n" +
             "F1: Bảng phím tắt này\n" +
             "Ctrl+Q hoặc Alt+F4: Thoát");
    }


    private const string ExcelFilter = "Excel (*.xlsx)|*.xlsx";

    // Xuất: file dữ liệu luôn được lưu sau mỗi thao tác nên chỉ cần chép ra chỗ người dùng chọn
    [RelayCommand]
    private void ExportExcel()
    {
        Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
        dialog.Filter = ExcelFilter;
        dialog.FileName = "HoSoSinhVien.xlsx";
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.Copy(dataPath, dialog.FileName, true);
            Message = $"Đã xuất ra {dialog.FileName}";
            Info($"Đã xuất {service.Tree.Count} sinh viên ra file:\n{dialog.FileName}");
        }
        catch (IOException ex)
        {
            Info("Không xuất được file (file đích có đang mở không?).\n" + ex.Message);
        }
    }

    // Nhập: đọc file được chọn rồi gộp vào dữ liệu hiện có (bỏ qua SV trùng hoặc sai)
    [RelayCommand]
    private void ImportExcel()
    {
        Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
        dialog.Filter = ExcelFilter;
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ExcelStudentRepository fileToImport = new ExcelStudentRepository(dialog.FileName);
            List<Student> students = fileToImport.Load();

            List<string> errors = new List<string>();
            StartTimer();
            int added = service.Import(students, errors);
            StopTimer();

            AfterWrite();

            string report = $"Đã nhập {added} sinh viên.";
            if (errors.Count > 0)
            {
                int maxShown = 15;
                report += $"\nBỏ qua {errors.Count} sinh viên:";
                for (int i = 0; i < errors.Count && i < maxShown; i++)
                {
                    report += "\n" + errors[i];
                }

                if (errors.Count > maxShown)
                {
                    report += $"\n... và {errors.Count - maxShown} sinh viên khác.";
                }
            }
            Info(report);
        }
        catch (StudentException ex)
        {
            Info($"File nhập bị lỗi: {ex.Message}");
        }
        catch (IOException ex)
        {
            Info("Không nhập được file (file có đang mở trong Excel không?).\n" + ex.Message);
        }
        catch (Exception ex)
        {
            Info("Không đọc được file Excel này. Hãy chọn file .xlsx đúng định dạng.\n" + ex.Message);
        }
    }


    [ObservableProperty] private GridLength detailWidth = new GridLength(320);
    private double lastDetailWidth = 320;

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
        if (DetailWidth.Value == 0)
        {
            ToggleDetailPane();
        }
    }



    [RelayCommand]
    private void CopyId()
    {
        if (SelectedItem != null)
        {
            Clipboard.SetText(SelectedItem.StudentId);
        }
    }


    [RelayCommand]
    private void Search()
    {
        StartTimer();
        string id = SearchId.Trim();
        Student? student = service.Find(id);
        StopTimer();

        if (student == null)
        {
            int visited = Tree.ShowPath(id);
            Info($"Không tìm thấy mã sinh viên này (đã ghé qua {visited} nút trên cây, xem ở tab Cấu trúc cây).");
            return;
        }

        List<Student> result = new List<Student>();
        result.Add(student);
        ShowList(result, $"Tìm theo mã '{id}'");
        SelectedItem = student;

        int steps = Tree.ShowPath(id);
        Message = $"Tìm thấy sau {steps} bước (cây có {service.Tree.Count} sinh viên, cao {service.Tree.Height} tầng).";
    }

    [RelayCommand]
    private void Filter()
    {
        Status? status = null;
        if (FilterStatusIndex > 0)
        {
            status = Enum.GetValues<Status>()[FilterStatusIndex - 1];
        }

        Grade? grade = null;
        if (FilterGradeIndex > 0)
        {
            grade = Enum.GetValues<Grade>()[FilterGradeIndex - 1];
        }

        string? classFilter = null;
        if (FilterClassIndex > 0)
        {
            classFilter = ClassOptions[FilterClassIndex];
        }

        StartTimer();
        List<Student> result = service.Filter(classFilter, status, grade);
        StopTimer();

        ShowList(result, "Lọc theo các tiêu chí đã chọn");
    }

    [RelayCommand]
    private void TopN()
    {
        int n;
        if (!int.TryParse(TopCount.Trim(), out n) || n <= 0)
        {
            Info("Top N phải là số nguyên lớn hơn 0.");
            return;
        }

        StartTimer();
        List<Student> result = service.TopN(n);
        StopTimer();

        ShowList(result, $"{n} sinh viên điểm cao nhất");
    }

    [RelayCommand]
    private void ShowAll()
    {
        
        CollectionViewSource.GetDefaultView(Items).SortDescriptions.Clear();

        FilterStatusIndex = 0;
        FilterGradeIndex = 0;
        FilterClassIndex = 0;

        StartTimer();
        List<Student> result = service.GetAll();
        StopTimer();

        ShowList(result, "Tất cả sinh viên");
    }


    private static bool TryParseGpa(string text, out double value)
    {
        string normalized = text.Replace(',', '.');
        NumberStyles styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

        if (!double.TryParse(normalized, styles, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        return double.IsFinite(value);
    }

    private void ShowList(List<Student> list, string description)
    {
        Student? selected = SelectedItem;

        isLoadingTable = true;
        Items.Clear();
        foreach (Student student in list)
        {
            Items.Add(student);
        }

        if (selected != null && Items.Contains(selected))
        {
            SelectedItem = selected;
        }
        isLoadingTable = false;

        int count = service.Tree.Count;
        double log = 0;
        if (count > 0)
        {
            log = Math.Log2(count);
        }

        TotalCount = count;
        VisibleCount = Items.Count;
        TreeHeight = service.Tree.Height;
        Log2Text = log.ToString("F2");
        RotationCount = service.Tree.RotationCount;
        LastTimeText = $"{lastMs:F3} ms";
        Message = $"Hiển thị {Items.Count} kết quả.";


        //// Đường dài nhất từ gốc xuống lá: ở mỗi nút đi về phía con cao hơn
        //string deepestPath = "";
        //AvlNode? pathNode = service.Tree.Root;
        //while (pathNode != null)
        //{
        //    if (deepestPath != "")
        //    {
        //        deepestPath += " -> ";
        //    }
        //    deepestPath += pathNode.Data.StudentId;

        //    if (AvlNode.GetHeight(pathNode.Left) >= AvlNode.GetHeight(pathNode.Right))
        //    {
        //        pathNode = pathNode.Left;
        //    }
        //    else
        //    {
        //        pathNode = pathNode.Right;
        //    }
        //}

        double maxHeight = 1.44 * Math.Log2(count + 2);
        int lowerPower = (int)Math.Floor(log);
        long lowerValue = 1L << lowerPower;
        long upperValue = 1L << (lowerPower + 1);

        TotalTip = $"Tổng SV = {count}\nLà số nút của cây, mỗi sinh viên một nút. Chỉ đổi khi thêm, xóa, nhập.";

        VisibleTip = $"Đang hiển thị = {Items.Count} / {count}\nBảng đang hiện kết quả của: {description}.";

        HeightTip = $"h = {service.Tree.Height}\n" +
                    //$"Đường dài nhất từ gốc xuống lá: {deepestPath}\n" +
                    $"Nên tìm một sinh viên mất tối đa {service.Tree.Height} bước. Mức tối đa của AVL: {maxHeight:F2}.";

        Log2Tip = $"log2(n) = log2({count}) = {log:F2}\n" +
                  $"Vì 2^{lowerPower} = {lowerValue} <= {count} < 2^{lowerPower + 1} = {upperValue}.\n" +
                  "Là chiều cao lý tưởng của cây cân bằng hoàn hảo, h càng gần số này càng tốt.";

        RotationTip = $"Số lần xoay = {service.Tree.RotationCount}\nMỗi lần cây lệch quá 1 tầng thì xoay một lần. Đếm từ lúc mở app.";

        TimeTip = $"Thao tác gần nhất = {lastMs:F3} ms\nThêm, sửa, xóa có ghi file Excel nên lâu hơn tìm kiếm.";
        Tree.Highlight(list);
    }

    private void AfterWrite()
    {
        Clear();
        CollectionViewSource.GetDefaultView(Items).SortDescriptions.Clear();
        ShowList(service.GetAll(), "Tất cả sinh viên");
        Message = "Đã lưu thay đổi vào file Excel.";
    }

    private void StartTimer()
    {
        watch = Stopwatch.StartNew();
    }

    private void StopTimer()
    {
        watch.Stop();
        lastMs = watch.Elapsed.TotalMilliseconds;
    }

    // Báo lỗi khi ghi thất bại. Trả về false nếu là lỗi lạ (để chương trình báo lỗi chung).
    private bool ShowWriteError(Exception ex)
    {
        if (ex is StudentException)
        {
            StudentException studentError = (StudentException)ex;

            SetErrors(studentError.Errors);
            if (studentError.IsDuplicate)
            {
                Info(studentError.Message);
            }
            return true;
        }

        if (ex is IOException)
        {
            Info("Không lưu được file Excel (file có đang mở trong Excel không?). Thao tác đã được hoàn tác.\n" + ex.Message);
            return true;
        }

        if (ex is KeyNotFoundException)
        {
            Info(ex.Message);
            return true;
        }

        return false;
    }

    private static void Info(string message)
    {
        MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static bool Confirm(string message)
    {
        MessageBoxResult answer = MessageBox.Show(message, "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return answer == MessageBoxResult.Yes;
    }


    private Dictionary<string, string> errors = new Dictionary<string, string>();

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors
    {
        get { return errors.Count > 0; }
    }

    public IEnumerable GetErrors(string? propertyName)
    {
        List<string> result = new List<string>();
        string? error;
        if (propertyName != null && errors.TryGetValue(propertyName, out error))
        {
            result.Add(error);
        }
        return result;
    }

    // Lấy lỗi của một trường để hiện cạnh ô nhập: {Binding [StudentId]}.
    public string this[string field]
    {
        get
        {
            string? error;
            if (errors.TryGetValue(field, out error))
            {
                return error;
            }
            return "";
        }
    }

    private void SetErrors(Dictionary<string, string> newErrors)
    {
        List<string> fields = new List<string>(errors.Keys);
        foreach (string field in newErrors.Keys)
        {
            if (!fields.Contains(field))
            {
                fields.Add(field);
            }
        }

        errors = newErrors;
        if (ErrorsChanged != null)
        {
            foreach (string field in fields)
            {
                ErrorsChanged(this, new DataErrorsChangedEventArgs(field));
            }
        }
        OnPropertyChanged("Item[]");
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != null && errors.ContainsKey(e.PropertyName))
        {
            Dictionary<string, string> remaining = new Dictionary<string, string>(errors);
            remaining.Remove(e.PropertyName);
            SetErrors(remaining);
        }
    }
}

