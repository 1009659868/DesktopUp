#requires -Version 5.1
# Shared by setup/build/run/package. Callers restore environment and console
# output encoding in finally. Tool archives are versioned and hash-checked.
function Get-DesktopUpToolchain {
    param([string]$MakeNsis)
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
    $global = Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.sdk.version -cne $global.sdk.version) {
        throw 'Update global.json and scripts/toolchain.json together (SDK URL and hash included).'
    }
    $tools = Join-Path $repo 'out/toolchains'
    $sdk = Join-Path $tools ('dotnet-' + $manifest.sdk.version)
    if (!$MakeNsis) { $MakeNsis = Join-Path $tools ('nsis-' + $manifest.nsis.version + '/makensis.exe') }
    elseif (![IO.Path]::IsPathRooted($MakeNsis)) { $MakeNsis = Join-Path $repo $MakeNsis }
    [pscustomobject]@{
        Repo=$repo; Tools=$tools; Manifest=$manifest; SdkRoot=$sdk
        DotNet=(Join-Path $sdk 'dotnet.exe'); MakeNsis=[IO.Path]::GetFullPath($MakeNsis)
        Packages=(Join-Path $repo 'out/nuget/packages')
        OfflineConfig=(Join-Path $repo 'out/nuget/offline.config')
        SdkPresent=((Test-Path -LiteralPath (Join-Path $sdk 'dotnet.exe') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $sdk ('sdk/' + $manifest.sdk.version + '/dotnet.dll'))))
        NsisPresent=(Test-Path -LiteralPath $MakeNsis -PathType Leaf)
    }
}

function Receive-DesktopUpToolArchive {
    param([string]$Url, [string]$Path)
    Add-Type -AssemblyName System.Net.Http
    $client = New-Object Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds(60)
    $response = $null; $inputStream = $null; $outputStream = $null
    try {
        # Stream the large SDK archive without buffering it in PowerShell's web cmdlet.
        $response = $client.GetAsync($Url, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode() | Out-Null
        $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $outputStream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        $buffer = New-Object byte[] 262144
        $received = 0L
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        $nextReport = 15
        while ($true) {
            # Fail after one minute without a chunk, or 30 minutes in total.
            $read = $inputStream.ReadAsync($buffer, 0, $buffer.Length)
            if (!$read.Wait(60000)) { throw 'No download data received for 60 seconds.' }
            $count = $read.GetAwaiter().GetResult()
            if ($count -eq 0) { break }
            $outputStream.Write($buffer, 0, $count)
            $received += $count
            if ($elapsed.Elapsed.TotalSeconds -ge 1800) { throw 'Tool download exceeded 30 minutes.' }
            if ($elapsed.Elapsed.TotalSeconds -ge $nextReport) {
                Write-Host ('Downloaded {0:N1} MiB...' -f ($received / 1MB))
                $nextReport = $elapsed.Elapsed.TotalSeconds + 15
            }
        }
    } finally {
        if ($outputStream) { $outputStream.Dispose() }
        if ($inputStream) { $inputStream.Dispose() }
        if ($response) { $response.Dispose() }
        $client.Dispose()
    }
}

function Install-DesktopUpToolArchive {
    param($Tool, [string]$ToolsRoot, [string]$Destination, [string]$ArchiveName,
        [string]$Subdirectory, [switch]$Offline)
    if (Test-Path -LiteralPath $Destination) {
        throw "Incomplete tool directory: $Destination. Move it aside and rerun setup; it will not be overwritten."
    }
    $downloads = Join-Path $ToolsRoot 'downloads'
    New-Item -ItemType Directory -Path $downloads -Force | Out-Null
    $archive = Join-Path $downloads $ArchiveName
    if (!(Test-Path -LiteralPath $archive)) {
        if ($Offline) { throw "Offline tool archive missing: $archive. Run setup online once or copy a prepared out/toolchains directory." }
        Write-Host "Downloading $ArchiveName into the project..."
        $oldProgress = $ProgressPreference
        $oldTls = [Net.ServicePointManager]::SecurityProtocol
        try {
            $ProgressPreference = 'SilentlyContinue'
            [Net.ServicePointManager]::SecurityProtocol = $oldTls -bor [Net.SecurityProtocolType]::Tls12
            foreach ($url in @($Tool.url, $Tool.fallbackUrl) | Where-Object { $_ }) {
                $partial = $archive + '.' + [Guid]::NewGuid().ToString('N') + '.partial'
                try {
                    Receive-DesktopUpToolArchive -Url $url -Path $partial
                    if ((Get-FileHash -LiteralPath $partial -Algorithm $Tool.algorithm).Hash -ine $Tool.hash) {
                        throw 'Unexpected download contents (archive hash mismatch).'
                    }
                    [IO.File]::Move($partial, $archive)
                    break
                } catch { Write-Warning "Download not accepted from $url : $($_.Exception.Message)" }
            }
            if (!(Test-Path -LiteralPath $archive)) { throw "Could not download $ArchiveName. Check network/proxy, or place the verified archive at $archive. Partial downloads retained." }
        } finally { $ProgressPreference = $oldProgress; [Net.ServicePointManager]::SecurityProtocol = $oldTls }
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm $Tool.algorithm).Hash -ine $Tool.hash) {
        throw "Cached tool archive hash mismatch: $archive. Move it aside and rerun setup."
    }
    $stage = Join-Path $ToolsRoot ('extract-' + [Guid]::NewGuid().ToString('N'))
    Write-Host "Extracting $ArchiveName..."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $stage)
    $expanded = if ($Subdirectory) { Join-Path $stage $Subdirectory } else { $stage }
    # Same-root rename publishes a complete tool only; interrupted extraction is retained.
    $toolBoundary = [IO.Path]::GetFullPath($ToolsRoot).TrimEnd('\') + '\'
    foreach ($toolPath in @($expanded, $Destination)) {
        if (![IO.Path]::GetFullPath($toolPath).StartsWith($toolBoundary, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Tool extraction must stay within its tool directory.'
        }
    }
    [IO.Directory]::Move($expanded, $Destination)
    if ($Subdirectory) { [IO.Directory]::Delete($stage, $false) }
}

function Enter-DesktopUpToolchain {
    param([switch]$ForPackaging, [string]$MakeNsis, [switch]$Offline)
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or ![Environment]::Is64BitProcess) {
        throw 'Use 64-bit Windows PowerShell 5.1 or PowerShell 7 on Windows x64.'
    }
    $context = Get-DesktopUpToolchain -MakeNsis $MakeNsis
    if (!$context.SdkPresent) {
        Install-DesktopUpToolArchive -Tool $context.Manifest.sdk -ToolsRoot $context.Tools -Destination $context.SdkRoot -ArchiveName ('dotnet-sdk-' + $context.Manifest.sdk.version + '-win-x64.zip') -Offline:$Offline
    }
    if ($ForPackaging -and !$context.NsisPresent) {
        if ($MakeNsis) { throw "Explicit NSIS compiler not found: $MakeNsis" }
        Install-DesktopUpToolArchive -Tool $context.Manifest.nsis -ToolsRoot $context.Tools -Destination (Split-Path -Parent $context.MakeNsis) -ArchiveName ('nsis-' + $context.Manifest.nsis.version + '.zip') -Subdirectory ('nsis-' + $context.Manifest.nsis.version) -Offline:$Offline
    }
    $nuget = Join-Path $context.Repo 'out/nuget'
    $values = @{
        DOTNET_ROOT=$context.SdkRoot; DOTNET_ROOT_X64=$context.SdkRoot
        DOTNET_HOST_PATH=$context.DotNet; DOTNET_MULTILEVEL_LOOKUP='0'
        DOTNET_CLI_HOME=(Join-Path $context.Tools 'cli-home')
        NUGET_PACKAGES=$context.Packages; NUGET_HTTP_CACHE_PATH=(Join-Path $nuget 'http-cache')
        NUGET_SCRATCH=(Join-Path $nuget 'scratch')
        PATH=($context.SdkRoot + [IO.Path]::PathSeparator + $env:PATH)
    }
    foreach ($directory in @($values.DOTNET_CLI_HOME,$values.NUGET_PACKAGES,$values.NUGET_HTTP_CACHE_PATH,$values.NUGET_SCRATCH)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    if ($Offline) {
        $feed = Join-Path $nuget 'offline-feed'
        New-Item -ItemType Directory -Path $feed -Force | Out-Null
        $escaped = [Security.SecurityElement]::Escape($feed)
        $offlineXml = '<configuration><packageSources><clear/><add key="offline" value="' + $escaped + '"/></packageSources></configuration>'
        [IO.File]::WriteAllText($context.OfflineConfig, $offlineXml)
    }
    $saved = @{}
    foreach ($key in $values.Keys) {
        $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $values[$key], 'Process')
    }
    $context | Add-Member -NotePropertyName SavedEnvironment -NotePropertyValue $saved
    $context | Add-Member -NotePropertyName SavedConsoleOutputEncoding -NotePropertyValue ([Console]::OutputEncoding)
    Push-Location $context.Repo
    try {
        # .NET emits UTF-8, but PS 5.1 can still decode native pipelines with cached CP936.
        # Set the decoder before launching tools; nested callers restore their own value.
        [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
        $actual = (& $context.DotNet --version | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $actual -cne $context.Manifest.sdk.version) { throw "Unexpected local SDK: $actual" }
        if ($ForPackaging) {
            $nsisVersion = (& $context.MakeNsis /VERSION | Out-String).Trim()
            if ($LASTEXITCODE -ne 0 -or (!$MakeNsis -and $nsisVersion -cne ('v' + $context.Manifest.nsis.version))) {
                throw "Unexpected NSIS compiler: $nsisVersion"
            }
        }
        Write-Host "Using project .NET SDK $actual ($($context.SdkRoot))"
        return $context
    } catch { Exit-DesktopUpToolchain $context; throw }
    finally { Pop-Location }
}

function Exit-DesktopUpToolchain {
    param($Context)
    foreach ($key in $Context.SavedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($key, $Context.SavedEnvironment[$key], 'Process')
    }
    [Console]::OutputEncoding = $Context.SavedConsoleOutputEncoding
}
