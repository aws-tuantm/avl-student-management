using System.Diagnostics;
using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Services;
using AVLStudentManagement.Core.Validation;

namespace AVLStudentManagement.Tests;

/// <summary>Chạy thật: Service + file Excel thật (CRUD rồi nạp lại), và đo tốc độ tìm kiếm trên cây.</summary>
[TestClass]
public class EndToEndTests
{
    private string dir = "";
    private string file = "";

    [TestInitialize]
    public void Init()
    {
        dir = Path.Combine(Path.GetTempPath(), "avl_e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        file = Path.Combine(dir, "db.xlsx");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(dir, true); } catch { /* bỏ qua */ }
    }

    private static Student Make(int n, double gpa = 3.0) => new()
    {
        StudentId = n.ToString("D8"),
        FullName = "Nguyễn Văn " + n,
        BirthDate = new DateTime(2004, 1, 1),
        Gender = Gender.Male,
        NationalId = n.ToString("D12"),
        Email = $"sv{n}@x.vn",
        Phone = "0912345678",
        Address = "Hà Nội",
        ClassName = "CNTT01",
        Faculty = "CNTT",
        Status = Status.Studying,
        Gpa = gpa,
    };

    // Mở service mới trên cùng file, giống như chạy lại app
    private StudentService NewService()
    {
        var service = new StudentService(new ExcelStudentRepository(file));
        service.Load();
        return service;
    }

    // Tạo file Excel có danh mục CNTT/CNTT01 để Validator cho qua
    private void CreateDbWithCatalog()
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var s = wb.AddWorksheet("SinhVien");
        string[] headers = { "MaSV", "HoTen", "NgaySinh", "GioiTinh", "CCCD", "Email", "SoDienThoai", "DiaChi", "Lop", "Khoa", "TrangThai", "DiemTB", "NgayTao", "NgayCapNhat" };
        for (int i = 0; i < headers.Length; i++) s.Cell(1, i + 1).Value = headers[i];
        var c = wb.AddWorksheet("DanhMuc");
        c.Cell(1, 1).Value = "Khoa"; c.Cell(1, 2).Value = "Lop";
        c.Cell(2, 1).Value = "CNTT"; c.Cell(2, 2).Value = "CNTT01";
        wb.SaveAs(file);
    }

    [TestMethod]
    public void Crud_PersistsInExcel_AndSurvivesReload()
    {
        CreateDbWithCatalog();
        var service = NewService();

        service.Add(Make(3, 2.5));
        service.Add(Make(1, 3.9));
        service.Add(Make(2, 3.1));
        service.Update(Make(2, 3.7) with { FullName = "  Trần   Thị B " });
        Assert.IsTrue(service.Delete(Make(3).StudentId));

        // Mở lại như lần chạy app sau: dữ liệu phải còn đúng trong file
        var reloaded = NewService();
        CollectionAssert.AreEqual(new[] { "00000001", "00000002" }, reloaded.GetAll().Select(s => s.StudentId).ToArray());
        Assert.AreEqual("Trần Thị B", reloaded.Find("00000002")!.FullName);
        Assert.AreEqual("00000001", reloaded.TopStudent()!.StudentId);
        Assert.AreEqual(3.7, reloaded.Find("00000002")!.Gpa);
        Assert.IsNull(reloaded.Find("00000003"));
    }

    [TestMethod]
    public void RangeDelete_AndImport_PersistInExcel()
    {
        CreateDbWithCatalog();
        var service = NewService();
        service.Import(Enumerable.Range(1, 20).Select(n => Make(n, n / 5.0)));

        Assert.AreEqual(5, service.DeleteByIdRange("00000006", "00000010"));
        var (added, skipped) = service.Import(new[] { Make(6), Make(7), Make(1) }); // 1 đã tồn tại

        var reloaded = NewService();
        Assert.AreEqual(17, reloaded.GetAll().Count());
        Assert.AreEqual(2, added);
        Assert.AreEqual(1, skipped);
        Assert.AreEqual(3, reloaded.FindByGpaRange(0.2, 0.6).Count); // n=1,2,3 -> 0.2,0.4,0.6
    }

    [TestMethod]
    public void DuplicateAndInvalid_AreRejected_NothingWritten()
    {
        CreateDbWithCatalog();
        var service = NewService();
        service.Add(Make(1));

        Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(Make(1)));
        Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(Make(2) with { NationalId = Make(1).NationalId }));
        Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(Make(3) with { Email = "SV1@X.VN" })); // email không phân biệt hoa thường
        Assert.ThrowsExactly<ValidationException>(() => service.Add(Make(4) with { Gpa = 4.5 }));
        Assert.ThrowsExactly<ValidationException>(() => service.Add(Make(5) with { StudentId = "abc" }));

        Assert.AreEqual(1, NewService().GetAll().Count());
    }

    [TestMethod]
    public void Search_100kStudents_IsLogarithmicallyFast()
    {
        var repo = new FakeRepository();
        repo.Catalog.Add("CNTT", "CNTT01");
        const int n = 100_000;
        var rng = new Random(1);
        repo.Data = Enumerable.Range(1, n).Select(i => Make(i, Math.Round(rng.NextDouble() * 4, 2))).OrderBy(_ => rng.Next()).ToList();
        var service = new StudentService(repo);

        var sw = Stopwatch.StartNew();
        service.Load();
        long loadMs = sw.ElapsedMilliseconds;

        Assert.AreEqual(n, service.Tree.Count);
        Assert.IsLessThanOrEqualTo(1.45 * Math.Log2(n + 2), service.Tree.Height);

        // 10.000 lần tìm theo mã
        sw.Restart();
        for (int i = 1; i <= 10_000; i++) Assert.IsNotNull(service.Find((i * 7 % n + 1).ToString("D8")));
        double findUs = sw.Elapsed.TotalMilliseconds * 1000 / 10_000;

        // Tìm khoảng mã 50 phần tử, khoảng điểm, Top 10, thủ khoa
        sw.Restart();
        var byId = service.FindByIdRange("00050000", "00050049");
        double rangeIdMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        var byGpa = service.FindByGpaRange(3.99, 4.0);
        double rangeGpaMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        var top = service.TopN(10);
        double topMs = sw.Elapsed.TotalMilliseconds;

        Assert.AreEqual(50, byId.Count);
        Assert.AreEqual(byGpa.Count, repo.Data.Count(s => s.Gpa >= 3.99 && s.Gpa <= 4.0));
        Assert.AreEqual(10, top.Count);
        Assert.AreEqual(repo.Data.Max(s => s.Gpa), service.TopStudent()!.Gpa);

        Console.WriteLine($"n={n}: Load {loadMs} ms, h={service.Tree.Height}, Find {findUs:F2} us/lần, " +
                          $"Range mã {rangeIdMs:F3} ms, Range điểm {rangeGpaMs:F3} ms ({byGpa.Count} kq), Top10 {topMs:F3} ms");
        Assert.IsLessThan(50, findUs);       // 1 lần tìm < 50 micro giây (thực tế ~1 us)
        Assert.IsLessThan(20, rangeIdMs);
    }

    [TestMethod]
    public void ExcelSave_5000Students_IsUsable()
    {
        CreateDbWithCatalog();
        var service = NewService();
        var sw = Stopwatch.StartNew();
        service.Import(Enumerable.Range(1, 5000).Select(i => Make(i)));
        long saveMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var reloaded = NewService();
        long loadMs = sw.ElapsedMilliseconds;

        Assert.AreEqual(5000, reloaded.Tree.Count);
        Console.WriteLine($"Excel 5000 SV: ghi {saveMs} ms, đọc+dựng cây {loadMs} ms");
    }
}
