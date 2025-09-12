


function Show-FastDownloadDllDialog {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    Add-Type -AssemblyName PresentationFramework
    Add-Type -AssemblyName PresentationCore


    $settings = New-Object FastDownloadDll.FastDownloadDll
    $ctrl = New-Object FastDownloadDll.FastDownloadDll.FastDownloadDll ($settings)
    $window = New-Object Windows.Window
    $window.Title = "Test GridConfigPage"
    $window.Content = $ctrl
    $window.Width = 500
    $window.Height = 400
    $window.ShowDialog()

}
