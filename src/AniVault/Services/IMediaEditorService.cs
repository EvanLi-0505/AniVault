using System.Threading.Tasks;
using AniVault.Models;

namespace AniVault.Services;

/// <summary>
/// Opens the modal media editor window. Implemented in the composition root so that
/// ViewModels never reference Views directly.
/// </summary>
public interface IMediaEditorService
{
    /// <summary>Opens the editor to add a new item of the given type. Returns true if the user saved.</summary>
    Task<bool> AddNewAsync(MediaType mediaType);

    /// <summary>Opens the editor for an existing item. Returns true if the user saved changes.</summary>
    Task<bool> EditAsync(int mediaId);
}
