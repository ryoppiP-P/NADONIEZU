//==============================================================================
//  File   : SaveCrypto.cs
//  Brief  : セーブデータの暗号化/復号化
//
//  Author : Ryoto Kikuchi
//  Date   : 2026/6/18
//------------------------------------------------------------------------------
//==============================================================================
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class SaveCrypto {
    // 鍵とIVは32バイト(256bit)と16バイト(128bit)
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("Nv8a1kALs7za2la0sZ-al2lk1nv8akol"); // 32文字
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("dvA8x'v+lac1MAzi"); // 16文字

    public static string Encrypt(string plain) {
        using var aes = Aes.Create();
        aes.Key = Key; aes.IV = IV;
        using var enc = aes.CreateEncryptor();
        byte[] input = Encoding.UTF8.GetBytes(plain);
        byte[] output = enc.TransformFinalBlock(input, 0, input.Length);
        return Convert.ToBase64String(output);
    }

    public static string Decrypt(string cipher) {
        using var aes = Aes.Create();
        aes.Key = Key; aes.IV = IV;
        using var dec = aes.CreateDecryptor();
        byte[] input = Convert.FromBase64String(cipher);
        byte[] output = dec.TransformFinalBlock(input, 0, input.Length);
        return Encoding.UTF8.GetString(output);
    }
}
