using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AniVault.Utilities;

/// <summary>
/// Loads image files into frozen, UI-thread-safe <see cref="ImageSource"/> objects without
/// blocking the caller, with a small bounded in-memory cache so re-visiting a page does not
/// re-read and re-decode the same files. Missing or corrupt files return null.
/// </summary>
public static class ImageLoading
{
    private const int MaxCacheEntries = 160;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, ImageSource> Cache = new();
    private static readonly LinkedList<string> Lru = new();

    public static async Task<ImageSource?> LoadAsync(string? absolutePath, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return null;
        }

        string key;
        try
        {
            key = $"{absolutePath}|{decodePixelWidth}|{File.GetLastWriteTimeUtc(absolutePath).Ticks}";
        }
        catch (IOException)
        {
            return null;
        }

        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                Touch(key);
                return cached;
            }
        }

        var image = await Task.Run(() => Decode(absolutePath, decodePixelWidth));
        if (image is null)
        {
            return null;
        }

        lock (Gate)
        {
            if (!Cache.ContainsKey(key))
            {
                Cache[key] = image;
                Lru.AddFirst(key);
                Evict();
            }
        }

        return image;
    }

    /// <summary>Writes a downscaled JPEG copy of <paramref name="sourceAbsolutePath"/> to <paramref name="destinationAbsolutePath"/>.</summary>
    public static async Task<bool> SaveThumbnailAsync(string sourceAbsolutePath, string destinationAbsolutePath, int width)
    {
        return await Task.Run(() =>
        {
            try
            {
                var source = Decode(sourceAbsolutePath, width);
                if (source is not BitmapSource bitmap)
                {
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationAbsolutePath)!);

                var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var stream = new FileStream(destinationAbsolutePath, FileMode.Create);
                encoder.Save(stream);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        });
    }

    private static ImageSource? Decode(string absolutePath, int decodePixelWidth)
    {
        try
        {
            var bytes = File.ReadAllBytes(absolutePath);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePixelWidth > 0)
            {
                image.DecodePixelWidth = decodePixelWidth;
            }

            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Touch(string key)
    {
        Lru.Remove(key);
        Lru.AddFirst(key);
    }

    private static void Evict()
    {
        while (Cache.Count > MaxCacheEntries && Lru.Last is { } last)
        {
            Cache.Remove(last.Value);
            Lru.RemoveLast();
        }
    }
}
