using System.Text;

namespace ShiftTrack.Tests.Shared.Fixtures;

public static class FixtureReader
{
    public static string ReadText(string relativePath)
    {
        var path = ResolvePath(relativePath);
        return File.ReadAllText(path, Encoding.UTF8);
    }

    public static byte[] ReadBytes(string relativePath)
    {
        var path = ResolvePath(relativePath);
        return File.ReadAllBytes(path);
    }

    private static string ResolvePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.Combine(AppContext.BaseDirectory, "Fixtures", normalized);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException($"Fixture file was not found: {relativePath}", candidate);
    }
}
