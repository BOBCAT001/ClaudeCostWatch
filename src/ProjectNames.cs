using System.IO;

namespace ClaudeCostWatch;

static class ProjectNames
{
    public static string Decode(string encoded)
    {
        if (encoded.Length >= 3 && encoded[1] == '-' && encoded[2] == '-')
        {
            var rest = encoded[3..].Replace('-', Path.DirectorySeparatorChar);
            var candidate = $@"{char.ToUpper(encoded[0])}:\{rest}";
            if (Directory.Exists(candidate))
                return Path.GetFileName(candidate)!;
        }
        return encoded;
    }

    // When two projects share the same folder name, prepend the parent directory.
    public static string Disambiguate(string encoded, string shortName)
    {
        if (encoded.Length >= 3 && encoded[1] == '-' && encoded[2] == '-')
        {
            var rest = encoded[3..].Replace('-', Path.DirectorySeparatorChar);
            var candidate = $@"{char.ToUpper(encoded[0])}:\{rest}";
            var parent = Path.GetFileName(Path.GetDirectoryName(candidate) ?? "");
            if (!string.IsNullOrEmpty(parent))
                return $"{parent}\\{shortName}";
        }
        return encoded;
    }
}
