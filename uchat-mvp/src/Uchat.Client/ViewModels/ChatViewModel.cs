namespace Uchat.Client.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Data;
    using CommunityToolkit.Mvvm.Input;
    using Serilog;
    using Uchat.Client.Helpers;
    using Uchat.Client.Services;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Enums;

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
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly HashSet<string> receivedMessageIds;
        private ConversationViewModel? selectedConversation;
        private string messageText;
        private bool isLoadingMessages;
        private bool isLoadingConversations;
        private string? errorMessage;
        private MessageViewModel? messageBeingEdited;
        private CancellationTokenSource? typingCancellationTokenSource;
        private CancellationTokenSource? searchCancellationTokenSource;
        private ObservableCollection<AttachmentViewModel> pendingAttachments;

        // --- SEARCH FIELDS ---
        private string chatSearchText;
        private string messageSearchText;
        private bool isSearchingMessages;

        // --- USER PROFILE MODAL FIELDS ---
        private bool isUserProfileVisible;
        private string profileEmail;
        private string profileUsername;
        private int filesCount;
        private int imagesCount;
        private int videosCount;

        // --- FILES AND IMAGES LIST FIELDS ---
        private ObservableCollection<AttachmentViewModel> filesList;
        private ObservableCollection<AttachmentViewModel> imagesList;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatViewModel"/> class.
        /// </summary>
        public ChatViewModel(
            IMessagingService messagingService,
            IAuthenticationService authenticationService,
            IUserDirectoryService userDirectoryService,
            IErrorHandlingService errorHandlingService,
            IThemeManager themeManager,
            IFileAttachmentService fileAttachmentService,
            IImageCompressionService imageCompressionService,
            INavigationService navigationService,
            IServiceProvider serviceProvider,
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
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.receivedMessageIds = new HashSet<string>();
            this.Title = "Uchat - Messages";
            this.messageText = string.Empty;

            // Initialize search fields to avoid CS8618
            this.chatSearchText = string.Empty;
            this.messageSearchText = string.Empty;
            
            // Initialize profile fields
            this.profileEmail = string.Empty;
            this.profileUsername = string.Empty;

            // Initialize files and images lists
            this.filesList = new ObservableCollection<AttachmentViewModel>();
            this.imagesList = new ObservableCollection<AttachmentViewModel>();

            this.pendingAttachments = new ObservableCollection<AttachmentViewModel>();
            this.Conversations = new ObservableCollection<ConversationViewModel>();

            // --- SEARCH INITIALIZATION ---
            this.ConversationsView = CollectionViewSource.GetDefaultView(this.Conversations);
            this.ConversationsView.Filter = this.FilterConversations;

            this.Messages = new ObservableCollection<IMessageListItem>();

            this.SubscribeToMessagingEvents();
        }

        /// <summary>
        /// Occurs when audio playback is requested.
        /// </summary>
        public event EventHandler<object>? OnAudioPlayRequested;

        /// <summary>
        /// Occurs when audio pause is requested.
        /// </summary>
        public event EventHandler<object>? OnAudioPlayPauseRequested;

        /// <summary>
        /// Occurs when audio stop is requested.
        /// </summary>
        public event EventHandler<object>? OnAudioStopRequested;

        /// <summary>
        /// Gets the conversations collection (Source).
        /// </summary>
        public ObservableCollection<ConversationViewModel> Conversations { get; }

        /// <summary>
        /// Gets the filtered view of conversations.
        /// </summary>
        public ICollectionView ConversationsView { get; }

        /// <summary>
        /// Gets the messages collection for the selected conversation.
        /// </summary>
        public ObservableCollection<IMessageListItem> Messages { get; }

        /// <summary>
        /// Gets the pending attachments collection.
        /// </summary>
        public ObservableCollection<AttachmentViewModel> PendingAttachments => this.pendingAttachments;

        /// <summary>
        /// Gets or sets the chat search text.
        /// </summary>
        public string ChatSearchText
        {
            get => this.chatSearchText;
            set
            {
                if (this.SetProperty(ref this.chatSearchText, value))
                {
                    this.ConversationsView.Refresh();
                }
            }
        }

        /// <summary>
        /// Gets or sets the message search text.
        /// </summary>
        public string MessageSearchText
        {
            get => this.messageSearchText;
            set
            {
                if (this.SetProperty(ref this.messageSearchText, value))
                {
                    this.ClearMessageSearchCommand.NotifyCanExecuteChanged();
                    this.SearchMessagesInChatCommand.NotifyCanExecuteChanged();
                    _ = this.TriggerSearchAsYouTypeAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether messages are being searched.
        /// </summary>
        public bool IsSearchingMessages
        {
            get => this.isSearchingMessages;
            set => this.SetProperty(ref this.isSearchingMessages, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the user profile modal is visible.
        /// </summary>
        public bool IsUserProfileVisible
        {
            get => this.isUserProfileVisible;
            set => this.SetProperty(ref this.isUserProfileVisible, value);
        }

        /// <summary>
        /// Gets or sets the profile email of the selected conversation user.
        /// </summary>
        public string ProfileEmail
        {
            get => this.profileEmail;
            set => this.SetProperty(ref this.profileEmail, value);
        }

        /// <summary>
        /// Gets or sets the profile username of the selected conversation user.
        /// </summary>
        public string ProfileUsername
        {
            get => this.profileUsername;
            set => this.SetProperty(ref this.profileUsername, value);
        }

        /// <summary>
        /// Gets or sets the number of files in the current chat.
        /// </summary>
        public int FilesCount
        {
            get => this.filesCount;
            set => this.SetProperty(ref this.filesCount, value);
        }

        /// <summary>
        /// Gets or sets the number of images in the current chat.
        /// </summary>
        public int ImagesCount
        {
            get => this.imagesCount;
            set => this.SetProperty(ref this.imagesCount, value);
        }

        /// <summary>
        /// Gets or sets the number of videos in the current chat.
        /// </summary>
        public int VideosCount
        {
            get => this.videosCount;
            set => this.SetProperty(ref this.videosCount, value);
        }

        /// <summary>
        /// Gets the files list collection.
        /// </summary>
        public ObservableCollection<AttachmentViewModel> FilesList => this.filesList;

        /// <summary>
        /// Gets the images list collection.
        /// </summary>
        public ObservableCollection<AttachmentViewModel> ImagesList => this.imagesList;

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
                    // Clear search when changing chats
                    this.MessageSearchText = string.Empty;
                    _ = this.LoadMessagesForSelectedConversationAsync();
                    this.SendMessageCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the message text being typed.
        /// </summary>
        public string MessageText
        {
            get => this.messageText;
            set
            {
                if (this.SetProperty(ref this.messageText, value))
                {
                    _ = this.SendTypingIndicatorAsync();
                    this.SendMessageCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether messages are currently loading.
        /// </summary>
        public bool IsLoadingMessages
        {
            get => this.isLoadingMessages;
            set
            {
                if (this.SetProperty(ref this.isLoadingMessages, value))
                {
                    this.SendMessageCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether conversations are currently loading.
        /// </summary>
        public bool IsLoadingConversations
        {
            get => this.isLoadingConversations;
            set => this.SetProperty(ref this.isLoadingConversations, value);
        }

        /// <summary>
        /// Gets or sets the current error message.
        /// </summary>
        public string? ErrorMessage
        {
            get => this.errorMessage;
            set => this.SetProperty(ref this.errorMessage, value);
        }

        /// <summary>
        /// Gets or sets the message currently being edited.
        /// </summary>
        public MessageViewModel? MessageBeingEdited
        {
            get => this.messageBeingEdited;
            set => this.SetProperty(ref this.messageBeingEdited, value);
        }

        /// <summary>
        /// Called when navigated to.
        /// </summary>
        public override void OnNavigatedTo()
        {
            _ = this.InitializeAsync();
        }

        /// <summary>
        /// Called when navigated from.
        /// </summary>
        public override void OnNavigatedFrom()
        {
            this.UnsubscribeFromMessagingEvents();
        }

        /// <summary>
        /// Downloads an attachment for playback.
        /// </summary>
        /// <param name="attachment">The attachment view model.</param>
        /// <returns>The local file path or null if failed.</returns>
        public async Task<string?> DownloadAttachmentForPlaybackAsync(AttachmentViewModel attachment)
        {
            if (attachment?.AttachmentDto == null)
            {
                return null;
            }

            try
            {
                return await this.fileAttachmentService.DownloadAttachmentAsync(attachment.AttachmentDto);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to download audio attachment for playback");
                return null;
            }
        }

        /// <summary>
        /// Processes files dropped onto the view.
        /// </summary>
        /// <param name="files">The array of file paths.</param>
        internal void ProcessDroppedFilesAsync(string[] files)
        {
            foreach (var filePath in files)
            {
                var validation = this.fileAttachmentService.ValidateFile(filePath);
                if (validation.IsValid)
                {
                    this.PendingAttachments.Add(new AttachmentViewModel(filePath));
                }
            }
        }

        /// <summary>
        /// Triggers the play audio event.
        /// </summary>
        /// <param name="parameter">The audio parameter.</param>
        [RelayCommand]
        private void RequestPlayAudio(object parameter)
        {
            this.OnAudioPlayRequested?.Invoke(this, parameter);
        }

        /// <summary>
        /// Triggers the pause audio event.
        /// </summary>
        /// <param name="parameter">The audio parameter.</param>
        [RelayCommand]
        private void RequestPauseAudio(object parameter)
        {
            this.OnAudioPlayPauseRequested?.Invoke(this, parameter);
        }

        /// <summary>
        /// Triggers the stop audio event.
        /// </summary>
        /// <param name="parameter">The audio parameter.</param>
        [RelayCommand]
        private void RequestStopAudio(object parameter)
        {
            this.OnAudioStopRequested?.Invoke(this, parameter);
        }

        // --- SEARCH LOGIC ---
        private bool FilterConversations(object item)
        {
            if (string.IsNullOrWhiteSpace(this.ChatSearchText))
            {
                return true;
            }

            if (item is ConversationViewModel chat)
            {
                return chat.Name != null && chat.Name.Contains(this.ChatSearchText, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        [RelayCommand]
        private async Task SearchMessagesInChatAsync()
        {
            if (this.SelectedConversation == null || string.IsNullOrWhiteSpace(this.MessageSearchText))
            {
                return;
            }

            this.IsSearchingMessages = true;
            this.ErrorMessage = null;

            try
            {
                var results = await this.messagingService.SearchMessagesAsync(
                    this.SelectedConversation.Id,
                    this.MessageSearchText);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.Messages.Clear();
                    this.receivedMessageIds.Clear();

                    var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;

                    foreach (var msgDto in results)
                    {
                        var vm = new MessageViewModel(msgDto, currentUserId, this.fileAttachmentService);
                        this.Messages.Add(vm);
                    }
                });
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to search messages");
                this.ErrorMessage = "Search failed. Please try again.";
            }
            finally
            {
                this.IsSearchingMessages = false;
            }
        }

        [RelayCommand]
        private async Task ClearMessageSearchAsync()
        {
            this.MessageSearchText = string.Empty;
            await this.LoadMessagesForSelectedConversationAsync();
        }

        private async Task TriggerSearchAsYouTypeAsync()
        {
            this.searchCancellationTokenSource?.Cancel();
            this.searchCancellationTokenSource = new CancellationTokenSource();
            var token = this.searchCancellationTokenSource.Token;

            try
            {
                await Task.Delay(300, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(this.MessageSearchText))
                {
                    await this.LoadMessagesForSelectedConversationAsync();
                }
                else
                {
                    await this.SearchMessagesInChatAsync();
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        // --- END SEARCH LOGIC ---

        // --- USER PROFILE MODAL COMMANDS ---
        [RelayCommand]
        private void OpenUserProfile()
        {
            if (this.SelectedConversation != null)
            {
                if (!this.IsUserProfileVisible)
                {
                    this.UpdateProfileData();
                    this.CalculateProfileStatistics();
                    this.PopulateProfileImages();
                    this.PopulateProfileFiles();
                    this.IsUserProfileVisible = true;
                }
                else
                {
                    this.IsUserProfileVisible = false;
                }
            }
        }

        [RelayCommand]
        private void CloseUserProfile()
        {
            this.IsUserProfileVisible = false;
        }

        [RelayCommand]
        private void ShareContact()
        {
            if (this.SelectedConversation == null || string.IsNullOrEmpty(this.ProfileUsername))
            {
                return;
            }

            try
            {
                var contactInfo = $"@{this.ProfileUsername}";
                Clipboard.SetText(contactInfo);
                this.errorHandlingService.ShowInfo($"Contact copied to clipboard: {contactInfo}");
                this.logger.Information("Contact shared: {Username}", this.ProfileUsername);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to share contact");
                this.errorHandlingService.ShowError("Failed to copy contact to clipboard.");
            }
        }

        [RelayCommand]
        private async Task BlockUserAsync()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            try
            {
                var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;
                var chat = await this.messagingService.GetChatByIdAsync(this.SelectedConversation.Id);
                var otherUser = chat.Participants.FirstOrDefault(p => p.Id != currentUserId);
                
                if (otherUser == null)
                {
                    this.errorHandlingService.ShowError("Cannot block this user.");
                    return;
                }

                var confirmMessage = "Are you sure you want to block this user? You will no longer receive messages from them.";
                if (MessageBox.Show(confirmMessage, "Block User", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                await this.messagingService.BlockUserAsync(otherUser.Id);
                this.errorHandlingService.ShowInfo($"User @{otherUser.Username} has been blocked.");
                this.logger.Information("User blocked: {UserId}", otherUser.Id);
                this.CloseUserProfile();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to block user");
                this.errorHandlingService.ShowError("Failed to block user. Please try again.");
            }
        }

        [RelayCommand]
        private async Task DeleteChatAsync()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            try
            {
                var confirmMessage = "Are you sure you want to delete this chat? All messages will be permanently removed.";
                if (MessageBox.Show(confirmMessage, "Delete Chat", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                var chatId = this.SelectedConversation.Id;
                await this.messagingService.DeleteChatAsync(chatId);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var chatToRemove = this.Conversations.FirstOrDefault(c => c.Id == chatId);
                    if (chatToRemove != null)
                    {
                        this.Conversations.Remove(chatToRemove);
                    }

                    this.SelectedConversation = this.Conversations.FirstOrDefault();
                    this.Messages.Clear();
                    this.receivedMessageIds.Clear();
                });

                this.errorHandlingService.ShowInfo("Chat deleted successfully.");
                this.logger.Information("Chat deleted: {ChatId}", chatId);
                this.CloseUserProfile();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to delete chat");
                this.errorHandlingService.ShowError("Failed to delete chat. Please try again.");
            }
        }

        // --- END USER PROFILE MODAL ---

        // --- FILES AND IMAGES LIST COMMANDS ---
        [RelayCommand]
        private void OpenFilesList()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            this.PopulateFilesList();
            
            var filesListViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<FilesListViewModel>(
                this.serviceProvider);
            filesListViewModel.SetFiles(this.filesList, this.SelectedConversation.Name ?? "Chat");
            
            this.navigationService.NavigateTo<FilesListViewModel>();
        }

        [RelayCommand]
        private void OpenImagesGrid()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            this.PopulateImagesList();
            
            var imagesGridViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ImagesGridViewModel>(
                this.serviceProvider);
            imagesGridViewModel.SetImages(this.imagesList, this.SelectedConversation.Name ?? "Chat");
            
            this.navigationService.NavigateTo<ImagesGridViewModel>();
        }

        // --- END FILES AND IMAGES LIST ---

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(this.MessageText) && this.PendingAttachments.Count == 0)
            {
                return;
            }

            try
            {
                if (this.MessageBeingEdited != null)
                {
                    await this.messagingService.EditMessageAsync(this.MessageBeingEdited.Id, this.MessageText.Trim());
                    this.MessageBeingEdited.IsEditing = false;
                    this.MessageBeingEdited = null;
                }
                else
                {
                    var messageContent = this.MessageText?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(messageContent) && this.PendingAttachments.Count > 0)
                    {
                        messageContent = " ";
                    }

                    var messageDto = await this.messagingService.SendMessageViaHttpAsync(
                        this.SelectedConversation.Id,
                        messageContent,
                        replyToId: null,
                        attachmentIds: new List<string>());

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (this.SelectedConversation != null && messageDto.ChatId == this.SelectedConversation.Id)
                        {
                            this.receivedMessageIds.Add(messageDto.Id);
                            var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;
                            var messageViewModel = new MessageViewModel(messageDto, currentUserId, this.fileAttachmentService);
                            int insertIndex = this.Messages.Count;
                            insertIndex = this.InsertDateSeparatorIfNeeded(messageDto.CreatedAt, insertIndex);
                            this.Messages.Insert(insertIndex, messageViewModel);
                        }
                    });

                    if (this.PendingAttachments.Count > 0)
                    {
                        foreach (var attachment in this.PendingAttachments.ToList())
                        {
                            try
                            {
                                attachment.IsUploading = true;
                                Stream? contentStream = null;
                                bool shouldCompress = attachment.IsImage && attachment.IsCompressed && attachment.CanCompress;

                                if (shouldCompress)
                                {
                                    try
                                    {
                                        contentStream = await this.imageCompressionService.CompressImageAsync(attachment.FilePath);
                                    }
                                    catch (Exception)
                                    {
                                        contentStream = null;
                                    }
                                }

                                await this.fileAttachmentService.UploadAttachmentAsync(attachment.FilePath, messageDto.Id, contentStream);
                                if (contentStream != null)
                                {
                                    await ((IAsyncDisposable)contentStream).DisposeAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                this.logger.Error(ex, "Failed upload attachment");
                                this.errorHandlingService.ShowError($"Failed to upload {attachment.FileName}");
                            }
                        }

                        this.PendingAttachments.Clear();
                        await this.LoadMessagesForSelectedConversationAsync();
                    }
                }

                this.MessageText = string.Empty;
                this.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to send message");
                this.ErrorMessage = "Failed to send message.";
            }
        }

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

        [RelayCommand]
        private async Task AttachFileAsync()
        {
            try
            {
                var files = await this.fileAttachmentService.PickFilesAsync(multiSelect: true);
                foreach (var filePath in files)
                {
                    var validation = this.fileAttachmentService.ValidateFile(filePath);
                    if (!validation.IsValid)
                    {
                        continue;
                    }

                    this.PendingAttachments.Add(new AttachmentViewModel(filePath));
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to attach files");
            }
        }

        [RelayCommand]
        private void RemoveAttachment(AttachmentViewModel attachment) => this.PendingAttachments.Remove(attachment);

        [RelayCommand]
        private void ViewImage(AttachmentViewModel attachment)
        {
            if (attachment?.AttachmentDto == null || !attachment.IsImage)
            {
                return;
            }

            try
            {
                var allImages = this.GetMessageViewModels()
                    .SelectMany(m => m.Attachments)
                    .Where(a => a.IsImage && a.AttachmentDto != null)
                    .Select(a => a.AttachmentDto!)
                    .ToList();

                if (allImages.Count == 0)
                {
                    this.errorHandlingService.ShowError("No images found");
                    return;
                }

                var currentIndex = allImages.FindIndex(img => img.Id == attachment.AttachmentDto.Id);
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }

                // Use ImagePreviewViewModel and navigate to ImagePreviewView
                var imagePreviewViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ImagePreviewViewModel>(
                    this.serviceProvider);
                if (imagePreviewViewModel != null)
                {
                    imagePreviewViewModel.SetImages(allImages, currentIndex);
                    this.navigationService.NavigateTo<ImagePreviewViewModel>();
                }
                else
                {
                    this.errorHandlingService.ShowError("Failed to initialize image preview");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to view image: {Error}", ex.Message);
                this.errorHandlingService.ShowError($"Failed to open image preview: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task OpenAttachment(AttachmentViewModel attachment)
        {
            if (attachment.AttachmentDto == null)
            {
                return;
            }

            try
            {
                await this.fileAttachmentService.OpenAttachmentAsync(attachment.AttachmentDto);
            }
            catch (Exception ex)
            {
                this.errorHandlingService.ShowError(ex.Message);
            }
        }

        [RelayCommand]
        private async Task DownloadAttachment(AttachmentViewModel attachment)
        {
            if (attachment.AttachmentDto == null)
            {
                return;
            }

            try
            {
                var path = await this.fileAttachmentService.DownloadAttachmentAsync(attachment.AttachmentDto);
                this.errorHandlingService.ShowInfo($"Downloaded to: {path}");
            }
            catch (Exception ex)
            {
                this.errorHandlingService.ShowError(ex.Message);
            }
        }

        [RelayCommand]
        private async Task PreviewCodeAsync(AttachmentViewModel attachment)
        {
            if (attachment.AttachmentDto == null || !attachment.IsCodeFile)
            {
                return;
            }

            try
            {
                var filePath = await this.fileAttachmentService.DownloadAttachmentAsync(attachment.AttachmentDto);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    this.errorHandlingService.ShowError("Failed to download code file for preview.");
                    return;
                }

                var codeContent = await File.ReadAllTextAsync(filePath);
                var previewWindow = new Views.CodePreviewWindow
                {
                    Owner = Application.Current.MainWindow,
                };
                previewWindow.LoadCode(codeContent, attachment.FileName);
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to preview code file");
                this.errorHandlingService.ShowError($"Failed to preview code: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteMessageAsync(MessageViewModel message)
        {
            if (!message.IsCurrentUser || message.IsDeleted)
            {
                return;
            }

            if (MessageBox.Show("Delete message?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    await this.messagingService.DeleteMessageAsync(message.Id);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var msg = this.FindMessageViewModel(message.Id);
                        if (msg != null)
                        {
                            this.Messages.Remove(msg);
                            this.receivedMessageIds.Remove(message.Id);
                            this.CleanupOrphanedSeparators();
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex, "Delete failed");
                }
            }
        }

        [RelayCommand]
        private async Task RefreshConversationsAsync() => await this.LoadConversationsAsync();

        [RelayCommand]
        private async Task CreateChatAsync()
        {
            try
            {
                var availableUsers = await this.userDirectoryService.GetAvailableUsersAsync();
                var dialog = new Views.CreateChatDialog { Owner = Application.Current.MainWindow };
                dialog.ViewModel.CurrentUser = this.authenticationService.CurrentUser;
                dialog.ViewModel.PopulateUsers(availableUsers);

                if (dialog.ShowDialog() == true)
                {
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
                this.logger.Error(ex, "Create chat failed");
            }
        }

        [RelayCommand]
        private void ToggleTheme() => this.themeManager.ToggleTheme();

        private bool CanSendMessage() => this.SelectedConversation != null && !this.IsLoadingMessages &&
                                       (!string.IsNullOrWhiteSpace(this.MessageText) || this.PendingAttachments.Count > 0);

        private async Task InitializeAsync()
        {
            try
            {
                if (!this.messagingService.IsConnected)
                {
                    var token = this.authenticationService.AccessToken;
                    if (!string.IsNullOrEmpty(token))
                    {
                        await this.messagingService.ConnectAsync(token);
                    }
                }

                await this.LoadConversationsAsync();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Init failed");
            }
        }

        private async Task LoadConversationsAsync()
        {
            this.IsLoadingConversations = true;
            this.ErrorMessage = null;
            try
            {
                var selectedChatId = this.SelectedConversation?.Id;
                var chats = await this.messagingService.GetChatsAsync();

                var conversationTasks = chats.Select(async chat =>
                {
                    var conversation = new ConversationViewModel(chat);
                    try
                    {
                        var messages = await this.messagingService.GetMessageHistoryAsync(chat.Id, limit: 1);
                        conversation.LastMessagePreview = messages.Any() ? this.GetMessagePreview(messages.Last()) : "No messages";
                    }
                    catch
                    {
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

                    if (!string.IsNullOrEmpty(selectedChatId))
                    {
                        this.SelectedConversation = this.Conversations.FirstOrDefault(c => c.Id == selectedChatId);
                    }

                    if (this.SelectedConversation == null && this.Conversations.Any())
                    {
                        this.SelectedConversation = this.Conversations.First();
                    }
                });
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Load conversations failed");
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
                var messages = await this.messagingService.GetMessageHistoryAsync(this.SelectedConversation.Id, limit: 50);
                var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.Messages.Clear();
                    this.receivedMessageIds.Clear();

                    foreach (var message in messages.OrderBy(m => m.CreatedAt))
                    {
                        if (message.ChatId == this.SelectedConversation.Id)
                        {
                            this.receivedMessageIds.Add(message.Id);
                            this.Messages.Add(new MessageViewModel(message, currentUserId, this.fileAttachmentService));
                        }
                    }

                    this.InsertDateSeparators();
                });

                if (messages.Any())
                {
                    var lastMessage = messages.Last();
                    this.SelectedConversation.LastMessagePreview = this.GetMessagePreview(lastMessage);
                }

                this.CalculateProfileStatistics();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Load messages failed");
            }
            finally
            {
                this.IsLoadingMessages = false;
            }
        }

        private void CalculateProfileStatistics()
        {
            var messageViewModels = this.GetMessageViewModels();
            var allAttachments = messageViewModels
                .SelectMany(m => m.Attachments)
                .ToList();

            this.FilesCount = allAttachments.Count(a => a.AttachmentType == AttachmentType.Document || 
                                                        a.AttachmentType == AttachmentType.Archive || 
                                                        a.AttachmentType == AttachmentType.Code ||
                                                        a.AttachmentType == AttachmentType.Audio);
            this.ImagesCount = allAttachments.Count(a => a.AttachmentType == AttachmentType.Image);
            this.VideosCount = allAttachments.Count(a => a.AttachmentType == AttachmentType.Video);
        }

        private void PopulateFilesList()
        {
            var messageViewModels = this.GetMessageViewModels();
            var files = messageViewModels
                .SelectMany(m => m.Attachments)
                .Where(a => a.AttachmentType == AttachmentType.Document || 
                           a.AttachmentType == AttachmentType.Archive || 
                           a.AttachmentType == AttachmentType.Code ||
                           a.AttachmentType == AttachmentType.Audio)
                .OrderByDescending(a => a.AttachmentDto?.UploadedAt)
                .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                this.filesList.Clear();
                foreach (var file in files)
                {
                    this.filesList.Add(file);
                }
            });
        }

        private void PopulateImagesList()
        {
            var messageViewModels = this.GetMessageViewModels();
            var images = messageViewModels
                .SelectMany(m => m.Attachments)
                .Where(a => a.AttachmentType == AttachmentType.Image)
                .OrderByDescending(a => a.AttachmentDto?.UploadedAt)
                .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                this.imagesList.Clear();
                foreach (var image in images)
                {
                    this.imagesList.Add(image);
                }
            });
        }

        private async void PopulateProfileImages()
        {
            // Populate images list for profile display
            this.PopulateImagesList();
            
            // Initialize thumbnails for profile images (first 6 for preview)
            foreach (var image in this.imagesList.Take(6))
            {
                if (image.IsImage)
                {
                    try
                    {
                        await image.InitializeAsync();
                    }
                    catch
                    {
                        // Ignore errors during initialization
                    }
                }
            }
        }

        private async void PopulateProfileFiles()
        {
            // Populate files list for profile display
            this.PopulateFilesList();
            
            // Initialize thumbnails for image files in profile (first 6 for preview)
            foreach (var file in this.filesList.Take(6))
            {
                if (file.IsImage)
                {
                    try
                    {
                        await file.InitializeAsync();
                    }
                    catch
                    {
                        // Ignore errors during initialization
                    }
                }
            }
        }

        private async void UpdateProfileData()
        {
            if (this.SelectedConversation == null)
            {
                return;
            }

            try
            {
                var chat = await this.messagingService.GetChatByIdAsync(this.SelectedConversation.Id);
                var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;
                
                var otherUser = chat.Participants.FirstOrDefault(p => p.Id != currentUserId);
                if (otherUser != null)
                {
                    this.ProfileEmail = otherUser.Email;
                    this.ProfileUsername = otherUser.Username;
                }
                else
                {
                    this.ProfileEmail = string.Empty;
                    this.ProfileUsername = string.Empty;
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load profile data");
                this.ProfileEmail = string.Empty;
                this.ProfileUsername = string.Empty;
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
            catch
            {
                // Ignored
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
                if (this.receivedMessageIds.Contains(messageDto.Id))
                {
                    return;
                }

                if (this.SelectedConversation != null && messageDto.ChatId == this.SelectedConversation.Id)
                {
                    this.receivedMessageIds.Add(messageDto.Id);
                    var currentUserId = this.authenticationService.CurrentUser?.Id ?? string.Empty;
                    var messageViewModel = new MessageViewModel(messageDto, currentUserId, this.fileAttachmentService);

                    int insertIndex = this.Messages.Count;
                    for (int i = 0; i < this.Messages.Count; i++)
                    {
                        if (this.Messages[i] is MessageViewModel existingMessage && existingMessage.CreatedAt > messageDto.CreatedAt)
                        {
                            insertIndex = i;
                            break;
                        }
                    }

                    insertIndex = this.InsertDateSeparatorIfNeeded(messageDto.CreatedAt, insertIndex);
                    this.Messages.Insert(insertIndex, messageViewModel);
                }
                else
                {
                    var conversation = this.Conversations.FirstOrDefault(c => c.Id == messageDto.ChatId);
                    if (conversation != null)
                    {
                        conversation.UnreadCount++;
                    }
                }

                var targetConversation = this.Conversations.FirstOrDefault(c => c.Id == messageDto.ChatId);
                if (targetConversation != null)
                {
                    targetConversation.LastMessagePreview = this.GetMessagePreview(messageDto);
                    targetConversation.LastMessageAt = messageDto.CreatedAt;
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
                var message = this.FindMessageViewModel(messageId);
                if (message != null)
                {
                    this.Messages.Remove(message);
                    this.receivedMessageIds.Remove(messageId);
                    this.CleanupOrphanedSeparators();
                }
            });
        }

        private void CleanupOrphanedSeparators()
        {
            var separatorsToRemove = new List<DateSeparatorViewModel>();
            for (int i = 0; i < this.Messages.Count; i++)
            {
                if (this.Messages[i] is DateSeparatorViewModel separator)
                {
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
                            break;
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
                            break;
                        }
                    }

                    if (!hasMessageBefore || !hasMessageAfter)
                    {
                        separatorsToRemove.Add(separator);
                    }
                }
            }

            foreach (var s in separatorsToRemove)
            {
                this.Messages.Remove(s);
            }
        }

        private void OnUserTyping(object? sender, string userId)
        {
        }

        private string GetMessagePreview(MessageDto message)
        {
            // If message has attachments, show attachment type
            if (message.Attachments != null && message.Attachments.Any())
            {
                var firstAttachment = message.Attachments.First();
                return firstAttachment.AttachmentType switch
                {
                    AttachmentType.Image => "image",
                    AttachmentType.Video => "video",
                    AttachmentType.Audio => "audio",
                    AttachmentType.Document => "file",
                    AttachmentType.Archive => "file",
                    AttachmentType.Code => "file",
                    _ => "file",
                };
            }

            // If message has text content, show truncated text
            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                return this.TruncateContent(message.Content, 50);
            }

            return string.Empty;
        }

        private string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            return content.Length <= maxLength ? content : content.Substring(0, maxLength - 3) + "...";
        }

        private void OnConnectionStateChanged(object? sender, bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(() => this.ErrorMessage = !isConnected ? "Connection lost..." : null);
        }

        private void InsertDateSeparators()
        {
            var existing = this.Messages.OfType<DateSeparatorViewModel>().ToList();
            foreach (var s in existing)
            {
                this.Messages.Remove(s);
            }

            DateTime? lastDate = null;
            var itemsToAdd = new List<(int Index, IMessageListItem Item)>();

            for (int i = 0; i < this.Messages.Count; i++)
            {
                if (this.Messages[i] is MessageViewModel m)
                {
                    var date = DateFormatter.GetDateKey(m.CreatedAt);
                    if (lastDate == null || DateFormatter.AreDifferentDays(lastDate.Value, date))
                    {
                        itemsToAdd.Add((i, new DateSeparatorViewModel(date)));
                        lastDate = date;
                    }
                }
            }

            foreach (var (index, item) in itemsToAdd.OrderByDescending(x => x.Index))
            {
                this.Messages.Insert(index, item);
            }
        }

        private int InsertDateSeparatorIfNeeded(DateTime messageDate, int insertIndex)
        {
            var messageDateKey = DateFormatter.GetDateKey(messageDate);
            DateTime? previousMessageDate = null;
            for (int i = insertIndex - 1; i >= 0; i--)
            {
                if (this.Messages[i] is MessageViewModel p)
                {
                    previousMessageDate = DateFormatter.GetDateKey(p.CreatedAt);
                    break;
                }
            }

            if (previousMessageDate == null || DateFormatter.AreDifferentDays(previousMessageDate.Value, messageDateKey))
            {
                bool separatorExists = insertIndex > 0 && this.Messages[insertIndex - 1] is DateSeparatorViewModel s && DateFormatter.GetDateKey(s.Date) == messageDateKey;
                if (!separatorExists)
                {
                    this.Messages.Insert(insertIndex, new DateSeparatorViewModel(messageDateKey));
                    return insertIndex + 1;
                }
            }

            return insertIndex;
        }

        private IEnumerable<MessageViewModel> GetMessageViewModels() => this.Messages.OfType<MessageViewModel>();

        private MessageViewModel? FindMessageViewModel(string messageId) => this.GetMessageViewModels().FirstOrDefault(m => m.Id == messageId);
    }
}
