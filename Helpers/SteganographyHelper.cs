using System.Buffers.Binary;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WatermarkApp.Helpers;

public static class SteganographyHelper
{
    // 4-byte signature + 4-byte payload length + 4-byte checksum
    private static readonly byte[] MessageMagic = Encoding.ASCII.GetBytes("WMSG");
    private const int HeaderSize = 12;

    /// <summary>
    /// Hides a text message within an image by modifying the Least Significant Bit (LSB) of the blue color channel.
    /// </summary>
    public static byte[] EncodeText(string imagePath, string textToHide)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path must not be empty.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("The selected image file was not found.", imagePath);
        }

        if (textToHide is null)
        {
            throw new ArgumentNullException(nameof(textToHide));
        }

        using var image = Image.Load<Rgba32>(imagePath);

        byte[] payloadBytes = Encoding.UTF8.GetBytes(textToHide);
        byte[] messageBytes = BuildFramedMessage(payloadBytes);

        int maximumByteCapacity = image.Width * image.Height / 8;

        if (messageBytes.Length > maximumByteCapacity)
        {
            int allowedVisibleBytes = Math.Max(0, maximumByteCapacity - HeaderSize);
            int bytesAttempted = payloadBytes.Length;

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
                    if (messageIndex >= messageBytes.Length)
                    {
                        break;
                    }

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

                if (messageIndex >= messageBytes.Length)
                {
                    break;
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
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path must not be empty.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("The selected image file was not found.", imagePath);
        }

        using var image = Image.Load<Rgba32>(imagePath);
        return DecodeImage(image);
    }

    public static string DecodeTextFromBytes(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes must not be empty.", nameof(imageBytes));
        }

        using var image = Image.Load<Rgba32>(imageBytes);
        return DecodeImage(image);
    }

    /// <summary>
    /// Decodes framed message bytes from image LSBs and validates payload checksum.
    /// </summary>
    private static string DecodeImage(Image<Rgba32> image)
    {
        int maximumByteCapacity = image.Width * image.Height / 8;
        if (maximumByteCapacity < HeaderSize)
        {
            return "No hidden message found (image too small for header).";
        }

        var bytes = new List<byte>(Math.Min(maximumByteCapacity, 512));
        byte currentByte = 0;
        int bitIndex = 0;
        int totalBytesNeeded = HeaderSize;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                int bit = pixel.B & 1;

                currentByte = (byte)((currentByte << 1) | bit);
                bitIndex++;

                if (bitIndex < 8)
                {
                    continue;
                }

                bytes.Add(currentByte);
                currentByte = 0;
                bitIndex = 0;

                if (bytes.Count == MessageMagic.Length && !HasValidMagicPrefix(bytes))
                {
                    return "No hidden message found.";
                }

                if (bytes.Count == HeaderSize)
                {
                    byte[] headerBytes = bytes.ToArray();
                    int payloadLength = BinaryPrimitives.ReadInt32BigEndian(headerBytes.AsSpan(4, 4));
                    if (payloadLength < 0 || payloadLength > maximumByteCapacity - HeaderSize)
                    {
                        return "No message found or image is corrupted.";
                    }

                    totalBytesNeeded = HeaderSize + payloadLength;

                    if (payloadLength == 0)
                    {
                        uint expectedChecksum = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(8, 4));
                        uint actualChecksum = ComputeChecksum(Array.Empty<byte>());
                        return expectedChecksum == actualChecksum ? string.Empty : "No message found or image is corrupted.";
                    }
                }

                if (bytes.Count == totalBytesNeeded)
                {
                    byte[] allBytes = bytes.ToArray();
                    int payloadLength = BinaryPrimitives.ReadInt32BigEndian(allBytes.AsSpan(4, 4));
                    uint expectedChecksum = BinaryPrimitives.ReadUInt32BigEndian(allBytes.AsSpan(8, 4));
                    byte[] payloadBytes = allBytes.AsSpan(HeaderSize, payloadLength).ToArray();
                    uint actualChecksum = ComputeChecksum(payloadBytes);

                    if (actualChecksum != expectedChecksum)
                    {
                        return "No message found or image is corrupted.";
                    }

                    return Encoding.UTF8.GetString(payloadBytes);
                }
            }
        }

        return "No message found or image is corrupted.";
    }

    public static byte[] ClearMessage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path must not be empty.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("The selected image file was not found.", imagePath);
        }

        using var image = Image.Load<Rgba32>(imagePath);
        return PerformClear(image);
    }

    public static byte[] ClearMessageFromBytes(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes must not be empty.", nameof(imageBytes));
        }

        using var image = Image.Load<Rgba32>(imageBytes);
        return PerformClear(image);
    }

    /// <summary>
    /// Overwrites the LSB of the blue channel for all pixels across the image, zeroing out any hidden messages.
    /// </summary>
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

    private static byte[] BuildFramedMessage(byte[] payloadBytes)
    {
        var framedMessage = new byte[HeaderSize + payloadBytes.Length];
        MessageMagic.CopyTo(framedMessage, 0);
        BinaryPrimitives.WriteInt32BigEndian(framedMessage.AsSpan(4, 4), payloadBytes.Length);
        BinaryPrimitives.WriteUInt32BigEndian(framedMessage.AsSpan(8, 4), ComputeChecksum(payloadBytes));
        payloadBytes.CopyTo(framedMessage, HeaderSize);
        return framedMessage;
    }

    private static bool HasValidMagicPrefix(List<byte> bytes)
    {
        if (bytes.Count < MessageMagic.Length)
        {
            return false;
        }

        for (int i = 0; i < MessageMagic.Length; i++)
        {
            if (bytes[i] != MessageMagic[i])
            {
                return false;
            }
        }

        return true;
    }

    private static uint ComputeChecksum(byte[] payload)
    {
        const uint fnvOffsetBasis = 2166136261;
        const uint fnvPrime = 16777619;

        uint hash = fnvOffsetBasis;
        foreach (byte b in payload)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }
}
