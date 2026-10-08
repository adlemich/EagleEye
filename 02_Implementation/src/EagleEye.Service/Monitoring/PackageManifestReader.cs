using System.Xml;

namespace EagleEye.Service.Monitoring;

/// <summary>The display name of a Store package: a literal name, or the key of an <c>ms-resource:</c> string.</summary>
/// <param name="Value">The literal name, or the resource key.</param>
/// <param name="IsResource">Whether <see cref="Value"/> is a resource key.</param>
public sealed record ManifestDisplayName(string Value, bool IsResource);

/// <summary>
/// Reads the display name of a Store app from its <c>AppxManifest.xml</c> (ADR-011 §7 item 15, T-1): the
/// <c>VisualElements DisplayName</c> of the application whose <c>Executable</c> is the app's program, else of the
/// first application, else the package's <c>Properties/DisplayName</c>. <see cref="XmlReader"/> with DTDs
/// prohibited, no resolver, at most <see cref="MaxManifestBytes"/>. A raw string starting with <c>@</c> is never
/// passed on; resource lookups use only references built by <see cref="BuildResourceReference"/>.
/// </summary>
public static class PackageManifestReader
{
    /// <summary>Largest manifest that is read.</summary>
    public const int MaxManifestBytes = 1024 * 1024;

    private const string ResourcePrefix = "ms-resource:";
    private const int MaxKeyLength = 256;

    /// <summary>Returns the display name, or <c>null</c> for a missing, oversized or malformed manifest.</summary>
    /// <param name="manifest">The manifest stream.</param>
    /// <param name="executable">The app's program relative to the package folder (e.g. <c>CalculatorApp.exe</c>), or null.</param>
    public static ManifestDisplayName? ReadDisplayName(Stream manifest, string? executable)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!manifest.CanSeek || manifest.Length > MaxManifestBytes)
        {
            return null;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxManifestBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        try
        {
            using var reader = XmlReader.Create(manifest, settings);
            return ToDisplayName(FindName(reader, Normalize(executable)));
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds <c>@{PackageFullName?ms-resource://…}</c> for <c>SHLoadIndirectString</c>, or <c>null</c> if the package
    /// name or key contains characters that do not belong there.
    /// </summary>
    public static string? BuildResourceReference(string packageFullName, string key)
    {
        ArgumentNullException.ThrowIfNull(packageFullName);
        ArgumentNullException.ThrowIfNull(key);
        var separator = packageFullName.IndexOf('_');
        if (separator <= 0 || !packageFullName.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            || key.Length is 0 or > MaxKeyLength || key.Any(c => char.IsControl(c) || c is '{' or '}' or '?' or '@'))
        {
            return null;
        }

        var packageName = packageFullName[..separator];
        var uri = key.StartsWith("//", StringComparison.Ordinal) ? ResourcePrefix + key
            : key.StartsWith('/') ? $"{ResourcePrefix}//{packageName}{key}"
            : key.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase) ? $"{ResourcePrefix}//{packageName}/{key}"
            : $"{ResourcePrefix}//{packageName}/Resources/{key}";
        return $"@{{{packageFullName}?{uri}}}";
    }

    private static string? FindName(XmlReader reader, string? executable)
    {
        string? propertiesName = null;
        string? firstAppName = null;
        string? currentExecutable = null;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.LocalName)
            {
                case "Application":
                    currentExecutable = Normalize(reader.GetAttribute("Executable"));
                    break;
                case "VisualElements" when currentExecutable is not null:
                    var name = reader.GetAttribute("DisplayName");
                    if (executable is not null && string.Equals(currentExecutable, executable, StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }

                    firstAppName ??= name;
                    break;
                case "DisplayName" when propertiesName is null && reader.Depth == 2:
                    propertiesName = reader.ReadElementContentAsString();
                    break;
            }
        }

        return firstAppName ?? propertiesName;
    }

    private static ManifestDisplayName? ToDisplayName(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value) || value.StartsWith('@'))
        {
            return null;
        }

        return value.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)
            ? new ManifestDisplayName(value[ResourcePrefix.Length..], IsResource: true)
            : new ManifestDisplayName(value, IsResource: false);
    }

    private static string? Normalize(string? path) => path?.Replace('/', '\\');
}
