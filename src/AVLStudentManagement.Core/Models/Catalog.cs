namespace AVLStudentManagement.Core.Models;

// Danh mục Khoa và các Lớp thuộc khoa đó.
public class Catalog
{
    private readonly Dictionary<string, List<string>> data = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Faculties
    {
        get { return data.Keys; }
    }

    public bool HasFaculty(string faculty)
    {
        return data.ContainsKey(faculty);
    }

    public IReadOnlyList<string> ClassesOf(string faculty)
    {
        List<string>? classes;
        if (data.TryGetValue(faculty, out classes))
        {
            return classes;
        }
        return new List<string>();
    }

    public void Add(string faculty, string className)
    {
        List<string>? classes;
        if (!data.TryGetValue(faculty, out classes))
        {
            classes = new List<string>();
            data[faculty] = classes;
        }

        if (!ContainsIgnoreCase(classes, className))
        {
            classes.Add(className);
        }
    }

    public bool Has(string faculty, string className)
    {
        List<string>? classes;
        if (!data.TryGetValue(faculty, out classes))
        {
            return false;
        }
        return ContainsIgnoreCase(classes, className);
    }

    private static bool ContainsIgnoreCase(List<string> list, string text)
    {
        foreach (string item in list)
        {
            if (item.Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
