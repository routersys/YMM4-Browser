using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YMM4Browser.ViewModel;

namespace YMM4Browser.View
{
    public partial class SettingsView : UserControl
    {
        private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;
        private Point _startPoint;
        private bool _isDragging = false;

        public SettingsView()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateGroupFilter();
            BrowserSettings.Default.BookmarkGroups.CollectionChanged += (s, ev) => UpdateGroupFilter();
        }

        private void UpdateGroupFilter()
        {
            var items = new List<object> { new { Name = "すべて", Group = (BookmarkGroup?)null } };
            items.AddRange(BrowserSettings.Default.BookmarkGroups.Select(group => new { Name = group.Name, Group = group }));

            var selectedGroup = GroupFilterComboBox.SelectedItem as dynamic;
            GroupFilterComboBox.ItemsSource = items;

            if (selectedGroup != null)
            {
                var newSelection = items.FirstOrDefault(i => (i as dynamic).Group?.Id == selectedGroup.Group?.Id);
                GroupFilterComboBox.SelectedItem = newSelection ?? items.First();
            }
            else
            {
                GroupFilterComboBox.SelectedIndex = 0;
            }
        }

        private void AddBookmarkButton_Click(object sender, RoutedEventArgs e) => ViewModel?.AddBookmark();
        private void RemoveBookmarkButton_Click(object sender, RoutedEventArgs e) => ViewModel?.RemoveSelectedBookmarks(BookmarkListView.SelectedItems.Cast<object>());
        private void MoveBookmarkUpButton_Click(object sender, RoutedEventArgs e) => ViewModel?.MoveBookmarkUp(BookmarkListView.SelectedItem);
        private void MoveBookmarkDownButton_Click(object sender, RoutedEventArgs e) => ViewModel?.MoveBookmarkDown(BookmarkListView.SelectedItem);
        private void AddBookmarkGroupButton_Click(object sender, RoutedEventArgs e) => ViewModel?.AddBookmarkGroup();
        private void RemoveBookmarkGroupButton_Click(object sender, RoutedEventArgs e) => ViewModel?.RemoveBookmarkGroup(BookmarkGroupListView.SelectedItem);
        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearHistory();

        private void GroupFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel != null && GroupFilterComboBox.SelectedItem != null)
            {
                dynamic selectedItem = GroupFilterComboBox.SelectedItem;
                ViewModel.SelectedBookmarkGroup = selectedItem.Group as BookmarkGroup;
            }
        }

        private void BookmarkListViewItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel != null && sender is ListViewItem item && item.DataContext is BookmarkItemViewModel viewModel)
            {
                var contextMenu = new ContextMenu();
                contextMenu.Items.Add(new MenuItem { Header = "編集", Command = ViewModel.EditBookmarkCommand, CommandParameter = viewModel });
                contextMenu.Items.Add(new MenuItem { Header = "削除", Command = ViewModel.RemoveBookmarkFromMenuCommand, CommandParameter = viewModel });
                contextMenu.PlacementTarget = item;
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void BookmarkGroupListViewItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel != null && sender is ListViewItem item && item.DataContext is BookmarkGroup group)
            {
                var contextMenu = new ContextMenu();
                contextMenu.Items.Add(new MenuItem { Header = "編集", Command = ViewModel.EditBookmarkGroupCommand, CommandParameter = group });
                contextMenu.Items.Add(new MenuItem { Header = "削除", Command = ViewModel.RemoveBookmarkGroupFromMenuCommand, CommandParameter = group });
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
            if (!_isDragging && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
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
            if (sender is not ListViewItem targetItem || e.Data.GetData(typeof(BookmarkItemViewModel)) is not BookmarkItemViewModel draggedItem || targetItem.DataContext is not BookmarkItemViewModel targetItemData || draggedItem == targetItemData || ViewModel == null)
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
                var oldGroupBookmarks = ViewModel.Bookmarks.Where(b => b.GroupId == originalGroupId).OrderBy(b => b.Order).ToList();
                oldGroupBookmarks.Remove(draggedBookmark);
                for (int i = 0; i < oldGroupBookmarks.Count; i++) oldGroupBookmarks[i].Order = i;
            }

            var newGroupBookmarks = ViewModel.Bookmarks.Where(b => b.GroupId == newGroupId).OrderBy(b => b.Order).ToList();
            newGroupBookmarks.Remove(draggedBookmark);
            int targetIndex = newGroupBookmarks.IndexOf(targetBookmark);
            var targetRect = VisualTreeHelper.GetDescendantBounds(targetItem);
            var dropPoint = e.GetPosition(targetItem);
            if (dropPoint.Y > targetRect.Height / 2) targetIndex++;
            newGroupBookmarks.Insert(targetIndex, draggedBookmark);
            for (int i = 0; i < newGroupBookmarks.Count; i++) newGroupBookmarks[i].Order = i;

            ViewModel.FilterBookmarks();
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
            if (sender is ListViewItem item) item.Background = Brushes.Transparent;
        }

        private void BookmarkListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(BookmarkItemViewModel))) e.Handled = true;
        }

        private void BookmarkListView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(BookmarkItemViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
        }
    }
}