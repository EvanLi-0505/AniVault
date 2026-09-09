using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Services.Export.Writers;

namespace AniVault.Services.Export;

/// <summary>
/// Exports the whole local library to a shareable document. Currently Markdown only;
/// the writer is pluggable so other formats (HTML, CSV) can be added without touching callers.
/// Offline — reads the local database and writes a file the user chose.
/// </summary>
public interface ILibraryExportService
{
    /// <summary>Writes a Markdown summary of every media item to <paramref name="destinationFilePath"/>.</summary>
    Task ExportMarkdownAsync(string destinationFilePath, CancellationToken cancellationToken = default);
}

public sealed class LibraryExportService : ILibraryExportService
{
    private readonly IMediaService _mediaService;
    private readonly MarkdownLibraryWriter _markdownWriter;

    public LibraryExportService(IMediaService mediaService)
    {
        _mediaService = mediaService;
        _markdownWriter = new MarkdownLibraryWriter();
    }

    public async Task ExportMarkdownAsync(string destinationFilePath, CancellationToken cancellationToken = default)
    {
        var media = await _mediaService.GetAllDetailedAsync(cancellationToken);
        var markdown = _markdownWriter.Write(media);

        var directory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(destinationFilePath, markdown, cancellationToken);
    }
}
