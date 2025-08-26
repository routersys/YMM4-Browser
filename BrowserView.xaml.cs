using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
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
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        await InitializeWebView();
        UpdateWindowTitle();
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
                webView.CoreWebView2.Settings.IsScriptEnabled = true;

                webView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;

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
                "/ads/",
                "/advertisement/",
                "/adsystem/",
                ".ads.",
                "_ads_"
            };

            if (adPatterns.Any(pattern => uri.Contains(pattern)))
            {
                e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 204, "No Content", "");
            }
        }
    }
}