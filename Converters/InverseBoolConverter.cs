using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace final_archiver.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var inverted = !boolValue;
            
            if (parameter != null && targetType == typeof(double))
            {
                return inverted ? 1.0 : 0.6;
            }
            
            return inverted;
        }
        return value;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}