using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Plugin;

namespace YMM4Browser;

public class BrowserSettings : SettingsBase<BrowserSettings>
{
    public override SettingsCategory Category => SettingsCategory.None;
    public override string Name => "YMM4 ブラウザ";

    public override bool HasSettingView => true;
    public override object? SettingView => new YMM4Browser.View.SettingsView();

    private string GetPluginDirectory()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var dir = Path.GetDirectoryName(asm.Location);
        return string.IsNullOrEmpty(dir) ? AppDomain.CurrentDomain.BaseDirectory : dir;
    }

    public string FilePath
    {
        get
        {
            return Path.Combine(GetPluginDirectory(), "YMM4Browser.settings.json");
        }
    }

    public string BookmarksFilePath
    {
        get
        {
            return Path.Combine(GetPluginDirectory(), "bookmarks.json");
        }
    }

    private string _homeUrl = "https://www.google.com";
    private bool _enableAdBlock = true;
    private bool _enablePopupBlock = true;
    private ObservableCollection<BookmarkItem> _bookmarks = new();
    private ObservableCollection<BookmarkGroup> _bookmarkGroups = new();
    private bool _compactMode = false;
    private bool _enableJavaScript = true;
    private bool _enableCookies = true;
    private bool _enableExtensions = false;
    private ObservableCollection<HistoryItem> _history = new();
    private int _maxHistoryItems = 1000;

    public string HomeUrl { get => _homeUrl; set => Set(ref _homeUrl, value); }
    public bool EnableAdBlock { get => _enableAdBlock; set => Set(ref _enableAdBlock, value); }
    public bool EnablePopupBlock { get => _enablePopupBlock; set => Set(ref _enablePopupBlock, value); }
    public bool EnableJavaScript { get => _enableJavaScript; set => Set(ref _enableJavaScript, value); }
    public bool EnableCookies { get => _enableCookies; set => Set(ref _enableCookies, value); }
    public bool EnableExtensions { get => _enableExtensions; set => Set(ref _enableExtensions, value); }
    public int MaxHistoryItems { get => _maxHistoryItems; set => Set(ref _maxHistoryItems, value); }

    [JsonIgnore]
    public ObservableCollection<BookmarkItem> Bookmarks
    {
        get => _bookmarks;
        set { SetAndSetupCollection(ref _bookmarks, value, OnItemPropertyChanged); OnPropertyChanged(); }
    }

    [JsonIgnore]
    public ObservableCollection<BookmarkGroup> BookmarkGroups
    {
        get => _bookmarkGroups;
        set { SetAndSetupCollection(ref _bookmarkGroups, value, OnItemPropertyChanged); OnPropertyChanged(); }
    }

    public ObservableCollection<HistoryItem> History
    {
        get => _history;
        set
        {
            if (_history != null) _history.CollectionChanged -= OnHistoryCollectionChanged;
            Set(ref _history, value ?? new ObservableCollection<HistoryItem>());
            if (_history != null) _history.CollectionChanged += OnHistoryCollectionChanged;
        }
    }

    public bool CompactMode { get => _compactMode; set => Set(ref _compactMode, value); }

    public override void Initialize()
    {
        if (!File.Exists(BookmarksFilePath))
        {
            CreateDefaultBookmarksFile();
        }
        LoadBookmarksAndGroups();

        _history ??= new ObservableCollection<HistoryItem>();

        SetupEventHandlers();
        UpdateBookmarkGroupCounts();
    }

    private void SetAndSetupCollection<T>(ref ObservableCollection<T> field, ObservableCollection<T> value, PropertyChangedEventHandler itemHandler) where T : INotifyPropertyChanged
    {
        if (field != null)
        {
            field.CollectionChanged -= OnCollectionChanged;
            foreach (var item in field)
            {
                item.PropertyChanged -= itemHandler;
            }
        }

        field = value ?? new ObservableCollection<T>();

        field.CollectionChanged += OnCollectionChanged;
        foreach (var item in field)
        {
            item.PropertyChanged += itemHandler;
        }
    }

    private void CreateDefaultBookmarksFile()
    {
        var defaultGroupId = Guid.NewGuid().ToString();
        var defaultData = new BookmarkData
        {
            BookmarkGroups = new List<BookmarkGroup>
            {
                new() { Id = defaultGroupId, Name = "デフォルト", IsExpanded = true, Order = 0 }
            },
            Bookmarks = new List<BookmarkItem>
            {
                new() { Name = "Google", Url = "https://www.google.com", GroupId = defaultGroupId, Order = 0 },
                new() { Name = "YouTube", Url = "https://www.youtube.com", GroupId = defaultGroupId, Order = 1 },
                new() { Name = "ゆっくりムービーメーカー4", Url = "https://manjubox.net/ymm4/", GroupId = defaultGroupId, Order = 2 },
                new() { Name = "ニコニコ動画", Url = "https://www.nicovideo.jp", GroupId = defaultGroupId, Order = 3 },
                new() { Name = "GitHub", Url = "https://github.com", GroupId = defaultGroupId, Order = 4 }
            }
        };
        SaveBookmarksAndGroups(defaultData.Bookmarks, defaultData.BookmarkGroups);
    }

    private void LoadBookmarksAndGroups()
    {
        if (!File.Exists(BookmarksFilePath)) return;
        try
        {
            var json = File.ReadAllText(BookmarksFilePath);
            var bookmarksData = JsonSerializer.Deserialize<BookmarkData>(json);

            if (bookmarksData != null)
            {
                Bookmarks = new ObservableCollection<BookmarkItem>(bookmarksData.Bookmarks ?? new List<BookmarkItem>());
                BookmarkGroups = new ObservableCollection<BookmarkGroup>(bookmarksData.BookmarkGroups ?? new List<BookmarkGroup>());
            }
        }
        catch (Exception)
        {
            Bookmarks = new ObservableCollection<BookmarkItem>();
            BookmarkGroups = new ObservableCollection<BookmarkGroup>();
        }
    }

    private void SaveBookmarksAndGroups()
    {
        SaveBookmarksAndGroups(Bookmarks, BookmarkGroups);
    }

    private void SaveBookmarksAndGroups(ICollection<BookmarkItem> bookmarks, ICollection<BookmarkGroup> groups)
    {
        try
        {
            var data = new BookmarkData
            {
                Bookmarks = bookmarks.ToList(),
                BookmarkGroups = groups.ToList()
            };
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(BookmarksFilePath, json);
        }
        catch (Exception) { }
    }

    public static void InitializeSettings() => Default.Initialize();

    private void SetupEventHandlers()
    {
        History.CollectionChanged -= OnHistoryCollectionChanged;
        History.CollectionChanged += OnHistoryCollectionChanged;

        SetAndSetupCollection(ref _bookmarks, _bookmarks, OnItemPropertyChanged);
        SetAndSetupCollection(ref _bookmarkGroups, _bookmarkGroups, OnItemPropertyChanged);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged += OnItemPropertyChanged;
        }
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged -= OnItemPropertyChanged;
        }
        UpdateAndSave();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateAndSave();

    private void UpdateAndSave()
    {
        UpdateBookmarkGroupCounts();
        SaveBookmarksAndGroups();
    }

    private void OnHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        while (History.Count > MaxHistoryItems && MaxHistoryItems > 0)
        {
            History.RemoveAt(History.Count - 1);
        }
    }

    private void UpdateBookmarkGroupCounts()
    {
        foreach (var group in BookmarkGroups)
        {
            group.UpdateBookmarkCount(Bookmarks.Count(b => b.GroupId == group.Id));
        }
    }

    public void AddToHistory(string url, string title)
    {
        if (string.IsNullOrEmpty(url)) return;
        var existingItem = History.FirstOrDefault(h => h.Url == url);
        if (existingItem != null)
        {
            History.Remove(existingItem);
        }
        History.Insert(0, new HistoryItem { Url = url, Title = title, VisitTime = DateTime.Now });
    }
}

public class BookmarkData
{
    public List<BookmarkGroup> BookmarkGroups { get; set; } = new();
    public List<BookmarkItem> Bookmarks { get; set; } = new();
}

public class BookmarkItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _url = "";
    private string _groupId = "";
    private int _order;

    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Url { get => _url; set => SetField(ref _url, value); }
    public string GroupId { get => _groupId; set => SetField(ref _groupId, value); }
    public int Order { get => _order; set => SetField(ref _order, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class BookmarkGroup : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "";
    private bool _isExpanded = true;
    private int _order;
    private int _bookmarkCount;

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public bool IsExpanded { get => _isExpanded; set => SetField(ref _isExpanded, value); }
    public int Order { get => _order; set => SetField(ref _order, value); }

    [JsonIgnore]
    public ObservableCollection<BookmarkItem> Bookmarks { get; set; } = new();
    public int BookmarkCount { get => _bookmarkCount; private set => SetField(ref _bookmarkCount, value); }

    public void UpdateBookmarkCount(int count) => BookmarkCount = count;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class HistoryItem : INotifyPropertyChanged
{
    private string _url = "";
    private string _title = "";
    private DateTime _visitTime = DateTime.Now;

    public string Url { get => _url; set => SetField(ref _url, value); }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public DateTime VisitTime { get => _visitTime; set => SetField(ref _visitTime, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}