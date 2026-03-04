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

namespace WatermarkApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string _baseVisiblePath = string.Empty;
    private string _watermarkVisiblePath = string.Empty;
    private byte[] _previewVisibleBytes = Array.Empty<byte>();

    public MainWindow()
    {
        InitializeComponent();
    }

    // --- VISIBLE WATERMARK SECTION ---

    private void BtnSelectImage_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" };
        if (ofd.ShowDialog() == true)
        {
            _baseVisiblePath = ofd.FileName;
            TxtBaseImagePath.Text = ofd.FileName;
            ImgPreviewVisible.Source = new BitmapImage(new Uri(ofd.FileName));
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

    private void BtnApplyVisible_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseVisiblePath) || string.IsNullOrEmpty(_watermarkVisiblePath))
        {
            MessageBox.Show("Please select both a base image and a watermark.");
            return;
        }

        float angle = 0;
        if (!float.TryParse(TxtAngle.Text, out angle))
        {
            MessageBox.Show("Invalid angle value.");
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
            MessageBox.Show($"Error applying watermark: {ex.Message}");
        }
    }

    private void BtnSaveVisible_Click(object sender, RoutedEventArgs e)
    {
        if (_previewVisibleBytes == null || _previewVisibleBytes.Length == 0)
        {
            MessageBox.Show("Apply a watermark first before saving.");
            return;
        }

        var sfd = new SaveFileDialog { Filter = "PNG Image|*.png" };
        if (sfd.ShowDialog() == true)
        {
            File.WriteAllBytes(sfd.FileName, _previewVisibleBytes);
            MessageBox.Show("Image saved successfully!");
        }
    }

    private void BtnViewMetaVisible_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseVisiblePath))
        {
            MessageBox.Show("Please select a base image first.");
            return;
        }

        string metadata = MetadataHelper.GetMetadata(_baseVisiblePath);
        MessageBox.Show(metadata, "Image Metadata", MessageBoxButton.OK, MessageBoxImage.Information);
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
            ImgPreviewStega.Source = new BitmapImage(new Uri(ofd.FileName));
            
            // Clear fields on new load
            TxtExtractedMessage.Text = string.Empty;
        }
    }

    private void BtnHideText_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
            MessageBox.Show("Please select an image first.");
            return;
        }
        
        if (string.IsNullOrEmpty(TxtSecretMessage.Text))
        {
            MessageBox.Show("Please enter a message to hide.");
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
            
            MessageBox.Show("Message hidden successfully. Please save the image as PNG!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Encoding failed: {ex.Message}");
        }
    }

    private void BtnSaveStega_Click(object sender, RoutedEventArgs e)
    {
        if (_previewStegaBytes == null || _previewStegaBytes.Length == 0)
        {
            MessageBox.Show("Hide a message first before saving.");
            return;
        }

        // Steganography requires precise pixel data. PNG is lossless. JPG will destroy the data.
        var sfd = new SaveFileDialog { Filter = "PNG Image (Required for Steganography)|*.png" };
        if (sfd.ShowDialog() == true)
        {
            File.WriteAllBytes(sfd.FileName, _previewStegaBytes);
            MessageBox.Show("Encoded image saved successfully!");
        }
    }

    private void BtnExtractText_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
             MessageBox.Show("Please select an encoded image first.");
             return;
        }
        
        try
        {
            string decodedText = SteganographyHelper.DecodeText(_stegaImagePath);
            TxtExtractedMessage.Text = decodedText;
        }
        catch (Exception ex)
        {
             MessageBox.Show($"Decoding failed. Are you sure this image contains a hidden message? Error: {ex.Message}");
        }
    }

    private void BtnClearMessage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
            MessageBox.Show("Please select an image first.");
            return;
        }
        
        try
        {
            byte[] clearedBytes = SteganographyHelper.ClearMessage(_stegaImagePath);
            
            var sfd = new SaveFileDialog { Filter = "PNG Image|*.png" };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllBytes(sfd.FileName, clearedBytes);
                MessageBox.Show("Cleared image saved successfully!");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Clear failed: {ex.Message}");
        }
    }

    private void BtnViewMetaStega_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_stegaImagePath))
        {
            MessageBox.Show("Please select an image first.");
            return;
        }

        string metadata = MetadataHelper.GetMetadata(_stegaImagePath);
        MessageBox.Show(metadata, "Image Metadata", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}