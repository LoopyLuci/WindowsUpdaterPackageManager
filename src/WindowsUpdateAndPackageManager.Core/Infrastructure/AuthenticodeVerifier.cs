using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface ISignatureVerifier
{
    bool Verify(string filePath);
}

public sealed class AuthenticodeVerifier : ISignatureVerifier
{
    public bool Verify(string filePath)
    {
        try
        {
#pragma warning disable SYSLIB0057
            using var cert = X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0057
            using var store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Contains(cert);
        }
        catch
        {
            return false;
        }
    }
}
