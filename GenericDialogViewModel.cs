using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace YMM4Browser.ViewModel
{
    public class GenericDialogViewModel : INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string Message { get; set; }

        public bool ShowOkButton { get; set; }
        public bool ShowCancelButton { get; set; }
        public bool ShowYesButton { get; set; }
        public bool ShowNoButton { get; set; }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand YesCommand { get; }
        public ICommand NoCommand { get; }

        public enum ButtonSet
        {
            Ok,
            OkCancel,
            YesNo,
            YesNoCancel
        }

        public GenericDialogViewModel(string title, string message, ButtonSet buttons = ButtonSet.Ok)
        {
            Title = title;
            Message = message;

            ShowOkButton = buttons == ButtonSet.Ok || buttons == ButtonSet.OkCancel;
            ShowCancelButton = buttons == ButtonSet.OkCancel || buttons == ButtonSet.YesNoCancel;
            ShowYesButton = buttons == ButtonSet.YesNo || buttons == ButtonSet.YesNoCancel;
            ShowNoButton = buttons == ButtonSet.YesNo || buttons == ButtonSet.YesNoCancel;

            OkCommand = new RelayCommand(p => CloseDialog(p, true));
            CancelCommand = new RelayCommand(p => CloseDialog(p, false));
            YesCommand = new RelayCommand(p => CloseDialog(p, true));
            NoCommand = new RelayCommand(p => CloseDialog(p, false));
        }

        private void CloseDialog(object parameter, bool result)
        {
            if (parameter is Window window)
            {
                window.DialogResult = result;
                window.Close();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}