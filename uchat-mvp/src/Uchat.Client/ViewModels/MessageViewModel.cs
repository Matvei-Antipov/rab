namespace Uchat.Client.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Serilog;
    using Uchat.Client.Services;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Enums;

    /// <summary>
    /// View model for a single message.
    /// </summary>
    public partial class MessageViewModel : ObservableObject, IMessageListItem
    {
        private readonly IFileAttachmentService? fileAttachmentService;
        private string id;
        private string chatId;
        private string senderId;
        private string content;
        private MessageStatus status;
        private DateTime createdAt;
        private DateTime? editedAt;
        private bool isDeleted;
        private bool isEditing;
        private bool isCurrentUser;
        private ObservableCollection<AttachmentViewModel> attachments;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageViewModel"/> class.
        /// </summary>
        /// <param name="messageDto">The message DTO.</param>
        /// <param name="currentUserId">The current user's ID.</param>
        /// <param name="fileAttachmentService">Optional file attachment service for loading images.</param>
        public MessageViewModel(MessageDto messageDto, string currentUserId, IFileAttachmentService? fileAttachmentService = null)
        {
            this.fileAttachmentService = fileAttachmentService;
            this.id = messageDto.Id;
            this.chatId = messageDto.ChatId;
            this.senderId = messageDto.SenderId;
            this.content = messageDto.Content;
            this.status = messageDto.Status;
            this.createdAt = messageDto.CreatedAt;
            this.editedAt = messageDto.EditedAt;
            this.isDeleted = messageDto.IsDeleted;
            this.isCurrentUser = messageDto.SenderId == currentUserId;
            
            // Log attachment information for debugging
            Log.Information("MessageViewModel: Creating for message {MessageId} with {AttachmentCount} attachments", 
                messageDto.Id, messageDto.Attachments?.Count ?? 0);
                
            if (messageDto.Attachments != null)
            {
                foreach (var attachment in messageDto.Attachments)
                {
                    Log.Information("MessageViewModel: Attachment {Id} - {FileName}, Type: {Type}, HasDownloadUrl: {HasUrl}", 
                        attachment.Id, attachment.FileName, attachment.AttachmentType, !string.IsNullOrEmpty(attachment.DownloadUrl));
                }
            }
            
            this.attachments = new ObservableCollection<AttachmentViewModel>(
                messageDto.Attachments?.Select(a => new AttachmentViewModel(a, fileAttachmentService)) ?? Enumerable.Empty<AttachmentViewModel>());

            // Initialize image thumbnails asynchronously
            if (fileAttachmentService != null)
            {
                _ = this.InitializeAttachmentsAsync();
            }
        }

        private async System.Threading.Tasks.Task InitializeAttachmentsAsync()
        {
            foreach (var attachment in this.attachments)
            {
                if (attachment.IsImage)
                {
                    try
                    {
                        Log.Information("MessageViewModel: Initializing attachment {FileName}", attachment.FileName);
                        await attachment.InitializeAsync();
                        Log.Information("MessageViewModel: Successfully initialized attachment {FileName}", attachment.FileName);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "MessageViewModel: Failed to initialize attachment {FileName}", attachment.FileName);
                        // Ignore errors during initialization
                    }
                }
            }
        }

        /// <summary>
        /// Gets the message ID.
        /// </summary>
        public string Id
        {
            get => this.id;
            private set => this.SetProperty(ref this.id, value);
        }

        /// <summary>
        /// Gets the chat ID.
        /// </summary>
        public string ChatId
        {
            get => this.chatId;
            private set => this.SetProperty(ref this.chatId, value);
        }

        /// <summary>
        /// Gets the sender ID.
        /// </summary>
        public string SenderId
        {
            get => this.senderId;
            private set => this.SetProperty(ref this.senderId, value);
        }

        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        public string Content
        {
            get => this.content;
            set
            {
                if (this.SetProperty(ref this.content, value))
                {
                    this.OnPropertyChanged(nameof(this.HasContent));
                }
            }
        }

        /// <summary>
        /// Gets or sets the message status.
        /// </summary>
        public MessageStatus Status
        {
            get => this.status;
            set => this.SetProperty(ref this.status, value);
        }

        /// <summary>
        /// Gets the created timestamp.
        /// </summary>
        public DateTime CreatedAt
        {
            get => this.createdAt;
            private set => this.SetProperty(ref this.createdAt, value);
        }

        /// <summary>
        /// Gets or sets the edited timestamp.
        /// </summary>
        public DateTime? EditedAt
        {
            get => this.editedAt;
            set => this.SetProperty(ref this.editedAt, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the message is deleted.
        /// </summary>
        public bool IsDeleted
        {
            get => this.isDeleted;
            set => this.SetProperty(ref this.isDeleted, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the message is being edited.
        /// </summary>
        public bool IsEditing
        {
            get => this.isEditing;
            set => this.SetProperty(ref this.isEditing, value);
        }

        /// <summary>
        /// Gets a value indicating whether this message was sent by the current user.
        /// </summary>
        public bool IsCurrentUser
        {
            get => this.isCurrentUser;
            private set => this.SetProperty(ref this.isCurrentUser, value);
        }

        /// <summary>
        /// Gets a value indicating whether the message is edited.
        /// </summary>
        public bool IsEdited => this.EditedAt.HasValue;

        /// <summary>
        /// Gets the attachments collection.
        /// </summary>
        public ObservableCollection<AttachmentViewModel> Attachments
        {
            get => this.attachments;
        }

        /// <summary>
        /// Gets a value indicating whether this message has attachments.
        /// </summary>
        public bool HasAttachments => this.attachments != null && this.attachments.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this message has visible content (not just whitespace).
        /// </summary>
        public bool HasContent => !string.IsNullOrWhiteSpace(this.content);

        /// <summary>
        /// Gets the status display text.
        /// </summary>
        public string StatusText => this.Status switch
        {
            MessageStatus.Sent => "Sent",
            MessageStatus.Delivered => "Delivered",
            MessageStatus.Read => "Read",
            MessageStatus.Failed => "Failed",
            _ => string.Empty,
        };

        /// <summary>
        /// Updates the message from a DTO.
        /// </summary>
        /// <param name="messageDto">The updated message DTO.</param>
        public void UpdateFromDto(MessageDto messageDto)
        {
            this.Content = messageDto.Content;
            this.Status = messageDto.Status;
            this.EditedAt = messageDto.EditedAt;
            this.IsDeleted = messageDto.IsDeleted;

            // Update attachments
            if (messageDto.Attachments != null)
            {
                this.attachments.Clear();
                foreach (var attachment in messageDto.Attachments)
                {
                    this.attachments.Add(new AttachmentViewModel(attachment, this.fileAttachmentService));
                }
            }
        }
    }
}
