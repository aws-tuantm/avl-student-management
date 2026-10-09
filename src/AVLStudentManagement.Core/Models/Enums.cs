namespace AVLStudentManagement.Core.Models;

public enum Gender { Male, Female, Other }

public enum Status { Studying, OnLeave, Graduated, DroppedOut }

public enum Grade { Excellent, VeryGood, Good, Average, Weak, Poor }

/// <summary>Nhãn tiếng Việt để hiển thị lên giao diện.</summary>
public static class EnumDisplay
{
    public static string ToDisplay(this Gender v) => v switch
    {
        Gender.Male => "Nam",
        Gender.Female => "Nữ",
        _ => "Khác",
    };

    public static string ToDisplay(this Status v) => v switch
    {
        Status.Studying => "Đang học",
        Status.OnLeave => "Bảo lưu",
        Status.Graduated => "Đã tốt nghiệp",
        _ => "Thôi học",
    };

    public static string ToDisplay(this Grade v) => v switch
    {
        Grade.Excellent => "Xuất sắc",
        Grade.VeryGood => "Giỏi",
        Grade.Good => "Khá",
        Grade.Average => "Trung bình",
        Grade.Weak => "Yếu",
        _ => "Kém",
    };
}
