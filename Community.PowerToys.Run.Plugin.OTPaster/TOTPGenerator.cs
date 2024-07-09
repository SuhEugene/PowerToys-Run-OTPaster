using System.Security.Cryptography;

public class TOTPGenerator
{
  private static readonly int TimeStep = 30;
  private static readonly int Digits = 6;
  public static int GetTimeLeft(int period = 30)
  {
    int msPeriod = period * 1000;
    long unixTime = (long)Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds);
    int timePassed = (int)(unixTime % msPeriod);

    return msPeriod - timePassed;
  }
  public static string GenerateCode(string secret, bool next = false)
  {
    byte[] secretBytes = ConvertToBytes(secret);
    using var hmac = new HMACSHA1(secretBytes);

    long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    byte[] timeBytes = BitConverter.GetBytes((unixTime / TimeStep) + (next ? 1 : 0));
    Array.Reverse(timeBytes); // Ensure big-endian byte order

    byte[] hash = hmac.ComputeHash(timeBytes);
    int offset = hash[hash.Length - 1] & 0x0F;

    int binaryCode = ((hash[offset] & 0x7F) << 24) |
                     ((hash[offset + 1] & 0xFF) << 16) |
                     ((hash[offset + 2] & 0xFF) << 8) |
                     (hash[offset + 3] & 0xFF);

    int totp = binaryCode % (int)Math.Pow(10, Digits);

    return totp.ToString().PadLeft(Digits, '0');
  }
  private static byte[] ConvertToBytes(string base32String)
  {
    const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    base32String = base32String.Trim().ToUpper();

    byte[] bytes = new byte[base32String.Length * 5 / 8];

    int bytesPosition = 0;
    int buffer = 0;
    int bufferSize = 0;

    foreach (char c in base32String)
    {
      int value = base32Chars.IndexOf(c);
      buffer = (buffer << 5) | value;
      bufferSize += 5;

      if (bufferSize >= 8)
      {
        bytes[bytesPosition++] = (byte)(buffer >> (bufferSize - 8));
        bufferSize -= 8;
      }
    }

    return bytes;
  }
}