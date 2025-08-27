using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YMM4Browser.ViewModel;

namespace YMM4Browser.View;

public partial class BrowserView : UserControl
{
    private BrowserViewModel? ViewModel => DataContext as BrowserViewModel;

    public BrowserView()
    {
        InitializeComponent();
        DataContext = new BrowserViewModel();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        await InitializeWebView();
        UpdateWindowTitle();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        webView?.Dispose();
    }

    private async Task InitializeWebView()
    {
        try
        {
            await webView.EnsureCoreWebView2Async();

            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                webView.CoreWebView2.Settings.IsWebMessageEnabled = false;
                webView.CoreWebView2.Settings.IsScriptEnabled = BrowserSettings.Default.EnableJavaScript;

                webView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
                webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
                webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                webView.CoreWebView2.SourceChanged += OnSourceChanged;

                if (BrowserSettings.Default.EnablePopupBlock)
                {
                    webView.CoreWebView2.WindowCloseRequested += OnWindowCloseRequested;
                    webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                }

                if (BrowserSettings.Default.EnableAdBlock)
                {
                    webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
                    webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                }

                if (BrowserSettings.Default.EnableExtensions)
                {
                    try
                    {
                        await SetupExtensionsAsync();
                    }
                    catch (Exception ex)
                    {
                        ViewModel?.SetStatusText($"拡張機能の初期化に失敗: {ex.Message}");
                    }
                }

                ViewModel?.SetWebView(webView);

                if (!string.IsNullOrEmpty(BrowserSettings.Default.HomeUrl))
                {
                    webView.CoreWebView2.Navigate(BrowserSettings.Default.HomeUrl);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2の初期化に失敗しました: {ex.Message}\n\nWebView2ランタイムがインストールされているか確認してください。", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SetupExtensionsAsync()
    {
        if (webView.CoreWebView2 == null) return;

        try
        {
            var userDataFolder = webView.CoreWebView2.Environment.UserDataFolder;
            var extensionsFolder = Path.Combine(userDataFolder, "Extensions");

            if (!Directory.Exists(extensionsFolder))
            {
                Directory.CreateDirectory(extensionsFolder);
            }

            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            webView.CoreWebView2.WebResourceRequested += OnWebResourceRequestedForExtensions;

            var userAgent = await webView.CoreWebView2.ExecuteScriptAsync("navigator.userAgent");
            if (!userAgent.Contains("Chrome"))
            {
                webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            }
        }
        catch (Exception ex)
        {
            ViewModel?.SetStatusText($"拡張機能設定エラー: {ex.Message}");
        }
    }

    private void OnWebResourceRequestedForExtensions(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!BrowserSettings.Default.EnableExtensions) return;

        try
        {
            var uri = e.Request.Uri.ToLowerInvariant();

            if (uri.Contains("chrome-extension://") || uri.Contains("extension://"))
            {
                var headers = "Access-Control-Allow-Origin: *\r\n" +
                             "Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS\r\n" +
                             "Access-Control-Allow-Headers: Content-Type, Authorization\r\n";

                if (webView.CoreWebView2 != null)
                {
                    e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        null, 200, "OK", headers);
                }
            }
        }
        catch (Exception ex)
        {
            ViewModel?.SetStatusText($"拡張機能リクエスト処理エラー: {ex.Message}");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        ViewModel?.SetStatusText($"読み込み中: {e.Uri}");
        ViewModel?.SetSecurityStatus(GetSecurityStatus(e.Uri));
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess && webView.CoreWebView2 != null)
        {
            ViewModel?.SetStatusText("完了");
            var title = !string.IsNullOrEmpty(webView.CoreWebView2.DocumentTitle)
                        ? webView.CoreWebView2.DocumentTitle
                        : webView.CoreWebView2.Source;
            BrowserSettings.Default.AddToHistory(webView.CoreWebView2.Source, title);
        }
        else
        {
            ViewModel?.SetStatusText($"読み込みエラー: {e.WebErrorStatus}");
        }
    }

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (webView.CoreWebView2 != null)
        {
            ViewModel?.SetSecurityStatus(GetSecurityStatus(webView.CoreWebView2.Source));
        }
    }

    private string GetSecurityStatus(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";

        if (url.StartsWith("https://"))
            return "🔒 安全";
        else if (url.StartsWith("http://"))
            return "⚠️ 非安全";
        else if (url.StartsWith("file://"))
            return "📁 ローカル";
        else
            return "";
    }

    private void OnDocumentTitleChanged(object? sender, object e)
    {
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var title = "";
                if (webView.CoreWebView2 != null && !string.IsNullOrEmpty(webView.CoreWebView2.DocumentTitle))
                {
                    title = webView.CoreWebView2.DocumentTitle;
                    window.Title = $"{title} - YMM4 ブラウザ";
                }
                else if (ViewModel != null && !string.IsNullOrEmpty(ViewModel.CurrentUrl))
                {
                    window.Title = $"{ViewModel.CurrentUrl} - YMM4 ブラウザ";
                }
                else
                {
                    window.Title = "YMM4 ブラウザ";
                }
            }
        }));
    }

    private void OnWindowCloseRequested(object? sender, object e)
    {
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.NewWindow = webView.CoreWebView2;
        e.Handled = true;
        webView.CoreWebView2.Navigate(e.Uri);
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (BrowserSettings.Default.EnableAdBlock)
        {
            var uri = e.Request.Uri.ToLowerInvariant();
            var adPatterns = new[]
            {
                "doubleclick.net",
                "googleadservices.com",
                "googlesyndication.com",
                "googletagservices.com",
                "google-analytics.com",
                "facebook.com/tr",
                "twitter.com/i/adsct",
                "amazon-adsystem.com",
                "media.net",
                "outbrain.com",
                "taboola.com",
                "adsystem.amazon.co.jp",
                "/ads/",
                "/advertisement/",
                "/adsystem/",
                "/adservice/",
                "/adserver/",
                "/adnxs/",
                ".ads.",
                "_ads_",
                "popads",
                "popup",
                "banner"
            };

            if (adPatterns.Any(pattern => uri.Contains(pattern)))
            {
                if (webView.CoreWebView2 != null)
                {
                    e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        null, 204, "No Content", "");
                }
            }
        }
    }

    public async Task ViewSourceAsync()
    {
        if (webView.CoreWebView2 != null)
        {
            try
            {
                var source = await webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");

                var sourceWindow = new Window
                {
                    Title = "ページソース",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this)
                };

                var textBox = new TextBox
                {
                    Text = source.Trim('"').Replace("\\n", "\n").Replace("\\\"", "\""),
                    IsReadOnly = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.NoWrap
                };

                sourceWindow.Content = textBox;
                sourceWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ソース表示エラー: {ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BookmarkButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.DataContext is BookmarkItem bookmark)
        {
            var contextMenu = new ContextMenu();

            var editMenuItem = new MenuItem
            {
                Header = "編集",
                Command = ViewModel?.EditBookmarkCommand,
                CommandParameter = bookmark
            };

            var deleteMenuItem = new MenuItem
            {
                Header = "削除",
                Command = ViewModel?.RemoveBookmarkCommand,
                CommandParameter = bookmark
            };

            contextMenu.Items.Add(editMenuItem);
            contextMenu.Items.Add(deleteMenuItem);

            contextMenu.PlacementTarget = button;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}