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
        try
        {
            BrowserSettings.InitializeSettings();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"YMM4Browser プラグインの初期化中にエラーが発生しました。\n\nエラー: {ex.Message}\n\n設定ファイルが破損している可能性があります。",
                "YMM4Browser 初期化エラー",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}