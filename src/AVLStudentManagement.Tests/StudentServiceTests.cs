using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Services;


namespace AVLStudentManagement.Tests;

// Repository giả: giữ danh sách trong bộ nhớ, bật FailOnSave để giả lập lỗi ghi file.
public class FakeRepository : IStudentRepository
{
    public List<Student> Data = new List<Student>();
    public bool FailOnSave;

    public Catalog Catalog { get; set; } = new Catalog();

    // Số dòng thật, để test báo lỗi đúng dòng
    public List<int>? RealRows { get; set; }

    public List<Student> Load()
    {
        return Data.ToList();
    }

    public void Save(List<Student> students)
    {
        if (FailOnSave)
        {
            throw new IOException("Lỗi ghi file giả");
        }
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
        var bad = MakeStudent("20240009").Change(s => { s.Gpa = 0; }); // điểm sai
        var errors = new List<string>();
        int added = service.Import(new List<Student> { MakeStudent("20240001"), MakeStudent("20240002"), bad }, errors);

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
        Assert.ThrowsExactly<IOException>(() => service.Import(new List<Student> { MakeStudent("20240002") }, new List<string>()));
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
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Load());
        StringAssert.Contains(ex.Message, "Dòng 3, cột StudentId");
    }


    // ---------- Hồi quy ----------

    [TestMethod]
    public void Update_IdWithSpaces_StillFound()
    {
        Seed(MakeStudent("20230001"));
        service.Update(MakeStudent("20230001", 3.5).Change(s => { s.StudentId = " 20230001 "; }));
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
        repo.RealRows = new List<int> { 2, 5 }; // giữa hai SV có dòng trống
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Load());
        StringAssert.Contains(ex.Message, "Dòng 5");
    }

    [TestMethod]
    public void Load_ErrorMidway_ServiceKeepsOldData()
    {
        Seed(MakeStudent("20240001"));
        repo.Data = new() { MakeStudent("20240002"), MakeStudent("20240003"), MakeStudent("20240003") };
        Assert.ThrowsExactly<StudentException>(() => service.Load());

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
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240001").Change(s => { s.NationalId = "111111111111"; s.Email = "k@x.vn"; })));
        Assert.AreEqual("StudentId", ex.Field);
    }

    [TestMethod]
    public void Add_DuplicateNationalId_Rejected()
    {
        Seed(MakeStudent("20240001"));
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240002").Change(s => { s.NationalId = "000020240001"; })));
        Assert.AreEqual("NationalId", ex.Field);
    }

    [TestMethod]
    public void Add_DuplicateEmail_IgnoresCase_Rejected()
    {
        Seed(MakeStudent("20240001"));
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240002").Change(s => { s.Email = "20240001@X.VN"; })));
        Assert.AreEqual("Email", ex.Field);
    }

    [TestMethod]
    public void Add_InvalidStudent_ThrowsStudentException()
    {
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240001").Change(s => { s.NationalId = "123"; })));
        Assert.IsTrue(ex.Errors.ContainsKey("NationalId"));
        Assert.AreEqual(0, service.Tree.Count);
    }

    // ---------- Update ----------

    [TestMethod]
    public void Update_ChangeGpa_TopNIsUpdated()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0), MakeStudent("20240003", 3.5));
        Assert.AreEqual("20240003", service.TopN(1)[0].StudentId);

        service.Update(MakeStudent("20240001", 3.9)); // thấp nhất lên cao nhất
        service.Update(MakeStudent("20240003", 1.0)); // cao nhất xuống thấp nhất

        CollectionAssert.AreEqual(new[] { "20240001", "20240002" }, Ids(service.TopN(2)));
        Assert.AreEqual(1.0, service.Find("20240003")!.Gpa);
        Assert.AreEqual(3, service.Tree.Count);
    }

    [TestMethod]
    public void Update_SameNationalIdAndEmail_NoDuplicateError()
    {
        Seed(MakeStudent("20240001"));
        service.Update(MakeStudent("20240001").Change(s => { s.FullName = "Tên Mới"; }));
        Assert.AreEqual("Tên Mới", service.Find("20240001")!.FullName);
    }

    [TestMethod]
    public void Update_NationalIdOfOtherStudent_Rejected()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"));
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Update(MakeStudent("20240001").Change(s => { s.NationalId = "000020240002"; })));
        Assert.AreEqual("NationalId", ex.Field);
    }

    [TestMethod]
    public void Update_EmailOfOtherStudent_Rejected()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"));
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Update(MakeStudent("20240001").Change(s => { s.Email = "20240002@x.vn"; })));
        Assert.AreEqual("Email", ex.Field);
    }

    [TestMethod]
    public void Update_NewNationalId_OldOneCanBeReused()
    {
        Seed(MakeStudent("20240001"));
        service.Update(MakeStudent("20240001").Change(s => { s.NationalId = "999999999999"; }));

        service.Add(MakeStudent("20240002").Change(s => { s.NationalId = "000020240001"; })); // NationalId cũ giờ trống
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
        Seed(MakeStudent("20240001").Change(s => { s.CreatedAt = new DateTime(2020, 1, 1); }));
        service.Update(MakeStudent("20240001"));
        Assert.AreEqual(new DateTime(2020, 1, 1), service.Find("20240001")!.CreatedAt);
    }

    // ---------- Delete ----------

    [TestMethod]
    public void Delete_ThenFindNull_AndNationalIdEmailCanBeReused()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"));

        Assert.AreEqual(1, service.DeleteMany(new List<Student> { MakeStudent("20240001") }));
        Assert.IsNull(service.Find("20240001"));
        Assert.AreEqual(0, service.DeleteMany(new List<Student> { MakeStudent("20240001") })); // xóa lần 2: không còn gì để xóa

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

    // ---------- Thủ khoa, Top N ----------

    [TestMethod]
    public void TopN_EmptyReturnsEmpty()
    {
        Assert.AreEqual(0, service.TopN(5).Count);
    }

    [TestMethod]
    public void TopN_Descending_AndMoreThanCount()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0), MakeStudent("20240003", 3.5));
        CollectionAssert.AreEqual(new[] { "20240003", "20240002" }, Ids(service.TopN(2)));
        Assert.AreEqual(3, service.TopN(10).Count);
    }

    // ---------- DeleteMany ----------

    [TestMethod]
    public void DeleteMany_RemovesAll_SavesOnce_IgnoresUnknown()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"), MakeStudent("20240003"));

        int removed = service.DeleteMany(new List<Student> { MakeStudent("20240001"), MakeStudent("20240003"), MakeStudent("99999999") });

        Assert.AreEqual(2, removed);
        CollectionAssert.AreEqual(new[] { "20240002" }, Ids(repo.Data));
        Assert.AreEqual(1, service.Tree.Count);
    }

    [TestMethod]
    public void DeleteMany_SaveFails_RollsBackAll()
    {
        Seed(MakeStudent("20240001"), MakeStudent("20240002"), MakeStudent("20240003"));
        repo.FailOnSave = true;

        Assert.ThrowsExactly<IOException>(() => service.DeleteMany(new List<Student> { MakeStudent("20240001"), MakeStudent("20240002") }));

        Assert.AreEqual(3, service.Tree.Count);
        CollectionAssert.AreEqual(new[] { "20240001", "20240002", "20240003" }, Ids(service.GetAll()));
    }

    // ---------- Filter ----------

    private void SeedFilter() => Seed(
        MakeStudent("20240001", 9.5).Change(s => { s.FullName = "Trần Minh Tuấn"; s.ClassName = "CNTT01"; s.Faculty = "CNTT"; s.Status = Status.Studying; }),
        MakeStudent("20240002", 8.2).Change(s => { s.FullName = "Lê Thị Hoa"; s.ClassName = "KT01"; s.Faculty = "KT"; s.Status = Status.OnLeave; }),
        MakeStudent("20240003", 4.5).Change(s => { s.FullName = "Phạm Tuấn Anh"; s.ClassName = "KT01"; s.Faculty = "KT"; s.Status = Status.Studying; }));

    [TestMethod]
    public void Filter_NoCriteria_ReturnsAll()
    {
        SeedFilter();
        Assert.AreEqual(3, service.Filter(null, null, null).Count);
    }

    [TestMethod]
    public void Filter_ByClass_Status_Grade()
    {
        SeedFilter();
        CollectionAssert.AreEqual(new[] { "20240002", "20240003" }, Ids(service.Filter("kt01", null, null))); // không phân biệt hoa thường
        CollectionAssert.AreEqual(new[] { "20240002" }, Ids(service.Filter(null, Status.OnLeave, null)));
        CollectionAssert.AreEqual(new[] { "20240001" }, Ids(service.Filter(null, null, Grade.Excellent)));
    }

    [TestMethod]
    public void Filter_CombinedCriteria()
    {
        SeedFilter();
        CollectionAssert.AreEqual(new[] { "20240003" }, Ids(service.Filter("KT01", Status.Studying, Grade.Weak)));
        Assert.AreEqual(0, service.Filter("CNTT01", Status.OnLeave, null).Count);
    }

    // ---------- Chỉ có đúng 1 sinh viên ----------

    [TestMethod]
    public void SingleStudent_Update_DoesNotCrash()
    {
        Seed(MakeStudent("20240001", 2.0));
        service.Update(MakeStudent("20240001", 3.9));

        Assert.AreEqual(3.9, service.TopN(1)[0].Gpa);
        Assert.AreEqual(1, service.Tree.Count);
    }

    [TestMethod]
    public void SingleStudent_Delete_DoesNotCrash()
    {
        Seed(MakeStudent("20240001"));
        Assert.AreEqual(1, service.DeleteMany(new List<Student> { MakeStudent("20240001") }));

        Assert.AreEqual(0, service.Tree.Count);
        Assert.AreEqual(0, service.TopN(1).Count);
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
        Assert.AreEqual("20240001", service.TopN(1)[0].StudentId); // 20240002 đã bị gỡ lại

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
            service.Update(MakeStudent("20240001", 3.9).Change(s => { s.NationalId = "999999999999"; s.Email = "moi@x.vn"; })));

        CollectionAssert.AreEqual(before, service.GetAll().ToList());
        Assert.AreEqual(2, service.Tree.Count);
        CollectionAssert.AreEqual(new[] { "20240002", "20240001" }, Ids(service.TopN(2)));

        repo.FailOnSave = false;
        // NationalId/Email mới phải còn trống, NationalId/Email cũ vẫn bị giữ
        service.Add(MakeStudent("20240003").Change(s => { s.NationalId = "999999999999"; s.Email = "moi@x.vn"; }));
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240004").Change(s => { s.NationalId = "000020240001"; })));
        Assert.AreEqual("NationalId", ex.Field);
        ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240004").Change(s => { s.Email = "20240001@x.vn"; })));
        Assert.AreEqual("Email", ex.Field);
    }

    [TestMethod]
    public void SaveFails_Delete_RollsBack()
    {
        Seed(MakeStudent("20240001", 2.0), MakeStudent("20240002", 3.0));
        var before = service.GetAll().ToList();
        repo.FailOnSave = true;

        Assert.ThrowsExactly<IOException>(() => service.DeleteMany(new List<Student> { MakeStudent("20240002") }));

        CollectionAssert.AreEqual(before, service.GetAll().ToList());
        Assert.AreEqual(2, service.Tree.Count);
        Assert.AreEqual("20240002", service.TopN(1)[0].StudentId);

        // NationalId của người bị xóa hụt vẫn phải bị giữ
        repo.FailOnSave = false;
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240009").Change(s => { s.NationalId = "000020240002"; })));
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
        var ex = Assert.ThrowsExactly<StudentException>(() => service.Add(MakeStudent("20240009").Change(s => { s.Email = "20240003@x.vn"; })));
        Assert.AreEqual("Email", ex.Field);
    }
}
