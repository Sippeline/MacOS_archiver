using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace final_archiver.Services.Compressors;

public class PipelineCompressor : CompressorBase
{
    private List<CompressorBase> _transformers;
    private CanonicalHuffmanCompressor _finalCompressor;
    
    public List<CompressorBase> Transformers => new List<CompressorBase>(_transformers);
    
    public CanonicalHuffmanCompressor FinalCompressor
    {
        get => _finalCompressor;
        set => _finalCompressor = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    public string PipelineDescription
    {
        get
        {
            var names = _transformers.Select(t => t.Name).ToList();
            names.Add(_finalCompressor.Name);
            return string.Join(" → ", names);
        }
    }
    
    public PipelineCompressor(List<CompressorBase> transformers, CanonicalHuffmanCompressor finalCompressor)
        : base("Pipeline Compressor", "Конвейерная компрессия")
    {
        _transformers = transformers?.ToList() ?? new List<CompressorBase>();
        _finalCompressor = finalCompressor ?? throw new ArgumentNullException(nameof(finalCompressor));
        SupportsParallelProcessing = _transformers.All(t => t.SupportsParallelProcessing);
        UpdateDescription();
    }
    
    public PipelineCompressor() 
        : this(new List<CompressorBase>(), new CanonicalHuffmanCompressor())
    {
    }
    
    public PipelineCompressor(PipelineCompressor other)
    {
        _transformers = other._transformers.Select(CreateDeepCopy).ToList();
        _finalCompressor = new CanonicalHuffmanCompressor(other._finalCompressor.Name, other._finalCompressor.UseCanonicalForm);
        Name = other.Name;
        Description = other.Description;
        SupportsParallelProcessing = other.SupportsParallelProcessing;
    }
    
    private void UpdateDescription()
    {
        Description = $"Конвейерная компрессия: {PipelineDescription}";
    }
    
    private CompressorBase CreateDeepCopy(CompressorBase compressor)
    {
        return compressor switch
        {
            BwtCompressor bwt => new BwtCompressor(bwt),
            MtfCompressor mtf => new MtfCompressor(mtf.Name, mtf.AlphabetSize),
            RleZeroCompressor rle => new RleZeroCompressor(rle.Name, rle.SpecialMarker, rle.MaxRunLength),
            CanonicalHuffmanCompressor huff => new CanonicalHuffmanCompressor(huff.Name, huff.UseCanonicalForm),
            PipelineCompressor pipe => new PipelineCompressor(pipe),
            _ => throw new InvalidOperationException($"Неизвестный тип компрессора: {compressor.GetType()}")
        };
    }
    
    public PipelineCompressor CreateBZip2Pipeline()
    {
        var transformers = new List<CompressorBase>
        {
            new BwtCompressor("BWT"),
            new MtfCompressor("MTF"),
            new RleZeroCompressor("RLE-Zero", 0, 255)
        };
        
        return new PipelineCompressor(transformers, new CanonicalHuffmanCompressor("Canonical Huffman", true));
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
            byte[] currentData = data;
            var stageResults = new List<Dictionary<string, object>>();
            var totalMetadata = new Dictionary<string, object>();
            
            totalMetadata["OriginalSize"] = data.Length;
            totalMetadata["PipelineStages"] = _transformers.Count + 1;
            
            int stage = 1;
            foreach (var transformer in _transformers)
            {
                var result = transformer.Compress(currentData, options);
                if (!result.IsSuccess)
                {
                    return new CompressionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Ошибка на этапе {stage} ({transformer.Name}): {result.ErrorMessage}"
                    };
                }
                
                stageResults.Add(new Dictionary<string, object>
                {
                    ["Stage"] = stage,
                    ["Compressor"] = transformer.Name,
                    ["InputSize"] = currentData.Length,
                    ["OutputSize"] = result.Data.Length,
                    ["Ratio"] = currentData.Length > 0 ? (double)result.Data.Length / currentData.Length : 0,
                    ["Compression"] = currentData.Length > 0 ? 
                        (1 - (double)result.Data.Length / currentData.Length) * 100 : 0,
                    ["Duration"] = transformer.LastOperationDuration.TotalMilliseconds
                });
                
                currentData = result.Data;
                stage++;
            }
            
            var finalResult = _finalCompressor.Compress(currentData, options);
            if (!finalResult.IsSuccess)
            {
                return new CompressionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Ошибка на финальном этапе ({_finalCompressor.Name}): {finalResult.ErrorMessage}"
                };
            }
            
            stageResults.Add(new Dictionary<string, object>
            {
                ["Stage"] = stage,
                ["Compressor"] = _finalCompressor.Name,
                ["InputSize"] = currentData.Length,
                ["OutputSize"] = finalResult.Data.Length,
                ["Ratio"] = currentData.Length > 0 ? (double)finalResult.Data.Length / currentData.Length : 0,
                ["Compression"] = currentData.Length > 0 ? 
                    (1 - (double)finalResult.Data.Length / currentData.Length) * 100 : 0,
                ["Duration"] = _finalCompressor.LastOperationDuration.TotalMilliseconds
            });
            
            totalMetadata["StageResults"] = stageResults;
            totalMetadata["FinalSize"] = finalResult.Data.Length;
            totalMetadata["TotalCompressionRatio"] = data.Length > 0 ? 
                (double)finalResult.Data.Length / data.Length : 0;
            totalMetadata["TotalCompressionPercentage"] = data.Length > 0 ? 
                (1 - (double)finalResult.Data.Length / data.Length) * 100 : 0;
            
            foreach (var stageResult in stageResults)
            {
                foreach (var kvp in stageResult)
                {
                    if (!totalMetadata.ContainsKey($"{stageResult["Compressor"]}_{kvp.Key}"))
                    {
                        totalMetadata[$"{stageResult["Compressor"]}_{kvp.Key}"] = kvp.Value;
                    }
                }
            }
            
            LastOperationDuration = DateTime.Now - startTime;
            totalMetadata["TotalDuration"] = LastOperationDuration.TotalMilliseconds;
            totalMetadata["Pipeline"] = PipelineDescription;
            
            return new CompressionResult
            {
                Data = finalResult.Data,
                IsSuccess = true,
                Metadata = totalMetadata
            };
        }
        catch (OutOfMemoryException ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Недостаточно памяти для конвейерной компрессии: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка конвейерной компрессии: {ex.Message}"
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
            var finalResult = _finalCompressor.Decompress(compressedData, options);
            if (!finalResult.IsSuccess)
            {
                return new CompressionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Ошибка декомпрессии на этапе {_finalCompressor.Name}: {finalResult.ErrorMessage}"
                };
            }
            
            byte[] currentData = finalResult.Data;
            
            for (int i = _transformers.Count - 1; i >= 0; i--)
            {
                var transformer = _transformers[i];
                var result = transformer.Decompress(currentData, options);
                
                if (!result.IsSuccess)
                {
                    return new CompressionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Ошибка декомпрессии на этапе {transformer.Name}: {result.ErrorMessage}"
                    };
                }
                
                currentData = result.Data;
            }
            
            LastOperationDuration = DateTime.Now - startTime;
            
            return new CompressionResult
            {
                Data = currentData,
                IsSuccess = true,
                Metadata = new Dictionary<string, object>
                {
                    ["TotalDuration"] = LastOperationDuration.TotalMilliseconds,
                    ["Pipeline"] = PipelineDescription,
                    ["DecompressedSize"] = currentData.Length
                }
            };
        }
        catch (OutOfMemoryException ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Недостаточно памяти для конвейерной декомпрессии: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка конвейерной декомпрессии: {ex.Message}"
            };
        }
    }
    
    public override async Task<CompressionResult> CompressAsync(byte[] data, CompressorOptions options = null)
    {
        return await Task.Run(() => Compress(data, options));
    }
    
    public override async Task<CompressionResult> DecompressAsync(byte[] compressedData, CompressorOptions options = null)
    {
        return await Task.Run(() => Decompress(compressedData, options));
    }
    
    public PipelineCompressor AddTransformer(CompressorBase transformer)
    {
        if (transformer == null) return this;
        
        _transformers.Add(transformer);
        UpdateDescription();
        SupportsParallelProcessing = SupportsParallelProcessing && transformer.SupportsParallelProcessing;
        return this;
    }
    
    public PipelineCompressor RemoveTransformer(int index)
    {
        if (index >= 0 && index < _transformers.Count)
        {
            _transformers.RemoveAt(index);
            UpdateDescription();
        }
        return this;
    }
    
    public PipelineCompressor InsertTransformer(int index, CompressorBase transformer)
    {
        if (transformer == null) return this;
        
        if (index >= 0 && index <= _transformers.Count)
        {
            _transformers.Insert(index, transformer);
            UpdateDescription();
            SupportsParallelProcessing = SupportsParallelProcessing && transformer.SupportsParallelProcessing;
        }
        return this;
    }
    
    public static PipelineCompressor operator +(PipelineCompressor pipeline, CompressorBase transformer)
    {
        if (pipeline == null) return null;
        
        pipeline.AddTransformer(transformer);
        return pipeline;
    }
    
    public static PipelineCompressor operator -(PipelineCompressor pipeline, CompressorBase transformer)
    {
        if (pipeline == null) return null;
        
        pipeline._transformers.Remove(transformer);
        return pipeline;
    }
    
    public static PipelineCompressor operator >>(PipelineCompressor pipeline, int count)
    {
        if (pipeline == null) return null;
        
        for (int i = 0; i < count && pipeline._transformers.Count > 0; i++)
        {
            pipeline._transformers.RemoveAt(pipeline._transformers.Count - 1);
        }
        return pipeline;
    }
    
    public static PipelineCompressor operator <<(PipelineCompressor pipeline, CompressorBase transformer)
    {
        if (pipeline == null) return null;
        
        pipeline._transformers.Insert(0, transformer);
        return pipeline;
    }
    
    public PipelineStatistics GetStatistics(byte[] sampleData)
    {
        if (sampleData == null || sampleData.Length == 0)
        {
            return new PipelineStatistics();
        }
        
        var stats = new PipelineStatistics();
        stats.OriginalSize = sampleData.Length;
        
        byte[] current = sampleData;
        
        foreach (var transformer in _transformers)
        {
            var result = transformer.Compress(current);
            if (result.IsSuccess)
            {
                stats.StageStatistics.Add(new StageStatistic
                {
                    CompressorName = transformer.Name,
                    InputSize = current.Length,
                    OutputSize = result.Data.Length,
                    CompressionRatio = current.Length > 0 ? (double)result.Data.Length / current.Length : 0,
                    Duration = transformer.LastOperationDuration
                });
                
                current = result.Data;
            }
        }
        
        var finalResult = _finalCompressor.Compress(current);
        if (finalResult.IsSuccess)
        {
            stats.StageStatistics.Add(new StageStatistic
            {
                CompressorName = _finalCompressor.Name,
                InputSize = current.Length,
                OutputSize = finalResult.Data.Length,
                CompressionRatio = current.Length > 0 ? (double)finalResult.Data.Length / current.Length : 0,
                Duration = _finalCompressor.LastOperationDuration
            });
            
            stats.FinalSize = finalResult.Data.Length;
            stats.TotalCompressionRatio = sampleData.Length > 0 ? 
                (double)finalResult.Data.Length / sampleData.Length : 0;
            stats.TotalCompressionPercentage = (1 - stats.TotalCompressionRatio) * 100;
        }
        
        return stats;
    }
    
    public override string ToString()
    {
        return $"PipelineCompressor: {Name} ({PipelineDescription})";
    }
}

public class PipelineStatistics
{
    public long OriginalSize { get; set; }
    public long FinalSize { get; set; }
    public double TotalCompressionRatio { get; set; }
    public double TotalCompressionPercentage { get; set; }
    public List<StageStatistic> StageStatistics { get; set; }
    
    public PipelineStatistics()
    {
        StageStatistics = new List<StageStatistic>();
    }
}

public class StageStatistic
{
    public string CompressorName { get; set; }
    public long InputSize { get; set; }
    public long OutputSize { get; set; }
    public double CompressionRatio { get; set; }
    public TimeSpan Duration { get; set; }
}
