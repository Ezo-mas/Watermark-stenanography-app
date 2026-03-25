# Watermark & Steganography App

A Windows desktop application built with C# and WPF for applying visible digital watermarks to images, as well as hiding and recovering secret text messages using LSB (Least Significant Bit) steganography.

## Features

- **Visible Watermarking:** Apply visible image watermarks. Auto-scales based on the source image size.
- **Invisible Text Steganography:** Hide secret text inside images. Uses the Least Significant Bit (LSB) of the blue channel and stores a framed payload (`magic + length + checksum`) for safer decode validation.
- **Steganography Decoding & Clearing:** Extract hidden text from an image, and optionally destroy the hidden message, clearing it from the image.
- **Metadata Comparison:** View and compare hidden EXIF data and other metadata tags to see exactly what changes between the original and modified image.
- **Lossless Handling:** Enforces `.png` exporting for steganography to prevent data loss via compression (which occurs with JPEGs).

## Tech Stack

- **Framework:** .NET 9.0 (C#)
- **UI:** Windows Presentation Foundation (WPF)
- **Image Processing:** [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) (Cross-platform, high-performance image processing)
- **Metadata Inspection:** [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet)

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows OS (Required for WPF applications)

## Installation & Running

You do not need to install the application through a traditional installer. You can run it directly from the source code.

1. Clone or download this repository.
2. Open a terminal (or Developer Command Prompt) in the root folder (where the `.sln` or `.csproj` is located).
3. Run the application using the .NET CLI:
   ```bash
   dotnet run
   ```
   *(Alternatively, you can open the project in Visual Studio 2022 and click "Start" / F5).*

## Usage

### Visible Watermarks
1. Open the app and ensure you are on the **Visible Watermarking** tab.
2. Load a base image using the **Select Image** button.
3. Select an image to use as your watermark.
4. Click **Apply Watermark** to see a preview.
5. Click **Save Image** to export the result. (You can also compare metadata using the **Compare Metadata** button).

### Secret Messages (Steganography)
1. Switch to the **Steganography** tab.
2. Load your original image.
3. Type your secret message into the text box.
4. Click **Hide Text (Encode)**.
5. Save the image (it must be saved as a `.png` to preserve the precise pixel data; the app automatically handles this).
6. To **Decode**, load a previously encoded image and click **Extract Text (Decode)**. The secret text will appear on the screen.

#### Capacity Math
- The app writes 1 bit per pixel (blue-channel LSB), so total storable bytes are `floor(width * height / 8)`.
- Steganography metadata header is 12 bytes (`magic + length + checksum`), so max payload bytes are:
  `floor(width * height / 8) - 12`.
- UTF-8 is used for text; some characters use multiple bytes.

#### PNG vs JPEG
- Use `.png` for steganography output.
- Saving to `.jpg` can sometimes appear to work for very short text, but JPEG is lossy and not reliable for LSB-based hidden data.

## Project Structure

- `MainWindow.xaml` / `MainWindow.xaml.cs`: Holds the UI layout and state logic.
- `Helpers/WatermarkHelper.cs`: Logic for rendering an image watermark directly onto the base image.
- `Helpers/SteganographyHelper.cs`: Encodes/decodes LSB payloads with framed header validation and message clearing logic.
- `Helpers/MetadataHelper.cs`: Reads and compares EXIF tags to show data modifications.
