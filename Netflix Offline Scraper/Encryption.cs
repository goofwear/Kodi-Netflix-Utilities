using System;
using System.IO;
using System.Security.Cryptography;

namespace AvalonProjects.Kodi.Netflix.Scraper
{
    // http://www.codeproject.com/Articles/5719/Simple-encrypting-and-decrypting-data-in-C
    class Encryption
    {
        public static byte[] Encrypt(byte[] clearData, byte[] key, byte[] IV)
        {
            MemoryStream ms = new MemoryStream();
            Rijndael alg = Rijndael.Create();
            alg.Key = key;
            alg.IV = IV;
            CryptoStream cs = new CryptoStream(ms, alg.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(clearData, 0, clearData.Length);
            cs.Close();
            byte[] encryptedData = ms.ToArray();
            return encryptedData;
        }

        public static string Encrypt(string clearText, string password)
        {
            byte[] clearBytes = System.Text.Encoding.Unicode.GetBytes(clearText);

            PasswordDeriveBytes pdb = new PasswordDeriveBytes(password,
                new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 
                             0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76});

            byte[] encryptedData = Encrypt(clearBytes, pdb.GetBytes(32), pdb.GetBytes(16));

            return Convert.ToBase64String(encryptedData);
        }

        public static byte[] Decrypt(byte[] cipherData, byte[] key, byte[] IV)
        {
            MemoryStream ms = new MemoryStream();
            Rijndael alg = Rijndael.Create();

            alg.Key = key;
            alg.IV = IV;

            CryptoStream cs = new CryptoStream(ms, alg.CreateDecryptor(), CryptoStreamMode.Write);

            cs.Write(cipherData, 0, cipherData.Length);

            byte[] decryptedData = ms.ToArray();

            return decryptedData;
        }

        public static string Decrypt(string cipherText, string password)
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            PasswordDeriveBytes pdb = new PasswordDeriveBytes(password,
                new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 
                             0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76});

            byte[] decryptedData = Decrypt(cipherBytes, pdb.GetBytes(32), pdb.GetBytes(16));

            return System.Text.Encoding.Unicode.GetString(decryptedData);
        }

    }
}
