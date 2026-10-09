using AVLStudentManagement.Core.Models;

namespace AVLStudentManagement.Core.Data;

public interface IStudentRepository
{
    Catalog Catalog { get; }

    List<int>? RealRows { get; }

    List<Student> Load();

    void Save(List<Student> students);
}

