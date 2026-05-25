using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Excel
{
    public class ExcelViewModel : ViewModelBase, IDisposable
    {
        public List<string> Trains { get; } = new List<string>
        {
            "E404P",
            "ETR700",
            "ETR1000",
            "ETR1000 I-F"
        };

        private string? _selectedTrain;
        public string? SelectedTrain
        {
            get => _selectedTrain;
            set
            {
                if (SetProperty(ref _selectedTrain, value))
                {
                    UpdateFolders();
                }
            }
        }

        public ObservableCollection<string> AvailableFolders { get; } = new ObservableCollection<string>();

        private string? _selectedFolder;
        public string? SelectedFolder
        {
            get => _selectedFolder;
            set => SetProperty(ref _selectedFolder, value);
        }

        public ExcelViewModel()
        {
            // Set default selection
            SelectedTrain = Trains.FirstOrDefault();

            // Subscribe to folder changes
            AppWatcher.OnLogDumpFolderChanged += AppWatcher_OnLogDumpFolderChanged;
        }

        private void AppWatcher_OnLogDumpFolderChanged()
        {
            // Re-evaluates folders since something changed in LOG & DUMP
            UpdateFolders();
        }

        private void UpdateFolders()
        {
            AvailableFolders.Clear();

            if (string.IsNullOrWhiteSpace(SelectedTrain))
            {
                return;
            }

            try
            {
                string logPath = AppConfig.LogAndDumpFolder;
                if (!Directory.Exists(logPath))
                {
                    return;
                }

                // Get all top-level directories
                var directories = Directory.GetDirectories(logPath);

                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(dirName)) continue;

                    // Filter based on the selected train name
                    if (dirName.Contains(SelectedTrain, StringComparison.OrdinalIgnoreCase))
                    {
                        AvailableFolders.Add(dirName);
                    }
                }
                
                // Optional: Select the first one automatically if available
                if (AvailableFolders.Count > 0)
                {
                    SelectedFolder = AvailableFolders[0];
                }
                else
                {
                    SelectedFolder = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating folders: {ex.Message}");
            }
        }

        public void Dispose()
        {
            AppWatcher.OnLogDumpFolderChanged -= AppWatcher_OnLogDumpFolderChanged;
        }
    }
}
