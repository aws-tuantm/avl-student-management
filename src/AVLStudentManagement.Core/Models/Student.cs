namespace AVLStudentManagement.Core.Models;

/// <summary>Hồ sơ sinh viên. Bất biến: muốn sửa thì tạo bản mới bằng <c>with</c>.</summary>
public sealed record Student
{
    public string StudentId { get; init; } = "";
    public string FullName { get; init; } = "";
    public DateTime BirthDate { get; init; }
    public Gender Gender { get; init; }
    public string NationalId { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Address { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string Faculty { get; init; } = "";
    public Status Status { get; init; }
    public double Gpa { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>Xếp loại tính từ Gpa (thang 4), không lưu.</summary>
    public Grade Grade => Gpa switch
    {
        >= 3.6 => Grade.Excellent,
        >= 3.2 => Grade.VeryGood,
        >= 2.5 => Grade.Good,
        >= 2.0 => Grade.Average,
        >= 1.0 => Grade.Weak,
        _ => Grade.Poor,
    };
}
