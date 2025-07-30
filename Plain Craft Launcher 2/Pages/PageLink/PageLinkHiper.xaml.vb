Public Class PageLinkHiper
    Public Const RequestVersion As Char = "2"

    '记录的启动情况
    Public Shared IsServerSide As Boolean
    Private Shared HostIp As String
    Private Shared HostPort As Integer

#Region "初始化"

    '加载器初始化
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanContent, PanAlways, InitLoader, AutoRun:=False)
        '注册自定义的 OnStateChanged
        AddHandler InitLoader.OnStateChangedUi, AddressOf OnLoadStateChanged
    End Sub

    Private IsLoad As Boolean = False
    Private Sub OnLoaded() Handles Me.Loaded
        FormMain.EndProgramForce(ProcessReturnValues.Aborted)
        If IsLoad Then Return
        IsLoad = True
        '启动监视线程
        If Not IsWatcherStarted Then RunInNewThread(AddressOf WatcherThread, "Hiper Watcher")
        '读取索引码
        Try
            Dim Time As String = Setup.Get("LinkHiperCertTime")
            If Time = "" Then
                Log("[HiPer] 没有缓存凭证")
            ElseIf Date.Parse(Time) > Date.Now Then
                TextCert.Text = Setup.Get("LinkHiperCertLast")
                Log("[HiPer] 缓存凭证尚未过期：" & Time)
                CurrentSubpage = Subpages.PanSelect
            Else
                Log("[HiPer] 缓存凭证已过期：" & Time)
                LabCertTitle.Text = "输入索引码"
                LabCertDesc.Text = "你的 HiPer 索引码已经过期，请输入新的索引码。" & vbCrLf & "如果实在没有索引码，可以在左侧选择 IOI 方式联机。"
            End If
        Catch ex As Exception
            Log(ex, "读取缓存凭证失败")
            Setup.Set("LinkHiperCertTime", "")
        End Try
    End Sub

#End Region

#Region "加载步骤"

    Public Shared PathHiper As String = PathAppdata & "联机模块\"
    Public Shared WithEvents InitLoader As New LoaderCombo(Of Integer)("HiPer 初始化", {
        New LoaderTask(Of Integer, Integer)("网络环境：连通检测", AddressOf InitPingCheck) With {.Block = False, .ProgressWeight = 0.5},
        New LoaderTask(Of Integer, Integer)("网络环境：IP 检测", AddressOf InitIpCheck) With {.Block = False, .ProgressWeight = 1},
        New LoaderTask(Of Integer, Integer)("检查网络环境", AddressOf InitCheck) With {.ProgressWeight = 0.5},
        New LoaderTask(Of Integer, List(Of NetFile))("获取所需文件", AddressOf InitGetFile) With {.ProgressWeight = 4},
        New LoaderDownload("下载所需文件", New List(Of NetFile)) With {.ProgressWeight = 4},
        New LoaderTask(Of Integer, Integer)("启动联机模块", AddressOf InitLaunch) With {.ProgressWeight = 7}
    })

    '检查网络状态
    Private Shared Sub InitPingCheck(Task As LoaderTask(Of Integer, Integer))
    End Sub
    Private Shared Sub InitIpCheck()
    End Sub
    Private Shared PingTime As Integer, IpCheckStatus As LoadState = LoadState.Loading, IpIsInChina As Boolean
    Private Shared Sub InitCheck(Task As LoaderTask(Of Integer, Integer))
    End Sub

    '获取所需文件
    Private Shared Sub InitGetFile(Task As LoaderTask(Of Integer, List(Of NetFile)))
    End Sub
    Public Class CertOutdatedException
        Inherits Exception
    End Class
    '启动联机模块
    Private Shared Sub InitLaunch(Task As LoaderTask(Of Integer, Integer))
    End Sub
    Private Shared PingNodes As Integer, AllNodes As List(Of String)

#End Region

#Region "进程管理"

    Private Shared _HiperState As LoadState = LoadState.Waiting
    Public Shared Property HiperState As LoadState
        Get
            Return _HiperState
        End Get
        Set(value As LoadState)
            _HiperState = value
            RunInUi(Sub() If FrmLinkLeft IsNot Nothing Then CType(FrmLinkLeft.ItemHiper.Buttons(0), MyIconButton).Visibility = If(HiperState = LoadState.Finished OrElse HiperState = LoadState.Loading, Visibility.Visible, Visibility.Collapsed))
        End Set
    End Property
    Public Shared Sub ModuleStopManually() '关闭联机模块按钮
        HiperExit(False)
    End Sub

    Private Shared HiperIp As String = Nothing
    Private Shared HiperProcessId As Integer = -1, McbProcessId As Integer = -1
    Private Shared HiperCertTime As Date = Date.Now

    ''' <summary>
    ''' 若程序正在运行，则结束程序进程，同时初始化状态数据。返回是否关闭了相关进程。
    ''' </summary>
    Public Shared Function HiperStop(SleepWhenKilled As Boolean) As Boolean
        Return False
    End Function
    ''' <summary>
    ''' 启动程序，并等待初始化完成后退出运行，同时更新 HiperIp。
    ''' 若启动失败，则会直接抛出异常。
    ''' 若程序正在运行，则会先停止其运行。
    ''' </summary>
    Public Shared Sub HiperStart(Task As LoaderTask(Of Integer, Integer))
    End Sub

    'Hiper 日志
    Private Shared Sub HiperLogLine(Content As String, Task As LoaderTask(Of Integer, Integer))
    End Sub
    Private Shared PossibleFailReason As String = Nothing

#End Region

#Region "监视线程"

    '主 Timer 线程
    Private IsWatcherStarted As Boolean = False
    Private Sub WatcherThread()
        Dim Sec15 As Integer = 0
        Do While True
            Try
                For i = 1 To 5
                    Thread.Sleep(200)
                    If InitLoader.State = LoadState.Loading Then
                        RunInUi(AddressOf UpdateProgress)
                    End If
                Next
                Thread.Sleep(1000)
                Sec15 += 1
                WatcherTimer1()
                If Sec15 = 15 Then
                    Sec15 = 0
                    WatcherTimer15()
                End If
            Catch ex As Exception
                Log(ex, "联机模块主时钟出错", LogLevel.Feedback)
                Thread.Sleep(20000)
            End Try
        Loop
    End Sub

    '每 1 秒执行的 Timer
    Private Sub WatcherTimer1()
        If HiperState <> LoadState.Finished Then Return
        RunInUi(Sub()
                    '索引码剩余时间
                    Dim Span As TimeSpan = HiperCertTime - Date.Now
                    If Span.TotalDays >= 30 Then
                        LabFinishTime.Text = "> 30 天"
                    ElseIf Span.TotalDays >= 4 Then
                        LabFinishTime.Text = Span.Days & " 天"
                    ElseIf Span.TotalDays >= 1 Then
                        LabFinishTime.Text = Span.Days & " 天" & If(Span.Hours > 0, " " & Span.Hours & " 小时", "")
                    ElseIf Span.TotalMinutes >= 10 Then
                        LabFinishTime.Text = Span.Hours & ":" & Span.Minutes.ToString.PadLeft(2, "0") & "'"
                    Else
                        LabFinishTime.Text = Span.Minutes & "'" & Span.Seconds.ToString.PadLeft(2, "0") & """"
                    End If
                    '提示索引码即将到期
                    If Span.TotalSeconds <= 5 * 60 AndAlso Span.TotalSeconds > 5 * 60 - 1 AndAlso Setup.Get("LinkHiperCertWarn") Then
                        MyMsgBox("你的索引码还有不到 5 分钟就要过期了！" & vbCrLf & "你可以在设置中关闭这个提示……", "索引码即将过期", "我知道了……")
                        ShowWindowToTop(Handle)
                        Beep()
                    End If
                    '检查索引码到期
                    If Span.TotalSeconds < 2 Then
                        LabCertTitle.Text = "索引码已过期"
                        LabCertDesc.Text = "你的 HiPer 索引码已经过期，请输入新的索引码。" & vbCrLf & "如果实在没有索引码，可以在左侧选择 IOI 方式联机。"
                        TextCert.Text = ""
                        HiperExit(True)
                        ShowWindowToTop(Handle)
                        Beep()
                        Return
                    End If
                    '网络质量
                    Dim QualityScore As Integer = If(IpIsInChina, 0, -2)
                    QualityScore -= Math.Ceiling((Math.Min(PingTime, 600) + Math.Min(PingNodes, 600)) / 80)
                    Select Case QualityScore
                        Case Is >= -1
                            LabFinishQuality.Text = "优秀"
                        Case Is >= -2
                            LabFinishQuality.Text = "优良"
                        Case Is >= -3
                            LabFinishQuality.Text = "良好"
                        Case Is >= -5
                            LabFinishQuality.Text = "一般"
                        Case Is >= -7
                            LabFinishQuality.Text = "较差"
                        Case Else
                            LabFinishQuality.Text = "很差"
                    End Select
                    'Ping
                    If HostPing <> -1 Then
                        If FrmLinkHiper IsNot Nothing AndAlso FrmLinkHiper.LabFinishPing.IsLoaded Then
                            FrmLinkHiper.LabFinishPing.Text = HostPing & "ms"
                        End If
                    End If
                End Sub)
    End Sub
    '每 15 秒执行的 Timer
    Private Shared HostPing As Integer = -1
    Private Sub WatcherTimer15()
    End Sub

#End Region

#Region "PanCert | 索引码输入页面"

    '检测输入
    Private Sub TextCert_ValidateChanged(sender As Object, e As EventArgs) Handles TextCert.ValidateChanged
        BtnCertDone.IsEnabled = TextCert.ValidateResult = ""
    End Sub
    Private Sub TextCert_KeyDown(sender As Object, e As KeyEventArgs) Handles TextCert.KeyDown
        If e.Key = Key.Enter AndAlso BtnCertDone.IsEnabled Then BtnCertDone_Click() '允许回车确认
    End Sub

    '确认
    Private Sub BtnCertDone_Click() Handles BtnCertDone.Click
        CurrentSubpage = Subpages.PanSelect
    End Sub

#End Region

#Region "PanSelect | 种类选择页面"

    '返回
    Private Sub BtnSelectReturn_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnSelectReturn.MouseLeftButtonUp
        LabCertTitle.Text = "输入索引码"
        LabCertDesc.Text = "你需要获取索引码才能使用 HiPer。" & vbCrLf & "如果实在没有索引码，可以在左侧选择 IOI 方式联机。"
        CurrentSubpage = Subpages.PanCert
    End Sub

    '创建房间
    Private Sub BtnSelectCreate_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnSelectCreate.MouseLeftButtonUp
    End Sub
    Private Sub RoomCreate(Port As Integer)
        '记录信息
        HostIp = Nothing : HostPort = Port
        IsServerSide = True
        '启动
        InitLoader.Start(IsForceRestart:=True)
    End Sub

    '加入房间
    Private Sub BtnSelectJoin_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnSelectJoin.MouseLeftButtonUp
    End Sub
    Private Sub RoomJoin(Ip As String, Port As Integer)
        '记录信息
        HostIp = Ip : HostPort = Port
        IsServerSide = False
        '启动
        InitLoader.Start(IsForceRestart:=True)
    End Sub

#End Region

#Region "PanLoad | 加载中页面"

    '承接状态切换的 UI 改变
    Private Sub OnLoadStateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
    End Sub
    Private Shared LoadStep As String = "准备初始化"
    Private Shared Sub SetLoadDesc(Intro As String, [Step] As String)
        Log("[Hiper] 连接步骤：" & Intro)
        LoadStep = [Step]
        RunInUiWait(Sub()
                        If FrmLinkHiper Is Nothing OrElse Not FrmLinkHiper.LabLoadDesc.IsLoaded Then Return
                        FrmLinkHiper.LabLoadDesc.Text = Intro
                        FrmLinkHiper.UpdateProgress()
                    End Sub)
    End Sub

    '承接重试
    Private Sub CardLoad_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles CardLoad.MouseLeftButtonUp
        If Not InitLoader.State = LoadState.Failed Then Return
        InitLoader.Start(IsForceRestart:=True)
    End Sub

    '取消加载
    Private Sub CancelLoad() Handles BtnLoadCancel.Click
        If InitLoader.State = LoadState.Loading Then
            CurrentSubpage = Subpages.PanSelect
            InitLoader.Abort()
        Else
            InitLoader.State = LoadState.Waiting
        End If
        HiperStop(False)
    End Sub

    '进度改变
    Private Sub UpdateProgress(Optional Value As Double = -1)
        If Value = -1 Then Value = InitLoader.Progress
        Dim DisplayingProgress As Double = ColumnProgressA.Width.Value
        If Math.Round(Value - DisplayingProgress, 3) = 0 Then Return
        If DisplayingProgress > Value Then
            ColumnProgressA.Width = New GridLength(Value, GridUnitType.Star)
            ColumnProgressB.Width = New GridLength(1 - Value, GridUnitType.Star)
            AniStop("Hiper Progress")
        Else
            Dim NewProgress As Double = If(Value = 1, 1, (Value - DisplayingProgress) * 0.2 + DisplayingProgress)
            AniStart({
                AaGridLengthWidth(ColumnProgressA, NewProgress - ColumnProgressA.Width.Value, 300, Ease:=New AniEaseOutFluent),
                AaGridLengthWidth(ColumnProgressB, (1 - NewProgress) - ColumnProgressB.Width.Value, 300, Ease:=New AniEaseOutFluent)
            }, "Hiper Progress")
        End If
    End Sub
    Private Sub CardResized() Handles CardLoad.SizeChanged
        RectProgressClip.Rect = New Rect(0, 0, CardLoad.ActualWidth, 12)
    End Sub

#End Region

#Region "PanFinish | 加载完成页面"

    '复制 IP
    Private Sub BtnFinishIp_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnFinishIp.MouseLeftButtonUp
        ClipboardSet(LabFinishIp.Text)
    End Sub

    '退出
    Private Sub BtnFinishExit_Click(sender As Object, e As EventArgs) Handles BtnFinishExit.Click
        If IsServerSide AndAlso MyMsgBox("你确定要关闭联机房间吗？", "确认退出", "确定", "取消", IsWarn:=True) = 2 Then Return
        HiperExit(False)
    End Sub

    '复制联机码
    Private Sub BtnFinishCopy_Click(sender As Object, e As EventArgs) Handles BtnFinishCopy.Click
    End Sub

    'Ping 房主
    Private Sub BtnFinishPing_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnFinishPing.MouseLeftButtonUp
        LabFinishPing.Text = "检测中"
        If TaskPingHost.State = LoadState.Loading Then Return
        TaskPingHost.Start(True, IsForceRestart:=True)
    End Sub
    Private Shared TaskPingHost As New LoaderTask(Of Boolean, Integer)("HiPer Ping Host",
    Sub(Task As LoaderTask(Of Boolean, Integer))
        HostPing = -1
        HostPing = Ping(HostIp, 5000, Task.Input)
    End Sub)

#End Region

#Region "子页面管理"

    Public Enum Subpages
        PanCert
        PanSelect
        PanFinish
    End Enum
    Private _CurrentSubpage As Subpages = Subpages.PanCert
    Public Property CurrentSubpage As Subpages
        Get
            Return _CurrentSubpage
        End Get
        Set(value As Subpages)
            If _CurrentSubpage = value Then Return
            _CurrentSubpage = value
            Log("[Hiper] 子页面更改为 " & GetStringFromEnum(value))
            PageOnContentExit()
            If value = Subpages.PanSelect Then
                LabSelectCode.Text = "(" & TextCert.Text.Substring(0, Math.Min(TextCert.Text.Length, 3)) & "…)"
            End If
        End Set
    End Property

    Private Sub PageLinkHiper_OnPageEnter() Handles Me.PageEnter
        FrmLinkHiper.PanCert.Visibility = If(CurrentSubpage = Subpages.PanCert, Visibility.Visible, Visibility.Collapsed)
        FrmLinkHiper.PanSelect.Visibility = If(CurrentSubpage = Subpages.PanSelect, Visibility.Visible, Visibility.Collapsed)
        FrmLinkHiper.PanFinish.Visibility = If(CurrentSubpage = Subpages.PanFinish, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Shared Sub HiperExit(ExitToCertPage As Boolean)
        Log("[Hiper] 要求退出 Hiper（当前加载器状态为 " & GetStringFromEnum(InitLoader.State) & "）")
        HiperStop(False)
        If InitLoader.State = LoadState.Loading Then InitLoader.Abort()
        If InitLoader.State = LoadState.Failed Then InitLoader.State = LoadState.Waiting
        RunInUi(Sub()
                    If FrmLinkHiper Is Nothing OrElse Not FrmLinkHiper.IsLoaded Then Return
                    FrmLinkHiper.CurrentSubpage = If(ExitToCertPage, Subpages.PanCert, Subpages.PanSelect)
                    FrmLinkHiper.PageOnContentExit()
                End Sub)
    End Sub

#End Region

End Class
