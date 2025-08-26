using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Plugin;

namespace YMM4Browser;

public class BrowserSettings : SettingsBase<BrowserSettings>
{
    public override SettingsCategory Category => SettingsCategory.None;
    public override string Name => "YMM4 ブラウザ";

    public override bool HasSettingView => true;
    public override object? SettingView => new YMM4Browser.View.SettingsView();

    public new string FilePath
    {
        get
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var dir = Path.GetDirectoryName(asm.Location);
            if (string.IsNullOrEmpty(dir))
            {
                return "YMM4Browser.settings.json";
            }
            return Path.Combine(dir, "YMM4Browser.settings.json");
        }
    }

    private string _homeUrl = "https://www.google.com";
    private bool _enableAdBlock = true;
    private bool _enablePopupBlock = true;
    private ObservableCollection<BookmarkItem> _bookmarks = new();
    private bool _compactMode = false;

    public string HomeUrl
    {
        get => _homeUrl;
        set => Set(ref _homeUrl, value);
    }

    public bool EnableAdBlock
    {
        get => _enableAdBlock;
        set => Set(ref _enableAdBlock, value);
    }

    public bool EnablePopupBlock
    {
        get => _enablePopupBlock;
        set => Set(ref _enablePopupBlock, value);
    }

    public ObservableCollection<BookmarkItem> Bookmarks
    {
        get => _bookmarks;
        set
        {
            if (_bookmarks != null)
            {
                _bookmarks.CollectionChanged -= OnBookmarksCollectionChanged;
                foreach (var bookmark in _bookmarks)
                {
                    bookmark.PropertyChanged -= OnBookmarkPropertyChanged;
                }
            }

            Set(ref _bookmarks, value);

            if (_bookmarks != null)
            {
                _bookmarks.CollectionChanged += OnBookmarksCollectionChanged;
                foreach (var bookmark in _bookmarks)
                {
                    bookmark.PropertyChanged += OnBookmarkPropertyChanged;
                }
            }
        }
    }

    public bool CompactMode
    {
        get => _compactMode;
        set => Set(ref _compactMode, value);
    }

    public override void Initialize()
    {
        if (_bookmarks.Count == 0)
        {
            _bookmarks.Add(new BookmarkItem { Name = "Google", Url = "https://www.google.com" });
            _bookmarks.Add(new BookmarkItem { Name = "YouTube", Url = "https://www.youtube.com" });
            _bookmarks.Add(new BookmarkItem { Name = "ゆっくりムービーメーカー4", Url = "https://manjubox.net/ymm4/" });
            _bookmarks.Add(new BookmarkItem { Name = "ニコニコ動画", Url = "https://www.nicovideo.jp" });
            _bookmarks.Add(new BookmarkItem { Name = "GitHub", Url = "https://github.com" });
        }
        SetupBookmarkEventHandlers();
    }

    public static void InitializeSettings()
    {
        var instance = Default;
        if (instance.Bookmarks.Count == 0)
        {
            instance.Initialize();
        }
        else
        {
            instance.SetupBookmarkEventHandlers();
        }
    }

    private void SetupBookmarkEventHandlers()
    {
        _bookmarks.CollectionChanged -= OnBookmarksCollectionChanged;
        _bookmarks.CollectionChanged += OnBookmarksCollectionChanged;

        foreach (var bookmark in _bookmarks)
        {
            bookmark.PropertyChanged -= OnBookmarkPropertyChanged;
            bookmark.PropertyChanged += OnBookmarkPropertyChanged;
        }
    }

    private void OnBookmarksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (BookmarkItem newItem in e.NewItems)
            {
                newItem.PropertyChanged -= OnBookmarkPropertyChanged;
                newItem.PropertyChanged += OnBookmarkPropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (BookmarkItem oldItem in e.OldItems)
            {
                oldItem.PropertyChanged -= OnBookmarkPropertyChanged;
            }
        }
        Save();
    }

    private void OnBookmarkPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
    }
}

public class BookmarkItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _url = "";

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (_url == value) return;
            _url = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}