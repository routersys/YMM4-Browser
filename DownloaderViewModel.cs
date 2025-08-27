using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using YMM4Browser.View;

namespace YMM4Browser.ViewModel
{
    public class DownloaderViewModel : INotifyPropertyChanged
    {
        private readonly WebView2 _webView;
        private string _currentUrl;
        private bool _isBusy;
        private string _statusText = "準備完了";
        private string _imageSavePath = "";
        private string _videoSavePath = "";
        private string _videoUrl;
        private bool _selectAllImages;
        private bool _isAudioOnly = false;
        private string _selectedFormat = "mp4";

        public ObservableCollection<ImageItem> ImageItems { get; } = new ObservableCollection<ImageItem>();

        public string CurrentUrl { get => _currentUrl; set => SetField(ref _currentUrl, value); }
        public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }
        public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }
        public string ImageSavePath { get => _imageSavePath; set => SetField(ref _imageSavePath, value); }
        public string VideoSavePath { get => _videoSavePath; set => SetField(ref _videoSavePath, value); }
        public string VideoUrl { get => _videoUrl; set => SetField(ref _videoUrl, value); }
        public bool IsAudioOnly { get => _isAudioOnly; set => SetField(ref _isAudioOnly, value); }

        public bool SelectAllImages
        {
            get => _selectAllImages;
            set
            {
                if (SetField(ref _selectAllImages, value))
                {
                    foreach (var item in ImageItems)
                    {
                        item.IsSelected = value;
                    }
                }
            }
        }

        public ICommand LoadImageUrlsCommand { get; }
        public ICommand SelectImageSavePathCommand { get; }
        public ICommand DownloadImagesCommand { get; }
        public ICommand SelectVideoSavePathCommand { get; }
        public ICommand DownloadVideoCommand { get; }

        public List<string> Formats { get; } = new List<string> { "mp4", "webm", "mkv", "m4a (音声)", "mp3 (音声)", "opus (音声)", "wav (音声)" };
        public string SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                if (SetField(ref _selectedFormat, value))
                {
                    IsAudioOnly = _selectedFormat.Contains("(音声)");
                }
            }
        }

        public List<string> Resolutions { get; } = new List<string> { "最高", "1440p", "1080p", "720p", "480p", "360p", "240p" };
        public string SelectedResolution { get; set; } = "最高";

        public List<string> Framerates { get; } = new List<string> { "最高", "60", "30" };
        public string SelectedFramerate { get; set; } = "最高";

        public List<string> AudioQualities { get; } = new List<string> { "最高 (0)", "高 (2)", "中 (5)", "低 (7)", "最低 (9)" };
        public string SelectedAudioQuality { get; set; } = "最高 (0)";

        public bool EmbedSubtitles { get; set; } = false;
        public bool EmbedThumbnail { get; set; } = false;
        public string CustomArguments { get; set; } = "";

        public DownloaderViewModel(WebView2 webView, string currentUrl)
        {
            _webView = webView;
            _currentUrl = currentUrl;
            _videoUrl = currentUrl;

            LoadSettings();

            LoadImageUrlsCommand = new RelayCommand(async _ => await LoadImageUrlsAsync(), _ => !IsBusy);
            SelectImageSavePathCommand = new RelayCommand(_ => SelectSavePath(isForImage: true));
            DownloadImagesCommand = new RelayCommand(async _ => await DownloadImagesAsync(), _ => ImageItems.Any(i => i.IsSelected) && !IsBusy);
            SelectVideoSavePathCommand = new RelayCommand(_ => SelectSavePath(isForImage: false));
            DownloadVideoCommand = new RelayCommand(async _ => await DownloadVideoAsync(), _ => !string.IsNullOrWhiteSpace(VideoUrl) && !IsBusy);
        }

        private void LoadSettings()
        {
            var settings = DownloaderSettings.Default;
            ImageSavePath = settings.ImageSavePath;
            VideoSavePath = settings.VideoSavePath;
            SelectedFormat = settings.SelectedFormat;
            SelectedResolution = settings.SelectedResolution;
            SelectedFramerate = settings.SelectedFramerate;
            SelectedAudioQuality = settings.SelectedAudioQuality;
            EmbedSubtitles = settings.EmbedSubtitles;
            EmbedThumbnail = settings.EmbedThumbnail;
        }

        public void SaveSettings()
        {
            var settings = DownloaderSettings.Default;
            settings.ImageSavePath = ImageSavePath;
            settings.VideoSavePath = VideoSavePath;
            settings.SelectedFormat = SelectedFormat;
            settings.SelectedResolution = SelectedResolution;
            settings.SelectedFramerate = SelectedFramerate;
            settings.SelectedAudioQuality = SelectedAudioQuality;
            settings.EmbedSubtitles = EmbedSubtitles;
            settings.EmbedThumbnail = EmbedThumbnail;
            settings.Save();
        }

        private async Task LoadImageUrlsAsync()
        {
            if (_webView?.CoreWebView2 == null) return;
            IsBusy = true;
            StatusText = "高度な画像スキャンを実行中...";
            ImageItems.Clear();

            try
            {
                var script = @"
(function() {
    const urls = new Set();
    function addUrl(url) {
        if (url && typeof url === 'string') {
            let cleanedUrl = url.trim().replace(/^url\(['""]?/, '').replace(/['""]?\)$/, '');
            if (cleanedUrl) {
                try {
                    if (cleanedUrl.startsWith('data:image')) {
                        urls.add(cleanedUrl);
                    } else {
                        urls.add(new URL(cleanedUrl, document.baseURI).href);
                    }
                } catch (e) { }
            }
        }
    }

    document.querySelectorAll('img').forEach(img => {
        if (img.src) addUrl(img.src);
        if (img.srcset) img.srcset.split(',').forEach(part => addUrl(part.trim().split(' ')[0]));
    });

    document.querySelectorAll('picture source').forEach(source => {
        if (source.srcset) source.srcset.split(',').forEach(part => addUrl(part.trim().split(' ')[0]));
    });

    document.querySelectorAll('*').forEach(el => {
        const style = window.getComputedStyle(el, null);
        const bgImage = style.getPropertyValue('background-image');
        if (bgImage && bgImage !== 'none') {
            const matches = bgImage.match(/url\(['""]?.*?['""]?\)/g);
            if (matches) matches.forEach(match => addUrl(match));
        }
    });

    document.querySelectorAll('svg image').forEach(image => {
        if (image.href && image.href.baseVal) addUrl(image.href.baseVal);
    });
    
    document.querySelectorAll('video').forEach(video => {
       if(video.poster) addUrl(video.poster);
    });
    
    document.querySelectorAll('[data-src]').forEach(el => addUrl(el.dataset.src));
    document.querySelectorAll('[data-lazy]').forEach(el => addUrl(el.dataset.lazy));
    document.querySelectorAll('[data-lazy-src]').forEach(el => addUrl(el.dataset.lazySrc));
    document.querySelectorAll('[data-bg]').forEach(el => addUrl(el.dataset.bg));

    return Array.from(urls);
})();";
                var imageUrlsJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);

                if (string.IsNullOrEmpty(imageUrlsJson) || imageUrlsJson == "null")
                {
                    StatusText = "ページ内に画像が見つかりません。";
                    return;
                }

                var imageUrls = JsonSerializer.Deserialize<List<string>>(imageUrlsJson);
                if (imageUrls != null)
                {
                    foreach (var url in imageUrls.Distinct())
                    {
                        ImageItems.Add(new ImageItem { Url = url });
                    }
                }
                StatusText = $"{ImageItems.Count}件の画像が見つかりました。";
            }
            catch (Exception ex)
            {
                StatusText = "画像URLの読み込みに失敗しました。";
                var dialogVM = new GenericDialogViewModel("エラー", $"画像URLの読み込み中にエラーが発生しました:\n\n{ex.Message}");
                var dialog = new GenericDialog { DataContext = dialogVM };
                dialog.ShowDialog();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SelectSavePath(bool isForImage)
        {
            var dialog = new SaveFileDialog
            {
                Title = "保存先フォルダを選択してください",
                Filter = "Folder|*.this.is.a.folder",
                FileName = "フォルダを選択"
            };

            if (dialog.ShowDialog() == true)
            {
                string? folderPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (isForImage)
                    {
                        ImageSavePath = folderPath;
                    }
                    else
                    {
                        VideoSavePath = folderPath;
                    }
                }
            }
        }

        private async Task DownloadImagesAsync()
        {
            var selectedImages = ImageItems.Where(i => i.IsSelected).ToList();
            if (!selectedImages.Any())
            {
                MessageBox.Show("ダウンロードする画像を選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!Directory.Exists(ImageSavePath))
            {
                Directory.CreateDirectory(ImageSavePath);
            }

            IsBusy = true;
            int successCount = 0;
            int totalCount = selectedImages.Count;
            var errorMessages = new List<string>();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                for (int i = 0; i < totalCount; i++)
                {
                    var item = selectedImages[i];
                    StatusText = $"画像をダウンロード中... ({i + 1}/{totalCount})";
                    try
                    {
                        if (item.Url.StartsWith("data:image"))
                        {
                            var parts = item.Url.Split(new[] { ';', ',' }, 3);
                            if (parts.Length == 3 && parts[1] == "base64")
                            {
                                var mimeType = parts[0].Split(':')[1];
                                var base64Data = parts[2];
                                var imageData = Convert.FromBase64String(base64Data);
                                var extension = mimeType.Split('/')[1] switch
                                {
                                    "jpeg" => ".jpg",
                                    "png" => ".png",
                                    "gif" => ".gif",
                                    "webp" => ".webp",
                                    "svg+xml" => ".svg",
                                    _ => ".dat"
                                };
                                var fileName = $"{Guid.NewGuid()}{extension}";
                                var filePath = Path.Combine(ImageSavePath, fileName);
                                await File.WriteAllBytesAsync(filePath, imageData);
                                successCount++;
                            }
                        }
                        else
                        {
                            var uri = new Uri(item.Url);
                            var fileName = Path.GetFileName(uri.LocalPath);
                            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                            {
                                fileName = $"{Guid.NewGuid()}.jpg";
                            }
                            var filePath = Path.Combine(ImageSavePath, fileName);

                            var imageData = await client.GetByteArrayAsync(uri);
                            await File.WriteAllBytesAsync(filePath, imageData);
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"ダウンロード失敗: {item.Url}\n理由: {ex.Message}");
                    }
                }
            }

            StatusText = $"{successCount}/{totalCount}件の画像をダウンロードしました。";
            IsBusy = false;

            if (errorMessages.Any())
            {
                var dialogVM = new GenericDialogViewModel("ダウンロードエラー", string.Join("\n\n", errorMessages));
                var dialog = new GenericDialog { DataContext = dialogVM };
                dialog.ShowDialog();
            }
            else
            {
                MessageBox.Show($"ダウンロードが完了しました。\n保存先: {ImageSavePath}", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task DownloadVideoAsync()
        {
            string? assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(assemblyLocation))
            {
                MessageBox.Show("プラグインのインストール場所を取得できませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var ytdlpPath = Path.Combine(assemblyLocation, "yt-dlp.exe");
            if (!File.Exists(ytdlpPath))
            {
                MessageBox.Show($"yt-dlp.exeが見つかりません。以下に配置してください。\n{ytdlpPath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(VideoSavePath))
            {
                Directory.CreateDirectory(VideoSavePath);
            }

            IsBusy = true;
            StatusText = "動画のダウンロード準備中...";

            var argsBuilder = new StringBuilder();

            string format = SelectedFormat.Replace(" (音声)", "");
            if (IsAudioOnly)
            {
                argsBuilder.Append($"-x --audio-format {format} ");

                string quality = SelectedAudioQuality.Split(' ')[1].Trim('(', ')');
                argsBuilder.Append($"--audio-quality {quality} ");
            }
            else
            {
                string res = SelectedResolution == "最高" ? "" : $"[height<={SelectedResolution.Replace("p", "")}]";
                string fps = SelectedFramerate == "最高" ? "" : $"[fps<={SelectedFramerate}]";
                argsBuilder.Append($"-f \"bestvideo{res}{fps}[ext={format}]+bestaudio[ext=m4a]/best{res}{fps}[ext={format}]/best\" ");
                argsBuilder.Append($"--merge-output-format {format} ");
            }

            if (EmbedSubtitles)
            {
                argsBuilder.Append("--embed-subs --all-subs ");
            }

            if (EmbedThumbnail)
            {
                argsBuilder.Append("--embed-thumbnail ");
            }

            argsBuilder.Append($"--output \"{Path.Combine(VideoSavePath, "%(title)s.%(ext)s")}\" ");

            if (!string.IsNullOrWhiteSpace(CustomArguments))
            {
                argsBuilder.Append($"{CustomArguments} ");
            }

            argsBuilder.Append($"\"{VideoUrl}\"");

            await Task.Run(() =>
            {
                var errorOutput = new StringBuilder();
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = argsBuilder.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Application.Current.Dispatcher.Invoke(() => StatusText = args.Data);
                    }
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        errorOutput.AppendLine(args.Data);
                        Application.Current.Dispatcher.Invoke(() => StatusText = args.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (process.ExitCode == 0)
                    {
                        StatusText = "ダウンロードが完了しました。";
                        MessageBox.Show($"ダウンロードが完了しました。\n保存先: {VideoSavePath}", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusText = "ダウンロードに失敗しました。";
                        var dialogVM = new GenericDialogViewModel("ダウンロード失敗", errorOutput.ToString());
                        var dialog = new GenericDialog { DataContext = dialogVM };
                        dialog.ShowDialog();
                    }
                    IsBusy = false;
                });
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class ImageItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Url { get; set; } = "";
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}