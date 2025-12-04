namespace Uchat.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Win32;
    using Serilog;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Helpers;

    /// <summary>
    /// Implementation of file attachment service for WPF client with robust caching.
    /// </summary>
    public class FileAttachmentService : IFileAttachmentService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger logger;
        private readonly IErrorHandlingService errorHandler;
        private readonly string downloadPath;
        private readonly string cachePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileAttachmentService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making requests.</param>
        /// <param name="logger">The logger for logging operations.</param>
        /// <param name="errorHandler">The error handling service.</param>
        public FileAttachmentService(
            HttpClient httpClient,
            ILogger logger,
            IErrorHandlingService errorHandler)
        {
            this.httpClient = httpClient;
            this.logger = logger;
            this.errorHandler = errorHandler;

            var baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Uchat");
            this.downloadPath = Path.Combine(baseFolder, "Downloads");
            this.cachePath = Path.Combine(baseFolder, "Cache");

            Directory.CreateDirectory(this.downloadPath);
            Directory.CreateDirectory(this.cachePath);
        }

        /// <summary>
        /// Shows a file picker dialog to select files for attachment.
        /// </summary>
        /// <param name="multiSelect">Whether to allow multiple file selection.</param>
        /// <returns>A task containing the list of selected file paths.</returns>
        public Task<List<string>> PickFilesAsync(bool multiSelect = true)
        {
            return Task.Run(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Multiselect = multiSelect,
                    Title = "Select files to attach",
                    Filter = "All Files (*.*)|*.*|" +
                             "Images (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|" +
                             "Documents (*.pdf;*.doc;*.docx;*.txt)|*.pdf;*.doc;*.docx;*.txt|" +
                             "Archives (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|" +
                             "Code Files (*.cs;*.js;*.py;*.java)|*.cs;*.js;*.py;*.java",
                };

                if (dialog.ShowDialog() == true)
                {
                    return dialog.FileNames.ToList();
                }

                return new List<string>();
            });
        }

        /// <summary>
        /// Uploads a file attachment to the server.
        /// </summary>
        /// <param name="filePath">The local file path to upload.</param>
        /// <param name="messageId">The message ID to associate with the attachment.</param>
        /// <param name="contentStream">Optional content stream to upload instead of reading from file.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task containing the uploaded attachment DTO.</returns>
        public async Task<MessageAttachmentDto> UploadAttachmentAsync(
            string filePath,
            string messageId,
            Stream? contentStream = null,
            CancellationToken cancellationToken = default)
        {
            FileStream? fileStream = null;
            try
            {
                var validation = this.ValidateFile(filePath);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(validation.ErrorMessage);
                }

                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name;

                this.logger.Debug("Uploading file: {FileName}", fileName);

                using var content = new MultipartFormDataContent();
                Stream streamToUpload;

                if (contentStream != null)
                {
                    if (contentStream.CanSeek)
                    {
                        contentStream.Position = 0;
                    }

                    streamToUpload = contentStream;
                }
                else
                {
                    fileStream = File.OpenRead(filePath);
                    streamToUpload = fileStream;
                }

                using var streamContent = new StreamContent(streamToUpload);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(FileHelper.GetContentType(fileName));
                content.Add(streamContent, "file", fileName);
                content.Add(new StringContent(messageId), "messageId");

                var response = await this.httpClient.PostAsync("/api/attachments/upload", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var attachment = JsonSerializer.Deserialize<MessageAttachmentDto>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Failed to deserialize attachment");

                // Сохраняем оригинал в кэш сразу же (копируем исходный файл)
                await this.CacheLocalFileAsync(filePath, attachment);

                return attachment;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to upload attachment: {FilePath}", filePath);
                this.errorHandler.ShowError($"Failed to upload file: {ex.Message}");
                throw;
            }
            finally
            {
                fileStream?.Dispose();
            }
        }

        /// <summary>
        /// Downloads an attachment to the local downloads folder.
        /// </summary>
        /// <param name="attachment">The attachment DTO to download.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task containing the local file path where the attachment was saved.</returns>
        public async Task<string> DownloadAttachmentAsync(
            MessageAttachmentDto attachment,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var localPath = this.GetUniqueFilePath(this.downloadPath, attachment.FileName);

                // 1. Пробуем взять из кэша
                var cachedPath = this.GetCachedFilePath(attachment);
                if (File.Exists(cachedPath))
                {
                    File.Copy(cachedPath, localPath, true);
                    this.logger.Information("File restored from cache: {Path}", localPath);
                    return localPath;
                }

                // 2. Если нет в кэше - качаем
                var response = await this.httpClient.GetAsync(attachment.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                using (var fileStream = File.Create(localPath))
                using (var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    await networkStream.CopyToAsync(fileStream, cancellationToken);
                }

                // 3. Сохраняем в кэш на будущее
                _ = this.CacheLocalFileAsync(localPath, attachment);

                this.logger.Information("File downloaded: {Path}", localPath);
                return localPath;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to download attachment: {Id}", attachment.Id);
                this.errorHandler.ShowError($"Failed to download: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Prompts the user to choose a save location and saves the attachment.
        /// </summary>
        /// <param name="attachment">The attachment DTO to save.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task containing the chosen save path, or null if cancelled.</returns>
        public async Task<string?> SaveAttachmentAsAsync(
            MessageAttachmentDto attachment,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    FileName = attachment.FileName,
                    Title = "Save File As",
                    Filter = $"Current Type (*{Path.GetExtension(attachment.FileName)})|*{Path.GetExtension(attachment.FileName)}|All files (*.*)|*.*",
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return null;
                }

                var destinationPath = saveDialog.FileName;
                var cachedPath = this.GetCachedFilePath(attachment);

                if (File.Exists(cachedPath))
                {
                    File.Copy(cachedPath, destinationPath, true);
                }
                else
                {
                    var response = await this.httpClient.GetAsync(
                        attachment.DownloadUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    response.EnsureSuccessStatusCode();

                    using var fs = File.Create(destinationPath);
                    await response.Content.CopyToAsync(fs, cancellationToken);

                    _ = this.CacheLocalFileAsync(destinationPath, attachment);
                }

                this.errorHandler.ShowInfo($"Saved to: {destinationPath}");
                return destinationPath;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to save attachment");
                this.errorHandler.ShowError("Failed to save file.");
                throw;
            }
        }

        /// <summary>
        /// Opens an attachment using the system's default application.
        /// </summary>
        /// <param name="attachment">The attachment DTO to open.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task OpenAttachmentAsync(MessageAttachmentDto attachment, CancellationToken cancellationToken = default)
        {
            try
            {
                // Проверяем, скачан ли файл явно пользователем
                var userDownloadPath = Path.Combine(this.downloadPath, attachment.FileName);
                string pathToOpen;

                if (File.Exists(userDownloadPath))
                {
                    pathToOpen = userDownloadPath;
                }
                else
                {
                    // Если нет - скачиваем (этот метод теперь сам проверит кэш)
                    pathToOpen = await this.DownloadAttachmentAsync(attachment, cancellationToken);
                }

                new Process { StartInfo = new ProcessStartInfo(pathToOpen) { UseShellExecute = true } }.Start();
            }
            catch (Exception ex)
            {
                this.errorHandler.ShowError($"Could not open file: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads the full image as a stream using caching.
        /// </summary>
        /// <param name="attachment">The attachment DTO representing the image.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task containing the image stream.</returns>
        public async Task<Stream> DownloadImageStreamAsync(MessageAttachmentDto attachment, CancellationToken cancellationToken = default)
        {
            // Скачиваем полную картинку через механизм кэширования в память
            return await this.GetCachedStreamAsync(attachment, attachment.DownloadUrl, cancellationToken, isThumbnail: false);
        }

        /// <summary>
        /// Downloads the thumbnail image as a stream using caching.
        /// </summary>
        /// <param name="attachment">The attachment DTO representing the image.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task containing the thumbnail stream.</returns>
        public async Task<Stream> DownloadThumbnailStreamAsync(MessageAttachmentDto attachment, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(attachment.ThumbnailUrl))
            {
                throw new InvalidOperationException("No thumbnail URL");
            }

            // Скачиваем миниатюру через механизм кэширования в память
            return await this.GetCachedStreamAsync(attachment, attachment.ThumbnailUrl, cancellationToken, isThumbnail: true);
        }

        /// <summary>
        /// Validates a file for upload based on type and size restrictions.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <returns>A tuple containing validation result and error message if invalid.</returns>
        public (bool IsValid, string ErrorMessage) ValidateFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return (false, "File not found.");
            }

            var fi = new FileInfo(filePath);
            if (!FileHelper.IsAllowedFileType(fi.Name))
            {
                return (false, "File type not allowed.");
            }

            var type = FileHelper.GetAttachmentType(fi.Name);
            if (!FileHelper.IsValidFileSize(fi.Length, type))
            {
                return (false, "File too large.");
            }

            return (true, string.Empty);
        }

        // --- PRIVATE HELPERS ---

        /// <summary>
        /// Ключевой метод: получает файл (из кэша или сети), загружает его в MemoryStream и возвращает.
        /// </summary>
        private async Task<Stream> GetCachedStreamAsync(MessageAttachmentDto attachment, string url, CancellationToken ct, bool isThumbnail)
        {
            var cachedPath = this.GetCachedFilePath(attachment, isThumbnail);

            // 1. Если есть в кэше — читаем в память и отдаем
            if (File.Exists(cachedPath))
            {
                try
                {
                    var ms = new MemoryStream();
                    using (var fs = new FileStream(cachedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await fs.CopyToAsync(ms, ct);
                    }

                    ms.Position = 0; // Сбрасываем позицию, это критично для WPF!
                    return ms;
                }
                catch (Exception ex)
                {
                    this.logger.Warning("Cache read failed, redownloading. Error: {Msg}", ex.Message);

                    // Если не удалось прочитать кэш, пробуем скачать заново ниже
                }
            }

            // 2. Скачиваем с сервера
            var response = await this.httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            response.EnsureSuccessStatusCode();

            // Сначала качаем во временный файл, чтобы не повредить кэш при обрыве
            var tempPath = cachedPath + ".tmp";
            using (var fs = File.Create(tempPath))
            using (var ns = await response.Content.ReadAsStreamAsync(ct))
            {
                await ns.CopyToAsync(fs, ct);
            }

            // Перемещаем в кэш
            if (File.Exists(cachedPath))
            {
                File.Delete(cachedPath);
            }

            File.Move(tempPath, cachedPath);

            // 3. Читаем из свежего кэша в память
            var memoryStream = new MemoryStream();
            using (var fs = new FileStream(cachedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await fs.CopyToAsync(memoryStream, ct);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        private async Task CacheLocalFileAsync(string sourcePath, MessageAttachmentDto attachment)
        {
            try
            {
                var cachedPath = this.GetCachedFilePath(attachment, isThumbnail: false);

                // Ждем немного, чтобы убедиться, что файл освобожден
                await Task.Delay(100);
                if (!File.Exists(cachedPath))
                {
                    File.Copy(sourcePath, cachedPath, true);
                }
            }
            catch (Exception ex)
            {
                this.logger.Warning("Failed to background cache file: {Msg}", ex.Message);
            }
        }

        private string GetCachedFilePath(MessageAttachmentDto attachment, bool isThumbnail = false)
        {
            var ext = Path.GetExtension(attachment.FileName);
            var name = isThumbnail ? $"thumb_{attachment.Id}{ext}" : $"{attachment.Id}{ext}";
            return Path.Combine(this.cachePath, name);
        }

        private string GetUniqueFilePath(string folder, string fileName)
        {
            var path = Path.Combine(folder, fileName);
            int i = 1;
            while (File.Exists(path))
            {
                var name = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                path = Path.Combine(folder, $"{name} ({i++}){ext}");
            }

            return path;
        }
    }
}
