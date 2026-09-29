namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Writes files so that a failure never leaves a half-written file behind.
/// </summary>
internal static class AtomicFile
{
    /// <summary>
    /// Writes to a temporary file that then replaces <paramref name="path" />, keeping the Unix file mode and writing through a symbolic link to its target.
    /// </summary>
    public static void WriteAllBytes(string path, byte[] content)
    {
        string fullPath = Path.GetFullPath(path);
        string targetPath = new FileInfo(fullPath).LinkTarget is null
            ? fullPath
            : File.ResolveLinkTarget(fullPath, returnFinalTarget: true)?.FullName ?? fullPath;
        string directory = Path.GetDirectoryName(targetPath)!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            if (!OperatingSystem.IsWindows() && File.Exists(targetPath))
            {
                File.SetUnixFileMode(temporaryPath, File.GetUnixFileMode(targetPath));
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
