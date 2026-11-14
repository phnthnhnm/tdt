namespace Tdt.Core.Services;

/// <summary>
/// Protects stored secrets before they reach disk.
/// </summary>
public interface ISecretProtector
{
    string Protect(string value);

    string Unprotect(string value);
}

/// <summary>
/// Windows DPAPI protection scoped to the current user: another account on the
/// same machine cannot read the stored qBittorrent password or API key.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "Tdt.Settings.v1"u8.ToArray();

    public string Protect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var encrypted = System.Security.Cryptography.ProtectedData.Protect(
            bytes,
            Entropy,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            var encrypted = Convert.FromBase64String(value);
            var bytes = System.Security.Cryptography.ProtectedData.Unprotect(
                encrypted,
                Entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (
            ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// Stores secrets as-is. Only for tests and for callers that already keep the
/// settings file on an encrypted volume.
/// </summary>
public sealed class PlaintextSecretProtector : ISecretProtector
{
    public string Protect(string value) => value;

    public string Unprotect(string value) => value;
}
