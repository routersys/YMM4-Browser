using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace YMM4Browser.ViewModel
{
    public interface IWebViewService
    {
        bool CanGoBack { get; }
        bool CanGoForward { get; }
        string? DocumentTitle { get; }
        Uri? Source { get; }

        event EventHandler<CoreWebView2NavigationStartingEventArgs>? NavigationStarting;
        event EventHandler<CoreWebView2NavigationCompletedEventArgs>? NavigationCompleted;
        event EventHandler<CoreWebView2SourceChangedEventArgs>? SourceChanged;
        event EventHandler<object>? HistoryChanged;
        event EventHandler<CoreWebView2InitializationCompletedEventArgs>? CoreWebView2InitializationCompleted;

        void Navigate(string url);
        void GoBack();
        void GoForward();
        void Reload();
        void Stop();
        Task CapturePreviewAsync(Stream stream);
        Task<string> ExecuteScriptAsync(string script);
    }
}