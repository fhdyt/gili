namespace TopBar.Core.Tests;

public class AppPathsTests
{
    private const string Install = @"C:\Users\a\AppData\Local\Programs\TopBar";

    [Theory]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\TopBar\TopBar.exe", Install, true)]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\TopBar\TopBar.exe", Install + @"\", true)]
    [InlineData(@"c:\users\a\appdata\local\programs\topbar\TopBar.exe", Install, true)]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\TopBar2\TopBar.exe", Install, false)]
    [InlineData(@"C:\src\top-taskbar\src\TopBar.App\bin\Debug\TopBar.exe", Install, false)]
    public void IsUnder_matches_whole_directory_names(string path, string directory, bool expected) =>
        Assert.Equal(expected, AppPaths.IsUnder(path, directory));
}
