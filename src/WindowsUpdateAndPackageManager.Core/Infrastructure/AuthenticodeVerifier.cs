namespace WindowsUpdateAndPackageManager.Infrastructure;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public interface ISignatureVerifier
{
    bool Verify(string filePath);
}

public sealed class SignaturePolicyOptions
{
    public string? TrustedPublisherStorePath { get; set; }
    public string[]? AllowedThumbprints { get; set; }
    public string[]? BlockedThumbprints { get; set; }
    public bool AllowUntrusted { get; set; }
}

public sealed class AuthenticodeVerifier : ISignatureVerifier
{
    private readonly SignaturePolicyOptions _options;

    public AuthenticodeVerifier(SignaturePolicyOptions? options = null)
    {
        _options = options ?? new SignaturePolicyOptions();
    }

    public bool Verify(string filePath)
    {
        try
        {
#pragma warning disable SYSLIB0057
            using var cert = X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0057
            var thumbprint = cert.GetCertHashString();
            if (!string.IsNullOrWhiteSpace(thumbprint) && _options.BlockedThumbprints is not null)
            {
                foreach (var blocked in _options.BlockedThumbprints)
                {
                    if (string.Equals(blocked, thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_options.TrustedPublisherStorePath))
            {
                using var store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                return store.Certificates.Contains(cert);
            }

            if (_options.AllowedThumbprints is not null && _options.AllowedThumbprints.Length > 0)
            {
                return Array.Exists(_options.AllowedThumbprints, t => string.Equals(t, thumbprint, StringComparison.OrdinalIgnoreCase));
            }

            return _options.AllowUntrusted;
        }
        catch
        {
            return _options.AllowUntrusted;
        }
    }
}
