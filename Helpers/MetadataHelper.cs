using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MetadataExtractor;

namespace WatermarkApp.Helpers;

public static class MetadataHelper
{
    public static string GetMetadata(string imagePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(imagePath);
            return FormatMetadata(directories);
        }
        catch (System.Exception ex)
        {
            return $"Error reading metadata: {ex.Message}";
        }
    }

    public static string GetMetadata(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            var directories = ImageMetadataReader.ReadMetadata(stream);
            return FormatMetadata(directories);
        }
        catch (System.Exception ex)
        {
            return $"Error reading metadata: {ex.Message}";
        }
    }

    private static string FormatMetadata(IEnumerable<MetadataExtractor.Directory> directories)
    {
        var sb = new StringBuilder();
        foreach (var directory in directories)
        {
            foreach (var tag in directory.Tags)
            {
                sb.AppendLine($"{directory.Name} - {tag.Name} = {tag.Description}");
            }
        }
        return sb.ToString();
    }

    public static string CompareMetadata(string originalImagePath, byte[] newImageBytes)
    {
        try
        {
            var origDirectories = ImageMetadataReader.ReadMetadata(originalImagePath);
            var newDirectories = ImageMetadataReader.ReadMetadata(new MemoryStream(newImageBytes));

            var origTags = ExtractTags(origDirectories);
            var newTags = ExtractTags(newDirectories);

            var sb = new StringBuilder();
            sb.AppendLine("=== METADATA DIFFERENCES ===");
            sb.AppendLine();

            var removedTags = origTags.Where(t => !newTags.ContainsKey(t.Key)).ToList();
            var addedTags = newTags.Where(t => !origTags.ContainsKey(t.Key)).ToList();
            var changedTags = newTags.Where(t => origTags.ContainsKey(t.Key) && origTags[t.Key] != t.Value).ToList();

            if (!removedTags.Any() && !addedTags.Any() && !changedTags.Any())
            {
                sb.AppendLine("No difference in metadata found.");
                return sb.ToString();
            }

            if (changedTags.Any())
            {
                sb.AppendLine("--- MODIFIED TAGS ---");
                foreach (var tag in changedTags)
                {
                    sb.AppendLine($"[{tag.Key}]");
                    sb.AppendLine($"  Old: {origTags[tag.Key]}");
                    sb.AppendLine($"  New: {tag.Value}");
                }
                sb.AppendLine();
            }

            if (addedTags.Any())
            {
                sb.AppendLine("--- ADDED TAGS (Present in new image only) ---");
                foreach (var tag in addedTags)
                {
                    sb.AppendLine($"[{tag.Key}] = {tag.Value}");
                }
                sb.AppendLine();
            }

            if (removedTags.Any())
            {
                sb.AppendLine("--- REMOVED EXIF/TAGS (Lost during processing) ---");
                foreach (var tag in removedTags)
                {
                    sb.AppendLine($"[{tag.Key}] = {tag.Value}");
                }
            }

            return sb.ToString();
        }
        catch (System.Exception ex)
        {
            return $"Error comparing metadata: {ex.Message}";
        }
    }

    private static Dictionary<string, string> ExtractTags(IEnumerable<MetadataExtractor.Directory> directories)
    {
        var dict = new Dictionary<string, string>();
        foreach (var dir in directories)
        {
            foreach (var tag in dir.Tags)
            {
                string key = $"{dir.Name} - {tag.Name}";
                // Handle duplicate keys gracefully just in case
                if (!dict.ContainsKey(key))
                {
                    dict.Add(key, tag.Description ?? "");
                }
            }
        }
        return dict;
    }
}