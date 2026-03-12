using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace final_archiver.Services.Validators;

public static class FileValidator
{
    private static readonly Regex ExtensionRegex = new Regex(
        @"^\.(bz2|arch|comp|zip|gz|7z)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly string[] DangerousExtensions = 
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".sh", 
        ".js", ".vbs", ".jar", ".class", ".py", ".php"
    };
    
    private static readonly long MaxFileSize = 1024 * 1024 * 1024;
    
    public static ValidationResult ValidateInputFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Error("Путь к файлу не может быть пустым");
        
        try
        {
            var fileInfo = new FileInfo(path);
            
            if (!fileInfo.Exists)
                return ValidationResult.Error("Файл не существует");
            
            if (fileInfo.Length == 0)
                return ValidationResult.Error("Файл пуст");
            
            if (fileInfo.Length > MaxFileSize)
                return ValidationResult.Error($"Файл слишком большой (максимум {MaxFileSize / (1024 * 1024 * 1024)} ГБ)");
            
            var extension = Path.GetExtension(path).ToLower();
            if (DangerousExtensions.Contains(extension))
                return ValidationResult.Warning($"Файл с расширением {extension} может быть опасным. Продолжить?");
            
            return ValidationResult.Success(fileInfo);
        }
        catch (UnauthorizedAccessException)
        {
            return ValidationResult.Error("Нет доступа к файлу");
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Ошибка проверки файла: {ex.Message}");
        }
    }
    
    public static ValidationResult ValidateOutputPath(string path, bool allowOverwrite = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Error("Путь для сохранения не может быть пустым");
        
        try
        {
            string directory = Path.GetDirectoryName(path);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch
                {
                    return ValidationResult.Error("Не удалось создать директорию для сохранения");
                }
            }
            
            if (File.Exists(path))
            {
                if (!allowOverwrite)
                    return ValidationResult.Error("Файл уже существует");
                
                var fileInfo = new FileInfo(path);
                if (fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
                    return ValidationResult.Error("Файл доступен только для чтения");
                
                return ValidationResult.Warning("Файл будет перезаписан");
            }
            
            return ValidationResult.Success();
        }
        catch (UnauthorizedAccessException)
        {
            return ValidationResult.Error("Нет прав для записи в указанную директорию");
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Ошибка проверки пути: {ex.Message}");
        }
    }
    
    public static bool IsCompressedFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        var extension = Path.GetExtension(path).ToLower();
        return ExtensionRegex.IsMatch(extension);
    }
    
    public static bool IsImageFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        var extension = Path.GetExtension(path).ToLower();
        return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || 
               extension == ".gif" || extension == ".bmp" || extension == ".tiff" ||
               extension == ".tif" || extension == ".webp" || extension == ".ico" ||
               extension == ".svg" || extension == ".raw" || extension == ".cr2" ||
               extension == ".nef" || extension == ".arw";
    }
    
    public static bool IsAudioFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        var extension = Path.GetExtension(path).ToLower();
        return extension == ".mp3" || extension == ".wav" || extension == ".flac" || 
               extension == ".aac" || extension == ".ogg" || extension == ".m4a" ||
               extension == ".wma" || extension == ".aiff" || extension == ".alac" ||
               extension == ".opus" || extension == ".mid" || extension == ".midi" ||
               extension == ".amr" || extension == ".mka";
    }
    
    public static bool IsVideoFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        var extension = Path.GetExtension(path).ToLower();
        return extension == ".mp4" || extension == ".avi" || extension == ".mkv" || 
               extension == ".mov" || extension == ".wmv" || extension == ".flv" || 
               extension == ".webm" || extension == ".m4v" || extension == ".mpg" ||
               extension == ".mpeg" || extension == ".3gp" || extension == ".3g2" ||
               extension == ".m2ts" || extension == ".ts" || extension == ".mts" ||
               extension == ".vob" || extension == ".ogv" || extension == ".rm" ||
               extension == ".rmvb" || extension == ".asf" || extension == ".divx";
    }
    
    public static bool IsTextFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        var extension = Path.GetExtension(path).ToLower();
        return extension == ".txt" || extension == ".csv" || extension == ".json" ||
               extension == ".xml" || extension == ".html" || extension == ".htm" ||
               extension == ".css" || extension == ".js" || extension == ".cs" ||
               extension == ".cpp" || extension == ".java" || extension == ".py" ||
               extension == ".php" || extension == ".rb" || extension == ".go" ||
               extension == ".rs" || extension == ".swift" || extension == ".kt" ||
               extension == ".md" || extension == ".log" || extension == ".ini" ||
               extension == ".cfg" || extension == ".conf" || extension == ".yaml" ||
               extension == ".yml" || extension == ".toml";
    }
    
    public static bool IsArchiveFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        var extension = Path.GetExtension(path).ToLower();
        return extension == ".zip" || extension == ".rar" || extension == ".7z" ||
               extension == ".tar" || extension == ".gz" || extension == ".bz2" ||
               extension == ".xz" || extension == ".lz" || extension == ".lzma" ||
               extension == ".cab" || extension == ".iso" || extension == ".dmg" ||
               extension == ".pkg" || extension == ".deb" || extension == ".rpm" ||
               extension == ".msi";
    }
    
    public static bool IsDocumentFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        var extension = Path.GetExtension(path).ToLower();
        return extension == ".pdf" || extension == ".doc" || extension == ".docx" ||
               extension == ".xls" || extension == ".xlsx" || extension == ".ppt" ||
               extension == ".pptx" || extension == ".odt" || extension == ".ods" ||
               extension == ".odp" || extension == ".rtf" || extension == ".tex" ||
               extension == ".epub" || extension == ".mobi" || extension == ".fb2" ||
               extension == ".djvu" || extension == ".chm";
    }
    
    public static string GetSafeOutputDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var archiverFolder = Path.Combine(documents, "FinalArchiver");
        
        try
        {
            if (!Directory.Exists(archiverFolder))
                Directory.CreateDirectory(archiverFolder);
            
            return archiverFolder;
        }
        catch
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            archiverFolder = Path.Combine(desktop, "FinalArchiver");
            
            if (!Directory.Exists(archiverFolder))
                Directory.CreateDirectory(archiverFolder);
            
            return archiverFolder;
        }
    }
    
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed_file";
        
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());
        
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "unnamed_file";
        
        if (sanitized.Length > 255)
            sanitized = sanitized.Substring(0, 255);
        
        return sanitized.Trim();
    }
    
    public static string GenerateUniqueFileName(string directory, string baseName, string extension)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        string path = Path.Combine(directory, $"{baseName}{extension}");
        
        if (!File.Exists(path))
            return path;
        
        int counter = 1;
        while (counter < 1000)
        {
            path = Path.Combine(directory, $"{baseName}_{counter}{extension}");
            if (!File.Exists(path))
                return path;
            counter++;
        }
        
        return Path.Combine(directory, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }
    
    public static string GetFileTypeDescription(string path)
    {
        if (IsImageFile(path)) return "Изображение";
        if (IsAudioFile(path)) return "Аудиофайл";
        if (IsVideoFile(path)) return "Видеофайл";
        if (IsTextFile(path)) return "Текстовый файл";
        if (IsArchiveFile(path)) return "Архив";
        if (IsDocumentFile(path)) return "Документ";
        if (IsCompressedFile(path)) return "Сжатый архив (BWT+MTF+RLE+Huffman)";
        return "Двоичный файл";
    }
    
    public static long GetFileSizeInBytes(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
        }
        catch
        {
        }
        return 0;
    }
    
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F2} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }
    
    public static string ExtractOriginalFormatFromCompressedFile(string compressedFilePath)
    {
        if (!IsCompressedFile(compressedFilePath))
            return "";
        
        try
        {
            var data = File.ReadAllBytes(compressedFilePath);
            
            if (data.Length >= 7 && System.Text.Encoding.UTF8.GetString(data, 0, 7) == "FARCHV1")
            {
                string originalExtension = System.Text.Encoding.UTF8.GetString(data, 7, 8).TrimEnd('\0');
                if (!string.IsNullOrEmpty(originalExtension) && originalExtension != "\0\0\0\0\0\0\0\0")
                    return originalExtension;
            }
            
            if (data.Length < 10) return "";
            
            if (data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return ".png";
            
            if (data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return ".jpg";
            
            if (data.Length > 2 && data[0] == 0x42 && data[1] == 0x4D)
                return ".bmp";
            
            if (data.Length > 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data[8] == 0x57 && data[9] == 0x41 && data[10] == 0x56 && data[11] == 0x45)
                return ".wav";
            
            if (data.Length > 3 && 
                ((data[0] == 0x49 && data[1] == 0x44 && data[2] == 0x33) ||
                 (data[0] == 0xFF && (data[1] == 0xFB || data[1] == 0xF3 || data[1] == 0xF2))))
                return ".mp3";
            
            if (data.Length > 12 && 
                (data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70))
                return ".mp4";
            
            if (data.Length > 4 && 
                ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00) ||
                 (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A)))
                return ".cr2";
        }
        catch
        {
        }
        
        return "";
    }
    
    public static bool TryDetectOriginalFormat(string compressedFilePath, out string originalExtension)
    {
        originalExtension = ExtractOriginalFormatFromCompressedFile(compressedFilePath);
        return !string.IsNullOrEmpty(originalExtension);
    }
}

public class ValidationResult
{
    public bool IsValid { get; }
    public string Message { get; }
    public bool IsWarning { get; }
    public object Data { get; }
    
    private ValidationResult(bool isValid, string message, bool isWarning, object data = null)
    {
        IsValid = isValid;
        Message = message;
        IsWarning = isWarning;
        Data = data;
    }
    
    public static ValidationResult Success(object data = null) => 
        new ValidationResult(true, string.Empty, false, data);
    
    public static ValidationResult Error(string message) => 
        new ValidationResult(false, message, false);
    
    public static ValidationResult Warning(string message) => 
        new ValidationResult(true, message, true);
    
    public T GetData<T>() where T : class
    {
        return Data as T;
    }
}