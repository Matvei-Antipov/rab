namespace Uchat.Client.Services
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using Uchat.Client.ViewModels;

    /// <summary>
    /// Service for navigation between views.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Stack<ViewModelBase> navigationHistory = new Stack<ViewModelBase>();

        // Храним текущую ViewModel здесь, внутри сервиса
        private ViewModelBase? currentViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider.</param>
        public NavigationService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public event EventHandler<ViewModelBase>? CurrentViewModelChanged;

        /// <inheritdoc/>
        public ViewModelBase? CurrentViewModel => this.currentViewModel;

        /// <inheritdoc/>
        public void NavigateTo<TViewModel>()
            where TViewModel : ViewModelBase
        {
            // Call OnNavigatedFrom on the current view model
            if (this.currentViewModel != null)
            {
                this.currentViewModel.OnNavigatedFrom();
                this.navigationHistory.Push(this.currentViewModel);
            }

            // Create new view model
            var newViewModel = this.serviceProvider.GetRequiredService<TViewModel>();

            // Update internal state
            this.currentViewModel = newViewModel;

            // Call OnNavigatedTo on the new view model
            newViewModel.OnNavigatedTo();

            // Notify subscribers (Main Window will listen to this)
            this.CurrentViewModelChanged?.Invoke(this, newViewModel);
        }

        /// <inheritdoc/>
        public void NavigateBack()
        {
            if (this.navigationHistory.Count > 0)
            {
                // Call OnNavigatedFrom on the current view model
                if (this.currentViewModel != null)
                {
                    this.currentViewModel.OnNavigatedFrom();
                }

                var previousViewModel = this.navigationHistory.Pop();

                // Update internal state
                this.currentViewModel = previousViewModel;

                // Call OnNavigatedTo on the restored view model
                previousViewModel.OnNavigatedTo();

                // Notify subscribers
                this.CurrentViewModelChanged?.Invoke(this, previousViewModel);
            }
        }

        /// <inheritdoc/>
        public void GoBack()
        {
            this.NavigateBack();
        }

        /// <inheritdoc/>
        public void ClearHistory()
        {
            this.navigationHistory.Clear();
            this.currentViewModel = null;
        }
    }
}
