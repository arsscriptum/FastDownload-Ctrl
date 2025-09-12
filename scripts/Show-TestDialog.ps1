
function Show-ExtensionItemDialog {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Register-ExtensionControlDll
    Add-Type -AssemblyName PresentationFramework

    # Create window
    $window = New-Object System.Windows.Window
    $window.Width = 400
    $window.Height = 120
    $window.Title = "ExtensionItem State Demo"
    $window.WindowStartupLocation = "CenterScreen"

    # Main grid
    $grid = New-Object System.Windows.Controls.Grid
    $grid.Margin = [System.Windows.Thickness]::new(8)

    $grid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition))
    $grid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition))

    # Add ExtensionItem control
    $ctrl = New-Object WebExtensionPack.Controls.ExtensionItem
    $ctrl.ResetExtensionStatus();
    $ctrl.ExtensionLabel = "Sample Extension"
    $ctrl.Status = [WebExtensionPack.Controls.ExtensionStatus]::Pending
    [System.Windows.Controls.Grid]::SetRow($ctrl, 0)
    $grid.Children.Add($ctrl)

    # Add Button
    $button = New-Object System.Windows.Controls.Button
    $button.Content = "Change State"
    $button.Width = 120
    $button.Height = 28
    $button.HorizontalAlignment = "Left"
    $button.Margin = [System.Windows.Thickness]::new(0, 10, 0, 0)
    [System.Windows.Controls.Grid]::SetRow($button, 1)
    $grid.Children.Add($button)

    # Cycling logic
    $states = [WebExtensionPack.Controls.ExtensionStatus]::GetValues([WebExtensionPack.Controls.ExtensionStatus])
    $i = 0
    $button.Add_Click({
            $ctrl.SetNextExtensionStatus();
            $currStatus = $ctrl.GetExtensionStatusString()
            $button.Content = "State: $currStatus (click to cycle)"
        })
    $button.Content = "State: Pending (click to cycle)"

    $window.Content = $grid
    $window.ShowDialog()
}


function Show-SettingsDialog {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    Add-Type -AssemblyName PresentationFramework
    Add-Type -AssemblyName PresentationCore


    $settings = New-Object WebExtensionPack.Controls.GridConfigSettings
    $ctrl = New-Object WebExtensionPack.Controls.GridConfigPagePageControl ($settings)
    $window = New-Object Windows.Window
    $window.Title = "Test GridConfigPage"
    $window.Content = $ctrl
    $window.Width = 500
    $window.Height = 400
    $window.ShowDialog()

}
