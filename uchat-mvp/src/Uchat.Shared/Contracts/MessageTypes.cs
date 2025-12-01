namespace Uchat.Shared.Contracts
{
    /// <summary>
    /// Constants for WebSocket message type identifiers.
    /// Used for message discrimination and routing.
    /// </summary>
    public static class MessageTypes
    {
        /// <summary>
        /// Message type for chat messages.
        /// </summary>
        public const string ChatMessage = "chat_message";

        /// <summary>
        /// Message type for user joined notifications.
        /// </summary>
        public const string UserJoined = "user_joined";

        /// <summary>
        /// Message type for user left notifications.
        /// </summary>
        public const string UserLeft = "user_left";

        /// <summary>
        /// Message type for typing indicators.
        /// </summary>
        public const string TypingIndicator = "typing_indicator";

        /// <summary>
        /// Message type for message delivered notifications.
        /// </summary>
        public const string MessageDelivered = "message_delivered";

        /// <summary>
        /// Message type for message read notifications.
        /// </summary>
        public const string MessageRead = "message_read";

        /// <summary>
        /// Message type for user status changes.
        /// </summary>
        public const string UserStatusChanged = "user_status_changed";

        /// <summary>
        /// Message type for errors.
        /// </summary>
        public const string Error = "error";
    }
}
