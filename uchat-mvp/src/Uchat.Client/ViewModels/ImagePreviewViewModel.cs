namespace Uchat.Client.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.Input;
    using Serilog;
    using Uchat.Client.Services;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// View model for the image preview view.
    /// </summary>
    public partial class ImagePreviewViewModel : ViewModelBase
    {
        private readonly INavigationService navigationService;
        private readonly IFileAttachmentService fileAttachmentService;
        private readonly IErrorHandlingService errorHandlingService;
        private readonly ILogger logger;
        private List<MessageAttachmentDto> allImages;
        private int currentIndex;
        private MessageAttachmentDto? currentImage;
        private System.Windows.Media.Imaging.BitmapImage? currentImageSource;
        private bool isLoading;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImagePreviewViewModel"/> class.
        /// </summary>
        public ImagePreviewViewModel(
            INavigationService navigationService,
            IFileAttachmentService fileAttachmentService,
            IErrorHandlingService errorHandlingService,
            ILogger logger)
        {
            this.navigationService = navigationService;
            this.fileAttachmentService = fileAttachmentService;
            this.errorHandlingService = errorHandlingService;
            this.logger = logger;
            this.Title = "Image Preview";
            this.allImages = new List<MessageAttachmentDto>();
            this.currentIndex = 0;
        }

        /// <summary>
        /// Gets the current image.
        /// </summary>
        public MessageAttachmentDto? CurrentImage
        {
            get => this.currentImage;
            private set => this.SetProperty(ref this.currentImage, value);
        }

        /// <summary>
        /// Gets the current image index (1-based).
        /// </summary>
        public int CurrentImageNumber => this.currentIndex + 1;

        /// <summary>
        /// Gets the total number of images.
        /// </summary>
        public int TotalImages => this.allImages.Count;

        /// <summary>
        /// Gets a value indicating whether there is a previous image.
        /// </summary>
        public bool HasPrevious => this.currentIndex > 0;

        /// <summary>
        /// Gets a value indicating whether there is a next image.
        /// </summary>
        public bool HasNext => this.currentIndex < this.allImages.Count - 1;

        /// <summary>
        /// Gets the current image URL.
        /// </summary>
        public string CurrentImageUrl => this.CurrentImage?.DownloadUrl ?? string.Empty;

        /// <summary>
        /// Gets or sets the current image source.
        /// </summary>
        public System.Windows.Media.Imaging.BitmapImage? CurrentImageSource
        {
            get => this.currentImageSource;
            private set => this.SetProperty(ref this.currentImageSource, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the image is currently loading.
        /// </summary>
        public bool IsLoading
        {
            get => this.isLoading;
            private set => this.SetProperty(ref this.isLoading, value);
        }

        /// <summary>
        /// Sets the images to display.
        /// </summary>
        /// <param name="images">The collection of image attachments.</param>
        /// <param name="startIndex">The starting index.</param>
        public void SetImages(List<MessageAttachmentDto> images, int startIndex)
        {
            this.allImages = images;
            this.currentIndex = Math.Max(0, Math.Min(startIndex, images.Count - 1));
            this.UpdateCurrentImage();
        }

        [RelayCommand]
        private void NavigateBack()
        {
            this.navigationService.NavigateBack();
        }

        [RelayCommand(CanExecute = nameof(HasPrevious))]
        private void PreviousImage()
        {
            if (this.HasPrevious)
            {
                this.currentIndex--;
                this.UpdateCurrentImage();
            }
        }

        [RelayCommand(CanExecute = nameof(HasNext))]
        private void NextImage()
        {
            if (this.HasNext)
            {
                this.currentIndex++;
                this.UpdateCurrentImage();
            }
        }

        [RelayCommand]
        private async Task DownloadCurrentImageAsync()
        {
            if (this.CurrentImage == null)
            {
                return;
            }

            try
            {
                var path = await this.fileAttachmentService.DownloadAttachmentAsync(this.CurrentImage);
                this.errorHandlingService.ShowInfo($"Downloaded to: {path}");
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to download image");
                this.errorHandlingService.ShowError(ex.Message);
            }
        }

        [RelayCommand]
        private async Task SaveCurrentImageAsAsync()
        {
            if (this.CurrentImage == null)
            {
                return;
            }

            try
            {
                var path = await this.fileAttachmentService.SaveAttachmentAsAsync(this.CurrentImage);
                if (path != null)
                {
                    this.errorHandlingService.ShowInfo($"Saved to: {path}");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to save image");
                this.errorHandlingService.ShowError(ex.Message);
            }
        }

        private void UpdateCurrentImage()
        {
            this.CurrentImage = this.currentIndex >= 0 && this.currentIndex < this.allImages.Count
                ? this.allImages[this.currentIndex]
                : null;

            this.OnPropertyChanged(nameof(this.CurrentImageNumber));
            this.OnPropertyChanged(nameof(this.HasPrevious));
            this.OnPropertyChanged(nameof(this.HasNext));
            this.OnPropertyChanged(nameof(this.CurrentImageUrl));
            this.PreviousImageCommand.NotifyCanExecuteChanged();
            this.NextImageCommand.NotifyCanExecuteChanged();

            // Load image for current attachment
            if (this.CurrentImage != null)
            {
                _ = this.LoadCurrentImageAsync();
            }
        }

        private async Task LoadCurrentImageAsync()
        {
            if (this.CurrentImage == null)
            {
                return;
            }

            try
            {
                this.IsLoading = true;

                // Load full image for preview
                Stream imageStream = await this.fileAttachmentService.DownloadImageStreamAsync(this.CurrentImage);

                using (imageStream)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await imageStream.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = memoryStream;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        this.CurrentImageSource = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load image for preview");
                this.CurrentImageSource = null;
            }
            finally
            {
                this.IsLoading = false;
            }
        }
    }
}
