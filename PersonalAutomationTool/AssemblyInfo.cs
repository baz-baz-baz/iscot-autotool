using System.Runtime.CompilerServices;
using System.Windows;

// Permette a PersonalAutomationTool.Tests di chiamare i membri "internal" (es. EmailService.BuildHtmlBody
// per il golden-file test) senza doverli rendere public: nessun cambiamento di superficie per il resto dell'app.
[assembly: InternalsVisibleTo("PersonalAutomationTool.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
