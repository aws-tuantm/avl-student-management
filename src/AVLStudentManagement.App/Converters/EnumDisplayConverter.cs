using System.Globalization;
using System.Windows.Data;
using AVLStudentManagement.Core.Models;

namespace AVLStudentManagement.App.Converters;

/// <summary>Đổi enum thành nhãn tiếng Việt (gọi ToDisplay của Core).</summary>
public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Gender g => g.ToDisplay(),
        Status t => t.ToDisplay(),
        Grade x => x.ToDisplay(),
        _ => value?.ToString() ?? "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
