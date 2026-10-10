using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
namespace FileStorage.Models;
public class AppUser : IdentityUser {
    [MaxLength(80)] public string DisplayName { get; set; } = "";
}
public class StoredFile {
    public int Id { get; set; }
    [MaxLength(255)] public string OriginalName { get; set; } = "";
    [MaxLength(100)] public string StoredName { get; set; } = "";
    public long Size { get; set; }
    [MaxLength(100)] public string ContentType { get; set; } = "";
    [MaxLength(20)] public string Extension { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public AppUser Owner { get; set; } = null!;
    public string StoragePath { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
public class StorageOptions {
    public const int MaxFilesPerUpload = 20;
    public const long MaxBatchBytes = 100 * 1024 * 1024;
    public long MaxFileBytes { get; set; } = 25 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".txt", ".csv", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".docx", ".xlsx", ".pptx", ".zip"];
}
public class RegisterModel {
    [Required, StringLength(80)] public string DisplayName { get; set; } = "";
    [Required, StringLength(32, MinimumLength = 3), RegularExpression(@"[a-zA-Z0-9._-]+", ErrorMessage = "Use letters, numbers, dots, underscores or hyphens.")] public string Username { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), MinLength(8)] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
}
public class LoginModel {
    [Required] public string Username { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
public class FilesModel {
    public List<StoredFile> Files { get; set; } = [];
    public string? Search { get; set; }
    public string? Type { get; set; }
    public int TotalCount { get; set; }
    public long TotalSize { get; set; }
}
public static class FileDisplay {
    /// <summary>
    /// Formats a byte count as bytes, kilobytes, or megabytes for display.
    /// </summary>
    public static string Size(long bytes) => bytes >= 1048576 ? $"{bytes / 1048576d:0.#} MB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes} B";
}
