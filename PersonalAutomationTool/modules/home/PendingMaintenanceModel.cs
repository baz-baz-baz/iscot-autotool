using System;
using System.Collections.Generic;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Home
{
    public class PendingMaintenanceModel : ViewModelBase
    {
        public string TipoTreno { get; set; } = string.Empty;
        public int NumeroCartelle { get; set; }
        public string Data { get; set; } = string.Empty;
        public int Giorni { get; set; }
        public string Percorso { get; set; } = string.Empty;
        
        private bool _isExpanded;
        public bool IsExpanded 
        { 
            get => _isExpanded; 
            set => SetProperty(ref _isExpanded, value); 
        }

        public List<string> SubFolders { get; set; } = [];
    }
}
