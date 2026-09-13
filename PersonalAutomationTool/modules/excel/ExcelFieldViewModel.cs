using System.Collections.Generic;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Excel
{
    public class ExcelFieldViewModel : ViewModelBase
    {
        private string _fieldName = string.Empty;
        public string FieldName
        {
            get => _fieldName;
            set => SetProperty(ref _fieldName, value);
        }

        private string _fieldValue = string.Empty;
        public string FieldValue
        {
            get => _fieldValue;
            set => SetProperty(ref _fieldValue, value);
        }

        private bool _isComboBox;
        public bool IsComboBox
        {
            get => _isComboBox;
            set
            {
                if (SetProperty(ref _isComboBox, value))
                {
                    OnPropertyChanged(nameof(IsTextBox));
                    OnPropertyChanged(nameof(IsClosedComboBox));
                }
            }
        }

        private bool _isEditableComboBox;

        /// <summary>
        /// Vero per un campo la cui ComboBox deve restare digitabile con ricerca testuale (oggi solo
        /// "Descrizione LRU", il catalogo componenti da 103 voci: vedi
        /// <see cref="ReportOptionsCatalog.IsSearchableComponentField"/>). Le altre ComboBox restano
        /// chiuse — <see cref="IsClosedComboBox"/> — per non permettere di scrivere nel report un
        /// valore fuori dalla lista concordata.
        /// </summary>
        public bool IsEditableComboBox
        {
            get => _isEditableComboBox;
            set
            {
                if (SetProperty(ref _isEditableComboBox, value))
                {
                    OnPropertyChanged(nameof(IsClosedComboBox));
                }
            }
        }

        private List<string> _options = [];
        public List<string> Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }

        private bool _isImportant;
        public bool IsImportant
        {
            get => _isImportant;
            set => SetProperty(ref _isImportant, value);
        }

        public bool IsTextBox => !IsComboBox;

        /// <summary>Vero per una ComboBox vincolata alla sola selezione dalla lista (tutte tranne quelle con <see cref="IsEditableComboBox"/>).</summary>
        public bool IsClosedComboBox => IsComboBox && !IsEditableComboBox;
    }
}
