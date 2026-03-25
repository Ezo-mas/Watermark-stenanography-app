using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WatermarkApp.Helpers;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WatermarkApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string _baseVisiblePath = string.Empty;
    private string _watermarkVisiblePath = string.Empty;
    private byte[] _previewVisibleBytes = Array.Empty<byte>();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();
        
        // Try enabling dark title bar on Windows 10 updated / Windows 11
        try
        {
            this.SourceInitialized += (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int useImmersiveDarkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
            };
        }
        catch { /* Ignore if not supported on older windows */ }
    }

    // --- VISIBLE WATERMARK SECTION ---

    private void BtnSelectImage_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" };
        if (ofd.ShowDialog() == true)
        {
            BitmapImage? loadedImage = LoadImageWithoutLocking(ofd.FileName);
            if (loadedImage is null)
            {
                _baseVisiblePath = string.Empty;
                TxtBaseImagePath.Text = "No image selected";
                _previewVisibleBytes = Array.Empty<byte>();
                ImgPreviewVisible.Source = null;
                return;
            }

            _baseVisiblePath = ofd.FileName;
            TxtBaseImagePath.Text = ofd.FileName;
            _previewVisibleBytes = Array.Empty<byte>();
            ImgPreviewVisible.Source = loadedImage;
        }
    }

    private void BtnSelectWatermark_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp" };
        if (ofd.ShowDialog() == true)
        {
            _watermarkVisiblePath = ofd.FileName;
            TxtWatermarkPath.Text = ofd.FileName;
            _previewVisibleBytes = Array.Empty<byte>();
        }
    }

    // Helper method to load images into the UI without keeping the file locked,
    // allowing the user to overwrite the original file if they want to.
    private BitmapImage? LoadImageWithoutLocking(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                AppMessageBox.Show("The selected image no longer exists.");
                return null;
            }

            byte[] imageBytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(imageBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            AppMessageBox.Show($"Could not load image preview: {ex.Message}");
            return null;
        }
    }

    private BitmapImage? LoadImageFromBytes(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            AppMessageBox.Show($"Could not render image preview: {ex.Message}");
            return null;
        }
    }

    private static void SaveBytesInSelectedFormat(byte[] sourceImageBytes, string outputPath)
    {
        string extension = Path.GetExtension(outputPath).ToLowerInvariant();
        if (extension == ".png")
        {
            File.WriteAllBytes(outputPath, sourceImageBytes);
            return;
        }

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(sourceImageBytes);
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                image.SaveAsJpeg(outputPath);
                break;
            case ".bmp":
                image.SaveAsBmp(outputPath);
                break;
            default:
                throw new InvalidOperationException("Unsupported file extension. Please use .png, .jpg, or .bmp.");
        }
    }

    /// <summary>
    /// Triggers the watermarking process based on selected UI options and displays the preview.
    /// </summary>
    private void BtnApplyVisible_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseVisiblePath) || string.IsNullOrEmpty(_watermarkVisiblePath))
        {
            AppMessageBox.Show("Please select both a base image and a watermark.");
            return;
        }

        float angle = 0;
        if (!float.TryParse(TxtAngle.Text, out angle))
        {
            AppMessageBox.Show("Invalid angle value.");
            return;
        }

        string position = (CmbPosition.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Center";
        bool isTiled = ChkRepeat.IsChecked == true;

        try
        {
            _previewVisibleBytes = WatermarkHelper.ApplyVisibleWatermark(
                _baseVisiblePath, 
                _watermarkVisiblePath, 
                position, 
                angle, 
                isTiled);

            ImgPreviewVisible.Source = LoadImageFromBytes(_previewVisibleBytes);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show($"Error applying watermark: {ex.Message}");
        }
    }

    private void BtnClearVisible_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseVisiblePath))
        {
            AppMessageBox.Show("No image loaded to clear.");
            return;
        }

        // Reset the preview to the original base image
        _previewVisibleBytes = Array.Empty<byte>();
        ImgPreviewVisible.Source = LoadImageWithoutLocking(_baseVisiblePath);
    }

    private void BtnSaveVisible_Click(object sender, RoutedEventArgs e)
    {
        if (_previewVisibleBytes == null || _previewVisibleBytes.Length == 0)
        {
            AppMessageBox.Show("Apply a watermark first before saving.");
            return;
        }

        var sfd = new SaveFileDialog { 
            Filter = "PNG Image|*.png|JPEG Image|*.jpg|BMP Image|*.bmp",
            DefaultExt = "png"
        };
        
        if (sfd.ShowDialog() == true)
        {
            try 
            {
                SaveBytesInSelectedFormat(_previewVisibleBytes, sfd.FileName);
                AppMessageBox.Show("Image saved successfully!");
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Failed to save image: {ex.Message}");
            }
        }
    }

    private void BtnViewMetaVisible_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseVisiblePath))
        {
            AppMessageBox.Show("Please select a base image first.");
            return;
        }

        string metadata = MetadataHelper.GetMetadata(_baseVisiblePath);
        AppMessageBox.Show(metadata, "Image Metadata", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCompareMetaVisible_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseVisiblePath))
        {
            AppMessageBox.Show("Please load an image first.");
            return;
        }

        if (_previewVisibleBytes == null || _previewVisibleBytes.Length == 0)
        {
            AppMessageBox.Show("Please apply a watermark first to generate the new metadata.");
            return;
        }

        string diff = MetadataHelper.CompareMetadata(_baseVisiblePath, _previewVisibleBytes);
        
        // Show differences in a message box, or optionally a simple scrollable window
        AppMessageBox.Show(diff, "Metadata Differences", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // --- INVISIBLE TEXT STEGANOGRAPHY SECTION ---

    private string _stegaImagePath = string.Empty;
    private byte[] _previewStegaBytes = Array.Empty<byte>();

    private void BtnSelectImageStega_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Image Files (PNG recommended)|*.png;*.jpg;*.jpeg;*.bmp" };
        if (ofd.ShowDialog() == true)
        {
            BitmapImage? loadedImage = LoadImageWithoutLocking(ofd.FileName);
            if (loadedImage is null)
            {
                _stegaImagePath = string.Empty;
                TxtStegaImagePath.Text = "No image selected";
                _previewStegaBytes = Array.Empty<byte>();
                ImgPreviewStega.Source = null;
                TxtExtractedMessage.Text = string.Empty;
                return;
            }

            _stegaImagePath = ofd.FileName;
            TxtStegaImagePath.Text = ofd.FileName;
            ImgPreviewStega.Source = loadedImage;
            _previewStegaBytes = Array.Empty<byte>();
            
            // Clear fields on new load
            TxtExtractedMessage.Text = string.Empty;
        }
    }

    /// <summary>
    /// Encodes the user's secret text into the selected image and displays the encoded preview.
    /// </summary>
    private void BtnHideText_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
            AppMessageBox.Show("Please select an image first.");
            return;
        }
        
        if (string.IsNullOrEmpty(TxtSecretMessage.Text))
        {
            AppMessageBox.Show("Please enter a message to hide.");
            return;
        }

        try
        {
            _previewStegaBytes = SteganographyHelper.EncodeText(_stegaImagePath, TxtSecretMessage.Text);
            ImgPreviewStega.Source = LoadImageFromBytes(_previewStegaBytes);
            
            AppMessageBox.Show("Message hidden successfully. Please save the image as PNG!");
        }
        catch (Exception ex)
        {
            AppMessageBox.Show($"Encoding failed: {ex.Message}");
        }
    }

    private void BtnSaveStega_Click(object sender, RoutedEventArgs e)
    {
        if (_previewStegaBytes == null || _previewStegaBytes.Length == 0)
        {
            AppMessageBox.Show("Hide a message first before saving.");
            return;
        }

        // Steganography requires precise pixel data. 
        // Saving as PNG is recommended to avoid compression artifacts that can corrupt the hidden message.
        var sfd = new SaveFileDialog { 
            Filter = "PNG Image (Required for Steganography)|*.png",
            DefaultExt = "png"
        };
        
        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.WriteAllBytes(sfd.FileName, _previewStegaBytes);
                AppMessageBox.Show("Encoded image saved successfully!");
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Failed to save image: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Attempts to extract and display a hidden message from the current image.
    /// </summary>
    private void BtnExtractText_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath) && (_previewStegaBytes == null || _previewStegaBytes.Length == 0))
        {
             AppMessageBox.Show("Please select an image or encode a message first.");
             return;
        }
        
        try
        {
            string decodedText;
            
            // If the user just encoded something and it's sitting in memory, decode that.
            // Otherwise, decode the image loaded from disk.
            if (_previewStegaBytes != null && _previewStegaBytes.Length > 0) 
            {
                decodedText = SteganographyHelper.DecodeTextFromBytes(_previewStegaBytes);
            }
            else 
            {
                decodedText = SteganographyHelper.DecodeText(_stegaImagePath);
            }
            
            TxtExtractedMessage.Text = decodedText;
        }
        catch (Exception ex)
        {
             AppMessageBox.Show($"Decoding failed. Are you sure this image contains a hidden message? Error: {ex.Message}");
        }
    }

    private void BtnClearMessage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
            AppMessageBox.Show("Please select an image first.");
            return;
        }

        try
        {
            // If the user encoded something currently in memory, clear from that.
            // Otherwise, clear the image they loaded from disk.
            byte[] clearedBytes;
            if (_previewStegaBytes != null && _previewStegaBytes.Length > 0)
            {
                clearedBytes = SteganographyHelper.ClearMessageFromBytes(_previewStegaBytes);
            }
            else
            {
                clearedBytes = SteganographyHelper.ClearMessage(_stegaImagePath);
            }

            var sfd = new SaveFileDialog { 
                Filter = "PNG Image|*.png",
                DefaultExt = "png"
            };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllBytes(sfd.FileName, clearedBytes);
                
                // Clear the preview memory since we wiped it
                _previewStegaBytes = Array.Empty<byte>();
                ImgPreviewStega.Source = LoadImageWithoutLocking(sfd.FileName);
                
                AppMessageBox.Show("Cleared image saved successfully!");
            }
        }
        catch (Exception ex)
        {
            AppMessageBox.Show($"Clear failed: {ex.Message}");
        }
    }

    private void BtnViewMetaStega_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
            AppMessageBox.Show("Please select an image first.");
            return;
        }

        string metadata = MetadataHelper.GetMetadata(_stegaImagePath);
        AppMessageBox.Show(metadata, "Image Metadata", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCompareMetaStega_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
            AppMessageBox.Show("Please load an image first.");
            return;
        }

        if (_previewStegaBytes == null || _previewStegaBytes.Length == 0)
        {
            AppMessageBox.Show("Please encode a message first to generate the new metadata.");
            return;
        }

        string diff = MetadataHelper.CompareMetadata(_stegaImagePath, _previewStegaBytes);
        AppMessageBox.Show(diff, "Metadata Differences", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
