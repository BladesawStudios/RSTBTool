#:property PublishAot=false

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;

string[] known = ["win-x64", "linux-x64", "osx-x64", "osx-arm64"];
string[] rids = args.Length > 0 ? args : known;

if (!File.Exists("RestblCalculator.slnx")) return Fail("Run this from the repo root.");
if (rids.Except(known).FirstOrDefault() is { } unknown) return Fail($"Unknown target {unknown}. Targets: {string.Join(", ", known)}.");

string root = Path.GetFullPath("publish");
Directory.CreateDirectory(root);

foreach (string rid in rids)
{
    string name = $"RestblCalculator-{rid}";
    string folder = Path.Combine(root, name);
    string archive = Path.Combine(root, name + ".zip");
    if (Directory.Exists(folder)) Directory.Delete(folder, true);
    File.Delete(archive);

    foreach (string project in new[] { "Gui", "Cli" })
    {
        Console.WriteLine($"{rid}: publishing {project}");
        if (!Publish($"src/RestblCalculator.{project}", rid, folder)) return Fail($"Publishing {project} for {rid} failed.");
    }

    File.Copy("license.md", Path.Combine(folder, "license.md"));
    foreach (string file in Directory.EnumerateFiles(folder))
        if (Path.GetExtension(file).ToLowerInvariant() is ".pdb" or ".exp" or ".lib" or ".dbg" or ".dwarf") File.Delete(file);

    WriteZip(folder, name, archive, unix: !rid.StartsWith("win"));
    Console.WriteLine($"{rid}: {Path.GetFileName(archive)}, {new FileInfo(archive).Length / 1048576.0:F1} MB");
}

return 0;

static bool Publish(string project, string rid, string output)
{
    ProcessStartInfo info = new("dotnet") { UseShellExecute = false };
    foreach (string arg in new[]
    {
        "publish", project, "-c", "Release", "-r", rid, "--self-contained", "true", "-o", output, "-v", "q", "-nologo",
        "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true", "-p:DebugType=none",
        "-p:AppendRuntimeIdentifierToOutputPath=true", "-p:EnableMacOSCodeSign=true",
    })
        info.ArgumentList.Add(arg);

    using Process process = Process.Start(info)!;
    process.WaitForExit();
    return process.ExitCode == 0;
}

static void WriteZip(string folder, string name, string archive, bool unix)
{
    using (ZipArchive zip = ZipFile.Open(archive, ZipArchiveMode.Create))
    {
        foreach (string path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            ZipArchiveEntry entry = zip.CreateEntryFromFile(path, $"{name}/{Path.GetRelativePath(folder, path).Replace('\\', '/')}", CompressionLevel.SmallestSize);
            if (unix) entry.ExternalAttributes = (0x8000 | (IsNative(path) ? 0b111_101_101 : 0b110_100_100)) << 16;
        }
    }

    if (unix) MarkMadeOnUnix(archive);
}

static void MarkMadeOnUnix(string archive)
{
    using FileStream file = File.Open(archive, FileMode.Open, FileAccess.ReadWrite);
    byte[] tail = new byte[Math.Min(file.Length, 65557)];
    file.Seek(-tail.Length, SeekOrigin.End);
    file.ReadExactly(tail);

    int end = tail.AsSpan().LastIndexOf((byte[])[0x50, 0x4B, 0x05, 0x06]);
    int count = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end + 10));
    int size = BinaryPrimitives.ReadInt32LittleEndian(tail.AsSpan(end + 12));
    int offset = BinaryPrimitives.ReadInt32LittleEndian(tail.AsSpan(end + 16));

    byte[] directory = new byte[size];
    file.Seek(offset, SeekOrigin.Begin);
    file.ReadExactly(directory);

    int at = 0;
    for (int i = 0; i < count; i++)
    {
        directory[at + 5] = 3;
        Span<byte> entry = directory.AsSpan(at);
        at += 46 + BinaryPrimitives.ReadUInt16LittleEndian(entry[28..]) + BinaryPrimitives.ReadUInt16LittleEndian(entry[30..]) + BinaryPrimitives.ReadUInt16LittleEndian(entry[32..]);
    }

    file.Seek(offset, SeekOrigin.Begin);
    file.Write(directory);
}

static bool IsNative(string path)
{
    using FileStream file = File.OpenRead(path);
    Span<byte> head = stackalloc byte[4];
    return file.Read(head) == 4 && (head.SequenceEqual((byte[])[0x7F, 0x45, 0x4C, 0x46]) || head.SequenceEqual((byte[])[0xCF, 0xFA, 0xED, 0xFE]) || head.SequenceEqual((byte[])[0xCA, 0xFE, 0xBA, 0xBE]));
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
