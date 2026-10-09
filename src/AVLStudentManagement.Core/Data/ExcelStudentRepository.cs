using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Services;
using ClosedXML.Excel;
using System.Globalization;

namespace AVLStudentManagement.Core.Data;

public class ExcelStudentRepository : IStudentRepository
{
    // Tên sheet và cột giữ tiếng Việt để mở Excel đọc được.
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

    private static readonly string[] Headers = new string[]
    {
        IdColumn,
        FullNameColumn,
        BirthDateColumn,
        GenderColumn,
        NationalIdColumn,
        EmailColumn,
        PhoneColumn,
        AddressColumn,
        ClassColumn,
        FacultyColumn,
        StatusColumn,
        GpaColumn,
        CreatedAtColumn,
        UpdatedAtColumn
    };

    private readonly string path;

    public Catalog Catalog { get; private set; } = new Catalog();

    public List<int>? RealRows { get; private set; }

    public ExcelStudentRepository(string path)
    {
        this.path = path;
    }

    public List<Student> Load()
    {
        if (!File.Exists(path))
        {
            Save(new List<Student>());
        }

        // FileShare.ReadWrite để vẫn đọc được khi file đang mở trong Excel
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using XLWorkbook workbook = new XLWorkbook(stream);

        IXLWorksheet sheet;
        if (!workbook.TryGetWorksheet(StudentSheet, out sheet))
        {
            throw new StudentException(1, StudentSheet, "Không tìm thấy sheet.");
        }

        // Đọc cột theo tên tiêu đề nên đổi thứ tự cột vẫn đúng
        Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (IXLCell cell in sheet.Row(1).CellsUsed())
        {
            columns[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        }

        foreach (string header in Headers)
        {
            if (!columns.ContainsKey(header))
            {
                throw new StudentException(1, header, "Thiếu cột.");
            }
        }

        List<Student> students = new List<Student>();
        List<int> rows = new List<int>();

        int lastRow = 1;
        IXLRow? lastUsedRow = sheet.LastRowUsed();
        if (lastUsedRow != null)
        {
            lastRow = lastUsedRow.RowNumber();
        }

        for (int row = 2; row <= lastRow; row++)
        {
            if (sheet.Row(row).IsEmpty())
            {
                continue;
            }
            students.Add(ReadRow(sheet, row, columns));
            rows.Add(row);
        }

        Catalog = ReadCatalog(workbook);
        RealRows = rows;
        return students;
    }

    public void Save(List<Student> students)
    {
        string tempPath = path + ".tmp";
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);

        using (XLWorkbook workbook = new XLWorkbook())
        {
            WriteStudents(workbook.AddWorksheet(StudentSheet), students);
            WriteCatalog(workbook.AddWorksheet(CatalogSheet));

            using FileStream file = File.Create(tempPath);
            workbook.SaveAs(file);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, path + ".bak");
        }
        else
        {
            File.Move(tempPath, path);
        }
    }


    private static Student ReadRow(IXLWorksheet sheet, int row, Dictionary<string, int> columns)
    {
        DateTime? birthDate = ReadDate(sheet.Cell(row, columns[BirthDateColumn]), row, BirthDateColumn);
        if (birthDate == null)
        {
            throw new StudentException(row, BirthDateColumn, "Thiếu ngày sinh.");
        }

        DateTime? createdAt = ReadDate(sheet.Cell(row, columns[CreatedAtColumn]), row, CreatedAtColumn);
        if (createdAt == null)
        {
            createdAt = DateTime.Now;
        }

        DateTime? updatedAt = ReadDate(sheet.Cell(row, columns[UpdatedAtColumn]), row, UpdatedAtColumn);
        if (updatedAt == null)
        {
            updatedAt = DateTime.Now;
        }

        Student student = new Student
        {
            StudentId = ReadText(sheet, row, columns, IdColumn),
            FullName = ReadText(sheet, row, columns, FullNameColumn),
            BirthDate = birthDate.Value,
            Gender = ReadGender(ReadText(sheet, row, columns, GenderColumn), row),
            NationalId = ReadText(sheet, row, columns, NationalIdColumn),
            Email = ReadText(sheet, row, columns, EmailColumn),
            Phone = ReadText(sheet, row, columns, PhoneColumn),
            Address = ReadText(sheet, row, columns, AddressColumn),
            ClassName = ReadText(sheet, row, columns, ClassColumn),
            Faculty = ReadText(sheet, row, columns, FacultyColumn),
            Status = ReadStatus(ReadText(sheet, row, columns, StatusColumn), row),
            Gpa = ReadDouble(sheet.Cell(row, columns[GpaColumn]), row, GpaColumn),
            CreatedAt = createdAt.Value,
            UpdatedAt = updatedAt.Value
        };
        return student;
    }

    private static string ReadText(IXLWorksheet sheet, int row, Dictionary<string, int> columns, string columnName)
    {
        return sheet.Cell(row, columns[columnName]).GetString().Trim();
    }

    private static DateTime? ReadDate(IXLCell cell, int row, string column)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime();
        }

        DateTime date;
        if (DateTime.TryParse(cell.GetString(), new CultureInfo("vi-VN"), DateTimeStyles.None, out date))
        {
            return date;
        }

        throw new StudentException(row, column, $"'{cell.GetString()}' không phải ngày hợp lệ.");
    }

    private static double ReadDouble(IXLCell cell, int row, string column)
    {
        if (cell.DataType == XLDataType.Number)
        {
            return cell.GetDouble();
        }

        double value;
        if (double.TryParse(cell.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return value;
        }

        throw new StudentException(row, column, $"'{cell.GetString()}' không phải số.");
    }

    private static Gender ReadGender(string text, int row)
    {
        foreach (Gender gender in Enum.GetValues<Gender>())
        {
            if (text.Equals(gender.ToString(), StringComparison.OrdinalIgnoreCase) ||
                text.Equals(gender.ToDisplay(), StringComparison.OrdinalIgnoreCase))
            {
                return gender;
            }
        }
        throw new StudentException(row, GenderColumn, $"Giá trị '{text}' không hợp lệ.");
    }

    private static Status ReadStatus(string text, int row)
    {
        foreach (Status status in Enum.GetValues<Status>())
        {
            if (text.Equals(status.ToString(), StringComparison.OrdinalIgnoreCase) ||
                text.Equals(status.ToDisplay(), StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }
        }
        throw new StudentException(row, StatusColumn, $"Giá trị '{text}' không hợp lệ.");
    }

    private static Catalog ReadCatalog(XLWorkbook workbook)
    {
        Catalog result = new Catalog();

        IXLWorksheet sheet;
        if (!workbook.TryGetWorksheet(CatalogSheet, out sheet))
        {
            return result;
        }

        int lastRow = 1;
        IXLRow? lastUsedRow = sheet.LastRowUsed();
        if (lastUsedRow != null)
        {
            lastRow = lastUsedRow.RowNumber();
        }

        for (int row = 2; row <= lastRow; row++)
        {
            string faculty = sheet.Cell(row, 1).GetString().Trim();
            string className = sheet.Cell(row, 2).GetString().Trim();
            if (faculty != "" && className != "")
            {
                result.Add(faculty, className);
            }
        }
        return result;
    }


    private static void WriteStudents(IXLWorksheet sheet, List<Student> students)
    {
        for (int i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }
        sheet.Row(1).Style.Font.Bold = true;

        List<object[]> rows = new List<object[]>();
        foreach (Student student in students)
        {
            rows.Add(new object[]
            {
                student.StudentId,
                student.FullName,
                student.BirthDate,
                student.Gender.ToDisplay(),
                student.NationalId,
                student.Email,
                student.Phone,
                student.Address,
                student.ClassName,
                student.Faculty,
                student.Status.ToDisplay(),
                student.Gpa,
                student.CreatedAt,
                student.UpdatedAt
            });
        }
        sheet.Cell(2, 1).InsertData(rows);

        sheet.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.Column(12).Style.NumberFormat.Format = "0.00";
        sheet.Column(13).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
        sheet.Column(14).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
        double[] widths = { 12, 26, 12, 10, 15, 30, 13, 60, 11, 30, 14, 8, 20, 20 };
        for (int i = 0; i < widths.Length; i++)
        {
            sheet.Column(i + 1).Width = widths[i];
        }
    }

    private void WriteCatalog(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = FacultyColumn;
        sheet.Cell(1, 2).Value = ClassColumn;
        sheet.Row(1).Style.Font.Bold = true;

        int row = 2;
        foreach (string faculty in Catalog.Faculties)
        {
            foreach (string className in Catalog.ClassesOf(faculty))
            {
                sheet.Cell(row, 1).Value = faculty;
                sheet.Cell(row, 2).Value = className;
                row++;
            }
        }
        sheet.Columns().AdjustToContents();
    }
}
