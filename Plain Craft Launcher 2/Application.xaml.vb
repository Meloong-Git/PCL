Imports System.Reflection
Imports System.Windows.Threading

Public Class Application

#If DEBUG Then
    ''' <summary>
    ''' 用于开始程序时的一些测试。
    ''' </summary>
    Private Sub Test()
        Try
            ModDevelop.Start()
        Catch ex As Exception
            Log(ex, "开发者模式测试出错", LogLevel.Msgbox)
        End Try
    End Sub
#End If

    '开始
    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            SecretOnApplicationStart()
            '检查参数调用
            If e.Args.Length > 0 Then
                If e.Args(0) = "--update" Then
                    '自动更新
                    UpdateReplace(e.Args(1), e.Args(2).Trim(""""), e.Args(3).Trim(""""), e.Args(4))
                    Environment.Exit(ProcessReturnValues.TaskDone)
                ElseIf e.Args(0) = "--gpu" Then
                    '调整显卡设置
                    Try
                        SetGPUPreference(e.Args(1).Trim(""""))
                        Environment.Exit(ProcessReturnValues.TaskDone)
                    Catch ex As Exception
                        Environment.Exit(ProcessReturnValues.Fail)
                    End Try
                ElseIf e.Args(0).StartsWithF("--memory") Then
                    '内存优化
                    Dim Ram = My.Computer.Info.AvailablePhysicalMemory
                    Try
                        PageOtherTest.MemoryOptimizeInternal(False)
                    Catch ex As Exception
                        MsgBox(ex.Message, MsgBoxStyle.Critical, "内存优化失败")
                        Environment.Exit(-1)
                    End Try
                    If My.Computer.Info.AvailablePhysicalMemory < Ram Then '避免 ULong 相减出现负数
                        Environment.Exit(0)
                    Else
                        Environment.Exit((My.Computer.Info.AvailablePhysicalMemory - Ram) / 1024) '返回清理的内存量（K）
                    End If
#If DEBUG Then
                    '制作更新包
                ElseIf e.Args(0) = "--edit1" Then
                    ExeEdit(e.Args(1), True)
                    Environment.Exit(ProcessReturnValues.TaskDone)
                ElseIf e.Args(0) = "--edit2" Then
                    ExeEdit(e.Args(1), False)
                    Environment.Exit(ProcessReturnValues.TaskDone)
#End If
                End If
            End If
            '初始化文件结构
            Directory.CreateDirectory(Path & "PCL\Pictures")
            Directory.CreateDirectory(Path & "PCL\Musics")
            Try
                Directory.CreateDirectory(PathTemp)
                If Not CheckPermission(PathTemp) Then Throw New Exception("PCL 没有对 " & PathTemp & " 的访问权限")
            Catch ex As Exception
                If PathTemp = IO.Path.GetTempPath() & "PCL\" Then
                    MyMsgBox("PCL 无法访问缓存文件夹，可能导致程序出错或无法正常使用！" & vbCrLf & "错误原因：" & GetExceptionDetail(ex), "缓存文件夹不可用")
                Else
                    MyMsgBox("手动设置的缓存文件夹不可用，PCL 将使用默认缓存文件夹。" & vbCrLf & "错误原因：" & GetExceptionDetail(ex), "缓存文件夹不可用")
                    Setup.Set("SystemSystemCache", "")
                    PathTemp = IO.Path.GetTempPath() & "PCL\"
                End If
            End Try
            Directory.CreateDirectory(PathTemp & "Cache")
            Directory.CreateDirectory(PathTemp & "Download")
            Directory.CreateDirectory(PathAppdata)
            '检测单例
#If Not DEBUG Then
            Dim ShouldWaitForExit As Boolean = e.Args.Length > 0 AndAlso e.Args(0) = "--wait" '要求等待已有的 PCL 退出
            Dim WaitRetryCount As Integer = 0
WaitRetry:
            Dim WindowHwnd As IntPtr = FindWindow(Nothing, "Plain Craft Launcher　")
            If WindowHwnd = IntPtr.Zero Then FindWindow(Nothing, "Plain Craft Launcher 2　")
            If WindowHwnd <> IntPtr.Zero Then
                If ShouldWaitForExit AndAlso WaitRetryCount < 20 Then '至多等待 10 秒
                    WaitRetryCount += 1
                    Thread.Sleep(500)
                    GoTo WaitRetry
                End If
                '将已有的 PCL 窗口拖出来
                ShowWindowToTop(WindowHwnd)
                '播放提示音并退出
                Beep()
                Environment.[Exit](ProcessReturnValues.Cancel)
            End If
#End If
            '设置 ToolTipService 默认值
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(300))
            ToolTipService.BetweenShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(400))
            ToolTipService.ShowDurationProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(9999999))
            ToolTipService.PlacementProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(Primitives.PlacementMode.Bottom))
            ToolTipService.HorizontalOffsetProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(8.0))
            ToolTipService.VerticalOffsetProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(4.0))
            '设置初始窗口
            If Setup.Get("UiLauncherLogo") Then
                FrmStart = New SplashScreen("Images\icon.ico")
                FrmStart.Show(False, True)
            End If
            '动态 DLL 调用
            AddHandler AppDomain.CurrentDomain.AssemblyResolve, AddressOf AssemblyResolve
            '日志初始化
            LogStart()
            '添加日志
            Log($"[Start] 程序版本：{VersionDisplayName} ({VersionCode}{If(CommitHash = "", "", $"，#{CommitHash}")})")
#If RELEASE Then
            Log($"[Start] 识别码：{UniqueAddress}{If(ThemeCheckOne(9), "，正式版", "")}")
#Else
            Log($"[Start] 识别码：{UniqueAddress}{If(ThemeCheckOne(9), "，已解锁反馈主题", "")}")
#End If
            Log($"[Start] 程序路径：{PathWithName}")
            Log($"[Start] 系统编码：{Encoding.Default.HeaderName} ({Encoding.Default.CodePage}, GBK={IsGBKEncoding})")
            Log($"[Start] 管理员权限：{IsAdmin()}")
            '检测异常环境
            If Path.Contains(IO.Path.GetTempPath()) OrElse Path.Contains("AppData\Local\Temp\") Then
                MyMsgBox("请将 PCL 从压缩包中解压之后再使用！" & vbCrLf & "在当前环境下运行可能会导致丢失游戏存档或设置，部分功能也可能无法使用！", "环境警告", "我知道了", IsWarn:=True)
            End If
            If Is32BitSystem Then
                MyMsgBox("PCL 和新版 Minecraft 均不再支持 32 位系统，部分功能将无法使用。" & vbCrLf & "非常建议重装为 64 位系统后再进行游戏！", "环境警告", "我知道了", IsWarn:=True)
            End If
            '设置初始化
            Setup.Load("SystemDebugMode")
            Setup.Load("SystemDebugAnim")
            Setup.Load("ToolDownloadThread")
            Setup.Load("ToolDownloadCert")
            Setup.Load("ToolDownloadSpeed")
            '网络配置初始化
            ServicePointManager.Expect100Continue = True
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 Or SecurityProtocolType.Tls Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls12
            ServicePointManager.DefaultConnectionLimit = 1024
            '计时
            Log("[Start] 第一阶段加载用时：" & GetTimeTick() - ApplicationStartTick & " ms")
            ApplicationStartTick = GetTimeTick()
            '执行测试
#If DEBUG Then
            Test()
#End If
            AniControlEnabled += 1
        Catch ex As Exception
            Dim FilePath As String = Nothing
            Try
                FilePath = PathWithName
            Catch
            End Try
            MsgBox(GetExceptionDetail(ex, True) & vbCrLf & "PCL 所在路径：" & If(String.IsNullOrEmpty(FilePath), "获取失败", FilePath), MsgBoxStyle.Critical, "PCL 初始化错误")
            FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Try
    End Sub

    '结束
    Private Sub Application_SessionEnding(sender As Object, e As SessionEndingCancelEventArgs) Handles Me.SessionEnding
        FrmMain.EndProgram(False)
    End Sub

    '异常
    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs) Handles Me.DispatcherUnhandledException
        On Error Resume Next
        e.Handled = True
        If IsProgramEnded Then Return
        FeedbackInfo()
        Dim Detail As String = GetExceptionDetail(e.Exception, True)
        If Detail.Contains("System.Windows.Threading.Dispatcher.Invoke") OrElse Detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") OrElse Detail.Contains("未能加载文件或程序集") OrElse
           Detail.Contains(".NET Framework") Then ' “自动错误判断” 的结果分析
            OpenWebsite("https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net462-offline-installer")
            Log(e.Exception, "你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.6.2！" & vbCrLf & "若无法安装，可在卸载高版本的 .NET Framework 后再试。", LogLevel.Critical, "运行环境错误")
        Else
            Log(e.Exception, "程序出现未知错误", LogLevel.Critical, "锟斤拷烫烫烫")
        End If
    End Sub

    '动态 DLL 调用
    Private Shared AssemblyNAudio As Assembly
    Private Shared AssemblyJson As Assembly
    Private Shared AssemblyDialog As Assembly
    Private Shared AssemblyImazenWebp As Assembly
    Private Shared ReadOnly AssemblyNAudioLock As New Object
    Private Shared ReadOnly AssemblyJsonLock As New Object
    Private Shared ReadOnly AssemblyDialogLock As New Object
    Private Shared ReadOnly AssemblyImazenWebpLock As New Object
    Private Declare Function SetDllDirectory Lib "kernel32" Alias "SetDllDirectoryA" (lpPathName As String) As Boolean
    Public Shared Function AssemblyResolve(sender As Object, args As ResolveEventArgs) As Assembly
        If args.Name.StartsWithF("NAudio") Then
            SyncLock AssemblyNAudioLock
                If AssemblyNAudio Is Nothing Then
                    Log("[Start] 加载 DLL：NAudio")
                    AssemblyNAudio = Assembly.Load(GetResources("NAudio"))
                End If
                Return AssemblyNAudio
            End SyncLock
        ElseIf args.Name.StartsWithF("Newtonsoft.Json") Then
            SyncLock AssemblyJsonLock
                If AssemblyJson Is Nothing Then
                    Log("[Start] 加载 DLL：Json")
                    AssemblyJson = Assembly.Load(GetResources("Json"))
                End If
                Return AssemblyJson
            End SyncLock
        ElseIf args.Name.StartsWithF("Ookii.Dialogs.Wpf") Then
            SyncLock AssemblyDialogLock
                If AssemblyDialog Is Nothing Then
                    Log("[Start] 加载 DLL：Dialogs")
                    AssemblyDialog = Assembly.Load(GetResources("Dialogs"))
                End If
                Return AssemblyDialog
            End SyncLock
        ElseIf args.Name.StartsWithF("Imazen.WebP") Then
            SyncLock AssemblyImazenWebpLock
                If AssemblyImazenWebp Is Nothing Then
                    Log("[Start] 加载 DLL：Imazen.WebP")
                    AssemblyImazenWebp = Assembly.Load(GetResources("Imazen_WebP"))
                    SetDllDirectory(PathPure.TrimEnd("\"))
                    WriteFile(PathPure & "libwebp.dll", GetResources("libwebp64"))
                End If
                Return AssemblyImazenWebp
            End SyncLock
        Else
            Return Nothing
        End If
    End Function

    '切换窗口

    '控件模板事件
    Private Sub MyIconButton_Click(sender As Object, e As EventArgs)
        Select Case Setup.Get("LoginType")
            Case McLoginType.Ms
                '微软
                Dim MsJson As JObject = GetJson(Setup.Get("LoginMsJson"))
                MsJson.Remove(sender.Tag)
                Setup.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None))
                If FrmLoginMs.ComboAccounts.SelectedItem Is sender.Parent Then FrmLoginMs.ComboAccounts.SelectedIndex = 0
                FrmLoginMs.ComboAccounts.Items.Remove(sender.Parent)
            Case McLoginType.Legacy
                '离线
                Dim Names As New List(Of String)
                Names.AddRange(Setup.Get("LoginLegacyName").ToString.Split("¨"))
                Names.Remove(sender.Tag)
                Setup.Set("LoginLegacyName", Join(Names, "¨"))
                FrmLoginLegacy.ComboName.ItemsSource = Names
                FrmLoginLegacy.ComboName.Text = If(Names.Any, Names(0), "")
            Case Else
                '第三方
                Dim Token As String = GetStringFromEnum(Setup.Get("LoginType"))
                Dim Dict As New Dictionary(Of String, String)
                Dim Names As New List(Of String)
                Dim Passs As New List(Of String)
                If Not Setup.Get("Login" & Token & "Email") = "" Then Names.AddRange(Setup.Get("Login" & Token & "Email").ToString.Split("¨"))
                If Not Setup.Get("Login" & Token & "Pass") = "" Then Passs.AddRange(Setup.Get("Login" & Token & "Pass").ToString.Split("¨"))
                For i = 0 To Names.Count - 1
                    Dict.Add(Names(i), Passs(i))
                Next
                Dict.Remove(sender.Tag)
                Setup.Set("Login" & Token & "Email", Join(Dict.Keys, "¨"))
                Setup.Set("Login" & Token & "Pass", Join(Dict.Values, "¨"))
                Select Case Token
                    Case "Nide"
                        FrmLoginNide.ComboName.ItemsSource = Dict.Keys
                        FrmLoginNide.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginNide.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                    Case "Auth"
                        FrmLoginAuth.ComboName.ItemsSource = Dict.Keys
                        FrmLoginAuth.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginAuth.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                End Select
        End Select
    End Sub

    Public Shared ShowingTooltips As New List(Of Border)
    Private Sub TooltipLoaded(sender As Border, e As EventArgs)
        ShowingTooltips.Add(sender)
    End Sub
    Private Sub TooltipUnloaded(sender As Border, e As RoutedEventArgs)
        ShowingTooltips.Remove(sender)
    End Sub

End Class
