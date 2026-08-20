using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfUnlock.ViewModels;

namespace PdfUnlock.Views;

public partial class SavePasswordsWindow : Window
{
    public SavePasswordsWindow()
    {
        InitializeComponent();
        NotNowButton.Click += (_, _) => Close();
        SaveButton.Click += OnSave;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SavePasswordsViewModel model)
            return;

        model.SaveSelected();
        // Left open briefly so a per-folder failure is actually read rather than flashing
        // past; closing is the user's decision.
        SaveButton.IsEnabled = false;
        NotNowButton.Content = "Close";
    }
}
