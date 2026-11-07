using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;

namespace RestblCalculator.Core;

public static class Checksums
{
    public static Dictionary<ulong, ulong> Load(string dataDir)
    {
        string path = Path.Combine(dataDir, "checksums.bin");
        if (!File.Exists(path)) return [];

        byte[] data = File.ReadAllBytes(path);
        int count = data.Length / 16;
        Dictionary<ulong, ulong> sums = new(count);
        for (int i = 0; i < count; i++)
            sums[BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i * 8))] = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan((count + i) * 8));
        return sums;
    }

    public static void Save(string dataDir, Dictionary<ulong, ulong> sums)
    {
        byte[] data = new byte[sums.Count * 16];
        int i = 0;
        foreach ((ulong key, ulong sum) in sums.OrderBy(p => p.Key))
        {
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(i * 8), key);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan((sums.Count + i) * 8), sum);
            i++;
        }

        File.WriteAllBytes(Path.Combine(dataDir, "checksums.bin"), data);
    }

    public static ulong? Find(Dictionary<ulong, ulong> sums, string path, int version, int[] versions)
    {
        foreach (int v in versions)
            if (v <= version && sums.TryGetValue(Key($"{path}#{v}"), out ulong sum)) return sum;
        return sums.TryGetValue(Key(path), out ulong first) ? first : null;
    }

    public static ulong Key(string path) => XxHash64.HashToUInt64(Encoding.Unicode.GetBytes(path));

    public static ulong Hash(byte[] data) => XxHash64.HashToUInt64(data);
}
