using System.Security.Cryptography;

namespace HaladeHighSchool.Api.Services;

public interface IProfilePhotoStorage
{
    Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<string> SaveSignatureAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? photoUrl);
}

public sealed class ProfilePhotoStorage : IProfilePhotoStorage
{
    private const long MaxFileSize = 2 * 1024 * 1024;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProfilePhotoStorage> _logger;

    public ProfilePhotoStorage(
        IWebHostEnvironment environment,
        ILogger<ProfilePhotoStorage> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
        => SaveImageAsync(file, "photos", cancellationToken);

    public Task<string> SaveSignatureAsync(IFormFile file, CancellationToken cancellationToken = default)
        => SaveImageAsync(file, "signatures", cancellationToken);

    private async Task<string> SaveImageAsync(
        IFormFile file,
        string directoryName,
        CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxFileSize)
        {
            throw new InvalidProfilePhotoException("Choose a JPG or PNG image no larger than 2 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
        {
            throw new InvalidProfilePhotoException("Only JPG and PNG images are supported.");
        }

        var signature = new byte[8];
        await using (var stream = file.OpenReadStream())
        {
            var bytesRead = await stream.ReadAsync(signature.AsMemory(), cancellationToken);
            var validJpeg = bytesRead >= 3
                && signature[0] == 0xFF
                && signature[1] == 0xD8
                && signature[2] == 0xFF;
            var validPng = bytesRead == 8
                && signature.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            if ((extension is ".jpg" or ".jpeg") ? !validJpeg : !validPng)
            {
                throw new InvalidProfilePhotoException("The uploaded file is not a valid JPG or PNG image.");
            }
        }

        var fileName = $"{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}{extension}";
        var directory = Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            directoryName);
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, fileName);

        await using (var destination = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous))
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        var photoUrl = $"/uploads/{directoryName}/{fileName}";
        _logger.LogInformation(
            "Saved profile image {PhotoUrl} ({FileSize} bytes)",
            photoUrl,
            file.Length);
        return photoUrl;
    }

    public Task DeleteAsync(string? photoUrl)
    {
        if (photoUrl is null)
        {
            return Task.CompletedTask;
        }

        var relativePath = photoUrl switch
        {
            _ when photoUrl.StartsWith("/uploads/photos/", StringComparison.Ordinal) => photoUrl["/uploads/photos/".Length..],
            _ when photoUrl.StartsWith("/uploads/signatures/", StringComparison.Ordinal) => photoUrl["/uploads/signatures/".Length..],
            _ => null,
        };
        var fileName = relativePath is null ? null : Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName != relativePath)
        {
            return Task.CompletedTask;
        }

        var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var directoryName = photoUrl.StartsWith("/uploads/signatures/", StringComparison.Ordinal)
            ? "signatures"
            : "photos";
        var filePath = Path.Combine(root, "uploads", directoryName, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}

public sealed class InvalidProfilePhotoException(string message) : Exception(message);
