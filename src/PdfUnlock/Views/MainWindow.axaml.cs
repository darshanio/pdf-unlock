using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfUnlock.ViewModels;

namespace PdfUnlock.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType PdfFileType = new("PDF documents")
    {
        Patterns = ["*.pdf"],
        AppleUniformTypeIdentifiers = ["com.adobe.pdf"],
        MimeTypes = ["application/pdf"],
    };

    public MainWindow()
    {
        InitializeComponent();

        ChooseFilesButton.Click += OnChooseFiles;
        SettingsButton.Click += OnOpenSettings;
        RevealDefaultToggle.IsCheckedChanged += OnRevealToggled;

        // Drag and drop is how people will actually use this, so it is wired from the start.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel model)
                model.RunFinished += OfferToSavePasswords;
        };

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    private async void OnChooseFiles(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose PDFs to decrypt",
            AllowMultiple = true,
            FileTypeFilter = [PdfFileType],
        });

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
            Model?.AddFilesCommand.Execute(paths);
    }

    private async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        if (Model is null)
            return;

        var settings = new SettingsWindow
        {
            DataContext = new SettingsViewModel(Model.SettingsStore, Model.Settings, Model.IsRunning, Model.Passwords),
        };
        // Modal to the main window: a batch should not be edited underneath a
        // half-changed setting.
        await settings.ShowDialog(this);

        Model.SettingsStore.Save(Model.Settings);
        Model.ReapplyRules();
        Model.RedetectQpdfCommand.Execute(null);
    }

    /// <summary>Offers to remember the passwords a run proved correct. Shown only when
    /// there is something worth offering.</summary>
    public async void OfferToSavePasswords()
    {
        if (Model is null || Model.PendingSaves.Count == 0)
            return;

        var prompt = new SavePasswordsWindow
        {
            DataContext = new SavePasswordsViewModel(Model.Passwords, Model.PendingSaves),
        };
        await prompt.ShowDialog(this);
        Model.PendingSaves.Clear();
        Model.ReapplyRules();
    }

    private void OnRevealToggled(object? sender, RoutedEventArgs e) =>
        DefaultPasswordBox.PasswordChar = RevealDefaultToggle.IsChecked == true ? '\0' : '•';

    private static void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.TryGetFiles() is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var items = e.DataTransfer.TryGetFiles();
        if (items is null)
            return;

        var paths = new List<string>();
        foreach (var item in items)
        {
            var path = item.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }

        if (paths.Count > 0)
            Model?.AddFilesCommand.Execute(paths);
    }
}
