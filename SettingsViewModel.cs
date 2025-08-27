using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YMM4Browser.View;

namespace YMM4Browser.ViewModel
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public ICommand EditBookmarkCommand { get; }
        public ICommand RemoveBookmarkFromMenuCommand { get; }
        public ICommand EditBookmarkGroupCommand { get; }
        public ICommand RemoveBookmarkGroupFromMenuCommand { get; }

        private static readonly HttpClient httpClient = new();
        private static bool isUpdateCheckCompleted = false;
        private static string? updateCheckResult = null;
        private static bool isUpdateAvailable = false;

        private BookmarkGroup? _selectedBookmarkGroup;
        private ObservableCollection<BookmarkItemViewModel> _filteredBookmarks = new();

        public string HomeUrl
        {
            get => BrowserSettings.Default.HomeUrl;
            set { BrowserSettings.Default.HomeUrl = value; OnPropertyChanged(); }
        }

        public bool CompactMode
        {
            get => BrowserSettings.Default.CompactMode;
            set { BrowserSettings.Default.CompactMode = value; OnPropertyChanged(); }
        }

        public int MaxHistoryItems
        {
            get => BrowserSettings.Default.MaxHistoryItems;
            set { BrowserSettings.Default.MaxHistoryItems = value; OnPropertyChanged(); }
        }

        public bool EnableAdBlock
        {
            get => BrowserSettings.Default.EnableAdBlock;
            set { BrowserSettings.Default.EnableAdBlock = value; OnPropertyChanged(); }
        }

        public bool EnablePopupBlock
        {
            get => BrowserSettings.Default.EnablePopupBlock;
            set { BrowserSettings.Default.EnablePopupBlock = value; OnPropertyChanged(); }
        }

        public bool EnableJavaScript
        {
            get => BrowserSettings.Default.EnableJavaScript;
            set { BrowserSettings.Default.EnableJavaScript = value; OnPropertyChanged(); }
        }

        public bool EnableCookies
        {
            get => BrowserSettings.Default.EnableCookies;
            set { BrowserSettings.Default.EnableCookies = value; OnPropertyChanged(); }
        }

        public bool EnableExtensions
        {
            get => BrowserSettings.Default.EnableExtensions;
            set { BrowserSettings.Default.EnableExtensions = value; OnPropertyChanged(); }
        }

        public BookmarkGroup? SelectedBookmarkGroup
        {
            get => _selectedBookmarkGroup;
            set
            {
                _selectedBookmarkGroup = value;
                OnPropertyChanged();
                FilterBookmarks();
            }
        }

        public ObservableCollection<BookmarkItemViewModel> FilteredBookmarks
        {
            get => _filteredBookmarks;
            set
            {
                _filteredBookmarks = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<BookmarkItem> Bookmarks => BrowserSettings.Default.Bookmarks;
        public ObservableCollection<BookmarkGroup> BookmarkGroups => BrowserSettings.Default.BookmarkGroups;
        public ObservableCollection<HistoryItem> History => BrowserSettings.Default.History;

        private string _currentVersionText = "";
        public string CurrentVersionText
        {
            get => _currentVersionText;
            set { _currentVersionText = value; OnPropertyChanged(); }
        }

        private string? _updateMessage;
        public string? UpdateMessage
        {
            get => _updateMessage;
            set { _updateMessage = value; OnPropertyChanged(); }
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set { _isUpdateAvailable = value; OnPropertyChanged(); }
        }


        public SettingsViewModel()
        {
            EditBookmarkCommand = new RelayCommand(EditBookmark);
            RemoveBookmarkFromMenuCommand = new RelayCommand(RemoveBookmarkFromMenu);
            EditBookmarkGroupCommand = new RelayCommand(EditBookmarkGroup);
            RemoveBookmarkGroupFromMenuCommand = new RelayCommand(RemoveBookmarkGroupFromMenu);

            BrowserSettings.Default.Bookmarks.CollectionChanged += (s, e) =>
            {
                FilterBookmarks();
                OnPropertyChanged(nameof(Bookmarks));
            };

            BrowserSettings.Default.BookmarkGroups.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(BookmarkGroups));
                FilterBookmarks();
            };

            BrowserSettings.Default.History.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(History));
            };

            FilterBookmarks();
            Initialize();
        }

        private async void Initialize()
        {
            CurrentVersionText = $"現在のバージョン: v{GetCurrentVersion()}";
            await CheckForUpdatesAsync();
        }

        public void FilterBookmarks()
        {
            var bookmarksToShow = SelectedBookmarkGroup == null
                ? Bookmarks.OrderBy(b => b.GroupId).ThenBy(b => b.Order)
                : Bookmarks.Where(b => b.GroupId == SelectedBookmarkGroup.Id).OrderBy(b => b.Order);

            var groupDict = BookmarkGroups.ToDictionary(g => g.Id);

            FilteredBookmarks = new ObservableCollection<BookmarkItemViewModel>(
                bookmarksToShow.Select(bookmark => new BookmarkItemViewModel
                {
                    BookmarkItem = bookmark,
                    Name = bookmark.Name,
                    Url = bookmark.Url,
                    GroupName = groupDict.TryGetValue(bookmark.GroupId, out var group) ? group.Name : "未分類"
                })
            );
        }

        private string GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.3.0";
        }

        private async Task CheckForUpdatesAsync()
        {
            if (isUpdateCheckCompleted)
            {
                DisplayUpdateStatus();
                return;
            }

            try
            {
                if (httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                {
                    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YMM4-Browser", GetCurrentVersion()));
                }

                var response = await httpClient.GetAsync("https://api.github.com/repos/routersys/YMM4-Browser/releases/latest");
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(jsonString);
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("tag_name", out var tagNameElement))
                {
                    string latestVersionTag = tagNameElement.GetString() ?? "";
                    string latestVersionStr = latestVersionTag.StartsWith("v") ? latestVersionTag.Substring(1) : latestVersionTag;

                    if (Version.TryParse(latestVersionStr, out var latestVersion) &&
                        Version.TryParse(GetCurrentVersion(), out var currentVersion) &&
                        latestVersion > currentVersion)
                    {
                        isUpdateAvailable = true;
                        updateCheckResult = $"新しいバージョン: v{latestVersionStr}";
                    }
                    else
                    {
                        isUpdateAvailable = false;
                        updateCheckResult = "新しいバージョンはありません";
                    }
                }
            }
            catch (Exception)
            {
                isUpdateAvailable = false;
                updateCheckResult = "更新情報を確認できませんでした";
            }
            finally
            {
                isUpdateCheckCompleted = true;
                DisplayUpdateStatus();
            }
        }

        private void DisplayUpdateStatus()
        {
            UpdateMessage = updateCheckResult;
            IsUpdateAvailable = isUpdateAvailable;
        }

        public void AddBookmark()
        {
            var inputDialog = new BookmarkInputDialog
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

                var selectedGroup = inputDialog.SelectedGroup ?? BookmarkGroups.FirstOrDefault();
                if (selectedGroup == null)
                {
                    selectedGroup = new BookmarkGroup { Name = "デフォルト" };
                    BookmarkGroups.Add(selectedGroup);
                }

                var bookmark = new BookmarkItem
                {
                    Name = inputDialog.BookmarkName.Trim(),
                    Url = url.Trim(),
                    GroupId = selectedGroup.Id,
                    Order = Bookmarks.Count(b => b.GroupId == selectedGroup.Id)
                };

                Bookmarks.Add(bookmark);
            }
        }

        private void EditBookmark(object? param)
        {
            BookmarkItem? bookmarkToEdit = null;

            if (param is BookmarkItemViewModel viewModel)
            {
                bookmarkToEdit = viewModel.BookmarkItem;
            }
            else if (param is BookmarkItem bookmark)
            {
                bookmarkToEdit = bookmark;
            }

            if (bookmarkToEdit == null) return;

            var inputDialog = new BookmarkInputDialog(bookmarkToEdit)
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

                bookmarkToEdit.Name = inputDialog.BookmarkName.Trim();
                bookmarkToEdit.Url = url.Trim();

                if (inputDialog.SelectedGroup != null && bookmarkToEdit.GroupId != inputDialog.SelectedGroup.Id)
                {
                    var oldGroupId = bookmarkToEdit.GroupId;
                    var newGroupId = inputDialog.SelectedGroup.Id;
                    bookmarkToEdit.GroupId = newGroupId;

                    var oldGroupBookmarks = Bookmarks.Where(b => b.GroupId == oldGroupId).OrderBy(b => b.Order).ToList();
                    for (int i = 0; i < oldGroupBookmarks.Count; i++)
                    {
                        oldGroupBookmarks[i].Order = i;
                    }

                    bookmarkToEdit.Order = Bookmarks.Count(b => b.GroupId == newGroupId);
                }
                FilterBookmarks();
            }
        }

        private void RemoveBookmarkFromMenu(object? param)
        {
            BookmarkItem? bookmarkToRemove = null;

            if (param is BookmarkItemViewModel viewModel)
            {
                bookmarkToRemove = viewModel.BookmarkItem;
            }
            else if (param is BookmarkItem bookmark)
            {
                bookmarkToRemove = bookmark;
            }

            if (bookmarkToRemove == null) return;

            var result = MessageBox.Show(
                $"ブックマーク「{bookmarkToRemove.Name}」を削除しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var groupId = bookmarkToRemove.GroupId;
                Bookmarks.Remove(bookmarkToRemove);

                var groupBookmarks = Bookmarks.Where(b => b.GroupId == groupId).OrderBy(b => b.Order).ToList();
                for (int i = 0; i < groupBookmarks.Count; i++)
                {
                    groupBookmarks[i].Order = i;
                }
            }
        }

        public void RemoveSelectedBookmarks(IEnumerable<object> selectedItems)
        {
            var itemsToRemove = selectedItems.Cast<BookmarkItemViewModel>()
                                                               .Select(vm => vm.BookmarkItem)
                                                               .ToList();
            if (itemsToRemove.Count == 0)
            {
                MessageBox.Show("削除するブックマークを選択してください。", "情報",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"{itemsToRemove.Count}個のブックマークを削除しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var affectedGroupIds = itemsToRemove.Select(item => item.GroupId).Distinct().ToList();
                foreach (var item in itemsToRemove)
                {
                    Bookmarks.Remove(item);
                }
                foreach (var groupId in affectedGroupIds)
                {
                    var groupBookmarks = Bookmarks.Where(b => b.GroupId == groupId).OrderBy(b => b.Order).ToList();
                    for (int i = 0; i < groupBookmarks.Count; i++)
                    {
                        groupBookmarks[i].Order = i;
                    }
                }
            }
        }

        public void MoveBookmarkUp(object selectedItem)
        {
            var selectedItemVM = selectedItem as BookmarkItemViewModel;
            if (selectedItemVM?.BookmarkItem == null) return;

            var bookmark = selectedItemVM.BookmarkItem;
            var sameGroupBookmarks = Bookmarks.Where(b => b.GroupId == bookmark.GroupId)
                                             .OrderBy(b => b.Order)
                                             .ToList();

            var currentIndex = sameGroupBookmarks.IndexOf(bookmark);
            if (currentIndex > 0)
            {
                var previousBookmark = sameGroupBookmarks[currentIndex - 1];
                (bookmark.Order, previousBookmark.Order) = (previousBookmark.Order, bookmark.Order);
                FilterBookmarks();
            }
        }

        public void MoveBookmarkDown(object selectedItem)
        {
            var selectedItemVM = selectedItem as BookmarkItemViewModel;
            if (selectedItemVM?.BookmarkItem == null) return;

            var bookmark = selectedItemVM.BookmarkItem;
            var sameGroupBookmarks = Bookmarks.Where(b => b.GroupId == bookmark.GroupId)
                                             .OrderBy(b => b.Order)
                                             .ToList();

            var currentIndex = sameGroupBookmarks.IndexOf(bookmark);
            if (currentIndex < sameGroupBookmarks.Count - 1)
            {
                var nextBookmark = sameGroupBookmarks[currentIndex + 1];
                (bookmark.Order, nextBookmark.Order) = (nextBookmark.Order, bookmark.Order);
                FilterBookmarks();
            }
        }

        public void AddBookmarkGroup()
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
            }
        }

        public void RemoveBookmarkGroup(object? selectedItem)
        {
            var selectedGroup = selectedItem as BookmarkGroup;
            if (selectedGroup == null)
            {
                MessageBox.Show("削除するグループを選択してください。", "情報",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var bookmarksInGroup = Bookmarks.Where(b => b.GroupId == selectedGroup.Id).ToList();

            var message = bookmarksInGroup.Any()
                ? $"グループ「{selectedGroup.Name}」とその中のブックマーク{bookmarksInGroup.Count}個を削除しますか？"
                : $"グループ「{selectedGroup.Name}」を削除しますか？";

            var result = MessageBox.Show(message, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var bookmark in bookmarksInGroup)
                {
                    Bookmarks.Remove(bookmark);
                }
                BookmarkGroups.Remove(selectedGroup);
            }
        }

        private void EditBookmarkGroup(object? param)
        {
            var groupToEdit = param as BookmarkGroup;
            if (groupToEdit == null) return;

            var inputDialog = new GroupInputDialog(groupToEdit)
            {
                Owner = Application.Current.MainWindow
            };

            if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.GroupName))
            {
                groupToEdit.Name = inputDialog.GroupName.Trim();
            }
        }

        private void RemoveBookmarkGroupFromMenu(object? param)
        {
            var groupToRemove = param as BookmarkGroup;
            if (groupToRemove == null) return;

            var bookmarksInGroup = Bookmarks.Where(b => b.GroupId == groupToRemove.Id).ToList();

            var message = bookmarksInGroup.Any()
                ? $"グループ「{groupToRemove.Name}」とその中のブックマーク{bookmarksInGroup.Count}個を削除しますか？"
                : $"グループ「{groupToRemove.Name}」を削除しますか？";

            var result = MessageBox.Show(message, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var bookmark in bookmarksInGroup)
                {
                    Bookmarks.Remove(bookmark);
                }
                BookmarkGroups.Remove(groupToRemove);
            }
        }

        public void ClearHistory()
        {
            var result = MessageBox.Show(
                "履歴をすべて削除しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                History.Clear();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BookmarkItemViewModel : INotifyPropertyChanged
    {
        public BookmarkItem BookmarkItem { get; set; } = null!;

        private string _name = "";
        private string _url = "";
        private string _groupName = "";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }

        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}