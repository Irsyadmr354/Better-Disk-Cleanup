using BetterDiskCleanup.Core.StorageAnalyzer;

namespace BetterDiskCleanup.Infrastructure.StorageAnalyzer;

/// <summary>
/// Maps file extensions to <see cref="StorageFileTypeCategory"/>.
/// Extension comparisons are case-insensitive.
/// </summary>
public static class FileTypeClassifier
{
    private static readonly Dictionary<string, StorageFileTypeCategory> ExtensionMap =
        BuildMap(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the <see cref="StorageFileTypeCategory"/> for the given file extension.
    /// </summary>
    /// <param name="extension">
    /// File extension including the leading dot (e.g. ".mp4").
    /// Pass an empty string or null for files without an extension.
    /// </param>
    public static StorageFileTypeCategory Classify(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return StorageFileTypeCategory.Other;
        }

        return ExtensionMap.TryGetValue(extension, out var category)
            ? category
            : StorageFileTypeCategory.Other;
    }

    private static Dictionary<string, StorageFileTypeCategory> BuildMap(StringComparer comparer)
    {
        var map = new Dictionary<string, StorageFileTypeCategory>(comparer);

        // Video
        foreach (var ext in new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp" })
            map[ext] = StorageFileTypeCategory.Video;

        // Audio
        foreach (var ext in new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus", ".ape" })
            map[ext] = StorageFileTypeCategory.Audio;

        // Image
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico", ".tiff", ".tif", ".psd", ".raw", ".heic", ".heif" })
            map[ext] = StorageFileTypeCategory.Image;

        // Document
        foreach (var ext in new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf", ".odt", ".ods", ".odp", ".md", ".epub" })
            map[ext] = StorageFileTypeCategory.Document;

        // Archive
        foreach (var ext in new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tar.gz", ".iso", ".dmg" })
            map[ext] = StorageFileTypeCategory.Archive;

        // Executable
        foreach (var ext in new[] { ".exe", ".msi", ".dll", ".bat", ".cmd", ".ps1", ".sh", ".appx", ".msix" })
            map[ext] = StorageFileTypeCategory.Executable;

        // System
        foreach (var ext in new[] { ".sys", ".log", ".tmp", ".bak", ".cab", ".dat", ".ini", ".cfg", ".reg", ".cat" })
            map[ext] = StorageFileTypeCategory.System;

        return map;
    }
}
