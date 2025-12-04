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
        private List<ImagePreviewItem> allImages;
        private int currentIndex;
        private ImagePreviewItem? currentItem;
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
            this.allImages = new List<ImagePreviewItem>();
            this.currentIndex = 0;
        }

        /// <summary>
        /// Gets the current image item.
        /// </summary>
        public ImagePreviewItem? CurrentItem
        {
            get => this.currentItem;
            private set
            {
                if (this.SetProperty(ref this.currentItem, value))
                {
                    this.OnPropertyChanged(nameof(this.CurrentImage));
                    this.OnPropertyChanged(nameof(this.SenderName));
                }
            }
        }

        /// <summary>
        /// Gets the current attachment DTO.
        /// </summary>
        public MessageAttachmentDto? CurrentImage => this.CurrentItem?.Attachment;

        /// <summary>
        /// Gets the sender name of the current image.
        /// </summary>
        public string SenderName => this.CurrentItem?.SenderName ?? string.Empty;

        /// <summary>
        /// Gets the formatted timestamp of the current image.
        /// </summary>
        public string FormattedTimestamp => this.CurrentItem?.FormattedTimestamp ?? string.Empty;

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
        /// Gets the current image source.
        /// </summary>
        public System.Windows.Media.Imaging.BitmapImage? CurrentImageSource
        {
            get => this.currentImageSource;
            private set => this.SetProperty(ref this.currentImageSource, value);
        }

        /// <summary>
        /// Gets a value indicating whether the image is currently loading.
        /// </summary>
        public bool IsLoading
        {
            get => this.isLoading;
            private set => this.SetProperty(ref this.isLoading, value);
        }

        /// <summary>
        /// Sets the images to display.
        /// </summary>
        /// <param name="images">The collection of image items.</param>
        /// <param name="startIndex">The starting index.</param>
        public void SetImages(List<ImagePreviewItem> images, int startIndex)
        {
            this.logger.Information("SetImages: Received {ImageCount} images, startIndex: {StartIndex}", images?.Count ?? 0, startIndex);
            
            this.allImages = images ?? new List<ImagePreviewItem>();
            this.currentIndex = Math.Max(0, Math.Min(startIndex, this.allImages.Count - 1));
            
            this.logger.Information("SetImages: Set currentIndex to {Index}, TotalImages: {Total}", this.currentIndex, this.allImages.Count);
            
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
            this.logger.Information("UpdateCurrentImage: currentIndex={Index}, allImages.Count={Count}", this.currentIndex, this.allImages.Count);
            
            this.CurrentItem = this.currentIndex >= 0 && this.currentIndex < this.allImages.Count
                ? this.allImages[this.currentIndex]
                : null;

            this.logger.Information("UpdateCurrentImage: CurrentItem set to {Item}, CurrentImageNumber={Number}", 
                this.CurrentItem?.Attachment?.FileName ?? "null", this.CurrentImageNumber);

            this.OnPropertyChanged(nameof(this.CurrentImageNumber));
            this.OnPropertyChanged(nameof(this.HasPrevious));
            this.OnPropertyChanged(nameof(this.HasNext));
            this.OnPropertyChanged(nameof(this.CurrentImageUrl));
            this.PreviousImageCommand.NotifyCanExecuteChanged();
            this.NextImageCommand.NotifyCanExecuteChanged();

            // Load image for current attachment
            if (this.CurrentImage != null)
            {
                this.logger.Information("UpdateCurrentImage: Loading image {FileName}", this.CurrentImage.FileName);
                _ = this.LoadCurrentImageAsync();
            }
            else
            {
                this.logger.Warning("UpdateCurrentImage: CurrentImage is null, not loading");
            }
        }

        private async Task LoadCurrentImageAsync()
        {
            if (this.CurrentImage == null)
            {
                this.logger.Warning("LoadCurrentImageAsync: CurrentImage is null");
                return;
            }

            try
            {
                this.logger.Information("LoadCurrentImageAsync: Starting load for {FileName} (ID: {Id})", this.CurrentImage.FileName, this.CurrentImage.Id);
                this.IsLoading = true;

                // Load full image for preview
                Stream imageStream = await this.fileAttachmentService.DownloadImageStreamAsync(this.CurrentImage);
                this.logger.Information("LoadCurrentImageAsync: Stream downloaded, length: {Length}", imageStream.Length);

                using (imageStream)
                {
                    var memoryStream = new MemoryStream();
                    await imageStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    this.logger.Information("LoadCurrentImageAsync: Stream copied to memory, length: {Length}", memoryStream.Length);

                    // Create bitmap on UI thread
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try 
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = memoryStream;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            this.CurrentImageSource = bitmap;
                            this.logger.Information("LoadCurrentImageAsync: Bitmap created and set. Width: {Width}, Height: {Height}", bitmap.PixelWidth, bitmap.PixelHeight);
                        }
                        catch (Exception ex)
                        {
                            this.logger.Error(ex, "LoadCurrentImageAsync: Failed to create bitmap on UI thread");
                            throw;
                        }
                    });
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
                this.logger.Information("LoadCurrentImageAsync: Finished. IsLoading set to false.");
            }
        }
    }
}
