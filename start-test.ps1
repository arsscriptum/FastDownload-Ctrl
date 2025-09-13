

#╔════════════════════════════════════════════════════════════════════════════════╗
#║                                                                                ║
#║   start-test.ps1                                                               ║
#║   Test functions for my WPF control                                            ║
#║                                                                                ║
#╟────────────────────────────────────────────────────────────────────────────────╢
#║   Guillaume Plante <codegp@icloud.com>                                         ║
#║   Code licensed under the GNU GPL v3.0. See the LICENSE file for details.      ║
#╚════════════════════════════════════════════════════════════════════════════════╝




$ProjectPath = (Resolve-Path -PAth "$PSScriptRoot").Path
$libsPath = Join-Path $ProjectPath "libs"
$BuildPath = Join-Path $ProjectPath "src"
$BinPath = Join-Path $BuildPath "bin"
$ArtifactsPath = Join-Path $BuildPath "artifacts"
$Target = "Release"

$CommonScript = Join-Path $scriptsPath "Common.ps1"
$IncludeScript = Join-Path $scriptsPath "Include.ps1"
$BuildQueueScript = Join-Path $scriptsPath "BuildQueue.ps1"
$BuildRequestScript = Join-Path $scriptsPath "BuildRequest.ps1"
$RegisterDepScript = Join-Path $scriptsPath "Register-Dependencies.ps1"

Write-Host "=========================================================" -f DarkGray
Write-Host " Global Class Declaration (build queue, build requests, etc...)`n" -f DarkGray
Write-Host "  ✔️  Including Script $CommonScript" -f DarkCyan
. "$CommonScript"
Write-Host "  ✔️  Including Script $IncludeScript" -f DarkCyan
. "$IncludeScript"
Write-Host "  ✔️  Including Script $BuildQueueScript" -f DarkCyan
. "$BuildQueueScript"
Write-Host "  ✔️  Including Script $BuildRequestScript" -f DarkCyan
. "$BuildRequestScript"

. "$CommonScript" -Reset

Write-Host "=========================================================" -f DarkGray
Write-Host " Dependencies...`n" -f DarkGray
Write-Host "  ✔️  Including Script $RegisterDepScript" -f Magenta
. "$RegisterDepScript"


$DllPath = Join-Path $libsPath $Target
$DllPath = Join-Path $DllPath "WebExtensionPack.Controls.dll"


Write-Host "=========================================================" -f DarkYellow
Write-Host " Register-ExtensionControlDll -- Registering Controls...`n" -f DarkRed
Write-Host "  ✔️  Registering Extension Control $DllPath`n" -f DarkRed
Write-Host "  ⚠️  IMPORTANT NOTE  ⚠️  " -f White
Write-Host "  =======================   " -f DarkYellow
Write-Host "  The WpfControl was loaded through the Dll ❗❗❗" -f White
Write-Host "  Remember that the file will remain locked and copy-protected"
Write-Host "  until the powershell process (pid $pid) is killed ❗❗❗" -f White
try{
  Register-ExtensionControlDll Release
  Get-ProcessUsingModule "WebExtensionPack.Controls.dll"
}catch{
  Write-Error "$_"
}
