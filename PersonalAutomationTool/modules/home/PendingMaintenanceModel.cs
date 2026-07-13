using System;
using System.Collections.Generic;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Home
{
    public class PendingMaintenanceModel : ViewModelBase
    {
        private string _tipoTreno = string.Empty;
        public string TipoTreno
        {
            get => _tipoTreno;
            set => SetProperty(ref _tipoTreno, value);
        }

        private int _numeroCartelle;
        public int NumeroCartelle
        {
            get => _numeroCartelle;
            set => SetProperty(ref _numeroCartelle, value);
        }

        private string _data = string.Empty;
        public string Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        private int _giorni;
        public int Giorni
        {
            get => _giorni;
            set => SetProperty(ref _giorni, value);
        }

        private string _percorso = string.Empty;
        public string Percorso
        {
            get => _percorso;
            set => SetProperty(ref _percorso, value);
        }
        
        private bool _isExpanded;
        public bool IsExpanded 
        { 
            get => _isExpanded; 
            set => SetProperty(ref _isExpanded, value); 
        }

        private List<string> _subFolders = [];
        public List<string> SubFolders
        {
            get => _subFolders;
            set => SetProperty(ref _subFolders, value);
        }
    }
}
