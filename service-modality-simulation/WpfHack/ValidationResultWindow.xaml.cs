using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace WpfHack;

public partial class ValidationResultWindow
{
    // Keep the image stream alive while the window is open because WpfAnimatedGif may access the decoder later.
    private MemoryStream? _animatedImageStream;
    private string? _pendingGifPath;

    public ValidationResultWindow(string message, string title = "Info", string? gifRelativePath = null)
    {
        InitializeComponent();

        Title = title;
        MessageText.Text = message;

        if (!string.IsNullOrWhiteSpace(gifRelativePath))
        {
            var found = ResolveFirstExistingPath(gifRelativePath);
            if (!string.IsNullOrEmpty(found))
            {
                // Defer actual heavy loading until after the window has loaded/rendered
                _pendingGifPath = found;
                Loaded += ValidationResultWindow_Loaded;
            }
        }
    }

    private async void ValidationResultWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Unsubscribe so we only run once
        Loaded -= ValidationResultWindow_Loaded;

        if (!string.IsNullOrEmpty(_pendingGifPath))
        {
            // Start async loading but await it so exceptions can be observed and handled
            try
            {
                await LoadGifDeferredAsync(_pendingGifPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ValidationResultWindow: error loading GIF asynchronously: {ex}");
            }
        }
    }

    // Asynchronously read file bytes on a background thread, then create stream/BitmapImage on the UI thread.
    private async Task LoadGifDeferredAsync(string filePath)
    {
        // Read bytes off the UI thread (fast IO, avoids blocking the UI)
        byte[] bytes = await Task.Run(() => File.ReadAllBytes(filePath));

        // Marshal back to the UI thread and create the MemoryStream and BitmapImage there.
        await Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _animatedImageStream?.Dispose();
                _animatedImageStream = new MemoryStream(bytes);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // read image data into memory
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.StreamSource = _animatedImageStream;
                bmp.EndInit();

                if (_animatedImageStream.CanSeek) _animatedImageStream.Position = 0;

                ImageBehavior.SetAnimatedSource(GifImage, bmp);
                ImageBehavior.SetRepeatBehavior(GifImage, new RepeatBehavior(1));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ValidationResultWindow: deferred load failed for '{filePath}': {ex}");
            }
        }), DispatcherPriority.Background);
    }

    // Return the first existing path from a set of likely locations (absolute or relative)
    private static string? ResolveFirstExistingPath(string relativeOrAbsolute)
    {
        if (Path.IsPathRooted(relativeOrAbsolute) && File.Exists(relativeOrAbsolute))
            return relativeOrAbsolute;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var workDir = Environment.CurrentDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, relativeOrAbsolute),
            Path.Combine(workDir, relativeOrAbsolute),
            relativeOrAbsolute
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return null;
    }

    // Ensure we dispose the kept MemoryStream when the window is closed.
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            _animatedImageStream?.Dispose();
            _animatedImageStream = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ValidationResultWindow: error disposing animated stream: {ex}");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}