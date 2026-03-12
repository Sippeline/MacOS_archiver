using System.Text.RegularExpressions;

namespace final_archiver.Utils;

public static class RegularExpressions
{
    public static readonly Regex FileName = new Regex(
        @"^[\w\-. ]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    public static readonly Regex Numeric = new Regex(
        @"^\d+$",
        RegexOptions.Compiled);
    
    public static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        
        return FileName.IsMatch(fileName) && 
               fileName.Length <= 255 &&
               !fileName.EndsWith(".") &&
               !fileName.EndsWith(" ");
    }
    
    public static bool IsNumeric(string input)
    {
        return !string.IsNullOrWhiteSpace(input) && Numeric.IsMatch(input);
    }
    
    public static string SanitizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        
        return Regex.Replace(input, @"[^\w\-. ]", "");
    }
    
    public static string ExtractFileNameFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        
        var match = Regex.Match(path, @"[^\\/]+$");
        return match.Success ? match.Value : string.Empty;
    }
    
    public static string ExtractFileExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;
        
        var match = Regex.Match(fileName, @"\.([^.]+)$");
        return match.Success ? match.Groups[1].Value.ToLower() : string.Empty;
    }
}
