using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YMM4Browser.View;

namespace YMM4Browser.ViewModel;

public class BrowserViewModel : INotifyPropertyChanged
{
    private string _currentUrl = "";
    private string _addressBarUrl = "";
    private bool _isNavigating = false;
    private bool _canGoBack = false;
    private bool _canGoForward = false;
    private string _pageTitle = "";
    private WebView2? _webView;

    public string CurrentUrl
    {
        get => _currentUrl;
        set
        {
            _currentUrl = value;
            OnPropertyChanged();
        }
    }

    public string AddressBarUrl
    {
        get => _addressBarUrl;
        set
        {
            _addressBarUrl = value;
            OnPropertyChanged();
        }
    }

    public bool IsNavigating
    {
        get => _isNavigating;
        set
        {
            _isNavigating = value;
            OnPropertyChanged();
        }
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        set
        {
            _canGoBack = value;
            OnPropertyChanged();
        }
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        set
        {
            _canGoForward = value;
            OnPropertyChanged();
        }
    }

    public string PageTitle
    {
        get => _pageTitle;
        set
        {
            _pageTitle = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<BookmarkItem> Bookmarks => BrowserSettings.Default.Bookmarks;

    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand HomeCommand { get; }
    public ICommand AddBookmarkCommand { get; }
    public ICommand NavigateToBookmarkCommand { get; }
    public ICommand RemoveBookmarkCommand { get; }
    public ICommand TakeScreenshotCommand { get; }

    public BrowserViewModel()
    {
        GoBackCommand = new RelayCommand(_ => GoBack(), _ => CanGoBack);
        GoForwardCommand = new RelayCommand(_ => GoForward(), _ => CanGoForward);
        RefreshCommand = new RelayCommand(_ => Refresh());
        NavigateCommand = new RelayCommand(url => Navigate(url?.ToString() ?? AddressBarUrl));
        HomeCommand = new RelayCommand(_ => Navigate(BrowserSettings.Default.HomeUrl));
        AddBookmarkCommand = new RelayCommand(_ => AddBookmark());
        NavigateToBookmarkCommand = new RelayCommand(bookmark => NavigateToBookmark(bookmark as BookmarkItem));
        RemoveBookmarkCommand = new RelayCommand(bookmark => RemoveBookmark(bookmark as BookmarkItem));
        TakeScreenshotCommand = new RelayCommand(_ => TakeScreenshot());

        AddressBarUrl = BrowserSettings.Default.HomeUrl;

        BrowserSettings.Default.Bookmarks.CollectionChanged += OnBookmarksCollectionChanged;
        BrowserSettings.Default.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnBookmarksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Bookmarks));
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserSettings.Bookmarks))
        {
            OnPropertyChanged(nameof(Bookmarks));
        }
    }

    public void SetWebView(WebView2 webView)
    {
        _webView = webView;

        webView.NavigationStarting += OnNavigationStarting;
        webView.NavigationCompleted += OnNavigationCompleted;
        webView.SourceChanged += OnSourceChanged;
        webView.CoreWebView2InitializationCompleted += (_, _) =>
        {
            UpdateNavigationButtons();
            UpdatePageTitle();
        };
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        IsNavigating = true;
        AddressBarUrl = e.Uri;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        IsNavigating = false;
        UpdateNavigationButtons();
        UpdatePageTitle();
    }

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (_webView?.Source != null)
        {
            CurrentUrl = _webView.Source.ToString();
            AddressBarUrl = CurrentUrl;
            UpdateNavigationButtons();
        }
    }

    private void GoBack()
    {
        _webView?.CoreWebView2?.GoBack();
    }

    private void GoForward()
    {
        _webView?.CoreWebView2?.GoForward();
    }

    private void Refresh()
    {
        _webView?.CoreWebView2?.Reload();
    }

    private void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            if (url.Contains('.') && !url.Contains(' '))
            {
                url = "https://" + url;
            }
            else
            {
                url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
            }
        }

        try
        {
            _webView?.CoreWebView2?.Navigate(url);
        }
        catch (Exception)
        {
        }
    }

    private void AddBookmark()
    {
        if (!string.IsNullOrEmpty(CurrentUrl) && Bookmarks.All(b => b.Url != CurrentUrl))
        {
            var title = !string.IsNullOrEmpty(PageTitle) ? PageTitle : CurrentUrl;
            var bookmark = new BookmarkItem { Name = title, Url = CurrentUrl };
            Bookmarks.Add(bookmark);
        }
    }

    private void NavigateToBookmark(BookmarkItem? bookmark)
    {
        if (bookmark != null)
        {
            Navigate(bookmark.Url);
        }
    }

    private void RemoveBookmark(BookmarkItem? bookmark)
    {
        if (bookmark is not BookmarkItem bookmarkToRemove) return;

        var result = System.Windows.MessageBox.Show(
            $"ブックマーク「{bookmarkToRemove.Name}」を削除しますか？",
            "確認",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            if (Bookmarks.Contains(bookmarkToRemove))
            {
                Bookmarks.Remove(bookmarkToRemove);
            }
        }
    }

    private async void TakeScreenshot()
    {
        if (_webView?.CoreWebView2 == null) return;
        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG files (*.png)|*.png",
                DefaultExt = "png",
                FileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (saveDialog.ShowDialog() == true)
            {
                using var fileStream = System.IO.File.Create(saveDialog.FileName);
                await _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fileStream);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"スクリーンショットの保存に失敗しました: {ex.Message}");
        }
    }

    private void UpdateNavigationButtons()
    {
        CanGoBack = _webView?.CoreWebView2?.CanGoBack ?? false;
        CanGoForward = _webView?.CoreWebView2?.CanGoForward ?? false;
    }

    private void UpdatePageTitle()
    {
        PageTitle = _webView?.CoreWebView2?.DocumentTitle ?? "";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}