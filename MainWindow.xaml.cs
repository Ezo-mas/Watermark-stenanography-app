using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
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
            _baseVisiblePath = ofd.FileName;
            TxtBaseImagePath.Text = ofd.FileName;
            ImgPreviewVisible.Source = LoadImageWithoutLocking(ofd.FileName);
        }
    }

    private void BtnSelectWatermark_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp" };
        if (ofd.ShowDialog() == true)
        {
            _watermarkVisiblePath = ofd.FileName;
            TxtWatermarkPath.Text = ofd.FileName;
        }
    }

    // Helper method to load images into the UI without keeping the file locked,
    // allowing the user to overwrite the original file if they want to.
    private BitmapImage LoadImageWithoutLocking(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // This is the magic line that releases the file lock
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        return bitmap;
    }

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

        string position = ((ComboBoxItem)CmbPosition.SelectedItem).Content.ToString() ?? "Center";
        bool isTiled = ChkRepeat.IsChecked == true;

        try
        {
            _previewVisibleBytes = WatermarkHelper.ApplyVisibleWatermark(
                _baseVisiblePath, 
                _watermarkVisiblePath, 
                position, 
                angle, 
                isTiled);

            // Update UI with preview
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(_previewVisibleBytes);
            bitmap.EndInit();
            ImgPreviewVisible.Source = bitmap;
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
                File.WriteAllBytes(sfd.FileName, _previewVisibleBytes);
                AppMessageBox.Show("Image saved successfully!");
            }
            catch (IOException ex)
            {
                AppMessageBox.Show($"File save error (is it open in another program?): {ex.Message}");
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
            _stegaImagePath = ofd.FileName;
            TxtStegaImagePath.Text = ofd.FileName;
            ImgPreviewStega.Source = LoadImageWithoutLocking(ofd.FileName);
            
            // Clear fields on new load
            TxtExtractedMessage.Text = string.Empty;
        }
    }

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
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(_previewStegaBytes);
            bitmap.EndInit();
            ImgPreviewStega.Source = bitmap;
            
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

        // Steganography requires precise pixel data. PNG is lossless. JPG will destroy the data.
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
            catch (IOException ex)
            {
                AppMessageBox.Show($"File save error (is it open in another program?): {ex.Message}");
            }
        }
    }

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
