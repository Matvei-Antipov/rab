namespace Uchat.Client.Services
{
    using System;
    using Uchat.Client.Infrastructure;

    /// <summary>
    /// Interface for theme management.
    /// </summary>
    public interface IThemeManager
    {
        /// <summary>
        /// Gets the current theme.
        /// </summary>
        AppTheme CurrentTheme { get; }

        /// <summary>
        /// Event raised when the theme changes.
        /// </summary>
        event EventHandler<AppTheme>? ThemeChanged;

        /// <summary>
        /// Applies the specified theme.
        /// </summary>
        /// <param name="theme">Theme to apply.</param>
        void ApplyTheme(AppTheme theme);

        /// <summary>
        /// Gets the current theme.
        /// </summary>
        /// <returns>Current theme.</returns>
        AppTheme GetCurrentTheme();

        /// <summary>
        /// Toggles between light and dark theme.
        /// </summary>
        void ToggleTheme();
    }
}
