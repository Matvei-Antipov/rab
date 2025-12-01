namespace Uchat.Shared.Enums
{
    /// <summary>
    /// Represents the type of WebSocket message.
    /// </summary>
    public enum WebSocketMessageType
    {
        /// <summary>
        /// New chat message from user.
        /// </summary>
        NewMessage = 0,

        /// <summary>
        /// Edit existing message.
        /// </summary>
        EditMessage = 1,

        /// <summary>
        /// Delete a message.
        /// </summary>
        DeleteMessage = 2,

        /// <summary>
        /// Message delivery confirmation.
        /// </summary>
        MessageDelivered = 3,

        /// <summary>
        /// Message read confirmation.
        /// </summary>
        MessageRead = 4,

        /// <summary>
        /// User typing indicator.
        /// </summary>
        Typing = 5,

        /// <summary>
        /// User status update.
        /// </summary>
        StatusUpdate = 6,

        /// <summary>
        /// Heartbeat/ping message.
        /// </summary>
        Heartbeat = 7,

        /// <summary>
        /// Error message.
        /// </summary>
        Error = 8,

        /// <summary>
        /// Authentication message.
        /// </summary>
        Authenticate = 9,

        /// <summary>
        /// Resume session with token.
        /// </summary>
        Resume = 10,
    }
}
