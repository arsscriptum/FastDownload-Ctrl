#╔════════════════════════════════════════════════════════════════════════════════╗
#║                                                                                ║
#║   Get-WpfExtensionCtrl.ps1                                                     ║
#║   Test functions for my WPF control                                            ║
#║                                                                                ║
#╟────────────────────────────────────────────────────────────────────────────────╢
#║   Guillaume Plante <codegp@icloud.com>                                         ║
#║   Code licensed under the GNU GPL v3.0. See the LICENSE file for details.      ║
#╚════════════════════════════════════════════════════════════════════════════════╝



function Get-ExtensionControlDllPath {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Position = 0, Mandatory = $false, HelpMessage = "targets")]
        [ValidateSet("Debug", "Release")]
        [Alias('Target')]
        [string]$Configuration = "Release"
    )

    try {
        Write-Verbose "[Get-ExtensionControlDllPath] $ENV:Target"
        $LibBasename = "WebExtensionPack.Controls"
        $LibName = "{0}.dll" -f $LibBasename


        $SearchPath = if($Configuration -eq "Release"){"ql"}else{"$Global:BinariesDebugPath"}

        Write-Host "[Get-ExtensionControlDllPath] Target $Target - Search Path $SearchPath"
       [string[]]$AllDlls = Get-ChildItem -Path "$SearchPath" -Filter "*.dll" -File -Recurse | Select -ExpandProperty Fullname | sort
        $DllFound = $AllDlls.Where({ $_.EndsWith("$LibName") }) | Select -First 1
        if ($DllFound) {
            Write-Verbose "Found `"$DllFound`""
            return $DllFound
        }
        

        return $Null
    } catch {
        throw $_
    }
}

