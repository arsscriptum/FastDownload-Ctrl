
enum BuildVerbosity {
    Quiet = 0
    Minimal = 1
    Normal = 2
    Detailed = 3
    Diagnostic = 4
}

class BuildRequest {
    static [uint32] $NextBuildId = 1

    [uint32]$BuildId
    [string]$GUID
    [string]$WorkingDirectory
    [string]$ProjectFilePath
    [string]$Architecture
    [string]$OutputPath
    [string]$DeployPath
    [string]$ArtifactsPath
    [string]$Configuration
    [string]$Framework
    [string]$Version
    [bool]$Incremental
    [bool]$RedirectStdOut
    [bool]$RedirectStdErr
    [BuildVerbosity]$LogLevel
    [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]]$MsBuildProperties = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]]::new()
    [string]$Owner
    [uint64]$Created
    [uint64]$Executed
    [pscustomobject]$ProcessExecutionData

    BuildRequest(
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
    ) {
        $this.BuildId = [BuildRequest]::NextBuildId++
        $this.GUID = [guid]::NewGuid().ToString()
        $this.WorkingDirectory = $WorkingDirectory
        $this.ProjectFilePath = $ProjectFilePath
        $this.Architecture = $Architecture
        $this.OutputPath = $OutputPath
        $this.DeployPath = $DeployPath
        $this.ArtifactsPath = $ArtifactsPath
        $this.Configuration = $Configuration
        $this.Framework = $Framework
        $this.Version = $Version
        $this.Incremental = $false
        $this.RedirectStdOut = $false
        $this.RedirectStdErr = $false
        $this.LogLevel = $LogLevel
        $this.MsBuildProperties = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]]::new()
        $this.Owner = $Owner
        $this.Created = [uint64]( [math]::Floor((Get-Date).ToUniversalTime().Subtract([datetime]'1970-01-01').TotalSeconds) )
        $this.Executed = 0
        $this.ProcessExecutionData = $null
    }

    [void] AddProperty([string]$Key, [string]$Value) {
        $pair = [System.Collections.Generic.KeyValuePair[string,string]]::new($Key, $Value)
        $this.MsBuildProperties.Add($pair)
    }
}
