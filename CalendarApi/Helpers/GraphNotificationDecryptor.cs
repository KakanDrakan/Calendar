using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CalendarApi.Dtos;

public static class GraphNotificationDecryptor
{
    /// <summary>
    /// Decrypts Microsoft Graph webhook encryptedContent using the provided certificate.
    /// Follows the official documentation: IV = first 16 bytes of AES session key.
    /// Also verifies the HMAC-SHA256 signature.
    /// </summary>
    /// <param name="notification">The EncryptedContent object from the webhook notification.</param>
    /// <param name="cert">X509 certificate with private key corresponding to the public key used in subscription.</param>
    /// <returns>The decrypted JSON string.</returns>
    public static string DecryptNotification(EncryptedContent notification, X509Certificate2 cert)
    {
        if (notification == null)
            throw new ArgumentNullException(nameof(notification));
        if (cert == null)
            throw new ArgumentNullException(nameof(cert));

        if (string.IsNullOrEmpty(notification.Data) || string.IsNullOrEmpty(notification.DataKey) || string.IsNullOrEmpty(notification.DataSignature))
            throw new InvalidOperationException("EncryptedContent is missing Data, DataKey, or DataSignature.");

        // 1️⃣ Decrypt AES session key with certificate's private key
        using var rsa = cert.GetRSAPrivateKey();
        if (rsa == null)
            throw new InvalidOperationException("Certificate does not have a private key.");

        byte[] aesKey = rsa.Decrypt(Convert.FromBase64String(notification.DataKey), RSAEncryptionPadding.OaepSHA1);

        // 2️⃣ Compute IV from the first 16 bytes of AES key
        byte[] iv = aesKey.Take(16).ToArray();

        // 3️⃣ Decode encrypted payload
        byte[] encryptedData = Convert.FromBase64String(notification.Data);

        // 4️⃣ Verify the signature using HMACSHA256
        byte[] signature = Convert.FromBase64String(notification.DataSignature);
        using (var hmac = new HMACSHA256(aesKey))
        {
            byte[] computedSig = hmac.ComputeHash(encryptedData);
            if (!computedSig.SequenceEqual(signature))
                throw new InvalidOperationException("Data signature mismatch! The payload may have been tampered with.");
        }

        // 5️⃣ Decrypt the payload using AES-CBC with PKCS7 padding
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
