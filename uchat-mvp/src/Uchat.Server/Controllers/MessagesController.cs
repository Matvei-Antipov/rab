namespace Uchat.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// Controller for message-related endpoints (search, edit, delete).
    /// </summary>
    [ApiController]
    [Route("api/messages")]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService messageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagesController"/> class.
        /// </summary>
        /// <param name="messageService">The message service.</param>
        public MessagesController(IMessageService messageService)
        {
            this.messageService = messageService;
        }

        /// <summary>
        /// Search messages within a specific chat.
        /// </summary>
        /// <param name="chatId">The chat ID to search in.</param>
        /// <param name="query">The text to search for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of matching messages.</returns>
        [HttpGet("{chatId}/search")]
        [ProducesResponseType(typeof(IEnumerable<MessageDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<IEnumerable<MessageDto>>> SearchMessages(
            string chatId,
            [FromQuery] string query,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return this.BadRequest("Search query cannot be empty.");
            }

            // Note: Ensure your IMessageService has a SearchMessagesAsync method.
            var messages = await this.messageService.SearchMessagesAsync(chatId, query, cancellationToken);
            return this.Ok(messages);
        }

        /// <summary>
        /// Edits a specific message.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <param name="request">The edit request containing new content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated message DTO.</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(MessageDto), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<MessageDto>> EditMessage(
            string id,
            [FromBody] EditMessageRequest request,
            CancellationToken cancellationToken)
        {
            var userId = this.HttpContext.Items["UserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return this.BadRequest("Message content cannot be empty.");
            }

            var updatedMessage = await this.messageService.EditMessageAsync(id, request.Content, userId, cancellationToken);
            return this.Ok(updatedMessage);
        }

        /// <summary>
        /// Deletes a specific message.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>No content result.</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> DeleteMessage(string id, CancellationToken cancellationToken)
        {
            var userId = this.HttpContext.Items["UserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            await this.messageService.DeleteMessageAsync(id, userId, cancellationToken);
            return this.NoContent();
        }
    }

    /// <summary>
    /// Request model for editing a message.
    /// </summary>
    public class EditMessageRequest
    {
        public string Content { get; set; } = string.Empty;
    }
}
