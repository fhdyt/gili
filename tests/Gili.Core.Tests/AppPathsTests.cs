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
