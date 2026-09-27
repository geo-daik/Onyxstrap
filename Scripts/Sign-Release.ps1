param(
    [Parameter(Mandatory)][string]$FilePath,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [string]$SignTool = 'signtool.exe',
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$target = (Resolve-Path -LiteralPath $FilePath).Path
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) {
    throw 'The signing certificate must be unexpired and have an accessible private key.'
}
if (-not ($certificate.EnhancedKeyUsageList.ObjectId.Value -contains '1.3.6.1.5.5.7.3.3')) {
    throw 'The certificate must support code signing.'
}
& $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $target
if ($LASTEXITCODE -ne 0) { throw 'Signing failed.' }
& $SignTool verify /pa /all /v $target
if ($LASTEXITCODE -ne 0) { throw 'Signature trust verification failed.' }
if ((Get-AuthenticodeSignature -LiteralPath $target).Status -ne 'Valid') {
    throw 'Windows does not consider this signature valid.'
}
Get-FileHash -LiteralPath $target -Algorithm SHA256
