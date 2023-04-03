using System.Security.Cryptography;
using System.Text;

namespace AFMapper;

/// <summary>
/// Encryption routines for encryption using AES256.
/// </summary>
public static class CryptoAES256
{
    private static byte[] _aesKey = { };
    private static byte[] _aesIV = { };

    /// <summary>
    /// Generates the key necessary for encryption/decryption.
    /// All encryption and decryption processes following the call 
    /// use the generated key.
    /// </summary>
    /// <param name="password"></param>
    public static void CreateKey(string password)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        _aesKey = SHA256.Create().ComputeHash(passwordBytes);
        _aesIV = MD5.Create().ComputeHash(passwordBytes);
    }

    /// <summary> Encryption of a byte[] using AES. </summary>
    /// <param name="clearData"> Data to be encrypted </param>
    /// <returns> the encrypted data </returns>
    public static byte[] Encrypt(byte[] clearData)
    {
        if (clearData == null) throw new ArgumentNullException(nameof(clearData));

        if (_aesKey.Length < 1)
        {
            throw new Exception(
                "Ver der Verwendung von Encrypt muss CreateKey aufgerufen werden um den passenden Schlüssel zu generieren.");
        }

        using (var aes = Aes.Create())
        {
            aes.Key = _aesKey;
            aes.IV = _aesIV;

            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV)) return _PerformCryptography(clearData, encryptor);
        }
    }

    /// <summary> 
    /// Decryption of a byte[] encrypted using AES. 
    /// 
    /// ATTENTION! Before decrypting, make sure that CreateKey has been called!
    /// </summary>
    /// <param name="encryptedData"> the encrypted data </param>
    /// <returns> the decrypted data </returns>
    public static byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData == null) throw new ArgumentNullException(nameof(encryptedData));

        if (_aesKey.Length < 1)
        {
            throw new Exception(
                "Ver der Verwendung von Decrypt muss CreateKey aufgerufen werden um den passenden Schlüssel zu generieren.");
        }

        using (var aes = Aes.Create())
        {
            aes.Key = _aesKey;
            aes.IV = _aesIV;

            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV)) return _PerformCryptography(encryptedData, decryptor);
        }
    }

    private static byte[] _PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
    {
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();

        return ms.ToArray();
    }
}

