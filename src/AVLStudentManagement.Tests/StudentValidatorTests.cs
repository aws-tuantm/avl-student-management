using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Validation;

namespace AVLStudentManagement.Tests;

[TestClass]
public class StudentValidatorTests
{
    // Danh mục mẫu: CNTT có lớp CNTT01, KT có lớp KT01
    private static Catalog MakeCatalog()
    {
        var catalog = new Catalog();
        catalog.Add("CNTT", "CNTT01");
        catalog.Add("KT", "KT01");
        return catalog;
    }

    // Sinh viên hợp lệ hoàn toàn, mỗi test sửa 1 trường rồi kiểm tra
    private static Student Ok() => new()
    {
        StudentId = "20240001",
        FullName = "Nguyễn Văn An",
        BirthDate = new DateTime(2004, 5, 20),
        Gender = Gender.Male,
        NationalId = "012345678901",
        Email = "an@gmail.com",
        Phone = "0912345678",
        Address = "Hà Nội",
        ClassName = "CNTT01",
        Faculty = "CNTT",
        Status = Status.Studying,
        Gpa = 3.0,
    };

    private static Dictionary<string, string> Run(Student student) => StudentValidator.Validate(student, MakeCatalog());

    // Chữ số Unicode (Ả Rập) không được tính là chữ số hợp lệ
    [TestMethod]
    public void StudentId_UnicodeDigits_Rejected()
    {
        var errors = StudentValidator.Validate(Ok() with { StudentId = "٢٠٢٤٠٠٠١" }, MakeCatalog());
        Assert.IsTrue(errors.ContainsKey("StudentId"));
    }

    [TestMethod]
    public void Valid_NoErrors() => Assert.AreEqual(0, Run(Ok()).Count);

    // ---- StudentId ----
    [TestMethod]
    public void StudentId_8Digits_Valid() => Assert.IsFalse(Run(Ok() with { StudentId = "12345678" }).ContainsKey("StudentId"));

    [TestMethod]
    public void StudentId_HasLetters_Invalid() => Assert.IsTrue(Run(Ok() with { StudentId = "2024abcd" }).ContainsKey("StudentId"));

    [TestMethod]
    public void StudentId_TooShort_Invalid() => Assert.IsTrue(Run(Ok() with { StudentId = "1234567" }).ContainsKey("StudentId"));

    // ---- FullName ----
    [TestMethod]
    public void FullName_Valid() => Assert.IsFalse(Run(Ok() with { FullName = "An" }).ContainsKey("FullName"));

    [TestMethod]
    public void FullName_TooShort_Invalid() => Assert.IsTrue(Run(Ok() with { FullName = "A" }).ContainsKey("FullName"));

    [TestMethod]
    public void FullName_TooLong_Invalid() => Assert.IsTrue(Run(Ok() with { FullName = new string('a', 101) }).ContainsKey("FullName"));

    // ---- BirthDate / tuổi ----
    [TestMethod]
    public void BirthDate_Age20_Valid() =>
        Assert.IsFalse(Run(Ok() with { BirthDate = DateTime.Today.AddYears(-20) }).ContainsKey("BirthDate"));

    [TestMethod]
    public void BirthDate_Exactly15_Valid() =>
        Assert.IsFalse(Run(Ok() with { BirthDate = DateTime.Today.AddYears(-15) }).ContainsKey("BirthDate"));

    [TestMethod]
    public void BirthDate_Under15_Invalid() =>
        Assert.IsTrue(Run(Ok() with { BirthDate = DateTime.Today.AddYears(-15).AddDays(1) }).ContainsKey("BirthDate"));

    [TestMethod]
    public void BirthDate_Exactly60_Valid() =>
        Assert.IsFalse(Run(Ok() with { BirthDate = DateTime.Today.AddYears(-60) }).ContainsKey("BirthDate"));

    [TestMethod]
    public void BirthDate_Over60_Invalid() =>
        Assert.IsTrue(Run(Ok() with { BirthDate = DateTime.Today.AddYears(-61) }).ContainsKey("BirthDate"));

    [TestMethod]
    public void BirthDate_Future_Invalid() =>
        Assert.IsTrue(Run(Ok() with { BirthDate = DateTime.Today.AddDays(1) }).ContainsKey("BirthDate"));

    // ---- NationalId ----
    [TestMethod]
    public void NationalId_12Digits_Valid() => Assert.IsFalse(Run(Ok() with { NationalId = "000000000001" }).ContainsKey("NationalId"));

    [TestMethod]
    public void NationalId_11Digits_Invalid() => Assert.IsTrue(Run(Ok() with { NationalId = "01234567890" }).ContainsKey("NationalId"));

    [TestMethod]
    public void NationalId_13Digits_Invalid() => Assert.IsTrue(Run(Ok() with { NationalId = "0123456789012" }).ContainsKey("NationalId"));

    [TestMethod]
    public void NationalId_HasLetters_Invalid() => Assert.IsTrue(Run(Ok() with { NationalId = "01234567890a" }).ContainsKey("NationalId"));

    // ---- Email ----
    [TestMethod]
    public void Email_Valid() => Assert.IsFalse(Run(Ok() with { Email = "a.b@x.edu.vn" }).ContainsKey("Email"));

    [TestMethod]
    public void Email_MissingAt_Invalid() => Assert.IsTrue(Run(Ok() with { Email = "abc.gmail.com" }).ContainsKey("Email"));

    [TestMethod]
    public void Email_MissingDomain_Invalid() => Assert.IsTrue(Run(Ok() with { Email = "a@gmail" }).ContainsKey("Email"));

    // ---- SĐT ----
    [TestMethod]
    public void Phone_Valid() => Assert.IsFalse(Run(Ok() with { Phone = "0987654321" }).ContainsKey("Phone"));

    [TestMethod]
    public void Phone_Empty_Valid() => Assert.IsFalse(Run(Ok() with { Phone = "" }).ContainsKey("Phone"));

    [TestMethod]
    public void Phone_NotStartingWithZero_Invalid() => Assert.IsTrue(Run(Ok() with { Phone = "1987654321" }).ContainsKey("Phone"));

    [TestMethod]
    public void Phone_9Digits_Invalid() => Assert.IsTrue(Run(Ok() with { Phone = "098765432" }).ContainsKey("Phone"));

    // ---- Lớp thuộc Faculty ----
    [TestMethod]
    public void ClassName_RightFaculty_Valid()
    {
        var e = Run(Ok() with { Faculty = "KT", ClassName = "KT01" });
        Assert.IsFalse(e.ContainsKey("ClassName"));
        Assert.IsFalse(e.ContainsKey("Faculty"));
    }

    [TestMethod]
    public void ClassName_WrongFaculty_Invalid() => Assert.IsTrue(Run(Ok() with { Faculty = "KT", ClassName = "CNTT01" }).ContainsKey("ClassName"));

    [TestMethod]
    public void Faculty_NotInCatalog_Invalid() => Assert.IsTrue(Run(Ok() with { Faculty = "YHOC" }).ContainsKey("Faculty"));

    [TestMethod]
    public void FacultyAndClass_IgnoresCase() =>
        Assert.AreEqual(0, Run(Ok() with { Faculty = "cntt", ClassName = "cntt01" }).Count);

    // ---- Gpa ----
    [TestMethod]
    public void Gpa_0_And_4_Valid()
    {
        Assert.IsFalse(Run(Ok() with { Gpa = 0 }).ContainsKey("Gpa"));
        Assert.IsFalse(Run(Ok() with { Gpa = 4 }).ContainsKey("Gpa"));
    }

    [TestMethod]
    public void Gpa_Negative_Invalid() => Assert.IsTrue(Run(Ok() with { Gpa = -0.01 }).ContainsKey("Gpa"));

    [TestMethod]
    public void Gpa_Above4_Invalid() => Assert.IsTrue(Run(Ok() with { Gpa = 4.01 }).ContainsKey("Gpa"));

    [TestMethod]
    public void Gpa_NaN_Invalid() => Assert.IsTrue(Run(Ok() with { Gpa = double.NaN }).ContainsKey("Gpa"));

    // ---- Xếp loại: ngưỡng biên ----
    [TestMethod]
    [DataRow(4.0, Grade.Excellent)]
    [DataRow(3.6, Grade.Excellent)]
    [DataRow(3.59, Grade.VeryGood)]
    [DataRow(3.2, Grade.VeryGood)]
    [DataRow(3.19, Grade.Good)]
    [DataRow(2.5, Grade.Good)]
    [DataRow(2.49, Grade.Average)]
    [DataRow(2.0, Grade.Average)]
    [DataRow(1.99, Grade.Weak)]
    [DataRow(1.0, Grade.Weak)]
    [DataRow(0.99, Grade.Poor)]
    [DataRow(0.0, Grade.Poor)]
    public void Grade_Boundaries(double gpa, Grade expected) =>
        Assert.AreEqual(expected, (Ok() with { Gpa = gpa }).Grade);
}
