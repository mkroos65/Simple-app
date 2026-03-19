// =============================================================================
// Services/CertificateHelper.cs — Auto-generates a self-signed TLS certificate
//                                   for secure WebSocket (wss://) connections
// =============================================================================

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SimpleFlightTracker.Services;

/// <summary>
/// Generates and caches a self-signed X509 certificate for TLS.
/// The certificate is saved to disk so it persists across restarts.
/// </summary>
public static class CertificateHelper
{
    private const string CertFileName = "SimpleFlightTracker.pfx";
    private const string CertPassword = "SimpleFlightTracker";

    /// <summary>
    /// Returns a valid X509Certificate2 for TLS use.
    /// Loads from disk if available, otherwise generates a new one.
    /// </summary>
    public static X509Certificate2 GetOrCreateCertificate(Action<string>? log = null)
    {
        var certDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleFlightTracker");
        Directory.CreateDirectory(certDir);

        var certPath = Path.Combine(certDir, CertFileName);

        // Try to load existing certificate
        if (File.Exists(certPath))
        {
            try
            {
                var existing = new X509Certificate2(certPath, CertPassword,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

                // Check if still valid (at least 7 days remaining)
                if (existing.NotAfter > DateTime.UtcNow.AddDays(7))
                {
                    log?.Invoke($"Loaded TLS certificate from: {certPath}");
                    log?.Invoke($"  Valid until: {existing.NotAfter:yyyy-MM-dd}");
                    return existing;
                }

                log?.Invoke("Existing certificate expires soon, generating new one...");
                existing.Dispose();
            }
            catch (Exception ex)
            {
                log?.Invoke($"Could not load existing certificate: {ex.Message}");
            }
        }

        // Generate a new self-signed certificate
        log?.Invoke("Generating self-signed TLS certificate for LAN connections...");

        var cert = CreateSelfSignedCertificate();

        // Save to disk for reuse
        var pfxBytes = cert.Export(X509ContentType.Pfx, CertPassword);
        File.WriteAllBytes(certPath, pfxBytes);

        log?.Invoke($"TLS certificate saved to: {certPath}");
        log?.Invoke($"  Valid until: {cert.NotAfter:yyyy-MM-dd}");
        log?.Invoke($"  Thumbprint: {cert.Thumbprint}");

        return cert;
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Dns.GetHostName());
        sanBuilder.AddIpAddress(IPAddress.Loopback);      // 127.0.0.1
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);   // ::1

        // Add all local IP addresses for LAN access
        try
        {
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in hostEntry.AddressList)
            {
                sanBuilder.AddIpAddress(ip);
            }
        }
        catch
        {
            // Best effort — localhost will always work
        }

        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=Simple Flight Tracker, O=SimpleFlightTracker",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") }, // serverAuth
                false));

        request.CertificateExtensions.Add(sanBuilder.Build());

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(2));

        // Export and re-import to ensure private key is available on Windows
        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, CertPassword),
            CertPassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }
}
