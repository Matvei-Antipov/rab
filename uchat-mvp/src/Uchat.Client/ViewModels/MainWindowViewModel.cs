namespace Uchat.Client.ViewModels
{
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Uchat.Client.Services;

    /// <summary>
    /// View model for the main window.
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IThemeManager themeManager;

        [ObservableProperty]
        private ViewModelBase? currentViewModel;

        [ObservableProperty]
        private string title = "Uchat Client";

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
        /// </summary>
        /// <param name="themeManager">Theme manager.</param>
        public MainWindowViewModel(IThemeManager themeManager)
        {
            this.themeManager = themeManager;

            // Напрямую вызываем метод ToggleTheme из IThemeManager
            this.ToggleThemeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(this.themeManager.ToggleTheme);
        }

        /// <summary>
        /// Gets the command to toggle theme.
        /// </summary>
        public ICommand ToggleThemeCommand { get; }

        /// <summary>
        /// Sets the current view model.
        /// </summary>
        /// <param name="viewModel">View model to set as current.</param>
        public void SetCurrentViewModel(ViewModelBase? viewModel)
        {
            this.CurrentViewModel = viewModel;
        }
    }
}
