Class PageInstanceModDisabled

    Private Sub BtnDownload_Click(sender As Object, e As EventArgs) Handles BtnDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    End Sub
    Private Sub BtnVersion_Click(sender As Object, e As EventArgs) Handles BtnVersion.Click
        FrmMain.PageChange(FormMain.PageType.Launch) '在版本选择页面选定版本的时候只会返回一层，因此如果不先锚定 Launch，在选择版本后会回退到版本设置的这个页面
        FrmMain.PageChange(FormMain.PageType.InstanceSelect)
    End Sub

    Public Sub BtnDownload_Loaded() Handles BtnDownload.Loaded
        Dim NewVisibility = If((Setup.Get("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow) OrElse If(FrmSelectRight Is Nothing, False, FrmSelectRight.ShowHidden), Visibility.Collapsed, Visibility.Visible)
        If BtnDownload.Visibility <> NewVisibility Then
            BtnDownload.Visibility = NewVisibility
            PanMain.TriggerForceResize()
        End If
    End Sub

End Class
