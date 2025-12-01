namespace Uchat.Client.Infrastructure.Messages
{
    using Uchat.Client.Infrastructure;

    /// <summary>
    /// Message sent when theme changes.
    /// </summary>
    public class ThemeChangedMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeChangedMessage"/> class.
        /// </summary>
        /// <param name="theme">New theme.</param>
        public ThemeChangedMessage(AppTheme theme)
        {
            this.Theme = theme;
        }

        /// <summary>
        /// Gets the new theme.
        /// </summary>
        public AppTheme Theme { get; }
    }
}
