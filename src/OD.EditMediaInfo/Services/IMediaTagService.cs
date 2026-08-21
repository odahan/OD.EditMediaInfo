using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

public interface IMediaTagService
{
    MediaInfo Read(string filePath);
    void Save(EditOptions options);
    void Clear(string filePath, bool noBackup, bool keepMtime);
}
