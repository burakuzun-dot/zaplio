<#
.SYNOPSIS
  Zaplio'nun öz denetimini çalıştırır: uygulama, ekran dışında, yerel bir demo listeyle kendi ekranlarını gezer
  (Canlı TV, Filmler, Diziler, sekmeler, Ayarlar, durum ekranları) ve yerleşim ya da animasyon hatalarını raporlar.

.DESCRIPTION
  - Gerçek verine dokunmaz: ZAPLIO_DATA_DIR ile geçici bir veri klasörü kullanır, iş bitince siler.
  - Ağ isteği yapmaz: demo liste yerel bir dosyadır, hiçbir yayın oynatılmaz, sürüm denetimi kapalıdır.
  - Açık duran Zaplio'yu etkilemez: ayrı bir klasöre derler.
  Çıkış kodu: 0 = hata yok, 1 = denetim hatası, 2 = çalıştırılamadı.

.EXAMPLE
  .\tools\SelfTest.ps1
#>
param(
    # Hazır bir Zaplio.exe; verilmezse proje geçici bir klasöre derlenir.
    [string]$Exe,
    # Uygulamanın bitmesi için beklenecek en uzun süre (saniye).
    [int]$TimeoutSeconds = 120,
    # Verilirse her ekranın görüntüsü bu klasöre PNG olarak yazılır (tasarımı gözle incelemek için).
    [string]$Shots
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if (-not $Exe) {
    $build = Join-Path $env:TEMP 'zaplio-selftest-build'
    dotnet build (Join-Path $root 'src\Zaplio') -o $build --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error 'Derleme başarısız.'; exit 2 }
    $Exe = Join-Path $build 'Zaplio.exe'
}
if (-not (Test-Path $Exe)) { Write-Error "Bulunamadı: $Exe"; exit 2 }

$data = Join-Path $env:TEMP ('zaplio-selftest-' + [guid]::NewGuid().ToString('N'))
$report = Join-Path $data 'rapor.txt'
New-Item -ItemType Directory -Force $data | Out-Null

$previous = $env:ZAPLIO_DATA_DIR
$env:ZAPLIO_DATA_DIR = $data
if ($Shots) {
    New-Item -ItemType Directory -Force $Shots | Out-Null
    $env:ZAPLIO_SELFTEST_SHOTS = (Resolve-Path $Shots).Path
}
try {
    $process = Start-Process $Exe -ArgumentList '--self-test', "`"$report`"" -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force
        Write-Output "SONUÇ: zaman aşımı ($TimeoutSeconds sn) - uygulama yanıt vermedi."
        exit 1
    }
    if (Test-Path $report) {
        $lines = Get-Content $report -Encoding UTF8
        $lines | Where-Object { $_ -like 'HATA*' }
        $lines | Select-Object -Last 1
    }
    else {
        Write-Output 'SONUÇ: rapor yazılmadı. Günlük:'
        Get-ChildItem (Join-Path $data 'logs') -ErrorAction SilentlyContinue | ForEach-Object { Get-Content $_.FullName -Encoding UTF8 -Tail 30 }
        exit 2
    }
    exit $process.ExitCode
}
finally {
    $env:ZAPLIO_DATA_DIR = $previous
    Remove-Item Env:ZAPLIO_SELFTEST_SHOTS -ErrorAction SilentlyContinue
    Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
}
