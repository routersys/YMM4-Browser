using System.Windows;

namespace YMM4Browser.View
{
    public partial class GenericDialog : Window
    {
        public GenericDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }
    }
}