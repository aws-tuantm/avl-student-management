namespace AVLStudentManagement.Core.Services;

// Lỗi duy nhất của chương trình, có 3 loại ứng với 3 hàm tạo bên dưới.
public class StudentException : Exception
{
    // Lỗi theo từng trường
    public Dictionary<string, string> Errors { get; } = new Dictionary<string, string>();

    public bool IsDuplicate { get; }
    public string Field { get; } = "";
    public string Value { get; } = "";

    // 1. Dữ liệu không hợp lệ
    public StudentException(Dictionary<string, string> errors) : base("Dữ liệu không hợp lệ: " + string.Join("; ", errors.Values))
    {
        Errors = errors;
    }

    // 2. Trùng khóa
    public StudentException(string field, string value) : base($"{field} '{value}' đã tồn tại.")
    {
        IsDuplicate = true;
        Field = field;
        Value = value;
        Errors[field] = Message;
    }

    // 3. File Excel sai định dạng
    public StudentException(int row, string column, string message) : base($"Dòng {row}, cột {column}: {message}")
    {
    }
}
