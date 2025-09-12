





function Write-DevProjectSettings {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(position = 0, Mandatory = $true)]
        [string]$Project,
        [Parameter(position = 1, Mandatory = $true)]
        [pscustomobject]$ProjectSettings,
        [Parameter(Mandatory = $false)]
        [switch]$Reset
    )


    [string]$registryPath = 'HKCU:\SOFTWARE\arsscriptum\development\projects\{0}' -f $Project
    $registryPathTypes = "$registryPath\Types"

    if ($Reset) {
        Remove-Item -Path $registryPath -Force | Out-Null
        Remove-Item -Path $registryPathTypes -Force | Out-Null
    }

    # Ensure the registry path exists
    if (-not (Test-Path $registryPath)) {
        New-Item -Path $registryPath -Force | Out-Null
    }
    if (-not (Test-Path $registryPathTypes)) {
        New-Item -Path $registryPathTypes -Force | Out-Null
    }

    foreach ($prop in $ProjectSettings.PSObject.Properties) {
        $VariableName = "$($prop.Name)"
        $value = $prop.Value
        $type = $value.GetType()
        $VariableValue = ($prop.Value -as $prop.TypeNameOfValue)
        $VariableType = ($prop.Value).GetType()
        $VariableTypeFull = $VariableType.FullName
        $RegValueType = 'String' # Default

        Write-Verbose "$VariableName is a [$($VariableType.Name)] ($VariableTypeFull)"

        $DefaultType = $False

        if (($VariableType -eq [uint32]) -or ($VariableType -eq [int32])) {
            $RegValueType = 'DWord'
        } elseif (($VariableType -eq [bool]) -or ($VariableType -eq [Boolean])) {
            $RegValueType = 'Binary'
        } elseif (($VariableType -eq [decimal]) -or ($VariableType -eq [int64]) -or ($VariableType -eq [uint64])) {
            $RegValueType = 'QWord'
        } elseif ($VariableType -eq [string[]]) {
            $RegValueType = 'MultiString'
        } elseif ($VariableType -eq [string]) {
            $RegValueType = if ($VariableValue -match '[$%]') { 'ExpandString' } else { 'String' }
        } else {
            $DefaultType = $True
            $RegValueType = 'String'
        }

        if ($DefaultType) {
            Write-Verbose "cannot identify $VariableName registry type. default to string"
        } else {
            Write-Verbose "identified $VariableName registry type to $RegValueType"
        }
        Write-Verbose "Property `"$VariableName`" has value $VariableValue as [$VariableType] ($VariableTypeFull). Saved as $RegValueType"
        # Write to registry
        try {
            # registry value type -> $Kind
            #  "String", "ExpandString", "Binary", "DWord", "MultiString", "QWord"
            Write-Verbose "[Write-DevProjectSettings] New-ItemProperty -Path $registryPath -Name $VariableName -Value $VariableValue -PropertyType $RegValueType"

            New-ItemProperty -Path $registryPath -Name $VariableName -Value $VariableValue -PropertyType $RegValueType -Force | Out-null
            New-ItemProperty -Path $registryPathTypes -Name "$VariableName" -Value "$VariableTypeFull" -PropertyType 'String' -Force | Out-null
        } catch {
            Write-Warning "Failed to register '$property_name': $_"
        }
    }
}


function Read-DevProjectSettings {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, position = 0, HelpMessage = "Name of the scheduled task")]
        [string]$Project
    )

    process {


        $registryPath = 'HKCU:\SOFTWARE\arsscriptum\development\projects\{0}' -f $Project
        $registryPathTypes = "$registryPath\Types"
        $registryPathProcesses = "$registryPath\processes"

        if (-not (Test-Path $registryPath)) {
            return $Null
        }

        $TaskProperties = [pscustomobject]@{}
        $Key = Get-Item -Path $registryPath -ErrorAction Stop
        $Properties = $Key.Property

        foreach ($property_name in $Properties) {
            try {
                # Read raw string type from Types subkey
                [string[]]$processes_using = (Get-ItemProperty -Path $registryPathProcesses -Name 'processes_using'). 'processes_using'
                $typeString = (Get-ItemProperty -Path $registryPathTypes -Name $property_name).$property_name
                $property_value = (Get-ItemProperty -Path $registryPath -Name $property_name).$property_name

                # Convert type string to actual [type]
                $resolvedType = [type]::GetType($typeString, $false)

                if ($null -eq $resolvedType) {
                    Write-Warning "Could not resolve type '$typeString' for property '$property_name'. Defaulting to string."
                    $resolvedType = [string]
                }

                # Try casting the value to the original type
                if (($resolvedType -eq [bool]) -or ($resolvedType -eq [Boolean])) {
                    $converted_boolean = if ($property_value -eq '0') { $False } else { $True }
                    $convertedValue = [bool]::Parse($converted_boolean)
                }
                elseif ($resolvedType.IsEnum) {
                    $convertedValue = [Enum]::Parse($resolvedType, $property_value)
                }
                elseif ($resolvedType -eq [string[]] -and ($property_value -is [string])) {
                    $convertedValue =, $property_value # Ensure it's an array
                }
                else {
                    $convertedValue = $property_value -as $resolvedType
                }

                Write-Verbose "Restoring '$property_name' as [$($resolvedType.FullName)]: $convertedValue"

                $TaskProperties | Add-Member -MemberType NoteProperty -Name $property_name -Value $convertedValue -Force
                $TaskProperties | Add-Member -MemberType NoteProperty -Name "processes_using" -Value $processes_using -Force
            } catch {
                Write-Warning "Failed to restore property '$property_name': $_"
            }
        }

        return $TaskProperties
    }
}


function Add-FastDownload-CtrlProcessId {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, position = 0, HelpMessage = "Name of the scheduled task")]
        [uint32]$Id
    )

    process {
        [string]$ProjectKey = 'HKCU:\SOFTWARE\arsscriptum\development\projects\FastDownload-Ctrl\processes'

        # Ensure parent key exists
        if (-not (Test-Path $ProjectKey)) {
            New-Item -Path $ProjectKey -Force | Out-Null
        }

        $props = Get-ItemProperty -Path $ProjectKey

        [System.Collections.ArrayList]$pList = [System.Collections.ArrayList]::new()
        if (!($props.processes_using)) {
            write-verbose "no process ids flagged so far. First!"
            [void]$pList.Add("$Id")
        } else {
            $duplicate = ($props.processes_using -as [string[]]).Contains("$Id")
            if ($duplicate) {
                write-verbose "id $Id already flagged. bailing out"
                return;
            }
            write-verbose "making a copy of all the ids flagged so far."
            ($props.processes_using -as [string[]]) | % { [void]$pList.Add("$_") }
            [void]$pList.Add("$Id")
            $entriesCount = $pList.Count
            write-verbose "new entries total $entriesCount "
            Remove-ItemProperty -Path $ProjectKey -Name "processes_using" -Force -ErrorAction Stop | Out-Null
        }
        write-verbose "saving process ids list"

        New-ItemProperty -Path $ProjectKey -Name "processes_using" -PropertyType MultiString -Value $pList -Force -ErrorAction Stop | Out-Null
    }
}



function Remove-FastDownload-CtrlProcessId {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    process {
        [string]$ProjectKey = 'HKCU:\SOFTWARE\arsscriptum\development\projects\FastDownload-Ctrl\processes'

        # Ensure parent key exists
        if (-not (Test-Path $ProjectKey)) {
            New-Item -Path $ProjectKey -Force | Out-Null
        }

        Remove-ItemProperty -Path $ProjectKey -Name "processes_using" -Force -ErrorAction Stop | Out-Null
        New-ItemProperty -Path $ProjectKey -Name "processes_using" -PropertyType MultiString -Value @() -Force -ErrorAction Stop | Out-Null
    }
}


function Write-FastDownload-CtrlSettings {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, position = 0, HelpMessage = "Name of the scheduled task")]
        [pscustomobject]$ProjectSettings
    )
    process {
        [string]$Project = 'FastDownload-Ctrl'
        Write-DevProjectSettings $Project $ProjectSettings
    }
}


function Reset-FastDownload-CtrlSettings {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Path
    )

    $BasePath = ""
    if ([string]::IsNullOrEmpty($Path)) {
        $BasePath = "C:\Dev\FastDownload-Ctrl"
    } else {
        $BasePath = (Resolve-Path -Path "$Path").Path
    }


    [pscustomobject]$ProjectSettings = [pscustomobject]@{
        'register_assemblies' = "register-FastDownloadDll"
        'unregister_assemblies' = "Unregister-FastDownloadDll"
        'project_root' = "$BasePath"
        'deployed_root_path' = "$BasePath\libs"
        'deploy_assemblies_path' = "$BasePath\libs\%Target%"
        'register_assemblies_after_build' = 0
        'scripts' = @("$BasePath\scripts\Get-FastDownloadDll.ps1",
            "$BasePath\scripts\Register-Control.ps1",
            "$BasePath\scripts\Register-Dependencies.ps1",
            "$BasePath\scripts\Show-TestDialog.ps1")
    }
    $ProjectSettings | Write-FastDownload-CtrlSettings
    Add-FastDownload-CtrlProcessId $pid

}




function Read-FastDownload-CtrlSettings {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    [string]$Project = 'FastDownload-Ctrl'
    return (Read-DevProjectSettings $Project)
}