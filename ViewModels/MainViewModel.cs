using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using final_archiver.Models;
using final_archiver.Services;
using final_archiver.Services.Compressors;
using final_archiver.Services.Validators;
using Microsoft.Maui.ApplicationModel;

namespace final_archiver.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly CompressionService _compressionService;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompressFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecompressFileCommand))]
    private string _inputFilePath;
    
    [ObservableProperty]
    private string _outputFilePath;
    
    [ObservableProperty]
    private bool _isProcessing;
    
    [ObservableProperty]
    private string _statusMessage;
    
    [ObservableProperty]
    private double _compressionRatio;
    
    [ObservableProperty]
    private double _compressionPercentage;
    
    [ObservableProperty]
    private long _originalSize;
    
    [ObservableProperty]
    private long _compressedSize;
    
    [ObservableProperty]
    private int _progressPercentage;
    
    [ObservableProperty]
    private bool _showProgress;
    
    [ObservableProperty]
    private ObservableCollection<CompressionPipeline> _availablePipelines;
    
    [ObservableProperty]
    private CompressionPipeline _selectedPipeline;
    
    [ObservableProperty]
    private string _currentOperation;
    
    [ObservableProperty]
    private string _fileInfo;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableCompressedFormats;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableDecompressedFormats;
    
    [ObservableProperty]
    private ObservableCollection<string> _currentAvailableFormats;
    
    [ObservableProperty]
    private string _selectedFormat;
    
    [ObservableProperty]
    private string _compressionDetails;
    
    private string _originalExtension = "";
    private string _lastDetectedFormat = "";
    private bool _isCompressedFile = false;
    
    public MainViewModel()
    {
        _compressionService = new CompressionService();
        _compressionService.ProgressChanged += OnProgressChanged;
        _compressionService.CompressionCompleted += OnCompressionCompleted;
        
        InitializeProperties();
    }
    
    private void InitializeProperties()
    {
        StatusMessage = "Выберите файл для сжатия или распаковки";
        FileInfo = "Информация о файле появится здесь";
        CompressionDetails = "Детали сжатия появятся здесь";
        InitializePipelines();
        InitializeFormats();
        CurrentAvailableFormats = AvailableDecompressedFormats;
        SelectedFormat = AvailableDecompressedFormats.First();
    }
    
    private void InitializePipelines()
    {
        AvailablePipelines = new ObservableCollection<CompressionPipeline>
        {
            new CompressionPipeline("BZip2 (BWT+MTF+RLE+Huffman)", new [] { "BWT", "MTF", "RLE", "Huffman" }.ToList()),
            new CompressionPipeline("RLE+Huffman", new [] { "RLE", "Huffman" }.ToList()),
            new CompressionPipeline("Только Huffman", new [] { "Huffman" }.ToList())
        };
        
        SelectedPipeline = AvailablePipelines.First();
    }
    
    private void InitializeFormats()
    {
        AvailableCompressedFormats = new ObservableCollection<string>
        {
            ".bz2", ".arch", ".comp"
        };
        
        AvailableDecompressedFormats = new ObservableCollection<string>
        {
            ".bin", ".dat", ".out", ".txt",
            ".mp3", ".mp4", ".wav", ".flac", ".aac", ".ogg",
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".webp",
            ".cr2", ".raw", ".nef", ".arw", ".dng"
        };
        
        SelectedFormat = AvailableDecompressedFormats.First();
    }
    
    private void OnProgressChanged(object sender, CompressionProgressEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ProgressPercentage = e.Percentage;
            CurrentOperation = e.Status;
            ShowProgress = true;
        });
    }
    
    private void OnCompressionCompleted(object sender, CompressionCompletedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ShowProgress = false;
            IsProcessing = false;
            
            if (e.Success)
            {
                var originalSize = e.Result.OriginalSize;
                var compressedSize = e.Result.CompressedSize;
                var percentage = e.Result.CompressionPercentage;
                
                string sizeInfo;
                string details;
                
                if (_isCompressedFile)
                {
                    var decompressionRatio = compressedSize > 0 ? (double)originalSize / compressedSize : 0;
                    sizeInfo = $"📈 Размер увеличен на: {Math.Abs(percentage):F1}%";
                    details = $"📦 Исходный архив: {FormatFileSize(compressedSize)}\n" +
                             $"📄 Распакованный файл: {FormatFileSize(originalSize)}\n" +
                             $"📊 Коэффициент сжатия: 1:{decompressionRatio:N1}";
                }
                else
                {
                    var compressionRatioValue = originalSize > 0 ? (double)compressedSize / originalSize : 0;
                    
                    if (percentage < 0)
                    {
                        sizeInfo = $"⚠️ Размер увеличен на: {Math.Abs(percentage):F1}%";
                        details = $"📄 Исходный файл: {FormatFileSize(originalSize)}\n" +
                                 $"⚠️ Сжатый архив: {FormatFileSize(compressedSize)}\n" +
                                 $"📊 Для изображений лучше использовать специализированные форматы (JPEG, PNG)";
                    }
                    else
                    {
                        sizeInfo = $"📉 Размер уменьшен на: {percentage:F1}%";
                        details = $"📄 Исходный файл: {FormatFileSize(originalSize)}\n" +
                                 $"🗜️ Сжатый архив: {FormatFileSize(compressedSize)}\n" +
                                 $"📊 Коэффициент сжатия: 1:{compressionRatioValue:N2}";
                    }
                }
                
                string outputFileName = Path.GetFileName(e.Result.OutputFilePath);
                string fileType = FileValidator.GetFileTypeDescription(e.Result.OutputFilePath);
                
                StatusMessage = $"✓ Операция завершена успешно!\n" +
                              $"{sizeInfo}\n" +
                              $"💾 Файл сохранен: {outputFileName}\n" +
                              $"📁 Тип: {fileType}";
                
                CompressionDetails = details;
                CompressionRatio = e.Result.CompressionRatio;
                CompressionPercentage = e.Result.CompressionPercentage;
                CompressedSize = e.Result.CompressedSize;
                OriginalSize = e.Result.OriginalSize;
            }
            else
            {
                StatusMessage = $"✗ Ошибка: {e.Error?.Message}";
                CompressionDetails = "Операция завершилась с ошибкой";
            }
        });
    }
    
    private string GetDefaultOutputDirectory()
    {
        return FileValidator.GetSafeOutputDirectory();
    }
    
    private string GenerateOutputPath(string inputPath, bool isCompression, string forcedExtension = null)
    {
        var outputDir = GetDefaultOutputDirectory();
        var inputName = Path.GetFileNameWithoutExtension(inputPath);
        
        var safeName = FileValidator.SanitizeFileName(inputName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "file";
        
        safeName = safeName.Replace("_compressed", "").Replace("_decompressed", "").Replace("_c", "").Replace("_d", "");
        
        string format;
        
        if (isCompression)
        {
            format = SelectedFormat;
            if (string.IsNullOrEmpty(format))
                format = ".bz2";
        }
        else
        {
            if (!string.IsNullOrEmpty(forcedExtension))
            {
                format = forcedExtension;
            }
            else if (!string.IsNullOrEmpty(SelectedFormat))
            {
                format = SelectedFormat;
            }
            else
            {
                format = ".bin";
            }
        }
        
        if (!format.StartsWith("."))
        {
            format = "." + format;
        }
        
        string baseFileName = isCompression 
            ? $"{safeName}_compressed{format}"
            : $"{safeName}_decompressed{format}";
        
        var path = Path.Combine(outputDir, baseFileName);
        
        if (File.Exists(path))
        {
            for (int i = 1; i < 100; i++)
            {
                string numberedName = isCompression 
                    ? $"{safeName}_compressed{i}{format}"
                    : $"{safeName}_decompressed{i}{format}";
                
                path = Path.Combine(outputDir, numberedName);
                
                if (!File.Exists(path))
                    break;
            }
        }
        
        return path;
    }
    
    [RelayCommand]
    private async Task SelectUncompressedFile()
    {
        try
        {
            var folderPath = GetDefaultOutputDirectory();
            
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                StatusMessage = $"Создана папка архиватора: {folderPath}\nВ папке пока нет файлов.";
                return;
            }
            
            var allFiles = Directory.GetFiles(folderPath);
            var uncompressedFiles = allFiles
                .Where(f => !FileValidator.IsCompressedFile(f))
                .Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileSelectionItem
                {
                    Name = Path.GetFileName(f),
                    FullPath = f,
                    Size = new FileInfo(f).Length,
                    Extension = Path.GetExtension(f)
                })
                .ToList();
            
            if (uncompressedFiles.Count == 0)
            {
                StatusMessage = "В папке архиватора нет несжатых файлов";
                return;
            }
            
            var fileOptions = uncompressedFiles
                .Select(f => $"{f.Name} ({FormatFileSize(f.Size)})")
                .ToArray();
            
            string selectedOption = await Application.Current.MainPage.DisplayActionSheet(
                "Выберите несжатый файл для сжатия",
                "Отмена",
                null,
                fileOptions);
            
            if (!string.IsNullOrEmpty(selectedOption) && selectedOption != "Отмена")
            {
                var selectedFileName = selectedOption.Split(' ')[0];
                var selectedFile = uncompressedFiles.FirstOrDefault(f => f.Name == selectedFileName);
                
                if (selectedFile != null)
                {
                    InputFilePath = selectedFile.FullPath;
                    ValidateInputFileCommand.Execute(null);
                    StatusMessage = $"Выбран файл для сжатия: {selectedFile.Name}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка выбора файла: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private async Task SelectCompressedFile()
    {
        try
        {
            var folderPath = GetDefaultOutputDirectory();
            
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                StatusMessage = $"Создана папка архиватора: {folderPath}\nВ папке пока нет файлов.";
                return;
            }
            
            var compressedFiles = Directory.GetFiles(folderPath, "*.bz2")
                .Concat(Directory.GetFiles(folderPath, "*.arch"))
                .Concat(Directory.GetFiles(folderPath, "*.comp"))
                .Select(f => new FileSelectionItem
                {
                    Name = Path.GetFileName(f),
                    FullPath = f,
                    Size = new FileInfo(f).Length
                })
                .ToList();
            
            if (compressedFiles.Count == 0)
            {
                StatusMessage = "В папке архиватора нет сжатых файлов";
                return;
            }
            
            var fileOptions = compressedFiles
                .Select(f => $"{f.Name} ({FormatFileSize(f.Size)})")
                .ToArray();
            
            string selectedOption = await Application.Current.MainPage.DisplayActionSheet(
                "Выберите сжатый файл для распаковки",
                "Отмена",
                null,
                fileOptions);
            
            if (!string.IsNullOrEmpty(selectedOption) && selectedOption != "Отмена")
            {
                var selectedFileName = selectedOption.Split(' ')[0];
                var selectedFile = compressedFiles.FirstOrDefault(f => f.Name == selectedFileName);
                
                if (selectedFile != null)
                {
                    InputFilePath = selectedFile.FullPath;
                    ValidateInputFileCommand.Execute(null);
                    StatusMessage = $"Выбран сжатый файл для распаковки: {selectedFile.Name}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка выбора файла: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void ValidateInputFile()
    {
        if (string.IsNullOrWhiteSpace(InputFilePath))
        {
            FileInfo = "Выберите файл";
            StatusMessage = "Файл не выбран";
            CompressionDetails = "Детали сжатия появятся здесь";
            return;
        }
        
        try
        {
            if (File.Exists(InputFilePath))
            {
                var fileInfo = new FileInfo(InputFilePath);
                OriginalSize = fileInfo.Length;
                
                _isCompressedFile = FileValidator.IsCompressedFile(InputFilePath);
                _originalExtension = Path.GetExtension(InputFilePath).ToLower();
                
                string fileType = _isCompressedFile ? "Сжатый архив" : "Обычный файл";
                
                if (FileValidator.IsImageFile(InputFilePath))
                {
                    fileType = "Изображение";
                }
                else if (FileValidator.IsAudioFile(InputFilePath))
                {
                    fileType = "Аудиофайл";
                }
                else if (FileValidator.IsVideoFile(InputFilePath))
                {
                    fileType = "Видеофайл";
                }
                else if (_isCompressedFile)
                {
                    fileType = "Архив BWT+MTF+RLE+Huffman";
                }
                
                FileInfo = $"✓ Файл найден\n" +
                          $"Имя: {Path.GetFileName(InputFilePath)}\n" +
                          $"Размер: {FormatFileSize(OriginalSize)}\n" +
                          $"Тип: {fileType}\n" +
                          $"Расширение: {_originalExtension}\n" +
                          $"Дата: {fileInfo.LastWriteTime:dd.MM.yyyy HH:mm}";
                
                if (_isCompressedFile)
                {
                    CurrentAvailableFormats = AvailableDecompressedFormats;
                    SelectedFormat = AvailableDecompressedFormats.First();
                    
                    string detectedFormat = FileValidator.ExtractOriginalFormatFromCompressedFile(InputFilePath);
                    if (!string.IsNullOrEmpty(detectedFormat))
                    {
                        _lastDetectedFormat = detectedFormat;
                        StatusMessage = $"Сжатый файл готов к распаковке\nОпределен возможный формат: {detectedFormat}\nВыберите желаемый формат сохранения";
                    }
                    else
                    {
                        StatusMessage = $"Сжатый файл готов к распаковке\nВыберите формат для сохранения файла";
                    }
                    CompressionDetails = $"Готов к распаковке\nИсходный размер: {FormatFileSize(OriginalSize)}\nВыбранный формат: {SelectedFormat}";
                }
                else
                {
                    CurrentAvailableFormats = AvailableCompressedFormats;
                    SelectedFormat = AvailableCompressedFormats.First();
                    
                    if (FileValidator.IsImageFile(InputFilePath))
                    {
                        StatusMessage = $"⚠️ Изображение готово к сжатию\nBWT+MTF+RLE+Huffman может увеличить размер изображений";
                        CompressionDetails = $"⚠️ Для изображений алгоритм BWT может увеличить размер\nИсходный размер: {FormatFileSize(OriginalSize)}";
                    }
                    else if (FileValidator.IsAudioFile(InputFilePath))
                    {
                        StatusMessage = $"⚠️ Аудиофайл готов к сжатию\nBWT+MTF+RLE+Huffman может увеличить размер аудиофайлов";
                        CompressionDetails = $"⚠️ Для аудиофайлов алгоритм BWT может увеличить размер\nИсходный размер: {FormatFileSize(OriginalSize)}";
                    }
                    else if (FileValidator.IsVideoFile(InputFilePath))
                    {
                        StatusMessage = $"⚠️ Видеофайл готов к сжатию\nBWT+MTF+RLE+Huffman может увеличить размер видеофайлов";
                        CompressionDetails = $"⚠️ Для видеофайлов алгоритм BWT может увеличить размер\nИсходный размер: {FormatFileSize(OriginalSize)}";
                    }
                    else
                    {
                        StatusMessage = $"Файл готов к сжатию\nВыберите формат для сжатого файла";
                        CompressionDetails = $"Готов к сжатию\nИсходный размер: {FormatFileSize(OriginalSize)}";
                    }
                }
                
                OutputFilePath = GenerateOutputPath(InputFilePath, !_isCompressedFile, null);
            }
            else
            {
                FileInfo = "✗ Файл не найден";
                StatusMessage = "Указанный файл не существует";
                CompressionDetails = "Файл не найден";
            }
        }
        catch (Exception ex)
        {
            FileInfo = $"✗ Ошибка: {ex.Message}";
            StatusMessage = "Не удалось прочитать информацию о файле";
            CompressionDetails = "Ошибка чтения файла";
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanCompressFile))]
    private async Task CompressFile()
    {
        if (!CanCompressFile())
        {
            StatusMessage = "Невозможно выполнить сжатие";
            return;
        }
        
        IsProcessing = true;
        StatusMessage = "Начало сжатия...";
        
        if (FileValidator.IsImageFile(InputFilePath))
        {
            CompressionDetails = $"⚠️ Сжатие изображения: {Path.GetFileName(InputFilePath)}\nИсходный размер: {FormatFileSize(OriginalSize)}\n⚠️ BWT+MTF+RLE+Huffman может увеличить размер изображений";
        }
        else if (FileValidator.IsAudioFile(InputFilePath))
        {
            CompressionDetails = $"⚠️ Сжатие аудиофайла: {Path.GetFileName(InputFilePath)}\nИсходный размер: {FormatFileSize(OriginalSize)}\n⚠️ BWT+MTF+RLE+Huffman может увеличить размер аудиофайлов";
        }
        else if (FileValidator.IsVideoFile(InputFilePath))
        {
            CompressionDetails = $"⚠️ Сжатие видеофайла: {Path.GetFileName(InputFilePath)}\nИсходный размер: {FormatFileSize(OriginalSize)}\n⚠️ BWT+MTF+RLE+Huffman может увеличить размер видеофайлов";
        }
        else
        {
            CompressionDetails = $"Сжатие файла: {Path.GetFileName(InputFilePath)}\nИсходный размер: {FormatFileSize(OriginalSize)}";
        }
        
        try
        {
            var outputDir = Path.GetDirectoryName(OutputFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            var options = new CompressorOptions();
            
            var result = await _compressionService.CompressFileAsync(InputFilePath, OutputFilePath, options);
            
            if (result.IsSuccess)
            {
                ValidateInputFileCommand.Execute(null);
            }
            else
            {
                StatusMessage = $"✗ Ошибка сжатия: {result.ErrorMessage}";
                CompressionDetails = "Сжатие завершилось с ошибкой";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Ошибка: {ex.Message}";
            CompressionDetails = "Неожиданная ошибка при сжатии";
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanDecompressFile))]
    private async Task DecompressFile()
    {
        if (!CanDecompressFile())
        {
            StatusMessage = "Невозможно выполнить распаковку";
            return;
        }
        
        IsProcessing = true;
        StatusMessage = "Начало распаковки...";
        
        string detectedFormat = FileValidator.ExtractOriginalFormatFromCompressedFile(InputFilePath);
        _lastDetectedFormat = detectedFormat;
        
        string selectedExtension = SelectedFormat;
        if (!string.IsNullOrEmpty(selectedExtension))
        {
            if (!selectedExtension.StartsWith("."))
            {
                selectedExtension = "." + selectedExtension;
            }
            
            CompressionDetails = $"Распаковка архива: {Path.GetFileName(InputFilePath)}\n" +
                               $"Исходный размер: {FormatFileSize(OriginalSize)}\n" +
                               $"Файл будет сохранен в формате: {selectedExtension}";
            
            if (!string.IsNullOrEmpty(detectedFormat))
            {
                CompressionDetails += $"\nОпределен возможный оригинальный формат: {detectedFormat}";
            }
        }
        else
        {
            CompressionDetails = $"Распаковка архива: {Path.GetFileName(InputFilePath)}\n" +
                               $"Исходный размер: {FormatFileSize(OriginalSize)}";
        }
        
        try
        {
            OutputFilePath = GenerateOutputPath(InputFilePath, false, selectedExtension);
            
            var outputDir = Path.GetDirectoryName(OutputFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            var options = new CompressorOptions();
            
            var result = await _compressionService.DecompressFileAsync(InputFilePath, OutputFilePath, options);
            
            if (result.IsSuccess)
            {
                var fileInfo = new FileInfo(OutputFilePath);
                if (fileInfo.Exists)
                {
                    string fileType = FileValidator.GetFileTypeDescription(OutputFilePath);
                    string actualExtension = Path.GetExtension(OutputFilePath);
                    StatusMessage = $"✓ Распаковка завершена успешно!\n" +
                                  $"Файл сохранен как: {Path.GetFileName(OutputFilePath)}\n" +
                                  $"Тип: {fileType}\n" +
                                  $"Формат: {actualExtension}\n" +
                                  $"Размер: {FormatFileSize(fileInfo.Length)}";
                }
                
                ValidateInputFileCommand.Execute(null);
            }
            else
            {
                StatusMessage = $"✗ Ошибка распаковки: {result.ErrorMessage}";
                CompressionDetails = "Распаковка завершилась с ошибкой";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Ошибка: {ex.Message}";
            CompressionDetails = "Неожиданная ошибка при распаковке";
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    private bool CanCompressFile()
    {
        return !string.IsNullOrWhiteSpace(InputFilePath) && 
               File.Exists(InputFilePath) &&
               !IsProcessing &&
               !FileValidator.IsCompressedFile(InputFilePath);
    }
    
    private bool CanDecompressFile()
    {
        return !string.IsNullOrWhiteSpace(InputFilePath) && 
               File.Exists(InputFilePath) &&
               !IsProcessing &&
               FileValidator.IsCompressedFile(InputFilePath);
    }
    
    [RelayCommand]
    private void UpdateOutputPath()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(InputFilePath) || !File.Exists(InputFilePath))
            {
                StatusMessage = "Сначала выберите файл";
                return;
            }
            
            var isCompressed = FileValidator.IsCompressedFile(InputFilePath);
            string forcedExtension = isCompressed ? SelectedFormat : null;
            OutputFilePath = GenerateOutputPath(InputFilePath, !isCompressed, forcedExtension);
            StatusMessage = $"Путь сохранения обновлен: {Path.GetFileName(OutputFilePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void UpdateFormat()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(InputFilePath) || !File.Exists(InputFilePath))
            {
                StatusMessage = "Сначала выберите файл";
                return;
            }
            
            var isCompressed = FileValidator.IsCompressedFile(InputFilePath);
            if (isCompressed)
            {
                StatusMessage = $"Формат для распаковки изменен на: {SelectedFormat}";
                CompressionDetails = $"Выбран формат сохранения: {SelectedFormat}\nНажмите 'Обновить путь' для применения";
            }
            else
            {
                OutputFilePath = GenerateOutputPath(InputFilePath, true, null);
                StatusMessage = $"Формат архива изменен на: {SelectedFormat}\nПуть сохранения обновлен";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void OpenArchiveFolder()
    {
        try
        {
            var folderPath = GetDefaultOutputDirectory();
            if (Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start("open", $"\"{folderPath}\"");
                StatusMessage = $"✓ Открыта папка архиватора: {folderPath}";
            }
            else
            {
                Directory.CreateDirectory(folderPath);
                System.Diagnostics.Process.Start("open", $"\"{folderPath}\"");
                StatusMessage = $"✓ Создана и открыта папка архиватора: {folderPath}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Ошибка: {ex.Message}";
        }
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double len = bytes;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
    
    private class FileSelectionItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; }
    }
}

//очень объемно, можно упростить наверняка