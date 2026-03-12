using System;
using System.Collections.Generic;
using System.Linq;

namespace final_archiver.Services.Compressors;

public class MtfCompressor : CompressorBase
{
    private byte[] _alphabet;
    private readonly object _syncLock = new object();
    
    public int AlphabetSize { get; set; } = 256;
    
    public MtfCompressor() : base("Move-To-Front", "Алгоритм Move-To-Front с алфавитом 256 символов")
    {
        InitializeAlphabet();
    }
    
    public MtfCompressor(string name, int alphabetSize = 256) : base(name, "Алгоритм Move-To-Front")
    {
        AlphabetSize = alphabetSize;
        InitializeAlphabet();
    }
    
    private void InitializeAlphabet()
    {
        lock (_syncLock)
        {
            _alphabet = new byte[AlphabetSize];
            for (int i = 0; i < AlphabetSize; i++)
            {
                _alphabet[i] = (byte)i;
            }
        }
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
            lock (_syncLock)
            {
                InitializeAlphabet();
                var result = new byte[data.Length];
                
                for (int i = 0; i < data.Length; i++)
                {
                    byte symbol = data[i];
                    
                    int index = Array.IndexOf(_alphabet, symbol);
                    
                    if (index == -1)
                    {
                        throw new InvalidOperationException($"Символ {symbol} не найден в алфавите размером {AlphabetSize}");
                    }
                    
                    result[i] = (byte)index;
                    
                    if (index > 0)
                    {
                        for (int j = index; j > 0; j--)
                        {
                            _alphabet[j] = _alphabet[j - 1];
                        }
                        
                        _alphabet[0] = symbol;
                    }
                }
                
                LastOperationDuration = DateTime.Now - startTime;
                
                var metadata = new Dictionary<string, object>
                {
                    ["AlphabetSize"] = AlphabetSize,
                    ["InputSize"] = data.Length,
                    ["OutputSize"] = result.Length,
                    ["Algorithm"] = "MTF",
                    ["Duration"] = LastOperationDuration.TotalMilliseconds,
                    ["CompressionRatio"] = data.Length > 0 ? (double)result.Length / data.Length : 0
                };
                
                return new CompressionResult
                {
                    Data = result,
                    IsSuccess = true,
                    Metadata = metadata
                };
            }
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка MTF сжатия: {ex.Message}"
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
            lock (_syncLock)
            {
                InitializeAlphabet();
                var result = new byte[compressedData.Length];
                
                for (int i = 0; i < compressedData.Length; i++)
                {
                    int index = compressedData[i];
                    
                    if (index < 0 || index >= _alphabet.Length)
                    {
                        throw new InvalidOperationException($"Индекс {index} вне диапазона алфавита размером {_alphabet.Length}");
                    }
                    
                    byte symbol = _alphabet[index];
                    result[i] = symbol;
                    
                    if (index > 0)
                    {
                        for (int j = index; j > 0; j--)
                        {
                            _alphabet[j] = _alphabet[j - 1];
                        }
                        
                        _alphabet[0] = symbol;
                    }
                }
                
                LastOperationDuration = DateTime.Now - startTime;
                
                return new CompressionResult
                {
                    Data = result,
                    IsSuccess = true,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Duration"] = LastOperationDuration.TotalMilliseconds
                    }
                };
            }
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка MTF декомпрессии: {ex.Message}"
            };
        }
    }
    
    public static MtfCompressor operator ++(MtfCompressor compressor)
    {
        if (compressor == null) return null;
        
        compressor.AlphabetSize = Math.Min(512, compressor.AlphabetSize * 2);
        compressor.InitializeAlphabet();
        return compressor;
    }
    
    public static MtfCompressor operator --(MtfCompressor compressor)
    {
        if (compressor == null) return null;
        
        compressor.AlphabetSize = Math.Max(64, compressor.AlphabetSize / 2);
        compressor.InitializeAlphabet();
        return compressor;
    }
    
    public override string ToString()
    {
        return $"MtfCompressor: {Name} (AlphabetSize: {AlphabetSize})";
    }
}
