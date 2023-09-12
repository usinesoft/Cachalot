using Client.Parsing;
using System.Security.Cryptography;
using System.Text;

namespace CachalotMonitor.Services;

public class AuthenticationService : IAuthenticationService
{
    public const string HashFile = "user_hashes.txt";
    private const string Salt = "cachalot371643681";

    private readonly Dictionary<string, string> _hashCache = new();

    
    static string ComputeSha256Hash(string rawData)
    {
        // Create a SHA256
        using SHA256 sha256Hash = SHA256.Create();
        
        // ComputeHash - returns byte array
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData + Salt));

        // Convert byte array to a string
        var builder = new StringBuilder();
        foreach (var t in bytes)
        {
            builder.Append(t.ToString("x2"));
        }
        return builder.ToString();
    }

    private string GetHash(string code)
    {
        

        // as sha256 is not very fast cache the result
        if (_hashCache.TryGetValue(code, out var hash))
        {
            return hash;
        }

        hash = ComputeSha256Hash(code);
        _hashCache[code] = hash;
        return hash;
    }

    public bool CheckAdminCode(string adminCode)
    {
        if(string.IsNullOrWhiteSpace(adminCode))
            return false;

        // a file that contains the HASH code issued from the admin code

        if (File.Exists(HashFile))
        {
            
            var lines = File.ReadAllLines(HashFile);
            foreach (var line in lines)
            {
                var txt = line.Trim();
                if (!string.IsNullOrWhiteSpace(txt) && !txt.StartsWith("//"))
                {
                    var parts = txt.Split('=');

                    if (parts.Length == 2 && parts[0].Trim().ToLower() == "admin")
                    {
                        var hash  = parts[1].Trim();
                        if (hash == GetHash(adminCode))
                        {
                            return true;
                        }
                    }
                }

            }
        }
        else // if no file exist accept any code and create the file
        {

            var hash = ComputeSha256Hash(adminCode);
            _hashCache[adminCode] = hash;
            
            var line = $"admin={hash}";

            File.WriteAllLines(HashFile, new []{line});

            return true;
        }


        return false;
    }
}