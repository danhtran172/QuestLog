using MasterPlay.Infrastructure.Configuration;

namespace MasterPlay.Host.Tests;

public sealed class AppPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "questlog-apppaths-" + Guid.NewGuid().ToString("N"));

    private string LocationFile => Path.Combine(_root, "local", "data-location.json");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Missing_location_file_means_the_default_folder()
    {
        Assert.Null(AppPaths.ReadChosenDataDirectory(LocationFile));
    }

    [Fact]
    public void Chosen_folder_round_trips_with_non_ascii_characters()
    {
        var chosen = Path.Combine(_root, "Dữ liệu", "QuestLog");

        AppPaths.SaveChosenDataDirectory(LocationFile, chosen);

        Assert.Equal(chosen, AppPaths.ReadChosenDataDirectory(LocationFile));
        Assert.Contains("Dữ liệu", File.ReadAllText(LocationFile));
        Assert.False(File.Exists(LocationFile + ".tmp"));
    }

    [Fact]
    public void Saving_again_replaces_the_previous_choice()
    {
        AppPaths.SaveChosenDataDirectory(LocationFile, Path.Combine(_root, "first"));
        AppPaths.SaveChosenDataDirectory(LocationFile, Path.Combine(_root, "second"));

        Assert.Equal(Path.Combine(_root, "second"), AppPaths.ReadChosenDataDirectory(LocationFile));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"dataDirectory\": \"relative\\\\folder\"}")]
    [InlineData("{\"dataDirectory\": 42}")]
    public void Damaged_location_file_is_reported_instead_of_silently_using_the_default(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LocationFile)!);
        File.WriteAllText(LocationFile, content);

        Assert.Throws<InvalidDataException>(() => AppPaths.ReadChosenDataDirectory(LocationFile));
    }

    [Fact]
    public void Relative_folder_is_rejected_when_saving()
    {
        Assert.Throws<ArgumentException>(() => AppPaths.SaveChosenDataDirectory(LocationFile, "QuestLog"));
        Assert.False(File.Exists(LocationFile));
    }

    [Fact]
    public void Folder_contains_data_when_config_or_any_database_name_exists()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "data")).FullName;
        Assert.False(AppPaths.ContainsData(folder));

        File.WriteAllText(Path.Combine(folder, "MasterPlay.db"), string.Empty);
        Assert.True(AppPaths.ContainsData(folder));
    }

    [Fact]
    public void Same_path_ignores_case_and_trailing_separator()
    {
        Assert.True(AppPaths.IsSamePath(@"C:\Users\Me\AppData\Roaming\QuestLog\", @"c:\users\me\appdata\roaming\questlog"));
        Assert.False(AppPaths.IsSamePath(@"D:\QuestLog", @"D:\QuestLog Data"));
    }
}
