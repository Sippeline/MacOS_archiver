using System;
using System.Collections.Generic;
using System.Linq;

namespace final_archiver.Services.Compressors;

public class BwtCompressor : CompressorBase
{
    public int BlockSize { get; set; } = 900000;
    
    public BwtCompressor() : base("BWT Transform", "Преобразование Барроуза-Уилера")
    {
        SupportsParallelProcessing = true;
    }
    
    public BwtCompressor(string name, int blockSize = 900000) : base(name, "Преобразование Барроуза-Уилера")
    {
        BlockSize = blockSize;
        SupportsParallelProcessing = true;
    }
    
    public BwtCompressor(BwtCompressor other) : this(other.Name, other.BlockSize)
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
            var result = new List<byte>();
            int blockCount = (int)Math.Ceiling((double)data.Length / BlockSize);
            
            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                int start = blockIndex * BlockSize;
                int length = Math.Min(BlockSize, data.Length - start);
                byte[] block = new byte[length];
                Array.Copy(data, start, block, 0, length);
                
                var (transformed, index) = PerformBWT(block);
                
                result.AddRange(BitConverter.GetBytes(index));
                result.AddRange(BitConverter.GetBytes(length));
                result.AddRange(transformed);
                
                if (blockIndex < blockCount - 1)
                {
                    result.AddRange(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });
                }
            }
            
            LastOperationDuration = DateTime.Now - startTime;
            
            var metadata = new Dictionary<string, object>
            {
                ["BlockCount"] = blockCount,
                ["OriginalSize"] = data.Length,
                ["CompressedSize"] = result.Count,
                ["Algorithm"] = "BWT",
                ["BlockSize"] = BlockSize,
                ["Duration"] = LastOperationDuration.TotalMilliseconds
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
                ErrorMessage = $"Недостаточно памяти для BWT: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка BWT сжатия: {ex.Message}"
            };
        }
    }
    
    public override CompressionResult Decompress(byte[] compressedData, CompressorOptions options = null)
    {
        if (compressedData == null || compressedData.Length < 8)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = "Неверные данные для декомпрессии BWT"
            };
        }
        
        var startTime = DateTime.Now;
        
        try
        {
            var result = new List<byte>();
            int position = 0;
            int blockNumber = 0;
            
            while (position < compressedData.Length)
            {
                if (position + 8 > compressedData.Length)
                    break;
                    
                int index = BitConverter.ToInt32(compressedData, position);
                position += 4;
                
                int length = BitConverter.ToInt32(compressedData, position);
                position += 4;
                
                if (position + length > compressedData.Length)
                    break;
                    
                byte[] block = new byte[length];
                Array.Copy(compressedData, position, block, 0, length);
                position += length;
                
                byte[] originalBlock = InverseBWT(block, index);
                result.AddRange(originalBlock);
                
                blockNumber++;
                
                if (position + 4 <= compressedData.Length && 
                    compressedData[position] == 0xAA && 
                    compressedData[position + 1] == 0xBB &&
                    compressedData[position + 2] == 0xCC &&
                    compressedData[position + 3] == 0xDD)
                {
                    position += 4;
                }
            }
            
            LastOperationDuration = DateTime.Now - startTime;
            
            return new CompressionResult
            {
                Data = result.ToArray(),
                IsSuccess = true,
                Metadata = new Dictionary<string, object>
                {
                    ["BlocksProcessed"] = blockNumber,
                    ["DecompressedSize"] = result.Count,
                    ["Duration"] = LastOperationDuration.TotalMilliseconds
                }
            };
        }
        catch (OutOfMemoryException ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Недостаточно памяти для BWT декомпрессии: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new CompressionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Ошибка BWT декомпрессии: {ex.Message}"
            };
        }
    }
    
    private (byte[] transformed, int index) PerformBWT(byte[] data)
    {
        int n = data.Length;
        
        if (n <= 10000)
        {
            return PerformBWTSimple(data);
        }
        
        return PerformBWTEnhanced(data);
    }
    
    private (byte[] transformed, int index) PerformBWTSimple(byte[] data)
    {
        int n = data.Length;
        
        var rotations = new List<(byte[] rotation, int index)>();
        
        for (int i = 0; i < n; i++)
        {
            var rotation = new byte[n];
            for (int j = 0; j < n; j++)
            {
                rotation[j] = data[(i + j) % n];
            }
            rotations.Add((rotation, i));
        }
        
        rotations.Sort((a, b) =>
        {
            for (int i = 0; i < n; i++)
            {
                int comparison = a.rotation[i].CompareTo(b.rotation[i]);
                if (comparison != 0) return comparison;
            }
            return 0;
        });
        
        byte[] result = new byte[n];
        int originalIndex = -1;
        
        for (int i = 0; i < n; i++)
        {
            result[i] = rotations[i].rotation[n - 1];
            if (rotations[i].index == 0)
            {
                originalIndex = i;
            }
        }
        
        if (originalIndex == -1)
        {
            throw new InvalidOperationException("Не удалось найти исходную позицию");
        }
        
        return (result, originalIndex);
    }
    
    private (byte[] transformed, int index) PerformBWTEnhanced(byte[] data)
    {
        int n = data.Length;
        
        int[] indices = new int[n];
        for (int i = 0; i < n; i++)
        {
            indices[i] = i;
        }
        
        Array.Sort(indices, (a, b) =>
        {
            for (int i = 0; i < n; i++)
            {
                byte byteA = data[(a + i) % n];
                byte byteB = data[(b + i) % n];
                int comparison = byteA.CompareTo(byteB);
                if (comparison != 0) return comparison;
            }
            return 0;
        });
        
        byte[] result = new byte[n];
        int originalIndex = -1;
        
        for (int i = 0; i < n; i++)
        {
            result[i] = data[(indices[i] + n - 1) % n];
            if (indices[i] == 0)
            {
                originalIndex = i;
            }
        }
        
        if (originalIndex == -1)
        {
            throw new InvalidOperationException("Не удалось найти исходную позицию");
        }
        
        return (result, originalIndex);
    }
    
    private byte[] InverseBWT(byte[] transformed, int index)
    {
        int n = transformed.Length;
        
        int[] counts = new int[256];
        foreach (byte b in transformed)
        {
            counts[b]++;
        }
        
        int total = 0;
        int[] startingPositions = new int[256];
        for (int i = 0; i < 256; i++)
        {
            startingPositions[i] = total;
            total += counts[i];
        }
        
        int[] next = new int[n];
        int[] currentPositions = new int[256];
        Array.Copy(startingPositions, currentPositions, 256);
        
        for (int i = 0; i < n; i++)
        {
            byte symbol = transformed[i];
            next[currentPositions[symbol]] = i;
            currentPositions[symbol]++;
        }
        
        byte[] result = new byte[n];
        int currentIndex = index;
        
        for (int i = 0; i < n; i++)
        {
            currentIndex = next[currentIndex];
            result[i] = transformed[currentIndex];
        }
        
        return result;
    }
    
    public void Validate(byte[] testData)
    {
        if (testData == null || testData.Length == 0)
            return;
            
        var (transformed, index) = PerformBWT(testData);
        var restored = InverseBWT(transformed, index);
        
        if (!testData.SequenceEqual(restored))
        {
            throw new InvalidOperationException("BWT преобразование некорректно");
        }
    }
    
    public static BwtCompressor operator +(BwtCompressor a, BwtCompressor b)
    {
        if (a == null || b == null)
            return new BwtCompressor();
            
        return new BwtCompressor($"{a.Name}+{b.Name}", Math.Max(a.BlockSize, b.BlockSize));
    }
    
    public static bool operator ==(BwtCompressor a, BwtCompressor b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Name == b.Name && a.BlockSize == b.BlockSize;
    }
    
    public static bool operator !=(BwtCompressor a, BwtCompressor b)
    {
        return !(a == b);
    }
    
    public override bool Equals(object obj)
    {
        return obj is BwtCompressor other && this == other;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Name, BlockSize);
    }
    
    public override string ToString()
    {
        return $"BwtCompressor: {Name} (BlockSize: {BlockSize})";
    }
}
