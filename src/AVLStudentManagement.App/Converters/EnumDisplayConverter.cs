using AVLStudentManagement.Core.Models;
using System.Globalization;
using System.Windows.Data;

namespace AVLStudentManagement.App.Converters;

// Đổi giá trị enum thành nhãn tiếng Việt để hiện lên bảng.
public class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Gender)
        {
            return ((Gender)value).ToDisplay();
        }
        if (value is Status)
        {
            return ((Status)value).ToDisplay();
        }
        if (value is Grade)
        {
            return ((Grade)value).ToDisplay();
        }
        if (value == null)
        {
            return "";
        }
        return value.ToString()!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
