[CmdletBinding()]
param(
    [string] $Version = '1.1.1',
    [switch] $SkipTests,
    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = Join-Path $repositoryRoot 'ImageConverter.sln'
$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish'
$releaseDirectory = Join-Path $repositoryRoot 'artifacts\release'
$buildLog = Join-Path ([IO.Path]::GetTempPath()) "ImageConverter-Build-$([Guid]::NewGuid().ToString('N')).log"
$versionProperty = "-p:Version=$Version"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)] [string] $Executable,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    & $Executable @Arguments 2>&1 | Tee-Object -FilePath $buildLog -Append
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE. Build log: $buildLog"
    }
}

Push-Location $repositoryRoot
try {
    Invoke-CheckedCommand dotnet @('restore', $solution, '-p:Platform=x64', '--nologo')
    Invoke-CheckedCommand dotnet @('build', $solution, '-c', 'Release', '-p:Platform=x64', '--no-restore', '--nologo')

    if (-not $SkipTests) {
        Invoke-CheckedCommand dotnet @(
            'test', 'tests\ImageConverter.Core.Tests\ImageConverter.Core.Tests.csproj',
            '-c', 'Release', '-p:Platform=x64', '--no-build', '--no-restore', '--nologo'
        )
    }

    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    Invoke-CheckedCommand dotnet @(
        'publish', 'src\ImageConverter.App\ImageConverter.App.csproj',
        '-c', 'Release', '-p:Platform=x64', '-r', 'win-x64',
        '--self-contained', 'true', '--no-restore',
        $versionProperty, '-o', $publishDirectory, '--nologo'
    )

    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    $zipPath = Join-Path $releaseDirectory 'ImageConverter-win-x64.zip'
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal

    if (-not $SkipInstaller) {
        $compilerCandidates = @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
        )
        $compiler = $compilerCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
        if (-not $compiler) {
            throw 'Inno Setup 6 was not found. Install it in the build environment or pass -SkipInstaller.'
        }
        Invoke-CheckedCommand $compiler @("/DMyAppVersion=$Version", 'installer\ImageConverter.iss')
    }

    Write-Output "Release build completed. Temporary log: $buildLog"
}
finally {
    Pop-Location
}
