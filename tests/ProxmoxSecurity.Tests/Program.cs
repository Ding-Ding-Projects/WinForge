using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("normally trusted certificate remains accepted", NormallyTrustedCertificateRemainsAccepted);
Run("name mismatch is never accepted", NameMismatchIsRejected);
Run("missing certificate is never accepted", MissingCertificateIsRejected);
Run("current self-signed leaf with only UntrustedRoot is accepted", CurrentSelfSignedLeafIsAccepted);
Run("expired self-signed leaf is rejected", ExpiredSelfSignedLeafIsRejected);
Run("non-self-issued multi-element chain is rejected", MultiElementChainIsRejected);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} Proxmox certificate-policy tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} Proxmox certificate-policy tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

static void NormallyTrustedCertificateRemainsAccepted()
{
    Assert(ProxmoxService.AcceptServerCertificate(null, null, SslPolicyErrors.None),
        "a certificate that passed normal TLS validation was rejected");
}

static void NameMismatchIsRejected()
{
    using var cert = CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
    using var chain = BuildChain(cert);
    Assert(!ProxmoxService.AcceptServerCertificate(cert, chain, SslPolicyErrors.RemoteCertificateNameMismatch),
        "trust-self-signed accepted a name mismatch");
}

static void MissingCertificateIsRejected()
{
    Assert(!ProxmoxService.AcceptServerCertificate(null, null, SslPolicyErrors.RemoteCertificateNotAvailable),
        "trust-self-signed accepted a missing certificate");
}

static void CurrentSelfSignedLeafIsAccepted()
{
    var now = DateTimeOffset.UtcNow;
    using var cert = CreateSelfSigned(now.AddDays(-1), now.AddDays(7));
    using var chain = BuildChain(cert);
    Assert(chain.ChainElements.Count == 1, "test certificate did not produce a one-element chain");
    Assert(chain.ChainStatus.Any(s => s.Status == X509ChainStatusFlags.UntrustedRoot),
        "test certificate did not produce the expected UntrustedRoot state");
    Assert(ProxmoxService.AcceptServerCertificate(cert, chain, SslPolicyErrors.RemoteCertificateChainErrors, now.UtcDateTime),
        "current self-signed leaf with only UntrustedRoot was rejected");
}

static void ExpiredSelfSignedLeafIsRejected()
{
    var now = DateTimeOffset.UtcNow;
    using var cert = CreateSelfSigned(now.AddDays(-7), now.AddDays(-1));
    using var chain = BuildChain(cert);
    Assert(chain.ChainStatus.Any(s => s.Status != X509ChainStatusFlags.UntrustedRoot),
        "test certificate did not produce a broader chain error");
    Assert(!ProxmoxService.AcceptServerCertificate(cert, chain, SslPolicyErrors.RemoteCertificateChainErrors, now.UtcDateTime),
        "trust-self-signed accepted an expired leaf");
}

static void MultiElementChainIsRejected()
{
    var now = DateTimeOffset.UtcNow;
    using var rootKey = RSA.Create(2048);
    var rootRequest = new CertificateRequest("CN=Test Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
    rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
    using var root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(30));

    using var leafKey = RSA.Create(2048);
    var leafRequest = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    leafRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(leafRequest.PublicKey, false));
    var serial = RandomNumberGenerator.GetBytes(16);
    using var leafWithoutKey = leafRequest.Create(root, now.AddDays(-1), now.AddDays(7), serial);
    using var leaf = leafWithoutKey.CopyWithPrivateKey(leafKey);
    using var chain = new X509Chain();
    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
    chain.ChainPolicy.CustomTrustStore.Add(root);
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    Assert(chain.Build(leaf), "test issuer chain did not build");
    Assert(chain.ChainElements.Count > 1, "test issuer chain was not multi-element");
    Assert(!ProxmoxService.AcceptServerCertificate(leaf, chain, SslPolicyErrors.RemoteCertificateChainErrors, now.UtcDateTime),
        "trust-self-signed accepted a non-self-issued or multi-element chain");
}

static X509Chain BuildChain(X509Certificate2 certificate)
{
    var chain = new X509Chain();
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
    _ = chain.Build(certificate);
    return chain;
}

static X509Certificate2 CreateSelfSigned(DateTimeOffset notBefore, DateTimeOffset notAfter)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
    return request.CreateSelfSigned(notBefore, notAfter);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
