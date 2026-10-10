using FileStorage.Data;
using FileStorage.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace FileStorage.Controllers;
[Authorize]
public class FilesController(AppDbContext db, UserManager<AppUser> users, IWebHostEnvironment env, IOptions<StorageOptions> settings, ILogger<FilesController> logger) : Controller {
    /// <summary>
    /// Gets the authenticated user ID used to restrict file operations.
    /// </summary>
    private string OwnerId => users.GetUserId(User)!;
    /// <summary>
    /// Builds the private upload-directory path outside the public web folder.
    /// </summary>
    private string StorageRoot => Path.Combine(env.ContentRootPath, "App_Data", "uploads");
    /// <summary>
    /// Creates a database query restricted to files owned by the current user.
    /// </summary>
    private IQueryable<StoredFile> Owned => db.Files.Where(f => f.OwnerId == OwnerId);
    /// <summary>
    /// Displays the current user’s name, file count, storage usage, and five most recent uploads.
    /// </summary>
    public async Task<IActionResult> Dashboard() {
        var user = await users.GetUserAsync(User);
        ViewBag.DisplayName = user?.DisplayName ?? User.Identity?.Name;
        return View(new FilesModel { Files = await Owned.AsNoTracking().OrderByDescending(f => f.UploadedAt).Take(5).ToListAsync(), TotalCount = await Owned.CountAsync(), TotalSize = await Owned.SumAsync(f => (long?)f.Size) ?? 0 });
    }
    /// <summary>
    /// Lists the current user’s files, optionally filtering by name and extension, with newest uploads first.
    /// </summary>
    public async Task<IActionResult> Index(string? search, string? type) {
        var query = Owned.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(f => f.OriginalName.ToLower().Contains(search.ToLower()));
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(f => f.Extension == type);
        ViewBag.Extensions = await Owned.Select(f => f.Extension).Distinct().OrderBy(e => e).ToListAsync();
        return View(new FilesModel { Files = await query.OrderByDescending(f => f.UploadedAt).ToListAsync(), Search = search, Type = type });
    }
    /// <summary>
    /// Displays the upload form with the configured file types and size limits.
    /// </summary>
    [HttpGet] public IActionResult Upload() { ViewBag.Options = settings.Value; return View(); }
    /// <summary>
    /// Validates every selected file and the batch limits before saving. Writes files under random names, saves their metadata together, and attempts cleanup if saving fails.
    /// </summary>
    [HttpPost] public async Task<IActionResult> Upload(List<IFormFile>? files, CancellationToken cancellationToken) {
        ViewBag.Options = settings.Value;
        var options = settings.Value;
        if (files == null || files.Count == 0) ModelState.AddModelError("", "Choose at least one file to upload.");
        else {
            if (files.Count > StorageOptions.MaxFilesPerUpload) ModelState.AddModelError("", $"Choose at most {StorageOptions.MaxFilesPerUpload} files at a time.");
            if (files.Sum(f => f.Length) > StorageOptions.MaxBatchBytes) ModelState.AddModelError("", $"Selected files must total {FileDisplay.Size(StorageOptions.MaxBatchBytes)} or smaller.");
            foreach (var file in files) {
                var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
                if (file.Length == 0) ModelState.AddModelError("", $"Choose a non-empty file: {name}.");
                else if (file.Length > options.MaxFileBytes) ModelState.AddModelError("", $"Files must be {FileDisplay.Size(options.MaxFileBytes)} or smaller: {name}.");
                if (name.Length > 255 || string.IsNullOrWhiteSpace(name)) ModelState.AddModelError("", "The file name is invalid or too long.");
                if (!options.AllowedExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError("", $"This file type is not allowed: {name}. Choose one of the listed types.");
            }
        }
        // Validate the entire selection before writing anything; save metadata atomically.
        if (!ModelState.IsValid) return View();
        var paths = new List<string>();
        try {
            Directory.CreateDirectory(StorageRoot);
            foreach (var file in files!) {
                var originalName = Path.GetFileName(file.FileName.Replace('\\', '/'));
                var extension = Path.GetExtension(originalName).ToLowerInvariant();
                var storedName = Guid.NewGuid().ToString("N");
                var path = Path.Combine(StorageRoot, storedName);
                await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    paths.Add(path);
                    await file.CopyToAsync(stream, cancellationToken);
                    if (stream.Length != file.Length || stream.Length > options.MaxFileBytes) throw new IOException("Upload size mismatch.");
                }
                db.Files.Add(new StoredFile { OriginalName = originalName, StoredName = storedName, StoragePath = Path.Combine("App_Data", "uploads", storedName), Size = file.Length, ContentType = MimeType(extension), Extension = extension, OwnerId = OwnerId });
            }
            await db.SaveChangesAsync(cancellationToken);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException or OperationCanceledException) {
            foreach (var path in paths) {
                try { System.IO.File.Delete(path); }
                catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException) { logger.LogWarning(cleanupError, "Could not clean failed upload {Path}", path); }
            }
            logger.LogError(ex, "File upload failed for user {Owner}", OwnerId);
            ModelState.AddModelError("", "The files could not be saved. Please select them again and retry.");
            return View();
        }
        TempData["Success"] = files!.Count == 1 ? $"{Path.GetFileName(files[0].FileName.Replace('\\', '/'))} uploaded successfully." : $"{files.Count} files uploaded successfully.";
        return RedirectToAction(nameof(Index));
    }
    /// <summary>
    /// Streams an owned file as a download using its original name. Returns 404 for unknown or unowned IDs and a message when the disk file is unavailable.
    /// </summary>
    public async Task<IActionResult> Download(int id) {
        var file = await Owned.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id);
        if (file == null) return NotFound();
        var path = Path.Combine(StorageRoot, file.StoredName);
        try {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, file.ContentType, file.OriginalName);
        } catch (IOException) { TempData["Error"] = "This file is no longer available on disk."; return RedirectToAction(nameof(Index)); }
    }
    /// <summary>
    /// Deletes an owned file and its metadata. Temporarily moves the disk file so it can be restored if the database deletion fails.
    /// </summary>
    [HttpPost] public async Task<IActionResult> Delete(int id) {
        var file = await Owned.SingleOrDefaultAsync(f => f.Id == id);
        if (file == null) return NotFound();
        var path = Path.Combine(StorageRoot, file.StoredName);
        // Quarantine first so a failed database delete can restore the file.
        var quarantine = path + ".deleting";
        var moved = false;
        try {
            if (System.IO.File.Exists(path)) { System.IO.File.Move(path, quarantine); moved = true; }
            db.Files.Remove(file);
            await db.SaveChangesAsync();
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException) {
            if (moved) System.IO.File.Move(quarantine, path);
            logger.LogError(ex, "File deletion failed for {Id}", id);
            TempData["Error"] = "The file could not be deleted. Please try again.";
            return RedirectToAction(nameof(Index));
        }
        if (moved) {
            try { System.IO.File.Delete(quarantine); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning(ex, "Could not clean quarantined file {Name}", file.StoredName); }
        }
        TempData["Success"] = $"{file.OriginalName} deleted.";
        return RedirectToAction(nameof(Index));
    }
    /// <summary>
    /// Maps an allowed extension to a server-selected download content type, using binary data as the fallback.
    /// </summary>
    private static string MimeType(string extension) => extension switch {
        ".pdf" => "application/pdf", ".txt" => "text/plain", ".csv" => "text/csv", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".zip" => "application/zip", _ => "application/octet-stream"
    };
}
