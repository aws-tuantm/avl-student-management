using AVLStudentManagement.Core.Data;
using AVLStudentManagement.Core.Services;

namespace AVLStudentManagement.Tests;

[TestClass]
public class SampleFileTests
{
    // Đọc đúng file Excel mà app dùng, chắc chắn đổi tên code không làm hỏng việc nạp dữ liệu.
    // Không kiểm số lượng cố định vì người dùng thêm/xóa/nhập trong app thì file đổi theo.
    [TestMethod]
    public void SampleFile_LoadsAndTreeIsBalanced()
    {
        var path = FindSampleFile();
        var service = new StudentService(new ExcelStudentRepository(path));

        service.Load();

        int count = service.GetAll().Count();
        Assert.IsGreaterThan(0, count);
        Assert.AreEqual(count, service.Tree.Count);
        Assert.IsNotNull(service.TopStudent());
        Assert.IsLessThanOrEqualTo(1.45 * Math.Log2(count + 2), service.Tree.Height); // chiều cao AVL tối đa
    }

    // Đi ngược lên các thư mục cha để tìm file mẫu trong project App
    private static string FindSampleFile()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null)
        {
            var path = Path.Combine(folder.FullName, "AVLStudentManagement.App", "data", "HoSoSinhVien.xlsx");
            if (File.Exists(path)) return path;
            folder = folder.Parent;
        }
        throw new FileNotFoundException("Không tìm thấy HoSoSinhVien.xlsx");
    }
}
