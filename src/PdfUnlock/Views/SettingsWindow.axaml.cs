using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfUnlock.ViewModels;

namespace PdfUnlock.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        ChooseQpdfButton.Click += OnChooseQpdf;
    }

    private SettingsViewModel? Model => DataContext as SettingsViewModel;

    private async void OnChooseQpdf(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the qpdf program",
            AllowMultiple = false,
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path) || Model is null)
            return;

        // Record the choice, then re-resolve: a path that is too old or is not qpdf is
        // rejected by resolution, and the previous answer stands.
        Model.Settings.QpdfPath = path;
        Model.RedetectQpdfCommand.Execute(null);
    }
}
