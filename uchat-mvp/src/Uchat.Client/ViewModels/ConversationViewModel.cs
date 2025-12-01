namespace Uchat.Client.ViewModels
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Enums;

    /// <summary>
    /// View model for a conversation in the conversation list.
    /// </summary>
    public partial class ConversationViewModel : ObservableObject
    {
        private string id;
        private ChatType type;
        private string name;
        private string? avatarUrl;
        private DateTime? lastMessageAt;
        private string lastMessagePreview;
        private int unreadCount;
        private bool isSelected;
        private bool isTyping;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationViewModel"/> class.
        /// </summary>
        /// <param name="chatDto">The chat DTO.</param>
        public ConversationViewModel(ChatDto chatDto)
        {
            this.id = chatDto.Id;
            this.type = chatDto.Type;
            this.name = chatDto.Name ?? "Direct Message";
            this.avatarUrl = chatDto.AvatarUrl;
            this.lastMessageAt = chatDto.LastMessageAt;
            this.lastMessagePreview = string.Empty;
        }

        /// <summary>
        /// Gets the chat ID.
        /// </summary>
        public string Id
        {
            get => this.id;
            private set => this.SetProperty(ref this.id, value);
        }

        /// <summary>
        /// Gets the chat type.
        /// </summary>
        public ChatType Type
        {
            get => this.type;
            private set => this.SetProperty(ref this.type, value);
        }

        /// <summary>
        /// Gets or sets the chat name.
        /// </summary>
        public string Name
        {
            get => this.name;
            set => this.SetProperty(ref this.name, value);
        }

        /// <summary>
        /// Gets or sets the avatar URL.
        /// </summary>
        public string? AvatarUrl
        {
            get => this.avatarUrl;
            set => this.SetProperty(ref this.avatarUrl, value);
        }

        /// <summary>
        /// Gets or sets the last message timestamp.
        /// </summary>
        public DateTime? LastMessageAt
        {
            get => this.lastMessageAt;
            set => this.SetProperty(ref this.lastMessageAt, value);
        }

        /// <summary>
        /// Gets or sets the last message preview text.
        /// </summary>
        public string LastMessagePreview
        {
            get => this.lastMessagePreview;
            set => this.SetProperty(ref this.lastMessagePreview, value);
        }

        /// <summary>
        /// Gets or sets the unread message count.
        /// </summary>
        public int UnreadCount
        {
            get => this.unreadCount;
            set => this.SetProperty(ref this.unreadCount, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this conversation is selected.
        /// </summary>
        public bool IsSelected
        {
            get => this.isSelected;
            set => this.SetProperty(ref this.isSelected, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether someone is typing.
        /// </summary>
        public bool IsTyping
        {
            get => this.isTyping;
            set => this.SetProperty(ref this.isTyping, value);
        }

        /// <summary>
        /// Gets the formatted last message time.
        /// </summary>
        public string LastMessageTime
        {
            get
            {
                if (!this.LastMessageAt.HasValue)
                {
                    return string.Empty;
                }

                var now = DateTime.UtcNow;
                var diff = now - this.LastMessageAt.Value;

                if (diff.TotalMinutes < 1)
                {
                    return "Just now";
                }

                if (diff.TotalHours < 1)
                {
                    return $"{(int)diff.TotalMinutes}m ago";
                }

                if (diff.TotalDays < 1)
                {
                    return $"{(int)diff.TotalHours}h ago";
                }

                if (diff.TotalDays < 7)
                {
                    return $"{(int)diff.TotalDays}d ago";
                }

                return this.LastMessageAt.Value.ToString("MMM dd");
            }
        }
    }
}
