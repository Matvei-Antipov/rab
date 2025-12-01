namespace Uchat.Shared.Serialization
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Uchat.Shared.Contracts;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// JSON serializer context for System.Text.Json source generation.
    /// Provides optimized serialization for all shared DTOs and contracts.
    /// Use this context for high-performance JSON operations.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(UserDto))]
    [JsonSerializable(typeof(ChatDto))]
    [JsonSerializable(typeof(MessageDto))]
    [JsonSerializable(typeof(LoginRequest))]
    [JsonSerializable(typeof(LoginResponse))]
    [JsonSerializable(typeof(RegisterRequest))]
    [JsonSerializable(typeof(RegisterResponse))]
    [JsonSerializable(typeof(SendMessageRequest))]
    [JsonSerializable(typeof(PaginatedResponse<UserDto>))]
    [JsonSerializable(typeof(PaginatedResponse<ChatDto>))]
    [JsonSerializable(typeof(PaginatedResponse<MessageDto>))]
    [JsonSerializable(typeof(ChatMessageContract))]
    [JsonSerializable(typeof(UserJoinedContract))]
    [JsonSerializable(typeof(UserLeftContract))]
    [JsonSerializable(typeof(TypingIndicatorContract))]
    [JsonSerializable(typeof(MessageDeliveredContract))]
    [JsonSerializable(typeof(MessageReadContract))]
    [JsonSerializable(typeof(UserStatusChangedContract))]
    [JsonSerializable(typeof(ErrorContract))]
    public partial class UchatJsonSerializerContext : JsonSerializerContext
    {
    }
}
