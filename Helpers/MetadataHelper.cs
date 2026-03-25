using System.IO;
using System.Text;
using MetadataExtractor;

namespace WatermarkApp.Helpers;

public static class MetadataHelper
{
    /// <summary>
    /// Reads and formats all EXIF and metadata directories/tags from an image file.
    /// </summary>
    public static string GetMetadata(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return "Error reading metadata: Image path is empty.";
        }

        if (!File.Exists(imagePath))
        {
            return "Error reading metadata: Image file not found.";
        }

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(imagePath);
            return FormatMetadata(directories);
        }
        catch (Exception ex)
        {
            return $"Error reading metadata: {ex.Message}";
        }
    }

    public static string GetMetadata(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return "Error reading metadata: Image bytes are empty.";
        }

        try
        {
            using var stream = new MemoryStream(imageBytes);
            var directories = ImageMetadataReader.ReadMetadata(stream);
            return FormatMetadata(directories);
        }
        catch (Exception ex)
        {
            return $"Error reading metadata: {ex.Message}";
        }
    }

    private static string FormatMetadata(IEnumerable<MetadataExtractor.Directory> directories)
    {
        var sb = new StringBuilder();
        int tagCount = 0;
        foreach (var directory in directories)
        {
            foreach (var tag in directory.Tags)
            {
                sb.AppendLine($"{directory.Name} - {tag.Name} = {tag.Description}");
                tagCount++;
            }
        }

        return tagCount == 0 ? "No metadata found." : sb.ToString();
    }


    /// <summary>
    /// Compares the metadata of two images, identifying added, removed, and modified tags.
    /// </summary>
    public static string CompareMetadata(string originalImagePath, byte[] newImageBytes)
    {
        if (string.IsNullOrWhiteSpace(originalImagePath))
        {
            return "Error comparing metadata: Original image path is empty.";
        }

        if (!File.Exists(originalImagePath))
        {
            return "Error comparing metadata: Original image file not found.";
        }

        if (newImageBytes is null || newImageBytes.Length == 0)
        {
            return "Error comparing metadata: New image bytes are empty.";
        }

        try
        {
            var origDirectories = ImageMetadataReader.ReadMetadata(originalImagePath);
            using var newImageStream = new MemoryStream(newImageBytes);
            var newDirectories = ImageMetadataReader.ReadMetadata(newImageStream);

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
        catch (Exception ex)
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
