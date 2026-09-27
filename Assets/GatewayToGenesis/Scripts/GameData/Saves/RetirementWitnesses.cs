using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>Three domain-bound authenticated witnesses. Any surviving witness repairs the other two.</summary>
public sealed class RetirementWitnesses
{
    private readonly SaveStorage storage;
    private readonly string prefix;
    // Neutral on-disk labels are not a security boundary; authentication provides integrity.
    private static readonly string[] Labels = { "cinder", "tide", "iris" };
    public RetirementWitnesses(SaveStorage storage, string prefix) { this.storage = storage; this.prefix = prefix; }
    public string PathFor(int index) => prefix + "." + Labels[index];
    public string[] Reconcile(IEnumerable<string> known, bool requireAll = false)
    {
        var union = new SortedSet<string>(known ?? Array.Empty<string>(), StringComparer.Ordinal);
        var texts = new string[3];
        for (int i = 0; i < 3; i++)
        {
            try
            {
                string text = storage.Open(File.ReadAllText(PathFor(i)));
                var lines = text.Split('\n');
                if (lines[0] != "ARC-WITNESS-1:" + i || lines.Skip(1).Any(id => !Guid.TryParseExact(id, "N", out _))) continue;
                texts[i] = text;
                foreach (string id in lines.Skip(1)) union.Add(id);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (FormatException) { }
            catch (System.Security.Cryptography.CryptographicException) { }
        }
        if (union.Any(id => !Guid.TryParseExact(id, "N", out _))) throw new InvalidDataException("Invalid retired identity.");
        if (union.Count == 0) return union.ToArray();
        for (int i = 0; i < 3; i++)
        {
            string text = "ARC-WITNESS-1:" + i + "\n" + string.Join("\n", union);
            if (texts[i] == text) continue;
            try
            {
                string path = PathFor(i), temp = path + ".tmp";
                var bytes = Encoding.UTF8.GetBytes(storage.Seal(text));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            catch (IOException) { if (requireAll) throw; }
            catch (UnauthorizedAccessException) { if (requireAll) throw; }
        }
        return union.ToArray();
    }
}
