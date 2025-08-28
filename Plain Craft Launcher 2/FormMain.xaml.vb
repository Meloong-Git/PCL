Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports PCL.Core.App
Imports PCL.Core.Logging
Imports PCL.Core.Link.Lobby
Imports PCL.Core.ProgramSetup
Imports PCL.Core.Utils

Public Class FormMain

#Region "基础"

    '更新日志
    Private Sub ShowUpdateLog()
        RunInNewThread(
            Sub()
                Dim ChangelogFile = $"{PathTemp}CEUpdateLog.md"
                Dim Changelog As String
                If File.Exists(ChangelogFile) Then
                    Changelog = ReadFile(ChangelogFile)
                Else
                    Changelog = "欢迎使用呀~"
                End If
                If MyMsgBoxMarkdown(Changelog, "PCL CE 已更新至 " & VersionBranchName & " " & VersionBaseName, "确定", "完整更新日志") = 2 Then
                    OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases")
                End If
            End Sub, "UpdateLog Output")
    End Sub

    '窗口加载
    Private IsWindowLoadFinished As Boolean = False
    Public Sub New()
        ApplicationStartTick = TimeUtils.GetTimeTick()
        '刷新主题
        ThemeCheckAll(False)
        Dim dark As Int32 = Setup.Get("UiDarkMode")
        Select Case dark
            Case 0
                IsDarkMode = False
            Case 1
                IsDarkMode = True
            Case 2
                IsDarkMode = IsSystemInDarkMode()
        End Select
        ThemeRefreshColor()
        '窗体参数初始化
        FrmMain = Me
        FrmLaunchLeft = New PageLaunchLeft
        FrmLaunchRight = New PageLaunchRight
        '版本号改变
        Dim LastVersion As Integer = Setup.Get("SystemLastVersionReg")
        If LastVersion < VersionCode Then
            '触发升级
            UpgradeSub(LastVersion)
        ElseIf LastVersion > VersionCode Then
            '触发降级
            DowngradeSub(LastVersion)
        End If
        '版本隔离设置迁移
        If Setup.IsUnset("LaunchArgumentIndieV2") Then
            If Not Setup.IsUnset("LaunchArgumentIndie") Then
                Log("[Start] 从老 PCL 迁移版本隔离")
                Setup.Set("LaunchArgumentIndieV2", Setup.Get("LaunchArgumentIndie"))
            ElseIf Not Setup.IsUnset("WindowHeight") Then
                Log("[Start] 从老 PCL 升级，但此前未调整版本隔离，使用老的版本隔离默认值")
                Setup.Set("LaunchArgumentIndieV2", Setup.GetDefault("LaunchArgumentIndie"))
            Else
                Log("[Start] 全新的 PCL，使用新的版本隔离默认值")
                Setup.Set("LaunchArgumentIndieV2", Setup.GetDefault("LaunchArgumentIndieV2"))
            End If
        End If
        Setup.Load("UiLauncherTheme")
        '注册拖拽事件（不能直接加 Handles，否则没用；#6340）
        [AddHandler](DragDrop.DragEnterEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        [AddHandler](DragDrop.DragOverEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        '加载 UI
        InitializeComponent()
        Opacity = 0
        Try
            Height = Setup.Get("WindowHeight")
            Width = Setup.Get("WindowWidth")
        Catch ex As Exception '修复 #2019
            Log(ex, "读取窗口默认大小失败", LogLevel.Hint)
            Height = MinHeight + 100
            Width = MinWidth + 100
        End Try
        ''开启管理员权限下的文件拖拽，但下列代码也没用（#2531）
        'If IsAdmin() Then
        '    Log("[Start] PCL 正以管理员权限运行")
        '    ChangeWindowMessageFilter(&H233, 1)
        '    ChangeWindowMessageFilter(&H4A, 1)
        '    ChangeWindowMessageFilter(&H49, 1)
        'End If
        '切换到首页
        If Not IsNothing(FrmLaunchLeft.Parent) Then FrmLaunchLeft.SetValue(ContentPresenter.ContentProperty, Nothing)
        If Not IsNothing(FrmLaunchRight.Parent) Then FrmLaunchRight.SetValue(ContentPresenter.ContentProperty, Nothing)
        PanMainLeft.Child = FrmLaunchLeft
        PageLeft = FrmLaunchLeft
        PanMainRight.Child = FrmLaunchRight
        PageRight = FrmLaunchRight
        FrmLaunchRight.PageState = MyPageRight.PageStates.ContentStay
        '调试模式提醒
        If ModeDebug Then Hint("[调试模式] PCL 正以调试模式运行，这可能会导致性能下降，若无必要请不要开启！")
        '尽早执行的加载池
        McFolderListLoader.Start(0) '为了让下载已存在文件检测可以正常运行，必须跑一次；为了让启动按钮尽快可用，需要尽早执行；为了与 PageLaunchLeft 联动，需要为 0 而不是 GetUuid

        Log("[Start] 第二阶段加载用时：" & TimeUtils.GetTimeTick() - ApplicationStartTick & " ms")
        '注册生命周期状态事件
        Lifecycle.When(LifecycleState.WindowCreated, AddressOf FormMain_Loaded)
    End Sub

    Private Sub FormMain_Loaded() '(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ApplicationStartTick = TimeUtils.GetTimeTick()
        Handle = New WindowInteropHelper(Me).Handle
        '读取设置
        Setup.Load("UiBackgroundOpacity")
        Setup.Load("UiBackgroundBlur")
        Setup.Load("UiLogoType")
        Setup.Load("UiHiddenPageDownload")
        SetupService.GetBool(SetupEntries.Ui.AutoPauseVideo) '智能暂停视频背景
        PageSetupUI.BackgroundRefresh(False, True)
        MusicRefreshPlay(False, True)
        '扩展按钮
        BtnExtraDownload.ShowCheck = AddressOf BtnExtraDownload_ShowCheck
        BtnExtraBack.ShowCheck = AddressOf BtnExtraBack_ShowCheck
        BtnExtraApril.ShowCheck = AddressOf BtnExtraApril_ShowCheck
        BtnExtraShutdown.ShowCheck = AddressOf BtnExtraShutdown_ShowCheck
        BtnExtraLog.ShowCheck = AddressOf BtnExtraLog_ShowCheck
        BtnExtraApril.ShowRefresh()
        '初始化尺寸改变
        Resizer = New MyResizer(Me)
        If Not Setup.Get("UiLockWindowSize") Then
            AddResizer()
        End If
        'PLC 彩蛋
        If RandomInteger(1, 1000) = 233 Then
            ShapeTitleLogo.Data = New GeometryConverter().ConvertFromString("M26,29 v-25 h6 a7,7 180 0 1 0,14 h-6 M83,6.5 a10,11.5 180 1 0 0,18 M48,2.5 v24.5 h13.5")
        End If
        '加载窗口

        ThemeRefresh()

        If Setup.Get("UiBlur") Then
            Application.Current.Resources("BlurValue") = CType(Setup.Get("UiBlurValue"), Double)
        Else
            Application.Current.Resources("BlurValue") = CType(0, Double)
        End If

        '#If DEBUG Then
        '        MinHeight = 50
        '        MinWidth = 50
        '#End If
        Topmost = False
        If FrmStart IsNot Nothing Then FrmStart.Close(New TimeSpan(0, 0, 0, 0, 400 / AniSpeed))
        '更改窗口
        'Top = (GetWPFSize(My.Computer.Screen.WorkingArea.Height) - Height) / 2
        'Left = (GetWPFSize(My.Computer.Screen.WorkingArea.Width) - Width) / 2
        IsSizeSaveable = True
        ShowWindowToTop()
        Dim HwndSource As Interop.HwndSource = PresentationSource.FromVisual(Me)
        HwndSource.AddHook(New Interop.HwndSourceHook(AddressOf WndProc))
        AniStart({
            AaCode(Sub() AniControlEnabled -= 1, 50),
            AaOpacity(Me, Setup.Get("UiLauncherTransparent") / 1000 + 0.4, 250, 100),
            AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 600, 100, New AniEaseOutBack(AniEasePower.Weak)),
            AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 500, 100, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(
            Sub()
                PanBack.RenderTransform = Nothing
                IsWindowLoadFinished = True
                Log($"[System] DPI：{DPI}，系统版本：{Environment.OSVersion.VersionString}，PCL 位置：{PathWithName}")
            End Sub, , True)
        }, "Form Show")
        'Timer 启动
        AniStart()
        TimerMainStart()
        '加载池
        RunInNewThread(
        Sub()
            '特殊版本提示
#If DEBUG Or DEBUGCI Then
            If Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT") Is Nothing Then
#If DEBUG Then
                Const hint = "当前运行的 PCL 社区版为 Debug 版本。" & vbCrLf &
                             "该版本仅适合开发者调试运行，可能会有严重的性能下降以及各种奇怪的网络问题。" & vbCrLf &
                             vbCrLf &
                             "非开发者用户使用该版本造成的一切问题均不被社区支持，相关 issue 可能会被直接关闭。" & vbCrLf &
                             "除非您是开发者，否则请立即删除该版本，并下载最新稳定版使用。"
#Else
                Const hint = "当前运行的 PCL 社区版为 CI 自动构建版本。" & vbCrLf &
                             "该版本包含最新的漏洞修复、优化和新特性，但性能和稳定性较差，不适合日常使用和制作整合包。" & vbCrLf &
                             vbCrLf &
                             "除非社区开发者要求或您自己想要这么做，否则请下载最新稳定版使用。"
#End If
                MyMsgBox($"{hint}{vbCrLf}{vbCrLf}可以添加 PCL_DISABLE_DEBUG_HINT 环境变量 (任意值) 来隐藏这个提示。",
                         "特殊版本提示", "我清楚我在做什么", "打开最新版下载页并退出", IsWarn:=True,
                         Button2Action:=Sub()
                                            OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/latest")
                                            EndProgram(False)
                                        End Sub)
            End If
#End If
            'EULA 提示
            If Not Setup.Get("SystemEula") Then
                Select Case MyMsgBox("在使用 PCL 前，请同意 PCL 的用户协议与免责声明。", "协议授权", "同意", "拒绝", "查看用户协议与免责声明",
                        Button3Action:=Sub() OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW"))
                    Case 1
                        Setup.Set("SystemEula", True)
                    Case 2
                        EndProgram(False)
                End Select
            End If
            '遥测提示
            If Setup.IsUnset("SystemTelemetry") Then
                Select Case MyMsgBox("这是一项与 Steam 硬件调查类似的计划，参与调查可以帮助我们更好的进行规划和开发，且我们会不定期发布该调查的统计结果。" & vbCrLf &
                                     "如果选择参与调查，我们将会收集以下信息：" & vbCrLf &
                                     "启动器版本信息与识别码，使用的 Windows 系统版本与架构，已安装的物理内存大小，NAT 与 IPv6 支持情况，是否使用过官方版 PCL、HMCL 或 BakaXL" & vbCrLf & vbCrLf &
                                     "这些数据均不与你关联，我们也绝不会向第三方出售数据。" & vbCrLf &
                                     "如果不想参与该调查，可以选择拒绝，不会影响其他功能使用。" & vbCrLf &
                                     "你可以随时在启动器设置中调整这项设置。", "参与 PCL CE 软硬件调查", "同意", "拒绝")
                    Case 1
                        Setup.Set("SystemTelemetry", True)
                    Case 2
                        Setup.Set("SystemTelemetry", False)
                End Select
            ElseIf Setup.Get("SystemTelemetry") Then
                RunInNewThread(Sub() SendTelemetry())
            End If
            '启动加载器池
            Try
                InitJava()
                Thread.Sleep(100)
                DlClientListMojangLoader.Start(1) 'PCL 会同时根据这里的加载结果决定是否使用官方源进行下载
                RunCountSub()
                ServerLoader.Start(1)
                RunInNewThread(AddressOf TryClearTaskTemp, "TryClearTaskTemp", ThreadPriority.BelowNormal)
            Catch ex As Exception
                Log(ex, "初始化加载池运行失败", LogLevel.Feedback)
            End Try
            '清理自动更新文件
            Try
                If File.Exists(Path & "PCL\Plain Craft Launcher Community Edition.exe") Then File.Delete(Path & "PCL\Plain Craft Launcher Community Edition.exe")
            Catch ex As Exception
                Log(ex, "清理自动更新文件失败")
            End Try
            GetCoR() '获取区域限制状态
            GetSystemInfo()
        End Sub, "Start Loader", ThreadPriority.Lowest)

        Log("[Start] 第三阶段加载用时：" & TimeUtils.GetTimeTick() - ApplicationStartTick & " ms")
    End Sub
    '根据打开次数触发的事件
    Private Sub RunCountSub()
        Setup.Set("SystemCount", Setup.Get("SystemCount") + 1)
        If Setup.Get("SystemCount") >= 99 Then
            If ThemeUnlock(6, False) Then
                MyMsgBox("你已经打开了 99 次 PCL 社区版啦，感谢你长期以来的支持！" & vbCrLf &
                         "隐藏主题 铁杆粉 未解锁！社区版不包含隐藏主题！", "提示")
            End If
        End If
    End Sub
    '升级与降级事件
    Private Sub UpgradeSub(LastVersionCode As Integer)
        Log("[Start] 版本号从 " & LastVersionCode & " 升高到 " & VersionCode)
        Setup.Set("SystemLastVersionReg", VersionCode)
        '检查有记录的最高版本号
        Dim LowerVersionCode As Integer
#If BETA Then
        LowerVersionCode = Setup.Get("SystemHighestBetaVersionReg")
        If LowerVersionCode < VersionCode Then
            Setup.Set("SystemHighestBetaVersionReg", VersionCode)
            Log("[Start] 最高版本号从 " & LowerVersionCode & " 升高到 " & VersionCode)
        End If
#Else
        LowerVersionCode = Setup.Get("SystemHighestAlphaVersionReg")
        If LowerVersionCode < VersionCode Then
            Setup.Set("SystemHighestAlphaVersionReg", VersionCode)
            Log("[Start] 最高版本号从 " & LowerVersionCode & " 升高到 " & VersionCode)
        End If
#End If
        '被移除的窗口设置选项
        If Setup.Get("LaunchArgumentWindowType") = 5 Then Setup.Set("LaunchArgumentWindowType", 1)
        '修改主题设置项名称
        If LowerVersionCode <= 207 Then
            Dim UnlockedTheme As New List(Of String) From {"2"}
            UnlockedTheme.AddRange(New List(Of String)(Setup.Get("UiLauncherThemeHide").ToString.Split("|")))
            UnlockedTheme.AddRange(New List(Of String)(Setup.Get("UiLauncherThemeHide2").ToString.Split("|")))
            Setup.Set("UiLauncherThemeHide2", Join(UnlockedTheme.Distinct.ToList, "|"))
        End If
        '重置欧皇彩
        If LastVersionCode <= 115 AndAlso Setup.Get("UiLauncherThemeHide2").ToString.Split("|").Contains("13") Then
            Dim UnlockedTheme As New List(Of String)(Setup.Get("UiLauncherThemeHide2").ToString.Split("|"))
            UnlockedTheme.Remove("13")
            Setup.Set("UiLauncherThemeHide2", Join(UnlockedTheme, "|"))
            MyMsgBox("由于新版 PCL 修改了欧皇彩的解锁方式，你需要重新解锁欧皇彩。" & vbCrLf &
                     "多谢各位的理解啦！", "重新解锁提醒")
        End If
        '重置滑稽彩
        If LastVersionCode <= 152 AndAlso Setup.Get("UiLauncherThemeHide2").ToString.Split("|").Contains("12") Then
            Dim UnlockedTheme As New List(Of String)(Setup.Get("UiLauncherThemeHide2").ToString.Split("|"))
            UnlockedTheme.Remove("12")
            Setup.Set("UiLauncherThemeHide2", Join(UnlockedTheme, "|"))
            MyMsgBox("由于新版 PCL 修改了滑稽彩的解锁方式，你需要重新解锁滑稽彩。" & vbCrLf &
                     "多谢各位的理解啦！", "重新解锁提醒")
        End If
        '移动自定义皮肤
        If LastVersionCode <= 161 AndAlso File.Exists(Path & "PCL\CustomSkin.png") AndAlso Not File.Exists(PathAppdata & "CustomSkin.png") Then
            CopyFile(Path & "PCL\CustomSkin.png", PathAppdata & "CustomSkin.png")
            Log("[Start] 已移动离线自定义皮肤 (162)")
        End If
        If LastVersionCode <= 263 AndAlso File.Exists(PathTemp & "CustomSkin.png") AndAlso Not File.Exists(PathAppdata & "CustomSkin.png") Then
            CopyFile(PathTemp & "CustomSkin.png", PathAppdata & "CustomSkin.png")
            Log("[Start] 已移动离线自定义皮肤 (264)")
        End If
        '解除帮助页面的隐藏
        If LastVersionCode <= 205 Then
            Setup.Set("UiHiddenOtherHelp", False)
            Log("[Start] 已解除帮助页面的隐藏")
        End If
        '迁移旧版用户档案
        If LastVersionCode <= 368 Then
            RunInNewThread(Sub() MigrateOldProfile())
        End If
        'Mod 命名设置迁移
        If Not Setup.IsUnset("ToolDownloadTranslate") AndAlso Setup.IsUnset("ToolDownloadTranslateV2") Then
            Setup.Set("ToolDownloadTranslateV2", Setup.Get("ToolDownloadTranslate") + 1)
            Log("[Start] 已从老版本迁移 Mod 命名设置")
        End If
        '社区版提示
        If Not Setup.Get("UiLauncherCEHint") Then ShowCEAnnounce(True)
        '输出更新日志
        If LastVersionCode <= 0 Then Return
        If LowerVersionCode >= VersionCode Then Return
        ShowUpdateLog()
    End Sub
    Private Sub DowngradeSub(LastVersionCode As Integer)
        Log("[Start] 版本号从 " & LastVersionCode & " 降低到 " & VersionCode)
        Setup.Set("SystemLastVersionReg", VersionCode)
    End Sub

#End Region

#Region "自定义窗口"

    '硬件加速
    Protected Overrides Sub OnSourceInitialized(e As EventArgs)
        If Setup.Get("SystemDisableHardwareAcceleration") Then
            Dim hwndSource As HwndSource = TryCast(PresentationSource.FromVisual(Me), HwndSource)
            If hwndSource IsNot Nothing Then
                hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly
            End If
        End If
        MyBase.OnSourceInitialized(e)
    End Sub

    '关闭
    Private Sub FormMain_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        EndProgram(True)
        e.Cancel = True
    End Sub
    ''' <summary>
    ''' 正常关闭程序。程序将在执行此方法后约 0.3s 退出。
    ''' </summary>
    ''' <param name="SendWarning">是否在还有下载任务未完成时发出警告。</param>
    Public Sub EndProgram(SendWarning As Boolean)
        '发出警告
        If SendWarning AndAlso HasDownloadingTask() Then
            If MyMsgBox("还有下载任务尚未完成，是否确定退出？", "提示", "确定", "取消") = 1 Then
                '强行结束下载任务
                RunInNewThread(
                Sub()
                    Log("[System] 正在强行停止任务")
                    For Each Task As LoaderBase In LoaderTaskbar.ToList()
                        Task.Abort()
                    Next
                End Sub, "强行停止下载任务")
            Else
                Return
            End If
        End If
        '关闭联机大厅
        LobbyController.Close()
        '存储上次使用的档案编号
        SaveProfile()
        '关闭
        RunInUiWait(
        Sub()
            '清理视频背景
            VideoBack.Stop()
            VideoBack.Source = Nothing
            VideoBack.Close()
            IsHitTestVisible = False
            If PanBack.RenderTransform Is Nothing Then
                Dim TransformPos As New TranslateTransform(0, 0)
                Dim TransformRotate As New RotateTransform(0)
                Dim TransformScale As New ScaleTransform(1, 1)
                PanBack.RenderTransform = New TransformGroup() With {.Children = New TransformCollection({TransformRotate, TransformPos, TransformScale})}
                AniStart({
                    AaOpacity(Me, -Opacity, 140, 40, New AniEaseOutFluent(AniEasePower.Weak)),
                    AaDouble(
                    Sub(i)
                        TransformScale.ScaleX += i
                        TransformScale.ScaleY += i
                    End Sub, 0.88 - TransformScale.ScaleX, 180),
                    AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 180, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                    AaDouble(Sub(i) TransformRotate.Angle += i, 0.6 - TransformRotate.Angle, 180, 0, New AniEaseInoutFluent(AniEasePower.Weak)),
                    AaCode(
                    Sub()
                        IsHitTestVisible = False
                        Top = -10000
                        ShowInTaskbar = False
                    End Sub, 210),
                    AaCode(AddressOf EndProgramForce, 230)
                }, "Form Close")
            Else
                EndProgramForce()
            End If
            Log("[System] 收到关闭指令")
        End Sub)
    End Sub
    Private Shared IsLogShown As Boolean = False
    Public Shared Sub EndProgramForce(Optional ReturnCode As ProcessReturnValues = ProcessReturnValues.Success)
        On Error Resume Next
        '关闭联机大厅
        LobbyController.Close()
        IsProgramEnded = True
        AniControlEnabled += 1
        If IsUpdateWaitingRestart Then UpdateRestart(False)
        If ReturnCode = ProcessReturnValues.Exception Then
            If Not IsLogShown Then
                FeedbackInfo()
                Log("请在 https://github.com/PCL-Community/PCL2-CE/issues 提交错误报告，以便于社区解决此问题！（这也有可能是原版 PCL 的问题）")
                IsLogShown = True
                ShellOnly(LogWrapper.CurrentLogger.LogFiles.Last())
            End If
            Thread.Sleep(500) '防止 PCL 在记事本打开前就被掐掉
        End If
        Log("[System] 程序已退出，返回值：" & GetStringFromEnum(ReturnCode))
        'If ReturnCode <> ProcessReturnValues.Success Then Environment.Exit(ReturnCode)
        'Process.GetCurrentProcess.Kill()
        Lifecycle.ForceShutdown(ReturnCode)
    End Sub
    Private Sub BtnTitleClose_Click(sender As Object, e As RoutedEventArgs) Handles BtnTitleClose.Click
        EndProgram(True)
    End Sub

    '移动
    Private Sub FormDragMove(sender As Object, e As MouseButtonEventArgs) Handles PanTitle.MouseLeftButtonDown, PanMsg.MouseLeftButtonDown
        On Error Resume Next
        If sender.IsMouseDirectlyOver Then DragMove()
    End Sub

    '改变大小
    ''' <summary>
    ''' 是否可以向注册表储存尺寸改变信息。以此避免初始化时误储存。
    ''' </summary>
    Public IsSizeSaveable As Boolean = False
    Private Sub FormMain_SizeChanged() Handles Me.SizeChanged, Me.Loaded
        If IsSizeSaveable Then
            Setup.Set("WindowHeight", Height)
            Setup.Set("WindowWidth", Width)
        End If
        RectForm.Rect = New Rect(0, 0, BorderForm.ActualWidth, BorderForm.ActualHeight)
        PanForm.Width = BorderForm.ActualWidth + 0.001
        PanForm.Height = BorderForm.ActualHeight + 0.001
        PanMain.Width = PanForm.Width
        PanMain.Height = Math.Max(0, PanForm.Height - PanTitle.ActualHeight)
        VideoBack.Width = PanForm.Width
        VideoBack.Height = PanForm.Height
        If WindowState = WindowState.Maximized Then WindowState = WindowState.Normal '修复 #1938
    End Sub

    '标题栏改变大小
    Private Sub PanTitle_SizeChanged() Handles PanTitleLeft.SizeChanged
        If PanTitleMain.ColumnDefinitions(0).ActualWidth - 30 <= 0 Then
            PanTitleLeft.ColumnDefinitions(0).MaxWidth = 0
        Else
            PanTitleLeft.ColumnDefinitions(0).MaxWidth = PanTitleMain.ColumnDefinitions(0).ActualWidth - 30
        End If
    End Sub

    '最小化
    Private Sub BtnTitleMin_Click() Handles BtnTitleMin.Click
        WindowState = WindowState.Minimized
    End Sub
#End Region

#Region "窗体事件"
    Private Resizer
    Public Sub AddResizer()
        Me.ResizeMode = ResizeMode.CanResize
        Resizer.addResizerDown(ResizerB)
        Resizer.addResizerLeft(ResizerL)
        Resizer.addResizerLeftDown(ResizerLB)
        Resizer.addResizerLeftUp(ResizerLT)
        Resizer.addResizerRight(ResizerR)
        Resizer.addResizerRightDown(ResizerRB)
        Resizer.addResizerRightUp(ResizerRT)
        Resizer.addResizerUp(ResizerT)
    End Sub
    Public Sub RemoveResizer()
        Me.ResizeMode = ResizeMode.NoResize
        Resizer.removeAllResizers()
    End Sub

    '按键事件
    Private Sub FormMain_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.IsRepeat Then Return
        '调用弹窗：回车选择第一个，Esc 选择最后一个
        If PanMsg.Children.Count > 0 Then
            If e.Key = Key.Enter Then
                CType(PanMsg.Children(0), Object).Btn1_Click()
                Return
            ElseIf e.Key = Key.Escape Then
                Dim Msg As Object = PanMsg.Children(0)
                If TypeOf Msg IsNot MyMsgInput AndAlso TypeOf Msg IsNot MyMsgSelect AndAlso Msg.Btn3.Visibility = Visibility.Visible Then
                    Msg.Btn3_Click()
                ElseIf Msg.Btn2.Visibility = Visibility.Visible Then
                    Msg.Btn2_Click()
                Else
                    Msg.Btn1_Click()
                End If
                Return
            End If
        End If
        '按 ESC 返回上一级
        If e.Key = Key.Escape Then TriggerPageBack()
        '更改隐藏实例可见性
        If e.Key = Key.F11 AndAlso PageCurrent = FormMain.PageType.InstanceSelect Then
            FrmSelectRight.ShowHidden = Not FrmSelectRight.ShowHidden
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Return
        End If
        '更改功能隐藏可见性
        If e.Key = Key.F12 Then
            PageSetupUI.HiddenForceShow = Not PageSetupUI.HiddenForceShow
            If PageSetupUI.HiddenForceShow Then
                Hint("功能隐藏设置已暂时关闭！", HintType.Finish)
            Else
                Hint("功能隐藏设置已重新开启！", HintType.Finish)
            End If
            PageSetupUI.HiddenRefresh()
            Return
        End If
        '按 F5 刷新页面
        If e.Key = Key.F5 Then
            If TypeOf PageLeft Is IRefreshable Then CType(PageLeft, IRefreshable).Refresh()
            If TypeOf PageRight Is IRefreshable Then CType(PageRight, IRefreshable).Refresh()
            Return
        End If
        '调用启动游戏
        If e.Key = Key.Enter AndAlso PageCurrent = FormMain.PageType.Launch Then
            If IsAprilEnabled AndAlso Not IsAprilGiveup Then
                Hint("木大！")
            Else
                FrmLaunchLeft.LaunchButtonClick()
            End If
        End If
        '修复按下 Alt 后误认为弹出系统菜单导致的冻结
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then e.Handled = True
    End Sub
    Private Sub FormMain_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseDown
        '鼠标侧键返回上一级
        If FrmMain.PanMsg.Children.Count > 0 OrElse WaitingMyMsgBox.Any Then Return '弹窗中（#5513）
        If e.ChangedButton = MouseButton.XButton1 OrElse e.ChangedButton = MouseButton.XButton2 Then TriggerPageBack()
    End Sub
    Private Sub TriggerPageBack()
        If PageCurrent = PageType.Download AndAlso PageCurrentSub = PageSubType.DownloadInstall AndAlso FrmDownloadInstall.IsInSelectPage Then
            FrmDownloadInstall.ExitSelectPage()
        ElseIf PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionInstall AndAlso FrmInstanceInstall.IsInSelectPage Then
            FrmInstanceInstall.ExitSelectPage()
        Else
            PageBack()
        End If
    End Sub

    '切回窗口
    Private Sub FormMain_Activated() Handles Me.Activated
        Try
            If Setup.Get("ToolDownloadClipboard") Then CompClipboard.GetClipboardResource()
            If PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionMod Then
                'Mod 管理自动刷新
                FrmInstanceMod.ReloadCompFileList()
            ElseIf PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionResourcePack Then
                '资源包管理自动刷新
                If FrmInstanceResourcePack IsNot Nothing Then FrmInstanceResourcePack.ReloadCompFileList()
            ElseIf PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionShader Then
                '光影包管理自动刷新
                If FrmInstanceShader IsNot Nothing Then FrmInstanceShader.ReloadCompFileList()
            ElseIf PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionSchematic Then
                '投影原理图管理自动刷新
                If FrmInstanceSchematic IsNot Nothing Then FrmInstanceSchematic.ReloadCompFileList()
            ElseIf PageCurrent = PageType.InstanceSelect Then
                '实例选择自动刷新
                LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
            End If
        Catch ex As Exception
            Log(ex, "切回窗口时出错", LogLevel.Feedback)
        End Try
    End Sub

    '文件拖放
    Private Sub HandleDrag(sender As Object, e As DragEventArgs)
        Try
            If e.Handled AndAlso (e.Effects <> DragDropEffects.None) Then Return
            e.Handled = True
            '缓存
            Static PrevData As IDataObject, PrevEffects As DragDropEffects
            If e.Data Is PrevData Then
                e.Effects = PrevEffects
                Return
            End If
            '确定拖放效果
            e.Effects = DragDropEffects.None
            If e.Data.GetDataPresent(DataFormats.Text) Then
                Dim Str As String = e.Data.GetData(DataFormats.Text)
                If Str.StartsWithF("authlib-injector:yggdrasil-server:") Then
                    e.Effects = DragDropEffects.Copy
                ElseIf Str.StartsWithF("file:///") Then
                    e.Effects = DragDropEffects.Copy
                End If
            ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim Files As String() = e.Data.GetData(DataFormats.FileDrop)
                If Files IsNot Nothing AndAlso Files.Length > 0 Then
                    e.Effects = DragDropEffects.Link
                End If
            End If
            PrevData = e.Data
            PrevEffects = e.Effects
            Log("[System] 设置拖放类型：" & GetStringFromEnum(e.Effects))
        Catch ex As Exception
            Log(ex, "处理拖放时出错", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub FrmMain_Drop(sender As Object, e As DragEventArgs) Handles Me.Drop
        Try
            If e.Data.GetDataPresent(DataFormats.Text) Then
                '获取文本
                Try
                    Dim Str As String = e.Data.GetData(DataFormats.Text)
                    Log("[System] 接受文本拖拽：" & Str)
                    If Str.StartsWithF("authlib-injector:yggdrasil-server:") Then
                        'Authlib 拖拽
                        e.Handled = True
                        e.Effects = DragDropEffects.Copy
                        Dim AuthlibServer As String = Net.WebUtility.UrlDecode(Str.Substring("authlib-injector:yggdrasil-server:".Length))
                        Log("[System] Authlib 拖拽：" & AuthlibServer)
                        If Not String.IsNullOrEmpty(New ValidateHttp().Validate(AuthlibServer)) Then
                            Hint($"输入的 Authlib 验证服务器不符合网址格式（{AuthlibServer}）！", HintType.Critical)
                            Return
                        End If
                        If MyMsgBox($"是否要创建新的第三方验证档案？{vbCrLf}验证服务器地址：{AuthlibServer}", "创建新的第三方验证档案", "确定", "取消") = 2 Then Exit Sub
                        SelectedProfile = Nothing
                        RunInUi(Sub()
                                    PageLoginAuth.DraggedAuthServer = AuthlibServer
                                    FrmLaunchLeft.RefreshPage(True, McLoginType.Auth)
                                End Sub)
                        If PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionSetup Then
                            '正在服务器选项页，需要刷新设置项显示
                            FrmInstanceSetup.Reload()
                        End If
                    ElseIf Str.StartsWithF("file:///") Then
                        '文件拖拽（例如从浏览器下载窗口拖入）
                        Dim FilePath = Net.WebUtility.UrlDecode(Str).Substring("file:///".Length).Replace("/", "\")
                        e.Handled = True
                        e.Effects = DragDropEffects.Copy
                        FileDrag(New List(Of String) From {FilePath})
                    End If
                Catch ex As Exception
                    Log(ex, "无法接取文本拖拽事件", LogLevel.Developer)
                    Return
                End Try
            ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
                '获取文件并检查
                Dim FilePathRaw = e.Data.GetData(DataFormats.FileDrop)
                If FilePathRaw Is Nothing Then '#2690
                    Hint("请将文件解压后再拖入！", HintType.Critical)
                    Return
                End If
                e.Handled = True
                e.Effects = DragDropEffects.Link
                FileDrag(CType(FilePathRaw, IEnumerable(Of String)))
            End If
        Catch ex As Exception
            Log(ex, "接取拖拽事件失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub FileDrag(FilePathList As IEnumerable(Of String))
        RunInNewThread(
        Sub()
            Dim FilePath As String = FilePathList.First
            Log("[System] 接受文件拖拽：" & FilePath & If(FilePathList.Any, $" 等 {FilePathList.Count} 个文件", ""), LogLevel.Developer)
            '基础检查
            If Directory.Exists(FilePathList.First) AndAlso Not File.Exists(FilePathList.First) Then
                Hint("请拖入一个文件，而非文件夹！", HintType.Critical)
                Return
            ElseIf Not File.Exists(FilePathList.First) Then
                Hint("拖入的文件不存在：" & FilePathList.First, HintType.Critical)
                Return
            End If
            '多文件拖拽
            If FilePathList.Count > 1 Then
                '检查是否为同类型文件
                Dim FirstExtension = FilePathList.First.AfterLast(".").ToLower
                Dim AllSameType = FilePathList.All(Function(f) f.AfterLast(".").ToLower = FirstExtension)

                If AllSameType AndAlso {"jar", "litemod", "disabled", "old", "litematic", "nbt", "schematic", "schem"}.Contains(FirstExtension) Then
                    '允许同类型的 Mod 文件或投影文件批量拖拽
                Else
                    Hint("一次请只拖入相同类型的文件！", HintType.Critical)
                    Return
                End If
            End If
            '主页
            Dim Extension As String = FilePath.AfterLast(".").ToLower
            If Extension = "xaml" Then
                Log("[System] 文件后缀为 XAML，作为主页加载")
                If File.Exists(Path & "PCL\Custom.xaml") Then
                    If MyMsgBox("已存在一个主页文件，是否要将它覆盖？", "覆盖确认", "覆盖", "取消") = 2 Then
                        Return
                    End If
                End If
                CopyFile(FilePath, Path & "PCL\Custom.xaml")
                RunInUi(
                Sub()
                    Setup.Set("UiCustomType", 1)
                    FrmLaunchRight.ForceRefresh()
                    Hint("已加载主页自定义文件！", HintType.Finish)
                End Sub)
                Return
            End If
            '安装 Mod
            If PageInstanceCompResource.InstallMods(FilePathList) Then Exit Sub
            '安装投影文件
            If {"litematic", "nbt", "schematic", "schem"}.Contains(Extension) Then
                Log($"[System] 文件为 {Extension} 格式，尝试作为原理图安装")
                ' 获取当前文件夹路径（如果在资源管理页面）
                Dim targetFolderPath As String = Nothing
                If PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionSchematic AndAlso
                   FrmInstanceSchematic IsNot Nothing AndAlso TypeOf FrmInstanceSchematic Is PageInstanceCompResource Then
                    targetFolderPath = DirectCast(FrmInstanceSchematic, PageInstanceCompResource).CurrentFolderPath
                End If
                PageInstanceCompResource.InstallCompFiles(FilePathList, CompType.Schematic, targetFolderPath)
                Exit Sub
            End If
            '处理资源安装
            If PageCurrent = PageType.InstanceSetup AndAlso {"zip"}.Any(Function(i) i = Extension) Then
                Select Case PageCurrentSub
                    Case PageSubType.VersionWorld
                        Dim DestFolder = PageInstanceLeft.Instance.PathIndie + "saves\" + GetFileNameWithoutExtentionFromPath(FilePath)
                        If Directory.Exists(DestFolder) Then
                            Hint("发现同名文件夹，无法粘贴：" + DestFolder, HintType.Critical)
                            Exit Sub
                        End If
                        ExtractFile(FilePath, DestFolder)
                        Hint($"已导入 {GetFileNameWithoutExtentionFromPath(FilePath)}", HintType.Finish)
                        If FrmInstanceSaves IsNot Nothing Then RunInUi(Sub() FrmInstanceSaves.Reload())
                        Exit Sub
                    Case PageSubType.VersionResourcePack
                        Dim DestFile = PageInstanceLeft.Instance.PathIndie + "resourcepacks\" + GetFileNameFromPath(FilePath)
                        If File.Exists(DestFile) Then
                            Hint("已存在同名文件：" + DestFile, HintType.Critical)
                            Exit Sub
                        End If
                        CopyFile(FilePath, DestFile)
                        Hint($"已导入 {GetFileNameFromPath(FilePath)}", HintType.Finish)
                        If FrmInstanceResourcePack IsNot Nothing Then RunInUi(Sub() FrmInstanceResourcePack.ReloadCompFileList())
                        Exit Sub
                    Case PageSubType.VersionShader
                        Dim DestFile = PageInstanceLeft.Instance.PathIndie + "shaderpacks\" + GetFileNameFromPath(FilePath)
                        If File.Exists(DestFile) Then
                            Hint("已存在同名文件：" + DestFile, HintType.Critical)
                            Exit Sub
                        End If
                        CopyFile(FilePath, DestFile)
                        Hint($"已导入 {GetFileNameFromPath(FilePath)}", HintType.Finish)
                        If FrmInstanceShader IsNot Nothing Then RunInUi(Sub() FrmInstanceShader.ReloadCompFileList())
                        Exit Sub
                End Select
            End If
            '处理投影文件
            If PageCurrent = PageType.InstanceSetup AndAlso {"litematic", "nbt", "schematic", "schem"}.Contains(Extension) AndAlso PageCurrentSub = PageSubType.VersionSchematic Then
                Dim DestFile = PageInstanceLeft.Instance.PathIndie + "schematics\" + GetFileNameFromPath(FilePath)
                If File.Exists(DestFile) Then
                    Hint("已存在同名文件：" + DestFile, HintType.Critical)
                    Exit Sub
                End If
                Directory.CreateDirectory(PageInstanceLeft.Instance.PathIndie + "schematics\")
                CopyFile(FilePath, DestFile)
                Hint($"已导入 {GetFileNameFromPath(FilePath)}", HintType.Finish)
                If FrmInstanceSchematic IsNot Nothing Then RunInUi(Sub() FrmInstanceSchematic.ReloadCompFileList())
                Exit Sub
            End If
            '安装整合包
            If {"zip", "rar", "mrpack"}.Any(Function(t) t = Extension) Then '部分压缩包是 zip 格式但后缀为 rar，总之试一试
                Log("[System] 文件为压缩包，尝试作为整合包安装")
                Try
                    ModpackInstall(FilePath)
                    Return
                Catch ex As CancelledException
                    Return '用户主动取消
                Catch ex As Exception
                    '安装失败，继续往后尝试
                End Try
            End If
            If {"zip", "rar"}.Any(Function(t) t = Extension) Then
                Log("[System] 文件为压缩包，尝试作为存档分析")
                Try
                    ReadWorld(FilePath)
                    Return
                Catch ex As CancelledException
                    Return '是存档，但是损坏了
                Catch ex As Exception
                    '不是存档（或遇到了其他问题），继续往后尝试
                End Try
            End If
            'RAR 处理
            If Extension = "rar" Then
                Hint("PCL 无法处理 rar 格式的压缩包，请在解压后重新压缩为 zip 格式再试！")
                Return
            End If
            '错误报告分析
            Try
                Log("[System] 尝试进行错误报告分析")
                Dim Analyzer As New CrashAnalyzer(GetUuid())
                Analyzer.Import(FilePath)
                If Not Analyzer.Prepare() Then Exit Try
                Analyzer.Analyze()
                Analyzer.Output(True, New List(Of String))
                Return
            Catch ex As Exception
                Log(ex, "自主错误报告分析失败", LogLevel.Feedback)
            End Try
            '未知操作
            Hint("PCL 无法确定应当执行的文件拖拽操作……")
        End Sub, "文件拖拽")
    End Sub

    '接受到 Windows 窗体事件
    Public IsSystemTimeChanged As Boolean = False
    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = 30 Then
            Dim NowDate = Date.Now
            If NowDate.Date = ApplicationOpenTime.Date Then
                Log("[System] 系统时间微调为：" & NowDate.ToLongDateString & " " & NowDate.ToLongTimeString)
                IsSystemTimeChanged = False
            Else
                Log("[System] 系统时间修改为：" & NowDate.ToLongDateString & " " & NowDate.ToLongTimeString)
                IsSystemTimeChanged = True
            End If
        ElseIf msg = 400 * 16 + 2 Then
            Log("[System] 收到置顶信息：" & hwnd.ToInt64)
            If Not IsWindowLoadFinished Then
                Log("[System] 窗口尚未加载完成，忽略置顶请求")
                Return IntPtr.Zero
            End If
            ShowWindowToTop()
            handled = True
        ElseIf msg = 26 Then 'WM_SETTINGCHANGE
            If Marshal.PtrToStringAuto(lParam) = "ImmersiveColorSet" Then
                Log($"[System] 系统主题更改，深色模式：{IsSystemInDarkMode()}")
                If Setup.Get("UiDarkMode") = 2 And IsDarkMode <> IsSystemInDarkMode() Then
                    IsDarkMode = IsSystemInDarkMode()
                    ThemeRefresh()
                End If
            End If
        End If

        Return IntPtr.Zero
    End Function

    '窗口隐藏与置顶
    Private _Hidden As Boolean = False
    Public Property Hidden As Boolean
        Get
            Return _Hidden
        End Get
        Set(value As Boolean)
            If _Hidden = value Then Return
            _Hidden = value
            If value Then
                '隐藏
                Left -= 10000
                ShowInTaskbar = False
                Visibility = Visibility.Hidden
                Log("[System] 窗口已隐藏，位置：(" & Left & "," & Top & ")")
            Else
                '取消隐藏
                If Left < -2000 Then Left += 10000
                ShowWindowToTop()
            End If
        End Set
    End Property
    '解决龙猫的非通用实现史山
    Protected Overrides Sub OnActivated(e As EventArgs)
        MyBase.OnActivated(e)
        If Hidden Then Hidden = False
    End Sub
    ''' <summary>
    ''' 把当前窗口拖到最前面。
    ''' </summary>
    Public Sub ShowWindowToTop()
        RunInUi(
        Sub()
            '这一坨乱七八糟的，别改，改了指不定就炸了，自己电脑还复现不出来
            Visibility = Visibility.Visible
            ShowInTaskbar = True
            WindowState = WindowState.Normal
            Hidden = False
            Topmost = True '偶尔 SetForegroundWindow 失效
            Topmost = False
            SetForegroundWindow(Handle)
            Focus()
            Log($"[System] 窗口已置顶，位置：({Left}, {Top}), {Width} x {Height}")
        End Sub)
    End Sub
    '背景视频循环播放
    Private Sub VideoEnded(sender As Object, e As RoutedEventArgs)
        VideoBack.Position = TimeSpan.Zero
        VideoBack.Play()
    End Sub
    '最小化时暂停背景视频
    Private Sub WindowStateChanged(sender As Object, e As EventArgs) Handles Me.StateChanged
        Select Case Me.WindowState
            Case WindowState.Minimized
                ModVideoBack.IsMinimized = True
                VideoPause()
            Case WindowState.Normal
                ModVideoBack.IsMinimized = False
                VideoPlay()
        End Select
    End Sub

#End Region

#Region "切换页面"

    '页面种类与属性
    '注意，这一枚举在 “切换页面” EventType 中调用，应视作公开 API 的一部分
    ''' <summary>
    ''' 页面种类。
    ''' </summary>
    Public Enum PageType
        ''' <summary>
        ''' 启动。
        ''' </summary>
        Launch = 0
        ''' <summary>
        ''' 下载。
        ''' </summary>
        Download = 1
        ''' <summary>
        ''' 联机。
        ''' </summary>
        Link = 2
        ''' <summary>
        ''' 设置。
        ''' </summary>
        Setup = 3
        ''' <summary>
        ''' 更多。
        ''' </summary>
        Other = 4
        ''' <summary>
        ''' 实例选择。这是一个副页面。
        ''' </summary>
        InstanceSelect = 5
        ''' <summary>
        ''' 任务管理。这是一个副页面。
        ''' </summary>
        TaskManager = 6
        ''' <summary>
        ''' 实例设置。这是一个副页面。
        ''' </summary>
        InstanceSetup = 7
        ''' <summary>
        ''' 资源工程详情。这是一个副页面。
        ''' </summary>
        CompDetail = 8
        ''' <summary>
        ''' 帮助详情。这是一个副页面。
        ''' </summary>
        HelpDetail = 9
        ''' <summary>
        ''' 游戏实时日志。这是一个副页面。
        ''' </summary>
        GameLog = 10
        ''' <summary>
        ''' Java 管理，这是一个副页面。
        ''' </summary>
        SetupJava = 11
        ''' <summary>
        ''' 存档详细管理，这是一个副页面。
        ''' </summary>
        VersionSaves = 12
        ''' <summary>
        ''' 主页市场，这是一个副页面。
        ''' </summary>
        HomePageMarket = 13
    End Enum
    ''' <summary>
    ''' 次要页面种类。其数值必须与 StackPanel 中的下标一致。
    ''' </summary>
    Public Enum PageSubType
        [Default] = 0
        DownloadInstall = 1
        DownloadClient = 4
        DownloadOptiFine = 5
        DownloadForge = 6
        DownloadNeoForge = 7
        DownloadCleanroom = 16
        DownloadFabric = 8
        DownloadQuilt = 10
        DownloadLiteLoader = 9
        DownloadLabyMod = 20
        DownloadLegacyFabric = 21
        DownloadMod = 11
        DownloadPack = 12
        DownloadDataPack = 13
        DownloadResourcePack = 14
        DownloadShader = 15
        DownloadCompFavorites = 17
        DownloadWorld = 18
        SetupLaunch = 0
        SetupUI = 1
        SetupSystem = 2
        SetupLink = 3
        LinkLobby = 1
        LinkSetup = 4
        LinkHelp = 5
        LinkFeedback = 6
        OtherHelp = 0
        OtherAbout = 1
        OtherTest = 2
        OtherFeedback = 3
        OtherVote = 4
        OtherLog = 5
        VersionOverall = 0
        VersionSetup = 1
        VersionExport = 2
        VersionWorld = 3
        VersionScreenshot = 4
        VersionMod = 5
        VersionModDisabled = 6
        VersionResourcePack = 7
        VersionShader = 8
        VersionSchematic = 9
        VersionInstall = 10
        VersionSavesInfo = 0
        VersionSavesBackup = 1
    End Enum
    ''' <summary>
    ''' 获取次级页面的名称。若并非次级页面则返回空字符串，故可以以此判断是否为次级页面。
    ''' </summary>
    Private Function PageNameGet(Stack As PageStackData) As String
        Select Case Stack.Page
            Case PageType.InstanceSelect
                Return "实例选择"
            Case PageType.TaskManager
                Return "任务管理"
            Case PageType.GameLog
                Return "实时日志"
            Case PageType.InstanceSetup
                Return "实例设置 - " & If(PageInstanceLeft.Instance Is Nothing, "未知实例", PageInstanceLeft.Instance.Name)
            Case PageType.CompDetail
                Return "资源下载 - " & CType(Stack.Additional(0), CompProject).TranslatedName
            Case PageType.HelpDetail
                Return CType(Stack.Additional(0), HelpEntry).Title
            Case PageType.SetupJava
                Return "Java 管理"
            Case PageType.VersionSaves
                Return $"存档管理 - {GetFolderNameFromPath(Stack.Additional)}"
            Case PageType.HomePageMarket
                Return "主页市场"
            Case Else
                Return ""
        End Select
    End Function
    ''' <summary>
    ''' 刷新次级页面的名称。
    ''' </summary>
    Public Sub PageNameRefresh(Type As PageStackData)
        LabTitleInner.Text = PageNameGet(Type)
    End Sub
    ''' <summary>
    ''' 刷新次级页面的名称。
    ''' </summary>
    Public Sub PageNameRefresh()
        PageNameRefresh(PageCurrent)
    End Sub

    '页面状态存储
    ''' <summary>
    ''' 当前的主页面。
    ''' </summary>
    Public PageCurrent As PageStackData = PageType.Launch
    ''' <summary>
    ''' 上一个主页面。
    ''' </summary>
    Public PageLast As PageStackData = PageType.Launch
    ''' <summary>
    ''' 当前的子页面。
    ''' </summary>
    Public ReadOnly Property PageCurrentSub As PageSubType
        Get
            Select Case PageCurrent
                Case PageType.Download
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    Return FrmDownloadLeft.PageID
                Case PageType.Setup
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    Return FrmSetupLeft.PageID
                Case PageType.Other
                    If FrmOtherLeft Is Nothing Then FrmOtherLeft = New PageOtherLeft
                    Return FrmOtherLeft.PageID
                Case PageType.InstanceSetup
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    Return FrmInstanceLeft.PageID
                Case Else
                    Return 0 '没有子页面
            End Select
        End Get
    End Property
    ''' <summary>
    ''' 上层页面的编号堆栈，用于返回。
    ''' </summary>
    Public PageStack As New List(Of PageStackData)
    Public Class PageStackData

        Public Page As PageType
        Public Additional As Object

        Public Overrides Function Equals(other As Object) As Boolean
            If other Is Nothing Then Return False
            If TypeOf other Is PageStackData Then
                Dim PageOther As PageStackData = other
                If Page <> PageOther.Page Then Return False
                If Additional Is Nothing Then
                    Return PageOther.Additional Is Nothing
                Else
                    Return PageOther.Additional IsNot Nothing AndAlso Additional.Equals(PageOther.Additional)
                End If
            ElseIf TypeOf other Is Integer Then
                If Page <> other Then Return False
                Return Additional Is Nothing
            Else
                Return False
            End If
        End Function
        Public Shared Operator =(left As PageStackData, right As PageStackData) As Boolean
            Return EqualityComparer(Of PageStackData).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As PageStackData, right As PageStackData) As Boolean
            Return Not left = right
        End Operator
        Public Shared Widening Operator CType(Value As PageType) As PageStackData
            Return New PageStackData With {.Page = Value}
        End Operator
        Public Shared Widening Operator CType(Value As PageStackData) As PageType
            Return Value.Page
        End Operator
    End Class
    Public PageLeft As MyPageLeft, PageRight As MyPageRight

    '引发实际页面切换的入口
    Private IsChangingPage As Boolean = False
    ''' <summary>
    ''' 切换页面，并引起对应选择 UI 的改变。
    ''' </summary>
    Public Sub PageChange(Stack As PageStackData, Optional SubType As PageSubType = PageSubType.Default)
        If PageNameGet(Stack) = "" Then
            '切换到主页面
            PageChangeExit()
            IsChangingPage = True '防止下面的勾选直接触发了 PageChangeActual
            CType(PanTitleSelect.Children(Stack), MyRadioButton).SetChecked(True, True, PageNameGet(PageCurrent) = "")
            IsChangingPage = False
            Select Case Stack.Page
                Case PageType.Download
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    For Each item In FrmDownloadLeft.PanItem.Children
                        If item.GetType() Is GetType(MyListItem) AndAlso Val(item.tag) = SubType Then
                            CType(item, MyListItem).SetChecked(True, True, Stack = PageCurrent)
                            Exit For
                        End If
                    Next
                Case PageType.Setup
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    CType(FrmSetupLeft.PanItem.Children(SubType), MyListItem).SetChecked(True, True, Stack = PageCurrent)
                Case PageType.Other
                    If FrmOtherLeft Is Nothing Then FrmOtherLeft = New PageOtherLeft
                    CType(FrmOtherLeft.PanItem.Children(SubType), MyListItem).SetChecked(True, True, Stack = PageCurrent)
            End Select
            PageChangeActual(Stack, SubType)
        Else
            '切换到次页面
            Select Case Stack.Page
                Case PageType.InstanceSetup
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    For Each item In FrmInstanceLeft.PanItem.Children
                        If item.GetType() Is GetType(MyListItem) AndAlso Val(item.tag) = SubType Then
                            CType(item, MyListItem).SetChecked(True, True, Stack = PageCurrent)
                            Exit For
                        End If
                    Next
                Case PageType.VersionSaves
                    If FrmInstanceSavesLeft Is Nothing Then FrmInstanceSavesLeft = New PageInstanceSavesLeft
                    For Each item In FrmInstanceSavesLeft.PanItem.Children
                        If item.GetType() Is GetType(MyListItem) AndAlso Val(item.tag) = SubType Then
                            CType(item, MyListItem).SetChecked(True, True, Stack = PageCurrent)
                            Exit For
                        End If
                    Next
            End Select
            PageChangeActual(Stack, SubType)
        End If
    End Sub
    ''' <summary>
    ''' 通过点击导航栏改变页面。
    ''' </summary>
    Private Sub BtnTitleSelect_Click(sender As MyRadioButton, raiseByMouse As Boolean) Handles BtnTitleSelect0.Check, BtnTitleSelect1.Check, BtnTitleSelect2.Check, BtnTitleSelect3.Check, BtnTitleSelect4.Check
        If IsChangingPage Then Return
        PageChangeActual(Val(sender.Tag))
    End Sub
    ''' <summary>
    ''' 通过点击返回按钮或手动触发返回来改变页面。
    ''' </summary>
    Public Sub PageBack() Handles BtnTitleInner.Click
        If PageStack.Any() Then
            PageChangeActual(PageStack(0))
        Else
            PageChange(PageType.Launch)
        End If
    End Sub

    '实际处理页面切换
    ''' <summary>
    ''' 切换现有页面的实际方法。
    ''' </summary>
    Private Sub PageChangeActual(Stack As PageStackData, Optional SubType As PageSubType = -1)
        If PageCurrent = Stack AndAlso (PageCurrentSub = SubType OrElse SubType = -1) Then Return
        AniControlEnabled += 1
        Try

#Region "子页面处理"
            Dim PageName As String = PageNameGet(Stack)
            If PageName = "" Then
                '即将切换到一个顶级页面
                PageChangeExit()
            Else
                '即将切换到一个子页面
                If PageStack.Any Then
                    '子页面 → 另一个子页面，更新
                    AniStart({
                        AaOpacity(LabTitleInner, -LabTitleInner.Opacity, 130),
                        AaCode(Sub() LabTitleInner.Text = PageName,, True),
                        AaOpacity(LabTitleInner, 1, 150, 30)
                    }, "FrmMain Titlebar SubLayer")
                    If PageStack.Contains(Stack) Then
                        '返回到更上层的子页面
                        Do While PageStack.Contains(Stack)
                            PageStack.RemoveAt(0)
                        Loop
                    Else
                        '进入更深层的子页面
                        PageStack.Insert(0, PageCurrent)
                    End If
                Else
                    '主页面 → 子页面，进入
                    PanTitleInner.Visibility = Visibility.Visible
                    PanTitleMain.IsHitTestVisible = False
                    PanTitleInner.IsHitTestVisible = True
                    PageNameRefresh(Stack)
                    AniStart({
                        AaOpacity(PanTitleMain, -PanTitleMain.Opacity, 150),
                        AaX(PanTitleMain, 12 - PanTitleMain.Margin.Left, 150,, New AniEaseInFluent(AniEasePower.Weak)),
                        AaOpacity(PanTitleInner, 1 - PanTitleInner.Opacity, 150, 200),
                        AaX(PanTitleInner, -PanTitleInner.Margin.Left, 350, 200, New AniEaseOutBack),
                        AaCode(Sub() PanTitleMain.Visibility = Visibility.Collapsed,, True)
                    }, "FrmMain Titlebar FirstLayer")
                    PageStack.Insert(0, PageCurrent)
                End If
            End If
#End Region

#Region "实际更改页面框架 UI"
            PageLast = PageCurrent
            PageCurrent = Stack
            Select Case Stack.Page
                Case PageType.Launch '启动
                    PageChangeAnim(FrmLaunchLeft, FrmLaunchRight)
                Case PageType.Download '下载
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    'PageGet 方法会在未设置 SubType 时指定默认值，并建立相关页面的实例
                    PageChangeAnim(FrmDownloadLeft, FrmDownloadLeft.PageGet(SubType))
                Case PageType.Link '联机
                    If FrmLinkLeft Is Nothing Then FrmLinkLeft = New PageLinkLeft
                    PageChangeAnim(FrmLinkLeft, FrmLinkLeft.PageGet(SubType))
                Case PageType.Setup '设置
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    PageChangeAnim(FrmSetupLeft, FrmSetupLeft.PageGet(SubType))
                Case PageType.SetupJava 'Java 设置
                    FrmSetupJava = If(FrmSetupJava, New PageSetupJava)
                    PageChangeAnim(New MyPageLeft, FrmSetupJava)
                Case PageType.Other '更多
                    If FrmOtherLeft Is Nothing Then FrmOtherLeft = New PageOtherLeft
                    PageChangeAnim(FrmOtherLeft, FrmOtherLeft.PageGet(SubType))
                Case PageType.GameLog '实时日志
                    If FrmLogLeft Is Nothing Then FrmLogLeft = New PageLogLeft
                    If FrmLogLeft Is Nothing Then FrmLogRight = New PageLogRight
                    PageChangeAnim(FrmLogLeft, FrmLogRight)
                Case PageType.InstanceSelect '实例选择
                    If FrmSelectLeft Is Nothing Then FrmSelectLeft = New PageSelectLeft
                    If FrmSelectRight Is Nothing Then FrmSelectRight = New PageSelectRight
                    PageChangeAnim(FrmSelectLeft, FrmSelectRight)
                Case PageType.TaskManager '任务管理
                    If FrmSpeedLeft Is Nothing Then FrmSpeedLeft = New PageSpeedLeft
                    If FrmSpeedRight Is Nothing Then FrmSpeedRight = New PageSpeedRight
                    PageChangeAnim(FrmSpeedLeft, FrmSpeedRight)
                Case PageType.InstanceSetup '实例设置
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    PageChangeAnim(FrmInstanceLeft, FrmInstanceLeft.PageGet(SubType))
                Case PageType.CompDetail 'Mod 信息
                    If FrmDownloadCompDetail Is Nothing Then FrmDownloadCompDetail = New PageDownloadCompDetail
                    PageChangeAnim(New MyPageLeft, FrmDownloadCompDetail)
                Case PageType.HelpDetail '帮助详情
                    PageChangeAnim(New MyPageLeft, Stack.Additional(1))
                Case PageType.VersionSaves '存档管理
                    If FrmInstanceSavesLeft Is Nothing Then FrmInstanceSavesLeft = New PageInstanceSavesLeft
                    PageInstanceSavesLeft.CurrentSave = Stack.Additional
                    PageChangeAnim(FrmInstanceSavesLeft, FrmInstanceSavesLeft.PageGet(SubType))
                Case PageType.HomePageMarket '主页市场
                    FrmHomepageMarket = If(FrmHomepageMarket, New PageHomePageMarket)
                    PageChangeAnim(New MyPageLeft, FrmHomepageMarket)
            End Select
#End Region

#Region "设置为最新状态"
            BtnExtraDownload.ShowRefresh()
            BtnExtraApril.ShowRefresh()
#End Region

            Log("[Control] 切换主要页面：" & GetStringFromEnum(Stack) & ", " & SubType)
        Catch ex As Exception
            Log(ex, "切换主要页面失败（ID " & PageCurrent.Page & "）", LogLevel.Feedback)
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Sub PageChangeAnim(TargetLeft As FrameworkElement, TargetRight As FrameworkElement)
        AniStop("FrmMain LeftChange")
        AniStop("PageLeft PageChange") '停止左边栏变更导致的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        AniControlEnabled += 1
        '清除新页面关联性
        If Not IsNothing(TargetLeft.Parent) Then TargetLeft.SetValue(ContentPresenter.ContentProperty, Nothing)
        If Not IsNothing(TargetRight) AndAlso Not IsNothing(TargetRight.Parent) Then TargetRight.SetValue(ContentPresenter.ContentProperty, Nothing)
        PageLeft = TargetLeft
        PageRight = TargetRight
        '触发页面通用动画
        CType(PanMainLeft.Child, MyPageLeft).TriggerHideAnimation()
        CType(PanMainRight.Child, MyPageRight).PageOnExit()
        AniControlEnabled -= 1
        '执行动画
        AniStart({
            AaCode(
            Sub()
                AniControlEnabled += 1
                '把新页面添加进容器
                PanMainLeft.Child = PageLeft
                PageLeft.Opacity = 0
                PanMainLeft.Background = Nothing
                AniControlEnabled -= 1
                RunInUi(Sub() PanMainLeft_Resize(PanMainLeft.ActualWidth), True)
            End Sub, 110),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageLeft.Opacity = 1
                PageLeft.TriggerShowAnimation()
            End Sub, 30, True)
        }, "FrmMain PageChangeLeft")
        AniStart({
            AaCode(
            Sub()
                AniControlEnabled += 1
                CType(PanMainRight.Child, MyPageRight).PageOnForceExit()
                '把新页面添加进容器
                PanMainRight.Child = PageRight
                PageRight.Opacity = 0
                PanMainRight.Background = Nothing
                AniControlEnabled -= 1
                RunInUi(Sub() BtnExtraBack.ShowRefresh(), True)
            End Sub, 110),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageRight.Opacity = 1
                PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "FrmMain PageChangeRight")
    End Sub
    ''' <summary>
    ''' 退出子界面。
    ''' </summary>
    Private Sub PageChangeExit()
        If PageStack.Any Then
            '子页面 → 主页面，退出
            PanTitleMain.Visibility = Visibility.Visible
            PanTitleMain.IsHitTestVisible = True
            PanTitleInner.IsHitTestVisible = False
            AniStart({
                AaOpacity(PanTitleInner, -PanTitleInner.Opacity, 150),
                AaX(PanTitleInner, -18 - PanTitleInner.Margin.Left, 150,, New AniEaseInFluent),
                AaOpacity(PanTitleMain, 1 - PanTitleMain.Opacity, 150, 200),
                AaX(PanTitleMain, -PanTitleMain.Margin.Left, 350, 200, New AniEaseOutBack(AniEasePower.Weak)),
                AaCode(Sub() PanTitleInner.Visibility = Visibility.Collapsed,, True)
            }, "FrmMain Titlebar FirstLayer")
            PageStack.Clear()
        Else
            '主页面 → 主页面，无事发生
        End If
    End Sub

    '左边栏改变
    Private Sub PanMainLeft_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles PanMainLeft.SizeChanged
        If Not e.WidthChanged Then Return
        PanMainLeft_Resize(e.NewSize.Width)
    End Sub
    Private Sub PanMainLeft_Resize(NewWidth As Double)
        Dim Delta As Double = NewWidth - RectLeftBackground.Width
        If Math.Abs(Delta) > 0.1 AndAlso AniControlEnabled = 0 Then
            If PanMain.Opacity < 0.1 Then PanMainLeft.IsHitTestVisible = False '避免左边栏指向背景未能完美覆盖左边栏
            If NewWidth > 0 Then
                '宽度足够，显示
                AniStart({
                    AaWidth(RectLeftBackground, NewWidth - RectLeftBackground.Width, 180,, New AniEaseOutFluent(AniEasePower.ExtraStrong)),
                    AaOpacity(RectLeftShadow, 1 - RectLeftShadow.Opacity, 180),
                    AaCode(Sub() PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            Else
                '宽度不足，隐藏
                AniStart({
                    AaWidth(RectLeftBackground, -RectLeftBackground.Width, 180,, New AniEaseOutFluent),
                    AaOpacity(RectLeftShadow, -RectLeftShadow.Opacity, 180),
                    AaCode(Sub() PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            End If
        Else
            RectLeftBackground.Width = NewWidth
            PanMainLeft.IsHitTestVisible = True
            AniStop("FrmMain LeftChange")
        End If
    End Sub

#End Region

#Region "控件拖动"

    '在时钟中调用，使得即使鼠标在窗口外松开，也可以释放控件
    Public Sub DragTick()
        If DragControl Is Nothing Then Return
        If Not Mouse.LeftButton = MouseButtonState.Pressed Then
            DragStop()
        End If
    End Sub
    '在鼠标移动时调用，以改变 Slider 位置
    Public Sub DragDoing() Handles PanBack.MouseMove
        If DragControl Is Nothing Then Return
        If Mouse.LeftButton = MouseButtonState.Pressed Then
            DragControl.DragDoing()
        Else
            DragStop()
        End If
    End Sub
    Public Sub DragStop()
        '存在其他线程调用的可能性，因此需要确保在 UI 线程运行
        RunInUi(Sub()
                    If DragControl Is Nothing Then Return
                    Dim Control = DragControl
                    DragControl = Nothing
                    Control.DragStop() '控件会在该事件中判断 DragControl，所以得放在后面
                End Sub)
    End Sub

#End Region

#Region "附加按钮"

    '音乐
    Private Sub BtnExtraMusic_Click(sender As Object, e As EventArgs) Handles BtnExtraMusic.Click
        MusicControlPause()
    End Sub
    Private Sub BtnExtraMusic_RightClick(sender As Object, e As EventArgs) Handles BtnExtraMusic.RightClick
        MusicControlNext()
    End Sub

    '任务管理
    Private Sub BtnExtraDownload_Click(sender As Object, e As EventArgs) Handles BtnExtraDownload.Click
        PageChange(PageType.TaskManager)
    End Sub
    Private Function BtnExtraDownload_ShowCheck() As Boolean
        Return HasDownloadingTask() AndAlso Not PageCurrent = PageType.TaskManager
    End Function

    '投降
    Public Sub AprilGiveup() Handles BtnExtraApril.Click
        If IsAprilEnabled AndAlso Not IsAprilGiveup Then
            Hint("=D", HintType.Finish)
            IsAprilGiveup = True
            FrmLaunchLeft.AprilScaleTrans.ScaleX = 1
            FrmLaunchLeft.AprilScaleTrans.ScaleY = 1
            BtnExtraApril.ShowRefresh()
        End If
    End Sub
    Public Function BtnExtraApril_ShowCheck() As Boolean
        Return IsAprilEnabled AndAlso Not IsAprilGiveup AndAlso PageCurrent = PageType.Launch
    End Function

    '关闭 Minecraft
    Public Sub BtnExtraShutdown_Click() Handles BtnExtraShutdown.Click
        Try
            If McLaunchLoaderReal IsNot Nothing Then McLaunchLoaderReal.Abort()
            For Each Watcher In McWatcherList
                Watcher.Kill()
            Next
            Hint("已关闭运行中的 Minecraft！", HintType.Finish)
        Catch ex As Exception
            Log(ex, "强制关闭所有 Minecraft 失败", LogLevel.Feedback)
        End Try
    End Sub
    Public Function BtnExtraShutdown_ShowCheck() As Boolean
        Return HasRunningMinecraft
    End Function

    '游戏日志
    Public Sub BtnExtraLog_Click() Handles BtnExtraLog.Click
        PageChange(PageType.GameLog)
    End Sub
    Public Function BtnExtraLog_ShowCheck() As Boolean
        If FrmLogLeft Is Nothing OrElse FrmLogRight Is Nothing OrElse PageCurrent = PageType.GameLog Then Return False
        Return FrmLogLeft.ShownLogs.Count > 0
    End Function

    ''' <summary>
    ''' 返回顶部。
    ''' </summary>
    Public Sub BackToTop() Handles BtnExtraBack.Click
        Dim RealScroll As MyScrollViewer = BtnExtraBack_GetRealChild()
        If RealScroll IsNot Nothing Then
            RealScroll.PerformVerticalOffsetDelta(-RealScroll.VerticalOffset)
        Else
            Log("[UI] 无法返回顶部，未找到合适的 RealScroll", LogLevel.Hint)
        End If
    End Sub
    Private Function BtnExtraBack_ShowCheck() As Boolean
        Dim RealScroll As MyScrollViewer = BtnExtraBack_GetRealChild()
        Return RealScroll IsNot Nothing AndAlso RealScroll.Visibility = Visibility.Visible AndAlso RealScroll.VerticalOffset > Height + If(BtnExtraBack.Show, 0, 700)
    End Function
    Private Function BtnExtraBack_GetRealChild() As MyScrollViewer
        If PanMainRight.Child Is Nothing OrElse TypeOf PanMainRight.Child IsNot MyPageRight Then Return Nothing
        Return CType(PanMainRight.Child, MyPageRight).PanScroll
    End Function

#End Region

    '愚人节鼠标位置
    Public lastMouseArg As MouseEventArgs = Nothing
    Private Sub FormMain_MouseMove(sender As Object, e As MouseEventArgs) Handles Me.MouseMove
        lastMouseArg = e
    End Sub

End Class
