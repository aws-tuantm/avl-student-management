using System.Globalization;
using AVLStudentManagement.Core.Models;
using ClosedXML.Excel;

namespace AVLStudentManagement.Core.Data;

/// <summary>Đọc/ghi hồ sơ sinh viên trong file .xlsx (sheet SinhVien và DanhMuc).</summary>
public sealed class ExcelStudentRepository : IStudentRepository
{
    // Tên sheet và tiêu đề cột trong file Excel giữ tiếng Việt để người dùng mở Excel đọc được.
    // Đây là định dạng dữ liệu, không phải tên trong code. Chỉ khai báo ở đây, nơi khác dùng tên tiếng Anh.
    private const string StudentSheet = "SinhVien";
    private const string CatalogSheet = "DanhMuc";

    private const string IdColumn = "MaSV";
    private const string FullNameColumn = "HoTen";
    private const string BirthDateColumn = "NgaySinh";
    private const string GenderColumn = "GioiTinh";
    private const string NationalIdColumn = "CCCD";
    private const string EmailColumn = "Email";
    private const string PhoneColumn = "SoDienThoai";
    private const string AddressColumn = "DiaChi";
    private const string ClassColumn = "Lop";
    private const string FacultyColumn = "Khoa";
    private const string StatusColumn = "TrangThai";
    private const string GpaColumn = "DiemTB";
    private const string CreatedAtColumn = "NgayTao";
    private const string UpdatedAtColumn = "NgayCapNhat";

    private static readonly string[] Headers =
    {
        IdColumn, FullNameColumn, BirthDateColumn, GenderColumn, NationalIdColumn, EmailColumn, PhoneColumn,
        AddressColumn, ClassColumn, FacultyColumn, StatusColumn, GpaColumn, CreatedAtColumn, UpdatedAtColumn,
    };

    private readonly string path;
    private Catalog catalog = new(); // giữ lại để ghi trả khi Save

    /// <summary>Số dòng thật trong file của từng SV ở lần Load gần nhất (bỏ qua dòng trống).</summary>
    public IReadOnlyList<int>? RealRows { get; private set; }

    public ExcelStudentRepository(string path)
    {
        this.path = path;
    }

    public (List<Student> Students, Catalog Catalog) Load()
    {
        if (!File.Exists(path)) Save(Array.Empty<Student>()); // chưa có file thì tạo mới

        // FileShare.ReadWrite để vẫn đọc được khi file đang mở trong Excel
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);

        if (!workbook.TryGetWorksheet(StudentSheet, out var sheet))
            throw new DataFormatException(1, StudentSheet, "Không tìm thấy sheet.");

        // Đọc cột theo tên header nên đổi thứ tự cột vẫn đúng
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed()) columns[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        foreach (var header in Headers)
            if (!columns.ContainsKey(header)) throw new DataFormatException(1, header, "Thiếu cột.");

        var students = new List<Student>();
        var rows = new List<int>();
        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            if (sheet.Row(row).IsEmpty()) continue;
            students.Add(ReadRow(sheet, row, columns));
            rows.Add(row);
        }

        catalog = ReadCatalog(workbook);
        RealRows = rows;
        return (students, catalog);
    }

    public void Save(IEnumerable<Student> students)
    {
        var tempPath = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using (var workbook = new XLWorkbook())
        {
            WriteStudents(workbook.AddWorksheet(StudentSheet), students);
            WriteCatalog(workbook.AddWorksheet(CatalogSheet));
            using var file = File.Create(tempPath); // SaveAs(đường dẫn) không nhận đuôi .tmp nên ghi qua stream
            workbook.SaveAs(file);
        }

        // Ghi ra .tmp trước, rồi thay vào file chính và giữ bản .bak
        if (File.Exists(path)) File.Replace(tempPath, path, path + ".bak");
        else File.Move(tempPath, path);
    }

    // ---------- Đọc ----------

    private static Student ReadRow(IXLWorksheet sheet, int row, Dictionary<string, int> columns)
    {
        string Text(string name) => sheet.Cell(row, columns[name]).GetString().Trim();

        return new Student
        {
            StudentId = Text(IdColumn),
            FullName = Text(FullNameColumn),
            BirthDate = ReadDate(sheet.Cell(row, columns[BirthDateColumn]), row, BirthDateColumn)
                ?? throw new DataFormatException(row, BirthDateColumn, "Thiếu ngày sinh."),
            Gender = ParseEnum<Gender>(Text(GenderColumn), EnumDisplay.ToDisplay, LegacyGender, row, GenderColumn),
            NationalId = Text(NationalIdColumn),
            Email = Text(EmailColumn),
            Phone = Text(PhoneColumn),
            Address = Text(AddressColumn),
            ClassName = Text(ClassColumn),
            Faculty = Text(FacultyColumn),
            Status = ParseEnum<Status>(Text(StatusColumn), EnumDisplay.ToDisplay, LegacyStatus, row, StatusColumn),
            Gpa = ReadDouble(sheet.Cell(row, columns[GpaColumn]), row, GpaColumn),
            // Hai cột audit có thể để trống thì lấy giờ hiện tại
            CreatedAt = ReadDate(sheet.Cell(row, columns[CreatedAtColumn]), row, CreatedAtColumn) ?? DateTime.Now,
            UpdatedAt = ReadDate(sheet.Cell(row, columns[UpdatedAtColumn]), row, UpdatedAtColumn) ?? DateTime.Now,
        };
    }

    private static DateTime? ReadDate(IXLCell cell, int row, string column)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();
        if (DateTime.TryParse(cell.GetString(), new CultureInfo("vi-VN"), DateTimeStyles.None, out var date)) return date;
        throw new DataFormatException(row, column, $"'{cell.GetString()}' không phải ngày hợp lệ.");
    }

    private static double ReadDouble(IXLCell cell, int row, string column)
    {
        if (cell.DataType == XLDataType.Number) return cell.GetDouble();
        if (double.TryParse(cell.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
        throw new DataFormatException(row, column, $"'{cell.GetString()}' không phải số.");
    }

    // Tên cũ của enum trong file Excel (file cũ vẫn đọc được, Status vẫn được ghi bằng tên cũ)
    private static string LegacyGender(Gender value) => value switch
    {
        Gender.Male => "Nam",
        Gender.Female => "Nu",
        _ => "Khac",
    };

    private static string LegacyStatus(Status value) => value switch
    {
        Status.Studying => "DangHoc",
        Status.OnLeave => "BaoLuu",
        Status.Graduated => "DaTotNghiep",
        _ => "ThoiHoc",
    };

    // Chấp nhận tên enum mới (Studying), tên cũ (DangHoc) hoặc nhãn tiếng Việt (Đang học)
    private static T ParseEnum<T>(string text, Func<T, string> display, Func<T, string> legacy, int row, string column)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
            if (text.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase) ||
                text.Equals(display(value), StringComparison.OrdinalIgnoreCase) ||
                text.Equals(legacy(value), StringComparison.OrdinalIgnoreCase))
                return value;
        throw new DataFormatException(row, column, $"Giá trị '{text}' không hợp lệ.");
    }

    // Sheet DanhMuc: cột A = Khoa, cột B = Lop, dòng 1 là header
    private static Catalog ReadCatalog(XLWorkbook workbook)
    {
        var result = new Catalog();
        if (!workbook.TryGetWorksheet(CatalogSheet, out var sheet)) return result;

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            string faculty = sheet.Cell(row, 1).GetString().Trim();
            string className = sheet.Cell(row, 2).GetString().Trim();
            if (faculty != "" && className != "") result.Add(faculty, className);
        }
        return result;
    }

    // ---------- Ghi ----------

    private static void WriteStudents(IXLWorksheet sheet, IEnumerable<Student> students)
    {
        for (int i = 0; i < Headers.Length; i++) sheet.Cell(1, i + 1).Value = Headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        int row = 2;
        foreach (var student in students)
        {
            sheet.Cell(row, 1).Value = student.StudentId;
            sheet.Cell(row, 2).Value = student.FullName;
            sheet.Cell(row, 3).Value = student.BirthDate;
            sheet.Cell(row, 4).Value = student.Gender.ToDisplay();
            sheet.Cell(row, 5).Value = student.NationalId;
            sheet.Cell(row, 6).Value = student.Email;
            sheet.Cell(row, 7).Value = student.Phone;
            sheet.Cell(row, 8).Value = student.Address;
            sheet.Cell(row, 9).Value = student.ClassName;
            sheet.Cell(row, 10).Value = student.Faculty;
            sheet.Cell(row, 11).Value = LegacyStatus(student.Status);
            sheet.Cell(row, 12).Value = student.Gpa;
            sheet.Cell(row, 13).Value = student.CreatedAt;
            sheet.Cell(row, 14).Value = student.UpdatedAt;
            row++;
        }

        sheet.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.Column(12).Style.NumberFormat.Format = "0.00";
        sheet.Column(13).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
        sheet.Column(14).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
        sheet.Columns().AdjustToContents();
    }

    private void WriteCatalog(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = FacultyColumn;
        sheet.Cell(1, 2).Value = ClassColumn;
        sheet.Row(1).Style.Font.Bold = true;

        int row = 2;
        foreach (var faculty in catalog.Faculties)
            foreach (var className in catalog.ClassesOf(faculty))
            {
                sheet.Cell(row, 1).Value = faculty;
                sheet.Cell(row, 2).Value = className;
                row++;
            }
        sheet.Columns().AdjustToContents();
    }
}
