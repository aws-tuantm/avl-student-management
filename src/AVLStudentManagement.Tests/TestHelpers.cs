using AVLStudentManagement.Core.Models;

namespace AVLStudentManagement.Tests;

public static class TestHelpers
{
    // Tạo bản sao sinh viên rồi sửa vài trường: student.Change(s => { s.Gpa = 3; })
    public static Student Change(this Student student, Action<Student> edit)
    {
        Student copy = student.Copy();
        edit(copy);
        return copy;
    }

    // So sánh từng trường của hai sinh viên
    public static void AssertSame(Student expected, Student actual)
    {
        Assert.AreEqual(expected.StudentId, actual.StudentId);
        Assert.AreEqual(expected.FullName, actual.FullName);
        Assert.AreEqual(expected.BirthDate, actual.BirthDate);
        Assert.AreEqual(expected.Gender, actual.Gender);
        Assert.AreEqual(expected.NationalId, actual.NationalId);
        Assert.AreEqual(expected.Email, actual.Email);
        Assert.AreEqual(expected.Phone, actual.Phone);
        Assert.AreEqual(expected.Address, actual.Address);
        Assert.AreEqual(expected.ClassName, actual.ClassName);
        Assert.AreEqual(expected.Faculty, actual.Faculty);
        Assert.AreEqual(expected.Status, actual.Status);
        Assert.AreEqual(expected.Gpa, actual.Gpa);
        Assert.AreEqual(expected.CreatedAt, actual.CreatedAt);
        Assert.AreEqual(expected.UpdatedAt, actual.UpdatedAt);
    }
}
