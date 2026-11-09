// Topic: Convert untrusted HTML and SVG uploads without letting them reach the network,
// read other files on the server or run scripts.
// Uses GroupDocs.Conversion for .NET 26.10: secure defaults, IExternalResourceHandler
// and stream input.

using System.Collections.Concurrent;
using GroupDocs.Conversion;
using GroupDocs.Conversion.Exceptions;
using GroupDocs.Conversion.Options.Convert;
using GroupDocs.Conversion.Options.Load;

namespace Demo.UntrustedUploads;

internal static class Program
{
    private const string InputFolder = "resources";
    private const string ResultFolder = "output";
    private const string LogoUrl = "https://cdn.example.com/brand/logo.svg";

    private static readonly string HtmlUpload =
        Path.Combine(InputFolder, "uploads", "invoice-upload.html");
    private static readonly string SvgUpload =
        Path.Combine(InputFolder, "uploads", "drawing-upload.svg");
    private static readonly string LogoFile =
        Path.Combine(InputFolder, "brand", "logo.svg");

    private static int Main()
    {
        Directory.CreateDirectory(ResultFolder);
        ApplyLicense();

        foreach (string input in new[] { HtmlUpload, SvgUpload, LogoFile })
        {
            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Missing input: {Path.GetFullPath(input)}");
                return 1;
            }
        }

        Console.WriteLine("1. HTML upload converted from its path, default options:");
        int fromPath = ConvertUploadFromPath(HtmlUpload, ResultPath("from-path.pdf"));
        Console.WriteLine($"   loaded: {fromPath}");

        Console.WriteLine("2. Same upload converted from a stream:");
        int fromStream = ConvertUploadFromStream(HtmlUpload, ResultPath("from-stream.pdf"));
        Console.WriteLine($"   loaded: {fromStream}");

        Console.WriteLine("3. Same upload, company logo supplied from a local file:");
        int withLogo = ConvertWithTrustedAsset(
            HtmlUpload, LogoUrl, LogoFile, ResultPath("with-logo.pdf"));
        Console.WriteLine($"   loaded or supplied: {withLogo}");

        Console.WriteLine("4. SVG upload converted to PDF:");
        int fromSvg = ConvertSvgUpload(SvgUpload, ResultPath("drawing.pdf"));
        Console.WriteLine($"   loaded: {fromSvg}");

        Console.WriteLine("5. Whitelist entry that is only a host name:");
        string? rejection = RejectLooseWhitelistEntry(HtmlUpload, "cdn.example.com");
        Console.WriteLine(rejection == null ? "   accepted" : "   rejected");

        Console.WriteLine($"Output folder: {Path.GetFullPath(ResultFolder)}");
        bool asExpected = fromStream == 0 && withLogo == 1 && fromSvg == 0
            && rejection != null;
        return asExpected ? 0 : 2;
    }

    private static string ResultPath(string fileName) =>
        Path.Combine(ResultFolder, fileName);

    private static void ApplyLicense()
    {
        // Point this at your .lic file to remove evaluation limits.
        // Get a free temporary licence: https://purchase.groupdocs.com/temporary-license
        const string licensePath = "REPLACE_WITH_YOUR_LICENSE_PATH";
        if (File.Exists(licensePath))
        {
            new License().SetLicense(licensePath);
            Console.WriteLine("[license] applied");
        }
        else
        {
            Console.WriteLine("[license] no licence set - running in evaluation mode");
        }
    }

    /// <summary>
    /// Converts an uploaded HTML file from its path with the default options and lists
    /// every resource the file asked for.
    /// </summary>
    /// <remarks>
    /// The defaults block remote URLs, local files outside the upload's folder, UNC paths
    /// and scripts. Files in the upload's own folder still load, so an upload saved next
    /// to other customers' files can pull them into its output. Returns the number of
    /// resources that loaded.
    /// </remarks>
    private static int ConvertUploadFromPath(string uploadPath, string outputPath)
    {
        var audit = new ResourceAudit();
        var loadOptions = new WebLoadOptions { ExternalResourceHandler = audit };

        using (var converter = new Converter(uploadPath, (LoadContext _) => loadOptions))
        {
            converter.Convert(outputPath, new PdfConvertOptions());
        }

        foreach (string decision in audit.Decisions)
        {
            Console.WriteLine($"   {decision}");
        }

        return audit.Loaded;
    }

    /// <summary>
    /// Converts the same upload from a stream, so it has no folder to load local files
    /// from.
    /// </summary>
    /// <remarks>
    /// A document read from a stream has no base folder. Relative and local references are
    /// blocked together with remote ones, and nothing outside the upload itself is read.
    /// Returns the number of resources that loaded.
    /// </remarks>
    private static int ConvertUploadFromStream(string uploadPath, string outputPath)
    {
        var audit = new ResourceAudit();
        var loadOptions = new WebLoadOptions { ExternalResourceHandler = audit };

        using (var converter = new Converter(
            () => File.OpenRead(uploadPath), (LoadContext _) => loadOptions))
        {
            converter.Convert(outputPath, new PdfConvertOptions());
        }

        foreach (string decision in audit.Decisions)
        {
            Console.WriteLine($"   {decision}");
        }

        return audit.Loaded;
    }

    /// <summary>
    /// Converts the upload with the company logo it references served from a local file.
    /// </summary>
    /// <remarks>
    /// The handler answers the logo's URL with the bytes of a file you control, so the PDF
    /// shows the logo and nothing is requested over the network. Every other resource keeps
    /// the default decision. Returns the number of resources that loaded or were supplied.
    /// </remarks>
    private static int ConvertWithTrustedAsset(
        string uploadPath, string assetUrl, string assetPath, string outputPath)
    {
        var trustedAssets = new Dictionary<string, byte[]>
        {
            [assetUrl] = File.ReadAllBytes(assetPath)
        };
        var audit = new ResourceAudit(trustedAssets);
        var loadOptions = new WebLoadOptions { ExternalResourceHandler = audit };

        using (var converter = new Converter(
            () => File.OpenRead(uploadPath), (LoadContext _) => loadOptions))
        {
            converter.Convert(outputPath, new PdfConvertOptions());
        }

        foreach (string decision in audit.Decisions)
        {
            Console.WriteLine($"   {decision}");
        }

        return audit.Loaded;
    }

    /// <summary>
    /// Converts an uploaded SVG drawing to PDF and lists every resource it asked for.
    /// </summary>
    /// <remarks>
    /// An SVG image element can point at a cloud metadata address or a local file, which
    /// makes SVG a common carrier of server-side request forgery. SvgLoadOptions blocks
    /// those references and does not run the drawing's scripts. Returns the number of
    /// resources that loaded.
    /// </remarks>
    private static int ConvertSvgUpload(string uploadPath, string outputPath)
    {
        var audit = new ResourceAudit();
        var loadOptions = new SvgLoadOptions { ExternalResourceHandler = audit };

        using (var converter = new Converter(
            () => File.OpenRead(uploadPath), (LoadContext _) => loadOptions))
        {
            converter.Convert(outputPath, new PdfConvertOptions());
        }

        foreach (string decision in audit.Decisions)
        {
            Console.WriteLine($"   {decision}");
        }

        return audit.Loaded;
    }

    /// <summary>
    /// Shows that a whitelist entry which is not an absolute URI prefix is rejected.
    /// </summary>
    /// <remarks>
    /// Each WhitelistedResources entry must be the absolute prefix of a trusted location,
    /// such as https://cdn.example.com/brand/. A bare host name fails the load with
    /// InvalidLoadOptionsException, whose message names the entry. Returns that message, or
    /// null when the entry is accepted.
    /// </remarks>
    private static string? RejectLooseWhitelistEntry(string uploadPath, string entry)
    {
        var loadOptions = new WebLoadOptions
        {
            WhitelistedResources = new List<string> { entry }
        };

        try
        {
            using var converter = new Converter(
                () => File.OpenRead(uploadPath), (LoadContext _) => loadOptions);
            converter.Convert(ResultPath("loose-whitelist.pdf"), new PdfConvertOptions());
            return null;
        }
        catch (InvalidLoadOptionsException exception)
        {
            Console.WriteLine($"   {exception.Message}");
            return exception.Message;
        }
    }
}

/// <summary>
/// Records each resource a document asks for and supplies trusted assets from local files.
/// </summary>
internal sealed class ResourceAudit : IExternalResourceHandler
{
    private readonly IReadOnlyDictionary<string, byte[]> _trustedAssets;
    private readonly ConcurrentQueue<string> _decisions = new();

    public ResourceAudit()
        : this(new Dictionary<string, byte[]>())
    {
    }

    public ResourceAudit(IReadOnlyDictionary<string, byte[]> trustedAssets)
    {
        _trustedAssets = trustedAssets;
    }

    public IEnumerable<string> Decisions => _decisions;

    public int Loaded => _decisions.Count(decision => !decision.StartsWith("blocked"));

    public void OnLoading(ExternalResourceLoadingArgs args)
    {
        if (_trustedAssets.TryGetValue(args.Uri, out byte[]? content))
        {
            args.Skipped = false;
            args.Data = content;
        }

        string decision = args.Skipped ? "blocked "
            : args.Data != null ? "supplied"
            : "loaded  ";
        _decisions.Enqueue($"{decision} {args.Uri}");
    }
}
