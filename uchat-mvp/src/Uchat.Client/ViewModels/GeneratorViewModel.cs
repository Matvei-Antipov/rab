namespace Uchat.Client.ViewModels
{
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.Input;
    using Uchat.Client.Services;

    /// <summary>
    /// View model for the image generator view.
    /// </summary>
    public partial class GeneratorViewModel : ViewModelBase
    {
        private readonly INavigationService navigationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratorViewModel"/> class.
        /// </summary>
        /// <param name="navigationService">The navigation service.</param>
        public GeneratorViewModel(INavigationService navigationService)
        {
            this.navigationService = navigationService;
        }

        /// <summary>
        /// Command to navigate back to chat view.
        /// </summary>
        [RelayCommand]
        private void NavigateBack()
        {
            this.navigationService.NavigateBack();
        }
    }
}
