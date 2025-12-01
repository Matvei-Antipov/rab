namespace Uchat.Client.Views
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Uchat.Client.ViewModels;

    /// <summary>
    /// Interaction logic for ChatView.xaml.
    /// </summary>
    public partial class ChatView : UserControl
    {
        // Используем MediaPlayer для программного воспроизведения, если нужно,
        // но также поддерживаем обработчики для MediaElement из XAML.
        private readonly MediaPlayer mediaPlayer = new MediaPlayer();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatView"/> class.
        /// </summary>
        public ChatView()
        {
            this.InitializeComponent();
            this.Loaded += this.ChatView_Loaded;
            this.Unloaded += this.ChatView_Unloaded;
        }

        private void ChatView_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ChatViewModel viewModel)
            {
                viewModel.OnAudioPlayRequested += this.ViewModel_OnAudioPlayRequested;
                viewModel.OnAudioPlayPauseRequested += this.ViewModel_OnAudioPlayPauseRequested;
                viewModel.OnAudioStopRequested += this.ViewModel_OnAudioStopRequested;
            }
        }

        private void ChatView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ChatViewModel viewModel)
            {
                viewModel.OnAudioPlayRequested -= this.ViewModel_OnAudioPlayRequested;
                viewModel.OnAudioPlayPauseRequested -= this.ViewModel_OnAudioPlayPauseRequested;
                viewModel.OnAudioStopRequested -= this.ViewModel_OnAudioStopRequested;
            }

            this.mediaPlayer.Close();
        }

        private async void ViewModel_OnAudioPlayRequested(object? sender, object e)
        {
            if (e is AttachmentViewModel audioAttachment && this.DataContext is ChatViewModel viewModel)
            {
                try
                {
                    if (audioAttachment.AttachmentDto != null)
                    {
                        var filePath = await viewModel.DownloadAttachmentForPlaybackAsync(audioAttachment);
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            this.mediaPlayer.Open(new Uri(filePath));
                            this.mediaPlayer.Play();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error playing audio: {ex.Message}");
                }
            }
        }

        private void ViewModel_OnAudioPlayPauseRequested(object? sender, object e)
        {
            // Simplified toggle logic: just pause for now
            this.mediaPlayer.Pause();
        }

        private void ViewModel_OnAudioStopRequested(object? sender, object e)
        {
            this.mediaPlayer.Stop();
        }

        private void MessagesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0)
            {
                if (this.MessagesScrollViewer.VerticalOffset == this.MessagesScrollViewer.ScrollableHeight)
                {
                    this.ScrollToBottomButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.ScrollToBottomButton.Visibility = Visibility.Visible;
                }
            }

            if (e.ExtentHeightChange > 0)
            {
                double oldExtentHeight = e.ExtentHeight - e.ExtentHeightChange;
                double oldVerticalOffset = e.VerticalOffset;
                double oldViewportHeight = e.ViewportHeight;

                if (Math.Abs(oldVerticalOffset + oldViewportHeight - oldExtentHeight) < 1.0)
                {
                    this.MessagesScrollViewer.ScrollToBottom();
                }
            }
        }

        private void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
        {
            this.MessagesScrollViewer.ScrollToBottom();
        }

        private void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (this.DataContext is ChatViewModel viewModel)
                {
                    viewModel.ProcessDroppedFilesAsync(files);
                }
            }
        }

        // --- Обработчики событий для MediaElement в XAML (если вы добавили контрол в разметку) ---
        private void AudioPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            // Логика при открытии медиа (например, обновить статус)
        }

        private void AudioPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Логика при завершении (например, сбросить иконку Play/Pause)
            if (sender is MediaElement mediaElement)
            {
                mediaElement.Stop();
            }
        }

        private void AudioPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Логика при ошибке
            // MessageBox.Show($"Media failed: {e.ErrorException.Message}");
        }
    }
}
