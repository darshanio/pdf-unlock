using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PdfUnlock.Services;
using PdfUnlock.ViewModels;
using PdfUnlock.Views;

namespace PdfUnlock;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SettingsStore();
            var settings = store.Load();
            var model = new MainViewModel(store, settings);

            // A shell invocation arrives as argv: the operating system hands us the files the
            // user right-clicked. They preload the batch and wait, rather than running
            // immediately. See CONTEXT.md, "Shell Invocation".
            var pdfs = (desktop.Args ?? [])
                .Where(argument => argument.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                                   && File.Exists(argument))
                .ToList();
            if (pdfs.Count > 0)
                model.AddFilesCommand.Execute(pdfs);

            var window = new MainWindow
            {
                DataContext = model,
                Width = settings.WindowWidth,
                Height = settings.WindowHeight,
            };
            window.Closing += (_, _) =>
            {
                settings.WindowWidth = window.Width;
                settings.WindowHeight = window.Height;
                store.Save(settings);
            };
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}