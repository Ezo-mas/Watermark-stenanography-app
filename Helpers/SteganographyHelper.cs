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
        
        // Convert text + marker to bit array
        string fullMessage = textToHide + EndOfMessageMarker;
        byte[] messageBytes = Encoding.UTF8.GetBytes(fullMessage);
        
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
        var bytes = new System.Collections.Generic.List<byte>();
        
        byte currentByte = 0;
        int bitIndex = 0;
        string currentDecodedText = "";
        
        // Need to loop manually as ProcessPixelRows can be tricky when we need early exit
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                
                // Read the Least Significant Bit from Blue channel
                int bit = pixel.B & 1;
                
                currentByte = (byte)((currentByte << 1) | bit);
                bitIndex++;
                
                if (bitIndex >= 8)
                {
                    bytes.Add(currentByte);
                    
                    // Decode incrementally for performance and EOF checking
                    currentDecodedText = Encoding.UTF8.GetString(bytes.ToArray());
                    if (currentDecodedText.EndsWith(EndOfMessageMarker))
                    {
                        return currentDecodedText.Replace(EndOfMessageMarker, "");
                    }
                    
                    currentByte = 0;
                    bitIndex = 0;
                }
            }
        }
        
        return "No message found or image is corrupted.";
    }
    
    public static byte[] ClearMessage(string imagePath)
    {
         using var image = Image.Load<Rgba32>(imagePath);
         
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