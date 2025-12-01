namespace Uchat.Client.Views
{
    using System;
    using System.IO;
    using System.Windows;
    using Microsoft.Win32;
    using Serilog;

    /// <summary>
    /// Interaction logic for CodePreviewWindow.xaml.
    /// </summary>
    public partial class CodePreviewWindow : Window
    {
        private string codeContent = string.Empty;
        private string fileName = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodePreviewWindow"/> class.
        /// </summary>
        public CodePreviewWindow()
        {
            this.InitializeComponent();
            this.KeyDown += this.CodePreviewWindow_KeyDown;
        }

        private void CodePreviewWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
            }
        }

        /// <summary>
        /// Loads code content into the preview window.
        /// </summary>
        /// <param name="code">The code content.</param>
        /// <param name="fileName">The file name.</param>
        public void LoadCode(string code, string fileName)
        {
            this.codeContent = code;
            this.fileName = fileName;
            this.Title = $"Code Preview - {fileName}";
            this.CodePreview.LoadCode(code, fileName);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(this.codeContent);
                MessageBox.Show("Code copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Log.Information("Code copied to clipboard from {FileName}", this.fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.Error(ex, "Failed to copy code to clipboard");
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    FileName = this.fileName,
                    Filter = "All Files (*.*)|*.*",
                    Title = "Save Code File",
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, this.codeContent);
                    MessageBox.Show("File saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log.Information("Code file saved to {FilePath}", saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.Error(ex, "Failed to save code file");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
