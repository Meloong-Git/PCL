Class PageSetupLink

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageSetupLink_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        TextLinkName.Text = Setup.Get("LinkName")
        CheckHiperCertWarn.Checked = Setup.Get("LinkHiperCertWarn")
    End Sub

    '初始化
    Public Sub Reset()
        Try
            Setup.Reset("LinkName")
            Setup.Reset("LinkHiperCertWarn")

            Log("[Setup] 已初始化联机页设置")
            Hint("已初始化联机页设置！", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化联机页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextLinkName.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckHiperCertWarn.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    End Sub

    Private Sub BtnHiperLog_Click(sender As Object, e As EventArgs) Handles BtnHiperLog.Click
    End Sub

End Class
