using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System;
using YMM4Browser.View;

namespace YMM4Browser.ViewModel
{
    public class BrowserViewModel : INotifyPropertyChanged
    {
        private string _currentUrl = "";
        private string _addressBarUrl = "";
        private bool _isNavigating = false;
        private bool _canGoBack = false;
        private bool _canGoForward = false;
        private string _pageTitle = "";
        private string _statusText = "準備完了";
        private string _securityStatus = "";
        private WebView2? _webView;

        public string CurrentUrl
        {
            get => _currentUrl;
            set { _currentUrl = value; OnPropertyChanged(); }
        }

        public string AddressBarUrl
        {
            get => _addressBarUrl;
            set { _addressBarUrl = value; OnPropertyChanged(); }
        }

        public bool IsNavigating
        {
            get => _isNavigating;
            set { _isNavigating = value; OnPropertyChanged(); }
        }

        public bool CanGoBack
        {
            get => _canGoBack;
            set { _canGoBack = value; OnPropertyChanged(); }
        }

        public bool CanGoForward
        {
            get => _canGoForward;
            set { _canGoForward = value; OnPropertyChanged(); }
        }

        public string PageTitle
        {
            get => _pageTitle;
            set { _pageTitle = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string SecurityStatus
        {
            get => _securityStatus;
            set { _securityStatus = value; OnPropertyChanged(); }
        }

        public ObservableCollection<BookmarkItem> Bookmarks { get; private set; }
        public ObservableCollection<BookmarkGroup> BookmarkGroups { get; private set; }
        public ObservableCollection<HistoryItem> History { get; private set; }

        public ObservableCollection<BookmarkItem> TopLevelBookmarks { get; } = new();
        public ObservableCollection<BookmarkGroup> OtherBookmarkGroups { get; } = new();

        public ICommand GoBackCommand { get; }
        public ICommand GoForwardCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand NavigateCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand AddBookmarkCommand { get; }
        public ICommand EditBookmarkCommand { get; }
        public ICommand NavigateToBookmarkCommand { get; }
        public ICommand RemoveBookmarkCommand { get; }
        public ICommand TakeScreenshotCommand { get; }
        public ICommand NavigateToHistoryCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand ViewSourceCommand { get; }
        public ICommand AddBookmarkGroupCommand { get; }
        public ICommand RemoveBookmarkGroupCommand { get; }

        public BrowserViewModel()
        {
            GoBackCommand = new RelayCommand(_ => GoBack(), _ => CanGoBack);
            GoForwardCommand = new RelayCommand(_ => GoForward(), _ => CanGoForward);
            RefreshCommand = new RelayCommand(_ => Refresh());
            StopCommand = new RelayCommand(_ => Stop(), _ => IsNavigating);
            NavigateCommand = new RelayCommand(url => Navigate(url?.ToString() ?? AddressBarUrl));
            HomeCommand = new RelayCommand(_ => Navigate(BrowserSettings.Default.HomeUrl));
            AddBookmarkCommand = new RelayCommand(_ => AddBookmark());
            EditBookmarkCommand = new RelayCommand(bookmark => EditBookmark(bookmark));
            NavigateToBookmarkCommand = new RelayCommand(bookmark => NavigateToBookmark(bookmark));
            RemoveBookmarkCommand = new RelayCommand(bookmark => RemoveBookmark(bookmark));
            TakeScreenshotCommand = new RelayCommand(_ => TakeScreenshot());
            NavigateToHistoryCommand = new RelayCommand(historyItem => NavigateToHistory(historyItem as HistoryItem));
            ClearHistoryCommand = new RelayCommand(_ => ClearHistory());
            ViewSourceCommand = new RelayCommand(_ => ViewSource());
            AddBookmarkGroupCommand = new RelayCommand(_ => AddBookmarkGroup());
            RemoveBookmarkGroupCommand = new RelayCommand(group => RemoveBookmarkGroup(group as BookmarkGroup));

            Bookmarks = new ObservableCollection<BookmarkItem>();
            BookmarkGroups = new ObservableCollection<BookmarkGroup>();
            History = new ObservableCollection<HistoryItem>();

            try
            {
                Bookmarks = BrowserSettings.Default.Bookmarks;
                BookmarkGroups = BrowserSettings.Default.BookmarkGroups;
                History = BrowserSettings.Default.History;

                AddressBarUrl = BrowserSettings.Default.HomeUrl;

                BrowserSettings.Default.Bookmarks.CollectionChanged += OnBookmarksCollectionChanged;
                BrowserSettings.Default.BookmarkGroups.CollectionChanged += OnBookmarkGroupsCollectionChanged;
                BrowserSettings.Default.History.CollectionChanged += OnHistoryCollectionChanged;
                BrowserSettings.Default.PropertyChanged += OnSettingsPropertyChanged;

                UpdateBookmarkBarView();
            }
            catch (Exception ex)
            {
                StatusText = "ViewModelの初期化に失敗しました。";
                MessageBox.Show(
                    $"YMM4BrowserのViewModel初期化中にエラーが発生しました。\n\nエラー: {ex.Message}",
                    "YMM4Browser 内部エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                }
                UpdateNavigationButtons();
                UpdatePageTitle();
            };
        }

        private void UpdateBookmarkBarView()
        {
            TopLevelBookmarks.Clear();
            OtherBookmarkGroups.Clear();

            var allBookmarks = Bookmarks.OrderBy(b => b.Order).ToList();
            var allGroups = BookmarkGroups.OrderBy(g => g.Order).ToList();

            var topLevelGroup = allGroups.FirstOrDefault();
            if (topLevelGroup != null)
            {
                foreach (var bookmark in allBookmarks.Where(b => b.GroupId == topLevelGroup.Id))
                {
                    TopLevelBookmarks.Add(bookmark);
                }

                foreach (var group in allGroups.Skip(1))
                {
                    group.Bookmarks.Clear();
                    foreach (var bookmark in allBookmarks.Where(b => b.GroupId == group.Id))
                    {
                        group.Bookmarks.Add(bookmark);
                    }
                    OtherBookmarkGroups.Add(group);
                }
            }
            else
            {
                foreach (var bookmark in allBookmarks)
                {
                    TopLevelBookmarks.Add(bookmark);
                }
            }
        }

        private void OnBookmarksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Bookmarks));
            UpdateBookmarkBarView();
        }

        private void OnBookmarkGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(BookmarkGroups));
            UpdateBookmarkBarView();
        }

        private void OnHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(History));
        }

        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BrowserSettings.Bookmarks) || e.PropertyName == nameof(BrowserSettings.BookmarkGroups))
            {
                OnPropertyChanged(e.PropertyName);
                UpdateBookmarkBarView();
            }
            else if (e.PropertyName == nameof(BrowserSettings.History))
            {
                OnPropertyChanged(nameof(History));
            }
        }

        private void CoreWebView2_HistoryChanged(object? sender, object e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateNavigationButtons();
            });
        }

        public void SetStatusText(string text)
        {
            StatusText = text;
        }

        public void SetSecurityStatus(string status)
        {
            SecurityStatus = status;
        }

        private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            IsNavigating = true;
            AddressBarUrl = e.Uri;
            StatusText = $"読み込み中: {e.Uri}";
            SecurityStatus = GetSecurityStatus(e.Uri);
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            IsNavigating = false;
            UpdateNavigationButtons();
            UpdatePageTitle();

            if (e.IsSuccess && _webView?.CoreWebView2 != null)
            {
                StatusText = "完了";
                var title = !string.IsNullOrEmpty(_webView.CoreWebView2.DocumentTitle)
                            ? _webView.CoreWebView2.DocumentTitle
                            : _webView.CoreWebView2.Source;
                BrowserSettings.Default.AddToHistory(_webView.CoreWebView2.Source, title);
            }
            else
            {
                StatusText = $"読み込みエラー: {e.WebErrorStatus}";
            }
        }

        private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (_webView?.Source != null)
            {
                CurrentUrl = _webView.Source.ToString();
                AddressBarUrl = CurrentUrl;
                UpdateNavigationButtons();
                SecurityStatus = GetSecurityStatus(CurrentUrl);
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

        private void GoBack() => _webView?.CoreWebView2?.GoBack();
        private void GoForward() => _webView?.CoreWebView2?.GoForward();
        private void Refresh() => _webView?.CoreWebView2?.Reload();
        private void Stop() => _webView?.CoreWebView2?.Stop();

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
            catch (Exception ex)
            {
                StatusText = $"ナビゲーションエラー: {ex.Message}";
            }
        }

        private void AddBookmark()
        {
            if (!string.IsNullOrEmpty(CurrentUrl))
            {
                var existingBookmark = Bookmarks.FirstOrDefault(b => b.Url == CurrentUrl);
                if (existingBookmark != null)
                {
                    StatusText = "このページは既にブックマークされています";
                    return;
                }

                var title = !string.IsNullOrEmpty(PageTitle) ? PageTitle : CurrentUrl;
                var defaultGroup = BookmarkGroups.FirstOrDefault();

                if (defaultGroup == null)
                {
                    defaultGroup = new BookmarkGroup { Name = "デフォルト" };
                    BookmarkGroups.Add(defaultGroup);
                }

                var bookmark = new BookmarkItem
                {
                    Name = title,
                    Url = CurrentUrl,
                    GroupId = defaultGroup.Id,
                    Order = Bookmarks.Count(b => b.GroupId == defaultGroup.Id)
                };

                Bookmarks.Add(bookmark);
                StatusText = "ブックマークに追加されました";
            }
        }

        private void EditBookmark(object? bookmark)
        {
            if (bookmark is not BookmarkItem bookmarkItem) return;

            try
            {
                var inputDialog = new BookmarkInputDialog(bookmarkItem)
                {
                    Owner = Application.Current.MainWindow
                };

                if (inputDialog.ShowDialog() == true &&
                    !string.IsNullOrWhiteSpace(inputDialog.BookmarkName) &&
                    !string.IsNullOrWhiteSpace(inputDialog.BookmarkUrl))
                {
                    var url = inputDialog.BookmarkUrl;
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        url = "https://" + url;
                    }

                    bookmarkItem.Name = inputDialog.BookmarkName.Trim();
                    bookmarkItem.Url = url.Trim();

                    if (inputDialog.SelectedGroup != null && bookmarkItem.GroupId != inputDialog.SelectedGroup.Id)
                    {
                        var oldGroupId = bookmarkItem.GroupId;
                        var newGroupId = inputDialog.SelectedGroup.Id;

                        bookmarkItem.GroupId = newGroupId;

                        var oldGroupBookmarks = Bookmarks.Where(b => b.GroupId == oldGroupId).OrderBy(b => b.Order).ToList();
                        for (int i = 0; i < oldGroupBookmarks.Count; i++)
                        {
                            oldGroupBookmarks[i].Order = i;
                        }

                        bookmarkItem.Order = Bookmarks.Count(b => b.GroupId == newGroupId);
                    }

                    StatusText = "ブックマークが更新されました";
                    UpdateBookmarkBarView();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"ブックマーク編集エラー: {ex.Message}";
            }
        }

        private void NavigateToBookmark(object? bookmarkObj)
        {
            if (bookmarkObj is BookmarkItem bookmark)
            {
                Navigate(bookmark.Url);
            }
        }

        private void RemoveBookmark(object? bookmark)
        {
            if (bookmark is not BookmarkItem bookmarkItem) return;

            try
            {
                var result = MessageBox.Show(
                    $"ブックマーク「{bookmarkItem.Name}」を削除しますか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (Bookmarks.Contains(bookmarkItem))
                    {
                        var groupId = bookmarkItem.GroupId;
                        Bookmarks.Remove(bookmarkItem);

                        var groupBookmarks = Bookmarks.Where(b => b.GroupId == groupId).OrderBy(b => b.Order).ToList();
                        for (int i = 0; i < groupBookmarks.Count; i++)
                        {
                            groupBookmarks[i].Order = i;
                        }

                        StatusText = "ブックマークが削除されました";
                        UpdateBookmarkBarView();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"ブックマーク削除エラー: {ex.Message}";
            }
        }

        private void NavigateToHistory(HistoryItem? historyItem)
        {
            if (historyItem != null)
            {
                Navigate(historyItem.Url);
            }
        }

        private void ClearHistory()
        {
            var result = MessageBox.Show(
                "履歴をすべて削除しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                History.Clear();
                StatusText = "履歴がクリアされました";
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
                    StatusText = "スクリーンショットが保存されました";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"スクリーンショットの保存に失敗しました: {ex.Message}");
                StatusText = "スクリーンショットの保存に失敗";
            }
        }

        private async void ViewSource()
        {
            if (_webView?.CoreWebView2 == null) return;
            try
            {
                var source = await _webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");

                var sourceWindow = new Window
                {
                    Title = "ページソース",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow
                };

                var textBox = new TextBox
                {
                    Text = source.Trim('"').Replace("\\n", "\n").Replace("\\\"", "\""),
                    IsReadOnly = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new FontFamily("Consolas"),
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

        private void AddBookmarkGroup()
        {
            var inputDialog = new GroupInputDialog()
            {
                Owner = Application.Current.MainWindow
            };

            if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.GroupName))
            {
                var group = new BookmarkGroup
                {
                    Name = inputDialog.GroupName.Trim(),
                    Order = BookmarkGroups.Count
                };

                BookmarkGroups.Add(group);
                StatusText = "ブックマークグループが追加されました";
            }
        }

        private void RemoveBookmarkGroup(BookmarkGroup? group)
        {
            if (group == null) return;

            var bookmarksInGroup = Bookmarks.Where(b => b.GroupId == group.Id).ToList();

            var message = bookmarksInGroup.Any()
                ? $"グループ「{group.Name}」とその中のブックマーク{bookmarksInGroup.Count}個を削除しますか？"
                : $"グループ「{group.Name}」を削除しますか？";

            var result = MessageBox.Show(
                message,
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var bookmark in bookmarksInGroup)
                {
                    Bookmarks.Remove(bookmark);
                }
                BookmarkGroups.Remove(group);
                StatusText = "ブックマークグループが削除されました";
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

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}