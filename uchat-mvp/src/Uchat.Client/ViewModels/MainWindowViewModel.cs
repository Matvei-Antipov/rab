namespace Uchat.Client.ViewModels
{
    using System;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Uchat.Client.Services;

    public partial class MainWindowViewModel : ViewModelBase
    {
        private const string MoonIcon = "\xE708";
        private const string SunIcon = "\xE706";

        private readonly IThemeManager themeManager;
        private readonly INavigationService navigationService;

        private bool isDarkTheme = true;

        [ObservableProperty]
        private ViewModelBase? currentViewModel;

        [ObservableProperty]
        private object? currentView;

        [ObservableProperty]
        private string title = "Uchat Client";

        [ObservableProperty]
        private string themeIcon = MoonIcon;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
        /// </summary>
        /// <param name="themeManager">The theme manager service.</param>
        /// <param name="navigationService">The navigation service.</param>
        public MainWindowViewModel(IThemeManager themeManager, INavigationService navigationService)
        {
            this.themeManager = themeManager;
            this.navigationService = navigationService;

            this.ToggleThemeCommand = new RelayCommand(this.ToggleTheme);
            this.NavigateToChatCommand = new RelayCommand(this.NavigateToChat);
            this.NavigateToGeneratorCommand = new RelayCommand(this.NavigateToGenerator);
            this.NavigateToSettingsCommand = new RelayCommand(this.NavigateToSettings);

            this.navigationService.CurrentViewModelChanged += this.OnNavigationChanged;

            if (this.navigationService.CurrentViewModel != null)
            {
                this.CurrentViewModel = this.navigationService.CurrentViewModel;
            }
        }

        /// <summary>
        /// Gets the command to toggle the theme.
        /// </summary>
        public ICommand ToggleThemeCommand { get; }

        /// <summary>
        /// Gets the command to navigate to the chat view.
        /// </summary>
        public ICommand NavigateToChatCommand { get; }

        /// <summary>
        /// Gets the command to navigate to the generator view.
        /// </summary>
        public ICommand NavigateToGeneratorCommand { get; }

        /// <summary>
        /// Gets the command to navigate to the settings view.
        /// </summary>
        public ICommand NavigateToSettingsCommand { get; }

        private void ToggleTheme()
        {
            this.themeManager.ToggleTheme();
            this.isDarkTheme = !this.isDarkTheme;
            this.ThemeIcon = this.isDarkTheme ? MoonIcon : SunIcon;
        }

        private void OnNavigationChanged(object? sender, ViewModelBase e)
        {
            this.CurrentViewModel = e;
        }

        private void NavigateToChat()
        {
            this.navigationService.NavigateTo<ChatViewModel>();
        }

        private void NavigateToGenerator()
        {
            this.navigationService.NavigateTo<GeneratorViewModel>();
        }

        private void NavigateToSettings()
        {
            // !!! ВАЖНОЕ ИСПРАВЛЕНИЕ: SettingsViewModel, а не SettingsView !!!
            this.navigationService.NavigateTo<SettingsViewModel>();
        }
    }
}
