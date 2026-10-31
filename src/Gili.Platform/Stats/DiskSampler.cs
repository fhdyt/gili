using Gili.Core.Modules;

namespace Gili.Platform.Stats;

/// <summary>Free space on Windows' own drive (<c>GetDiskFreeSpaceEx</c> through <see cref="DriveInfo"/>, plan 3c).</summary>
public sealed class DiskSampler
{
    private readonly string _root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    public DiskReading Sample()
    {
        var drive = new DriveInfo(_root);
        return new DiskReading(_root.TrimEnd(Path.DirectorySeparatorChar), (ulong)drive.AvailableFreeSpace, (ulong)drive.TotalSize);
    }
}
