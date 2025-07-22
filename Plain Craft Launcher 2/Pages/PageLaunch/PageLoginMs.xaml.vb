Public Class PageLoginMs

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        Dim IndexBefore = ComboAccounts.SelectedIndex
        '刷新下拉框列表
        ComboAccounts.Items.Clear()
        ComboAccounts.Items.Add(New MyComboBoxItem With {.Content = "添加新账号"})
        Try
            Dim MsJson As JObject = GetJson(Setup.Get("LoginMsJson"))
            For Each Account In MsJson
                Dim Item As MyListItem = CType(FindResource("ComboBoxItemTemplateWithDelete"), DataTemplate).LoadContent()
                Item.Tag = Account.Value.ToString
                Item.Title = Account.Key
                CType(Item.Buttons(0), MyIconButton).Tag = Account.Key
                ComboAccounts.Items.Add(Item)
            Next
        Catch ex As Exception
            Log(ex, $"微软登录信息出错，登录信息已被重置（{Setup.Get("LoginMsJson")}）", LogLevel.Hint)
            Setup.Set("LoginMsJson", "{}")
        End Try
        '如果不保留输入，刷新列表后自动选择第一项
        ComboAccounts.SelectedIndex = If(KeepInput, Math.Max(0, IndexBefore), 0)
    End Sub
    Private Sub ComboAccounts_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboAccounts.SelectionChanged
        If AniControlEnabled <> 0 OrElse ComboAccounts.SelectedItem Is Nothing OrElse ComboAccounts.ContentPresenter Is Nothing Then Return
        If TypeOf ComboAccounts.SelectedItem Is MyListItem Then
            ComboAccounts.ContentPresenter.Content = CType(ComboAccounts.SelectedItem, MyListItem).Title
        ElseIf TypeOf ComboAccounts.SelectedItem Is MyComboBoxItem Then
            ComboAccounts.ContentPresenter.Content = CType(ComboAccounts.SelectedItem, MyComboBoxItem).Content
        End If
    End Sub

    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginMs
        If FrmLoginMs Is Nothing Then Return New McLoginMs With {.OAuthRefreshToken = Setup.Get("CacheMsV2OAuthRefresh"), .UserName = Setup.Get("CacheMsV2Name")}
        Dim Result As McLoginMs = Nothing
        RunInUiWait(
        Sub()
            If FrmLoginMs.ComboAccounts.SelectedIndex = 0 Then
                Result = New McLoginMs
            Else
                Dim Item As MyListItem = FrmLoginMs.ComboAccounts.SelectedItem
                Result = New McLoginMs With {.OAuthRefreshToken = Item.Tag, .UserName = Item.Title}
            End If
        End Sub)
        Return Result
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginMs) As String
        If LoginData.OAuthRefreshToken = "" Then
            Return "请在登录账号后再启动游戏！"
        Else
            Return ""
        End If
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    Private Sub BtnLogin_Click(sender As Object, e As EventArgs) Handles BtnLogin.Click
        ComboAccounts.IsEnabled = False
        BtnLogin.IsEnabled = False
        BtnLogin.Text = "0%"
        RunInNewThread(
        Sub()
            Try
                McLoginMsLoader.Start(GetLoginData(), IsForceRestart:=True)
                Do While McLoginMsLoader.State = LoadState.Loading
                    RunInUi(Sub() BtnLogin.Text = Math.Round(McLoginMsLoader.Progress * 100) & "%")
                    Thread.Sleep(50)
                Loop
                If McLoginMsLoader.State = LoadState.Finished Then
                    RunInUi(Sub() FrmLaunchLeft.RefreshPage(False, True))
                ElseIf McLoginMsLoader.State = LoadState.Aborted Then
                    Throw New ThreadInterruptedException
                ElseIf McLoginMsLoader.Error Is Nothing Then
                    Throw New Exception("未知错误！")
                Else
                    Throw New Exception(McLoginMsLoader.Error.Message, McLoginMsLoader.Error)
                End If
            Catch ex As ThreadInterruptedException
                Hint("已取消登录！")
            Catch ex As Exception
                If ex.Message = "$$" Then
                ElseIf ex.Message.StartsWith("$") Then
                    Hint(ex.Message.TrimStart("$"), HintType.Critical)
                ElseIf TypeOf ex Is Security.Authentication.AuthenticationException AndAlso ex.Message.ContainsF("SSL/TLS") Then
                    Log(ex, "正版登录验证失败，请考虑在 [设置 → 其他] 中关闭 [在正版登录时验证 SSL 证书]，然后再试。" & vbCrLf & vbCrLf & "原始错误信息：", LogLevel.Msgbox)
                Else
                    Log(ex, "正版登录尝试失败", LogLevel.Msgbox)
                End If
            Finally
                RunInUi(
                Sub()
                    ComboAccounts.IsEnabled = True
                    BtnLogin.IsEnabled = True
                    BtnLogin.Text = "登录"
                End Sub)
            End Try
        End Sub, "Ms Login")
    End Sub

End Class
