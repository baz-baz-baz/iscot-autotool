using System.Collections.ObjectModel;
using System.IO;

namespace PersonalAutomationTool.Modules.Pdf.Models
{
    public class TrainCardModel
    {
        public string Title { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsND { get; set; }

        public ObservableCollection<FolderItemModel> Children { get; set; } = [];
    }

    public class FolderItemModel
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public string Extension { get; set; } = string.Empty;
        public bool IsNC { get; set; }
    }
}
