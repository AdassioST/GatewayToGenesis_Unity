using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>Authenticated local storage. Never trusts a filename or a plaintext header from a save.</summary>
public sealed class SaveStorage
{
    private readonly string root;
    private readonly byte[] key;
    public SaveStorage(string directory)
    {
        root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        string keyPath = Path.Combine(root, "identity.key");
        if (File.Exists(keyPath)) key = SaveKeyProtection.Unprotect(File.ReadAllBytes(keyPath));
        else
        {
            if (Directory.GetFiles(root, "*.arc*").Length != 0)
                throw new IOException("The identity key is missing. Restore the complete Saves folder from backup.");
            key = RandomBytes(64);
            Atomic(keyPath, SaveKeyProtection.Protect(key));
        }
        if (key.Length != 64) throw new IOException("The identity key is damaged.");
    }

    public static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return bytes;
    }

    public string Seal(string text)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key.Take(32).ToArray();
            aes.GenerateIV();
            byte[] plain = Encoding.UTF8.GetBytes(text);
            byte[] cipher;
            using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            byte[] body = Encoding.ASCII.GetBytes("ARC1").Concat(aes.IV).Concat(cipher).ToArray();
            using (var mac = new HMACSHA256(key.Skip(32).ToArray()))
                return Convert.ToBase64String(body.Concat(mac.ComputeHash(body)).ToArray());
        }
    }

    public string Open(string encrypted)
    {
        var data = Convert.FromBase64String(encrypted);
        if (data.Length < 68 || data.Length > 128 * 1024 * 1024) throw new InvalidDataException("Invalid save size.");
        int length = data.Length - 32;
        using (var mac = new HMACSHA256(key.Skip(32).ToArray()))
        {
            var expected = mac.ComputeHash(data, 0, length);
            int difference = 0;
            for (int i = 0; i < 32; i++) difference |= expected[i] ^ data[length + i];
            if (difference != 0) throw new CryptographicException("Save authentication failed; the file is damaged or belongs to another identity key.");
        }
        if (Encoding.ASCII.GetString(data, 0, 4) != "ARC1") throw new InvalidDataException("Unsupported save envelope.");
        using (var aes = Aes.Create())
        {
            aes.Key = key.Take(32).ToArray(); aes.IV = data.Skip(4).Take(16).ToArray();
            using (var dec = aes.CreateDecryptor()) return Encoding.UTF8.GetString(dec.TransformFinalBlock(data, 20, length - 20));
        }
    }

    private string PathFor(string id)
    {
        if (id != "profile" && !Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid save identity.");
        return Path.Combine(root, id + ".arc");
    }
    public bool Exists(string id) => File.Exists(PathFor(id));
    public string[] Slots() => Directory.GetFiles(root, "*.arc").Select(Path.GetFileNameWithoutExtension)
        .Where(id => Guid.TryParseExact(id, "N", out _)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    public void Write(string id, string json) => Atomic(PathFor(id), Encoding.UTF8.GetBytes(Seal(json)));
    public string Read(string id, bool backup = false)
    {
        string path = PathFor(id) + (backup ? ".bak" : "");
        if (new FileInfo(path).Length > 128 * 1024 * 1024) throw new InvalidDataException("Save is too large.");
        return Open(File.ReadAllText(path));
    }
    public void Delete(string id)
    {
        if (id == "profile") throw new InvalidOperationException("The lifetime profile is never a save slot.");
        string path = PathFor(id);
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
    // Scan only the game's managed storage, including renamed files and backups. Never scan user directories.
    public string[] FindCopies(Func<string, bool> matches)
    {
        var found = new System.Collections.Generic.List<string>();
        foreach (var path in Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(path) == "identity.key" || new FileInfo(path).Length > 128 * 1024 * 1024) continue;
            try { if (matches(Open(File.ReadAllText(path)))) found.Add(path); }
            catch (FormatException) { }
            catch (CryptographicException) { }
            catch (InvalidDataException) { }
        }
        return found.ToArray();
    }
    public void RemoveCopies(Func<string, bool> matches)
    {
        foreach (var path in FindCopies(matches)) File.Delete(path);
    }
    private static void Atomic(string path, byte[] bytes)
    {
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
}
