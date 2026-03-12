using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace final_archiver.Converters;

public class BoolToFormatTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompressed)
        {
            return isCompressed ? "Формат для распакованного файла:" : "Формат архива:";
        }
        return "Формат файла:";
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}