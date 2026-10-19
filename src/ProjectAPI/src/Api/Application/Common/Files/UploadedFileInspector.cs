using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Application.Common.Files;

/// <summary>A file that passed <see cref="UploadedFileInspector"/>: safe name, canonical type, size.</summary>
public sealed record InspectedFile(string FileName, string ContentType, long SizeBytes);

/// <summary>
/// Validates an uploaded paper-trail file (a contract, a payment proof): a
/// photo taken with a phone, a scan, or an office document.
///
/// The type is decided from the file's own leading bytes, never from the
/// Content-Type the browser sent: phones routinely send HEIC as
/// application/octet-stream or with no type at all, and a renamed executable
/// declares whatever its sender likes. The extension must agree with those
/// bytes, with one leniency: an image whose extension names another image
/// format (iOS exporting a ".heic" as JPEG) is accepted under its real type.
/// A missing extension is derived from the bytes. Macro-enabled office files
/// (.docm, .xlsm) and anything unrecognised are refused.
/// </summary>
public static class UploadedFileInspector
{
    /// <summary>20 MB — a multi-page scan or a full-resolution phone photo.</summary>
    public const long DefaultMaxSizeBytes = 20 * 1024 * 1024;

    public const string AcceptedFormatsFr =
        "photo (JPEG, PNG, HEIC/HEIF, WebP, AVIF, GIF, BMP, TIFF), PDF, Word (DOC, DOCX, ODT) ou Excel (XLS, XLSX, ODS)";

    private enum Signature { Unknown, Jpeg, Png, Gif, Bmp, Tiff, Webp, Heif, Avif, Pdf, Zip, Ole }

    private sealed record Format(Signature Signature, string ContentType, bool IsImage);

    private static readonly Dictionary<string, Format> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new(Signature.Jpeg, "image/jpeg", true),
        [".jpeg"] = new(Signature.Jpeg, "image/jpeg", true),
        [".jfif"] = new(Signature.Jpeg, "image/jpeg", true),
        [".png"] = new(Signature.Png, "image/png", true),
        [".gif"] = new(Signature.Gif, "image/gif", true),
        [".bmp"] = new(Signature.Bmp, "image/bmp", true),
        [".tif"] = new(Signature.Tiff, "image/tiff", true),
        [".tiff"] = new(Signature.Tiff, "image/tiff", true),
        [".webp"] = new(Signature.Webp, "image/webp", true),
        [".heic"] = new(Signature.Heif, "image/heic", true),
        [".heif"] = new(Signature.Heif, "image/heif", true),
        [".avif"] = new(Signature.Avif, "image/avif", true),
        [".pdf"] = new(Signature.Pdf, "application/pdf", false),
        [".docx"] = new(Signature.Zip, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", false),
        [".xlsx"] = new(Signature.Zip, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", false),
        [".odt"] = new(Signature.Zip, "application/vnd.oasis.opendocument.text", false),
        [".ods"] = new(Signature.Zip, "application/vnd.oasis.opendocument.spreadsheet", false),
        [".doc"] = new(Signature.Ole, "application/msword", false),
        [".xls"] = new(Signature.Ole, "application/vnd.ms-excel", false),
    };

    /// <summary>Extension used when the file arrives without one. Containers (ZIP/OLE) are ambiguous and have none.</summary>
    private static readonly Dictionary<Signature, string> DefaultExtension = new()
    {
        [Signature.Jpeg] = ".jpg", [Signature.Png] = ".png", [Signature.Gif] = ".gif", [Signature.Bmp] = ".bmp",
        [Signature.Tiff] = ".tiff", [Signature.Webp] = ".webp", [Signature.Heif] = ".heic", [Signature.Avif] = ".avif",
        [Signature.Pdf] = ".pdf",
    };

    private static readonly HashSet<string> HeifBrands = new(StringComparer.Ordinal)
        { "heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs", "mif1", "msf1" };

    private static readonly HashSet<string> AvifBrands = new(StringComparer.Ordinal) { "avif", "avis" };

    public static async Task<InspectedFile> InspectAsync(
        IFormFile? file, string fieldName, CancellationToken ct, long maxSizeBytes = DefaultMaxSizeBytes)
    {
        if (file is null || file.Length == 0)
        {
            throw new Exceptions.ValidationException(new[]
            {
                new ValidationFailure(fieldName, "Aucun fichier n'a été envoyé, ou le fichier est vide.")
            });
        }

        if (file.Length > maxSizeBytes)
        {
            throw Invalid($"Fichier trop volumineux ({file.Length / 1024d / 1024d:N1} Mo). Maximum : {maxSizeBytes / 1024 / 1024} Mo.");
        }

        var header = new byte[32];
        int read;
        await using (var stream = file.OpenReadStream())
        {
            read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        }
        var signature = Detect(header.AsSpan(0, read));

        if (signature == Signature.Unknown)
        {
            throw Invalid($"Format de fichier non reconnu. Formats acceptés : {AcceptedFormatsFr}.");
        }

        var name = SafeName(file.FileName);
        var extension = Path.GetExtension(name);

        if (string.IsNullOrEmpty(extension) || !Extensions.ContainsKey(extension))
        {
            // No usable extension: accept only when the bytes alone say what it is.
            if (!DefaultExtension.TryGetValue(signature, out var derived))
            {
                throw Invalid($"Extension de fichier manquante ou non acceptée. Formats acceptés : {AcceptedFormatsFr}.");
            }
            extension = derived;
            name = (string.IsNullOrEmpty(Path.GetExtension(name)) ? name : Path.GetFileNameWithoutExtension(name)) + derived;
        }

        var declared = Extensions[extension];
        if (declared.Signature != signature)
        {
            var actual = Extensions.Values.FirstOrDefault(f => f.Signature == signature);
            if (declared.IsImage && actual is { IsImage: true })
            {
                // An image mislabelled as another image format: keep it, under its real type.
                return new InspectedFile(
                    Path.GetFileNameWithoutExtension(name) + DefaultExtension[signature], actual.ContentType, file.Length);
            }

            throw Invalid($"Le contenu du fichier ne correspond pas à son extension ({extension}).");
        }

        return new InspectedFile(name, declared.ContentType, file.Length);
    }

    private static Signature Detect(ReadOnlySpan<byte> b)
    {
        static bool At(ReadOnlySpan<byte> s, int offset, params byte[] expected) =>
            s.Length >= offset + expected.Length && s.Slice(offset, expected.Length).SequenceEqual(expected);
        static string Ascii(ReadOnlySpan<byte> s, int offset) =>
            s.Length >= offset + 4 ? System.Text.Encoding.ASCII.GetString(s.Slice(offset, 4)) : string.Empty;

        if (At(b, 0, 0xFF, 0xD8, 0xFF)) return Signature.Jpeg;
        if (At(b, 0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return Signature.Png;
        if (At(b, 0, (byte)'G', (byte)'I', (byte)'F', (byte)'8')) return Signature.Gif;
        if (At(b, 0, (byte)'B', (byte)'M')) return Signature.Bmp;
        if (At(b, 0, 0x49, 0x49, 0x2A, 0x00) || At(b, 0, 0x4D, 0x4D, 0x00, 0x2A)) return Signature.Tiff;
        if (Ascii(b, 0) == "RIFF" && Ascii(b, 8) == "WEBP") return Signature.Webp;
        if (At(b, 0, (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-')) return Signature.Pdf;
        if (At(b, 0, 0x50, 0x4B, 0x03, 0x04)) return Signature.Zip;
        if (At(b, 0, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)) return Signature.Ole;

        // ISO-BMFF ("....ftyp<major><minor><compatible…>"): HEIC/HEIF and AVIF.
        if (Ascii(b, 4) == "ftyp")
        {
            for (var offset = 8; offset + 4 <= b.Length; offset += 4)
            {
                if (offset == 12) continue; // minor version, not a brand
                var brand = Ascii(b, offset);
                if (AvifBrands.Contains(brand)) return Signature.Avif;
                if (HeifBrands.Contains(brand)) return Signature.Heif;
            }
        }

        return Signature.Unknown;
    }

    private static string SafeName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "fichier" : name.Length > 200 ? name[^200..] : name;
    }

    private static BusinessRuleException Invalid(string message) =>
        new(BusinessErrorCodes.ValidationFailed, message);
}
