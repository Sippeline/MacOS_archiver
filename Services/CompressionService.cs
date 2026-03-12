using System;
using System.IO;
using System.Threading.Tasks;
using final_archiver.Models;
using final_archiver.Services.Compressors;
using System.Collections.Generic;
using System.Linq;
using final_archiver.Services.Validators;

namespace final_archiver.Services;

public class CompressionService : IDisposable
{
    private bool _disposed = false;
    private readonly PipelineCompressor _defaultPipeline;
    
    public event EventHandler<CompressionProgressEventArgs> ProgressChanged;
    public event EventHandler<CompressionCompletedEventArgs> CompressionCompleted;
    
    public CompressionService()
    {
        _defaultPipeline = CreateDefaultPipeline();
    }
    
    private PipelineCompressor CreateDefaultPipeline()
    {
        var transformers = new List<CompressorBase>
        {
            new BwtCompressor(),
            new MtfCompressor(),
            new RleZeroCompressor()
        };
        
        return new PipelineCompressor(transformers, new CanonicalHuffmanCompressor());
    }
    
    public async Task<Models.CompressionResult> CompressFileAsync(
        string inputPath, 
        string outputPath, 
        CompressorOptions options = null)
    {
        var startTime = DateTime.Now;
        var result = new Models.CompressionResult();
        
        try
        {
            var safeOutputDir = Services.Validators.FileValidator.GetSafeOutputDirectory();
            if (!outputPath.StartsWith(safeOutputDir, StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(outputPath);
                outputPath = Path.Combine(safeOutputDir, fileName);
            }
            
            if (!File.Exists(inputPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Файл не найден: {inputPath}";
                return result;
            }
            
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                try
                {
                    Directory.CreateDirectory(outputDir);
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Не удалось создать директорию: {ex.Message}";
                    return result;
                }
            }
            
            OnProgressChanged(0, "Чтение файла...");
            
            byte[] data;
            try
            {
                data = await File.ReadAllBytesAsync(inputPath);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Ошибка чтения файла: {ex.Message}";
                return result;
            }
            
            result.OriginalSize = data.Length;
            
            string originalExtension = Path.GetExtension(inputPath).ToLower();
            
            string fileType = "binary";
            if (FileValidator.IsImageFile(inputPath)) fileType = "image";
            else if (FileValidator.IsAudioFile(inputPath)) fileType = "audio";
            else if (FileValidator.IsVideoFile(inputPath)) fileType = "video";
            else if (FileValidator.IsTextFile(inputPath)) fileType = "text";
            else if (FileValidator.IsDocumentFile(inputPath)) fileType = "document";
            
            byte[] fileSignature = new byte[64];
            using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = await fs.ReadAsync(fileSignature, 0, Math.Min(64, (int)fs.Length));
                Array.Resize(ref fileSignature, bytesRead);
            }
            
            var formatHeader = new List<byte>();
            formatHeader.AddRange(System.Text.Encoding.UTF8.GetBytes("FARCHV1"));
            formatHeader.AddRange(System.Text.Encoding.UTF8.GetBytes(originalExtension.PadRight(8, '\0').Substring(0, 8)));
            formatHeader.AddRange(System.Text.Encoding.UTF8.GetBytes(fileType.PadRight(12, '\0').Substring(0, 12)));
            formatHeader.Add((byte)fileSignature.Length);
            formatHeader.AddRange(fileSignature);
            
            OnProgressChanged(30, "Сжатие данных...");
            
            var dataWithHeader = new List<byte>();
            dataWithHeader.AddRange(formatHeader);
            dataWithHeader.AddRange(data);
            
            var compressionResult = await _defaultPipeline.CompressAsync(dataWithHeader.ToArray(), options);
            if (!compressionResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Ошибка сжатия: {compressionResult.ErrorMessage}";
                return result;
            }
            
            OnProgressChanged(80, "Сохранение результата...");
            
            try
            {
                await File.WriteAllBytesAsync(outputPath, compressionResult.Data);
            }
            catch (UnauthorizedAccessException ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Нет прав на запись файла: {outputPath}\nОшибка: {ex.Message}";
                return result;
            }
            catch (IOException ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Ошибка ввода-вывода: {ex.Message}";
                return result;
            }
            
            result.CompressedSize = compressionResult.Data.Length;
            result.Duration = DateTime.Now - startTime;
            result.OutputFilePath = outputPath;
            
            if (result.OriginalSize > 0)
            {
                result.CompressionRatio = (double)result.CompressedSize / result.OriginalSize;
                result.CompressionPercentage = (1 - result.CompressionRatio) * 100;
            }
            else
            {
                result.CompressionRatio = 0;
                result.CompressionPercentage = 0;
            }
            
            OnProgressChanged(100, "Сжатие завершено!");
            OnCompressionCompleted(result, true, null);
            
            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Неожиданная ошибка: {ex.Message}";
            OnCompressionCompleted(result, false, ex);
            return result;
        }
    }
    
    public async Task<Models.CompressionResult> DecompressFileAsync(
        string inputPath,
        string outputPath,
        CompressorOptions options = null)
    {
        var startTime = DateTime.Now;
        var result = new Models.CompressionResult();
        
        try
        {
            if (!File.Exists(inputPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Файл не найден: {inputPath}";
                return result;
            }
            
            var safeOutputDir = Services.Validators.FileValidator.GetSafeOutputDirectory();
            if (!outputPath.StartsWith(safeOutputDir, StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(outputPath);
                outputPath = Path.Combine(safeOutputDir, fileName);
            }
            
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                if (!Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    catch (Exception ex)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Не удалось создать директорию: {ex.Message}";
                        return result;
                    }
                }
            }
            
            OnProgressChanged(0, "Чтение сжатого файла...");
            
            byte[] compressedData;
            try
            {
                compressedData = await File.ReadAllBytesAsync(inputPath);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Ошибка чтения файла: {ex.Message}";
                return result;
            }
            
            result.CompressedSize = compressedData.Length;
            
            OnProgressChanged(30, "Распаковка данных...");
            
            var decompressionResult = await _defaultPipeline.DecompressAsync(compressedData, options);
            if (!decompressionResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Ошибка распаковки: {decompressionResult.ErrorMessage}";
                return result;
            }
            
            string originalExtension = "";
            byte[] decompressedData = decompressionResult.Data;
            
            if (decompressedData.Length >= 7 && 
                System.Text.Encoding.UTF8.GetString(decompressedData, 0, 7) == "FARCHV1")
            {
                originalExtension = System.Text.Encoding.UTF8.GetString(decompressedData, 7, 8).TrimEnd('\0');
                
                int headerSize = 7 + 8 + 12 + 1 + decompressedData[7 + 8 + 12];
                decompressedData = decompressedData.Skip(headerSize).ToArray();
            }
            else
            {
                originalExtension = FileValidator.ExtractOriginalFormatFromCompressedFile(inputPath);
            }
            
            OnProgressChanged(80, $"Сохранение {Path.GetFileName(outputPath)}...");
            
            try
            {
                await File.WriteAllBytesAsync(outputPath, decompressedData);
                
                if (!string.IsNullOrEmpty(originalExtension))
                {
                    string lowerExt = originalExtension.ToLower();
                    if (lowerExt == ".png" || lowerExt == ".jpg" || lowerExt == ".jpeg" || lowerExt == ".bmp" || 
                        lowerExt == ".mp3" || lowerExt == ".wav" || lowerExt == ".mp4")
                    {
                        OnProgressChanged(85, "Проверка целостности файла...");
                        
                        var fileInfo = new FileInfo(outputPath);
                        if (fileInfo.Exists && fileInfo.Length > 0)
                        {
                            result.IsSuccess = true;
                        }
                        else
                        {
                            throw new IOException("Файл не был создан или пуст");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Ошибка записи файла: {ex.Message}";
                return result;
            }
            
            result.OriginalSize = decompressedData.Length;
            result.Duration = DateTime.Now - startTime;
            result.OutputFilePath = outputPath;
            
            if (result.CompressedSize > 0)
            {
                result.CompressionRatio = (double)result.OriginalSize / result.CompressedSize;
                result.CompressionPercentage = (1 - (double)result.CompressedSize / result.OriginalSize) * 100;
            }
            else
            {
                result.CompressionRatio = 0;
                result.CompressionPercentage = 0;
            }
            
            OnProgressChanged(100, "Распаковка завершена!");
            OnCompressionCompleted(result, true, null);
            
            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Неожиданная ошибка: {ex.Message}";
            OnCompressionCompleted(result, false, ex);
            return result;
        }
    }
    
    protected virtual void OnProgressChanged(int percentage, string status)
    {
        ProgressChanged?.Invoke(this, new CompressionProgressEventArgs
        {
            Percentage = percentage,
            Status = status
        });
    }
    
    protected virtual void OnCompressionCompleted(Models.CompressionResult result, bool success, Exception error)
    {
        CompressionCompleted?.Invoke(this, new CompressionCompletedEventArgs
        {
            Result = result,
            Success = success,
            Error = error
        });
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _defaultPipeline?.Dispose();
            }
            _disposed = true;
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class CompressionProgressEventArgs : EventArgs
{
    public int Percentage { get; set; }
    public string Status { get; set; }
}

public class CompressionCompletedEventArgs : EventArgs
{
    public Models.CompressionResult Result { get; set; }
    public bool Success { get; set; }
    public Exception Error { get; set; }
}