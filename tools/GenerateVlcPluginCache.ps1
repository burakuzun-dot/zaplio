# Writes VLC's plugin cache (plugins\plugins.dat) next to a LibVLC copy.
# With the cache LibVLC loads only the plugins a stream needs instead of all ~320 DLLs at start-up,
# which on Windows with Smart App Control / Defender took ~35 s for freshly copied files.
# Called from src/Zaplio/Zaplio.csproj after Build and Publish; never fails the build.
param([Parameter(Mandatory = $true)][string]$VlcDir)

$ErrorActionPreference = 'Stop'
try {
    $VlcDir = (Resolve-Path $VlcDir).Path
    $cache = Join-Path $VlcDir 'plugins\plugins.dat'
    if (Test-Path $cache) { return }
    if (-not (Test-Path (Join-Path $VlcDir 'libvlc.dll'))) { Write-Host "VLC plugin cache: libvlc.dll not found in $VlcDir"; return }

    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ZaplioVlcCache {
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SetDllDirectory(string path);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr LoadLibrary(string path);
    [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr libvlc_new(int argc, string[] argv);
    [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] public static extern void libvlc_release(IntPtr instance);
}
"@
    [ZaplioVlcCache]::SetDllDirectory($VlcDir) | Out-Null
    [ZaplioVlcCache]::LoadLibrary((Join-Path $VlcDir 'libvlccore.dll')) | Out-Null
    [ZaplioVlcCache]::LoadLibrary((Join-Path $VlcDir 'libvlc.dll')) | Out-Null
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $vlcArgs = [string[]]@('--no-video-title-show', '--reset-plugins-cache')
    $instance = [ZaplioVlcCache]::libvlc_new($vlcArgs.Length, $vlcArgs)
    if ($instance -ne [IntPtr]::Zero) { [ZaplioVlcCache]::libvlc_release($instance) }
    Write-Host "VLC plugin cache: $(if (Test-Path $cache) { 'written' } else { 'NOT written' }) in $($watch.ElapsedMilliseconds) ms ($cache)"
}
catch {
    Write-Host "VLC plugin cache skipped: $($_.Exception.Message)"
}
