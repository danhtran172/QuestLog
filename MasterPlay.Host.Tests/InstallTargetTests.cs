using MasterPlay.Installer;

namespace MasterPlay.Host.Tests;

public sealed class InstallTargetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "questlog-install-" + Guid.NewGuid().ToString("N"));

    public InstallTargetTests() => Directory.CreateDirectory(_root);

    private InstallEnvironment Environment => new(
        [Path.Combine(_root, "Program Files")],
        [Path.Combine(_root, "Data", "QuestLog")]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void New_folder_is_accepted_and_normalized()
    {
        var error = InstallTarget.Validate($"  \"{Path.Combine(_root, "Apps", "QuestLog")}\\\" ", Environment, out var fullPath);

        Assert.Equal(InstallPathError.None, error);
        Assert.Equal(Path.Combine(_root, "Apps", "QuestLog"), fullPath);
    }

    [Fact]
    public void Empty_existing_folder_is_accepted()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "Empty")).FullName;

        Assert.Equal(InstallPathError.None, InstallTarget.Validate(folder, Environment, out _));
    }

    [Fact]
    public void Folder_with_files_is_rejected_because_uninstall_deletes_it()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "Games")).FullName;
        File.WriteAllText(Path.Combine(folder, "save.dat"), "x");

        Assert.Equal(InstallPathError.NotEmpty, InstallTarget.Validate(folder, Environment, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_input_is_required(string input)
    {
        Assert.Equal(InstallPathError.Required, InstallTarget.Validate(input, Environment, out _));
    }

    [Theory]
    [InlineData(@"Apps\QuestLog")]
    [InlineData(@"\Apps\QuestLog")]
    [InlineData(@"C:Apps")]
    public void Relative_paths_are_rejected(string input)
    {
        Assert.Equal(InstallPathError.NotFullPath, InstallTarget.Validate(input, Environment, out _));
    }

    [Fact]
    public void Drive_root_is_rejected()
    {
        var root = Path.GetPathRoot(_root)!;

        Assert.Equal(InstallPathError.DriveRoot, InstallTarget.Validate(root, Environment, out _));
        Assert.Equal(InstallPathError.DriveRoot, InstallTarget.Validate(root + "\\", Environment, out _));
    }

    [Fact]
    public void Folders_needing_admin_rights_are_rejected()
    {
        var target = Path.Combine(_root, "Program Files", "QuestLog");

        Assert.Equal(InstallPathError.ProtectedFolder, InstallTarget.Validate(target, Environment, out _));
    }

    [Fact]
    public void Install_folder_cannot_overlap_the_data_folder_either_way()
    {
        Assert.Equal(InstallPathError.DataFolder,
            InstallTarget.Validate(Path.Combine(_root, "Data", "QuestLog"), Environment, out _));
        Assert.Equal(InstallPathError.DataFolder,
            InstallTarget.Validate(Path.Combine(_root, "Data", "QuestLog", "App"), Environment, out _));
        Assert.Equal(InstallPathError.DataFolder,
            InstallTarget.Validate(Path.Combine(_root, "Data"), Environment, out _));
    }

    [Fact]
    public void Similar_prefix_is_not_treated_as_inside()
    {
        var target = Path.Combine(_root, "Data", "QuestLogApp");

        Assert.Equal(InstallPathError.None, InstallTarget.Validate(target, Environment, out _));
    }

    [Fact]
    public void Picked_folder_gets_its_own_QuestLog_subfolder()
    {
        Assert.Equal(Path.Combine(_root, "QuestLog"), InstallTarget.FromPickedFolder(_root));
        Assert.Equal(Path.Combine(_root, "QuestLog"), InstallTarget.FromPickedFolder(_root + "\\"));
    }

    [Fact]
    public void Picked_empty_QuestLog_folder_is_used_as_is()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "QuestLog")).FullName;

        Assert.Equal(folder, InstallTarget.FromPickedFolder(folder));
    }

    [Fact]
    public void Picked_QuestLog_folder_with_files_gets_a_subfolder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "QuestLog")).FullName;
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "x");

        Assert.Equal(Path.Combine(folder, "QuestLog"), InstallTarget.FromPickedFolder(folder));
    }

    [Fact]
    public void Chosen_data_folder_is_read_from_the_location_file()
    {
        var file = Path.Combine(_root, "data-location.json");
        File.WriteAllText(file, """{ "dataDirectory": "D:\\Dữ liệu\\QuestLog" }""");

        Assert.Equal(@"D:\Dữ liệu\QuestLog", InstallTarget.ReadChosenDataDirectory(file));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "dataDirectory": "relative" }""")]
    [InlineData("{}")]
    public void Unusable_location_file_is_ignored(string content)
    {
        var file = Path.Combine(_root, "data-location.json");
        File.WriteAllText(file, content);

        Assert.Null(InstallTarget.ReadChosenDataDirectory(file));
    }

    [Fact]
    public void Prepare_creates_the_folder_and_cleanup_removes_it_when_empty()
    {
        var folder = Path.Combine(_root, "New", "QuestLog");

        Assert.True(InstallTarget.TryPrepare(folder, out var created, out _));
        Assert.True(created);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));

        InstallTarget.RemoveIfEmpty(folder);
        Assert.False(Directory.Exists(folder));
    }

    [Theory]
    [InlineData(new[] { "--installto", @"D:\Apps\Quest Log\" }, @"--installto ""D:\Apps\Quest Log\\""")]
    [InlineData(new[] { "--silent", @"C:\x" }, @"--silent C:\x")]
    [InlineData(new[] { "say \"hi\"", "" }, @"""say \""hi\"""" """"")]
    public void Command_line_quotes_like_CommandLineToArgvW(string[] arguments, string expected)
    {
        Assert.Equal(expected, VelopackSetup.BuildCommandLine(arguments));
    }
}
