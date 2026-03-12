using System.Collections.Generic;

namespace final_archiver.Models;

public class CompressionPipeline
{
    public List<string> TransformNames { get; set; }
    public string Name { get; set; }
    
    public CompressionPipeline()
    {
        TransformNames = new List<string>();
        Name = string.Empty;
    }
    
    public CompressionPipeline(string name, List<string> transforms)
    {
        Name = name;
        TransformNames = transforms ?? new List<string>();
    }
    
    public override string ToString()
    {
        return $"{Name} ({string.Join(" → ", TransformNames)})";
    }
}