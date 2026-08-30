using System.CommandLine;
using OD.EditMediaInfo.Commands;
using OD.EditMediaInfo.Services;
using Spectre.Console;

var root = new RootCommand("odmedia — media info viewer & editor");

var fileArg = new Argument<string?>("file") { Arity = ArgumentArity.ZeroOrOne, Description = "Media file to open in interactive mode" };
root.AddArgument(fileArg);

// Info command
var infoCmd = new Command("info", "Show media technical info and tags");
var infoFileArg = new Argument<string>("file", "Media file");
var infoJsonOpt = new Option<bool>("--json", "Output JSON instead of tables");
infoCmd.AddArgument(infoFileArg);
infoCmd.AddOption(infoJsonOpt);
infoCmd.SetHandler((context) =>
{
    var file = context.ParseResult.GetValueForArgument(infoFileArg);
    var json = context.ParseResult.GetValueForOption(infoJsonOpt);
    return InfoCommand.RunAsync(file, json);
});

// Set command
var setCmd = new Command("set", "Edit media tags");
var setFileArg = new Argument<string>("file", "Media file");
var titleOpt = new Option<string?>("--title", "Title");
var artistOpt = new Option<string?>("--artist", "Artist(s), ; separated");
var albumOpt = new Option<string?>("--album", "Album");
var albumArtistOpt = new Option<string?>("--album-artist", "Album artist(s)");
var genreOpt = new Option<string?>("--genre", "Genre(s)");
var yearOpt = new Option<uint?>("--year", "Year");
var trackOpt = new Option<uint?>("--track", "Track number");
var trackCountOpt = new Option<uint?>("--track-count", "Total tracks");
var discOpt = new Option<uint?>("--disc", "Disc number");
var discCountOpt = new Option<uint?>("--disc-count", "Total discs");
var commentOpt = new Option<string?>("--comment", "Comment");
var composerOpt = new Option<string?>("--composer", "Composer(s)");
var copyrightOpt = new Option<string?>("--copyright", "Copyright");
var publisherOpt = new Option<string?>("--publisher", "Publisher / Label");
var coverOpt = new Option<string?>("--cover", "Cover image path");
var coverTypeOpt = new Option<string?>("--cover-type", "Cover type: front, back, other");
var removeCoverOpt = new Option<bool>("--remove-cover", "Remove all covers");
var coverDescOpt = new Option<string?>("--cover-desc", "Cover description");
var noBackupOpt = new Option<bool>("--no-backup", "Do not create backup");
var keepMtimeOpt = new Option<bool>("--keep-mtime", "Preserve file modification time");
var dryRunOpt = new Option<bool>("--dry-run", "Show what would be done");
setCmd.AddArgument(setFileArg);
setCmd.AddOption(titleOpt);
setCmd.AddOption(artistOpt);
setCmd.AddOption(albumOpt);
setCmd.AddOption(albumArtistOpt);
setCmd.AddOption(genreOpt);
setCmd.AddOption(yearOpt);
setCmd.AddOption(trackOpt);
setCmd.AddOption(trackCountOpt);
setCmd.AddOption(discOpt);
setCmd.AddOption(discCountOpt);
setCmd.AddOption(commentOpt);
setCmd.AddOption(composerOpt);
setCmd.AddOption(copyrightOpt);
setCmd.AddOption(publisherOpt);
setCmd.AddOption(coverOpt);
setCmd.AddOption(coverTypeOpt);
setCmd.AddOption(removeCoverOpt);
setCmd.AddOption(coverDescOpt);
setCmd.AddOption(noBackupOpt);
setCmd.AddOption(keepMtimeOpt);
setCmd.AddOption(dryRunOpt);
setCmd.SetHandler((context) =>
{
    var file = context.ParseResult.GetValueForArgument(setFileArg);
    var title = context.ParseResult.GetValueForOption(titleOpt);
    var artist = context.ParseResult.GetValueForOption(artistOpt);
    var album = context.ParseResult.GetValueForOption(albumOpt);
    var albumArtist = context.ParseResult.GetValueForOption(albumArtistOpt);
    var genre = context.ParseResult.GetValueForOption(genreOpt);
    var year = context.ParseResult.GetValueForOption(yearOpt);
    var track = context.ParseResult.GetValueForOption(trackOpt);
    var trackCount = context.ParseResult.GetValueForOption(trackCountOpt);
    var disc = context.ParseResult.GetValueForOption(discOpt);
    var discCount = context.ParseResult.GetValueForOption(discCountOpt);
    var comment = context.ParseResult.GetValueForOption(commentOpt);
    var composer = context.ParseResult.GetValueForOption(composerOpt);
    var copyright = context.ParseResult.GetValueForOption(copyrightOpt);
    var publisher = context.ParseResult.GetValueForOption(publisherOpt);
    var cover = context.ParseResult.GetValueForOption(coverOpt);
    var coverType = context.ParseResult.GetValueForOption(coverTypeOpt);
    var removeCover = context.ParseResult.GetValueForOption(removeCoverOpt);
    var coverDesc = context.ParseResult.GetValueForOption(coverDescOpt);
    var noBackup = context.ParseResult.GetValueForOption(noBackupOpt);
    var keepMtime = context.ParseResult.GetValueForOption(keepMtimeOpt);
    var dryRun = context.ParseResult.GetValueForOption(dryRunOpt);
    return SetCommand.RunAsync(file, title, artist, album, albumArtist, genre, year, track, trackCount, disc, discCount, comment, composer, copyright, publisher, cover, coverType, removeCover, coverDesc, noBackup, keepMtime, dryRun);
});

// Clear command
var clearCmd = new Command("clear", "Remove all tags");
var clearFileArg = new Argument<string>("file", "Media file");
var yesOpt = new Option<bool>("--yes", "Skip confirmation");
var noBackupClearOpt = new Option<bool>("--no-backup", "Do not create backup");
var keepMtimeClearOpt = new Option<bool>("--keep-mtime", "Preserve file modification time");
clearCmd.AddArgument(clearFileArg);
clearCmd.AddOption(yesOpt);
clearCmd.AddOption(noBackupClearOpt);
clearCmd.AddOption(keepMtimeClearOpt);
clearCmd.SetHandler((context) =>
{
    var file = context.ParseResult.GetValueForArgument(clearFileArg);
    var yes = context.ParseResult.GetValueForOption(yesOpt);
    var noBackup = context.ParseResult.GetValueForOption(noBackupClearOpt);
    var keepMtime = context.ParseResult.GetValueForOption(keepMtimeClearOpt);
    return ClearCommand.RunAsync(file, yes, noBackup, keepMtime);
});

// Preset command
var presetCmd = new Command("preset", "Manage presets");

// save subcommand
var saveCmd = new Command("save", "Create preset");
var saveSourceArg = new Argument<string?>("sourceFile") { Arity = ArgumentArity.ZeroOrOne };
var saveNameOpt = new Option<string>("--name", "Preset name (required)") { IsRequired = true };
var saveArtistOpt = new Option<string?>("--artist", "Artist(s)");
var saveAlbumOpt = new Option<string?>("--album", "Album");
var saveAlbumArtistOpt = new Option<string?>("--album-artist", "Album artist(s)");
var saveGenreOpt = new Option<string?>("--genre", "Genre(s)");
var saveYearOpt = new Option<uint?>("--year", "Year");
var saveCommentOpt = new Option<string?>("--comment", "Comment");
var saveComposerOpt = new Option<string?>("--composer", "Composer(s)");
var saveCopyrightOpt = new Option<string?>("--copyright", "Copyright");
var savePublisherOpt = new Option<string?>("--publisher", "Publisher / Label");
var saveCoverOpt = new Option<string?>("--cover", "Cover image path");
var saveCoverTypeOpt = new Option<string?>("--cover-type", "Cover type: front, back, other");
var saveCoverDescOpt = new Option<string?>("--cover-desc", "Cover description");
var saveForceOpt = new Option<bool>("--force", "Overwrite existing preset");
saveCmd.AddArgument(saveSourceArg);
saveCmd.AddOption(saveNameOpt);
saveCmd.AddOption(saveArtistOpt);
saveCmd.AddOption(saveAlbumOpt);
saveCmd.AddOption(saveAlbumArtistOpt);
saveCmd.AddOption(saveGenreOpt);
saveCmd.AddOption(saveYearOpt);
saveCmd.AddOption(saveCommentOpt);
saveCmd.AddOption(saveComposerOpt);
saveCmd.AddOption(saveCopyrightOpt);
saveCmd.AddOption(savePublisherOpt);
saveCmd.AddOption(saveCoverOpt);
saveCmd.AddOption(saveCoverTypeOpt);
saveCmd.AddOption(saveCoverDescOpt);
saveCmd.AddOption(saveForceOpt);
saveCmd.SetHandler((context) =>
{
    var sourceFile = context.ParseResult.GetValueForArgument(saveSourceArg);
    var name = context.ParseResult.GetValueForOption(saveNameOpt);
    var artist = context.ParseResult.GetValueForOption(saveArtistOpt);
    var album = context.ParseResult.GetValueForOption(saveAlbumOpt);
    var albumArtist = context.ParseResult.GetValueForOption(saveAlbumArtistOpt);
    var genre = context.ParseResult.GetValueForOption(saveGenreOpt);
    var year = context.ParseResult.GetValueForOption(saveYearOpt);
    var comment = context.ParseResult.GetValueForOption(saveCommentOpt);
    var composer = context.ParseResult.GetValueForOption(saveComposerOpt);
    var copyright = context.ParseResult.GetValueForOption(saveCopyrightOpt);
    var publisher = context.ParseResult.GetValueForOption(savePublisherOpt);
    var cover = context.ParseResult.GetValueForOption(saveCoverOpt);
    var coverType = context.ParseResult.GetValueForOption(saveCoverTypeOpt);
    var coverDesc = context.ParseResult.GetValueForOption(saveCoverDescOpt);
    var force = context.ParseResult.GetValueForOption(saveForceOpt);
    return PresetCommand.SaveAsync(sourceFile, name!, artist, album, albumArtist, genre, year, comment, composer, copyright, publisher, cover, coverType, coverDesc, force);
});

// list subcommand
var listCmd = new Command("list", "List presets");
var listJsonOpt = new Option<bool>("--json", "Output JSON");
listCmd.AddOption(listJsonOpt);
listCmd.SetHandler((context) =>
{
    var json = context.ParseResult.GetValueForOption(listJsonOpt);
    return PresetCommand.ListAsync(json);
});

// show subcommand
var showCmd = new Command("show", "Show preset details");
var showNameArg = new Argument<string>("name", "Preset name");
var showJsonOpt = new Option<bool>("--json", "Output JSON");
showCmd.AddArgument(showNameArg);
showCmd.AddOption(showJsonOpt);
showCmd.SetHandler((context) =>
{
    var name = context.ParseResult.GetValueForArgument(showNameArg);
    var json = context.ParseResult.GetValueForOption(showJsonOpt);
    return PresetCommand.ShowAsync(name, json);
});

// apply subcommand
var applyCmd = new Command("apply", "Apply preset to file");
var applyFileArg = new Argument<string>("file", "Target file");
var applyNameOpt = new Option<string>("--name", "Preset name") { IsRequired = true };
var applyNoBackupOpt = new Option<bool>("--no-backup", "Do not create backup");
var applyKeepMtimeOpt = new Option<bool>("--keep-mtime", "Preserve file modification time");
var applyDryRunOpt = new Option<bool>("--dry-run", "Show what would be done");
applyCmd.AddArgument(applyFileArg);
applyCmd.AddOption(applyNameOpt);
applyCmd.AddOption(applyNoBackupOpt);
applyCmd.AddOption(applyKeepMtimeOpt);
applyCmd.AddOption(applyDryRunOpt);
applyCmd.SetHandler((context) =>
{
    var file = context.ParseResult.GetValueForArgument(applyFileArg);
    var name = context.ParseResult.GetValueForOption(applyNameOpt);
    var noBackup = context.ParseResult.GetValueForOption(applyNoBackupOpt);
    var keepMtime = context.ParseResult.GetValueForOption(applyKeepMtimeOpt);
    var dryRun = context.ParseResult.GetValueForOption(applyDryRunOpt);
    return PresetCommand.ApplyAsync(file, name!, noBackup, keepMtime, dryRun);
});

// delete subcommand
var deleteCmd = new Command("delete", "Delete preset");
var deleteNameArg = new Argument<string>("name", "Preset name");
var deleteYesOpt = new Option<bool>("--yes", "Skip confirmation");
deleteCmd.AddArgument(deleteNameArg);
deleteCmd.AddOption(deleteYesOpt);
deleteCmd.SetHandler((context) =>
{
    var name = context.ParseResult.GetValueForArgument(deleteNameArg);
    var yes = context.ParseResult.GetValueForOption(deleteYesOpt);
    return PresetCommand.DeleteAsync(name, yes);
});

presetCmd.AddCommand(saveCmd);
presetCmd.AddCommand(listCmd);
presetCmd.AddCommand(showCmd);
presetCmd.AddCommand(applyCmd);
presetCmd.AddCommand(deleteCmd);

root.AddCommand(infoCmd);
root.AddCommand(setCmd);
root.AddCommand(clearCmd);
root.AddCommand(presetCmd);

// Root handler for interactive mode
root.SetHandler((context) =>
{
    var file = context.ParseResult.GetValueForArgument(fileArg);
    if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
    {
        try
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            return InteractiveEditor.RunAsync(file, cts.Token);
        }
        catch (OperationCanceledException) { return Task.FromResult(130); }
        catch (Exception ex) { return Task.FromResult(ErrorHandler.Fail(ex.Message)); }
    }
    else
    {
        root.Invoke("--help");
        return Task.FromResult(0);
    }
});

return await root.InvokeAsync(args);