using System;
using System.Collections.Generic;
using System.Text;

namespace final_archiver.Utils;

public static class ByteArrayExtensions
{
    public static string ToHexString(this byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;
        
        var hex = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            hex.AppendFormat("{0:x2}", b);
        }
        return hex.ToString();
    }
    
    public static byte[] Concatenate(this byte[] first, byte[] second)
    {
        if (first == null && second == null)
            return Array.Empty<byte>();
        
        if (first == null)
            return second;
        
        if (second == null)
            return first;
        
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }
    
    public static bool SequenceEqual(this byte[] first, byte[] second)
    {
        if (ReferenceEquals(first, second))
            return true;
        
        if (first == null || second == null)
            return false;
        
        if (first.Length != second.Length)
            return false;
        
        for (int i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
                return false;
        }
        
        return true;
    }
    
    public static byte[] Slice(this byte[] data, int start, int length)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        
        if (start < 0 || start >= data.Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        
        if (length < 0 || start + length > data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        
        var result = new byte[length];
        Buffer.BlockCopy(data, start, result, 0, length);
        return result;
    }
    
    public static Dictionary<byte, int> CalculateFrequencies(this byte[] data)
    {
        var frequencies = new Dictionary<byte, int>();
        
        if (data == null)
            return frequencies;
        
        foreach (byte b in data)
        {
            if (frequencies.ContainsKey(b))
                frequencies[b]++;
            else
                frequencies[b] = 1;
        }
        
        return frequencies;
    }
    
    public static byte[] PadToMultiple(this byte[] data, int blockSize, byte paddingValue = 0)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        
        if (blockSize <= 0)
            throw new ArgumentException("Размер блока должен быть положительным", nameof(blockSize));
        
        int paddingLength = (blockSize - (data.Length % blockSize)) % blockSize;
        
        if (paddingLength == 0)
            return data;
        
        var result = new byte[data.Length + paddingLength];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        
        for (int i = data.Length; i < result.Length; i++)
        {
            result[i] = paddingValue;
        }
        
        return result;
    }
}
