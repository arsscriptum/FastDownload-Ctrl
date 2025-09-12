
#╔════════════════════════════════════════════════════════════════════════════════╗
#║                                                                                ║
#║   Common.ps1                                                                   ║
#║   Test functions for my WPF control                                            ║
#║                                                                                ║
#╟────────────────────────────────────────────────────────────────────────────────╢
#║   Guillaume Plante <codegp@icloud.com>                                         ║
#║   Code licensed under the GNU GPL v3.0. See the LICENSE file for details.      ║
#╚════════════════════════════════════════════════════════════════════════════════╝

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false)]
    [switch]$Reset
)

try {
    function Get-ProjectRootPath {
        [CmdletBinding(SupportsShouldProcess)]
        param()
        process {
            $ProjectRootPath = (Resolve-Path -Path "$PSScriptRoot\..").Path
            return $ProjectRootPath
        }
    }

    function Get-SourcesPath {
        [CmdletBinding(SupportsShouldProcess)]
        param()
        process {
            $SourcesPath = Join-Path (Get-ProjectRootPath) "src"
            return $SourcesPath
        }
    }


    function Get-BinariesPath {
        [CmdletBinding(SupportsShouldProcess)]
        param()

        $BinariesPath = Join-Path (Get-SourcesPath) "bin"
        return $BinariesPath
    }

    function Get-ProjectFrameworkVersion {
        [CmdletBinding(SupportsShouldProcess)]
        param()
        process {
            $SourcesPath = "net472"
            return $SourcesPath
        }
    }

    $DebugPath = Join-Path (Get-BinariesPath) "Debug"

    $ReleasePath = Join-Path (Get-BinariesPath) "Release"
    $DeployedRootPath = Join-Path (Get-ProjectRootPath) "libs"
    $ScriptsPath = Join-Path (Get-ProjectRootPath) "scripts"
    $Global:BinariesDebugPath = $DebugPath
    $Global:BinariesReleasePath = $ReleasePath
    Set-Variable -Name "BinariesDebugPath" -Value "$DebugPath" -Force -Option AllScope -Visibility Public -Scope Global -ErrorAction Ignore
    Set-Variable -Name "BinariesReleasePath" -Value "$ReleasePath" -Force -Option AllScope -Visibility Public -Scope Global -ErrorAction Ignore

    [pscustomobject]$Global:ProjectSettingsToSave = [pscustomobject]@{
        ProjectRootPath = Get-ProjectRootPath
        SourcesPath = Join-Path (Get-ProjectRootPath) "src"
        ScriptsPath = Join-Path (Get-ProjectRootPath) "scripts"
        BinariesPath = Join-Path (Get-SourcesPath) "bin"
        ObjectsPath = Join-Path (Get-SourcesPath) "obj"
        FrameworkVersion = "net472"
        DeployedRootPath = "$DeployedRootPath"

        BinariesDebugPath = $Global:BinariesDebugPath
        BinariesReleasePath = $Global:BinariesReleasePath
        DeployedAssembliesPath = "$($DeployedRootPath)\%Target%"


    }
    $Global:ProjectSettingsToSave
} catch {
    Write-Error "$_"
}


function Write-ProjectPathProperties {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, Position = 0, HelpMessage = "Name of the scheduled task")]
        [string]$ProjectName,

        [Parameter(Mandatory = $true, Position = 1, HelpMessage = "Properties of the scheduled task")]
        [pscustomobject]$ProjectProperties
    )

    $registryPath = "HKCU:\SOFTWARE\arsscriptum\development\projects\{0}\paths" -f $ProjectName
    $registryPathTypes = "$registryPath\Types"

    # Ensure the registry path exists
    if (-not (Test-Path $registryPath)) {
        New-Item -Path $registryPath -Force | Out-Null
    }
    if (-not (Test-Path $registryPathTypes)) {
        New-Item -Path $registryPathTypes -Force | Out-Null
    }

    foreach ($prop in $ProjectProperties.PSObject.Properties) {
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
            Write-Verbose "[Write-Sched$ProjectProperties] New-ItemProperty -Path $registryPath -Name $VariableName -Value $VariableValue -PropertyType $RegValueType"

            New-ItemProperty -Path $registryPath -Name $VariableName -Value $VariableValue -PropertyType $RegValueType -Force | Out-null
            New-ItemProperty -Path $registryPathTypes -Name "$VariableName" -Value "$VariableTypeFull" -PropertyType 'String' -Force | Out-null
        } catch {
            Write-Warning "Failed to register '$property_name': $_"
        }
    }
}



function Read-ProjectPathProperties {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, Position = 0, HelpMessage = "Name of the scheduled task")]
        [string]$ProjectName
    )

    process {

        $registryPath = "HKCU:\SOFTWARE\arsscriptum\development\projects\{0}\paths" -f $ProjectName
        $registryPathTypes = "$registryPath\Types"

        if (-not (Test-Path $registryPath)) {
            throw "Task registry path '$registryPath' does not exist."
        }


        $Key = Get-Item -Path $registryPath -ErrorAction Stop
        $Properties = $Key.Property

        foreach ($property_name in $Properties) {
            try {
                # Read raw string type from Types subkey
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

                $LogPart1 = "Registering new global variable `"{0}`"" -f "$property_name"
                $LogPart2 = "representing the path `"{0}`"" -f "$convertedValue"
                $LogFull = "   ✔   {0,-60} {1,-80}" -f "$LogPart1", "$LogPart2"


                try {
                    New-Variable -Name "$property_name" -Value "$convertedValue" -Option AllScope -Visibility Public -Force -Scope Global -Confirm:$false -ErrorAction Ignore
                    Write-Host "$LogFull" -f White
                } catch {

                    Write-Host "   ⚠️ variable `"$property_name`" already registered" -f White
                }

            } catch {
                Write-Warning "Failed to restore property '$property_name': $_"
            }
        }

        return $ProjectProperties
    }
}


function Reset-ProjectRegistrySettings {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$ProjectName = "FastDownload-Ctrl"
    )
    try {
        Write-ProjectPathProperties -ProjectName "$ProjectName" $Script:ProjectSettingsToSave
    } catch {
        Write-Error "$_"
    }
}

function Initialize-ProjectRegistrySettings {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$ProjectName = "FastDownload-Ctrl"
    )
    Read-ProjectPathProperties -ProjectName "$ProjectName"
}

if($Reset){
    Reset-ProjectRegistrySettings "FastDownload-Ctrl"
    Initialize-ProjectRegistrySettings "FastDownload-Ctrl"
}