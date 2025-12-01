namespace Uchat.Client.Views
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using Uchat.Client.ViewModels;

    /// <summary>
    /// Logic usability with ChatView.xaml.
    /// </summary>
    public partial class ChatView : UserControl
    {
        private const double ScrollThreshold = 50.0; // Показывать кнопку, если пользователь выше последнего сообщения более чем на 50 пикселей
        private const int SmoothScrollDurationMs = 300;
        private DispatcherTimer? smoothScrollTimer;
        private double smoothScrollTarget;
        private double smoothScrollStart;
        private DateTime smoothScrollStartTime;
        private string? currentAudioTempPath;
        private AttachmentViewModel? currentPlayingAttachment;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatView"/> class.
        /// </summary>
        public ChatView()
        {
            this.InitializeComponent();
            this.DataContextChanged += this.OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChatViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= this.ViewModel_PropertyChanged;
                oldViewModel.Messages.CollectionChanged -= this.Messages_CollectionChanged;
                oldViewModel.OnAudioPlayRequested -= this.ViewModel_OnAudioPlayRequested;
                oldViewModel.OnAudioPlayPauseRequested -= this.ViewModel_OnAudioPlayPauseRequested;
                oldViewModel.OnAudioStopRequested -= this.ViewModel_OnAudioStopRequested;
            }

            if (e.NewValue is ChatViewModel newViewModel)
            {
                newViewModel.PropertyChanged += this.ViewModel_PropertyChanged;
                newViewModel.Messages.CollectionChanged += this.Messages_CollectionChanged;
                newViewModel.OnAudioPlayRequested += this.ViewModel_OnAudioPlayRequested;
                newViewModel.OnAudioPlayPauseRequested += this.ViewModel_OnAudioPlayPauseRequested;
                newViewModel.OnAudioStopRequested += this.ViewModel_OnAudioStopRequested;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatViewModel.IsLoadingMessages))
            {
                var viewModel = this.DataContext as ChatViewModel;
                if (viewModel != null && !viewModel.IsLoadingMessages)
                {
                    // Messages just finished loading, scroll to bottom
                    this.ScrollToBottom();
                }
            }
        }

        private void Messages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var viewModel = this.DataContext as ChatViewModel;
            if (viewModel != null && viewModel.IsLoadingMessages)
            {
                return;
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                // New message added, scroll to bottom
                this.ScrollToBottom();
            }
        }

        private void ScrollToBottom()
        {
            Application.Current.Dispatcher.InvokeAsync(
                () =>
                {
                    this.MessagesScrollViewer.ScrollToBottom();

                    // Скрываем кнопку после скролла
                    this.UpdateScrollButtonVisibility();
                },
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void MessagesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            this.UpdateScrollButtonVisibility();
        }

        private void UpdateScrollButtonVisibility()
        {
            if (this.MessagesScrollViewer == null)
            {
                return;
            }

            // Проверяем, находится ли пользователь внизу скролла
            var verticalOffset = this.MessagesScrollViewer.VerticalOffset;
            var scrollableHeight = this.MessagesScrollViewer.ScrollableHeight;
            var isAtBottom = scrollableHeight - verticalOffset <= ScrollThreshold;

            // Показываем кнопку, если пользователь не внизу и есть что скроллить
            this.ScrollToBottomButton.Visibility = (!isAtBottom && scrollableHeight > 0)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
        {
            this.SmoothScrollToBottom();
        }

        private void SmoothScrollToBottom()
        {
            if (this.MessagesScrollViewer == null)
            {
                return;
            }

            // Останавливаем предыдущий таймер, если он запущен
            if (this.smoothScrollTimer != null)
            {
                this.smoothScrollTimer.Stop();
                this.smoothScrollTimer = null;
            }

            this.smoothScrollTarget = this.MessagesScrollViewer.ScrollableHeight;
            this.smoothScrollStart = this.MessagesScrollViewer.VerticalOffset;
            this.smoothScrollStartTime = DateTime.Now;

            if (Math.Abs(this.smoothScrollTarget - this.smoothScrollStart) < 1.0)
            {
                // Уже внизу
                this.UpdateScrollButtonVisibility();
                return;
            }

            // Создаем таймер для плавного скролла
            this.smoothScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16), // ~60 FPS
            };
            this.smoothScrollTimer.Tick += this.SmoothScrollTimer_Tick;
            this.smoothScrollTimer.Start();
        }

        private void SmoothScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (this.MessagesScrollViewer == null || this.smoothScrollTimer == null)
            {
                return;
            }

            var elapsed = (DateTime.Now - this.smoothScrollStartTime).TotalMilliseconds;
            var progress = Math.Min(elapsed / SmoothScrollDurationMs, 1.0);

            // Используем easing функцию для плавности (ease-out)
            var easedProgress = 1.0 - Math.Pow(1.0 - progress, 3);

            var currentOffset = this.smoothScrollStart + ((this.smoothScrollTarget - this.smoothScrollStart) * easedProgress);
            this.MessagesScrollViewer.ScrollToVerticalOffset(currentOffset);

            if (progress >= 1.0)
            {
                // Анимация завершена
                this.smoothScrollTimer.Stop();
                this.smoothScrollTimer = null;
                this.MessagesScrollViewer.ScrollToVerticalOffset(this.smoothScrollTarget);
                this.UpdateScrollButtonVisibility();
            }
        }

        /// <summary>
        /// Handles the DragEnter event for the chat area.
        /// </summary>
        private void ChatArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        /// <summary>
        /// Handles the DragOver event for the chat area.
        /// </summary>
        private void ChatArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        /// <summary>
        /// Handles the Drop event for the chat area.
        /// </summary>
        private void ChatArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var viewModel = this.DataContext as ChatViewModel;
                    if (viewModel != null)
                    {
                        // Use the same logic as AttachFileCommand but directly with file paths
                        viewModel.ProcessDroppedFilesAsync(files);
                    }
                }
            }

            e.Handled = true;
        }

        private async void ViewModel_OnAudioPlayRequested(AttachmentViewModel attachment, string audioUrl)
        {
            try
            {
                // MediaElement doesn't support HTTP with authentication headers
                // So we need to download the file to a temp location first
                if (attachment.AttachmentDto == null)
                {
                    attachment.IsPlaying = false;
                    return;
                }

                // Clean up previous temp file if exists
                if (!string.IsNullOrEmpty(this.currentAudioTempPath) && System.IO.File.Exists(this.currentAudioTempPath))
                {
                    try
                    {
                        System.IO.File.Delete(this.currentAudioTempPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                // Download to temp file
                var viewModel = this.DataContext as ChatViewModel;
                if (viewModel != null)
                {
                    var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"uchat_audio_{Guid.NewGuid()}{System.IO.Path.GetExtension(attachment.FileName)}");

                    // Download using the service
                    try
                    {
                        // Store reference to current attachment
                        this.currentPlayingAttachment = attachment;

                        // Use DownloadAttachmentAsync which handles authentication
                        var localPath = await viewModel.DownloadAttachmentForPlaybackAsync(attachment.AttachmentDto);
                        this.currentAudioTempPath = localPath;

                        // Now play from local file
                        this.AudioPlayer.Source = new Uri(localPath);

                        // Don't call Play() here - wait for MediaOpened event
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to download audio: {ex.Message}");
                        attachment.IsPlaying = false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set audio source: {ex.Message}");
                attachment.IsPlaying = false;
            }
        }

        private void AudioPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                this.AudioPlayer.Play();

                // Update IsPlaying state for the current attachment
                if (this.currentPlayingAttachment != null)
                {
                    this.currentPlayingAttachment.IsPlaying = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play audio: {ex.Message}");
                if (this.currentPlayingAttachment != null)
                {
                    this.currentPlayingAttachment.IsPlaying = false;
                }
            }
        }

        private void AudioPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Media failed: {e.ErrorException?.Message}");

            // Reset playing state
            if (this.currentPlayingAttachment != null)
            {
                this.currentPlayingAttachment.IsPlaying = false;
                this.currentPlayingAttachment = null;
            }
        }

        private void ViewModel_OnAudioPlayPauseRequested(AttachmentViewModel attachment)
        {
            try
            {
                // Check if this is the currently playing attachment
                if (this.currentPlayingAttachment == attachment && this.AudioPlayer.Source != null)
                {
                    // Toggle play/pause based on current state
                    if (attachment.IsPlaying)
                    {
                        this.AudioPlayer.Pause();
                        attachment.IsPlaying = false;
                    }
                    else
                    {
                        this.AudioPlayer.Play();
                        attachment.IsPlaying = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to toggle audio playback: {ex.Message}");
                attachment.IsPlaying = false;
            }
        }

        private void ViewModel_OnAudioStopRequested(AttachmentViewModel attachment)
        {
            try
            {
                // Stop the audio player
                this.AudioPlayer.Stop();
                attachment.IsPlaying = false;
                this.currentPlayingAttachment = null;

                // Clean up temp file
                if (!string.IsNullOrEmpty(this.currentAudioTempPath) && System.IO.File.Exists(this.currentAudioTempPath))
                {
                    try
                    {
                        System.IO.File.Delete(this.currentAudioTempPath);
                        this.currentAudioTempPath = null;
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop audio: {ex.Message}");
            }
        }

        private void AudioPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Store references
            var attachment = this.currentPlayingAttachment;
            var tempPath = this.currentAudioTempPath;

            // Clear references immediately
            this.currentPlayingAttachment = null;
            this.currentAudioTempPath = null;

            // Update UI state asynchronously with high priority to avoid blocking
            // This ensures UI updates quickly without blocking the MediaEnded event handler
            if (attachment != null)
            {
                this.Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new Action(() =>
                    {
                        attachment.IsPlaying = false;
                    }));
            }

            // Clean up temp file asynchronously in background thread (non-blocking)
            if (!string.IsNullOrEmpty(tempPath))
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (System.IO.File.Exists(tempPath))
                        {
                            System.IO.File.Delete(tempPath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                });
            }
        }
    }
}
