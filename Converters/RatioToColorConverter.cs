using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace final_archiver.Converters;

public class RatioToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            if (percentage > 50) return Color.FromArgb("#4CAF50");
            if (percentage > 20) return Color.FromArgb("#8BC34A");
            if (percentage > 0) return Color.FromArgb("#FFC107");
            if (percentage == 0) return Color.FromArgb("#9E9E9E");
            return Color.FromArgb("#F44336");
        }
        
        if (value is float floatPercentage)
            return Convert((double)floatPercentage, targetType, parameter, culture);
            
        return Colors.Black;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}