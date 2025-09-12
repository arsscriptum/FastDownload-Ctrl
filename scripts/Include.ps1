

function Import-BuildSystemScripts {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    $regPath = "HKCU:\SOFTWARE\arsscriptum\development\powershell\buildsystem"
    $scriptsPath = (Get-ItemProperty -Path $regPath -Name "ScriptsPath").ScriptsPath
    if (-not (Test-Path $scriptsPath -PathType Container)) {
        throw "ScriptsPath '$scriptsPath' does not exist."
    }
    Get-ChildItem -Path $scriptsPath -Filter '*.ps1' | ForEach-Object {
        $fn = "$($_.FullName)"
        Write-Verbose "$fn"
        . "$fn"
    }
}


function Find-LoadedModule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Module,
        [Parameter(Mandatory = $false)]
        [string]$ProcessName,
        [Parameter(Mandatory = $false)]
        [switch]$Kill
    )

    $tasklistExe = (Get-Command "tasklist").Source
    $results = & "$tasklistExe" '/m' '/FO' 'CSV'
    $allProcesses = $results | ConvertFrom-Csv
    $allProcessesFiltered = $allProcesses | sort -Property PID -Descending
    $allProcessesCount = $allProcesses.Count
    Write-Host "$allProcessesFilteredCount Processes to search."
    if (-not ([string]::IsNullOrEmpty($ProcessName))) {
        $allProcessesFiltered = $allProcesses.Where({ $_. 'Image Name' -eq "$ProcessName" }) | sort -Property PID -Descending
        $allProcessesFilteredCount = $allProcessesFiltered.Count
        Write-Verbose "Filetering Process list with `"$ProcessName`". $allProcessesFilteredCount Processes to search ($allProcessesCount => $allProcessesFilteredCount)"
    }
    [System.Collections.ArrayList]$ResultsList = [System.Collections.ArrayList]::new()
    [string[]]$WhenIncluded = @()
    [string[]]$NotIncluded = @()

    foreach ($ps in $allProcessesFiltered) {
        [int]$ProcessId = $ps.PID
        [string]$ProcessName = $ps. 'Image Name'

        [string[]]$ModulesList = $ps.Modules.Split(',')
        $ModulesListCount = $ModulesList.Count
        $ModuleFound = $ModulesList.Contains($Module)
        if ($ModuleFound) {
            [pscustomobject]$pobj = [pscustomobject]@{
                ProcessName = $ProcessName
                ProcessId = $ProcessId
                FoundModule = $Module
                ModulesList = $ModulesList
            }
            [void]$ResultsList.Add($pobj)
            Write-Verbose " ✔ Process $ProcessName (pid $ProcessId) loaded $ModulesListCount modules. Out of which $Module was included"
            $WhenIncluded = $ModulesList
            if ($($WhenIncluded.Count) -gt $($NotIncluded.Count)) {
                $Diff = $WhenIncluded | Where-Object { $_ -notin $NotIncluded }
                $diffStr = $diff -join ", "
                Write-Verbose "Difference: $diffStr"
            }
        }
        else {
            Write-Verbose " ❌ Process $ProcessName (pid $ProcessId) loaded $ModulesListCount modules, but $Module was not included"
            $NotIncluded = $ModulesList
        }
    }


    if ($Kill) {
        foreach ($r in $ResultsList) {
            Write-Verbose "Killing pid $($r.ProcessId)"
            Get-Process -Id $($r.ProcessId) | Stop-Process -Force -Confirm:$False
        }
    }

    $ResultsList
}


function Get-ProcessUsingModule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Module,
        [Parameter(Mandatory = $false)]
        [switch]$Kill
    )

    $tasklistExe = (Get-Command "tasklist").Source

    $result = & "$tasklistExe" '/m' "$Module" '/FO' 'CSV'
    $found = $result | Where-Object { $_. 'Modules' -match [regex]::Escape($Module) }
    if (($result -match 'No tasks') -or (-not $found)) {
        Write-Host "[Get-ProcessUsingModule] " -f Blue -n
        Write-Host "No process found using module: $Module" -ForegroundColor DarkYellow
        return @()
    }

    $found | ForEach-Object {
        Write-Host "[Get-ProcessUsingModule] " -f Blue -n
        Write-Host "Image Name: $($_.'Image Name'), PID: $($_.PID), Modules: $($_.Modules)" -f White
        if ($Kill) {
            try {
                Write-Host "[Get-ProcessUsingModule] " -f Blue -n
                Stop-Process -Id ([int]$_.PID) -Force -ErrorAction Stop
                Write-Host "Killed PID $($_.PID)" -ForegroundColor White
            } catch {
                Write-Host "Failed to kill PID $($_.PID): $_" -ForegroundColor DarkRed
            }
        }
    }

    return $found
}


function Out-BuildProperty {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Position = 0, Mandatory = $true)]
        [string]$Name,
        [Parameter(Position = 1, Mandatory = $true)]
        [string]$Value
    )

    process {
        $NameStr = "{0,18}" -f $Name
        $ValueStr = "{0,-30}" -f $Value
        Write-Host "  $NameStr" -f DarkGray -n
        Write-Host "  $ValueStr" -f DarkCyan
    }

}


function Out-MsBuildProperties {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline = $true, Mandatory = $true, HelpMessage = "MsBuildProperties")]
        [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]]$MsBuildProperties
    )
    process {
        if (-not $MsBuildProperties -or $MsBuildProperties.Count -eq 0) {
            Write-Host "No MsBuildProperties"
        } else {
            Write-Host "MsBuildProperties" -f White
            foreach ($kv in $MsBuildProperties) {
                Out-BuildProperty "$($kv.Key)" "$($kv.Value)"
            }
        }
    }
}



function Out-BuildTitle {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Position = 0, Mandatory = $true, ValueFromPipeline = $True)]
        [string]$Title,
        [Parameter(Mandatory = $false)]
        [switch]$Red
    )

    process {
        $Len = 120
        $TitleLen = $Title.Length
        $LineSep = [string]::new('=', $Len)
        $SpacesLen = ($Len / 2) - ($TitleLen / 2)
        $Spaces = [string]::new(' ', $SpacesLen)
        $TitleStr = "{0}{1}{0}" -f $Spaces, $Title
        if ($Red) {
            Write-Host "$LineSep" -f DarkRed
            Write-Host "$TitleStr" -f DarkYellow
            Write-Host "$LineSep" -f DarkRed
        } else {
            Write-Host "$LineSep" -f DarkCyan
            Write-Host "$TitleStr" -f White
            Write-Host "$LineSep" -f DarkCyan
        }

    }

}

function Invoke-BuildRequest {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Position = 0, Mandatory = $true, HelpMessage = "BuildRequest object")]
        [BuildRequest]$BuildRequest = $Null,
        [Parameter(Mandatory = $false)]
        [switch]$Clean
    )

    $regPath = "HKCU:\SOFTWARE\arsscriptum\development\powershell\buildsystem"
    $dotnetPath = (Get-ItemProperty -Path $regPath -Name "DotNetPath").DotNetPath

    if (-not $BuildRequest) {
        $RequestsRemaining = Test-BuildRequestsRemaining
        if ($RequestsRemaining -eq 0) {
            Write-Host "No build request found in queue." -ForegroundColor Yellow
            return
        }
        $BuildRequest = Get-NextBuildRequest
    }

    $OutPath = Join-Path "$($BuildRequest.WorkingDirectory)" "$($BuildRequest.OutputPath)"
    $OutPath = Join-Path "$OutPath" "$($BuildRequest.Configuration)"
    $RealDeployPath = Join-Path "$($BuildRequest.DeployPath)" "$($BuildRequest.Configuration)"

    Out-BuildProperty "Build ID" "$($BuildRequest.BuildId)"
    Out-BuildProperty "GUID" "$($BuildRequest.GUID)"
    Out-BuildProperty "Working Directory" "$($BuildRequest.WorkingDirectory)"
    Out-BuildProperty "Project File" "$($BuildRequest.ProjectFilePath)"
    Out-BuildProperty "Architecture" "$($BuildRequest.Architecture)"
    Out-BuildProperty "Output Path" "$($BuildRequest.OutputPath)"
    Out-BuildProperty "Deploy Path" "$RealDeployPath"
    Out-BuildProperty "Configuration" "$($BuildRequest.Configuration)"
    Out-BuildProperty "Framework" "$($BuildRequest.Framework)"
    Out-BuildProperty "Version" "$($BuildRequest.Version)"
    Out-BuildProperty "Log Level" "$($BuildRequest.LogLevel)"
    $BuildRequest.MsBuildProperties | Out-MsBuildProperties

    # Clean output path if requested
    if ($Clean -and $OutPath -and (Test-Path $OutPath)) {
        Write-Host "Cleaning output path: $($OutPath)" -ForegroundColor Yellow
        Remove-Item -Path $OutPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    $args = @('build', "$($BuildRequest.ProjectFilePath)")
    if ($BuildRequest.Architecture) { $args += @('--arch', $BuildRequest.Architecture) }
    if ($BuildRequest.ArtifactsPath) { $args += @('--artifacts-path', $BuildRequest.ArtifactsPath) }
    if ($BuildRequest.Configuration) { $args += @('-c', $BuildRequest.Configuration) }
    if ($BuildRequest.Framework) { $args += @('-f', $BuildRequest.Framework) }
    if ($OutPath) { $args += @('-o', $OutPath) }
    if ($BuildRequest.Version) { $args += @('-p:Version=' + $BuildRequest.Version) }
    if ($BuildRequest.MsBuildProperties.Count -gt 0) {
        $propList = $BuildRequest.MsBuildProperties | ForEach-Object { "$($_.Key)=$($_.Value)" }
        $args += @("-p:" + ($propList -join ";"))
    }
    if ($BuildRequest.LogLevel) {
        $args += @('-v', "$($BuildRequest.LogLevel)")
    }
    if (-not $BuildRequest.Incremental) {
        $args += '--no-incremental'
    }

    Write-Host "dotnet command: $dotnetPath" -ForegroundColor Magenta
    Write-Host "dotnet arguments:" -ForegroundColor Magenta
    $args | ForEach-Object { Write-Host "  $_" -ForegroundColor Magenta }

    $processParams = @{
        FilePath = $dotnetPath
        ArgumentList = $args
        WorkingDirectory = $BuildRequest.WorkingDirectory
        NoNewWindow = $true
        PassThru = $true
    }
    if ($BuildRequest.RedirectStdOut) { $processParams['RedirectStandardOutput'] = (New-TemporaryFile).FullName }
    if ($BuildRequest.RedirectStdErr) { $processParams['RedirectStandardError'] = (New-TemporaryFile).FullName }

    $BuildRequest.Executed = [uint64]([math]::Floor((Get-Date).ToUniversalTime().Subtract([datetime]'1970-01-01').TotalSeconds))
    $proc = Start-Process @processParams
    while (-not $proc.HasExited) { Start-Sleep -Milliseconds 200 }

    $trackedProperties = @('Id', 'TotalProcessorTime', 'ExitCode', 'HasExited', 'StartTime', 'ExitTime', 'Responding')
    $data = [ordered]@{}
    foreach ($prop in $trackedProperties) {
        $data[$prop] = $proc.$prop
    }
    $BuildRequest.ProcessExecutionData = [pscustomobject]$data

    if ($BuildRequest.RedirectStdOut) { $BuildRequest.ProcessExecutionData | Add-Member -MemberType NoteProperty -Name 'StdOutPath' -Value $processParams['RedirectStandardOutput'] }
    if ($BuildRequest.RedirectStdErr) { $BuildRequest.ProcessExecutionData | Add-Member -MemberType NoteProperty -Name 'StdErrPath' -Value $processParams['RedirectStandardError'] }

    if ($proc.ExitCode -eq 0) {
        "Build Succeeded (ExitCode: 0)" | Out-BuildTitle
    } else {
        "Build Failed (ExitCode: $($proc.ExitCode))" | Out-BuildTitle -Red
    }

    $roboExe = (get-command "robocopy").Source
    if (Test-Path "$OutPath") {
        Write-Host "Files in OutputPath ($OutPath):" -ForegroundColor Blue
        Get-ChildItem -Path $OutPath -Recurse | ForEach-Object {
            $fn = "$($_.FullName)"
            Write-Host "  $relative" -ForegroundColor White -n
            Write-Host " copy to `"$RealDeployPath`"" -f DarkCyan

            $relative = $fn.Substring($OutPath.Length).TrimStart('\', '/')

        }
    }
    & "$roboExe" "$OutPath" "$RealDeployPath" * /E /NFL /NDL /NJH /NJS /NC /NS /NP /R:20 /W:5

}




New-Alias -Name StartBuild -Value Invoke-BuildRequest -Force -ErrorAction Ignore

