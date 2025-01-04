using System.Security.Cryptography;

namespace SPA.Services
{
    public class SecurityService : ISecurityService
    {
        private readonly IConfiguration _configuration;

        public SecurityService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string Decrypt(string cipherText)
        {
            string[] data = cipherText.Split(":");
            string key = _configuration["AES_Key"];
            byte[] keyBytes = Enumerable.Range(0, key.Length / 2)
                                        .Select(x => Convert.ToByte(key.Substring(x * 2, 2), 16))
                                        .ToArray();

            byte[] cipherTextBytes = Convert.FromBase64String(data[1]);
            byte[] ivBytes = Convert.FromBase64String(data[0]);
            string plaintext = null;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;
                aesAlg.IV = ivBytes;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7; // Ensure padding mode matches the one used during encryption

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherTextBytes))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            return plaintext;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                throw new ArgumentException("Text to encrypt is required.", nameof(plainText));
            }

            string key = _configuration["AES_Key"];
            byte[] keyBytes = Enumerable.Range(0, key.Length / 2)
                                        .Select(x => Convert.ToByte(key.Substring(x * 2, 2), 16))
                                        .ToArray();

            byte[] ivBytes;
            byte[] cipherTextBytes;

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.GenerateIV();
                ivBytes = aes.IV;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new System.IO.MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new System.IO.StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    cipherTextBytes = ms.ToArray();
                }
            }

            string ivBase64 = Convert.ToBase64String(ivBytes);
            string cipherTextBase64 = Convert.ToBase64String(cipherTextBytes);

            return $"{ivBase64}:{cipherTextBase64}";
        }

    }
}
