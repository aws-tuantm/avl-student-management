namespace AVLStudentManagement.Core.Models;

public enum Gender
{
    Male,
    Female,
    Other
}

public enum Status
{
    Studying,
    OnLeave,
    Graduated,
    DroppedOut
}

public enum Grade
{
    Excellent,
    VeryGood,
    Good,
    Average,
    Weak,
    Poor
}

public static class EnumDisplay
{
    private static readonly string[] GenderTexts = { "Nam", "Nữ", "Khác" };
    private static readonly string[] StatusTexts = { "Đang học", "Bảo lưu", "Đã tốt nghiệp", "Thôi học" };
    private static readonly string[] GradeTexts = { "Xuất sắc", "Giỏi", "Khá", "Trung bình", "Yếu", "Kém" };

    public static string ToDisplay(this Gender value)
    {
        return GenderTexts[(int)value];
    }

    public static string ToDisplay(this Status value)
    {
        return StatusTexts[(int)value];
    }

    public static string ToDisplay(this Grade value)
    {
        return GradeTexts[(int)value];
    }
}
