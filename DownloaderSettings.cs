using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using YukkuriMovieMaker.Plugin;

namespace YMM4Browser.ViewModel
{
    public class DownloaderSettings : SettingsBase<DownloaderSettings>
    {
        public override string Name => "Downloader Settings";
        public override SettingsCategory Category => SettingsCategory.None;
        public override bool HasSettingView => false;
        public override object? SettingView => null;

        private static readonly string FilePath;

        public bool AgreementAcknowledged { get; set; } = false;
        public string ImageSavePath { get; set; } = "";
        public string VideoSavePath { get; set; } = "";
        public string SelectedFormat { get; set; } = "mp4";
        public string SelectedResolution { get; set; } = "最高";
        public string SelectedFramerate { get; set; } = "最高";
        public string SelectedAudioQuality { get; set; } = "最高 (0)";
        public bool EmbedSubtitles { get; set; } = false;
        public bool EmbedThumbnail { get; set; } = false;

        static DownloaderSettings()
        {
            string? assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            FilePath = string.IsNullOrEmpty(assemblyLocation)
                ? "downloader-config.json"
                : Path.Combine(assemblyLocation, "downloader-config.json");
        }

        public static void InitializeSettings()
        {
            Default.Initialize();
        }

        public override void Initialize()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    var json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<DownloaderSettings>(json);
                    if (loaded != null)
                    {
                        AgreementAcknowledged = loaded.AgreementAcknowledged;
                        ImageSavePath = loaded.ImageSavePath;
                        VideoSavePath = loaded.VideoSavePath;
                        SelectedFormat = loaded.SelectedFormat;
                        SelectedResolution = loaded.SelectedResolution;
                        SelectedFramerate = loaded.SelectedFramerate;
                        SelectedAudioQuality = loaded.SelectedAudioQuality;
                        EmbedSubtitles = loaded.EmbedSubtitles;
                        EmbedThumbnail = loaded.EmbedThumbnail;
                    }
                }
                catch { CreateDefault(); }
            }
            else
            {
                CreateDefault();
            }

            if (string.IsNullOrEmpty(ImageSavePath))
                ImageSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "YMM4BrowserDownloads");
            if (string.IsNullOrEmpty(VideoSavePath))
                VideoSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "YMM4BrowserDownloads");
        }

        private void CreateDefault()
        {
            ImageSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "YMM4BrowserDownloads");
            VideoSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "YMM4BrowserDownloads");
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}