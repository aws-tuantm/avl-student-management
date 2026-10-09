using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.DataStructures;
using AVLStudentManagement.Core.Models;
using System.Text.RegularExpressions;

namespace AVLStudentManagement.Core.Services;

// Giữ 1 cây AVL (theo StudentId) và 2 HashSet (NationalId, Email).
// Mỗi thao tác ghi: kiểm tra, cập nhật cây, lưu Excel. Lưu lỗi thì hoàn tác.
public class StudentService
{
    private readonly IStudentRepository repo;
    private AvlTree idTree = new AvlTree();
    private HashSet<string> nationalIds = new HashSet<string>();
    private HashSet<string> emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public Catalog Catalog { get; private set; } = new Catalog();

    public AvlTree Tree
    {
        get { return idTree; }
    }

    public StudentService(IStudentRepository repo)
    {
        this.repo = repo;
    }


    // Đọc Excel và dựng cây. Trùng khóa trong file thì báo lỗi kèm số dòng.
    public void Load()
    {
        List<Student> list = repo.Load();

        // Dựng vào biến tạm, đủ hợp lệ mới gán vào service (lỗi giữa chừng thì service giữ nguyên)
        AvlTree newIdTree = new AvlTree();
        HashSet<string> newNationalIds = new HashSet<string>();
        HashSet<string> newEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < list.Count; i++)
        {
            Student student = list[i];

            // Số dòng thật trong file (repo bỏ qua dòng trống nên i + 2 có thể lệch). Dòng 1 là tiêu đề.
            int row = i + 2;
            if (repo.RealRows != null)
            {
                row = repo.RealRows[i];
            }

            StudentException? duplicate = FindDuplicate(student, newIdTree, newNationalIds, newEmails);
            if (duplicate != null)
            {
                throw new StudentException(row, duplicate.Field, $"{duplicate.Field} '{duplicate.Value}' bị trùng.");
            }

            Insert(student, newIdTree, newNationalIds, newEmails);
        }

        idTree = newIdTree;
        nationalIds = newNationalIds;
        emails = newEmails;
        Catalog = repo.Catalog;
    }

    public List<Student> GetAll()
    {
        return idTree.InOrder();
    }

    public Student? Find(string studentId)
    {
        return idTree.Find(MakeIdSample(studentId));
    }


    public void Add(Student student)
    {
        DateTime now = DateTime.Now;
        student = Normalize(student);
        student.CreatedAt = now;
        student.UpdatedAt = now;
        Check(student);

        StudentException? duplicate = FindDuplicate(student, idTree, nationalIds, emails);
        if (duplicate != null)
        {
            throw duplicate;
        }

        Insert(student);
        try
        {
            Save();
        }
        catch
        {
            Remove(student);
            throw;
        }
    }

    // Nhập hàng loạt, chỉ lưu 1 lần. Sinh viên bị bỏ qua thì thêm lý do vào errors, trả về số đã thêm.
    public int Import(List<Student> students, List<string> errors)
    {
        List<Student> added = new List<Student>();
        DateTime now = DateTime.Now;

        foreach (Student rawStudent in students)
        {
            Student student = Normalize(rawStudent);
            student.CreatedAt = now;
            student.UpdatedAt = now;
            string label = $"SV '{student.StudentId}' ({student.FullName})";

            Dictionary<string, string> invalidFields = Validate(student, Catalog);
            if (invalidFields.Count > 0)
            {
                errors.Add($"{label}: {string.Join(" ", invalidFields.Values)}");
                continue;
            }

            StudentException? duplicate = FindDuplicate(student, idTree, nationalIds, emails);
            if (duplicate != null)
            {
                errors.Add($"{label}: trùng {duplicate.Field} '{duplicate.Value}' với sinh viên đã có.");
                continue;
            }

            Insert(student);
            added.Add(student);
        }

        if (added.Count == 0)
        {
            return 0;
        }

        try
        {
            Save();
        }
        catch
        {
            foreach (Student student in added)
            {
                Remove(student);
            }
            throw;
        }

        return added.Count;
    }

    public void Update(Student student)
    {
        // Chuẩn hóa trước để StudentId có khoảng trắng vẫn tìm được
        student = Normalize(student);

        Student? old = Find(student.StudentId);
        if (old == null)
        {
            throw new KeyNotFoundException($"Không có sinh viên {student.StudentId}.");
        }

        student.CreatedAt = old.CreatedAt;
        student.UpdatedAt = DateTime.Now;
        Check(student);

        bool nationalIdChanged = student.NationalId != old.NationalId;
        if (nationalIdChanged && nationalIds.Contains(student.NationalId))
        {
            throw new StudentException(nameof(Student.NationalId), student.NationalId);
        }

        bool emailChanged = !student.Email.Equals(old.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged && emails.Contains(student.Email))
        {
            throw new StudentException(nameof(Student.Email), student.Email);
        }

        Replace(old, student);
        try
        {
            Save();
        }
        catch
        {
            Replace(student, old);
            throw;
        }
    }


    public List<Student> FindByIdRange(string from, string to)
    {
        return idTree.Range(MakeIdSample(from), MakeIdSample(to));
    }

    public int DeleteByIdRange(string from, string to)
    {
        return DeleteMany(FindByIdRange(from, to));
    }

    // Xóa nhiều sinh viên cùng lúc, chỉ lưu 1 lần. Mã không có trong cây thì bỏ qua. Lưu lỗi thì hoàn tác tất cả.
    // Trả về số sinh viên đã xóa.
    public int DeleteMany(List<Student> students)
    {
        List<Student> removed = new List<Student>();
        foreach (Student student in students)
        {
            Student? old = Find(student.StudentId);
            if (old != null)
            {
                Remove(old);
                removed.Add(old);
            }
        }

        if (removed.Count == 0)
        {
            return 0;
        }

        try
        {
            Save();
        }
        catch
        {
            foreach (Student old in removed)
            {
                Insert(old);
            }
            throw;
        }
        return removed.Count;
    }

    // N sinh viên điểm cao nhất, giảm dần (bằng điểm thì mã SV nhỏ đứng trước). Cây chỉ sắp xếp theo mã SV nên phải sắp xếp lại danh sách.
    public List<Student> TopN(int n)
    {
        List<Student> sorted = GetAll();
        sorted.Sort(CompareGpaDescending);

        if (sorted.Count > n)
        {
            sorted.RemoveRange(n, sorted.Count - n);
        }
        return sorted;
    }

    private static int CompareGpaDescending(Student a, Student b)
    {
        int result = b.Gpa.CompareTo(a.Gpa);
        if (result != 0)
        {
            return result;
        }
        return string.CompareOrdinal(a.StudentId, b.StudentId);
    }

    // Lọc theo lớp, trạng thái, xếp loại. Tiêu chí null thì bỏ qua.
    public List<Student> Filter(string? className, Status? status, Grade? grade)
    {
        List<Student> result = new List<Student>();

        foreach (Student student in GetAll())
        {
            if (className != null && !student.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (status != null && student.Status != status)
            {
                continue;
            }

            if (grade != null && student.Grade != grade)
            {
                continue;
            }

            result.Add(student);
        }

        return result;
    }


    public const string StudentIdPattern = @"^[0-9]+$";
    public const string NationalIdPattern = @"^[0-9]{12}$";
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    public const string PhonePattern = @"^0[0-9]{9}$";

    public const int MinAge = 15;
    public const int MaxAge = 60;

    public static Dictionary<string, string> Validate(Student student, Catalog catalog)
    {
        Dictionary<string, string> errors = new Dictionary<string, string>();

        if (!Regex.IsMatch(student.StudentId, StudentIdPattern))
        {
            errors[nameof(Student.StudentId)] = "Mã SV chỉ gồm chữ số, ít nhất 1 chữ số.";
        }

        if (student.FullName.Length < 2 || student.FullName.Length > 100)
        {
            errors[nameof(Student.FullName)] = "Họ tên dài 2-100 ký tự.";
        }

        if (!IsAgeValid(student.BirthDate))
        {
            errors[nameof(Student.BirthDate)] = $"Tuổi phải từ {MinAge} đến {MaxAge}.";
        }

        if (!Enum.IsDefined(student.Gender))
        {
            errors[nameof(Student.Gender)] = "Giới tính không hợp lệ.";
        }

        if (!Regex.IsMatch(student.NationalId, NationalIdPattern))
        {
            errors[nameof(Student.NationalId)] = "CCCD gồm đúng 12 chữ số.";
        }

        if (!Regex.IsMatch(student.Email, EmailPattern))
        {
            errors[nameof(Student.Email)] = "Email không đúng định dạng.";
        }

        if (student.Phone != "" && !Regex.IsMatch(student.Phone, PhonePattern))
        {
            errors[nameof(Student.Phone)] = "Số điện thoại gồm 10 số, bắt đầu bằng 0.";
        }

        if (student.Address.Length > 255)
        {
            errors[nameof(Student.Address)] = "Địa chỉ tối đa 255 ký tự.";
        }

        if (student.Faculty == "")
        {
            errors[nameof(Student.Faculty)] = "Chưa chọn khoa.";
        }
        else if (!catalog.HasFaculty(student.Faculty))
        {
            errors[nameof(Student.Faculty)] = "Khoa không có trong danh mục.";
        }

        if (student.ClassName == "")
        {
            errors[nameof(Student.ClassName)] = "Chưa chọn lớp.";
        }
        else if (!errors.ContainsKey(nameof(Student.Faculty)) && !catalog.Has(student.Faculty, student.ClassName))
        {
            errors[nameof(Student.ClassName)] = "Lớp không thuộc khoa đã chọn.";
        }

        if (!Enum.IsDefined(student.Status))
        {
            errors[nameof(Student.Status)] = "Trạng thái không hợp lệ.";
        }

        if (double.IsNaN(student.Gpa) || student.Gpa <= 0)
        {
            errors[nameof(Student.Gpa)] = "Điểm TB phải lớn hơn 0.";
        }

        return errors;
    }

    private static bool IsAgeValid(DateTime birthDate)
    {
        DateTime today = DateTime.Today;
        int age = today.Year - birthDate.Year;

        // Chưa tới sinh nhật năm nay thì trừ đi 1 tuổi
        if (birthDate.Date > today.AddYears(-age))
        {
            age--;
        }

        return age >= MinAge && age <= MaxAge;
    }


    private void Save()
    {
        repo.Save(GetAll());
    }

    private void Check(Student student)
    {
        Dictionary<string, string> errors = Validate(student, Catalog);
        if (errors.Count > 0)
        {
            throw new StudentException(errors);
        }
    }

    // Sinh viên "mẫu" chỉ có StudentId, dùng để tìm trong cây theo mã.
    private static Student MakeIdSample(string studentId)
    {
        return new Student { StudentId = studentId };
    }

    // Chuẩn hóa: bỏ khoảng trắng thừa, làm tròn điểm 2 chữ số
    private static Student Normalize(Student student)
    {
        Student result = student.Copy();
        result.StudentId = student.StudentId.Trim();
        result.FullName = RemoveExtraSpaces(student.FullName);
        result.NationalId = student.NationalId.Trim();
        result.Email = student.Email.Trim();
        result.Phone = student.Phone.Trim();
        result.Address = student.Address.Trim();
        result.ClassName = student.ClassName.Trim();
        result.Faculty = student.Faculty.Trim();
        result.Gpa = Math.Round(student.Gpa, 2, MidpointRounding.AwayFromZero);
        return result;
    }

    private static string RemoveExtraSpaces(string text)
    {
        string[] words = text.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words);
    }

    private void Insert(Student student)
    {
        Insert(student, idTree, nationalIds, emails);
    }

    private static void Insert(
        Student student,
        AvlTree targetIdTree,
        HashSet<string> nationalIdSet,
        HashSet<string> emailSet)
    {
        targetIdTree.Insert(student);
        nationalIdSet.Add(student.NationalId);
        emailSet.Add(student.Email);
    }

    private static StudentException? FindDuplicate(
        Student student,
        AvlTree targetIdTree,
        HashSet<string> nationalIdSet,
        HashSet<string> emailSet)
    {
        if (targetIdTree.Find(student) != null)
        {
            return new StudentException(nameof(Student.StudentId), student.StudentId);
        }

        if (nationalIdSet.Contains(student.NationalId))
        {
            return new StudentException(nameof(Student.NationalId), student.NationalId);
        }

        if (emailSet.Contains(student.Email))
        {
            return new StudentException(nameof(Student.Email), student.Email);
        }

        return null;
    }

    private void Remove(Student student)
    {
        idTree.Delete(student);
        nationalIds.Remove(student.NationalId);
        emails.Remove(student.Email);
    }

    // Thay bản cũ bằng bản mới (cùng StudentId): cây chỉ đổi dữ liệu của nút
    private void Replace(Student old, Student student)
    {
        idTree.Update(student);

        nationalIds.Remove(old.NationalId);
        nationalIds.Add(student.NationalId);

        emails.Remove(old.Email);
        emails.Add(student.Email);
    }
}
