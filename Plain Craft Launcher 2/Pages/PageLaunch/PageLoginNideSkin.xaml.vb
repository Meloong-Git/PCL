Public Class PageLoginNideSkin

    Public Sub New()
        InitializeComponent()
        Skin.Loader = PageLaunchLeft.SkinNide
    End Sub
    Private Sub PageLoginLegacy_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Skin.Loader.Start()
    End Sub

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        TextName.Text = Setup.Get("CacheNideName")
        TextEmail.Text = Setup.Get("CacheNideUsername")
        TextEmail.Visibility = If(Setup.Get("UiLauncherEmail"), Visibility.Collapsed, Visibility.Visible)
        '皮肤在 Loaded 加载
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Dim Server As String = If(IsNothing(McVersionCurrent), Setup.Get("CacheNideServer"), Setup.Get("VersionServerNide", Version:=McVersionCurrent))
        Return New McLoginServer(McLoginType.Nide) With {.Token = "Nide", .UserName = Setup.Get("CacheNideUsername"), .Password = Setup.Get("CacheNidePass"), .Description = GetLang("LangPageLoginNideDesc"), .Type = McLoginType.Nide, .BaseUrl = "https://auth.mc-user.com:233/" & Server & "/authserver"}
    End Function

    Private Sub PageLoginNideSkin_MouseEnter(sender As Object, e As MouseEventArgs) Handles PanData.MouseEnter
        AniStart({
                 AaOpacity(BtnEdit, 1 - BtnEdit.Opacity, 80),
                 AaHeight(BtnEdit, 25.5 - BtnEdit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnEdit, -1.5, 50, 140, New AniEaseInFluent),
                 AaOpacity(BtnExit, 1 - BtnExit.Opacity, 80),
                 AaHeight(BtnExit, 25.5 - BtnExit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnExit, -1.5, 50, 140, New AniEaseInFluent)
        }, "PageLoginNideSkin Button")
    End Sub
    Private Sub PageLoginNideSkin_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanData.MouseLeave
        AniStart({
                 AaOpacity(BtnEdit, -BtnEdit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnEdit, 14 - BtnEdit.Height, 120,, New AniEaseInFluent),
                 AaOpacity(BtnExit, -BtnExit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnExit, 14 - BtnExit.Height, 120,, New AniEaseInFluent)
        }, "PageLoginNideSkin Button")
    End Sub

    Private Sub BtnEdit_Click(sender As Object, e As EventArgs) Handles BtnEdit.Click
        OpenWebsite("https://login.mc-user.com:233/account/changepw")
    End Sub
    Public Shared Sub ExitLogin() Handles BtnExit.Click
        Setup.Set("CacheNideAccess", "")
        McLoginNideLoader.Input = Nothing '防止因为输入的用户名密码相同，直接使用了上次登录的加载器结果
        FrmLaunchLeft.RefreshPage(False, True)
    End Sub

    Private Sub Skin_Click(sender As Object, e As MouseButtonEventArgs) Handles Skin.Click
        OpenWebsite("https://login.mc-user.com:233/" & If(IsNothing(McVersionCurrent), Setup.Get("CacheNideServer"), Setup.Get("VersionServerNide", Version:=McVersionCurrent)) & "/skin")
    End Sub

End Class
