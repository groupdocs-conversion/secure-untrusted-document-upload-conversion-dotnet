# Convert Untrusted HTML and SVG Uploads Safely in .NET

[![Product Page](https://img.shields.io/badge/Product%20Page-2865E0?style=for-the-badge&logo=appveyor&logoColor=white)](https://github.com/groupdocs-conversion/GroupDocs.Conversion-Docs)
[![Docs](https://img.shields.io/badge/Docs-2865E0?style=for-the-badge&logo=Hugo&logoColor=white)](https://docs.groupdocs.com/conversion/net/)
[![Blog](https://img.shields.io/badge/Blog-2865E0?style=for-the-badge&logo=WordPress&logoColor=white)](https://blog.groupdocs.com/categories/groupdocs.conversion-product-family/)
[![Free Support](https://img.shields.io/badge/Free%20Support-2865E0?style=for-the-badge&logo=Discourse&logoColor=white)](https://forum.groupdocs.com/c/conversion/)
[![Temporary License](https://img.shields.io/badge/Temporary%20License-2865E0?style=for-the-badge&logo=rocket&logoColor=white)](https://purchase.groupdocs.com/temp-license/100032)

## Overview

`secure-untrusted-document-upload-conversion-dotnet` is a runnable .NET console
sample that converts untrusted HTML and SVG uploads to PDF while keeping
GroupDocs.Conversion's external-resource defaults in force. It uses
GroupDocs.Conversion for .NET 26.10 and C# 12 on .NET 8.0, loading one upload
from a path, the same upload from a stream, an upload with a trusted asset
substituted locally, an SVG upload, and a whitelist entry that is rejected
because it is not an absolute URI prefix. The five examples are written for
developers who accept documents from outsiders - web uploads, e-mail
attachments, API payloads - and need to convert them without exposing the
server to server-side request forgery (SSRF), local file disclosure, or
script execution.

## Technology Stack

- **Platform**: .NET 8.0
- **Product**: GroupDocs.Conversion for .NET 26.10
- **Language**: C# 12
- **Package**: `GroupDocs.Conversion` (NuGet)

## Problem Statement

An upload handler that converts whatever a user sends - an HTML invoice, an
SVG drawing, an attached spreadsheet - inherits whatever that file asks for
during conversion. An HTML `<img>` tag, an SVG `<image>` element, or a
stylesheet `@import` can point at an internal address, a cloud metadata
endpoint, a UNC share, or a file on the conversion server itself. If the
converter fetches every reference it is told to, the untrusted file decides
what the server requests next. The file is in charge.

This sample's own first run showed why. Every remote reference was blocked,
and the PDF still carried another customer's signature, because the upload
sat in a shared folder and a relative image reference inside it resolved to
the file beside it. The question that matters is not only "did it call out
to the internet", it is "what could it read on this machine".

GroupDocs.Conversion addresses this with secure-by-default load options
rather than a network firewall:

- **`SkipExternalResources`**: `true` by default, so remote URLs and local
  files outside the document's own folder are not loaded.
- **`WhitelistedResources`**: lets you allow specific trusted locations by
  absolute URI prefix, never by host name alone.
- **`IExternalResourceHandler`**: a callback invoked for each resource, which
  can confirm, override, or supply the resource's bytes directly.
- **Script suppression**: inline and referenced scripts in HTML, MHTML, XHTML
  and SVG documents do not run unless `ExecuteScripts` is set for documents
  you trust.
- **No UNC loading**: UNC path references never load, regardless of
  `SkipExternalResources`.

## Prerequisites

Before running these examples, ensure you have:

- **.NET SDK** - 8.0 or later (the project targets `net8.0`).
- **GroupDocs.Conversion for .NET** - version 26.10.0, installed via NuGet.
- **A license file (optional)** - the sample runs in evaluation mode without
  one; set `licensePath` in `Program.cs` to use your own license.

## Getting Started

### Installation

**Using Package Manager:**

```bash
dotnet add package GroupDocs.Conversion --version 26.10.0
```

### Configuration

1. Clone this repository and restore the project with `dotnet restore`.
2. Leave the `licensePath` constant in `Program.cs` set to
   `REPLACE_WITH_YOUR_LICENSE_PATH`, or point it at your own `.lic` file.
3. Run `dotnet run` - the examples write four PDFs to `output/`; the
   fifth prints the rejection message instead.

## Why does the same upload load a file from its own folder but not from the internet?

`SkipExternalResources` draws the line at the document's own folder, not
at the network. A file next to the upload counts as part of it. Everything
else - a remote URL, a UNC path, any file outside that folder - is denied.
Stream input removes the folder entirely. That's why the same invoice,
converted from a stream, blocks the one file that loading it from a path
let through.

## Repository Structure

```
secure-untrusted-document-upload-conversion-dotnet/
│
├── Program.cs
├── UntrustedUploadsDemo.csproj
└── resources/
    ├── brand/
    │   └── logo.svg
    └── uploads/
        ├── drawing-upload.svg
        ├── invoice-upload.html
        └── other-customer-signature.svg
```

### File Descriptions

- **Program.cs** - the five conversion examples and the `ResourceAudit`
  handler they all share.
- **UntrustedUploadsDemo.csproj** - .NET 8.0 console project referencing
  `GroupDocs.Conversion` 26.10.0.
- **resources/uploads/invoice-upload.html** - the untrusted HTML upload:
  a stylesheet, a script, a remote logo, an internal address, `file:///`,
  a UNC path, and a same-folder image reference.
- **resources/uploads/drawing-upload.svg** - the untrusted SVG upload: a
  cloud metadata reference, a `file:///` reference, a remote beacon, and
  an inline script.
- **resources/uploads/other-customer-signature.svg** - the sibling file the
  invoice's relative image reference resolves to when converted from a path.
- **resources/brand/logo.svg** - the trusted copy of the company logo
  supplied locally instead of being fetched from its original URL.

## Supporting Helper: ResourceAudit

The first four examples below build a `WebLoadOptions` or `SvgLoadOptions` with its
`ExternalResourceHandler` set to a `ResourceAudit` instance. `ResourceAudit`
implements `IExternalResourceHandler.OnLoading`: it records a `blocked`,
`loaded`, or `supplied` line for each resource the document asks for, and -
when constructed with a dictionary of trusted assets - answers a matching URL
with local bytes instead of letting the request through. It is not one of the
five documented conversion methods; it is the shared observation point that
makes the methods' output visible.

## Code Implementation

### Implementation: Convert an upload from its path with the default options

Converts an uploaded HTML file from its path with the default options and
lists every resource the file asked for. The defaults block remote URLs,
local files outside the upload's folder, UNC paths and scripts, but a file
inside the upload's own folder still loads - so an upload saved next to
another customer's file can pull it into the output. `ResourceAudit.Loaded`
returns how many resources actually loaded.

```csharp
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
```

#### Technical Details

`Converter` loads the invoice from its own path here, and that choice
permits one thing beyond the document itself: anything already sitting in
that same folder. `WebLoadOptions` applies to HTML, MHTML and XHTML and
defaults `SkipExternalResources` to `true`; the
`ExternalResourceHandler` still runs for every resource so `ResourceAudit`
can log the decision that was already made. That one permission is enough.
In the sample run, it is exactly what lets the sibling SVG signature load.

**Key Components:**
- `WebLoadOptions.ExternalResourceHandler`: receives every load decision
- `Converter(string, Func<LoadContext, LoadOptions>)`: loads from a file path
- `ResourceAudit.Loaded`: counts decisions that were not blocked

**Output:** `from-path.pdf`, with the neighboring signature pulled in.

---

### Implementation: Convert the same upload from a stream

Converts the same upload from a stream, so it has no folder to load local
files from. A document read from a stream has no base folder, so relative and
local references are blocked together with remote ones, and nothing outside
the upload itself is read.

```csharp
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
```

#### Technical Details

The only change from the first example is the constructor overload:
`Converter(Func<Stream>, Func<LoadContext, LoadOptions>)` opens the same file
as a stream instead of a path. Because the document has no base folder, the
same relative reference that resolved to the sibling signature in the first
example is blocked here. In the sample run, all six references in the
invoice are blocked and nothing loads.

**Key Components:**
- `Converter(Func<Stream>, Func<LoadContext, LoadOptions>)`: loads from a
  stream, so the document has no base folder
- `ResourceAudit.Decisions`: the per-resource blocked/loaded/supplied log

**Output:** `from-stream.pdf`, with every reference blocked.

---

### Implementation: Supply a trusted asset from a local file

Converts the upload with the company logo it references served from a local
file. The handler answers the logo's URL with the bytes of a file you
control, so the PDF shows the logo and nothing is requested over the
network; every other resource keeps the default decision.

```csharp
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
```

#### Technical Details

`ResourceAudit`'s constructor takes a dictionary of trusted assets keyed by
the URL the document references. Inside `OnLoading`, when the requested
`args.Uri` matches a dictionary key, the handler sets `args.Skipped = false`
and `args.Data` to the local bytes, so `GroupDocs.Conversion` never issues the
request. Nothing is fetched. In the sample run, the logo is supplied this way and every other
reference in the invoice stays blocked, for a total of one resource
loaded or supplied.

**Key Components:**
- `ExternalResourceLoadingArgs.Data`: the bytes the handler can supply
  instead of letting the resource load over the network
- `ExternalResourceLoadingArgs.Skipped`: set to `false` to accept a resource

**Output:** `with-logo.pdf`, showing the company logo from the local file.

---

### Implementation: Convert an SVG upload to PDF

Converts an uploaded SVG drawing to PDF and lists every resource it asked
for. An SVG `<image>` element can point at a cloud metadata address or a
local file, which makes SVG a common carrier of SSRF; `SvgLoadOptions`
blocks those references and does not run the drawing's scripts.

```csharp
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
```

#### Technical Details

`SvgLoadOptions` is the load options class GroupDocs.Conversion selects for
SVG and SVGZ input by default, and it applies the same resource policy
`WebLoadOptions` applies to HTML. The drawing's inline script never runs,
so its caption stays exactly as drawn. Three references in this one
upload - a remote beacon image, a `file:///` path, and a cloud metadata
address - are all blocked before the PDF is produced.

**Key Components:**
- `SvgLoadOptions.ExternalResourceHandler`: the same handler contract as
  `WebLoadOptions`, applied to SVG input
- `ResourceAudit`: reused unmodified across HTML and SVG uploads

**Output:** `drawing.pdf`, with its caption text untouched.

---

### Implementation: Reject a whitelist entry that is not an absolute prefix

Shows that a whitelist entry which is not an absolute URI prefix is rejected.
Each `WhitelistedResources` entry must be the absolute prefix of a trusted
location, such as `https://cdn.example.com/brand/`; a bare host name fails
the load.

```csharp
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
```

#### Technical Details

`WhitelistedResources` takes a list of strings, each expected to be an
absolute URI prefix. Passing the bare host name `cdn.example.com` instead of
`https://cdn.example.com/brand/` makes the load fail with
`InvalidLoadOptionsException`. The exception's message is: `Whitelisted resource 'cdn.example.com' is not
valid. Expected an absolute URI prefix without a query, fragment or
encoded '/', '\' or '.', such as 'https://cdn.example.com/assets/' or
'file:///C:/assets/'.`

**Key Components:**
- `WebLoadOptions.WhitelistedResources`: the allow-list of absolute URI
  prefixes
- `InvalidLoadOptionsException`: thrown when an entry is not a valid prefix

**Output:** no PDF is written; the exception message names the bad entry.

## Best Practices

When implementing untrusted-upload conversion, consider these practices:

- **Prefer stream input for uploads**: converting from a stream removes the
  base folder, so a relative reference in the upload cannot resolve to a
  sibling file.
- **Keep `SkipExternalResources` at its default**: do not set it to `false`
  for documents you have not reviewed.
- **Whitelist by absolute prefix, never by host name**: a bare host name is
  rejected with `InvalidLoadOptionsException`.
- **Supply trusted assets through the handler**: set `ExternalResourceLoadingArgs.Data`
  instead of allowing a network request for assets you already control.
- **Never enable `ExecuteScripts` for uploads**: it is meant for documents
  you trust, not for content a user submitted.
- **Store each upload in its own folder** when you must convert from a path,
  so the folder contains nothing but that one upload.
- **Dispose the `Converter` instance**: the `using` blocks in every example
  release the underlying document resources.

## Additional Resources

For more in-depth information about converting untrusted uploads, explore
these technical resources:

- [**Step-by-step use case guide in the documentation**](https://docs.groupdocs.com/conversion/net/use-cases/convert-untrusted-uploads-safely/)
- [**In-depth blog article about this project**](https://blog.groupdocs.com/conversion/convert-untrusted-uploads-safely-net/)
- [**Skip loading of external resources**](https://docs.groupdocs.com/conversion/net/skip-loading-external-resources/) - the full `WhitelistedResources` and `ExternalResourceHandler` rules
- [**Network access and data privacy**](https://docs.groupdocs.com/conversion/net/network-access-and-data-privacy/) - when GroupDocs.Conversion for .NET touches the network at all
- [**GroupDocs.Conversion for .NET documentation**](https://docs.groupdocs.com/conversion/net/)
- [**API Reference**](https://reference.groupdocs.com/conversion/net/)

## Keywords

`GroupDocs.Conversion`, `.NET`, `C#`, `SSRF`, `untrusted uploads`, `file upload security`, `external resources`, `IExternalResourceHandler`, `WhitelistedResources`, `HTML to PDF`, `SVG to PDF`, `secure document conversion`, `WebLoadOptions`, `SvgLoadOptions`, `SkipExternalResources`, `server-side request forgery`, `path traversal`, `document conversion security`

## Support

For technical support, visit:
- [Free Support Forum](https://forum.groupdocs.com/c/conversion/)
- [Product Documentation](https://docs.groupdocs.com/conversion/net/)
- [Get Temporary License](https://purchase.groupdocs.com/temp-license/100032)
