namespace Uchat.Client.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.Input;
    using Serilog;
    using Uchat.Client.Helpers;
    using Uchat.Client.Services;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// View model for the main chat view with conversation list and messages.
    /// </summary>
    public partial class ChatViewModel : ViewModelBase
    {
        private readonly IMessagingService messagingService;
        private readonly IAuthenticationService authenticationService;
        private readonly IUserDirectoryService userDirectoryService;
        private readonly IErrorHandlingService errorHandlingService;
        private readonly IThemeManager themeManager;
        private readonly IFileAttachmentService fileAttachmentService;
        private readonly IImageCompressionService imageCompressionService;
        private readonly INavigationService navigationService;
        private readonly ILogger logger;
        private readonly HashSet<string> receivedMessageIds;
        private ConversationViewModel? selectedConversation;
        private string messageText;
        private bool isLoadingMessages;
        private bool isLoadingConversations;
        private string? errorMessage;
        private MessageViewModel? messageBeingEdited;
        private CancellationTokenSource? typingCancellationTokenSource;
        private ObservableCollection<AttachmentViewModel> pendingAttachments;
        private AttachmentViewModel? currentlyPlayingAudio;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatViewModel"/> class.
        /// </summary>
        /// <param name="messagingService">The messaging service.</param>
        /// <param name="authenticationService">The authentication service.</param>
        /// <param name="userDirectoryService">The user directory service.</param>
        /// <param name="errorHandlingService">The error handling service.</param>
        /// <param name="themeManager">The theme manager.</param>
        /// <param name="fileAttachmentService">The file attachment service.</param>
        /// <param name="imageCompressionService">The image compression service.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="logger">The logger.</param>
        public ChatViewModel(
            IMessagingService messagingService,
            IAuthenticationService authenticationService,
            IUserDirectoryService userDirectoryService,
            IErrorHandlingService errorHandlingService,
            IThemeManager themeManager,
            IFileAttachmentService fileAttachmentService,
            IImageCompressionService imageCompressionService,
            INavigationService navigationService,
            ILogger logger)
        {
            this.messagingService = messagingService;
            this.authenticationService = authenticationService;
            this.userDirectoryService = userDirectoryService;
            this.errorHandlingService = errorHandlingService;
            this.themeManager = themeManager;
            this.fileAttachmentService = fileAttachmentService;
            this.imageCompressionService = imageCompressionService;
            this.navigationService = navigationService;
            this.logger = logger;
            this.receivedMessageIds = new HashSet<string>();
            this.Title = "Uchat - Messages";
            this.messageText = string.Empty;
            this.pendingAttachments = new ObservableCollection<AttachmentViewModel>();

            this.Conversations = new ObservableCollection<ConversationViewModel>();
            this.Messages = new ObservableCollection<IMessageListItem>();

            this.SubscribeToMessagingEvents();
        }

        /// <summary>
        /// Gets the conversations collection.
        /// </summary>
        public ObservableCollection<ConversationViewModel> Conversations { get; }

        /// <summary>
        /// Gets the messages collection for the selected conversation.
        /// Contains both messages and date separators.
        /// </summary>
        public ObservableCollection<IMessageListItem> Messages { get; }

        /// <summary>
        /// Gets the pending attachments collection to be sent with next message.
        /// </summary>
        public ObservableCollection<AttachmentViewModel> PendingAttachments
        {
            get => this.pendingAttachments;
        }

        /// <summary>
        /// Gets or sets the selected conversation.
        /// </summary>
        public ConversationViewModel? SelectedConversation
        {
            get => this.selectedConversation;
            set
            {
                if (this.SetProperty(ref this.selectedConversation, value))
                {
                    _ = this.LoadMessagesForSelectedConversationAsync();
                    this.SendMessageCommand.NotifyCanExecuteChanged();  // ✅ ДОБАВЛЕНО
                }
            }
        }

        /// <summary>
        /// Gets or sets the message text being composed.
        /// </summary>
        public string MessageText
        {
            get => this.messageText;
            set
            {
                if (this.SetProperty(ref this.messageText, value))
                {
                    _ = this.SendTypingIndicatorAsync();
                    this.SendMessageCommand.NotifyCanExecuteChanged();  // ✅ ДОБАВЛЕНО
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether messages are being loaded.
        /// </summary>
        public bool IsLoadingMessages
        {
            get => this.isLoadingMessages;
            set
            {
                if (this.SetProperty(ref this.isLoadingMessages, value))
                {
                    this.SendMessageCommand.NotifyCanExecuteChanged();  // ✅ ДОБАВЛЕНО
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether conversations are being loaded.
        /// </summary>
        public bool IsLoadingConversations
        {
            get => this.isLoadingConversations;
            set => this.SetProperty(ref this.isLoadingConversations, value);
        }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string? ErrorMessage
        {
            get => this.errorMessage;
            set => this.SetProperty(ref this.errorMessage, value);
        }

        /// <summary>
        /// Gets or sets the message being edited.
        /// </summary>
        public MessageViewModel? MessageBeingEdited
        {
            get => this.messageBeingEdited;
            set => this.SetProperty(ref this.messageBeingEdited, value);
        }

        /// <inheritdoc/>
        public override void OnNavigatedTo()
        {
            _ = this.InitializeAsync();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom()
        {
            this.UnsubscribeFromMessagingEvents();
        }

        /// <summary>
        /// Command to send a message.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            // Allow sending if we have text OR attachments
            if (string.IsNullOrWhiteSpace(this.MessageText) && this.PendingAttachments.Count == 0)
            {
                return;
            }

            try
            {
                if (this.MessageBeingEdited != null)
                {
                    // Editing - attachments not supported for now
                    await this.messagingService.EditMessageAsync(
                        this.MessageBeingEdited.Id,
                        this.MessageText.Trim());

                    this.MessageBeingEdited.IsEditing = false;
                    this.MessageBeingEdited = null;
                }
                else
                {
                    // Prepare message content - if empty but has attachments, use a placeholder
                    var messageContent = this.MessageText?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(messageContent) && this.PendingAttachments.Count > 0)
                    {
                        messageContent = " "; // Single space as placeholder for attachments-only message
                    }

                    // First, send the message to get the real messageId (use HTTP for attachments)
                    var messageDto = await this.messagingService.SendMessageViaHttpAsync(
                        this.SelectedConversation.Id,
                        messageContent,
                        replyToId: null,
                        attachmentIds: new List<string>());

                    // Optimistic UI Update
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (this.SelectedConversation != null && messageDto.ChatId == this.SelectedConversation.Id)
                        {
                            this.receivedMessageIds.Add(messageDto.Id);
                            var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;
                            var messageViewModel = new MessageViewModel(messageDto, currentUserId, this.fileAttachmentService);

                            // Insert date separator if needed (returns adjusted index)
                            int insertIndex = this.Messages.Count;
                            insertIndex = this.InsertDateSeparatorIfNeeded(messageDto.CreatedAt, insertIndex);

                            this.Messages.Insert(insertIndex, messageViewModel);
                        }
                    });

                    // Now upload attachments with the real message ID
                    if (this.PendingAttachments.Count > 0)
                    {
                        foreach (var attachment in this.PendingAttachments.ToList())
                        {
                            try
                            {
                                attachment.IsUploading = true;

                                Stream? contentStream = null;
                                bool shouldCompress = attachment.IsImage && attachment.IsCompressed && attachment.CanCompress;

                                this.logger.Information(
                                    "Uploading attachment: {FileName}, IsImage: {IsImage}, IsCompressed: {IsCompressed}, CanCompress: {CanCompress}, ShouldCompress: {ShouldCompress}",
                                    attachment.FileName,
                                    attachment.IsImage,
                                    attachment.IsCompressed,
                                    attachment.CanCompress,
                                    shouldCompress);

                                if (shouldCompress)
                                {
                                    try
                                    {
                                        contentStream = await this.imageCompressionService.CompressImageAsync(attachment.FilePath);
                                        if (contentStream != null && contentStream.CanSeek)
                                        {
                                            this.logger.Information(
                                                "Image compressed successfully: {FileName}, Compressed stream length: {Length}",
                                                attachment.FileName,
                                                contentStream.Length);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Compression failed or didn't reduce size - use original file
                                        this.logger.Warning(
                                            ex,
                                            "Compression skipped for {FileName}, using original file. Reason: {Reason}",
                                            attachment.FileName,
                                            ex.Message);
                                        contentStream = null; // Will use original file
                                    }
                                }
                                else
                                {
                                    this.logger.Information("Skipping compression for {FileName}, using original file", attachment.FileName);
                                }

                                await this.fileAttachmentService.UploadAttachmentAsync(
                                    attachment.FilePath,
                                    messageDto.Id,
                                    contentStream);

                                if (contentStream != null)
                                {
                                    await ((IAsyncDisposable)contentStream).DisposeAsync();
                                }

                                this.logger.Information("Uploaded attachment: {FileName}", attachment.FileName);
                            }
                            catch (Exception ex)
                            {
                                this.logger.Error(ex, "Failed to upload attachment: {FileName}", attachment.FileName);
                                this.errorHandlingService.ShowError($"Failed to upload {attachment.FileName}");
                                attachment.IsUploading = false;
                            }
                        }

                        // Clear pending attachments after upload attempts
                        this.PendingAttachments.Clear();

                        // Reload messages to show attachments
                        await this.LoadMessagesForSelectedConversationAsync();
                    }
                }

                this.MessageText = string.Empty;
                this.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to send message");
                this.ErrorMessage = "Failed to send message. Please try again.";
            }
        }

        /// <summary>
        /// Command to edit a message.
        /// </summary>
        /// <param name="message">The message to edit.</param>
        [RelayCommand]
        private void EditMessage(MessageViewModel message)
        {
            if (message.IsCurrentUser && !message.IsDeleted)
            {
                this.MessageBeingEdited = message;
                this.MessageText = message.Content;
                message.IsEditing = true;
            }
        }

        /// <summary>
        /// Command to cancel editing.
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            if (this.MessageBeingEdited != null)
            {
                this.MessageBeingEdited.IsEditing = false;
                this.MessageBeingEdited = null;
            }

            this.MessageText = string.Empty;
        }

        /// <summary>
        /// Command to attach files to message.
        /// </summary>
        [RelayCommand]
        private async Task AttachFileAsync()
        {
            try
            {
                var files = await this.fileAttachmentService.PickFilesAsync(multiSelect: true);

                foreach (var filePath in files)
                {
                    // Validate file
                    var validation = this.fileAttachmentService.ValidateFile(filePath);
                    if (!validation.IsValid)
                    {
                        this.errorHandlingService.ShowWarning($"{System.IO.Path.GetFileName(filePath)}: {validation.ErrorMessage}");
                        continue;
                    }

                    // Add to pending attachments
                    var attachmentVm = new AttachmentViewModel(filePath);
                    this.PendingAttachments.Add(attachmentVm);

                    this.logger.Information("File attached: {FileName} ({Size})", attachmentVm.FileName, attachmentVm.FileSizeFormatted);
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to attach files");
                this.errorHandlingService.ShowError("Failed to attach files. Please try again.");
            }
        }

        /// <summary>
        /// Command to remove a pending attachment.
        /// </summary>
        /// <param name="attachment">The attachment to remove.</param>
        [RelayCommand]
        private void RemoveAttachment(AttachmentViewModel attachment)
        {
            this.PendingAttachments.Remove(attachment);
            this.logger.Information("Removed pending attachment: {FileName}", attachment.FileName);
        }

        /// <summary>
        /// Views an image attachment in the application.
        /// </summary>
        /// <param name="attachment">The attachment to view.</param>
        [RelayCommand]
        private void ViewImage(AttachmentViewModel attachment)
        {
            if (attachment.AttachmentDto == null || !attachment.IsImage)
            {
                return;
            }

            try
            {
                // Collect all image attachments from all messages in the current conversation
                var allImages = new List<Shared.Dtos.MessageAttachmentDto>();
                foreach (var message in this.GetMessageViewModels())
                {
                    foreach (var msgAttachment in message.Attachments)
                    {
                        if (msgAttachment.IsImage && msgAttachment.AttachmentDto != null)
                        {
                            allImages.Add(msgAttachment.AttachmentDto);
                        }
                    }
                }

                // Find current image index
                var currentIndex = allImages.FindIndex(img => img.Id == attachment.AttachmentDto.Id);
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }

                var imageViewModel = new ImageViewModel(allImages, currentIndex, this.fileAttachmentService);
                var dialog = new Views.ImageViewerDialog(imageViewModel)
                {
                    Owner = Application.Current.MainWindow,
                };

                dialog.ShowDialog();
                this.logger.Information("Viewed image: {FileName}", attachment.FileName);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to view image: {FileName}", attachment.FileName);
                this.errorHandlingService.ShowError($"Failed to view image: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens a non-image attachment with the default system application.
        /// </summary>
        /// <param name="attachment">The attachment to open.</param>
        [RelayCommand]
        private async Task OpenAttachment(AttachmentViewModel attachment)
        {
            if (attachment.AttachmentDto == null || attachment.IsImage)
            {
                return; // Images should use ViewImageCommand
            }

            try
            {
                await this.fileAttachmentService.OpenAttachmentAsync(attachment.AttachmentDto);
                this.logger.Information("Opened attachment: {FileName}", attachment.FileName);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to open attachment: {FileName}", attachment.FileName);
                this.errorHandlingService.ShowError($"Failed to open file: {ex.Message}");
            }
        }

        /// <summary>
        /// Command to play or pause audio attachment.
        /// </summary>
        /// <param name="attachment">The audio attachment to play/pause.</param>
        [RelayCommand]
        private void PlayPauseAudio(AttachmentViewModel attachment)
        {
            if (attachment == null || !attachment.IsAudio || attachment.AttachmentDto == null)
            {
                return;
            }

            // If this is the currently playing audio, toggle pause
            if (this.currentlyPlayingAudio == attachment)
            {
                // Toggle pause/play
                this.OnAudioPlayPauseRequested?.Invoke(attachment);
                return;
            }

            // Stop currently playing audio if any
            if (this.currentlyPlayingAudio != null)
            {
                this.currentlyPlayingAudio.IsPlaying = false;
                this.OnAudioStopRequested?.Invoke(this.currentlyPlayingAudio);
            }

            // Start playing new audio
            this.currentlyPlayingAudio = attachment;
            attachment.IsPlaying = true;
            this.OnAudioPlayRequested?.Invoke(attachment, attachment.AttachmentDto.DownloadUrl);
        }

        /// <summary>
        /// Event raised when audio play is requested.
        /// </summary>
        public event Action<AttachmentViewModel, string>? OnAudioPlayRequested;

        /// <summary>
        /// Event raised when audio play/pause toggle is requested.
        /// </summary>
        public event Action<AttachmentViewModel>? OnAudioPlayPauseRequested;

        /// <summary>
        /// Event raised when audio stop is requested.
        /// </summary>
        public event Action<AttachmentViewModel>? OnAudioStopRequested;

        /// <summary>
        /// Downloads an attachment to a temporary file for playback.
        /// </summary>
        /// <param name="attachment">The attachment DTO.</param>
        /// <returns>The local file path.</returns>
        public async Task<string> DownloadAttachmentForPlaybackAsync(MessageAttachmentDto attachment)
        {
            // Download to temp directory instead of Downloads
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"uchat_audio_{Guid.NewGuid()}{System.IO.Path.GetExtension(attachment.FileName)}");

            try
            {
                // Use DownloadImageStreamAsync pattern - it works for any file type
                var stream = await this.fileAttachmentService.DownloadImageStreamAsync(attachment);

                using (var fileStream = System.IO.File.Create(tempPath))
                using (stream)
                {
                    await stream.CopyToAsync(fileStream);
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to download audio for playback: {AttachmentId}", attachment.Id);
                throw;
            }
        }

        /// <summary>
        /// Downloads an attachment to local storage.
        /// </summary>
        /// <param name="attachment">The attachment to download.</param>
        [RelayCommand]
        private async Task DownloadAttachment(AttachmentViewModel attachment)
        {
            if (attachment.AttachmentDto == null)
            {
                this.errorHandlingService.ShowError("Attachment information is not available.");
                return;
            }

            try
            {
                var localPath = await this.fileAttachmentService.DownloadAttachmentAsync(attachment.AttachmentDto);
                this.logger.Information("Downloaded attachment: {FileName} to {Path}", attachment.FileName, localPath);
                this.errorHandlingService.ShowInfo($"File downloaded to: {localPath}");
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to download attachment: {FileName}", attachment.FileName);
                this.errorHandlingService.ShowError($"Failed to download file: {ex.Message}");
            }
        }

        /// <summary>
        /// Command to preview code file attachment.
        /// </summary>
        /// <param name="attachment">The code attachment to preview.</param>
        [RelayCommand]
        private async Task PreviewCode(AttachmentViewModel attachment)
        {
            if (attachment.AttachmentDto == null || !attachment.IsCodeFile)
            {
                return;
            }

            try
            {
                var stream = await this.fileAttachmentService.DownloadImageStreamAsync(attachment.AttachmentDto);

                string codeContent;
                using (var reader = new StreamReader(stream))
                {
                    codeContent = await reader.ReadToEndAsync();
                }

                var previewWindow = new Views.CodePreviewWindow
                {
                    Owner = Application.Current.MainWindow,
                };

                previewWindow.LoadCode(codeContent, attachment.FileName);
                previewWindow.ShowDialog();

                this.logger.Information("Previewed code file: {FileName}", attachment.FileName);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to preview code: {FileName}", attachment.FileName);
                this.errorHandlingService.ShowError($"Failed to preview code: {ex.Message}");
            }
        }

        /// <summary>
        /// Command to delete a message.
        /// </summary>
        /// <param name="message">The message to delete.</param>
        [RelayCommand]
        private async Task DeleteMessageAsync(MessageViewModel message)
        {
            if (!message.IsCurrentUser || message.IsDeleted)
            {
                return;
            }

            var result = MessageBox.Show(
                "Are you sure you want to delete this message?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await this.messagingService.DeleteMessageAsync(message.Id);

                    // Optimistically remove message from UI immediately
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var messageToRemove = this.FindMessageViewModel(message.Id);
                        if (messageToRemove != null)
                        {
                            this.Messages.Remove(messageToRemove);
                            this.receivedMessageIds.Remove(message.Id);

                            // Clean up orphaned date separators
                            this.CleanupOrphanedSeparators();

                            this.logger.Information("Message removed from UI immediately: {MessageId}", message.Id);
                        }
                    });

                    this.ErrorMessage = null;
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex, "Failed to delete message");
                    this.ErrorMessage = "Failed to delete message. Please try again.";
                }
            }
        }

        /// <summary>
        /// Command to refresh conversations.
        /// </summary>
        [RelayCommand]
        private async Task RefreshConversationsAsync()
        {
            await this.LoadConversationsAsync();
        }

        /// <summary>
        /// Command to create a new chat.
        /// </summary>
        [RelayCommand]
        private async Task CreateChatAsync()
        {
            try
            {
                var availableUsers = await this.userDirectoryService.GetAvailableUsersAsync();

                var dialog = new Views.CreateChatDialog
                {
                    Owner = Application.Current.MainWindow,
                };

                // Передаем текущего пользователя в ViewModel
                dialog.ViewModel.CurrentUser = this.authenticationService.CurrentUser;
                dialog.ViewModel.PopulateUsers(availableUsers);

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    // Используем GetFinalChatName() для получения финального названия чата
                    var chatName = dialog.ViewModel.GetFinalChatName();
                    var participantIds = dialog.ViewModel.GetSelectedParticipants();

                    var newChat = await this.messagingService.CreateChatAsync(chatName, participantIds);

                    var conversationVm = new ConversationViewModel(newChat);
                    this.Conversations.Insert(0, conversationVm);
                    this.SelectedConversation = conversationVm;

                    await this.LoadMessagesForSelectedConversationAsync();
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Error creating chat");
                this.errorHandlingService.HandleError(ex, "Failed to create chat");
            }
        }

        /// <summary>
        /// Command to toggle theme.
        /// </summary>
        [RelayCommand]
        private void ToggleTheme()
        {
            this.themeManager.ToggleTheme();
        }

        /// <summary>
        /// Command to navigate to the image generator view.
        /// </summary>
        [RelayCommand]
        private void NavigateToGenerator()
        {
            this.navigationService.NavigateTo<GeneratorViewModel>();
        }

        private bool CanSendMessage()
        {
            return this.SelectedConversation != null &&
                   !this.IsLoadingMessages &&
                   (!string.IsNullOrWhiteSpace(this.MessageText) || this.PendingAttachments.Count > 0);
        }

        /// <summary>
        /// Processes files dropped via drag and drop.
        /// </summary>
        /// <param name="files">The file paths that were dropped.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "Method must be internal to be accessible from view code-behind")]
        internal void ProcessDroppedFilesAsync(string[] files)
        {
            try
            {
                foreach (var filePath in files)
                {
                    // Validate file
                    var validation = this.fileAttachmentService.ValidateFile(filePath);
                    if (!validation.IsValid)
                    {
                        this.errorHandlingService.ShowWarning($"{System.IO.Path.GetFileName(filePath)}: {validation.ErrorMessage}");
                        continue;
                    }

                    // Add to pending attachments
                    var attachmentVm = new AttachmentViewModel(filePath);
                    this.PendingAttachments.Add(attachmentVm);

                    this.logger.Information("File attached via drag-drop: {FileName} ({Size})", attachmentVm.FileName, attachmentVm.FileSizeFormatted);
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to process dropped files");
                this.errorHandlingService.ShowError("Failed to process dropped files. Please try again.");
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                this.logger.Information("ChatViewModel.InitializeAsync called");

                if (!this.messagingService.IsConnected)
                {
                    var token = this.authenticationService.AccessToken;
                    if (!string.IsNullOrEmpty(token))
                    {
                        this.logger.Information("Attempting to connect WebSocket with token");
                        await this.messagingService.ConnectAsync(token);
                        this.logger.Information("WebSocket connection completed");
                    }
                    else
                    {
                        this.logger.Warning("No access token available for WebSocket connection");
                    }
                }

                await this.LoadConversationsAsync();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to initialize chat view");
                this.ErrorMessage = "Failed to connect to chat service. Please try again.";
            }
        }

        private async Task LoadConversationsAsync()
        {
            this.IsLoadingConversations = true;
            this.ErrorMessage = null;

            try
            {
                // BUG FIX #3: Save current selection before clearing
                var selectedChatId = this.SelectedConversation?.Id;
                this.logger.Debug("Saved selected chat ID before refresh: {ChatId}", selectedChatId);

                var chats = await this.messagingService.GetChatsAsync();

                // BUG FIX #2: Load last message preview for each chat
                var conversationTasks = chats.Select(async chat =>
                {
                    var conversation = new ConversationViewModel(chat);

                    // Load last message for preview
                    try
                    {
                        var messages = await this.messagingService.GetMessageHistoryAsync(chat.Id, limit: 1);
                        if (messages.Any())
                        {
                            var lastMessage = messages.Last();
                            conversation.LastMessagePreview = this.TruncateContent(lastMessage.Content, 50);
                            this.logger.Debug("Loaded preview for chat {ChatId}: {Preview}", chat.Id, conversation.LastMessagePreview);
                        }
                        else
                        {
                            conversation.LastMessagePreview = "No messages yet";
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warning(ex, "Failed to load preview for chat {ChatId}", chat.Id);
                        conversation.LastMessagePreview = "Unable to load preview";
                    }

                    return conversation;
                }).ToList();

                var conversationVms = await Task.WhenAll(conversationTasks);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.Conversations.Clear();
                    foreach (var conversation in conversationVms.OrderByDescending(c => c.LastMessageAt))
                    {
                        this.Conversations.Add(conversation);
                    }

                    // BUG FIX #3: Restore previous selection
                    if (!string.IsNullOrEmpty(selectedChatId))
                    {
                        var restoredChat = this.Conversations.FirstOrDefault(c => c.Id == selectedChatId);
                        if (restoredChat != null)
                        {
                            this.SelectedConversation = restoredChat;
                            this.logger.Information("Selection restored: {ChatId}", selectedChatId);
                        }
                        else
                        {
                            // Selected chat was deleted, select first available
                            this.SelectedConversation = this.Conversations.FirstOrDefault();
                            this.logger.Warning("Selected chat was deleted, selecting first chat");
                        }
                    }
                    else if (this.Conversations.Any() && this.SelectedConversation == null)
                    {
                        // No previous selection, select first
                        this.SelectedConversation = this.Conversations.First();
                        this.logger.Debug("No previous selection, selecting first chat");
                    }
                });
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load conversations");
                this.ErrorMessage = "Failed to load conversations. Please try again.";
            }
            finally
            {
                this.IsLoadingConversations = false;
            }
        }

        private async Task LoadMessagesForSelectedConversationAsync()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            this.IsLoadingMessages = true;
            this.ErrorMessage = null;

            try
            {
                var messages = await this.messagingService.GetMessageHistoryAsync(
                    this.SelectedConversation.Id,
                    limit: 50);

                var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.Messages.Clear();
                    this.receivedMessageIds.Clear();

                    foreach (var message in messages.OrderBy(m => m.CreatedAt))
                    {
                        // CRITICAL: Filter messages by ChatId and track received IDs
                        if (message.ChatId == this.SelectedConversation.Id)
                        {
                            this.receivedMessageIds.Add(message.Id);
                            this.Messages.Add(new MessageViewModel(message, currentUserId, this.fileAttachmentService));
                        }
                        else
                        {
                            this.logger.Warning(
                                "Message {MessageId} belongs to chat {MessageChatId}, not selected chat {SelectedChatId}",
                                message.Id,
                                message.ChatId,
                                this.SelectedConversation.Id);
                        }
                    }

                    // Insert date separators after all messages are loaded
                    this.InsertDateSeparators();

                    var messageCount = this.GetMessageViewModels().Count();
                    this.logger.Information(
                        "Loaded {Count} messages for chat {ChatId}",
                        messageCount,
                        this.SelectedConversation.Id);
                });

                if (messages.Any())
                {
                    this.SelectedConversation.LastMessagePreview = this.TruncateContent(messages.Last().Content, 50);
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load messages for conversation {ConversationId}", this.SelectedConversation.Id);
                this.ErrorMessage = "Failed to load messages. Please try again.";
            }
            finally
            {
                this.IsLoadingMessages = false;
            }
        }

        private async Task SendTypingIndicatorAsync()
        {
            if (this.SelectedConversation == null || string.IsNullOrWhiteSpace(this.MessageText))
            {
                return;
            }

            this.typingCancellationTokenSource?.Cancel();
            this.typingCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(1000, this.typingCancellationTokenSource.Token);
                await this.messagingService.SendTypingIndicatorAsync(this.SelectedConversation.Id);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to send typing indicator");
            }
        }

        private void SubscribeToMessagingEvents()
        {
            this.messagingService.MessageReceived += this.OnMessageReceived;
            this.messagingService.MessageEdited += this.OnMessageEdited;
            this.messagingService.MessageDeleted += this.OnMessageDeleted;
            this.messagingService.UserTyping += this.OnUserTyping;
            this.messagingService.ConnectionStateChanged += this.OnConnectionStateChanged;
        }

        private void UnsubscribeFromMessagingEvents()
        {
            this.messagingService.MessageReceived -= this.OnMessageReceived;
            this.messagingService.MessageEdited -= this.OnMessageEdited;
            this.messagingService.MessageDeleted -= this.OnMessageDeleted;
            this.messagingService.UserTyping -= this.OnUserTyping;
            this.messagingService.ConnectionStateChanged -= this.OnConnectionStateChanged;
        }

        private void OnMessageReceived(object? sender, MessageDto messageDto)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // CRITICAL: Deduplicate messages by ID
                if (this.receivedMessageIds.Contains(messageDto.Id))
                {
                    this.logger.Warning(
                        "Duplicate message received: {MessageId} in chat {ChatId}",
                        messageDto.Id,
                        messageDto.ChatId);
                    return;
                }

                // CRITICAL: Only add message to UI if it belongs to the selected conversation
                if (this.SelectedConversation != null && messageDto.ChatId == this.SelectedConversation.Id)
                {
                    this.receivedMessageIds.Add(messageDto.Id);
                    var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;
                    var messageViewModel = new MessageViewModel(messageDto, currentUserId, this.fileAttachmentService);

                    // Determine where to insert the message (keep messages sorted by date)
                    int insertIndex = this.Messages.Count;
                    for (int i = 0; i < this.Messages.Count; i++)
                    {
                        if (this.Messages[i] is MessageViewModel existingMessage)
                        {
                            if (existingMessage.CreatedAt > messageDto.CreatedAt)
                            {
                                insertIndex = i;
                                break;
                            }
                        }
                    }

                    // Insert date separator if needed (returns adjusted index)
                    insertIndex = this.InsertDateSeparatorIfNeeded(messageDto.CreatedAt, insertIndex);

                    this.Messages.Insert(insertIndex, messageViewModel);

                    this.logger.Information(
                        "Message {MessageId} added to chat {ChatId} by user {SenderId}",
                        messageDto.Id,
                        messageDto.ChatId,
                        messageDto.SenderId);
                }
                else if (messageDto.ChatId != this.SelectedConversation?.Id)
                {
                    // Message for different conversation - update unread count
                    var conversation = this.Conversations.FirstOrDefault(c => c.Id == messageDto.ChatId);
                    if (conversation != null)
                    {
                        conversation.UnreadCount++;
                        this.logger.Information(
                            "Message {MessageId} for inactive chat {ChatId}, unread count now {UnreadCount}",
                            messageDto.Id,
                            messageDto.ChatId,
                            conversation.UnreadCount);
                    }
                }

                // Update conversation preview for all conversations
                var targetConversation = this.Conversations.FirstOrDefault(c => c.Id == messageDto.ChatId);
                if (targetConversation != null)
                {
                    targetConversation.LastMessagePreview = this.TruncateContent(messageDto.Content, 50);
                    targetConversation.LastMessageAt = messageDto.CreatedAt;
                    this.logger.Debug("Chat preview updated for {ChatId}", messageDto.ChatId);
                }
            });
        }

        private void OnMessageEdited(object? sender, MessageDto messageDto)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var message = this.FindMessageViewModel(messageDto.Id);
                if (message != null)
                {
                    message.UpdateFromDto(messageDto);
                }
            });
        }

        private void OnMessageDeleted(object? sender, string messageId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // BUG FIX #1: Remove deleted message from UI completely
                var message = this.FindMessageViewModel(messageId);
                if (message != null)
                {
                    this.Messages.Remove(message);
                    this.receivedMessageIds.Remove(messageId);

                    // Clean up orphaned date separators
                    this.CleanupOrphanedSeparators();

                    this.logger.Information("Message removed from UI: {MessageId}", messageId);
                }
            });
        }

        /// <summary>
        /// Removes date separators that no longer have messages before or after them.
        /// </summary>
        private void CleanupOrphanedSeparators()
        {
            var separatorsToRemove = new List<DateSeparatorViewModel>();

            for (int i = 0; i < this.Messages.Count; i++)
            {
                if (this.Messages[i] is DateSeparatorViewModel separator)
                {
                    // Check if separator has messages on both sides
                    bool hasMessageBefore = false;
                    bool hasMessageAfter = false;

                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (this.Messages[j] is MessageViewModel)
                        {
                            hasMessageBefore = true;
                            break;
                        }

                        if (this.Messages[j] is DateSeparatorViewModel)
                        {
                            break; // Stop at previous separator
                        }
                    }

                    for (int j = i + 1; j < this.Messages.Count; j++)
                    {
                        if (this.Messages[j] is MessageViewModel)
                        {
                            hasMessageAfter = true;
                            break;
                        }

                        if (this.Messages[j] is DateSeparatorViewModel)
                        {
                            break; // Stop at next separator
                        }
                    }

                    // Remove separator if it doesn't separate messages
                    if (!hasMessageBefore || !hasMessageAfter)
                    {
                        separatorsToRemove.Add(separator);
                    }
                }
            }

            foreach (var separator in separatorsToRemove)
            {
                this.Messages.Remove(separator);
            }
        }

        private void OnUserTyping(object? sender, string userId)
        {
        }

        private string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            if (content.Length <= maxLength)
            {
                return content;
            }

            return content.Substring(0, maxLength - 3) + "...";
        }

        private void OnConnectionStateChanged(object? sender, bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                if (!isConnected)
                {
                    // Connection lost - clear all UI data
                    this.logger.Information("Connection lost, clearing all messages and conversations");
                    this.ErrorMessage = "Connection lost. Attempting to reconnect...";

                    // Clear messages and conversations from UI
                    this.Messages.Clear();
                    this.receivedMessageIds.Clear();
                    this.Conversations.Clear();
                    this.SelectedConversation = null;
                }
                else
                {
                    // Connection restored - reload all data automatically
                    this.logger.Information("Connection restored, reloading conversations and messages");
                    this.ErrorMessage = null;

                    try
                    {
                        // Reload conversations
                        await this.LoadConversationsAsync();

                        // Messages will be loaded automatically when SelectedConversation is set
                        // in LoadConversationsAsync (first available conversation will be selected)
                    }
                    catch (Exception ex)
                    {
                        this.logger.Error(ex, "Failed to reload data after reconnection");
                        this.ErrorMessage = "Reconnected, but failed to load data. Please refresh manually.";
                    }
                }
            });
        }

        /// <summary>
        /// Inserts date separators into the messages collection based on message dates.
        /// </summary>
        private void InsertDateSeparators()
        {
            // First, remove any existing date separators to avoid duplicates
            var existingSeparators = this.Messages.OfType<DateSeparatorViewModel>().ToList();
            foreach (var separator in existingSeparators)
            {
                this.Messages.Remove(separator);
            }

            DateTime? lastDate = null;
            var itemsToAdd = new List<(int Index, IMessageListItem Item)>();

            // Go through messages and identify where separators are needed
            for (int i = 0; i < this.Messages.Count; i++)
            {
                if (this.Messages[i] is MessageViewModel messageVm)
                {
                    var messageDate = DateFormatter.GetDateKey(messageVm.CreatedAt);

                    // Insert separator if this is the first message or if day changed
                    if (lastDate == null || DateFormatter.AreDifferentDays(lastDate.Value, messageDate))
                    {
                        itemsToAdd.Add((i, new DateSeparatorViewModel(messageDate)));
                        lastDate = messageDate;
                    }
                }
            }

            // Insert separators in reverse order to maintain correct indices
            foreach (var (index, item) in itemsToAdd.OrderByDescending(x => x.Index))
            {
                this.Messages.Insert(index, item);
            }
        }

        /// <summary>
        /// Inserts a date separator before a message if needed.
        /// </summary>
        /// <param name="messageDate">The date of the message to check.</param>
        /// <param name="insertIndex">The index where the message will be inserted (will be adjusted if separator is added).</param>
        /// <returns>The adjusted insert index after potential separator insertion.</returns>
        private int InsertDateSeparatorIfNeeded(DateTime messageDate, int insertIndex)
        {
            var messageDateKey = DateFormatter.GetDateKey(messageDate);

            // Find the previous message date (skip separators)
            DateTime? previousMessageDate = null;
            for (int i = insertIndex - 1; i >= 0; i--)
            {
                if (this.Messages[i] is MessageViewModel previousMessage)
                {
                    previousMessageDate = DateFormatter.GetDateKey(previousMessage.CreatedAt);
                    break;
                }
            }

            // Check if we need a separator (different day or no previous message)
            if (previousMessageDate == null || DateFormatter.AreDifferentDays(previousMessageDate.Value, messageDateKey))
            {
                // Check if there's already a separator for this date at the insert position
                bool separatorExists = false;
                if (insertIndex > 0 && this.Messages[insertIndex - 1] is DateSeparatorViewModel existingSeparator)
                {
                    var existingDate = DateFormatter.GetDateKey(existingSeparator.Date);
                    if (existingDate == messageDateKey)
                    {
                        separatorExists = true;
                    }
                }

                if (!separatorExists)
                {
                    this.Messages.Insert(insertIndex, new DateSeparatorViewModel(messageDateKey));
                    return insertIndex + 1; // Return adjusted index
                }
            }

            return insertIndex;
        }

        /// <summary>
        /// Gets all message view models from the messages collection.
        /// </summary>
        /// <returns>Collection of message view models.</returns>
        private IEnumerable<MessageViewModel> GetMessageViewModels()
        {
            return this.Messages.OfType<MessageViewModel>();
        }

        /// <summary>
        /// Finds a message view model by ID.
        /// </summary>
        /// <param name="messageId">The message ID to find.</param>
        /// <returns>The message view model if found, null otherwise.</returns>
        private MessageViewModel? FindMessageViewModel(string messageId)
        {
            return this.GetMessageViewModels().FirstOrDefault(m => m.Id == messageId);
        }
    }
}
