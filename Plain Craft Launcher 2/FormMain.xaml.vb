Imports System.ComponentModel
Imports System.Windows.Interop

Public Class FormMain

#Region "基础"

    '更新日志
    Private Sub ShowUpdateLog(LastVersion As Integer)
        Dim FeatureCount As Integer = 0, BugCount As Integer = 0
        Dim FeatureList As New List(Of KeyValuePair(Of Integer, String))
        '统计更新日志条目
#If BETA Then
        If LastVersion < 367 Then 'Release 2.10.6
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：启用 MCIM 社区资源镜像源，以缓解社区资源难以下载的问题"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：正版登录出错时无法给出正确的错误信息"))
            FeatureCount += 9
            BugCount += 9
        End If
        If LastVersion < 365 Then 'Release 2.10.5
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：下载资源时，会单独记忆每种资源上次下载到的文件夹，以防混淆"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：网络底层框架与下载稳定性"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法启动一部分 LabyMod 和 GTNH 客户端"))
            FeatureCount += 22
            BugCount += 26
        End If
        If LastVersion < 361 Then 'Release 2.10.3
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法安装部分使用老版本 PCL 导出的整合包"))
        End If
        If LastVersion < 359 Then 'Release 2.10.2
            If LastVersion >= 357 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：下载资源包、光影包时能自动跳转到对应的文件夹"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：调整界面样式与动画，让整体视觉更干净，操作体验更顺滑"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法从 CurseForge 下载 Mod 等资源，或是安装整合包"))
            FeatureCount += 28
            BugCount += 28
        End If
        If LastVersion < 357 Then 'Release 2.10.0
            FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：下载资源包、光影包、数据包"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：允许设置文件下载源"))
            FeatureCount += 9
            BugCount += 26
        End If
        If LastVersion < 355 Then 'Release 2.9.3
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：Minecraft 会优先使用独立显卡运行"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：简化下载新版本第二步的 UI"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：使用新的版本隔离策略"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：添加了在全局启动设置与版本独立设置之间互相跳转的按钮"))
            FeatureCount += 20
            BugCount += 28
        End If
        If LastVersion < 352 Then 'Release 2.9.1
            FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：导出整合包功能"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：支持在超长路径下安装、启动游戏"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法安装 1.21 OptiFine"))
            FeatureCount += 23
            BugCount += 10
        End If
        If LastVersion < 349 Then 'Release 2.8.13
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：网络环境不佳时，Mod、整合包页面的加载速度"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：部分电脑无法打开程序"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：可能无法识别、更新 Mod"))
            FeatureCount += 6
            BugCount += 14
        End If
        If LastVersion < 347 Then 'Release 2.8.12
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：Mod 管理页面添加下载 Mod、安装 Mod 选项"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：Mod 详情页面支持按加载器、游戏版本进行分类和筛选"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：支持安装同时包含 modpack 文件和启动器的懒人包"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "优化：整合包导入流程"))
            FeatureCount += 43
            BugCount += 37
        End If
        If LastVersion < 342 Then 'Release 2.8.9
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：支持下载原版服务端"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：本地 Mod 的标题支持选择显示 Mod 原始文件名"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：搜索后启用/禁用 Mod 时出错"))
            FeatureCount += 17
            BugCount += 13
        End If
        If LastVersion < 340 Then 'Release 2.8.8
            If LastVersion = 338 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：数个与新正版登录相关的严重 Bug"))
            FeatureCount += 3
            BugCount += 7
        End If
        If LastVersion < 338 Then 'Release 2.8.7
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "优化：使用新的正版登录方式，以提高安全性"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：安装整合包、检索 Mod 的稳定性"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法加载部分 Mod 的图标"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：在 Mod 管理页面删除 Mod 导致报错"))
            FeatureCount += 11
            BugCount += 21
        End If
#Else
        '5：          FEAT+
        '4：     IMP+ FEAT*
        '3：BUG+ IMP* FEAT-
        '2：BUG* IMP-
        '1：BUG-
        If LastVersion < 366 Then 'Snapshot 2.10.6
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：启用 MCIM 社区资源镜像源，以缓解社区资源难以下载的问题"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：正版登录出错时无法给出正确的错误信息"))
            FeatureCount += 9
            BugCount += 9
        End If
        If LastVersion < 364 Then 'Snapshot 2.10.5
            If LastVersion >= 363 Then
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法添加新的正版账号"))
                BugCount += 1
            End If
        End If
        If LastVersion < 363 Then 'Snapshot 2.10.4
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：下载资源时，会单独记忆每种资源上次下载到的文件夹，以防混淆"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：网络底层框架与下载稳定性"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法启动一部分 LabyMod 和 GTNH 客户端"))
            FeatureCount += 22
            BugCount += 26
        End If
        If LastVersion < 362 Then 'Snapshot 2.10.3
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法安装部分使用老版本 PCL 导出的整合包"))
        End If
        If LastVersion < 360 Then 'Snapshot 2.10.2
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法从 CurseForge 下载 Mod 等资源，或是安装整合包"))
            If LastVersion >= 358 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法加载正版皮肤的头像"))
            FeatureCount += 3
            BugCount += 5
        End If
        If LastVersion < 358 Then 'Snapshot 2.10.1
            If LastVersion >= 356 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：下载资源包、光影包时能自动跳转到对应的文件夹"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：调整界面样式与动画，让整体视觉更干净，操作体验更顺滑"))
            FeatureCount += 25
            BugCount += 23
        End If
        If LastVersion < 356 Then 'Snapshot 2.10.0
            FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：下载资源包、光影包、数据包"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：允许设置文件下载源"))
            FeatureCount += 9
            BugCount += 26
        End If
        If LastVersion < 354 Then 'Snapshot 2.9.3
            If LastVersion = 352 Then
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：低版本 MC 没有声音"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：若不安装 Mod 加载器，则无法安装 OptiFine 1.14+"))
            End If
            FeatureCount += 8
            BugCount += 8
        End If
        If LastVersion < 352 Then 'Snapshot 2.9.2
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：Minecraft 会优先使用独立显卡运行"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：简化下载新版本第二步的 UI"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：使用新的版本隔离策略"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：添加了在全局启动设置与版本独立设置之间互相跳转的按钮"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：使用快照版导出带 PCL 的整合包可能失败"))
            FeatureCount += 12
            BugCount += 20
        End If
        If LastVersion < 351 Then 'Snapshot 2.9.1
            If LastVersion = 350 Then
                FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "优化：对导出整合包功能进行了 7 项优化，详见完整更新日志"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法进行第三方登录"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：在部分版本下，打开导出页面会导致启动器崩溃"))
                BugCount += 1
            End If
            BugCount += 2
        End If
        If LastVersion < 350 Then 'Snapshot 2.9.0
            FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：导出整合包功能"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：支持在超长路径下安装、启动游戏"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法安装 1.21 OptiFine"))
            FeatureCount += 21
            BugCount += 10
        End If
        If LastVersion < 348 Then 'Snapshot 2.8.13
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：网络环境不佳时，Mod、整合包页面的加载速度"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：部分电脑无法打开程序"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：可能无法识别、更新 Mod"))
            FeatureCount += 6
            BugCount += 14
        End If
        If LastVersion < 346 Then 'Snapshot 2.8.12
            If LastVersion = 345 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：帮助页面报错"))
        End If
        If LastVersion < 345 Then 'Snapshot 2.8.11
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：Mod 管理页面添加下载 Mod、安装 Mod 选项"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：Mod 详情页面支持按加载器、游戏版本进行分类和筛选"))
            FeatureCount += 23
            BugCount += 21
        End If
        If LastVersion < 343 Then 'Snapshot 2.8.10
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：支持安装同时包含 modpack 文件和启动器的懒人包"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "优化：整合包导入流程"))
            FeatureCount += 20
            BugCount += 16
        End If
        If LastVersion < 341 Then 'Snapshot 2.8.9
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：支持下载原版服务端"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：本地 Mod 的标题支持选择显示 Mod 原始文件名"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：搜索后启用/禁用 Mod 时出错"))
            FeatureCount += 17
            BugCount += 13
        End If
        If LastVersion < 339 Then 'Snapshot 2.8.8
            If LastVersion = 337 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：数个与新正版登录相关的严重 Bug"))
            FeatureCount += 3
            BugCount += 7
        End If
        If LastVersion < 337 Then 'Snapshot 2.8.7
            FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "优化：使用新的正版登录方式，以提高安全性"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：安装整合包、检索 Mod 的稳定性"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法加载部分 Mod 的图标"))
            FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：在 Mod 管理页面删除 Mod 导致报错"))
            FeatureCount += 11
            BugCount += 21
        End If
#End If
        '整理更新日志文本
        Dim ContentList As New List(Of String)
        Dim SortedFeatures = FeatureList.OrderByDescending(Function(f) f.Key).ToList
        If Not SortedFeatures.Any() AndAlso FeatureCount = 0 AndAlso BugCount = 0 Then ContentList.Add("没有更新日志……")
        For i = 0 To Math.Min(9, SortedFeatures.Count - 1) '最多取 10 项
            ContentList.Add(SortedFeatures(i).Value)
        Next
        If SortedFeatures.Count > 10 Then FeatureCount += SortedFeatures.Count - 10
        If FeatureCount > 0 OrElse BugCount > 0 Then
            ContentList.Add(If(FeatureCount > 0, FeatureCount & " 项小调整与修改", "") &
                If(FeatureCount > 0 AndAlso BugCount > 0, "，", "") &
                If(BugCount > 0, "修复了 " & BugCount & " 个 Bug", "") &
                "，详见完整更新日志")
        End If
        Dim Content As String = "· " & Join(ContentList, vbCrLf & "· ")
        '输出更新日志
        RunInNewThread(
        Sub()
            If MyMsgBox(Content, "PCL 已更新至 " & VersionDisplayName, "确定", "完整更新日志") = 2 Then
                OpenWebsite("https://afdian.com/a/LTCat?tab=feed")
            End If
        End Sub, "UpdateLog Output")
    End Sub

    '窗口加载
    Private IsWindowLoadFinished As Boolean = False
    Public Sub New()
        ApplicationStartTick = GetTimeTick()
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
            ElseIf Not Setup.IsUnset("LaunchVersionSelect") Then
                Log("[Start] 从老 PCL 升级，但此前未调整版本隔离，使用老的版本隔离默认值")
                Setup.Set("LaunchArgumentIndieV2", Setup.GetDefault("LaunchArgumentIndie"))
            Else
                Log("[Start] 全新的 PCL，使用新的版本隔离默认值")
                Setup.Set("LaunchArgumentIndieV2", Setup.GetDefault("LaunchArgumentIndieV2"))
            End If
        End If
        '刷新主题
        ThemeCheckAll(False)
        Setup.Load("UiLauncherTheme")
        '注册拖拽事件（不能直接加 Handles，否则没用；#6340）
        [AddHandler](DragDrop.DragEnterEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        [AddHandler](DragDrop.DragOverEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        '加载 UI
        InitializeComponent()
        Opacity = 0
        '开启管理员权限下的文件拖拽
        If IsAdmin() Then
            Static Helper As New DragHelper
            AddHandler SourceInitialized,
            Sub()
                Dim WpfHelper As New WindowInteropHelper(Me)
                Helper.HwndIntPtrSource = HwndSource.FromHwnd(WpfHelper.Handle)
                Helper.AddHook()
            End Sub
            AddHandler Closing, Sub() Helper.RemoveDragHook()
            AddHandler Helper.DragDrop, Sub() FileDrag(Helper.DropFilePaths)
        End If
        '切换到首页
        If Not IsNothing(FrmLaunchLeft.Parent) Then FrmLaunchLeft.SetValue(ContentPresenter.ContentProperty, Nothing)
        If Not IsNothing(FrmLaunchRight.Parent) Then FrmLaunchRight.SetValue(ContentPresenter.ContentProperty, Nothing)
        PanMainLeft.Child = FrmLaunchLeft
        PageLeft = FrmLaunchLeft
        PanMainRight.Child = FrmLaunchRight
        PageRight = FrmLaunchRight
        FrmLaunchRight.PageState = MyPageRight.PageStates.ContentStay
        '模式提醒
#If DEBUG Then
        Hint("[开发者模式] PCL 正以开发者模式运行，这可能会造成严重的性能下降，请务必立即向开发者反馈此问题！", HintType.Critical)
#End If
        If ModeDebug Then Hint("[调试模式] PCL 正以调试模式运行，这可能会导致性能下降，若无必要请不要开启！")
        '尽早执行的加载池
        McFolderListLoader.Start(0) '为了让下载已存在文件检测可以正常运行，必须跑一次；为了让启动按钮尽快可用，需要尽早执行；为了与 PageLaunchLeft 联动，需要为 0 而不是 GetUuid

        Log("[Start] 第二阶段加载用时：" & GetTimeTick() - ApplicationStartTick & " ms")
    End Sub
    Private Sub FormMain_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ApplicationStartTick = GetTimeTick()
        Handle = New Interop.WindowInteropHelper(Me).Handle
        '读取设置
        Setup.Load("UiBackgroundOpacity")
        Setup.Load("UiBackgroundBlur")
        Setup.Load("UiLogoType")
        Setup.Load("UiHiddenPageDownload")
        PageSetupUI.BackgroundRefresh(False, True)
        MusicRefreshPlay(False, True)
        '扩展按钮
        BtnExtraDownload.ShowCheck = AddressOf BtnExtraDownload_ShowCheck
        BtnExtraBack.ShowCheck = AddressOf BtnExtraBack_ShowCheck
        BtnExtraApril.ShowCheck = AddressOf BtnExtraApril_ShowCheck
        BtnExtraShutdown.ShowCheck = AddressOf BtnExtraShutdown_ShowCheck
        BtnExtraApril.ShowRefresh()
        '初始化尺寸改变
        Dim Resizer As New MyResizer(Me)
        Resizer.addResizerDown(ResizerB)
        Resizer.addResizerLeft(ResizerL)
        Resizer.addResizerLeftDown(ResizerLB)
        Resizer.addResizerLeftUp(ResizerLT)
        Resizer.addResizerRight(ResizerR)
        Resizer.addResizerRightDown(ResizerRB)
        Resizer.addResizerRightUp(ResizerRT)
        Resizer.addResizerUp(ResizerT)
        'PLC 彩蛋
        If RandomInteger(1, 1000) = 233 Then
            ShapeTitleLogo.Data = New GeometryConverter().ConvertFromString("M26,29 v-25 h6 a7,7 180 0 1 0,14 h-6 M83,6.5 a10,11.5 180 1 0 0,18 M48,2.5 v24.5 h13.5")
        End If
        '加载窗口
        ThemeRefreshMain()
        Try
            Height = Setup.Get("WindowHeight")
            Width = Setup.Get("WindowWidth")
        Catch ex As Exception '修复 #2019
            Log(ex, "读取窗口默认大小失败", LogLevel.Hint)
            Height = MinHeight + 100
            Width = MinWidth + 100
        End Try
        '#If DEBUG Then
        '        MinHeight = 50
        '        MinWidth = 50
        '#End If
        Topmost = False
        If FrmStart IsNot Nothing Then FrmStart.Close(New TimeSpan(0, 0, 0, 0, 400 / AniSpeed))
        '更改窗口
        Top = (GetWPFSize(My.Computer.Screen.WorkingArea.Height) - Height) / 2
        Left = (GetWPFSize(My.Computer.Screen.WorkingArea.Width) - Width) / 2
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
            '启动加载器池
            Try
                JavaListInit() '延后到同意协议后再执行，避免在初次启动时进行进程操作
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
                If File.Exists(Path & "PCL\Plain Craft Launcher 2.exe") Then File.Delete(Path & "PCL\Plain Craft Launcher 2.exe")
            Catch ex As Exception
                Log(ex, "清理自动更新文件失败")
            End Try
            '上报
            Telemetry("启动")
        End Sub, "初始化", ThreadPriority.Lowest)

        Log("[Start] 第三阶段加载用时：" & GetTimeTick() - ApplicationStartTick & " ms")
    End Sub
    '根据打开次数触发的事件
    Private Sub RunCountSub()
        Setup.Set("SystemCount", Setup.Get("SystemCount") + 1)
#If Not BETA Then
        Select Case Setup.Get("SystemCount")
            Case 1
                MyMsgBox("欢迎使用 PCL 快照版！" & vbCrLf &
                         "快照版包含尚未在正式版发布的测试性功能，仅用于赞助者本人尝鲜。所以请不要发给其他人或者用于制作整合包哦！" & vbCrLf &
                         "如果你并非通过赞助或赞助者本人邀请进群获得的本程序，那么可能是有人在违规传播，记得提醒他一下啦。", "快照版使用说明")
        End Select
        If Setup.Get("SystemCount") >= 99 Then
            If ThemeUnlock(6, False) Then
                MyMsgBox("你已经使用了 99 次 PCL 啦，感谢你长期以来的支持！" & vbCrLf &
                         "隐藏主题 铁杆粉 已解锁！", "提示")
            End If
        End If
#End If
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
        '单向迁移微软登录结果（#4836）
        If Not Setup.Get("CacheMsV2Migrated") Then
            Setup.Set("CacheMsV2Migrated", True)
            Setup.Set("CacheMsV2OAuthRefresh", Setup.Get("CacheMsOAuthRefresh"))
            Setup.Set("CacheMsV2Access", Setup.Get("CacheMsAccess"))
            Setup.Set("CacheMsV2ProfileJson", Setup.Get("CacheMsProfileJson"))
            Setup.Set("CacheMsV2Uuid", Setup.Get("CacheMsUuid"))
            Setup.Set("CacheMsV2Name", Setup.Get("CacheMsName"))
            Log("[Start] 已从老版本迁移微软登录结果")
        End If
        'Mod 命名设置迁移
        If Not Setup.IsUnset("ToolDownloadTranslate") AndAlso Setup.IsUnset("ToolDownloadTranslateV2") Then
            Setup.Set("ToolDownloadTranslateV2", Setup.Get("ToolDownloadTranslate") + 1)
            Log("[Start] 已从老版本迁移 Mod 命名设置")
        End If
        '输出更新日志
        If LastVersionCode <= 0 Then Return
        If LowerVersionCode >= VersionCode Then Return
        ShowUpdateLog(LowerVersionCode)
    End Sub
    Private Sub DowngradeSub(LastVersionCode As Integer)
        Log("[Start] 版本号从 " & LastVersionCode & " 降低到 " & VersionCode)
        Setup.Set("SystemLastVersionReg", VersionCode)
    End Sub

#End Region

#Region "自定义窗口"

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
        '关闭
        RunInUiWait(
        Sub()
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
        IsProgramEnded = True
        AniControlEnabled += 1
        If IsUpdateWaitingRestart Then UpdateRestart(False)
        If ReturnCode = ProcessReturnValues.Exception Then
            If Not IsLogShown Then
                FeedbackInfo()
                Log("请在 https://github.com/Meloong-Git/PCL/issues 提交错误报告，以便于作者解决此问题！")
                IsLogShown = True
                ShellOnly(Path & "PCL\Log1.txt")
            End If
            Thread.Sleep(500) '防止 PCL 在记事本打开前就被掐掉
        End If
        Log("[System] 程序已退出，返回值：" & GetStringFromEnum(ReturnCode))
        LogFlush()
        If ReturnCode <> ProcessReturnValues.Success Then Environment.Exit(ReturnCode)
        Process.GetCurrentProcess.Kill()
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
        If WindowState = WindowState.Maximized Then WindowState = WindowState.Normal '修复 #1938
    End Sub

    '最小化
    Private Sub BtnTitleMin_Click() Handles BtnTitleMin.Click
        WindowState = WindowState.Minimized
    End Sub

#End Region

#Region "窗体事件"

    '按键事件
    Private Sub FormMain_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.IsRepeat Then Return
        '修复按下 Alt 后误认为弹出系统菜单导致的冻结
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then e.Handled = True
        '在有弹窗时：回车选择第一个，Esc 选择最后一个
        If PanMsg.Children.Any Then
            If e.Key = Key.Enter Then
                CType(PanMsg.Children(0), Object).Btn1_Click()
            ElseIf e.Key = Key.Escape Then
                Dim Msg As Object = PanMsg.Children(0)
                If TypeOf Msg IsNot MyMsgInput AndAlso TypeOf Msg IsNot MyMsgSelect AndAlso Msg.Btn3.Visibility = Visibility.Visible Then
                    Msg.Btn3_Click()
                ElseIf Msg.Btn2.Visibility = Visibility.Visible Then
                    Msg.Btn2_Click()
                Else
                    Msg.Btn1_Click()
                End If
            End If
            Return
        End If

        '==========================
        ' 在没有弹窗时：继续检查……
        '==========================

        '按 ESC 返回上一级
        If e.Key = Key.Escape Then TriggerPageBack()
        '更改隐藏版本可见性
        If e.Key = Key.F11 AndAlso PageCurrent = FormMain.PageType.VersionSelect Then
            FrmSelectRight.ShowHidden = Not FrmSelectRight.ShowHidden
            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
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
    End Sub
    Private Sub FormMain_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseDown
        '鼠标侧键返回上一级
        If FrmMain.PanMsg.Children.Count > 0 OrElse WaitingMyMsgBox.Any Then Return '弹窗中（#5513）
        If e.ChangedButton = MouseButton.XButton1 OrElse e.ChangedButton = MouseButton.XButton2 Then TriggerPageBack()
    End Sub
    Private Sub TriggerPageBack()
        If PageCurrent = PageType.Download AndAlso PageCurrentSub = PageSubType.DownloadInstall AndAlso FrmDownloadInstall.IsInSelectPage Then
            FrmDownloadInstall.ExitSelectPage()
        Else
            PageBack()
        End If
    End Sub

    '切回窗口
    Private Sub FormMain_Activated() Handles Me.Activated
        Try
            If PageCurrent = PageType.VersionSetup AndAlso PageCurrentSub = PageSubType.VersionMod Then
                'Mod 管理自动刷新
                FrmVersionMod.ReloadModList()
            ElseIf PageCurrent = PageType.VersionSelect Then
                '版本选择自动刷新
                LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
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
            ShowWindowToTop()
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
                        Dim TargetVersion = If(PageCurrent = PageType.VersionSetup, PageVersionLeft.Version, McVersionCurrent)
                        If TargetVersion Is Nothing Then
                            Hint("请先下载游戏，再设置第三方登录！", HintType.Critical)
                            Return
                        End If
                        If AuthlibServer = "https://littleskin.cn/api/yggdrasil" Then
                            'LittleSkin
                            If MyMsgBox($"是否要在版本 {TargetVersion.Name} 中开启 LittleSkin 登录？" & vbCrLf &
                                        "你可以在 版本设置 → 设置 → 服务器选项 中修改登录方式。", "第三方登录开启确认", "确定", "取消") = 2 Then
                                Return
                            End If
                            Setup.Set("VersionServerLogin", 4, Version:=TargetVersion)
                            Setup.Set("VersionServerAuthServer", "https://littleskin.cn/api/yggdrasil", Version:=TargetVersion)
                            Setup.Set("VersionServerAuthRegister", "https://littleskin.cn/auth/register", Version:=TargetVersion)
                            Setup.Set("VersionServerAuthName", "LittleSkin 登录", Version:=TargetVersion)
                        Else
                            '第三方 Authlib 服务器
                            If MyMsgBox($"是否要在版本 {TargetVersion.Name} 中开启第三方登录？" & vbCrLf &
                                        $"登录服务器：{AuthlibServer}" & vbCrLf & vbCrLf &
                                        "你可以在 版本设置 → 设置 → 服务器选项 中修改登录方式。", "第三方登录开启确认", "确定", "取消") = 2 Then
                                Return
                            End If
                            Setup.Set("VersionServerLogin", 4, Version:=TargetVersion)
                            Setup.Set("VersionServerAuthServer", AuthlibServer, Version:=TargetVersion)
                            Setup.Set("VersionServerAuthRegister", AuthlibServer.Replace("api/yggdrasil", "auth/register"), Version:=TargetVersion)
                            Setup.Set("VersionServerAuthName", "", Version:=TargetVersion)
                        End If
                        If PageCurrent = PageType.VersionSetup AndAlso PageCurrentSub = PageSubType.VersionSetup Then
                            '正在服务器选项页，需要刷新设置项显示
                            FrmVersionSetup.Reload()
                        ElseIf PageCurrent = PageType.Launch Then
                            '正在主页，需要刷新左边栏
                            FrmLaunchLeft.RefreshPage(True, False)
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
                '必须要求全部为 jar 文件
                For Each File In FilePathList
                    If Not {"jar", "litemod", "disabled", "old"}.Contains(File.AfterLast(".").ToLower) Then
                        Hint("一次请只拖入一个文件！", HintType.Critical)
                        Return
                    End If
                Next
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
            If PageVersionMod.InstallMods(FilePathList) Then Return
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
            'RAR 处理
            If Extension = "rar" Then
                Hint("PCL 无法处理 rar 格式的压缩包，请在解压后重新压缩为 zip 格式再试！")
                Return
            End If
            '错误报告分析
            Try
                Log("[System] 尝试进行错误报告分析")
                Dim Analyzer As New CrashAnalyzer
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
        ''' 版本选择。这是一个副页面。
        ''' </summary>
        VersionSelect = 5
        ''' <summary>
        ''' 下载管理。这是一个副页面。
        ''' </summary>
        DownloadManager = 6
        ''' <summary>
        ''' 版本设置。这是一个副页面。
        ''' </summary>
        VersionSetup = 7
        ''' <summary>
        ''' 资源工程详情。这是一个副页面。
        ''' </summary>
        CompDetail = 8
        ''' <summary>
        ''' 帮助详情。这是一个副页面。
        ''' </summary>
        HelpDetail = 9
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
        DownloadFabric = 8
        DownloadLiteLoader = 9
        DownloadMod = 11
        DownloadPack = 12
        DownloadDataPack = 13
        DownloadResourcePack = 14
        DownloadShader = 15
        SetupLaunch = 0
        SetupUI = 1
        SetupSystem = 2
        SetupLink = 3
        LinkHiper = 1
        LinkIoi = 2
        LinkSetup = 4
        LinkHelp = 5
        LinkFeedback = 6
        OtherHelp = 0
        OtherAbout = 1
        OtherTest = 2
        VersionOverall = 0
        VersionSetup = 1
        VersionMod = 2
        VersionModDisabled = 3
        VersionExport = 4
    End Enum
    ''' <summary>
    ''' 获取次级页面的名称。若并非次级页面则返回空字符串，故可以以此判断是否为次级页面。
    ''' </summary>
    Private Function PageNameGet(Stack As PageStackData) As String
        Select Case Stack.Page
            Case PageType.VersionSelect
                Return "版本选择"
            Case PageType.DownloadManager
                Return "下载管理"
            Case PageType.VersionSetup
                Return "版本设置 - " & If(PageVersionLeft.Version Is Nothing, "未知版本", PageVersionLeft.Version.Name)
            Case PageType.CompDetail
                Return "资源下载 - " & CType(Stack.Additional(0), CompProject).TranslatedName
            Case PageType.HelpDetail
                Return CType(Stack.Additional(0), HelpEntry).Title
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
                Case PageType.VersionSetup
                    If FrmVersionLeft Is Nothing Then FrmVersionLeft = New PageVersionLeft
                    Return FrmVersionLeft.PageID
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
                    CType(FrmDownloadLeft.PanItem.Children(SubType), MyListItem).SetChecked(True, True, Stack = PageCurrent)
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
                Case PageType.VersionSetup
                    If FrmVersionLeft Is Nothing Then FrmVersionLeft = New PageVersionLeft
                    CType(FrmVersionLeft.PanItem.Children(SubType), MyListItem).SetChecked(True, True, Stack = PageCurrent)
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
                Case PageType.Other '更多
                    If FrmOtherLeft Is Nothing Then FrmOtherLeft = New PageOtherLeft
                    PageChangeAnim(FrmOtherLeft, FrmOtherLeft.PageGet(SubType))
                Case PageType.VersionSelect '版本选择
                    If FrmSelectLeft Is Nothing Then FrmSelectLeft = New PageSelectLeft
                    If FrmSelectRight Is Nothing Then FrmSelectRight = New PageSelectRight
                    PageChangeAnim(FrmSelectLeft, FrmSelectRight)
                Case PageType.DownloadManager '下载管理
                    If FrmSpeedLeft Is Nothing Then FrmSpeedLeft = New PageSpeedLeft
                    If FrmSpeedRight Is Nothing Then FrmSpeedRight = New PageSpeedRight
                    PageChangeAnim(FrmSpeedLeft, FrmSpeedRight)
                Case PageType.VersionSetup '版本设置
                    If FrmVersionLeft Is Nothing Then FrmVersionLeft = New PageVersionLeft
                    PageChangeAnim(FrmVersionLeft, FrmVersionLeft.PageGet(SubType))
                Case PageType.CompDetail 'Mod 信息
                    If FrmDownloadCompDetail Is Nothing Then FrmDownloadCompDetail = New PageDownloadCompDetail
                    PageChangeAnim(New MyPageLeft, FrmDownloadCompDetail)
                Case PageType.HelpDetail '帮助详情
                    PageChangeAnim(New MyPageLeft, Stack.Additional(1))
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

    '下载管理
    Private Sub BtnExtraDownload_Click(sender As Object, e As EventArgs) Handles BtnExtraDownload.Click
        PageChange(PageType.DownloadManager)
    End Sub
    Private Function BtnExtraDownload_ShowCheck() As Boolean
        Return HasDownloadingTask() AndAlso Not PageCurrent = PageType.DownloadManager
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
