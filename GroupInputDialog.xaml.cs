using System.Windows;

namespace YMM4Browser.View
{
    public partial class GroupInputDialog : Window
    {
        public string GroupName => nameTextBox.Text;

        public GroupInputDialog(BookmarkGroup? group = null)
        {
            InitializeComponent();

            Title = group == null ? "グループ追加" : "グループ編集";
            okButton.Content = group == null ? "追加" : "保存";

            if (group != null)
            {
                nameTextBox.Text = group.Name;
            }

            nameTextBox.Focus();
            nameTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show("グループ名を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}