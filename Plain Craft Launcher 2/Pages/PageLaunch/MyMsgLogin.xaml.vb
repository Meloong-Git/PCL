Public Class MyMsgLogin
    Private Data As JObject
    Private UserCode As String '需要用户在网页上输入的设备代码
    Private DeviceCode As String '用于轮询的设备代码
    Private Website As String '验证网页的网址

#Region "弹窗"

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()

    Public Sub New(Converter As MyMsgBoxConverter)
        Try
            InitializeComponent()
            Btn1.Name = Btn1.Name & GetUuid()
            Btn2.Name = Btn2.Name & GetUuid()
            Btn3.Name = Btn3.Name & GetUuid()
            MyConverter = Converter
            ShapeLine.StrokeThickness = GetWPFSize(1)
            Data = Converter.Content
            Init()
        Catch ex As Exception
            Log(ex, "登录弹窗初始化失败", LogLevel.Hint)
        End Try
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try
            '动画
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, If(MyConverter.IsWarn, New MyColor(140, 80, 0, 0), New MyColor(90, 0, 0, 0)) - FrmMain.PanMsg.Background, 200), "PanMsg Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgBox " & Uuid)
            '记录日志
            Log("[Control] 登录弹窗：" & LabTitle.Text & vbCrLf & LabCaption.Text)
        Catch ex As Exception
            Log(ex, "登录弹窗加载失败", LogLevel.Hint)
        End Try
    End Sub
    Private Sub Close()
        '动画
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 6 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgBox " & Uuid)
    End Sub

    '实现回车和 Esc 的接口（#4857）
    Public Sub Btn1_Click() Handles Btn1.Click
    End Sub
    Public Sub Btn3_Click() Handles Btn3.Click
        Finished(New ThreadInterruptedException)
    End Sub

    Private Sub Drag(sender As Object, e As MouseButtonEventArgs) Handles PanBorder.MouseLeftButtonDown, LabTitle.MouseLeftButtonDown
        On Error Resume Next
        If e.GetPosition(ShapeLine).Y <= 2 Then FrmMain.DragMove()
    End Sub

#End Region

    Private Sub Finished(Result As Object)
        If MyConverter.IsExited Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Result
        RunInUi(AddressOf Close)
        Thread.Sleep(200)
        FrmMain.ShowWindowToTop()
    End Sub

    Private Sub Init()
        UserCode = Data("user_code")
        DeviceCode = Data("device_code")
        Website = Data("verification_uri")
        '设置 UI
        LabTitle.Text = "登录 Minecraft"
        LabCaption.Text =
            $"登录网页将自动开启，请在网页中输入 {UserCode}（已自动复制）。" & vbCrLf & vbCrLf &
            $"如果网络环境不佳，网页可能一直加载不出来，届时请使用加速器或 VPN 改善网络环境。" & vbCrLf &
            $"你也可以用其他设备打开 {Website} 并输入上述代码。"
        CustomEventService.SetEventData(Btn1, Website)
        CustomEventService.SetEventData(Btn2, UserCode)
        '启动工作线程
        RunInNewThread(AddressOf WorkThread, "MyMsgLogin")
    End Sub

    Private Sub WorkThread()
        Thread.Sleep(2000)
        If MyConverter.IsExited Then Return
        OpenWebsite(Website)
        ClipboardSet(UserCode)
        Thread.Sleep((Data("interval").ToObject(Of Integer) - 1) * 1000)
        '轮询
        Dim UnknownFailureCount As Integer = 0
        Do While Not MyConverter.IsExited
            Try
                Dim Result = NetRequestByClient(
                    "https://login.microsoftonline.com/consumers/oauth2/v2.0/token", HttpMethod.Post,
                    Content:=
                        "grant_type=urn:ietf:params:oauth:grant-type:device_code" & "&" &
                        "client_id=" & OAuthClientId & "&" &
                        "device_code=" & DeviceCode & "&" &
                        "scope=XboxLive.signin%20offline_access",
                    ContentType:="application/x-www-form-urlencoded",
                    Timeout:=5000 + UnknownFailureCount * 5000,
                    MakeLog:=False, RequireJson:=True)
                '获取结果
                Dim ResultJson As JObject = GetJson(Result)
                McLaunchLog($"令牌过期时间：{ResultJson("expires_in")} 秒")
                Hint("网页登录成功！", HintType.Green)
                Finished({ResultJson("access_token").ToString, ResultJson("refresh_token").ToString})
                Return
            Catch ex As Exception
                '修改错误列表时，同时修改 ModLaunch.MsLoginStep1Refresh 中的对应代码
                If TypeOf ex Is ResponsedWebException Then
                    Dim Response = CType(ex, ResponsedWebException).Response
                    If Response.Contains("authorization_declined") Then
                        Finished(New Exception("$你拒绝了 PCL 申请的权限……"))
                        Return
                    ElseIf Response.Contains("expired_token") Then
                        Finished(New Exception("$登录用时太长啦，重新试试吧！"))
                        Return
                    ElseIf Response.Contains("Account security interrupt") Then
                        Finished(New Exception("$该账号由于安全问题无法登陆，请前往微软账户页获取更多信息。"))
                        Return
                    ElseIf Response.Contains("service abuse") Then
                        Finished(New Exception("$非常抱歉，该账号已被微软封禁，无法登录。"))
                        Return
                    ElseIf Response.Contains("AADSTS70000") Then '可能不能判 “invalid_grant”，见 #269
                        Finished(New RestartException)
                        Return
                    ElseIf Response.Contains("authorization_pending") Then
                        Thread.Sleep(2000)
                        Continue Do
                    End If
                End If
                If UnknownFailureCount <= 2 Then
                    UnknownFailureCount += 1
                    Log(ex, $"登录轮询第 {UnknownFailureCount} 次失败")
                    Thread.Sleep(2000)
                Else
                    Finished(New Exception("登录轮询失败", ex))
                    Return
                End If
            End Try
        Loop
    End Sub

End Class
