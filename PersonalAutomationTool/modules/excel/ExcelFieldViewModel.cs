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
    }
}
