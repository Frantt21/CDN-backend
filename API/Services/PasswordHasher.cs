using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace CDNBackend.API.Services;

public class PasswordHasher
{
    // Parámetros recomendados por OWASP para Argon2id (m=19 MiB, t=2, p=1)
    private const int MemorySizeKiB = 19 * 1024;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>Devuelve el hash en formato "saltBase64:hashBase64".</summary>
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Argon2idHash(password, salt);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expected = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Argon2idHash(password, salt);
        if (actual.Length != expected.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Argon2idHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySizeKiB,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };
        return argon2.GetBytes(HashSize);
    }
}
