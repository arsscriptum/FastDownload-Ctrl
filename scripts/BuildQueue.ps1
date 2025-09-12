
$Global:BuildQueue = [System.Collections.Queue]::new()

function New-BuildRequest {
    param(
        [string]$WorkingDirectory,
        [string]$ProjectFilePath,
        [string]$Architecture,
        [string]$OutputPath,
        [string]$DeployPath,
        [string]$ArtifactsPath,
        [string]$Configuration,
        [string]$Framework,
        [string]$Version,
        [BuildVerbosity]$LogLevel = [BuildVerbosity]::Normal,
        [string]$Owner = ""
    )
    $request = [BuildRequest]::new($WorkingDirectory, $ProjectFilePath, $Architecture, $OutputPath, $DeployPath, $ArtifactsPath, $Configuration, $Framework, $Version, $LogLevel, $Owner)
    $Global:BuildQueue.Enqueue($request)
    return $request
}

function Get-NextBuildRequest {
    if ($Global:BuildQueue.Count -eq 0) { return $null }
    return $Global:BuildQueue.Dequeue()
}

function Test-BuildRequestInQueue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = "ById")]
        [uint32]$BuildId,
        [Parameter(Mandatory = $true, ParameterSetName = "ByGuid")]
        [string]$GUID
    )
    foreach ($req in $Global:BuildQueue) {
        if ($PSCmdlet.ParameterSetName -eq "ById" -and $req.BuildId -eq $BuildId) { return $true }
        if ($PSCmdlet.ParameterSetName -eq "ByGuid" -and $req.GUID -eq $GUID) { return $true }
    }
    return $false
}

function Test-BuildRequestsRemaining {
    [CmdletBinding()]
    param()
    return ($Global:BuildQueue.Count -gt 0)
}


New-Alias -Name BuildsRemaining -Value Test-BuildRequestsRemaining -Force -ErrorAction Ignore
New-Alias -Name NextBuild -Value Get-NextBuildRequest -Force -ErrorAction Ignore

