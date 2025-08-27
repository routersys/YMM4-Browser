using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using YMM4Browser.ViewModel;

namespace YMM4Browser.View;

public partial class SettingsView : UserControl, INotifyPropertyChanged
{
    public ICommand EditBookmarkCommand { get; }
    public ICommand RemoveBookmarkFromMenuCommand { get; }
    public ICommand EditBookmarkGroupCommand { get; }
    public ICommand RemoveBookmarkGroupFromMenuCommand { get; }
    public ICommand MoveBookmarkToGroupCommand { get; }

    private static readonly HttpClient httpClient = new();
    private static bool isUpdateCheckCompleted = false;
    private static string? updateCheckResult = null;
    private static bool isUpdateAvailable = false;

    private BookmarkGroup? _selectedBookmarkGroup;
    private ObservableCollection<BookmarkItemViewModel> _filteredBookmarks = new();
    private Point _startPoint;
    private bool _isDragging = false;

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

    public SettingsView()
    {
        InitializeComponent();

        DataContext = this;

        EditBookmarkCommand = new RelayCommand(EditBookmark);
        RemoveBookmarkFromMenuCommand = new RelayCommand(RemoveBookmarkFromMenu);
        EditBookmarkGroupCommand = new RelayCommand(EditBookmarkGroup);
        RemoveBookmarkGroupFromMenuCommand = new RelayCommand(RemoveBookmarkGroupFromMenu);
        MoveBookmarkToGroupCommand = new RelayCommand(MoveBookmarkToGroup);

        Loaded += OnLoaded;

        BrowserSettings.Default.Bookmarks.CollectionChanged += (s, e) =>
        {
            FilterBookmarks();
            OnPropertyChanged(nameof(Bookmarks));
        };

        BrowserSettings.Default.BookmarkGroups.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(BookmarkGroups));
            FilterBookmarks();
            UpdateGroupFilter();
        };

        BrowserSettings.Default.History.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(History));
        };

        FilterBookmarks();
        UpdateGroupFilter();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionText.Text = $"現在のバージョン: v{GetCurrentVersion()}";
        await CheckForUpdatesAsync();
    }

    private void UpdateGroupFilter()
    {
        var items = new List<object>();
        items.Add(new { Name = "すべて", Group = (BookmarkGroup?)null });

        foreach (var group in BookmarkGroups)
        {
            items.Add(new { Name = group.Name, Group = group });
        }

        GroupFilterComboBox.ItemsSource = items;

        if (GroupFilterComboBox.SelectedIndex == -1)
        {
            GroupFilterComboBox.SelectedIndex = 0;
        }
    }

    private void FilterBookmarks()
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
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
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
        UpdateMessage.Text = updateCheckResult;
        if (isUpdateAvailable)
        {
            UpdateNotification.Background = (LinearGradientBrush)FindResource("UpdateBrush");
            UpdateNotification.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 199, 89));
            UpdateIcon.Fill = Brushes.White;
            UpdateIcon.Data = Geometry.Parse("M19,9H15V3H9V9H5L12,16L19,9M5,18V20H19V18H5Z");
            UpdateMessage.Foreground = Brushes.White;
        }
        else
        {
            UpdateNotification.Background = new SolidColorBrush(SystemColors.ControlColor);
            UpdateNotification.BorderBrush = new SolidColorBrush(SystemColors.ActiveBorderColor);
            UpdateIcon.Fill = new SolidColorBrush(SystemColors.GrayTextColor);
            UpdateIcon.Data = Geometry.Parse("M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,17A1.5,1.5 0 0,0 13.5,15.5V11A1.5,1.5 0 0,0 12,9.5A1.5,1.5 0 0,0 10.5,11V15.5A1.5,1.5 0 0,0 12,17M12,5.5A1.5,1.5 0 0,0 10.5,7A1.5,1.5 0 0,0 12,8.5A1.5,1.5 0 0,0 13.5,7A1.5,1.5 0 0,0 12,5.5Z");
            UpdateMessage.Foreground = new SolidColorBrush(SystemColors.ControlTextColor);
        }
        UpdateNotification.Visibility = Visibility.Visible;
    }

    private void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var inputDialog = new BookmarkInputDialog
        {
            Owner = Window.GetWindow(this)
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
            Owner = Window.GetWindow(this)
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

    private void RemoveBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var itemsToRemove = BookmarkListView.SelectedItems.Cast<BookmarkItemViewModel>()
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

    private void MoveBookmarkUpButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItemVM = BookmarkListView.SelectedItem as BookmarkItemViewModel;
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

    private void MoveBookmarkDownButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItemVM = BookmarkListView.SelectedItem as BookmarkItemViewModel;
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

    private void AddBookmarkGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var inputDialog = new GroupInputDialog()
        {
            Owner = Window.GetWindow(this)
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

    private void RemoveBookmarkGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedGroup = BookmarkGroupListView.SelectedItem as BookmarkGroup;
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
            Owner = Window.GetWindow(this)
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

    private void MoveBookmarkToGroup(object? param)
    {
        if (param is not BookmarkGroup targetGroup) return;

        var selectedItemVM = BookmarkListView.SelectedItem as BookmarkItemViewModel;
        if (selectedItemVM?.BookmarkItem == null) return;

        var bookmark = selectedItemVM.BookmarkItem;
        var oldGroupId = bookmark.GroupId;

        bookmark.GroupId = targetGroup.Id;

        var oldGroupBookmarks = Bookmarks.Where(b => b.GroupId == oldGroupId).OrderBy(b => b.Order).ToList();
        for (int i = 0; i < oldGroupBookmarks.Count; i++)
        {
            oldGroupBookmarks[i].Order = i;
        }

        bookmark.Order = Bookmarks.Count(b => b.GroupId == targetGroup.Id);
        FilterBookmarks();
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
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

    private void GroupFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupFilterComboBox.SelectedItem != null)
        {
            dynamic selectedItem = GroupFilterComboBox.SelectedItem;
            SelectedBookmarkGroup = selectedItem.Group as BookmarkGroup;
        }
    }

    private void BookmarkListViewItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item && item.DataContext is BookmarkItemViewModel viewModel)
        {
            var contextMenu = new ContextMenu();

            var editMenuItem = new MenuItem
            {
                Header = "編集",
                Command = EditBookmarkCommand,
                CommandParameter = viewModel
            };

            var removeMenuItem = new MenuItem
            {
                Header = "削除",
                Command = RemoveBookmarkFromMenuCommand,
                CommandParameter = viewModel
            };

            contextMenu.Items.Add(editMenuItem);
            contextMenu.Items.Add(removeMenuItem);

            contextMenu.PlacementTarget = item;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void BookmarkGroupListViewItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item && item.DataContext is BookmarkGroup group)
        {
            var contextMenu = new ContextMenu();

            var editMenuItem = new MenuItem
            {
                Header = "編集",
                Command = EditBookmarkGroupCommand,
                CommandParameter = group
            };

            var removeMenuItem = new MenuItem
            {
                Header = "削除",
                Command = RemoveBookmarkGroupFromMenuCommand,
                CommandParameter = group
            };

            contextMenu.Items.Add(editMenuItem);
            contextMenu.Items.Add(removeMenuItem);

            contextMenu.PlacementTarget = item;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void ListViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListViewItem item) return;

        Point mousePos = e.GetPosition(null);
        Vector diff = _startPoint - mousePos;

        if (!_isDragging && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                           Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _isDragging = true;
            if (item.DataContext is BookmarkItemViewModel data)
            {
                DragDrop.DoDragDrop(item, data, DragDropEffects.Move);
            }
        }
    }

    private void ListViewItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListViewItem targetItem ||
            e.Data.GetData(typeof(BookmarkItemViewModel)) is not BookmarkItemViewModel draggedItem ||
            targetItem.DataContext is not BookmarkItemViewModel targetItemData ||
            draggedItem == targetItemData)
        {
            return;
        }

        var draggedBookmark = draggedItem.BookmarkItem;
        var targetBookmark = targetItemData.BookmarkItem;
        var originalGroupId = draggedBookmark.GroupId;
        var newGroupId = targetBookmark.GroupId;

        if (originalGroupId != newGroupId)
        {
            draggedBookmark.GroupId = newGroupId;
            var oldGroupBookmarks = Bookmarks.Where(b => b.GroupId == originalGroupId).OrderBy(b => b.Order).ToList();
            oldGroupBookmarks.Remove(draggedBookmark);
            for (int i = 0; i < oldGroupBookmarks.Count; i++)
            {
                oldGroupBookmarks[i].Order = i;
            }
        }

        var newGroupBookmarks = Bookmarks.Where(b => b.GroupId == newGroupId).OrderBy(b => b.Order).ToList();
        newGroupBookmarks.Remove(draggedBookmark);

        int targetIndex = newGroupBookmarks.IndexOf(targetBookmark);

        var targetRect = VisualTreeHelper.GetDescendantBounds(targetItem);
        var dropPoint = e.GetPosition(targetItem);
        if (dropPoint.Y > targetRect.Height / 2)
        {
            targetIndex++;
        }

        newGroupBookmarks.Insert(targetIndex, draggedBookmark);

        for (int i = 0; i < newGroupBookmarks.Count; i++)
        {
            newGroupBookmarks[i].Order = i;
        }

        FilterBookmarks();
        targetItem.Background = Brushes.Transparent;
    }

    private void ListViewItem_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is ListViewItem item && e.Data.GetDataPresent(typeof(BookmarkItemViewModel)))
        {
            item.Background = new SolidColorBrush(SystemColors.HighlightColor) { Opacity = 0.3 };
        }
    }

    private void ListViewItem_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListViewItem item)
        {
            item.Background = Brushes.Transparent;
        }
    }

    private void BookmarkListView_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(BookmarkItemViewModel)))
        {
            e.Handled = true;
        }
    }

    private void BookmarkListView_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(BookmarkItemViewModel)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
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
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            _url = value;
            OnPropertyChanged();
        }
    }

    public string GroupName
    {
        get => _groupName;
        set
        {
            _groupName = value;
            OnPropertyChanged();
        }
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

public class BookmarkInputDialog : Window
{
    private readonly TextBox nameTextBox = new();
    private readonly TextBox urlTextBox = new();
    private readonly ComboBox groupComboBox = new();
    private readonly Button okButton = new();

    public string BookmarkName => nameTextBox.Text;
    public string BookmarkUrl => urlTextBox.Text;
    public BookmarkGroup? SelectedGroup => groupComboBox.SelectedItem as BookmarkGroup;

    public BookmarkInputDialog(BookmarkItem? bookmark = null)
    {
        Title = bookmark == null ? "ブックマーク追加" : "ブックマーク編集";
        Width = 480;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(SystemColors.ControlColor);

        var grid = new Grid { Margin = new Thickness(15) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nameLabel = new Label
        {
            Content = "名前:",
            Margin = new Thickness(0, 0, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(SystemColors.ControlTextColor)
        };
        Grid.SetRow(nameLabel, 0);
        Grid.SetColumn(nameLabel, 0);
        grid.Children.Add(nameLabel);

        nameTextBox.Margin = new Thickness(0, 0, 0, 10);
        nameTextBox.Height = 25;
        nameTextBox.VerticalContentAlignment = VerticalAlignment.Center;
        nameTextBox.Background = new SolidColorBrush(SystemColors.WindowColor);
        nameTextBox.Foreground = new SolidColorBrush(SystemColors.WindowTextColor);
        nameTextBox.BorderBrush = new SolidColorBrush(SystemColors.ActiveBorderColor);
        nameTextBox.BorderThickness = new Thickness(1);
        Grid.SetRow(nameTextBox, 0);
        Grid.SetColumn(nameTextBox, 1);
        grid.Children.Add(nameTextBox);

        var urlLabel = new Label
        {
            Content = "URL:",
            Margin = new Thickness(0, 0, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(SystemColors.ControlTextColor)
        };
        Grid.SetRow(urlLabel, 1);
        Grid.SetColumn(urlLabel, 0);
        grid.Children.Add(urlLabel);

        urlTextBox.Margin = new Thickness(0, 0, 0, 10);
        urlTextBox.Height = 25;
        urlTextBox.VerticalContentAlignment = VerticalAlignment.Center;
        urlTextBox.Background = new SolidColorBrush(SystemColors.WindowColor);
        urlTextBox.Foreground = new SolidColorBrush(SystemColors.WindowTextColor);
        urlTextBox.BorderBrush = new SolidColorBrush(SystemColors.ActiveBorderColor);
        urlTextBox.BorderThickness = new Thickness(1);
        Grid.SetRow(urlTextBox, 1);
        Grid.SetColumn(urlTextBox, 1);
        grid.Children.Add(urlTextBox);

        var groupLabel = new Label
        {
            Content = "グループ:",
            Margin = new Thickness(0, 0, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(SystemColors.ControlTextColor)
        };
        Grid.SetRow(groupLabel, 2);
        Grid.SetColumn(groupLabel, 0);
        grid.Children.Add(groupLabel);

        groupComboBox.Background = new SolidColorBrush(SystemColors.WindowColor);
        groupComboBox.Foreground = new SolidColorBrush(SystemColors.WindowTextColor);
        groupComboBox.BorderBrush = new SolidColorBrush(SystemColors.ActiveBorderColor);
        groupComboBox.BorderThickness = new Thickness(1);
        groupComboBox.Margin = new Thickness(0, 0, 0, 10);
        groupComboBox.Height = 25;
        groupComboBox.ItemsSource = BrowserSettings.Default.BookmarkGroups;
        groupComboBox.DisplayMemberPath = "Name";
        Grid.SetRow(groupComboBox, 2);
        Grid.SetColumn(groupComboBox, 1);
        grid.Children.Add(groupComboBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(buttonPanel, 4);
        Grid.SetColumn(buttonPanel, 1);

        okButton.Content = bookmark == null ? "追加" : "保存";
        okButton.Width = 70;
        okButton.Height = 28;
        okButton.Margin = new Thickness(0, 0, 10, 0);
        okButton.IsDefault = true;
        okButton.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text) || string.IsNullOrWhiteSpace(urlTextBox.Text))
            {
                MessageBox.Show("名前とURLを入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "キャンセル",
            Width = 80,
            Height = 28,
            IsCancel = true
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(buttonPanel);
        Content = grid;

        if (bookmark != null)
        {
            nameTextBox.Text = bookmark.Name;
            urlTextBox.Text = bookmark.Url;
            var group = BrowserSettings.Default.BookmarkGroups.FirstOrDefault(g => g.Id == bookmark.GroupId);
            if (group != null)
            {
                groupComboBox.SelectedItem = group;
            }
        }
        else if (BrowserSettings.Default.BookmarkGroups.Any())
        {
            groupComboBox.SelectedIndex = 0;
        }

        nameTextBox.Focus();
        nameTextBox.SelectAll();

        var highlightBrush = SystemColors.HighlightBrush;
        var defaultBorderBrush = new SolidColorBrush(SystemColors.ActiveBorderColor);

        Action<Control> setFocusHandlers = (control) =>
        {
            control.GotFocus += (s, e) => control.BorderBrush = highlightBrush;
            control.LostFocus += (s, e) => control.BorderBrush = defaultBorderBrush;
        };

        setFocusHandlers(nameTextBox);
        setFocusHandlers(urlTextBox);
        setFocusHandlers(groupComboBox);
    }
}

public class GroupInputDialog : Window
{
    private readonly TextBox nameTextBox = new();
    public string GroupName => nameTextBox.Text;

    public GroupInputDialog(BookmarkGroup? group = null)
    {
        Title = group == null ? "グループ追加" : "グループ編集";
        Width = 350;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(SystemColors.ControlColor);

        var grid = new Grid { Margin = new Thickness(15) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nameLabel = new Label
        {
            Content = "グループ名:",
            Margin = new Thickness(0, 0, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(SystemColors.ControlTextColor)
        };
        Grid.SetRow(nameLabel, 0);
        Grid.SetColumn(nameLabel, 0);
        grid.Children.Add(nameLabel);

        nameTextBox.Margin = new Thickness(0, 0, 0, 10);
        nameTextBox.Height = 25;
        nameTextBox.VerticalContentAlignment = VerticalAlignment.Center;
        nameTextBox.Background = new SolidColorBrush(SystemColors.WindowColor);
        nameTextBox.Foreground = new SolidColorBrush(SystemColors.WindowTextColor);
        Grid.SetRow(nameTextBox, 0);
        Grid.SetColumn(nameTextBox, 1);
        grid.Children.Add(nameTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(buttonPanel, 2);
        Grid.SetColumn(buttonPanel, 1);

        var okButton = new Button
        {
            Content = group == null ? "追加" : "保存",
            Width = 70,
            Height = 28,
            Margin = new Thickness(0, 0, 10, 0),
            IsDefault = true
        };
        okButton.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show("グループ名を入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "キャンセル",
            Width = 80,
            Height = 28,
            IsCancel = true
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(buttonPanel);
        Content = grid;

        if (group != null)
        {
            nameTextBox.Text = group.Name;
        }

        nameTextBox.Focus();
        nameTextBox.SelectAll();
    }
}