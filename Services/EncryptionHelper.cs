using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace SACCOBlockChainSystem.Services;

public class KeyGenerator : Controller
{
    public static void GenerateKeyAndIV(out byte[] key, out byte[] iv)
    {
        using (Aes aes = Aes.Create())
        {
            aes.GenerateKey();
            aes.GenerateIV();
            key = aes.Key;
            iv = aes.IV;
        }
    }
}

public class EncryptionHelper : Controller
{
    private static byte[] Key;
    private static byte[] IV;
    public static void GenKey()
    {
        byte[] key;
        byte[] iv;

        //KeyGenerator.GenerateKeyAndIV(out key, out iv);
        Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012"); ;
        IV = Encoding.UTF8.GetBytes("1234567890123456");
    }

    public static byte[] GetKey()
    {
        return Key;
    }

    public static string Encrypt(string plainText)
    {
        GenKey();
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetKey();
            aes.IV = IV;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    public static string Decrypt(string cipherText)
    {
        GenKey();
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetKey();
            aes.IV = IV;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }


}
