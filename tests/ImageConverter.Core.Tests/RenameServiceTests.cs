using ImageConverter.Core.Models;
using ImageConverter.Core.Services;

namespace ImageConverter.Core.Tests;

public sealed class RenameServiceTests
{
    [Fact]
    public void CreatePlan_SortsByFileNameAndNumbersFiles()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo z = workspace.CreateImage("z.jpg");
        ImageFileInfo a = workspace.CreateImage("a.jpg");

        IReadOnlyList<RenamePlanItem> plan = new RenameService().CreatePlan(
            [z, a], new RenameOptions("Bild", 1, 3, RenameSortMode.CurrentFileName));

        Assert.Equal("a.jpg", Path.GetFileName(plan[0].SourcePath));
        Assert.Equal("Bild001.jpg", Path.GetFileName(plan[0].DestinationPath));
        Assert.Equal("Bild002.jpg", Path.GetFileName(plan[1].DestinationPath));
    }

    [Fact]
    public void Execute_RenamesAllFilesWithoutCollisions()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo first = workspace.CreateImage("one.jpg");
        ImageFileInfo second = workspace.CreateImage("two.jpg");
        var service = new RenameService();
        IReadOnlyList<RenamePlanItem> plan = service.CreatePlan(
            [first, second], new RenameOptions("Bild", 1, 1, RenameSortMode.ManualOrder));

        service.Execute(plan);

        Assert.False(File.Exists(first.Path));
        Assert.False(File.Exists(second.Path));
        Assert.True(File.Exists(workspace.PathFor("Bild1.jpg")));
        Assert.True(File.Exists(workspace.PathFor("Bild2.jpg")));
    }

    [Fact]
    public void CreateSinglePlan_PreservesExtension()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo file = workspace.CreateImage("original.jpg");

        RenamePlanItem item = new RenameService().CreateSinglePlan(file, "portfolio").Single();

        Assert.Equal("portfolio.jpg", Path.GetFileName(item.DestinationPath));
    }

    [Fact]
    public void CreateSinglePlan_RejectsFormatChange()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo file = workspace.CreateImage("original.jpg");

        Assert.Throws<ArgumentException>(() => new RenameService().CreateSinglePlan(file, "portfolio.png"));
    }

    [Fact]
    public void CreatePlan_RejectsExistingUnrelatedTarget()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo file = workspace.CreateImage("original.jpg");
        File.WriteAllText(workspace.PathFor("Bild1.jpg"), "collision");

        Assert.Throws<IOException>(() => new RenameService().CreatePlan(
            [file], new RenameOptions("Bild", 1, 1, RenameSortMode.ManualOrder)));
    }
}
