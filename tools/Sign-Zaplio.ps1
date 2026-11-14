# Signs every unsigned .exe/.dll in a build or publish folder (Zaplio itself plus unsigned NuGet libraries such as
# LibVLCSharp). Smart App Control blocks an app when any of its binaries is unsigned, so all of them are signed.
# Files that already carry a valid signature (VLC, the .NET runtime) are left alone.
#
# Settings come from parameters or environment variables:
#   ZAPLIO_SIGN_MODE           Certificate (default) or Azure
#   ZAPLIO_CERT_THUMBPRINT     Certificate mode: SHA-1 thumbprint of a code-signing certificate in the Windows
#                              certificate store (hardware token, Certum SimplySign, SSL.com eSigner, …)
#   ZAPLIO_AZURE_METADATA      Azure mode: path of the Artifact Signing metadata.json
#   ZAPLIO_AZURE_DLIB          Azure mode: path of Azure.CodeSigning.Dlib.dll (Artifact Signing client tools)
#   ZAPLIO_TIMESTAMP_URL       RFC 3161 timestamp server (a sensible default is chosen per mode)
#   ZAPLIO_SIGNTOOL            signtool.exe to use (default: the Microsoft.Windows.SDK.BuildTools NuGet package)
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Mode = $env:ZAPLIO_SIGN_MODE,
    [string]$CertificateThumbprint = $env:ZAPLIO_CERT_THUMBPRINT,
    [string]$AzureMetadata = $env:ZAPLIO_AZURE_METADATA,
    [string]$AzureDlib = $env:ZAPLIO_AZURE_DLIB,
    [string]$TimestampUrl = $env:ZAPLIO_TIMESTAMP_URL,
    [string]$SignTool = $env:ZAPLIO_SIGNTOOL
)

$ErrorActionPreference = 'Stop'
function Fail([string]$message) { Write-Host "error AXSIGN: $message"; exit 1 }

if (-not $Mode) { $Mode = 'Certificate' }
if (-not (Test-Path $Path)) { Fail "Klasör bulunamadı: $Path" }
$Path = (Resolve-Path $Path).Path

if (-not $SignTool) {
    $SignTool = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools') -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*\x64\signtool.exe' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $SignTool -or -not (Test-Path $SignTool)) { Fail 'signtool.exe bulunamadı. Önce "dotnet restore" çalıştır.' }

switch ($Mode) {
    'Certificate' {
        if (-not $CertificateThumbprint) { Fail 'ZAPLIO_CERT_THUMBPRINT tanımlı değil (kod imzalama sertifikasının parmak izi).' }
        $CertificateThumbprint = $CertificateThumbprint -replace '\s', ''
        $certificate = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My | Where-Object Thumbprint -eq $CertificateThumbprint | Select-Object -First 1
        if (-not $certificate) { Fail "Parmak izi $CertificateThumbprint olan sertifika Windows sertifika deposunda yok." }
        if ($certificate.PublicKey.Oid.FriendlyName -notmatch 'RSA') { Fail 'Akıllı Uygulama Denetimi yalnızca RSA anahtarlı sertifikaları kabul eder; bu sertifika RSA değil.' }
        if (-not $TimestampUrl) { $TimestampUrl = 'http://timestamp.digicert.com' }
        $identity = @('/sha1', $CertificateThumbprint)
    }
    'Azure' {
        if (-not $AzureMetadata -or -not (Test-Path $AzureMetadata)) { Fail 'ZAPLIO_AZURE_METADATA geçerli bir metadata.json dosyasını göstermiyor.' }
        if (-not $AzureDlib -or -not (Test-Path $AzureDlib)) { Fail 'ZAPLIO_AZURE_DLIB, Azure.CodeSigning.Dlib.dll dosyasını göstermiyor.' }
        if (-not $TimestampUrl) { $TimestampUrl = 'http://timestamp.acs.microsoft.com' }
        $identity = @('/dlib', $AzureDlib, '/dmdf', $AzureMetadata)
    }
    default { Fail "Bilinmeyen imzalama modu: $Mode (Certificate veya Azure olmalı)" }
}

# A single file (e.g. the Inno Setup installer, which may have a .tmp name) is signed as given.
$candidates = if (Test-Path $Path -PathType Leaf) { Get-Item $Path } else { Get-ChildItem $Path -Recurse -File -Include *.exe, *.dll }
$targets = $candidates |
    Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -eq 'NotSigned' } |
    Select-Object -ExpandProperty FullName
if (-not $targets) { Write-Host "Zaplio imzalama: $Path içinde imzasız dosya yok."; exit 0 }

Write-Host "Zaplio imzalama: $($targets.Count) dosya imzalanıyor ($Mode)…"
# signtool takes many files per call; keep command lines well below the Windows limit.
$batch = New-Object System.Collections.Generic.List[string]
$length = 0
$batches = New-Object System.Collections.Generic.List[object]
foreach ($file in $targets) {
    if ($batch.Count -gt 0 -and $length + $file.Length -gt 24000) { $batches.Add($batch.ToArray()); $batch.Clear(); $length = 0 }
    $batch.Add($file); $length += $file.Length + 3
}
if ($batch.Count -gt 0) { $batches.Add($batch.ToArray()) }

foreach ($files in $batches) {
    & $SignTool sign /fd SHA256 /tr $TimestampUrl /td SHA256 @identity @files
    if ($LASTEXITCODE -ne 0) { Fail "signtool $LASTEXITCODE koduyla başarısız oldu." }
}

$invalid = $targets | Where-Object { (Get-AuthenticodeSignature $_).Status -ne 'Valid' }
if ($invalid) { Fail "İmza doğrulanamadı: $($invalid -join ', ')" }
Write-Host "Zaplio imzalama: $($targets.Count) dosya imzalandı ve doğrulandı."
