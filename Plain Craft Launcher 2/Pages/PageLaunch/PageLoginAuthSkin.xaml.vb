Public Class PageLoginAuthSkin

    Public Sub New()
        InitializeComponent()
        Skin.Loader = PageLaunchLeft.SkinAuth
    End Sub
    Private Sub PageLoginLegacy_Loaded() Handles Me.Loaded
        Skin.Loader.Start()
    End Sub

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        TextName.Text = Setup.Get("CacheAuthName")
        TextEmail.Text = Setup.Get("CacheAuthUsername")
        TextEmail.Visibility = If(Setup.Get("UiLauncherEmail"), Visibility.Collapsed, Visibility.Visible)
        PageLoginLegacy_Loaded()
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Dim Server As String = If(IsNothing(McVersionCurrent), Setup.Get("CacheAuthServerServer"), Setup.Get("VersionServerAuthServer", Version:=McVersionCurrent)) & "/authserver"
        Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = Setup.Get("CacheAuthUsername"), .Password = Setup.Get("CacheAuthPass"), .Description = "Authlib-Injector", .Type = McLoginType.Auth}
    End Function

    Private Sub PageLoginAuthSkin_MouseEnter(sender As Object, e As MouseEventArgs) Handles PanData.MouseEnter
        AniStart({
                 AaOpacity(BtnEdit, 1 - BtnEdit.Opacity, 80),
                 AaHeight(BtnEdit, 25.5 - BtnEdit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnEdit, -1.5, 50, 140, New AniEaseInFluent),
                 AaOpacity(BtnExit, 1 - BtnExit.Opacity, 80),
                 AaHeight(BtnExit, 25.5 - BtnExit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnExit, -1.5, 50, 140, New AniEaseInFluent)
        }, "PageLoginAuthSkin Button")
    End Sub
    Private Sub PageLoginAuthSkin_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanData.MouseLeave
        AniStart({
                 AaOpacity(BtnEdit, -BtnEdit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnEdit, 14 - BtnEdit.Height, 120,, New AniEaseInFluent),
                 AaOpacity(BtnExit, -BtnExit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnExit, 14 - BtnExit.Height, 120,, New AniEaseInFluent)
        }, "PageLoginAuthSkin Button")
    End Sub

    Private Sub BtnEdit_Click(sender As Object, e As EventArgs) Handles BtnEdit.Click
        If McLoginLoader.State = LoadState.Loading Then
            Log("[Launch] 要求更换角色，但登录加载器繁忙", LogLevel.Debug)
            If CType(McLoginLoader.Input, McLoginServer).ForceReselectProfile Then
                Hint("正在尝试更换，请稍候！")
            Else
                Hint("正在登录中，请稍后再更换角色！", HintType.Critical)
            End If
            Return
        End If
        Hint("正在尝试更换，请稍候！")
        Setup.Set("CacheAuthUuid", "") '清空选择缓存
        Setup.Set("CacheAuthName", "")
        RunInThread(
        Sub()
            Try
                Dim Data As McLoginServer = GetLoginData()
                Data.ForceReselectProfile = True
                McLoginLoader.WaitForExit(Data, IsForceRestart:=True)
                RunInUi(Sub() Reload(True))
            Catch ex As Exception
                Log(ex, "更换角色失败", LogLevel.Hint)
            End Try
        End Sub)
    End Sub
    Public Shared Sub ExitLogin() Handles BtnExit.Click
        Setup.Set("CacheAuthAccess", "")
        Setup.Set("CacheAuthUuid", "")
        Setup.Set("CacheAuthName", "")
        McLoginAuthLoader.Input = Nothing '防止因为输入的用户名密码相同，直接使用了上次登录的加载器结果
        FrmLaunchLeft.RefreshPage(False, True)
    End Sub

    Private Sub Skin_Click(sender As Object, e As MouseButtonEventArgs) Handles Skin.Click
        Dim Address As String = If(McVersionCurrent IsNot Nothing, Setup.Get("VersionServerAuthRegister", Version:=McVersionCurrent), Setup.Get("CacheAuthServerRegister"))
        If String.IsNullOrEmpty(New ValidateHttp().Validate(Address)) Then OpenWebsite(Address.Replace("/auth/register", "/user/closet"))
    End Sub

End Class
