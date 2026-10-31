using Gili.Data;

namespace Gili.Core.Tests.Data;

public sealed class TaskbarStateFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-taskbar-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Missing_file_reads_as_nothing_saved() => Assert.Null(new TaskbarStateFile(_folder).Read());

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void State_survives_a_new_instance(bool autoHide)
    {
        new TaskbarStateFile(_folder).Write(autoHide);
        Assert.Equal(autoHide, new TaskbarStateFile(_folder).Read());
    }

    [Fact]
    public void Delete_forgets_the_state()
    {
        var file = new TaskbarStateFile(_folder);
        file.Write(true);
        file.Delete();
        Assert.Null(file.Read());
    }

    [Fact]
    public void A_broken_file_reads_as_nothing_saved()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "taskbar-state.json"), "{not json");
        Assert.Null(new TaskbarStateFile(_folder).Read());
    }
}
