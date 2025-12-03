namespace Uchat.Client.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.Input;
    using Serilog;
    using Uchat.Client.Services;

    /// <summary>
    /// View model for the images grid view.
    /// </summary>
    public partial class ImagesGridViewModel : ViewModelBase
    {
        private readonly INavigationService navigationService;
        private readonly IFileAttachmentService fileAttachmentService;
        private readonly IErrorHandlingService errorHandlingService;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private string conversationName;
        private ObservableCollection<AttachmentViewModel> imagesList;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImagesGridViewModel"/> class.
        /// </summary>
        public ImagesGridViewModel(
            INavigationService navigationService,
            IFileAttachmentService fileAttachmentService,
            IErrorHandlingService errorHandlingService,
            IServiceProvider serviceProvider,
            ILogger logger)
        {
            this.navigationService = navigationService;
            this.fileAttachmentService = fileAttachmentService;
            this.errorHandlingService = errorHandlingService;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.Title = "Images";
            this.conversationName = string.Empty;
            this.imagesList = new ObservableCollection<AttachmentViewModel>();
        }

        /// <summary>
        /// Gets or sets the conversation name.
        /// </summary>
        public string ConversationName
        {
            get => this.conversationName;
            set => this.SetProperty(ref this.conversationName, value);
        }

        /// <summary>
        /// Gets the images list collection.
        /// </summary>
        public ObservableCollection<AttachmentViewModel> ImagesList => this.imagesList;

        /// <summary>
        /// Sets the images to display.
        /// </summary>
        /// <param name="images">The collection of image attachments.</param>
        /// <param name="conversationName">The name of the conversation.</param>
        public async void SetImages(ObservableCollection<AttachmentViewModel> images, string conversationName)
        {
            this.imagesList.Clear();
            foreach (var image in images)
            {
                this.imagesList.Add(image);
            }

            this.ConversationName = conversationName;

            // Initialize thumbnails for all images
            foreach (var image in this.imagesList)
            {
                try
                {
                    await image.InitializeAsync();
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "Failed to initialize thumbnail for {FileName}", image.FileName);
                }
            }
        }

        [RelayCommand]
        private void NavigateBack()
        {
            this.navigationService.NavigateBack();
        }

        [RelayCommand]
        private void PreviewImage(AttachmentViewModel image)
        {
            if (image?.AttachmentDto == null)
            {
                return;
            }

            try
            {
                var imagePreviewViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ImagePreviewViewModel>(
                    this.serviceProvider);
                
                var allImageDtos = this.imagesList
                    .Where(img => img.AttachmentDto != null)
                    .Select(img => img.AttachmentDto!)
                    .ToList();
                
                var currentIndex = allImageDtos.FindIndex(img => img.Id == image.AttachmentDto.Id);
                imagePreviewViewModel.SetImages(allImageDtos, Math.Max(0, currentIndex));
                
                this.navigationService.NavigateTo<ImagePreviewViewModel>();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to preview image");
                this.errorHandlingService.ShowError("Failed to preview image");
            }
        }

        [RelayCommand]
        private async Task DownloadAttachmentAsync(AttachmentViewModel attachment)
        {
            if (attachment?.AttachmentDto == null)
            {
                return;
            }

            try
            {
                var path = await this.fileAttachmentService.DownloadAttachmentAsync(attachment.AttachmentDto);
                this.errorHandlingService.ShowInfo($"Downloaded to: {path}");
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to download attachment");
                this.errorHandlingService.ShowError(ex.Message);
            }
        }
    }
}
