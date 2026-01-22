namespace Wingman.Agent.Tools.Extensions;

public static class FileInfoExtensions
{
    public static IEnumerable<FileInfo> FilterByExtensionAndSize(
        this IEnumerable<FileInfo> files,
        string? extension = null,
        long? minSizeBytes = null,
        long? maxSizeBytes = null)
    {
        return files.Where(f =>
        {
            if (extension != null && !f.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                return false;
            if (minSizeBytes.HasValue && f.Length < minSizeBytes.Value)
                return false;
            if (maxSizeBytes.HasValue && f.Length > maxSizeBytes.Value)
                return false;
            return true;
        });
    }
}
