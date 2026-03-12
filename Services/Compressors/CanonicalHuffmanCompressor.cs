using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace final_archiver.Services.Compressors;

public class CanonicalHuffmanCompressor : CompressorBase
{
    private class HuffmanNode : IComparable<HuffmanNode>
    {
        public byte? Symbol { get; set; }
        public int Frequency { get; set; }
        public HuffmanNode Left { get; set; }
        public HuffmanNode Right { get; set; }
        public bool IsLeaf => Left == null && Right == null;
        
        public int CompareTo(HuffmanNode other)
        {
            int freqCompare = Frequency.CompareTo(other.Frequency);
            if (freqCompare != 0) return freqCompare;
            
            if (IsLeaf && !other.IsLeaf) return -1;
            if (!IsLeaf && other.IsLeaf) return 1;
            
            if (IsLeaf && other.IsLeaf)
                return Symbol.Value.CompareTo(other.Symbol.Value);
                
            return 0;
        }
    }
    
    private class HuffmanCode
    {
        public byte Symbol { get; set; }
        public string Code { get; set; }
        public int Length { get; set; }
        public uint CanonicalCode { get; set; }
    }
    
    public bool UseCanonicalForm { get; set; } = true;
    public int MinCodeLength { get; set; } = 1;
    public int MaxCodeLength { get; set; } = 32;
    
    public CanonicalHuffmanCompressor() : base("Canonical Huffman", "Каноническое кодирование Хаффмана с битовой записью длин")
    {
        SupportsParallelProcessing = false;
    }
    
    public CanonicalHuffmanCompressor(string name, bool canonical = true) : base(name, "Кодирование Хаффмана")
    {
        UseCanonicalForm = canonical;
        SupportsParallelProcessing = false;
    }
    
    public CanonicalHuffmanCompressor(CanonicalHuffmanCompressor other) : this(other.Name, other.UseCanonicalForm)
    {
    }
    
    public override CompressionResult Compress(byte[] data, CompressorOptions options = null)
    {
        if (data == null || data.Length == 0)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = "Входные данные не могут быть пустыми"
            };
        }
        
        var startTime = DateTime.Now;
        
        try
        {
            var frequencies = CalculateFrequencies(data);
            
            if (frequencies.Count == 1)
            {
                return CompressSingleSymbol(data, frequencies.Keys.First());
            }
            
            Dictionary<byte, HuffmanCode> codes;
            
            if (UseCanonicalForm)
            {
                codes = GenerateCanonicalHuffmanCodes(frequencies);
            }
            else
            {
                codes = GenerateStandardHuffmanCodes(frequencies);
            }
            
            var header = CreateHeader(codes);
            var encodedData = EncodeData(data, codes);
            
            var result = new List<byte>();
            result.AddRange(BitConverter.GetBytes(data.Length));
            result.AddRange(header);
            result.AddRange(encodedData);
            
            LastOperationDuration = DateTime.Now - startTime;
            
            var metadata = new Dictionary<string, object>
            {
                ["OriginalSize"] = data.Length,
                ["CompressedSize"] = result.Count,
                ["CodeCount"] = codes.Count,
                ["AverageCodeLength"] = codes.Values.Average(c => c.Length),
                ["UseCanonicalForm"] = UseCanonicalForm,
                ["Algorithm"] = "Huffman",
                ["Duration"] = LastOperationDuration.TotalMilliseconds,
                ["UniqueSymbols"] = frequencies.Count,
                ["Entropy"] = CalculateEntropy(frequencies, data.Length)
            };
            
            return new CompressionResult
            {
                Data = result.ToArray(),
                IsSuccess = true,
                Metadata = metadata
            };
        }
        catch (OutOfMemoryException ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Недостаточно памяти для Huffman сжатия: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка Huffman сжатия: {ex.Message}"
            };
        }
    }
    
    public override CompressionResult Decompress(byte[] compressedData, CompressorOptions options = null)
    {
        if (compressedData == null || compressedData.Length < 260)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = "Неверные данные для декомпрессии Huffman (слишком короткие)"
            };
        }
        
        var startTime = DateTime.Now;
        
        try
        {
            int position = 0;
            
            int originalSize = BitConverter.ToInt32(compressedData, position);
            position += 4;
            
            if (originalSize <= 0 || originalSize > 500 * 1024 * 1024)
            {
                return new CompressionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Некорректный размер данных: {originalSize}"
                };
            }
            
            var codeLengths = new int[256];
            for (int i = 0; i < 256; i++)
            {
                if (position >= compressedData.Length)
                {
                    return new CompressionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Неполная таблица длин кодов"
                    };
                }
                codeLengths[i] = compressedData[position];
                position++;
            }
            
            var codes = ReconstructCanonicalCodes(codeLengths);
            var lookupTable = BuildDecodeLookupTable(codes);
            
            var encodedData = new byte[compressedData.Length - position];
            Array.Copy(compressedData, position, encodedData, 0, encodedData.Length);
            
            var decodedData = DecodeDataOptimized(encodedData, lookupTable, originalSize);
            
            if (decodedData.Length != originalSize)
            {
                Console.WriteLine($"Предупреждение: Размер декодированных данных ({decodedData.Length}) не совпадает с ожидаемым ({originalSize})");
            }
            
            LastOperationDuration = DateTime.Now - startTime;
            
            var metadata = new Dictionary<string, object>
            {
                ["Duration"] = LastOperationDuration.TotalMilliseconds,
                ["OriginalSize"] = originalSize,
                ["DecodedSize"] = decodedData.Length,
                ["CodeCount"] = codes.Count,
                ["SizeMismatch"] = decodedData.Length != originalSize
            };
            
            return new CompressionResult
            {
                Data = decodedData,
                IsSuccess = true,
                Metadata = metadata
            };
        }
        catch (OutOfMemoryException ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Недостаточно памяти для Huffman декомпрессии: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка Huffman декомпрессии: {ex.Message}"
            };
        }
    }
    
    private Dictionary<byte, int> CalculateFrequencies(byte[] data)
    {
        var frequencies = new Dictionary<byte, int>();
        
        foreach (byte b in data)
        {
            frequencies[b] = frequencies.TryGetValue(b, out int freq) ? freq + 1 : 1;
        }
        
        return frequencies;
    }
    
    private Dictionary<byte, HuffmanCode> GenerateStandardHuffmanCodes(Dictionary<byte, int> frequencies)
    {
        var nodes = new List<HuffmanNode>();
        
        foreach (var kvp in frequencies)
        {
            nodes.Add(new HuffmanNode
            {
                Symbol = kvp.Key,
                Frequency = kvp.Value
            });
        }
        
        while (nodes.Count > 1)
        {
            nodes.Sort();
            
            var left = nodes[0];
            var right = nodes[1];
            
            var parent = new HuffmanNode
            {
                Frequency = left.Frequency + right.Frequency,
                Left = left,
                Right = right
            };
            
            nodes.RemoveRange(0, 2);
            nodes.Add(parent);
        }
        
        var codes = new Dictionary<byte, HuffmanCode>();
        if (nodes.Count > 0)
            GenerateCodesFromTree(nodes[0], "", codes);
            
        return codes;
    }
    
    private Dictionary<byte, HuffmanCode> GenerateCanonicalHuffmanCodes(Dictionary<byte, int> frequencies)
    {
        var standardCodes = GenerateStandardHuffmanCodes(frequencies);
        
        var symbolsByLength = standardCodes.Values
            .GroupBy(c => c.Length)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.Symbol).Select(c => c.Symbol).ToList()
            );
        
        var canonicalCodes = new Dictionary<byte, HuffmanCode>();
        uint currentCode = 0;
        
        foreach (var length in symbolsByLength.Keys.OrderBy(l => l))
        {
            var symbols = symbolsByLength[length];
            
            foreach (var symbol in symbols)
            {
                string codeBinary = Convert.ToString(currentCode, 2)
                    .PadLeft(length, '0');
                
                canonicalCodes[symbol] = new HuffmanCode
                {
                    Symbol = symbol,
                    Code = codeBinary,
                    Length = length,
                    CanonicalCode = currentCode
                };
                
                currentCode++;
            }
            
            currentCode <<= 1;
        }
        
        return canonicalCodes;
    }
    
    private Dictionary<byte, HuffmanCode> ReconstructCanonicalCodes(int[] codeLengths)
    {
        var symbolsByLength = new Dictionary<int, List<byte>>();
        
        for (int i = 0; i < 256; i++)
        {
            int length = codeLengths[i];
            if (length > 0)
            {
                if (!symbolsByLength.ContainsKey(length))
                    symbolsByLength[length] = new List<byte>();
                symbolsByLength[length].Add((byte)i);
            }
        }
        
        foreach (var length in symbolsByLength.Keys)
        {
            symbolsByLength[length].Sort();
        }
        
        var codes = new Dictionary<byte, HuffmanCode>();
        uint currentCode = 0;
        
        foreach (var length in symbolsByLength.Keys.OrderBy(l => l))
        {
            var symbols = symbolsByLength[length];
            
            foreach (var symbol in symbols)
            {
                string codeBinary = Convert.ToString(currentCode, 2)
                    .PadLeft(length, '0');
                    
                codes[symbol] = new HuffmanCode
                {
                    Symbol = symbol,
                    Code = codeBinary,
                    Length = length,
                    CanonicalCode = currentCode
                };
                
                currentCode++;
            }
            
            currentCode <<= 1;
        }
        
        return codes;
    }
    
    private Dictionary<int, byte> BuildDecodeLookupTable(Dictionary<byte, HuffmanCode> codes)
    {
        var lookupTable = new Dictionary<int, byte>();
        
        foreach (var kvp in codes)
        {
            var code = kvp.Value;
            uint canonicalCode = code.CanonicalCode;
            
            int lookupKey = (int)((code.Length << 24) | canonicalCode);
            lookupTable[lookupKey] = kvp.Key;
        }
        
        return lookupTable;
    }
    
    private byte[] CreateHeader(Dictionary<byte, HuffmanCode> codes)
    {
        var header = new byte[256];
        
        foreach (var kvp in codes)
        {
            header[kvp.Key] = (byte)kvp.Value.Length;
        }
        
        return header;
    }
    
    private byte[] EncodeData(byte[] data, Dictionary<byte, HuffmanCode> codes)
    {
        int totalBits = 0;
        foreach (byte b in data)
        {
            if (!codes.TryGetValue(b, out var code))
            {
                throw new InvalidOperationException($"Символ {b} не имеет кода Хаффмана");
            }
            totalBits += code.Length;
        }
        
        int byteCount = (totalBits + 7) / 8;
        var result = new byte[byteCount];
        
        int currentBit = 0;
        foreach (byte b in data)
        {
            var code = codes[b];
            
            foreach (char bit in code.Code)
            {
                if (bit == '1')
                {
                    int byteIndex = currentBit / 8;
                    int bitPosition = 7 - (currentBit % 8);
                    result[byteIndex] |= (byte)(1 << bitPosition);
                }
                currentBit++;
            }
        }
        
        int paddingBits = (8 - (currentBit % 8)) % 8;
        for (int i = 0; i < paddingBits; i++)
        {
            int byteIndex = currentBit / 8;
            if (byteIndex < result.Length)
            {
                currentBit++;
            }
        }
        
        return result;
    }
    
    private byte[] DecodeDataOptimized(byte[] encodedData, Dictionary<int, byte> lookupTable, int expectedSize)
    {
        var result = new List<byte>(expectedSize);
        uint currentCode = 0;
        int currentLength = 0;
        
        for (int bitIndex = 0; bitIndex < encodedData.Length * 8; bitIndex++)
        {
            if (result.Count >= expectedSize)
                break;
                
            int byteIndex = bitIndex / 8;
            int bitPosition = 7 - (bitIndex % 8);
            
            if (byteIndex >= encodedData.Length)
                break;
                
            bool bit = ((encodedData[byteIndex] >> bitPosition) & 1) == 1;
            
            currentCode = (currentCode << 1) | (bit ? 1u : 0u);
            currentLength++;
            
            int lookupKey = (int)((currentLength << 24) | currentCode);
            
            if (lookupTable.TryGetValue(lookupKey, out byte symbol))
            {
                result.Add(symbol);
                currentCode = 0;
                currentLength = 0;
            }
            
            if (currentLength > 32)
            {
                throw new InvalidOperationException($"Не удалось декодировать битовую последовательность (длина {currentLength} бит)");
            }
        }
        
        return result.ToArray();
    }
    
    private void GenerateCodesFromTree(HuffmanNode node, string code, Dictionary<byte, HuffmanCode> codes)
    {
        if (node == null) return;
        
        if (node.IsLeaf && node.Symbol.HasValue)
        {
            codes[node.Symbol.Value] = new HuffmanCode
            {
                Symbol = node.Symbol.Value,
                Code = code,
                Length = code.Length
            };
        }
        else
        {
            GenerateCodesFromTree(node.Left, code + "0", codes);
            GenerateCodesFromTree(node.Right, code + "1", codes);
        }
    }
    
    private CompressionResult CompressSingleSymbol(byte[] data, byte symbol)
    {
        var result = new List<byte>();
        
        result.Add(0xFF);
        result.Add(symbol);
        result.AddRange(BitConverter.GetBytes(data.Length));
        
        return new CompressionResult
        {
            Data = result.ToArray(),
            IsSuccess = true,
            Metadata = new Dictionary<string, object>
            {
                ["Algorithm"] = "Huffman-SingleSymbol",
                ["Symbol"] = symbol,
                ["Count"] = data.Length,
                ["OriginalSize"] = data.Length,
                ["CompressedSize"] = result.Count
            }
        };
    }
    
    private double CalculateEntropy(Dictionary<byte, int> frequencies, int totalLength)
    {
        double entropy = 0;
        
        foreach (var freq in frequencies.Values)
        {
            double probability = (double)freq / totalLength;
            entropy -= probability * Math.Log(probability, 2);
        }
        
        return entropy;
    }
    
    public Dictionary<byte, string> AnalyzeCompression(byte[] data)
    {
        var frequencies = CalculateFrequencies(data);
        var codes = GenerateCanonicalHuffmanCodes(frequencies);
        
        var analysis = new Dictionary<byte, string>();
        foreach (var kvp in codes)
        {
            analysis[kvp.Key] = $"Код: {kvp.Value.Code} (длина: {kvp.Value.Length}, частота: {frequencies[kvp.Key]})";
        }
        
        return analysis;
    }
    
    public void ValidateCompression(byte[] testData)
    {
        var compressed = Compress(testData);
        if (!compressed.IsSuccess)
            throw new InvalidOperationException($"Сжатие не удалось: {compressed.ErrorMessage}");
            
        var decompressed = Decompress(compressed.Data);
        if (!decompressed.IsSuccess)
            throw new InvalidOperationException($"Распаковка не удалась: {decompressed.ErrorMessage}");
            
        if (!testData.SequenceEqual(decompressed.Data))
            throw new InvalidOperationException("Данные после сжатия/распаковки не совпадают");
    }
    
    public static CanonicalHuffmanCompressor operator !(CanonicalHuffmanCompressor compressor)
    {
        if (compressor == null) return null;
        
        compressor.UseCanonicalForm = !compressor.UseCanonicalForm;
        compressor.Name = compressor.UseCanonicalForm ? 
            "Canonical Huffman" : "Standard Huffman";
        return compressor;
    }
    
    public override string ToString()
    {
        return $"CanonicalHuffmanCompressor: {Name} (Canonical: {UseCanonicalForm})";
    }
}

// очень объемно, точно можно упростить
