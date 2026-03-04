using System;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WatermarkApp.Helpers;

public static class SteganographyHelper
{
    // A simple EOF marker to signify where the text stops
    private const string EndOfMessageMarker = "@@END@@";

    public static byte[] EncodeText(string imagePath, string textToHide)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        
        string fullMessage = textToHide + EndOfMessageMarker;
        byte[] messageBytes = Encoding.UTF8.GetBytes(fullMessage);
        
        int maximumByteCapacity = (image.Width * image.Height) / 8;
        
        if (messageBytes.Length > maximumByteCapacity)
        {
            int allowedVisibleBytes = maximumByteCapacity - Encoding.UTF8.GetByteCount(EndOfMessageMarker);
            int bytesAttempted = messageBytes.Length - Encoding.UTF8.GetByteCount(EndOfMessageMarker);

            throw new Exception($"Message is too large for this image.\n\nImage allows: {allowedVisibleBytes} bytes.\nYou attempted: {textToHide.Length} characters ({bytesAttempted} bytes). Note that special characters and line breaks may use more than 1 byte.");
        }
        
        int messageIndex = 0;
        int bitIndex = 0;

        // Iterate through image pixels
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (messageIndex < messageBytes.Length)
                    {
                        byte currentByte = messageBytes[messageIndex];
                        int bit = (currentByte >> (7 - bitIndex)) & 1;

                        // Clear Least Significant Bit of Blue channel, then set our bit
                        ref Rgba32 pixel = ref row[x];
                        pixel.B = (byte)((pixel.B & 0xFE) | bit);

                        bitIndex++;
                        if (bitIndex >= 8)
                        {
                            bitIndex = 0;
                            messageIndex++;
                        }
                    }
                }
            }
        });

        // Ensure we don't return data lossy formats like JPG which will destroy LSB
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static string DecodeText(string imagePath)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        return DecodeImage(image);
    }

    public static string DecodeTextFromBytes(byte[] imageBytes)
    {
        using var image = Image.Load<Rgba32>(imageBytes);
        return DecodeImage(image);
    }

    private static string DecodeImage(Image<Rgba32> image)
    {
        var bytes = new System.Collections.Generic.List<byte>();
        
        byte[] markerBytes = Encoding.UTF8.GetBytes(EndOfMessageMarker);
        byte currentByte = 0;
        int bitIndex = 0;
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                int bit = pixel.B & 1;
                
                currentByte = (byte)((currentByte << 1) | bit);
                bitIndex++;
                
                if (bitIndex >= 8)
                {
                    bytes.Add(currentByte);
                    
                    if (bytes.Count >= markerBytes.Length)
                    {
                        bool isMatch = true;
                        for (int i = 0; i < markerBytes.Length; i++)
                        {
                            if (bytes[bytes.Count - markerBytes.Length + i] != markerBytes[i])
                            {
                                isMatch = false;
                                break;
                            }
                        }
                        
                        if (isMatch)
                        {
                            return Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count - markerBytes.Length);
                        }
                    }
                    
                    currentByte = 0;
                    bitIndex = 0;
                    
                    int maximumByteCapacity = (image.Width * image.Height) / 8;
                    if (bytes.Count > maximumByteCapacity) 
                    {
                        return "No hidden message found (reached end of image capacity).";
                    }
                }
            }
        }
        
        return "No message found or image is corrupted.";
    }

    public static byte[] ClearMessage(string imagePath)
    {
         using var image = Image.Load<Rgba32>(imagePath);
         return PerformClear(image);
    }

    public static byte[] ClearMessageFromBytes(byte[] imageBytes)
    {
        using var image = Image.Load<Rgba32>(imageBytes);
        return PerformClear(image);
    }

    private static byte[] PerformClear(Image<Rgba32> image)
    {
         image.ProcessPixelRows(accessor =>
         {
             for (int y = 0; y < accessor.Height; y++)
             {
                 Span<Rgba32> row = accessor.GetRowSpan(y);
                 for (int x = 0; x < row.Length; x++)
                 {
                     // Clear the LSB on the blue channel for every pixel
                     ref Rgba32 pixel = ref row[x];
                     pixel.B = (byte)(pixel.B & 0xFE); 
                 }
             }
         });
         
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}