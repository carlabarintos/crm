using System.Security.Cryptography;
using System.Text;
using CrmSales.Settings.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrmSales.Settings.Infrastructure.Services;

internal sealed class AesEncryptionService : IEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration, IHostEnvironment environment, ILogger<AesEncryptionService> logger)
    {
        var keyBase64 = configuration["Encryption:Key"];

        if (string.IsNullOrEmpty(keyBase64))
        {
            if (environment.IsProduction())
                throw new InvalidOperationException(
                    "Encryption:Key is not configured. " +
                    "Set Encryption__Key=<base64-32-bytes> as an environment variable before starting in production.");

            var keyFilePath = configuration["Encryption:KeyFile"]
                ?? Path.Combine(AppContext.BaseDirectory, "encryption.key");

            if (File.Exists(keyFilePath))
            {
                keyBase64 = File.ReadAllText(keyFilePath).Trim();
                logger.LogInformation("Encryption key loaded from {KeyFilePath}.", keyFilePath);
            }
            else
            {
                var newKey = new byte[32];
                RandomNumberGenerator.Fill(newKey);
                keyBase64 = Convert.ToBase64String(newKey);

                var dir = Path.GetDirectoryName(keyFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(keyFilePath, keyBase64);

                logger.LogWarning(
                    "Encryption:Key was not configured. A new AES-256 key has been auto-generated and saved to " +
                    "{KeyFilePath}. For production, read that file and set Encryption__Key=<value> as an environment variable.",
                    keyFilePath);
            }
        }

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption:Key must decode to exactly 32 bytes (AES-256).");
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Layout: nonce (12) | tag (16) | ciphertext
        var combined = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, combined, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;

        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException)
        {
            // Not base64 — treat as plaintext (legacy unencrypted value)
            return ciphertext;
        }

        if (combined.Length < NonceSize + TagSize)
            return ciphertext; // Too short to be valid ciphertext — return as-is

        var nonce = combined[..NonceSize];
        var tag = combined[NonceSize..(NonceSize + TagSize)];
        var encryptedData = combined[(NonceSize + TagSize)..];
        var plaintextBytes = new byte[encryptedData.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, encryptedData, tag, plaintextBytes);
        }
        catch (CryptographicException)
        {
            // Authentication tag mismatch — return as-is (legacy plaintext that happened to be valid base64)
            return ciphertext;
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
