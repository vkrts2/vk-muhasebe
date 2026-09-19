using System.Security.Cryptography;
using System.Text;

namespace ErmayMuhasebe.Services;

public class AuthService
{
    public static string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    public static string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var saltedPassword = password + salt;
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
        return Convert.ToBase64String(hashedBytes);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(storedSalt))
        {
            // Backward compatibility for unsalted passwords
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var hashToVerify = Convert.ToBase64String(hashedBytes);
            return hashToVerify == storedHash;
        }

        var saltToUse = storedSalt;
        var hashToVerifySalted = HashPassword(password, saltToUse);
        return hashToVerifySalted == storedHash;
    }

    private static readonly byte[] LegacyKey = Encoding.UTF8.GetBytes("ERMAY-SECURE-KEY-2025");
    private static readonly byte[] FallbackAesKey = SHA256.HashData(Encoding.UTF8.GetBytes("ERMAY-AES-SECURE-MASTER-KEY-2025-V2!"));
    private const string EncryptPrefix = "ENC::AES::";

    private static byte[] GetDerivedKey()
    {
        try
        {
            var seed = $"{Environment.MachineName}::{Environment.UserName}::VK_SECURE_VAULT_2025";
            using var hmac = new HMACSHA256(FallbackAesKey);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }
        catch
        {
            return FallbackAesKey;
        }
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        try
        {
            using var aes = Aes.Create();
            aes.Key = GetDerivedKey();
            aes.GenerateIV();
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            using var ms = new MemoryStream();
            ms.Write(iv, 0, iv.Length); // İlk 16 byte IV

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs, Encoding.UTF8))
            {
                sw.Write(plainText);
            }

            return EncryptPrefix + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return "";
        }
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        
        // AES formatında mı kontrol et
        if (cipherText.StartsWith(EncryptPrefix))
        {
            var base64Data = cipherText.Substring(EncryptPrefix.Length);
            byte[] fullBytes;
            try
            {
                fullBytes = Convert.FromBase64String(base64Data);
            }
            catch
            {
                return "";
            }
            if (fullBytes.Length < 16) return "";

            // 1. Önce cihaz-türetimli dinamik anahtar ile dene
            try
            {
                using var aes = Aes.Create();
                aes.Key = GetDerivedKey();
                byte[] iv = new byte[16];
                Array.Copy(fullBytes, 0, iv, 0, 16);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(fullBytes, 16, fullBytes.Length - 16);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs, Encoding.UTF8);
                string res = sr.ReadToEnd();
                if (!string.IsNullOrEmpty(res)) return res;
            }
            catch
            {
                // Fallback'e devam et
            }

            // 2. Geriye dönük uyumluluk: Eski FallbackAesKey ile dene
            try
            {
                using var aes = Aes.Create();
                aes.Key = FallbackAesKey;
                byte[] iv = new byte[16];
                Array.Copy(fullBytes, 0, iv, 0, 16);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(fullBytes, 16, fullBytes.Length - 16);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs, Encoding.UTF8);
                return sr.ReadToEnd();
            }
            catch
            {
                return "";
            }
        }

        // Geriye dönük uyumluluk: Eski XOR formatını dene
        try
        {
            var bytes = Convert.FromBase64String(cipherText);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ LegacyKey[i % LegacyKey.Length]);
            }
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}
