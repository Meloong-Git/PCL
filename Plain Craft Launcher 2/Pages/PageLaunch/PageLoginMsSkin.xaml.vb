Public Class PageLoginMsSkin

    Public Sub New()
        InitializeComponent()
        Skin.Loader = PageLaunchLeft.SkinMs
    End Sub
    Private Sub PageLoginLegacy_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Skin.Loader.Start()
    End Sub

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        TextName.Text = Setup.Get("CacheMsV2Name")
        '皮肤在 Loaded 加载
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginMs
        If McLoginMsLoader.State = LoadState.Finished Then
            Return New McLoginMs With {.OAuthRefreshToken = Setup.Get("CacheMsV2OAuthRefresh"), .UserName = Setup.Get("CacheMsV2Name"), .AccessToken = Setup.Get("CacheMsV2Access"), .Uuid = Setup.Get("CacheMsV2Uuid"), .ProfileJson = Setup.Get("CacheMsV2ProfileJson")}
        Else
            Return New McLoginMs With {.OAuthRefreshToken = Setup.Get("CacheMsV2OAuthRefresh"), .UserName = Setup.Get("CacheMsV2Name")}
        End If
    End Function

#Region "下边栏其他内容"

    '显示/隐藏控制
    Private Sub ShowPanel(sender As Object, e As MouseEventArgs) Handles PanData.MouseEnter
        AniStart(AaOpacity(PanButtons, 1 - PanButtons.Opacity, 120), "PageLoginMsSkin Button")
    End Sub
    Public Sub HidePanel() Handles PanData.MouseLeave
        If BtnEdit.ContextMenu.IsOpen OrElse BtnSkin.ContextMenu.IsOpen OrElse PanData.IsMouseOver Then Return
        AniStart(AaOpacity(PanButtons, -PanButtons.Opacity, 120), "PageLoginMsSkin Button")
    End Sub

    '修改账号信息
    Private Sub BtnEdit_Click(sender As Object, e As EventArgs) Handles BtnEdit.Click
        BtnEdit.ContextMenu.IsOpen = True
    End Sub
    Public Sub BtnEditPassword_Click(sender As Object, e As RoutedEventArgs)
        OpenWebsite("https://account.live.com/password/Change")
    End Sub
    Public Sub BtnEditName_Click(sender As Object, e As RoutedEventArgs)
        OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile")
    End Sub

    '退出登录
    Private Sub BtnExit_Click() Handles BtnExit.Click
        Setup.Set("CacheMsV2OAuthRefresh", "")
        Setup.Set("CacheMsV2Access", "")
        Setup.Set("CacheMsV2ProfileJson", "")
        Setup.Set("CacheMsV2Uuid", "")
        Setup.Set("CacheMsV2Name", "")
        McLoginMsLoader.Abort()
        FrmLaunchLeft.RefreshPage(False, True)
    End Sub

#End Region

#Region "皮肤/披风"

    '展开
    Private Sub BtnSkin_Click(sender As Object, e As RoutedEventArgs) Handles BtnSkin.Click
        BtnSkin.ContextMenu.IsOpen = True
    End Sub

    '修改皮肤
    Private IsChanging As Boolean = False
    Public Sub BtnSkinEdit_Click(sender As Object, e As RoutedEventArgs)
        '检查条件，获取新皮肤
        If IsChanging Then
            Hint("正在更改皮肤中，请稍候！")
            Return
        End If
        If McLoginLoader.State = LoadState.Failed Then
            Hint("登录失败，无法更改皮肤！", HintType.Critical)
            Return
        End If
        Dim SkinInfo As McSkinInfo = McSkinSelect()
        If Not SkinInfo.IsVaild Then Return
        Hint("正在更改皮肤……")
        IsChanging = True
        '开始实际获取
        RunInNewThread(
        Async Sub()
            Try
Retry:
                If McLoginMsLoader.State = LoadState.Loading Then McLoginMsLoader.WaitForExit() '等待登录结束
                Dim AccessToken As String = Setup.Get("CacheMsV2Access")
                Dim Uuid As String = Setup.Get("CacheMsV2Uuid")

                Dim Client As New Net.Http.HttpClient With {.Timeout = New TimeSpan(0, 0, 30)}
                Client.DefaultRequestHeaders.Authorization = New Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken)
                Client.DefaultRequestHeaders.Accept.Add(New Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"))
                Client.DefaultRequestHeaders.UserAgent.Add(New Net.Http.Headers.ProductInfoHeaderValue("MojangSharp", "0.1"))
                Dim Contents As New Net.Http.MultipartFormDataContent From {
                    {New Net.Http.StringContent(If(SkinInfo.IsSlim, "slim", "classic")), "variant"},
                    {New Net.Http.ByteArrayContent(ReadFileBytes(SkinInfo.LocalFile)), "file", GetFileNameFromPath(SkinInfo.LocalFile)}
                }
                Dim Result As String = Await (Await Client.PostAsync(New Uri("https://api.minecraftservices.com/minecraft/profile/skins"), Contents)).Content.ReadAsStringAsync
                If Result.Contains("request requires user authentication") Then
                    Hint("正在登录，将在登录完成后继续更改皮肤……")
                    McLoginMsLoader.Start(GetLoginData(), IsForceRestart:=True)
                    GoTo Retry
                ElseIf Result.Contains("""error""") Then
                    Hint("更改皮肤失败：" & GetJson(Result)("error"), HintType.Critical)
                    Return
                End If
                '获取新皮肤地址
                Log("[Skin] 皮肤修改返回值：" & vbCrLf & Result)
                Dim ResultJson As JObject = GetJson(Result)
                If ResultJson.ContainsKey("errorMessage") Then Throw New Exception(ResultJson("errorMessage").ToString) '#5309
                For Each Skin As JObject In ResultJson("skins")
                    If Skin("state").ToString = "ACTIVE" Then
                        MySkin.ReloadCache(Skin("url"))
                        Return
                    End If
                Next
                Throw New Exception("未知错误（" & Result & "）")
            Catch ex As Exception
                If ex.GetType.Equals(GetType(Tasks.TaskCanceledException)) Then
                    Hint("更改皮肤失败：与 Mojang 皮肤服务器的连接超时，请检查你的网络是否通畅！", HintType.Critical)
                Else
                    Log(ex, "更改皮肤失败", LogLevel.Hint)
                End If
            Finally
                IsChanging = False
            End Try
        End Sub, "Ms Skin Upload")
    End Sub

    '保存皮肤
    Public Sub BtnSkinSave_Click(sender As Object, e As RoutedEventArgs)
        Skin.BtnSkinSave_Click()
    End Sub

    '刷新头像
    Public Sub BtnSkinRefresh_Click(sender As Object, e As RoutedEventArgs)
        Skin.RefreshClick()
    End Sub

    '修改披风
    Public Sub BtnSkinCape_Click(sender As Object, e As RoutedEventArgs)
        Skin.BtnSkinCape_Click()
    End Sub

#End Region

End Class
