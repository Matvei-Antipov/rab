namespace Uchat.Client.ViewModels
{
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.Input;
    using Uchat.Client.Services;

    /// <summary>
    /// View model for the settings screen.
    /// </summary>
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly IAuthenticationService authenticationService;
        private readonly INavigationService navigationService;
        private readonly IThemeManager themeManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
        /// </summary>
        /// <param name="authenticationService">The authentication service.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="themeManager">The theme manager service.</param>
        public SettingsViewModel(
            IAuthenticationService authenticationService,
            INavigationService navigationService,
            IThemeManager themeManager)
        {
            this.authenticationService = authenticationService;
            this.navigationService = navigationService;
            this.themeManager = themeManager;
            this.Title = "Settings";
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await this.authenticationService.LogoutAsync();
            this.navigationService.ClearHistory();
            this.navigationService.NavigateTo<WelcomeViewModel>();
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            this.themeManager.ToggleTheme();
        }

        [RelayCommand]
        private void NavigateToChat()
        {
            this.navigationService.NavigateTo<ChatViewModel>();
        }

        [RelayCommand]
        private void NavigateToGenerator()
        {
            this.navigationService.NavigateTo<GeneratorViewModel>();
        }
    }
}
