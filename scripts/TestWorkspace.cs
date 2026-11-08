using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>Isolate relative server paths before any test/game static initialization.</summary>
internal static class TestWorkspace
{
    internal static string CallerDirectory { get; private set; } = "";
    internal static string RepositoryRoot { get; private set; } = "";
    internal static string RunDirectory { get; private set; } = "";

    [ModuleInitializer]
    internal static void Initialize()
    {
        CallerDirectory = Directory.GetCurrentDirectory();
        RepositoryRoot = FindRoot();
        var stamp = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("yyyyMMdd-HHmmss");
        RunDirectory = Path.Combine(RepositoryRoot, ".ai", "test-runs", Assembly.GetExecutingAssembly().GetName().Name!,
            stamp + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RunDirectory);
        Directory.SetCurrentDirectory(RunDirectory);
        Console.WriteLine($"Test workspace: {RunDirectory}");
    }

    private static string FindRoot([CallerFilePath] string sourceFile = "")
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, ".."));
        if (!Directory.Exists(Path.Combine(root, "Client")) || !Directory.Exists(Path.Combine(root, "Server")))
            throw new DirectoryNotFoundException("Test source checkout is unavailable; refusing to write relative game data in the caller directory.");
        return root;
    }

    internal static string ResolvePath(string path) => Path.GetFullPath(path, CallerDirectory);

    internal static void DeleteFixture(string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(RunDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to remove a directory outside this test run.");
        Directory.Delete(full, recursive: true);
    }
}
