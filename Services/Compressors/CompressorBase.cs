using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace final_archiver.Services.Compressors;

public abstract class CompressorBase : IDisposable
{
    public string Name { get; set; }
    public string Description { get; protected set; }
    public bool SupportsParallelProcessing { get; protected set; }
    public TimeSpan LastOperationDuration { get; protected set; }
    
    private bool _disposed = false;
    
    protected CompressorBase()
    {
        Name = "Base Compressor";
        Description = "Базовый класс компрессора";
        SupportsParallelProcessing = false;
    }
    
    protected CompressorBase(string name, string description)
    {
        Name = name;
        Description = description;
        SupportsParallelProcessing = false;
    }
    
    public virtual Task<CompressionResult> CompressAsync(byte[] data, CompressorOptions options = null)
    {
        var startTime = DateTime.Now;
        try
        {
            var result = Compress(data, options);
            LastOperationDuration = DateTime.Now - startTime;
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка сжатия: {ex.Message}"
            });
        }
    }
    
    public virtual Task<CompressionResult> DecompressAsync(byte[] compressedData, CompressorOptions options = null)
    {
        var startTime = DateTime.Now;
        try
        {
            var result = Decompress(compressedData, options);
            LastOperationDuration = DateTime.Now - startTime;
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка декомпрессии: {ex.Message}"
            });
        }
    }
    
    public abstract CompressionResult Compress(byte[] data, CompressorOptions options = null);
    public abstract CompressionResult Decompress(byte[] compressedData, CompressorOptions options = null);
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class CompressionResult
{
    public byte[] Data { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    
    public CompressionResult()
    {
        Data = Array.Empty<byte>();
        IsSuccess = true;
        ErrorMessage = string.Empty;
        Metadata = new Dictionary<string, object>();
    }
    
    public CompressionResult(byte[] data) : this()
    {
        Data = data;
    }
}
