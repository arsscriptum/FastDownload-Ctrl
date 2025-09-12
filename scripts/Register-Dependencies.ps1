#╔════════════════════════════════════════════════════════════════════════════════╗
#║                                                                                ║
#║   Register-Dependencies.ps1                                                    ║
#║   Test functions for my WPF control                                            ║
#║                                                                                ║
#╟────────────────────────────────────────────────────────────────────────────────╢
#║   Guillaume Plante <codegp@icloud.com>                                         ║
#║   Code licensed under the GNU GPL v3.0. See the LICENSE file for details.      ║
#╚════════════════════════════════════════════════════════════════════════════════╝


[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false, HelpMessage = "targets")]
    [switch]$ErrorDetails,
    [Parameter(Mandatory = $false, HelpMessage = "ShowStack")]
    [switch]$ShowStack
)

try{

 (Get-ChildItem -Path "$PSScriptRoot" -File -Filter "*.ps1") | Where { $_.Name -ne "Register-Dependencies.ps1" }  | Select -ExpandProperty Fullname | % {
    $fn = "$_"
    Write-Host "  ✔️ Sourcing " -f White -n 
    Write-Host "$fn" -f DarkMagenta
    . "$fn"
 }

}catch{
    Show-ExceptionDetails  ($_) -ShowStack
}
New-Alias -Name incdeps -Value Register-ScriptDependencies -Option AllScope -Force -ErrorAction Ignore

