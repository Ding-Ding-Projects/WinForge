using System.IO.Compression;
using System.Security;
using System.Text;

if (args.Length == 0 || !string.Equals(args[0], "pack", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: SquirrelPackaging pack --source <publish-dir> --output <nupkg> --version <1.1.x>");
    return 2;
}

string? source = null;
string? output = null;
string? version = null;
for (int i = 1; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--source" when i + 1 < args.Length: source = args[++i]; break;
        case "--output" when i + 1 < args.Length: output = args[++i]; break;
        case "--version" when i + 1 < args.Length: version = args[++i]; break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
            return 2;
    }
}

if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(version))
{
    Console.Error.WriteLine("Source, output, and version are required.");
    return 2;
}

source = Path.GetFullPath(source);
output = Path.GetFullPath(output);
if (!Directory.Exists(source))
{
    Console.Error.WriteLine($"Publish directory does not exist: {source}");
    return 1;
}

if (!System.Version.TryParse(version, out var parsed) || parsed.Major != 1 || parsed.Minor != 1 || parsed.Build is < 1 or > 65535)
{
    Console.Error.WriteLine($"Version is outside the WinForge 1.1.x contract: {version}");
    return 1;
}

string? parent = Path.GetDirectoryName(output);
if (string.IsNullOrWhiteSpace(parent))
{
    Console.Error.WriteLine("Output must name a file in a directory.");
    return 1;
}
Directory.CreateDirectory(parent);
if (File.Exists(output)) File.Delete(output);

using (var archive = ZipFile.Open(output, ZipArchiveMode.Create))
{
    AddText(archive, "WinForge.nuspec", $"""
        <?xml version="1.0" encoding="utf-8"?>
        <package>
          <metadata>
            <id>WinForge</id>
            <version>{SecurityElement.Escape(version)}</version>
            <title>WinForge</title>
            <authors>Ding-Ding-Projects</authors>
            <owners>Ding-Ding-Projects</owners>
            <description>WinForge bilingual Windows 11 control center.</description>
            <language>en-US</language>
          </metadata>
        </package>
        """);
    AddText(archive, "[Content_Types].xml", """
        <?xml version="1.0" encoding="utf-8"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="psmdcp" ContentType="application/vnd.openxmlformats-package.core-properties+xml" />
          <Default Extension="nuspec" ContentType="application/octet" />
          <Default Extension="dll" ContentType="application/octet-stream" />
          <Default Extension="exe" ContentType="application/octet" />
          <Default Extension="json" ContentType="application/json" />
          <Default Extension="xml" ContentType="application/xml" />
          <Default Extension="png" ContentType="image/png" />
          <Default Extension="ico" ContentType="image/x-icon" />
          <Default Extension="ttf" ContentType="font/ttf" />
          <Default Extension="html" ContentType="text/html" />
          <Default Extension="js" ContentType="text/javascript" />
          <Default Extension="css" ContentType="text/css" />
          <Default Extension="dat" ContentType="application/octet-stream" />
          <Default Extension="bin" ContentType="application/octet-stream" />
          <Default Extension="config" ContentType="application/xml" />
          <Default Extension="pdb" ContentType="application/octet-stream" />
        </Types>
        """);
    AddText(archive, "_rels/.rels", """
        <?xml version="1.0" encoding="utf-8"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/WinForge.nuspec" Id="RWINFORGENUSPEC" />
          <Relationship Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="/package/services/metadata/core-properties/WinForge.psmdcp" Id="RWINFORGECORE" />
        </Relationships>
        """);
    AddText(archive, "package/services/metadata/core-properties/WinForge.psmdcp", """
        <?xml version="1.0" encoding="utf-8"?>
        <coreProperties xmlns="http://schemas.microsoft.com/packaging/2010/07/metadata-core-properties"
                        xmlns:dc="http://purl.org/dc/elements/1.1/"
                        xmlns:dcterms="http://purl.org/dc/terms/"
                        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <dc:creator>Ding-Ding-Projects</dc:creator>
          <dc:description>WinForge bilingual Windows 11 control center.</dc:description>
          <dc:subject>Windows desktop utility</dc:subject>
          <dc:title>WinForge</dc:title>
          <dc:language>en-US</dc:language>
          <dcterms:created xsi:type="dcterms:W3CDTF">1980-01-01T00:00:00Z</dcterms:created>
        </coreProperties>
        """);

    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
        if (relative.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
        string entryName = "lib/net45/" + relative;
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using Stream input = File.OpenRead(file);
        using Stream destination = entry.Open();
        input.CopyTo(destination);
    }
}

Console.WriteLine($"Created Squirrel NuGet package: {output}");
return 0;

static void AddText(ZipArchive archive, string name, string text)
{
    ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
    writer.Write(text.Replace("\r\n", "\n"));
}
