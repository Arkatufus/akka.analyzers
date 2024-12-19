param($installPath, $toolsPath, $package, $project)

if($project.Object.SupportsPackageDependencyResolution)
{
    if($project.Object.SupportsPackageDependencyResolution())
    {
        # Do not uninstall analyzers via uninstall.ps1, instead let the project system handle it.
        return
    }
}

$analyzersPath = Join-Path (Split-Path -Path $toolsPath -Parent) "analyzers\dotnet\roslyn3.11\cs"
if (Test-Path $analyzersPath)
{
    foreach ($analyzerFilePath in Get-ChildItem -Path "$analyzersPath\*.dll" -Exclude *.resources.dll)
    {
        if($project.Object.AnalyzerReferences)
        {
            $project.Object.AnalyzerReferences.Remove($analyzerFilePath.FullName)
        }
    }
}
