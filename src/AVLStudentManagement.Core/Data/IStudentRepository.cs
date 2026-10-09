using AVLStudentManagement.Core.Models;

namespace AVLStudentManagement.Core.Data;

public interface IStudentRepository
{
    (List<Student> Students, Catalog Catalog) Load();
    /// <summary>Số dòng thật của từng SV sau Load (null thì coi như liên tục từ dòng 2).</summary>
    IReadOnlyList<int>? RealRows => null;

    void Save(IEnumerable<Student> students);
}

/// <summary>Ô trong file bị sai định dạng. Có số dòng và tên cột để người dùng sửa.</summary>
public sealed class DataFormatException : Exception
{
    public int Row { get; }
    public string Column { get; }

    public DataFormatException(int row, string column, string message)
        : base($"Dòng {row}, cột {column}: {message}")
    {
        Row = row;
        Column = column;
    }
}
