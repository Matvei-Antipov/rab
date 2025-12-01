namespace Uchat.Client.Services
{
    using System;
    using System.Linq;
    using System.Windows;
    using Uchat.Client.Infrastructure;

    /// <summary>
    /// Manages application themes.
    /// </summary>
    public class ThemeManager : IThemeManager
    {
        private AppTheme currentTheme = AppTheme.Dark;

        /// <inheritdoc/>
        public event EventHandler<AppTheme>? ThemeChanged;

        /// <inheritdoc/>
        public AppTheme CurrentTheme => this.currentTheme;

        /// <inheritdoc/>
        public void ApplyTheme(AppTheme theme)
        {
            if (this.currentTheme == theme)
            {
                return;
            }

            this.currentTheme = theme;

            var themeUri = theme switch
            {
                AppTheme.Dark => new Uri("Themes/DarkTheme.xaml", UriKind.Relative),
                AppTheme.Light => new Uri("Themes/LightTheme.xaml", UriKind.Relative),
                _ => new Uri("Themes/DarkTheme.xaml", UriKind.Relative),
            };

            var existingTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme.xaml") == true);

            if (existingTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
            }

            var newTheme = new ResourceDictionary { Source = themeUri };
            Application.Current.Resources.MergedDictionaries.Add(newTheme);

            this.ThemeChanged?.Invoke(this, theme);
        }

        /// <inheritdoc/>
        public AppTheme GetCurrentTheme()
        {
            return this.currentTheme;
        }

        /// <inheritdoc/>
        public void ToggleTheme()
        {
            var newTheme = this.currentTheme == AppTheme.Dark
                ? AppTheme.Light
                : AppTheme.Dark;
            this.ApplyTheme(newTheme);
        }
    }
}
