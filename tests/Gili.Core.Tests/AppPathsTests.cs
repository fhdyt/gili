namespace Gili.Core.Tests;

public class AppPathsTests
{
    private const string Install = @"C:\Users\a\AppData\Local\Programs\Gili";

    [Theory]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\Gili\Gili.exe", Install, true)]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\Gili\Gili.exe", Install + @"\", true)]
    [InlineData(@"c:\users\a\appdata\local\programs\gili\Gili.exe", Install, true)]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\Gili2\Gili.exe", Install, false)]
    [InlineData(@"C:\src\top-taskbar\src\Gili.App\bin\Debug\Gili.exe", Install, false)]
    public void IsUnder_matches_whole_directory_names(string path, string directory, bool expected) =>
        Assert.Equal(expected, AppPaths.IsUnder(path, directory));
}

public class DataFoldersTests
{
    [Fact]
    public void Keep_copies_live_outside_every_data_folder() =>
        Assert.Equal(@"C:\Users\a\.gili\backups", AppPaths.KeepFolderIn(@"C:\Users\a"));

    private const string Install = @"C:\Users\a\AppData\Local\Programs\Gili";

    [Fact]
    public void Data_lives_in_the_package_folder_when_packaged()
    {
        var data = AppPaths.Data(@"C:\P\LocalState");
        Assert.Equal(@"C:\P\LocalState", data.Root);
        Assert.Equal(@"C:\P\LocalState\logs", data.Logs);
        Assert.Equal(@"C:\P\LocalState\last-crash.txt", data.CrashMarker);
    }

    [Fact]
    public void Data_lives_in_local_app_data_otherwise() =>
        Assert.EndsWith(@"\AppData\Local\Gili", AppPaths.Data(null).Root, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Kind_is_store_inside_a_package() =>
        Assert.Equal(InstallKind.Store, AppPaths.KindOf(Install + @"\Gili.exe", packaged: true, Install));

    [Fact]
    public void Kind_is_installed_under_programs_gili() =>
        Assert.Equal(InstallKind.Installed, AppPaths.KindOf(Install + @"\Gili.exe", packaged: false, Install));

    [Theory]
    [InlineData(@"C:\src\gili\src\Gili.App\bin\Debug\Gili.exe")]
    [InlineData(null)]
    public void Kind_is_development_elsewhere(string? exe) =>
        Assert.Equal(InstallKind.Development, AppPaths.KindOf(exe, packaged: false, Install));
}
