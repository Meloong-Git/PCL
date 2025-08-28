Public Class PageLoginNide
    Private IsFirstLoad As Boolean = True
    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        '记住密码
        CheckRemember.Checked = Setup.Get("LoginRemember")
        If KeepInput AndAlso Not IsFirstLoad Then '避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            '保留输入，只刷新下拉框列表
            Dim Input As String = ComboName.Text
            ComboName.ItemsSource = If(Setup.Get("LoginNideEmail") = "", Nothing, Setup.Get("LoginNideEmail").ToString.Split("¨"))
            ComboName.Text = Input
        Else
            '不保留输入，刷新列表后自动选择第一项
            If Setup.Get("LoginNideEmail") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Setup.Get("LoginNideEmail").ToString.Split("¨")
                ComboName.Text = Setup.Get("LoginNideEmail").ToString.BeforeFirst("¨")
                If Setup.Get("LoginRemember") Then TextPass.Password = Setup.Get("LoginNidePass").ToString.BeforeFirst("¨").Trim
            End If
        End If
        IsFirstLoad = False
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Dim Server As String = If(IsNothing(McVersionCurrent), Setup.Get("CacheNideServer"), Setup.Get("VersionServerNide", Version:=McVersionCurrent))
        If FrmLoginNide Is Nothing Then
            Return New McLoginServer(McLoginType.Nide) With {.Token = "Nide", .UserName = "", .Password = "", .Description = GetLang("LangPageLoginNideDesc"), .Type = McLoginType.Nide, .BaseUrl = "https://auth.mc-user.com:233/" & Server & "/authserver"}
        Else
            Return New McLoginServer(McLoginType.Nide) With {.Token = "Nide", .UserName = FrmLoginNide.ComboName.Text.Replace("¨", "").Trim, .Password = FrmLoginNide.TextPass.Password.Replace("¨", "").Trim, .Description = GetLang("LangPageLoginNideDesc"), .Type = McLoginType.Nide, .BaseUrl = "https://auth.mc-user.com:233/" & Server & "/authserver"}
        End If
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginServer) As String
        If LoginData.UserName = "" Then Return GetLang("LangPageLoginNideNoEmptyAccount")
        If LoginData.Password = "" Then Return GetLang("LangPageLoginNideNoEmptyPassword")
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    '保存输入信息
    Private Sub ComboName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If sender.Text = "" Then TextPass.Password = ""
        If AniControlEnabled = 0 Then Setup.Set("CacheNideAccess", "")  '迫使其不进行 Validate
    End Sub
    Private Sub TextPass_PasswordChanged(sender As Object, e As RoutedEventArgs) Handles TextPass.PasswordChanged
        If AniControlEnabled = 0 Then Setup.Set("CacheNideAccess", "")
    End Sub
    Private Sub ComboName_SelectionChanged(sender As MyComboBox, e As SelectionChangedEventArgs) Handles ComboName.SelectionChanged
        If sender.SelectedIndex = -1 OrElse Not Setup.Get("LoginRemember") Then
            TextPass.Password = ""
        Else
            TextPass.Password = Setup.Get("LoginNidePass").ToString.Split("¨")(sender.SelectedIndex).Trim
        End If
    End Sub
    Private Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckRemember.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    End Sub

    '链接处理
    Private Sub ComboName_TextChanged() Handles ComboName.TextChanged
        BtnLink.Content = If(ComboName.Text = "", GetLang("LangPageLoginNideRegister"), GetLang("LangPageLoginNideForgetPassword"))
    End Sub
    Private Sub Btn_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        If BtnLink.Content = GetLang("LangPageLoginNideRegister") Then
            OpenWebsite("https://login.mc-user.com:233/" & Setup.Get("VersionServerNide", Version:=McVersionCurrent) & "/register")
        Else
            OpenWebsite("https://login.mc-user.com:233/account/login")
        End If
    End Sub

End Class
