namespace AVLStudentManagement.Core.Models;

/// <summary>Danh mục Khoa và các Lớp thuộc khoa đó.</summary>
public sealed class Catalog
{
    private readonly Dictionary<string, List<string>> data = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Faculties => data.Keys;

    public IReadOnlyList<string> ClassesOf(string faculty) =>
        data.TryGetValue(faculty, out var list) ? list : Array.Empty<string>();

    public void Add(string faculty, string className)
    {
        if (!data.TryGetValue(faculty, out var list)) data[faculty] = list = new List<string>();
        if (!list.Contains(className, StringComparer.OrdinalIgnoreCase)) list.Add(className);
    }

    /// <summary>Lớp có thuộc đúng khoa không.</summary>
    public bool Has(string faculty, string className) =>
        data.TryGetValue(faculty, out var list) && list.Contains(className, StringComparer.OrdinalIgnoreCase);
}
