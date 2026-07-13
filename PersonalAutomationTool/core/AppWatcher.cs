using System;
using System.IO;
using System.Windows;

namespace PersonalAutomationTool.Core
{
    public static class AppWatcher
    {
        private static FileSystemWatcher? _watcher;
        private static System.Threading.Timer? _debounceTimer;
        private static readonly object _timerLock = new();

        public static event Action? OnLogDumpFolderChanged;

        public static void Initialize()
        {
            try
            {
                string folder = AppConfig.LogAndDumpFolder;
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                _watcher = new FileSystemWatcher(folder);
                _watcher.NotifyFilter = NotifyFilters.FileName 
                                      | NotifyFilters.DirectoryName 
                                      | NotifyFilters.CreationTime
                                      | NotifyFilters.LastWrite;

                _watcher.IncludeSubdirectories = true;

                _watcher.Created += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnChanged;

                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppWatcher Error: {ex.Message}");
            }
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (_timerLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(DebounceTimerCallback, null, 300, System.Threading.Timeout.Infinite);
            }
        }

        private static void DebounceTimerCallback(object? state)
        {
            lock (_timerLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            // Dispatch directly to UI thread
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                OnLogDumpFolderChanged?.Invoke();
            });
        }
    }
}
