using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Models;
using ClosedXML.Excel;

namespace AVLStudentManagement.Tests;

[TestClass]
public class ExcelStudentRepositoryTests
{
    private string dir = "";
    private string file = "";

    // Mỗi test một thư mục tạm riêng
    [TestInitialize]
    public void Init()
    {
        dir = Path.Combine(Path.GetTempPath(), "avl_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        file = Path.Combine(dir, "student.xlsx");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(dir, true); } catch { /* bỏ qua */ }
    }

    private static Student MakeStudent(string id = "00123456") => new()
    {
        StudentId = id,
        FullName = "Nguyễn Thị Thùy Dương",
        BirthDate = new DateTime(2003, 12, 31),
        Gender = Gender.Female,
        NationalId = "001203000123",
        Email = "duong@gmail.com",
        Phone = "0901234567",
        Address = "12 Lê Lợi, Quận 1, TP. Hồ Chí Minh",
        ClassName = "CNTT01",
        Faculty = "Công nghệ thông tin",
        Status = Status.Graduated,
        Gpa = 3.57,
        CreatedAt = new DateTime(2024, 1, 2, 3, 4, 5),
        UpdatedAt = new DateTime(2024, 6, 7, 8, 9, 10),
    };

    // Sửa file Excel đã ghi để tạo dữ liệu hỏng
    private void Edit(Action<IXLWorksheet> change)
    {
        using var workbook = new XLWorkbook(file);
        change(workbook.Worksheet("SinhVien"));
        workbook.Save();
    }

    [TestMethod]
    public void SaveThenLoad_DataIsCorrect()
    {
        var original = MakeStudent();
        new ExcelStudentRepository(file).Save(new[] { original, MakeStudent("20240002") with { NationalId = "001203000124", Email = "b@x.vn" } });

        var (list, _) = new ExcelStudentRepository(file).Load();

        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(original, list[0]); // record so sánh từng trường
    }

    [TestMethod]
    public void StudentId_LeadingZeros_And_Phone_ArePreserved()
    {
        new ExcelStudentRepository(file).Save(new[] { MakeStudent() });
        var student = new ExcelStudentRepository(file).Load().Students[0];

        Assert.AreEqual("00123456", student.StudentId);
        Assert.AreEqual("0901234567", student.Phone);
        Assert.AreEqual("001203000123", student.NationalId);
    }

    [TestMethod]
    public void VietnameseAccents_ArePreserved()
    {
        new ExcelStudentRepository(file).Save(new[] { MakeStudent() });
        var student = new ExcelStudentRepository(file).Load().Students[0];

        Assert.AreEqual("Nguyễn Thị Thùy Dương", student.FullName);
        Assert.AreEqual("12 Lê Lợi, Quận 1, TP. Hồ Chí Minh", student.Address);
        Assert.AreEqual("Công nghệ thông tin", student.Faculty);
        Assert.AreEqual(Gender.Female, student.Gender);
    }

    [TestMethod]
    public void MissingFile_LoadReturnsEmpty_AndCreatesFile()
    {
        var (list, catalog) = new ExcelStudentRepository(file).Load();

        Assert.AreEqual(0, list.Count);
        Assert.AreEqual(0, catalog.Faculties.Count());
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void MissingFile_SaveCreatesFile()
    {
        new ExcelStudentRepository(file).Save(new[] { MakeStudent() });

        Assert.IsTrue(File.Exists(file));
        Assert.IsFalse(File.Exists(file + ".bak")); // lần đầu chưa có bản sao
        Assert.IsFalse(File.Exists(file + ".tmp"));
    }

    [TestMethod]
    public void SecondSave_CreatesBakFile()
    {
        var repo = new ExcelStudentRepository(file);
        repo.Save(new[] { MakeStudent() });
        repo.Save(new[] { MakeStudent(), MakeStudent("20240002") with { NationalId = "001203000124", Email = "b@x.vn" } });

        Assert.IsTrue(File.Exists(file + ".bak"));
        // .bak là bản cũ (1 sinh viên), file chính là bản mới (2 sinh viên)
        Assert.AreEqual(2, repo.Load().Students.Count);
        var old = Path.Combine(dir, "old.xlsx");
        File.Copy(file + ".bak", old);
        Assert.AreEqual(1, new ExcelStudentRepository(old).Load().Students.Count);
    }

    [TestMethod]
    public void MissingRequiredColumn_ThrowsWithColumn()
    {
        new ExcelStudentRepository(file).Save(new[] { MakeStudent() });
        Edit(sheet => sheet.Cell(1, 5).Value = "OtherColumn"); // cột 5 là CCCD (header Excel)

        var ex = Assert.ThrowsExactly<DataFormatException>(() => new ExcelStudentRepository(file).Load());
        Assert.AreEqual("CCCD", ex.Column);
    }

    [TestMethod]
    public void BadBirthDate_ThrowsWithRow()
    {
        new ExcelStudentRepository(file).Save(new[] { MakeStudent(), MakeStudent("20240002") });
        Edit(sheet => sheet.Cell(3, 3).Value = "không phải ngày"); // dòng 3, cột BirthDate

        var ex = Assert.ThrowsExactly<DataFormatException>(() => new ExcelStudentRepository(file).Load());
        Assert.AreEqual(3, ex.Row);
        Assert.AreEqual("NgaySinh", ex.Column);
    }

    [TestMethod]
    public void BadGpa_ThrowsWithRow()
    {
        new ExcelStudentRepository(file).Save(new[] { MakeStudent() });
        Edit(sheet => sheet.Cell(2, 12).Value = "abc"); // dòng 2, cột Gpa

        var ex = Assert.ThrowsExactly<DataFormatException>(() => new ExcelStudentRepository(file).Load());
        Assert.AreEqual(2, ex.Row);
        Assert.AreEqual("DiemTB", ex.Column);
    }
}
