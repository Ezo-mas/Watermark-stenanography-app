using System.Collections.Generic;
using System.Text;
using MetadataExtractor;

namespace WatermarkApp.Helpers;

public static class MetadataHelper
{
    public static string GetMetadata(string imagePath)
    {
        var sb = new StringBuilder();
        
        try
        {
            IEnumerable<Directory> directories = ImageMetadataReader.ReadMetadata(imagePath);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    sb.AppendLine($"{directory.Name} - {tag.Name} = {tag.Description}");
                }
            }
        }
        catch (System.Exception ex)
        {
            sb.AppendLine($"Error reading metadata: {ex.Message}");
        }

        return sb.ToString();
    }
}