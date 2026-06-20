using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Infrastructure.LargeFiles;

namespace BetterDiskCleanup.Tests.LargeFiles;

public sealed class LargeFileCategoryMappingTests
{
    [Theory]
    [InlineData(".mp4", FileCategory.Video)]
    [InlineData(".mkv", FileCategory.Video)]
    [InlineData(".avi", FileCategory.Video)]
    [InlineData(".mov", FileCategory.Video)]
    [InlineData(".wmv", FileCategory.Video)]
    [InlineData(".flv", FileCategory.Video)]
    [InlineData(".webm", FileCategory.Video)]
    public void CategorizeFile_VideoExtensions_ReturnsVideo(string extension, FileCategory expected)
    {
        Assert.Equal(expected, LargeFileScanner.CategorizeFile(extension));
    }

    [Theory]
    [InlineData(".iso", FileCategory.DiskImage)]
    [InlineData(".vhd", FileCategory.DiskImage)]
    [InlineData(".vhdx", FileCategory.DiskImage)]
    [InlineData(".img", FileCategory.DiskImage)]
    public void CategorizeFile_DiskImageExtensions_ReturnsDiskImage(string extension, FileCategory expected)
    {
        Assert.Equal(expected, LargeFileScanner.CategorizeFile(extension));
    }

    [Theory]
    [InlineData(".zip", FileCategory.Archive)]
    [InlineData(".rar", FileCategory.Archive)]
    [InlineData(".7z", FileCategory.Archive)]
    [InlineData(".tar", FileCategory.Archive)]
    [InlineData(".gz", FileCategory.Archive)]
    public void CategorizeFile_ArchiveExtensions_ReturnsArchive(string extension, FileCategory expected)
    {
        Assert.Equal(expected, LargeFileScanner.CategorizeFile(extension));
    }

    [Theory]
    [InlineData(".pdf", FileCategory.Document)]
    [InlineData(".doc", FileCategory.Document)]
    [InlineData(".docx", FileCategory.Document)]
    [InlineData(".xls", FileCategory.Document)]
    [InlineData(".xlsx", FileCategory.Document)]
    [InlineData(".ppt", FileCategory.Document)]
    [InlineData(".pptx", FileCategory.Document)]
    public void CategorizeFile_DocumentExtensions_ReturnsDocument(string extension, FileCategory expected)
    {
        Assert.Equal(expected, LargeFileScanner.CategorizeFile(extension));
    }

    [Theory]
    [InlineData(".exe", FileCategory.Other)]
    [InlineData(".dll", FileCategory.Other)]
    [InlineData(".txt", FileCategory.Other)]
    [InlineData(".log", FileCategory.Other)]
    [InlineData("", FileCategory.Other)]
    public void CategorizeFile_UnknownExtensions_ReturnsOther(string extension, FileCategory expected)
    {
        Assert.Equal(expected, LargeFileScanner.CategorizeFile(extension));
    }

    [Fact]
    public void CategorizeFile_CaseInsensitive()
    {
        Assert.Equal(FileCategory.Video, LargeFileScanner.CategorizeFile(".MP4"));
        Assert.Equal(FileCategory.Video, LargeFileScanner.CategorizeFile(".Mp4"));
        Assert.Equal(FileCategory.Archive, LargeFileScanner.CategorizeFile(".ZIP"));
    }
}
