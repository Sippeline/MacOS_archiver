using System.Collections.Generic;

namespace final_archiver.Services.Compressors;

public class CompressorOptions
{
    public int CompressionLevel { get; set; } = 6;
    public bool UseParallelProcessing { get; set; } = false;
    public int BufferSize { get; set; } = 8192;
    public Dictionary<string, object> CustomSettings { get; set; }
    
    public CompressorOptions()
    {
        CustomSettings = new Dictionary<string, object>();
    }
    
    public T GetSetting<T>(string key, T defaultValue = default)
    {
        if (CustomSettings.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }
    
    public void SetSetting<T>(string key, T value)
    {
        CustomSettings[key] = value;
    }
}