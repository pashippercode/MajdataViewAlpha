using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MajdataEdit;

namespace MajdataEdit.Avalonia;

public partial class MainWindow : Window
{
    private string? _path;

    public MainWindow()
    {
        InitializeComponent();
        OpenButton.Click += OnOpen;
        SaveButton.Click += OnSave;
        AutoOnsetButton.Click += OnAutoOnset;
        Status("Ready — lightweight cross-platform editor");
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open chart",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Chart") { Patterns = new[] { "*.txt", "*.maimai", "*" } }
            }
        });
        if (files.Count == 0)
            return;

        _path = files[0].TryGetLocalPath();
        if (_path == null)
            return;

        Editor.Text = await File.ReadAllTextAsync(_path, Encoding.UTF8);
        Status($"Opened {_path}");
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save chart",
                DefaultExtension = "txt",
                SuggestedFileName = "maidata.txt"
            });
            if (file?.TryGetLocalPath() is not { } picked)
                return;
            _path = picked;
        }

        await File.WriteAllTextAsync(_path, Editor.Text ?? string.Empty, Encoding.UTF8);
        Status($"Saved {_path}");
    }

    private async void OnAutoOnset(object? sender, RoutedEventArgs e)
    {
        var audioFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick audio for auto-onset",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Audio") { Patterns = new[] { "*.wav", "*.ogg", "*.mp3", "*.flac", "*" } }
            }
        });
        if (audioFiles.Count == 0)
            return;

        var audioPath = audioFiles[0].TryGetLocalPath();
        if (audioPath == null)
            return;

        AutoOnsetButton.IsEnabled = false;
        try
        {
            double? bpm = double.TryParse(BpmBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
                ? b : null;
            var result = await AutoOnsetRunner.GenerateAsync(
                new AutoOnsetRequest(audioPath, LevelBox.Text ?? "10", bpm, null, 0.55, Path.GetFileNameWithoutExtension(audioPath)),
                Status,
                CancellationToken.None);
            Editor.Text = result.Chart;
            Status($"Auto-onset: {result.PredictedOnsets} onsets @ {result.Bpm:F1} BPM");
        }
        catch (Exception ex)
        {
            Status(ex.Message);
        }
        finally
        {
            AutoOnsetButton.IsEnabled = true;
        }
    }

    private void Status(string message) => StatusText.Text = message;
}
