using System.Linq;
using System.Windows;

namespace YMM4Browser.View
{
    public partial class BookmarkInputDialog : Window
    {
        public string BookmarkName => nameTextBox.Text;
        public string BookmarkUrl => urlTextBox.Text;
        public BookmarkGroup? SelectedGroup => groupComboBox.SelectedItem as BookmarkGroup;

        public BookmarkInputDialog(BookmarkItem? bookmark = null)
        {
            InitializeComponent();

            Title = bookmark == null ? "ブックマーク追加" : "ブックマーク編集";
            okButton.Content = bookmark == null ? "追加" : "保存";

            groupComboBox.ItemsSource = BrowserSettings.Default.BookmarkGroups;

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
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text) || string.IsNullOrWhiteSpace(urlTextBox.Text))
            {
                MessageBox.Show("名前とURLを入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}