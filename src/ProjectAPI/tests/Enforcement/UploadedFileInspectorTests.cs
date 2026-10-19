using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Files;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Contract and payment-proof uploads: the type is decided by the file's bytes,
/// not by its name or the browser's Content-Type. No database is needed.
/// </summary>
public class UploadedFileInspectorTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F' };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D };
    private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();
    private static readonly byte[] Zip = { 0x50, 0x4B, 0x03, 0x04, 0x14, 0, 0x06, 0 };
    private static readonly byte[] Ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
    private static readonly byte[] Exe = { (byte)'M', (byte)'Z', 0x90, 0, 0x03, 0, 0, 0 };

    // ftyp box: size, "ftyp", major brand, minor version, compatible brands.
    private static byte[] Ftyp(string major, params string[] compatible)
    {
        var bytes = new List<byte> { 0, 0, 0, 0x18 };
        bytes.AddRange("ftyp"u8.ToArray());
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(major));
        bytes.AddRange(new byte[] { 0, 0, 0, 0 });
        foreach (var b in compatible) bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(b));
        return bytes.ToArray();
    }

    private static IFormFile File(string name, byte[] header, string contentType = "application/octet-stream", long? length = null)
    {
        var content = header.Concat(new byte[64]).ToArray();
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, length ?? content.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static Task<InspectedFile> Inspect(IFormFile? file) =>
        UploadedFileInspector.InspectAsync(file, "File", CancellationToken.None);

    [Theory]
    [InlineData("recu.jpg", "image/jpeg")]
    [InlineData("RECU.JPEG", "image/jpeg")]
    [InlineData("virement.pdf", "application/pdf")]
    [InlineData("contrat.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("releve.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("contrat.doc", "application/msword")]
    public async Task Accepts_photos_and_documents_by_their_bytes(string name, string expectedType)
    {
        var header = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => Jpeg,
            ".pdf" => Pdf,
            ".docx" or ".xlsx" => Zip,
            _ => Ole
        };

        var result = await Inspect(File(name, header));

        result.ContentType.Should().Be(expectedType);
        result.FileName.Should().Be(name);
    }

    [Fact]
    public async Task Accepts_an_iphone_heic_sent_without_a_content_type()
    {
        var result = await Inspect(File("IMG_0042.HEIC", Ftyp("heic", "mif1", "heic"), contentType: ""));

        result.ContentType.Should().Be("image/heic");
    }

    [Fact]
    public async Task Recognises_heif_by_a_compatible_brand()
    {
        var result = await Inspect(File("photo.heif", Ftyp("mif1", "heic")));

        result.ContentType.Should().StartWith("image/hei");
    }

    [Fact]
    public async Task Keeps_an_image_mislabelled_as_another_image_under_its_real_type()
    {
        var result = await Inspect(File("photo.heic", Jpeg));

        result.ContentType.Should().Be("image/jpeg");
        result.FileName.Should().Be("photo.jpg");
    }

    [Fact]
    public async Task Derives_the_extension_when_the_file_has_none()
    {
        var result = await Inspect(File("image", Png));

        result.ContentType.Should().Be("image/png");
        result.FileName.Should().Be("image.png");
    }

    [Theory]
    [InlineData("recu.pdf")]       // executable renamed as a PDF
    [InlineData("recu.jpg")]
    [InlineData("setup.exe")]
    public async Task Refuses_an_executable_whatever_its_name(string name)
    {
        var act = () => Inspect(File(name, Exe, "application/pdf"));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be(BusinessErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Refuses_a_document_whose_bytes_contradict_its_extension()
    {
        var act = () => Inspect(File("contrat.pdf", Zip));

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Theory]
    [InlineData("macro.docm")]     // macro-enabled: not in the accepted list
    [InlineData("archive.zip")]    // a ZIP container with no document extension
    public async Task Refuses_containers_without_an_accepted_document_extension(string name)
    {
        var act = () => Inspect(File(name, Zip));

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Refuses_a_missing_or_empty_file()
    {
        await ((Func<Task>)(() => Inspect(null))).Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => Inspect(File("vide.pdf", Array.Empty<byte>(), length: 0)))).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Refuses_a_file_above_the_size_limit()
    {
        var act = () => Inspect(File("scan.pdf", Pdf, length: UploadedFileInspector.DefaultMaxSizeBytes + 1));

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Strips_any_path_from_the_file_name()
    {
        var result = await Inspect(File(@"..\..\C:\temp\recu.pdf", Pdf));

        result.FileName.Should().Be("recu.pdf");
    }
}
