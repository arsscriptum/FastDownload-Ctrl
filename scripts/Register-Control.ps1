#╔════════════════════════════════════════════════════════════════════════════════╗
#║                                                                                ║
#║   Register-Control.ps1                                                         ║
#║   Test functions for my WPF control                                            ║
#║                                                                                ║
#╟────────────────────────────────────────────────────────────────────────────────╢
#║   Guillaume Plante <codegp@icloud.com>                                         ║
#║   Code licensed under the GNU GPL v3.0. See the LICENSE file for details.      ║
#╚════════════════════════════════════════════════════════════════════════════════╝



function Register-FastDownloadDll {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Position = 0, Mandatory = $false, HelpMessage = "targets")]
        [ValidateSet("Debug", "Release", "Any")]
        [string]$Target = "Any",
        [Parameter(Mandatory = $false)]
        [switch]$Force
    )

    $DoCheck = $True
    if ($Force) {
        $DoCheck = $False
    }


    if ($DoCheck) {
        if (Test-ExtensionControlLoaded) {
            Write-Verbose "[Register-FastDownloadDll] already loaded"
            return;
        }
    }

    $DllPath = Get-FastDownloadDllPath $Target
    if (($DllPath) -and (Test-Path $DllPath)) {
        Write-Verbose "[Register-FastDownloadDll] Add-Type -Path `"$DllPath`""
        $moduleType = Add-Type -Path "$DllPath" -Passthru
        $DllItem = Get-Item -Path "$DllPath"
        $DllBaseName = $DllItem.BaseName
        $typeNames = $moduleType | Select -ExpandProperty Name
        foreach ($typename in $typeNames) {
            $fullTypeName = "{0}.{1}" -f $DllBaseName, $typename
            $typeTest = Get-Type -FullName "$fullTypeName" -ErrorAction Ignore
            if ($typeTest) {
                $mtname = $typeTest.Name
                $mtmod = $typeTest.Module
                $log = " ✔  type {0} loaded from module `"{1}`"" -f $mtname, $mtmod
                Write-Host "$log" -f White
            } else {
                $mtname = $typeTest.Name
                $mtmod = $typeTest.Module
                $log = " ❌  type {0} not loaded" -f $fullTypeName
                Write-Host "$log" -f DarkYellow
            }

        }
    } else {
        throw "No Assemblies Found"
    }
}


function Unregister-FastDownloadDll {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $false, HelpMessage = "targets")]
        [switch]$Force
    )


    [System.Collections.ArrayList]$ProcessesFlagged = [System.Collections.ArrayList]::new()
    [System.Collections.ArrayList]$ProcessesRemaining = [System.Collections.ArrayList]::new()
    [System.Collections.ArrayList]$KilledProcessIds = [System.Collections.ArrayList]::new()
    Write-Verbose "Get-RegListItemList RegisteredExtensionControlProcessId"

    ((Read-WpfCtrlSettings | Select -ExpandProperty 'processes_using') -as [string[]]) | Select -Unique | sort | % { [void]$ProcessesRemaining.Add("$_") }
    $ProcessesRemainingCount = $ProcessesFlagged.Count
    $TotalKilledProcesses = 0
    Write-Verbose "$ProcessIdListCount Registration Entries"

    if ($ProcessIdListCount -gt 0) {
        foreach ($strProcessId in $ProcessesFlagged) {

            if ("$pid" -eq "$strProcessId") {
                $StrErr = " ⚠️ skipping ourselved for now"
                $Log = " {0,-30}" -f $StrErr
                Write-Host "$Log`t" -n
                Write-Host "$pname" -f DarkGreen

            } else {

                [int]$Id = $strProcessId -as [int]
                $ProcessPtr = Get-Process -Id $Id -ErrorAction Ignore
                if ($ProcessPtr) {
                    $Status = if ($Process.Responding) { "active and responding" } else { "process crashed, hanged" }
                    $pname = "Process Id {0} [{1}.exe]" -f $Id, $Process.Name
                    $StrLog = "FastDownloadDll {0} ({1})" -f $pname, $Status
                    Write-Verbose "$StrLog"
                    try {
                        $PtrId = $ProcessPtr.Id
                        $ProcessPtr | Stop-Process -ErrorAction Stop -Confirm:$False
                        $TotalKilledProcesses++
                        [void]$KilledProcessIds.Add($PtrId)
                        [void]$ProcessesRemaining.Remove($PtrId)

                        $StrErr = " ✔️ successfully killed"
                        $Log = " {0,-30}" -f $StrErr
                        Write-Host "$Log`t" -n
                        Write-Host "$pname" -f DarkGreen
                    } catch {
                        $StrErr = " ❌ Found Error"
                        $Log = " {0,-30}" -f $StrErr
                        Write-Host "$Log`t" -n
                        Write-Host "$pname" -f DarkYellow
                        Write-Verbose "Cannot Stop Process PID $Id"
                    }
                }

            }

        }
    } else {
        Write-Verbose "No Control Registration Found!"
    }
}



function Test-ExtensionControlLoaded {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    try {
        $Obj = [WebExtensionPack.Controls.ExtensionStatus] -as [type]
        return $True
    } catch {
        return $False
    }
}


