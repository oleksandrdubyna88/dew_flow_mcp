namespace Mcp.Tests.Leftovers;

/// <summary>A scratch folder for the tests of the primitives that <c>OwnedDir</c> itself is built on
/// (the process probe, the kernel-owner read, the identity read): those tests cannot use the helper they
/// justify. It carries NO prefix on purpose — a name of the owner form belongs to the helper alone — and it
/// is removed by its test. Replaced by <c>OwnedDir</c> in the commit that adds the helper.</summary>
internal sealed class ScratchDir : IDisposable
{
    public string Path { get; }

    public ScratchDir()
    {
        // testsToDelete-exempt: raw-temp — scratch for the tests of the primitives the helper is built on
        var temp = System.IO.Path.GetTempPath();
        Path = System.IO.Path.Combine(temp, "mcp-s2-scratch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
