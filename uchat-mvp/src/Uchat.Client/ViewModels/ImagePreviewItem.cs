namespace Uchat.Client.ViewModels
{
    using System;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// Represents an item in the image preview.
    /// </summary>
    public class ImagePreviewItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImagePreviewItem"/> class.
        /// </summary>
        /// <param name="attachment">The message attachment.</param>
        /// <param name="senderName">The name of the sender.</param>
        /// <param name="timestamp">The timestamp when the image was sent.</param>
        public ImagePreviewItem(MessageAttachmentDto attachment, string senderName, DateTime timestamp)
        {
            this.Attachment = attachment;
            this.SenderName = senderName;
            this.Timestamp = timestamp;
        }

        /// <summary>
        /// Gets the message attachment.
        /// </summary>
        public MessageAttachmentDto Attachment { get; }

        /// <summary>
        /// Gets the sender name.
        /// </summary>
        public string SenderName { get; }

        /// <summary>
        /// Gets the timestamp when the image was sent.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the formatted timestamp string.
        /// </summary>
        public string FormattedTimestamp => this.Timestamp.ToString("dd MMM yyyy, HH:mm");

        /// <summary>
        /// Gets the formatted file size string.
        /// </summary>
        public string FormattedSize => this.FormatFileSize(this.Attachment.FileSize);

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
