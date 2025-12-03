namespace Uchat.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Uchat.Server.Data.Repositories;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Abstractions;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Exceptions;
    using Uchat.Shared.Models;

    /// <summary>
    /// Service for chat operations.
    /// </summary>
    public class ChatService : IChatService
    {
        private readonly IChatRepository chatRepository;
        private readonly IUserRepository userRepository;
        private readonly IMessageRepository messageRepository;
        private readonly IIdGenerator idGenerator;
        private readonly IClock clock;
        private readonly ILogger logger;
        private readonly IUserStatusService userStatusService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatService"/> class.
        /// </summary>
        /// <param name="chatRepository">Chat repository.</param>
        /// <param name="userRepository">User repository.</param>
        /// <param name="messageRepository">Message repository.</param>
        /// <param name="idGenerator">ID generator.</param>
        /// <param name="clock">Clock for timestamps.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="userStatusService">User status service.</param>
        public ChatService(
            IChatRepository chatRepository,
            IUserRepository userRepository,
            IMessageRepository messageRepository,
            IIdGenerator idGenerator,
            IClock clock,
            ILogger logger,
            IUserStatusService userStatusService)
        {
            this.chatRepository = chatRepository;
            this.userRepository = userRepository;
            this.messageRepository = messageRepository;
            this.idGenerator = idGenerator;
            this.clock = clock;
            this.logger = logger;
            this.userStatusService = userStatusService;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ChatDto>> GetUserChatsAsync(string userId, CancellationToken cancellationToken = default)
        {
            this.logger.Information("Getting chats for user {UserId}", userId);
            var chats = await this.chatRepository.GetByUserIdAsync(userId, cancellationToken);
            return chats.Select(this.MapToDto);
        }

        /// <inheritdoc/>
        public async Task<ChatDto?> GetChatByIdAsync(string chatId, CancellationToken cancellationToken = default)
        {
            this.logger.Information("Getting chat {ChatId}", chatId);
            var chat = await this.chatRepository.GetByIdAsync(chatId, cancellationToken);
            return chat != null ? this.MapToDto(chat) : null;
        }

        /// <inheritdoc/>
        public async Task<ChatDto> CreateChatAsync(CreateChatRequest request, string creatorUserId, CancellationToken cancellationToken = default)
        {
            this.logger.Information("Creating chat {ChatName} for user {UserId}", request.Name, creatorUserId);

            // Check for duplicate chat name for this user
            var nameExists = await this.chatRepository.ChatNameExistsForUserAsync(request.Name, creatorUserId, cancellationToken);
            if (nameExists)
            {
                this.logger.Warning("Chat name {ChatName} already exists for user {UserId}", request.Name, creatorUserId);
                throw new DuplicateChatException(request.Name);
            }

            // Deduplicate participant IDs and ensure creator is included
            var participantIds = request.ParticipantIds.Distinct().ToList();
            if (!participantIds.Contains(creatorUserId))
            {
                participantIds.Add(creatorUserId);
            }

            // Validate that all participants exist
            var participantUsers = new List<User>();
            foreach (var participantId in participantIds)
            {
                var user = await this.userRepository.GetByIdAsync(participantId, cancellationToken);
                if (user == null)
                {
                    this.logger.Warning("Participant user {UserId} not found during chat creation", participantId);
                    throw new ValidationException($"User with ID '{participantId}' does not exist.");
                }

                participantUsers.Add(user);
            }

            // Create the chat entity
            var now = this.clock.UtcNow;
            var chat = new Chat
            {
                Id = this.idGenerator.GenerateId(),
                Type = request.Type,
                Name = request.Name,
                Description = request.Description,
                AvatarUrl = request.AvatarUrl,
                ParticipantIds = participantIds,
                CreatedBy = creatorUserId,
                CreatedAt = now,
                UpdatedAt = now,
                LastMessageAt = null,
            };

            // Persist the chat
            await this.chatRepository.CreateAsync(chat, cancellationToken);
            this.logger.Information("Chat {ChatId} created successfully by user {UserId}", chat.Id, creatorUserId);

            // Build the response DTO with participant details
            var chatDto = this.MapToDto(chat);
            var participantDtos = new List<UserDto>();
            foreach (var participant in participantUsers)
            {
                participantDtos.Add(await this.MapUserToDtoAsync(participant, cancellationToken));
            }

            chatDto.Participants = participantDtos;

            return chatDto;
        }

        /// <inheritdoc/>
        public async Task DeleteChatAsync(string chatId, CancellationToken cancellationToken = default)
        {
            this.logger.Information("Deleting chat {ChatId}", chatId);

            await this.messageRepository.DeleteAllByChatIdAsync(chatId, cancellationToken);
            this.logger.Information("Deleted all messages for chat {ChatId}", chatId);

            await this.chatRepository.DeleteAsync(chatId, cancellationToken);
            this.logger.Information("Chat {ChatId} deleted successfully", chatId);
        }

        private ChatDto MapToDto(Chat chat)
        {
            return new ChatDto
            {
                Id = chat.Id,
                Type = chat.Type,
                Name = chat.Name,
                Description = chat.Description,
                AvatarUrl = chat.AvatarUrl,
                ParticipantIds = chat.ParticipantIds,
                CreatedBy = chat.CreatedBy,
                CreatedAt = chat.CreatedAt,
                LastMessageAt = chat.LastMessageAt,
            };
        }

        private async Task<UserDto> MapUserToDtoAsync(User user, CancellationToken cancellationToken = default)
        {
            // Get status from Redis instead of Oracle
            var status = await this.userStatusService.GetStatusAsync(user.Id, cancellationToken);

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Status = status,
                LastSeenAt = user.LastSeenAt,
            };
        }
    }
}
