using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace YMM4Browser.View;

public partial class SettingsView : UserControl
{
    public ICommand EditBookmarkCommand { get; }
    public ICommand RemoveBookmarkFromMenuCommand { get; }

    private static readonly HttpClient httpClient = new();
    private static bool isUpdateCheckCompleted = false;
    private static string? updateCheckResult = null;
    private static bool isUpdateAvailable = false;

    public SettingsView()
    {
        InitializeComponent();

        DataContext = BrowserSettings.Default;

        EditBookmarkCommand = new RelayCommand(EditBookmark);
        RemoveBookmarkFromMenuCommand = new RelayCommand(RemoveBookmarkFromMenu);

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionText.Text = $"現在のバージョン: v{GetCurrentVersion()}";
        await CheckForUpdatesAsync();
    }

    private string GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1";
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
            UpdateNotification.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#EEEEEE");
            UpdateNotification.BorderBrush = new SolidColorBrush(SystemColors.ActiveBorderColor);
            UpdateIcon.Fill = new SolidColorBrush(SystemColors.GrayTextColor);
            UpdateIcon.Data = Geometry.Parse("M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,17A1.5,1.5 0 0,0 13.5,15.5V11A1.5,1.5 0 0,0 12,9.5A1.5,1.5 0 0,0 10.5,11V15.5A1.5,1.5 0 0,0 12,17M12,5.5A1.5,1.5 0 0,0 10.5,7A1.5,1.5 0 0,0 12,8.5A1.5,1.5 0 0,0 13.5,7A1.5,1.5 0 0,0 12,5.5Z");
            UpdateMessage.Foreground = new SolidColorBrush(SystemColors.ControlDarkDarkColor);
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

            BrowserSettings.Default.Bookmarks.Add(new BookmarkItem
            {
                Name = inputDialog.BookmarkName.Trim(),
                Url = url.Trim()
            });
        }
    }

    private void EditBookmark(object? param)
    {
        if (param is not BookmarkItem bookmarkToEdit) return;

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
        }
    }

    private void RemoveBookmarkFromMenu(object? param)
    {
        if (param is not BookmarkItem bookmarkToRemove) return;

        var result = MessageBox.Show(
            $"ブックマーク「{bookmarkToRemove.Name}」を削除しますか？",
            "確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            BrowserSettings.Default.Bookmarks.Remove(bookmarkToRemove);
        }
    }

    private void RemoveBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var itemsToRemove = BookmarkListView.SelectedItems.Cast<BookmarkItem>().ToList();
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
            foreach (var item in itemsToRemove)
            {
                BrowserSettings.Default.Bookmarks.Remove(item);
            }
        }
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
    private readonly Button okButton = new();

    public string BookmarkName => nameTextBox.Text;
    public string BookmarkUrl => urlTextBox.Text;

    public BookmarkInputDialog(BookmarkItem? bookmark = null)
    {
        Title = bookmark == null ? "ブックマーク追加" : "ブックマーク編集";
        Width = 450;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(SystemColors.ControlColor);

        var grid = new Grid { Margin = new Thickness(15) };
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
        Grid.SetRow(urlTextBox, 1);
        Grid.SetColumn(urlTextBox, 1);
        grid.Children.Add(urlTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(buttonPanel, 3);
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
                MessageBox.Show("名前とURLを入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        }

        nameTextBox.Focus();
        nameTextBox.SelectAll();

        nameTextBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                urlTextBox.Focus();
                urlTextBox.SelectAll();
            }
        };

        urlTextBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                okButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        };
    }
}