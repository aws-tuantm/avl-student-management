namespace AVLStudentManagement.Core.Models;

public class Student
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public Gender Gender { get; set; }
    public string NationalId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Faculty { get; set; } = "";
    public Status Status { get; set; }
    public double Gpa { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Xếp loại tính từ Gpa
    public Grade Grade
    {
        get
        {
            if (Gpa >= 9.0)
            {
                return Grade.Excellent;
            }
            if (Gpa >= 8.0)
            {
                return Grade.VeryGood;
            }
            if (Gpa >= 7.0)
            {
                return Grade.Good;
            }
            if (Gpa >= 5.0)
            {
                return Grade.Average;
            }
            if (Gpa >= 4.0)
            {
                return Grade.Weak;
            }
            return Grade.Poor;
        }
    }

    public Student Copy()
    {
        Student copy = new Student();
        copy.StudentId = StudentId;
        copy.FullName = FullName;
        copy.BirthDate = BirthDate;
        copy.Gender = Gender;
        copy.NationalId = NationalId;
        copy.Email = Email;
        copy.Phone = Phone;
        copy.Address = Address;
        copy.ClassName = ClassName;
        copy.Faculty = Faculty;
        copy.Status = Status;
        copy.Gpa = Gpa;
        copy.CreatedAt = CreatedAt;
        copy.UpdatedAt = UpdatedAt;
        return copy;
    }
}
