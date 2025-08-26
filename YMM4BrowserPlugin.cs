using System.Reflection;
using YukkuriMovieMaker.Plugin;
using YMM4Browser.View;
using YMM4Browser.ViewModel;

namespace YMM4Browser;

[PluginDetails(
    AuthorName = "routersys",
    ContentId = ""
)]
public class YMM4BrowserPlugin : IToolPlugin
{
    public string Name { get; } = "YMM4 ブラウザ";

    public PluginDetailsAttribute Details =>
        GetType().GetCustomAttribute<PluginDetailsAttribute>() ?? new();

    public Type ViewModelType { get; } = typeof(BrowserViewModel);
    public Type ViewType { get; } = typeof(BrowserView);

    public YMM4BrowserPlugin()
    {
        BrowserSettings.InitializeSettings();
    }
}