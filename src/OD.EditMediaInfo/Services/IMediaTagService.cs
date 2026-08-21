using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

/// <summary>Defines operations for reading and editing media tags.</summary>
public interface IMediaTagService
{
    /// <summary>Reads media information from a file.</summary>
    MediaInfo Read(string filePath);
    /// <summary>Applies a tag edit request.</summary>
    void Save(EditOptions options);
    /// <summary>Removes all tags from a media file.</summary>
    void Clear(string filePath, bool noBackup, bool keepMtime);
}
