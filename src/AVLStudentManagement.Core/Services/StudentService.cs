using AVLStudentManagement.Core.DataStructures;
using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Models;
using AVLStudentManagement.Core.Validation;

namespace AVLStudentManagement.Core.Services;

/// <summary>Trùng khóa (StudentId, NationalId hoặc Email). Field là tên trường bị trùng.</summary>
public sealed class DuplicateKeyException : Exception
{
    public string Field { get; }

    public DuplicateKeyException(string field, string value)
        : base($"{field} '{value}' đã tồn tại.")
    {
        Field = field;
    }
}

/// <summary>
/// Giữ 2 cây AVL (StudentId; Gpa+StudentId) và 2 HashSet (NationalId, Email).
/// Mỗi thao tác ghi: kiểm tra, cập nhật cây, lưu Excel. Lưu lỗi thì hoàn tác.
/// </summary>
public sealed class StudentService
{
    private readonly IStudentRepository repo;
    private AvlTree<string, Student> byId = new(StringComparer.Ordinal);
    private AvlTree<(double Gpa, string StudentId), Student> byGpa = new(GpaComparer);
    private HashSet<string> nationalIds = new();
    private HashSet<string> emails = new(StringComparer.OrdinalIgnoreCase);

    public Catalog Catalog { get; private set; } = new();

    /// <summary>Cây StudentId, chỉ để đọc (màn hình vẽ cây dùng).</summary>
    public AvlTree<string, Student> Tree => byId;

    public StudentService(IStudentRepository repo)
    {
        this.repo = repo;
    }

    // So Gpa trước, bằng nhau thì so StudentId để khóa luôn duy nhất
    private static readonly IComparer<(double Gpa, string StudentId)> GpaComparer =
        Comparer<(double Gpa, string StudentId)>.Create((a, b) =>
        {
            int c = a.Gpa.CompareTo(b.Gpa);
            return c != 0 ? c : string.CompareOrdinal(a.StudentId, b.StudentId);
        });

    private static (double, string) GpaKey(Student student) => (student.Gpa, student.StudentId);

    // ---------- Đọc ----------

    /// <summary>Đọc Excel và dựng 2 cây. Trùng khóa trong file thì báo lỗi kèm số dòng.</summary>
    public void Load()
    {
        var (list, catalog) = repo.Load();

        // Dựng vào biến tạm, đủ hợp lệ mới gán vào service (lỗi giữa chừng thì service giữ nguyên)
        var newById = new AvlTree<string, Student>(StringComparer.Ordinal);
        var newByGpa = new AvlTree<(double Gpa, string StudentId), Student>(GpaComparer);
        var newNationalIds = new HashSet<string>();
        var newEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < list.Count; i++)
        {
            var student = list[i];
            // Số dòng thật trong file (repo bỏ qua dòng trống nên i + 2 có thể lệch)
            int row = repo.RealRows?[i] ?? i + 2; // dòng 1 là header
            if (FindDuplicate(student, newById, newNationalIds, newEmails) is var (field, value))
                throw new DataFormatException(row, field, $"{field} '{value}' bị trùng.");
            Insert(student, newById, newByGpa, newNationalIds, newEmails);
        }

        byId = newById;
        byGpa = newByGpa;
        nationalIds = newNationalIds;
        emails = newEmails;
        Catalog = catalog;
    }

    /// <summary>Toàn bộ sinh viên, tăng dần theo StudentId.</summary>
    public IEnumerable<Student> GetAll() => byId.InOrder().Select(p => p.Value);

    public Student? Find(string studentId) => byId.TryGet(studentId, out var student) ? student : null;

    // ---------- Ghi ----------

    public void Add(Student student)
    {
        var now = DateTime.Now;
        student = Normalize(student) with { CreatedAt = now, UpdatedAt = now };
        Check(student);

        if (byId.TryGet(student.StudentId, out _)) throw new DuplicateKeyException(nameof(Student.StudentId), student.StudentId);
        if (nationalIds.Contains(student.NationalId)) throw new DuplicateKeyException(nameof(Student.NationalId), student.NationalId);
        if (emails.Contains(student.Email)) throw new DuplicateKeyException(nameof(Student.Email), student.Email);

        Insert(student);
        try { Save(); }
        catch { Remove(student); throw; }
    }

    /// <summary>
    /// Nhập hàng loạt: SV hợp lệ và không trùng thì thêm, còn lại bỏ qua. Chỉ lưu 1 lần.
    /// Errors có một dòng cho mỗi SV bị bỏ qua, nêu rõ trường nào sai và vì sao.
    /// </summary>
    public (int Added, List<string> Errors) Import(IEnumerable<Student> students)
    {
        var added = new List<Student>();
        var errors = new List<string>();
        var now = DateTime.Now;
        foreach (var raw in students)
        {
            var student = Normalize(raw) with { CreatedAt = now, UpdatedAt = now };
            string label = $"SV '{student.StudentId}' ({student.FullName})";

            var invalid = StudentValidator.Validate(student, Catalog);
            if (invalid.Count > 0)
            {
                errors.Add($"{label}: {string.Join(" ", invalid.Values)}");
                continue;
            }
            if (FindDuplicate(student, byId, nationalIds, emails) is var (field, value))
            {
                errors.Add($"{label}: trùng {field} '{value}' với sinh viên đã có.");
                continue;
            }
            Insert(student);
            added.Add(student);
        }
        if (added.Count == 0) return (0, errors);

        try { Save(); }
        catch { foreach (var student in added) Remove(student); throw; }
        return (added.Count, errors);
    }

    /// <summary>Sửa hồ sơ. StudentId dùng để tìm và không đổi được.</summary>
    public void Update(Student student)
    {
        student = Normalize(student); // chuẩn hóa trước để StudentId có khoảng trắng vẫn tìm được
        var old = Find(student.StudentId) ?? throw new KeyNotFoundException($"Không có sinh viên {student.StudentId}.");
        student = student with { CreatedAt = old.CreatedAt, UpdatedAt = DateTime.Now };
        Check(student);

        if (student.NationalId != old.NationalId && nationalIds.Contains(student.NationalId))
            throw new DuplicateKeyException(nameof(Student.NationalId), student.NationalId);
        if (!student.Email.Equals(old.Email, StringComparison.OrdinalIgnoreCase) && emails.Contains(student.Email))
            throw new DuplicateKeyException(nameof(Student.Email), student.Email);

        Replace(old, student);
        try { Save(); }
        catch { Replace(student, old); throw; }
    }

    /// <summary>Xóa theo StudentId. Trả về false nếu không có.</summary>
    public bool Delete(string studentId)
    {
        var old = Find(studentId);
        if (old == null) return false;

        Remove(old);
        try { Save(); }
        catch { Insert(old); throw; }
        return true;
    }

    // ---------- Tìm theo khoảng ----------

    public List<Student> FindByIdRange(string from, string to) =>
        byId.Range(from, to).Select(p => p.Value).ToList();

    /// <summary>Xóa các sinh viên có StudentId trong [from, to]. Trả về số lượng đã xóa.</summary>
    public int DeleteByIdRange(string from, string to)
    {
        var list = FindByIdRange(from, to); // lấy ra list trước rồi mới xóa
        if (list.Count == 0) return 0;

        foreach (var student in list) Remove(student);
        try { Save(); }
        catch { foreach (var student in list) Insert(student); throw; }
        return list.Count;
    }

    public List<Student> FindByGpaRange(double min, double max) =>
        byGpa.Range((min, ""), (max, "￿")).Select(p => p.Value).ToList();

    // ---------- Thủ khoa, Top N, Lọc ----------

    /// <summary>Sinh viên điểm cao nhất, null nếu chưa có ai.</summary>
    public Student? TopStudent() => byGpa.Count == 0 ? null : byGpa.Max().Value;

    /// <summary>Sinh viên điểm thấp nhất, null nếu chưa có ai.</summary>
    public Student? LowestGpaStudent() => byGpa.Count == 0 ? null : byGpa.Min().Value;

    /// <summary>N sinh viên điểm cao nhất, giảm dần.</summary>
    public List<Student> TopN(int n) =>
        byGpa.InOrderDescending().Take(n).Select(p => p.Value).ToList();

    /// <summary>Lọc nhiều tiêu chí. Tiêu chí để trống/null thì bỏ qua.</summary>
    public List<Student> Filter(string? fullName, string? className, string? faculty, Status? status, Grade? grade)
    {
        return GetAll().Where(s =>
            (string.IsNullOrWhiteSpace(fullName) || s.FullName.Contains(fullName.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(className) || s.ClassName.Equals(className.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(faculty) || s.Faculty.Equals(faculty.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (status == null || s.Status == status) &&
            (grade == null || s.Grade == grade)).ToList();
    }

    // ---------- Hàm nội bộ ----------

    private void Save() => repo.Save(GetAll());

    private void Check(Student student)
    {
        var errors = StudentValidator.Validate(student, Catalog);
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    // Chuẩn hóa: bỏ khoảng trắng thừa, làm tròn điểm 2 chữ số
    private static Student Normalize(Student student) => student with
    {
        StudentId = student.StudentId.Trim(),
        FullName = string.Join(' ', student.FullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
        NationalId = student.NationalId.Trim(),
        Email = student.Email.Trim(),
        Phone = student.Phone.Trim(),
        Address = student.Address.Trim(),
        ClassName = student.ClassName.Trim(),
        Faculty = student.Faculty.Trim(),
        Gpa = Math.Round(student.Gpa, 2, MidpointRounding.AwayFromZero),
    };

    // Chèn vào cả 2 cây và 2 set
    private void Insert(Student student) => Insert(student, byId, byGpa, nationalIds, emails);

    private static void Insert(Student student, AvlTree<string, Student> byId,
        AvlTree<(double Gpa, string StudentId), Student> byGpa, HashSet<string> nationalIds, HashSet<string> emails)
    {
        byId.Insert(student.StudentId, student);
        byGpa.Insert(GpaKey(student), student);
        nationalIds.Add(student.NationalId);
        emails.Add(student.Email);
    }

    // Kiểm tra trùng StudentId, NationalId, Email. Trả về (tên trường, giá trị) bị trùng, không trùng thì null
    private static (string Field, string Value)? FindDuplicate(Student student, AvlTree<string, Student> byId,
        HashSet<string> nationalIds, HashSet<string> emails)
    {
        if (byId.TryGet(student.StudentId, out _)) return (nameof(Student.StudentId), student.StudentId);
        if (nationalIds.Contains(student.NationalId)) return (nameof(Student.NationalId), student.NationalId);
        if (emails.Contains(student.Email)) return (nameof(Student.Email), student.Email);
        return null;
    }

    private void Remove(Student student)
    {
        byId.Delete(student.StudentId);
        byGpa.Delete(GpaKey(student));
        nationalIds.Remove(student.NationalId);
        emails.Remove(student.Email);
    }

    // Thay bản cũ bằng bản mới (cùng StudentId). Cây StudentId chỉ đổi giá trị, cây điểm xóa/chèn lại
    private void Replace(Student old, Student student)
    {
        byId.TryUpdate(student.StudentId, student);
        byGpa.Delete(GpaKey(old));
        byGpa.Insert(GpaKey(student), student);
        nationalIds.Remove(old.NationalId);
        nationalIds.Add(student.NationalId);
        emails.Remove(old.Email);
        emails.Add(student.Email);
    }
}
