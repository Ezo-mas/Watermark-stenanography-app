using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace WatermarkApp.Helpers;

public static class WatermarkHelper
{
    public static byte[] ApplyVisibleWatermark(string basePath, string watermarkPath, string position, float angle, bool isTiled)
    {
        using var baseImage = Image.Load<Rgba32>(basePath);
        using var watermark = Image.Load<Rgba32>(watermarkPath);

        // Auto-scale the watermark if it happens to be larger than the base image
        if (watermark.Width > baseImage.Width || watermark.Height > baseImage.Height)
        {
            int newWidth = Math.Min(watermark.Width, baseImage.Width);
            int newHeight = Math.Min(watermark.Height, baseImage.Height);
            watermark.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(newWidth, newHeight),
                Mode = ResizeMode.Max
            }));
        }

        // Rotate the watermark based on user input
        if (angle != 0)
        {
            watermark.Mutate(x => x.Rotate(angle));
        }

        // Apply a bit of opacity to the watermark (0.5f)
        float opacity = 0.5f;

        if (isTiled)
        {
            // Tile the watermark across the entire base image
            for (int y = 0; y < baseImage.Height; y += watermark.Height)
            {
                for (int x = 0; x < baseImage.Width; x += watermark.Width)
                {
                    baseImage.Mutate(ctx => ctx.DrawImage(watermark, new Point(x, y), opacity));
                }
            }
        }
        else
        {
            // Calculate X, Y based on desired position
            int x = 0;
            int y = 0;

            switch (position)
            {
                case "Top Left":
                    break;
                case "Top Right":
                    x = baseImage.Width - watermark.Width;
                    break;
                case "Bottom Left":
                    y = baseImage.Height - watermark.Height;
                    break;
                case "Bottom Right":
                    x = baseImage.Width - watermark.Width;
                    y = baseImage.Height - watermark.Height;
                    break;
                case "Top Center":
                    x = (baseImage.Width - watermark.Width) / 2;
                    break;
                case "Bottom Center":
                    x = (baseImage.Width - watermark.Width) / 2;
                    y = baseImage.Height - watermark.Height;
                    break;
                case "Center":
                default:
                    x = (baseImage.Width - watermark.Width) / 2;
                    y = (baseImage.Height - watermark.Height) / 2;
                    break;
            }

            // Ensure coordinates aren't completely out of bounds for very large watermarks
            x = Math.Max(0, x);
            y = Math.Max(0, y);

            baseImage.Mutate(ctx => ctx.DrawImage(watermark, new Point(x, y), opacity));
        }

        // Save to stream to return to the WPF UI
        using var ms = new MemoryStream();
        baseImage.SaveAsPng(ms);
        return ms.ToArray();
    }
}