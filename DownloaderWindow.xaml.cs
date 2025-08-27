using System.ComponentModel;
using System.Windows;
using YMM4Browser.ViewModel;

namespace YMM4Browser.View
{
    public partial class DownloaderWindow : Window
    {
        public DownloaderWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is DownloaderViewModel vm)
            {
                vm.SaveSettings();
            }
            base.OnClosing(e);
        }
    }
}