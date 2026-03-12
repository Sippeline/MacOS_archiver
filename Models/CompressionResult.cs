using System;

namespace final_archiver.Models;

public class CompressionResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    public double CompressionPercentage { get; set; }
    public TimeSpan Duration { get; set; }
    public string OutputFilePath { get; set; }
    
    public CompressionResult()
    {
        IsSuccess = true;
    }
    
    public void CalculateMetrics()
    {
        if (OriginalSize > 0)
        {
            CompressionRatio = (double)CompressedSize / OriginalSize;
            CompressionPercentage = (1 - CompressionRatio) * 100;
        }
    }
}