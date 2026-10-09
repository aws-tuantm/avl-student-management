using System.Text.RegularExpressions;
using AVLStudentManagement.Core.Models;

namespace AVLStudentManagement.Core.Validation;

/// <summary>Lỗi kiểm tra dữ liệu, key của Errors là tên trường.</summary>
public sealed class ValidationException : Exception
{
    public Dictionary<string, string> Errors { get; }

    public ValidationException(Dictionary<string, string> errors)
        : base("Dữ liệu không hợp lệ: " + string.Join("; ", errors.Values))
    {
        Errors = errors;
    }
}

public static class StudentValidator
{
    public const string StudentIdPattern = @"^[0-9]{8,12}$";
    public const string NationalIdPattern = @"^[0-9]{12}$";
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    public const string PhonePattern = @"^0[0-9]{9}$";

    public const int MinAge = 15;
    public const int MaxAge = 60;

    /// <summary>Trả về lỗi theo tên trường. Rỗng nghĩa là hợp lệ.</summary>
    public static Dictionary<string, string> Validate(Student student, Catalog catalog)
    {
        var errors = new Dictionary<string, string>();

        if (!Regex.IsMatch(student.StudentId, StudentIdPattern))
            errors[nameof(student.StudentId)] = "Mã SV gồm 8-12 chữ số.";

        if (student.FullName.Length < 2 || student.FullName.Length > 100)
            errors[nameof(student.FullName)] = "Họ tên dài 2-100 ký tự.";

        if (!IsAgeValid(student.BirthDate))
            errors[nameof(student.BirthDate)] = $"Tuổi phải từ {MinAge} đến {MaxAge}.";

        if (!Enum.IsDefined(student.Gender))
            errors[nameof(student.Gender)] = "Giới tính không hợp lệ.";

        if (!Regex.IsMatch(student.NationalId, NationalIdPattern))
            errors[nameof(student.NationalId)] = "CCCD gồm đúng 12 chữ số.";

        if (!Regex.IsMatch(student.Email, EmailPattern))
            errors[nameof(student.Email)] = "Email không đúng định dạng.";

        if (student.Phone != "" && !Regex.IsMatch(student.Phone, PhonePattern))
            errors[nameof(student.Phone)] = "Số điện thoại gồm 10 số, bắt đầu bằng 0.";

        if (student.Address.Length > 255)
            errors[nameof(student.Address)] = "Địa chỉ tối đa 255 ký tự.";

        if (student.Faculty == "") errors[nameof(student.Faculty)] = "Chưa chọn khoa.";
        else if (!catalog.Faculties.Contains(student.Faculty, StringComparer.OrdinalIgnoreCase))
            errors[nameof(student.Faculty)] = "Khoa không có trong danh mục.";

        if (student.ClassName == "") errors[nameof(student.ClassName)] = "Chưa chọn lớp.";
        else if (!errors.ContainsKey(nameof(student.Faculty)) && !catalog.Has(student.Faculty, student.ClassName))
            errors[nameof(student.ClassName)] = "Lớp không thuộc khoa đã chọn.";

        if (!Enum.IsDefined(student.Status))
            errors[nameof(student.Status)] = "Trạng thái không hợp lệ.";

        if (double.IsNaN(student.Gpa) || student.Gpa < 0 || student.Gpa > 4)
            errors[nameof(student.Gpa)] = "Điểm TB từ 0 đến 4.";

        return errors;
    }

    private static bool IsAgeValid(DateTime birthDate)
    {
        var today = DateTime.Today;
        int age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--; // chưa tới sinh nhật năm nay
        return age >= MinAge && age <= MaxAge;
    }
}
