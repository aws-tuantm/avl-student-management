using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Services;
using AVLStudentManagement.Core.Validation;

namespace AVLStudentManagement.Tests;

/// <summary>Repository giả: giữ danh sách trong bộ nhớ, bật FailOnSave để giả lập lỗi ghi file.</summary>
internal sealed class FakeRepository : IStudentRepository
{
    public List<Student> Data = new();
    public Catalog Catalog = new();
    public bool FailOnSave;
    public IReadOnlyList<int>? RealRows { get; set; } // số dòng thật, để test báo lỗi đúng dòng

    public (List<Student> Students, Catalog Catalog) Load() => (Data.ToList(), Catalog);

    public void Save(IEnumerable<Student> students)
    {
        if (FailOnSave) throw new IOException("Lỗi ghi file giả");
        Data = students.ToList();
    }
}

[TestClass]
public class StudentServiceTests
{
    private FakeRepository repo = null!;
    private StudentService service = null!;

    [TestInitialize]
    public void Init()
    {
        repo = new FakeRepository();
        repo.Catalog.Add("CNTT", "CNTT01");
        repo.Catalog.Add("KT", "KT01");
        service = new StudentService(repo);
        service.Load(); // nạp danh mục (danh sách SV đang rỗng)
    }

    // Sinh viên mẫu: NationalId và Email sinh từ StudentId để không trùng nhau
    private static Student MakeStudent(string id, double gpa = 3.0) => new()
    {
        StudentId = id,
        FullName = "Nguyễn Văn " + id,
        BirthDate = new DateTime(2004, 1, 1),
        Gender = Gender.Male,
        NationalId = "0000" + id,
        Email = id + "@x.vn",
        Phone = "0912345678",
        Address = "Hà Nội",
        ClassName = "CNTT01",
        Faculty = "CNTT",
        Status = Status.Studying,
        Gpa = gpa,
    };

    // Nạp sẵn vài sinh viên qua Load (không đi qua Add)
    private void Seed(params Student[] list)
    {
        repo.Data = list.ToList();
        service.Load();
    }

    private static string[] Ids(IEnumerable<Student> l) => l.Select(s => s.StudentId).ToArray();

    // ---------- Import ----------

    [TestMethod]
    public void Import_SkipsDuplicateAndInvalid_SavesOnce()
    {
        Seed(MakeStudent("20240001"));
        var bad = MakeStudent("20240009") with { Gpa = 9 }; // điểm sai
        var (added, errors) = service.Import(new[] { MakeStudent("20240001"), MakeStudent("20240002"), bad });

        Assert.AreEqual(1, added);
        Assert.HasCount(2, errors);
        StringAssert.Contains(errors[0], "StudentId");
        StringAssert.Contains(errors[1], "Điểm TB");
        CollectionAssert.AreEqual(new[] { "20240001", "20240002" }, Ids(repo.Data));
    }

    [TestMethod]
    public void Import_SaveFails_RollsBack()
    {
        repo.FailOnSave = true;
        Assert.ThrowsExactly<IOException>(() => service.Import(new[] { MakeStudent("20240002") }));
        Assert.AreEqual(0, service.Tree.Count);
    }

    // ---------- Load ----------

    [TestMethod]
    public void Load_ManyStudents_CorrectCountAndOrder()
    {
        Seed(MakeStudent("20240003"), MakeStudent("20240001"), MakeStudent("20240002"));

        CollectionAssert.AreEqual(new[] { "20240001", "20240002", "20240003" }, Ids(service.GetAll()));
        Assert.AreEqual(3, service.Tree.Count);
    }

    [TestMethod]
    public void Load_DuplicateId_ThrowsWithRow()
    {
        repo.Data = new() { MakeStudent("20240001"), MakeStudent("20240001") };
        var ex = Assert.ThrowsExactly<DataFormatException>(() => service.Load());
        Assert.AreEqual(3, ex.Row);
        Assert.AreEqual("StudentId", ex.Column);
    }


    // ---------- Hồi quy ----------

    [TestMethod]
    public void Update_IdWithSpaces_StillFound()
    {
        Seed(MakeStudent("20230001"));
        service.Update(MakeStudent("20230001", 3.5) with { StudentId = " 20230001 " });
        Assert.AreEqual(3.5, service.Find("20230001")!.Gpa);
    }

    [TestMethod]
    public void Add_Gpa_RoundsAwayFromZero()
    {
        service.Add(MakeStudent("20240001", 3.125)); // 3.125 làm tròn banker ra 3.12, kiểu toán ra 3.13
        Assert.AreEqual(3.13, service.Find("20240001")!.Gpa);
    }

    [TestMethod]
    public void Load_DuplicateId_ReportsRealRowWhenEmptyRowsExist()
    {
        repo.Data = new() { MakeStudent("20240001"), MakeStudent("20240001") };
        repo.RealRows = new[] { 2, 5 }; // giữa hai SV có dòng trống
        var ex = Assert.ThrowsExactly<DataFormatException>(() => service.Load());
        Assert.AreEqual(5, ex.Row);
    }

    [TestMethod]
    public void Load_ErrorMidway_ServiceKeepsOldData()
    {
        Seed(MakeStudent("20240001"));
        repo.Data = new() { MakeStudent("20240002"), MakeStudent("20240003"), MakeStudent("20240003") };
        Assert.ThrowsExactly<DataFormatException>(() => service.Load());

        CollectionAssert.AreEqual(new[] { "20240001" }, Ids(service.GetAll()));
    }
    // ---------- Add ----------

    [TestMethod]
    public void Add_Valid_FoundAndSaved()
    {
        service.Add(MakeStudent("20240001"));

        Assert.IsNotNull(service.Find("20240001"));
        Assert.AreEqual(1, repo.Data.Count);
    }

    [TestMethod]
    public void Add_DuplicateId_Rejected()
    {
        Seed(MakeStudent("20240001"));
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(MakeStudent("20240001") with { NationalId = "111111111111", Email = "k@x.vn" }));
        Assert.AreEqual("StudentId", ex.Field);
    }

    [TestMethod]
    public void Add_DuplicateNationalId_Rejected()
    {
        Seed(MakeStudent("20240001"));
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(MakeStudent("20240002") with { NationalId = "000020240001" }));
        Assert.AreEqual("NationalId", ex.Field);
    }

    [TestMethod]
    public void Add_DuplicateEmail_IgnoresCase_Rejected()
    {
        Seed(MakeStudent("20240001"));
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(MakeStudent("20240002") with { Email = "20240001@X.VN" }));
        Assert.AreEqual("Email", ex.Field);
    }

    [TestMethod]
    public void Add_InvalidStudent_ThrowsValidationException()
    {
        var ex = Assert.ThrowsExactly<ValidationException>(() => service.Add(MakeStudent("20240001") with { NationalId = "123" }));
        Assert.IsTrue(ex.Errors.ContainsKey("NationalId"));
        Assert.AreEqual(0, service.Tree.Count);
    }

    // ---------- Update ----------

    [TestMethod]
    public void Update_ChangeGpa_TopLowestTopNFindAreUpdated()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0), MakeStudent("20240003", 3.5));
        Assert.AreEqual("20240003", service.TopStudent()!.StudentId);
        Assert.AreEqual("20240001", service.LowestGpaStudent()!.StudentId);

        service.Update(MakeStudent("20240001", 3.9)); // thấp nhất lên cao nhất
        service.Update(MakeStudent("20240003", 1.0)); // cao nhất xuống thấp nhất

        Assert.AreEqual("20240001", service.TopStudent()!.StudentId);
        Assert.AreEqual("20240003", service.LowestGpaStudent()!.StudentId);
        CollectionAssert.AreEqual(new[] { "20240001", "20240002" }, Ids(service.TopN(2)));
        CollectionAssert.AreEqual(new[] { "20240002" }, Ids(service.FindByGpaRange(2.5, 3.5)));
        Assert.AreEqual(3, service.Tree.Count);
    }

    [TestMethod]
    public void Update_SameNationalIdAndEmail_NoDuplicateError()
    {
        Seed(MakeStudent("20240001"));
        service.Update(MakeStudent("20240001") with { FullName = "Tên Mới" });
        Assert.AreEqual("Tên Mới", service.Find("20240001")!.FullName);
    }

    [TestMethod]
    public void Update_NationalIdOfOtherStudent_Rejected()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"));
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Update(MakeStudent("20240001") with { NationalId = "000020240002" }));
        Assert.AreEqual("NationalId", ex.Field);
    }

    [TestMethod]
    public void Update_EmailOfOtherStudent_Rejected()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"));
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Update(MakeStudent("20240001") with { Email = "20240002@x.vn" }));
        Assert.AreEqual("Email", ex.Field);
    }

    [TestMethod]
    public void Update_NewNationalId_OldOneCanBeReused()
    {
        Seed(MakeStudent("20240001"));
        service.Update(MakeStudent("20240001") with { NationalId = "999999999999" });

        service.Add(MakeStudent("20240002") with { NationalId = "000020240001" }); // NationalId cũ giờ trống
        Assert.IsNotNull(service.Find("20240002"));
    }

    [TestMethod]
    public void Update_MissingId_ThrowsKeyNotFound()
    {
        Seed(MakeStudent("20240001"));
        Assert.ThrowsExactly<KeyNotFoundException>(() => service.Update(MakeStudent("20249999")));
    }

    [TestMethod]
    public void Update_KeepsCreatedAt()
    {
        Seed(MakeStudent("20240001") with { CreatedAt = new DateTime(2020, 1, 1) });
        service.Update(MakeStudent("20240001"));
        Assert.AreEqual(new DateTime(2020, 1, 1), service.Find("20240001")!.CreatedAt);
    }

    // ---------- Delete ----------

    [TestMethod]
    public void Delete_ThenFindNull_AndNationalIdEmailCanBeReused()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"));

        Assert.IsTrue(service.Delete("20240001"));
        Assert.IsNull(service.Find("20240001"));
        Assert.IsFalse(service.Delete("20240001")); // xóa lần 2 trả false

        service.Add(MakeStudent("20240001")); // cùng NationalId và Email như cũ
        Assert.IsNotNull(service.Find("20240001"));
    }

    // ---------- Khoảng ----------

    [TestMethod]
    public void FindByIdRange_CorrectCount()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"), MakeStudent("20240003"), MakeStudent("20240004"));
        CollectionAssert.AreEqual(new[] { "20240002", "20240003" }, Ids(service.FindByIdRange("20240002", "20240003")));
        Assert.AreEqual(0, service.FindByIdRange("30000000", "40000000").Count);
    }

    [TestMethod]
    public void DeleteByIdRange_DeletesCorrectCount()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"), MakeStudent("20240003"), MakeStudent("20240004"));

        Assert.AreEqual(2, service.DeleteByIdRange("20240002", "20240003"));
        CollectionAssert.AreEqual(new[] { "20240001", "20240004" }, Ids(service.GetAll()));
        Assert.AreEqual(2, service.Tree.Count);
        Assert.AreEqual(0, service.DeleteByIdRange("30000000", "40000000"));
    }

    [TestMethod]
    public void FindByGpaRange_IncludesBothEnds()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0), MakeStudent("20240003", 3.0), MakeStudent("20240004", 3.5));
        CollectionAssert.AreEqual(new[] { "20240002", "20240003", "20240004" }, Ids(service.FindByGpaRange(3.0, 3.5)));
    }

    // ---------- Thủ khoa, Top N ----------

    [TestMethod]
    public void TopStudent_LowestGpaStudent_EmptyReturnsNull()
    {
        Assert.IsNull(service.TopStudent());
        Assert.IsNull(service.LowestGpaStudent());
        Assert.AreEqual(0, service.TopN(5).Count);
    }

    [TestMethod]
    public void TopN_Descending_AndMoreThanCount()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0), MakeStudent("20240003", 3.5));
        CollectionAssert.AreEqual(new[] { "20240003", "20240002" }, Ids(service.TopN(2)));
        Assert.AreEqual(3, service.TopN(10).Count);
    }

    // ---------- Filter ----------

    private void SeedFilter() => Seed(
        MakeStudent("20240001", 3.8) with { FullName = "Trần Minh Tuấn", ClassName = "CNTT01", Faculty = "CNTT", Status = Status.Studying },
        MakeStudent("20240002", 3.3) with { FullName = "Lê Thị Hoa", ClassName = "KT01", Faculty = "KT", Status = Status.OnLeave },
        MakeStudent("20240003", 1.5) with { FullName = "Phạm Tuấn Anh", ClassName = "KT01", Faculty = "KT", Status = Status.Studying });

    [TestMethod]
    public void Filter_NoCriteria_ReturnsAll()
    {
        SeedFilter();
        Assert.AreEqual(3, service.Filter(null, "", "  ", null, null).Count);
    }

    [TestMethod]
    public void Filter_ByFullName_Contains_IgnoresCase()
    {
        SeedFilter();
        CollectionAssert.AreEqual(new[] { "20240001", "20240003" }, Ids(service.Filter("TUẤN", null, null, null, null)));
    }

    [TestMethod]
    public void Filter_ByClass_Faculty_Status_Grade()
    {
        SeedFilter();
        CollectionAssert.AreEqual(new[] { "20240002", "20240003" }, Ids(service.Filter(null, "kt01", null, null, null)));
        CollectionAssert.AreEqual(new[] { "20240002", "20240003" }, Ids(service.Filter(null, null, "kt", null, null)));
        CollectionAssert.AreEqual(new[] { "20240002" }, Ids(service.Filter(null, null, null, Status.OnLeave, null)));
        CollectionAssert.AreEqual(new[] { "20240001" }, Ids(service.Filter(null, null, null, null, Grade.Excellent)));
    }

    [TestMethod]
    public void Filter_CombinedCriteria()
    {
        SeedFilter();
        CollectionAssert.AreEqual(new[] { "20240003" }, Ids(service.Filter("tuấn", "KT01", "KT", Status.Studying, Grade.Weak)));
        Assert.AreEqual(0, service.Filter("Hoa", null, "CNTT", null, null).Count);
    }

    // ---------- Chỉ có đúng 1 sinh viên ----------

    [TestMethod]
    public void SingleStudent_Update_DoesNotCrash()
    {
        Seed(MakeStudent("20240001", 2.0));
        service.Update(MakeStudent("20240001", 3.9));

        Assert.AreEqual(3.9, service.TopStudent()!.Gpa);
        Assert.AreEqual(3.9, service.LowestGpaStudent()!.Gpa);
        Assert.AreEqual(1, service.Tree.Count);
    }

    [TestMethod]
    public void SingleStudent_Delete_DoesNotCrash()
    {
        Seed(MakeStudent("20240001"));
        Assert.IsTrue(service.Delete("20240001"));

        Assert.AreEqual(0, service.Tree.Count);
        Assert.IsNull(service.TopStudent());
        Assert.IsNull(service.LowestGpaStudent());
        Assert.AreEqual(0, service.GetAll().Count());
    }

    // ---------- Hoàn tác khi Save lỗi ----------

    [TestMethod]
    public void SaveFails_Add_RollsBack()
    {
        Seed(MakeStudent("20240001", 2.0));
        var before = service.GetAll().ToList();
        repo.FailOnSave = true;

        Assert.ThrowsExactly<IOException>(() => service.Add(MakeStudent("20240002", 3.9)));

        CollectionAssert.AreEqual(before, service.GetAll().ToList());
        Assert.AreEqual(1, service.Tree.Count);
        Assert.AreEqual("20240001", service.TopStudent()!.StudentId); // cây điểm đã bỏ 20240002

        repo.FailOnSave = false;
        service.Add(MakeStudent("20240002", 3.9)); // NationalId/Email đã được trả lại nên Add lại được
        Assert.AreEqual(2, service.Tree.Count);
    }

    [TestMethod]
    public void SaveFails_Update_RollsBack()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0));
        var before = service.GetAll().ToList();
        repo.FailOnSave = true;

        Assert.ThrowsExactly<IOException>(() =>
            service.Update(MakeStudent("20240001", 3.9) with { NationalId = "999999999999", Email = "moi@x.vn" }));

        CollectionAssert.AreEqual(before, service.GetAll().ToList());
        Assert.AreEqual(2, service.Tree.Count);
        Assert.AreEqual("20240002", service.TopStudent()!.StudentId);
        Assert.AreEqual("20240001", service.LowestGpaStudent()!.StudentId);
        CollectionAssert.AreEqual(new[] { "20240002", "20240001" }, Ids(service.TopN(2)));

        repo.FailOnSave = false;
        // NationalId/Email mới phải còn trống, NationalId/Email cũ vẫn bị giữ
        service.Add(MakeStudent("20240003") with { NationalId = "999999999999", Email = "moi@x.vn" });
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(MakeStudent("20240004") with { NationalId = "000020240001" }));
        Assert.AreEqual("NationalId", ex.Field);
        ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(MakeStudent("20240004") with { Email = "20240001@x.vn" }));
        Assert.AreEqual("Email", ex.Field);
    }

    [TestMethod]
    public void SaveFails_Delete_RollsBack()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0));
        var before = service.GetAll().ToList();
        repo.FailOnSave = true;

        Assert.ThrowsExactly<IOException>(() => service.Delete("20240002"));

        CollectionAssert.AreEqual(before, service.GetAll().ToList());
        Assert.AreEqual(2, service.Tree.Count);
        Assert.AreEqual("20240002", service.TopStudent()!.StudentId);

        // NationalId của người bị xóa hụt vẫn phải bị giữ
        repo.FailOnSave = false;
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(MakeStudent("20240009") with { NationalId = "000020240002" }));
        Assert.AreEqual("NationalId", ex.Field);
    }

    [TestMethod]
    public void SaveFails_DeleteByIdRange_RollsBack()
    {
        Seed(MakeStudent("20240001", 1.0), MakeStudent("20240002", 2.0), MakeStudent("20240003", 3.0), MakeStudent("20240004", 3.5));
        var before = service.GetAll().ToList();
        repo.FailOnSave = true;

        Assert.ThrowsExactly<IOException>(() => service.DeleteByIdRange("20240002", "20240003"));

        CollectionAssert.AreEqual(before, service.GetAll().ToList());
        Assert.AreEqual(4, service.Tree.Count);
        CollectionAssert.AreEqual(new[] { "20240004", "20240003", "20240002", "20240001" }, Ids(service.TopN(10)));

        repo.FailOnSave = false;
        var ex = Assert.ThrowsExactly<DuplicateKeyException>(() => service.Add(MakeStudent("20240009") with { Email = "20240003@x.vn" }));
        Assert.AreEqual("Email", ex.Field);
    }
}
