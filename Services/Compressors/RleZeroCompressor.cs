using System;
using System.Collections.Generic;

namespace final_archiver.Services.Compressors;

public class RleZeroCompressor : CompressorBase
{
    public int MaxRunLength { get; set; } = 255;
    public byte SpecialMarker { get; set; } = 0;
    
    public RleZeroCompressor() : base("RLE Zero Compressor", "RLE сжатие только для нулевых байтов (как в bzip2)")
    {
    }
    
    public RleZeroCompressor(string name, byte marker = 0, int maxRunLength = 255) : base(name, "RLE сжатие для указанных байтов")
    {
        SpecialMarker = marker;
        MaxRunLength = maxRunLength;
    }
    
    public override CompressionResult Compress(byte[] data, CompressorOptions options = null)
    {
        if (data == null)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = "Входные данные не могут быть null"
            };
        }
        
        var startTime = DateTime.Now;
        
        try
        {
            var result = new List<byte>();
            int i = 0;
            int totalCompressed = 0;
            int totalRuns = 0;
            
            while (i < data.Length)
            {
                if (data[i] == SpecialMarker)
                {
                    int count = 0;
                    while (i < data.Length && data[i] == SpecialMarker && count < MaxRunLength)
                    {
                        count++;
                        i++;
                    }
                    
                    result.Add(SpecialMarker);
                    result.Add((byte)count);
                    totalCompressed += count;
                    totalRuns++;
                }
                else
                {
                    result.Add(data[i]);
                    i++;
                }
            }
            
            LastOperationDuration = DateTime.Now - startTime;
            
            var metadata = new Dictionary<string, object>
            {
                ["SpecialMarker"] = SpecialMarker,
                ["MaxRunLength"] = MaxRunLength,
                ["TotalRuns"] = totalRuns,
                ["TotalCompressedBytes"] = totalCompressed,
                ["CompressionEfficiency"] = totalCompressed > 0 ? 
                    (double)totalCompressed / data.Length : 0,
                ["Algorithm"] = "RLE-Zero",
                ["Duration"] = LastOperationDuration.TotalMilliseconds,
                ["InputSize"] = data.Length,
                ["OutputSize"] = result.Count
            };
            
            return new CompressionResult
            {
                Data = result.ToArray(),
                IsSuccess = true,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка RLE сжатия: {ex.Message}"
            };
        }
    }
    
    public override CompressionResult Decompress(byte[] compressedData, CompressorOptions options = null)
    {
        if (compressedData == null)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = "Сжатые данные не могут быть null"
            };
        }
        
        var startTime = DateTime.Now;
        
        try
        {
            var result = new List<byte>();
            int i = 0;
            int totalExpanded = 0;
            
            while (i < compressedData.Length)
            {
                if (compressedData[i] == SpecialMarker && i + 1 < compressedData.Length)
                {
                    int count = compressedData[i + 1];
                    for (int j = 0; j < count; j++)
                    {
                        result.Add(SpecialMarker);
                    }
                    totalExpanded += count;
                    i += 2;
                }
                else
                {
                    result.Add(compressedData[i]);
                    i++;
                }
            }
            
            LastOperationDuration = DateTime.Now - startTime;
            
            var metadata = new Dictionary<string, object>
            {
                ["TotalExpandedBytes"] = totalExpanded,
                ["Duration"] = LastOperationDuration.TotalMilliseconds,
                ["InputSize"] = compressedData.Length,
                ["OutputSize"] = result.Count
            };
            
            return new CompressionResult
            {
                Data = result.ToArray(),
                IsSuccess = true,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка RLE декомпрессии: {ex.Message}"
            };
        }
    }
    
    public static RleZeroCompressor operator *(RleZeroCompressor compressor, int multiplier)
    {
        if (compressor == null) return null;
        
        compressor.MaxRunLength = Math.Min(1024, compressor.MaxRunLength * multiplier);
        return compressor;
    }
    
    public static RleZeroCompressor operator /(RleZeroCompressor compressor, int divisor)
    {
        if (compressor == null) return null;
        
        if (divisor != 0)
        {
            compressor.MaxRunLength = Math.Max(16, compressor.MaxRunLength / divisor);
        }
        return compressor;
    }
    
    public override string ToString()
    {
        return $"RleZeroCompressor: {Name} (Marker: {SpecialMarker}, MaxRun: {MaxRunLength})";
    }
}
