Imports System.Reflection
Imports System.Windows.Threading
Imports Microsoft.Win32

Public Class Application

    '开始
    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            '核心初始化
            MeloongCore.Main.Init("PCL")
            MeloongCore.Wpf.Main.Init()
            Logger.Instance = New PclLogger With {.logFolder = Paths.BaseThenName, .MinLevel = If(ModeDebug, LogLevel.Trace, LogLevel.Info)}
            '同步语言到新配置系统
            Settings.Set("SystemLang", Lang)
            '刷新语言
            Try
                Application.Current.Resources.MergedDictionaries(1) = New ResourceDictionary With {.Source = New Uri("pack://application:,,,/Resources/Language/" & Lang & ".xaml", UriKind.RelativeOrAbsolute)}
            Catch ex As Exception
                MsgBox("无法找到语言资源：" & Lang & vbCrLf & "Language resource cannot be found:" & Lang, MsgBoxStyle.Critical)
                Lang = GetDefaultLang()
            End Try

            '依照选择语言切换字体
            Dim LaunchFont As FontFamily
            Select Case Lang
                Case "zh-TW", "zh-HK", "lzh", "zh-MARS"
                    LaunchFont = New FontFamily(New Uri("pack://application:,,,/"), "./Resources/#PCL English, Segoe UI, Microsoft JhengHei UI")
                Case "ja-JP"
                    LaunchFont = New FontFamily(New Uri("pack://application:,,,/"), "./Resources/#PCL English, Segoe UI, Yu Gothic UI, Microsoft YaHei UI")
                Case "ko-KR"
                    LaunchFont = New FontFamily(New Uri("pack://application:,,,/"), "./Resources/#PCL English, Segoe UI, Malgun Gothic, Microsoft YaHei UI")
                Case "en-US", "en-GB", "zh-CN", "zh-MEME"
                    LaunchFont = New FontFamily(New Uri("pack://application:,,,/"), "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI")
                Case Else '非英语的其他西欧语言统一使用 Segoe UI
                    LaunchFont = New FontFamily(New Uri("pack://application:,,,/"), "Segoe UI, ./Resources/#PCL English, Microsoft YaHei UI")
            End Select
            SwitchApplicationFont(LaunchFont)
            '提升主线程优先级
            Thread.CurrentThread.Priority = ThreadPriority.Highest
            '执行开发版测试
            If BuildType = BuildTypes.Debug Then
                Try
                    ModDevelop.Start()
                Catch ex As Exception
                    Logger.Error(ex, "开发者模式测试出错", LogBehavior.Alert)
                End Try
            End If
            '检查 .NET Framework 版本
            Try
                Using ndpKey As RegistryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\")
                    If ndpKey?.GetValue("Release") IsNot Nothing AndAlso CInt(ndpKey.GetValue("Release")) < 528040 Then OldEnvironmentAssert()
                End Using
            Catch ex As Exception
                Logger.Warn(ex, "检查 .NET Framework 版本失败")
            End Try
            '修复 WPF 字体加载（#3467）
            Try
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
            Catch ex As UriFormatException
                Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User)
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
            End Try
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
                        MsgBox(ex.Message, MsgBoxStyle.Critical, GetLang("LangApplicationDialogContentMemReduceFail"))
                        Environment.Exit(-1)
                    End Try
                    If My.Computer.Info.AvailablePhysicalMemory < Ram Then '避免 ULong 相减出现负数
                        Environment.Exit(0)
                    Else
                        Environment.Exit((My.Computer.Info.AvailablePhysicalMemory - Ram) / 1024) '返回清理的内存量（K）
                    End If
                End If
            End If
            '初始化文件结构
            Try
                DirectoryUtils.Create(Paths.Base & "PCL\Pictures\")
                DirectoryUtils.Create(Paths.Base & "PCL\Musics\")
                CheckPermissionWithException(Paths.Base & "PCL\")
            Catch ex As Exception
                MsgBox(GetLang("LangApplicationDialogContentNoPermission", Paths.Base,
                                If(Paths.Base.StartsWithF("C:", True), GetLang("LangApplicationDialogNoPermissionMoveSuffixC"), GetLang("LangApplicationDialogNoPermissionMoveSuffixDefault"))),
                       MsgBoxStyle.Critical, GetLang("LangModSecretPermissionError"))
                Environment.[Exit](ProcessReturnValues.Cancel)
            End Try
RetryCacheCheck:
            Try
                DirectoryUtils.Create(PathTemp)
                CheckPermissionWithException(PathTemp)
            Catch ex As Exception
                If PathTemp = Path.GetTempPath() & "PCL\" Then
                    MyMsgBox(GetLang("LangApplicationDialogContentCacheFolderUnavailable", ex.GetDisplay(True)), GetLang("LangApplicationDialogTitleCacheFolderUnavailable"))
                Else
                    MyMsgBox(GetLang("LangApplicationDialogContentCustomCacheFolderUnavailable", ex.GetDisplay(True)), GetLang("LangApplicationDialogTitleCacheFolderUnavailable"))
                    Settings.Set("SystemSystemCache", "")
                    PathTemp = Path.GetTempPath() & "PCL\"
                    GoTo RetryCacheCheck
                End If
            End Try
            DirectoryUtils.Create(PathTemp & "Cache\")
            DirectoryUtils.Create(Paths.AppDataThenName)
            '要求单例
            WaitingMutex(e)
            '设置 ToolTipService 默认值
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(100))
            '设置网络配置默认值
            ServicePointManager.Expect100Continue = False
            ServicePointManager.DefaultConnectionLimit = 10000
            ServicePointManager.UseNagleAlgorithm = False
            ServicePointManager.EnableDnsRoundRobin = True
            ServicePointManager.ReusePort = True
            If Environment.OSVersion.Version < New Version(6, 2) Then 'Windows 7 回退（#8519）
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 Or SecurityProtocolType.Tls Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls12
            End If
            '设置初始窗口
            If Settings.Get(Of Boolean)("UiLauncherLogo") Then
                FrmStart = New SplashScreen("Images\icon.ico")
                FrmStart.Show(False, True)
            End If
            '基础信息
            Logger.Info($"程序版本：{VersionDisplay} ({VersionCode}{If(CommitHash = "", "", $"，#{CommitHash}")})")
            If BuildType = BuildTypes.Snapshot Then
                Logger.Info($"识别码：{Identify}{If(ThemeCheckOne(9), "，已解锁反馈主题", "，未解锁反馈主题")}")
            Else
                Logger.Info($"识别码：{Identify}")
            End If
            Logger.Info($"程序路径：{PathExe}")
            Logger.Info($"系统编码：{Encoding.Default.HeaderName} ({Encoding.Default.CodePage}, GBK={IsGBKEncoding})")
            Logger.Info($"管理员权限：{WindowsUtils.HasAdminRole()}")
            Logger.Info("[Location] 启动器语言：" & Lang)
            Logger.Info("[Location] 当前系统环境是否为中国大陆：" & IsLocationZH())
            '检测异常环境
            If Paths.Base.Contains(Path.GetTempPath()) OrElse Paths.Base.Contains("AppData\Local\Temp\") Then
                MyMsgBox(GetLang("LangApplicationDialogContentUnzipLauncher"), GetLang("LangApplicationDialogTitleUnzipLauncher"), GetLang("LangDialogBtnOK"), IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            If Not Environment.Is64BitOperatingSystem Then
                MyMsgBox(GetLang("LangApplicationDialogContent32BitWarn"), GetLang("LangApplicationDialogTitle32BitWarn"), GetLang("LangDialogBtnOK"), IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            SetUnmanagedDll()
            '计时
            Logger.Info($"第一阶段加载用时：{GetTimeMs() - ApplicationStartTick} ms")
            ApplicationStartTick = GetTimeMs()
            AniControlEnabled += 1
        Catch ex As Exception
            Dim FilePath As String = Nothing
            Try
                FilePath = PathExe
            Catch
            End Try
            MsgBox(ex.GetDisplay(True) & vbCrLf & GetLang("LangApplicationDialogPathPrefix") & If(String.IsNullOrEmpty(FilePath), GetLang("LangApplicationDialogPathGetFail"), FilePath), MsgBoxStyle.Critical, GetLang("LangApplicationDialogTitleInitError"))
            FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Try
    End Sub
    ''' <summary>
    ''' 要求程序以单例运行。
    ''' 如果已在运行，则将其窗口拖出来并播放提示音，然后结束当前进程。
    ''' </summary>
    Private Shared Sub WaitingMutex(e As StartupEventArgs)
        If BuildType = BuildTypes.Debug Then Return
        Try
            Dim MutexCreatedNew As Boolean
            PclMutex = New Mutex(True, "PCL_SingletonMutex", MutexCreatedNew)
            If MutexCreatedNew Then Return
            Logger.Warn("已有一个 PCL 实例正在运行")
            '等待已有的 PCL 退出
            If e.Args.Length > 0 AndAlso e.Args(0) = "--wait" Then
                Try
                    If PclMutex.WaitOne(10000) Then Return '等待至多 10 秒
                Catch Ignored As AbandonedMutexException
                    Return '已有实例异常退出，继续启动
                End Try
            End If
            '将已有的 PCL 窗口拖出来
            Dim WindowHwnd As IntPtr = FindWindow(Nothing, "Plain Craft Launcher　")
            If WindowHwnd = IntPtr.Zero Then WindowHwnd = FindWindow(Nothing, "Plain Craft Launcher 2　")
            If WindowHwnd <> IntPtr.Zero Then ShowWindowToTop(WindowHwnd)
            '播放提示音并退出
            Beep()
            Environment.[Exit](ProcessReturnValues.Cancel)
        Catch ex As Exception
            Logger.Warn(ex, "单例检查失败")
        End Try
    End Sub
    Private Shared PclMutex As Mutex

    '结束
    Private Sub Application_SessionEnding(sender As Object, e As SessionEndingCancelEventArgs) Handles Me.SessionEnding
        FrmMain?.EndProgram(False)
    End Sub

    '异常
    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs) Handles Me.DispatcherUnhandledException
        On Error Resume Next
        '触发页面的 Dispatcher
        If FrmMain?.PageLeft IsNot Nothing AndAlso TypeOf FrmMain.PageLeft Is IDispatcherUnhandledException Then
            CType(FrmMain.PageLeft, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        If FrmMain?.PageRight IsNot Nothing AndAlso TypeOf FrmMain.PageRight Is IDispatcherUnhandledException Then
            CType(FrmMain.PageRight, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        '正常处理
        e.Handled = True
        If IsProgramEnding Then Return
        FeedbackInfo()
        Dim Detail As String = e.Exception.GetDisplay(True)
        If Detail.Contains("System.Windows.Threading.Dispatcher.Invoke") OrElse Detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") OrElse Detail.Contains("未能加载文件或程序集") OrElse
           Detail.Contains(".NET Framework") Then ' “自动错误判断” 的结果分析
            OldEnvironmentAssert(e.Exception)
        Else
            Logger.Error(e.Exception, "程序出现未知错误", LogBehavior.AlertThenCrash)
        End If
    End Sub
    Private Shared Sub OldEnvironmentAssert(Optional ex As Exception = Nothing)
        OpenWebsite("https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net48-offline-installer")
        If ex Is Nothing Then
            Logger.Error($"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        Else
            Logger.Error(ex, $"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        End If
    End Sub

    Private Declare Function SetDllDirectory Lib "kernel32" Alias "SetDllDirectoryA" (lpPathName As String) As Boolean
    Private Shared Sub SetUnmanagedDll()
        SetDllDirectory(PathPure.TrimEnd("\"c))
        Try
            ExtractResources(PathPure & "libwebp.dll", "libwebp64")
        Catch ex As Exception
            Logger.Warn(ex, "写入 libwebp.dll 失败") '防止同时加载多个图片时，同时写入文件导致文件占用，进而导致崩溃
        End Try
    End Sub

    '切换窗口

    '控件模板事件
    Private Sub MyIconButton_Click(sender As Object, e As EventArgs)
        Select Case Settings.Get(Of McLoginType)("LoginType")
            Case McLoginType.Ms
                '微软
                Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
                MsJson.Remove(sender.Tag)
                Settings.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None))
                If FrmLoginMs.ComboAccounts.SelectedItem Is sender.Parent Then FrmLoginMs.ComboAccounts.SelectedIndex = 0
                FrmLoginMs.ComboAccounts.Items.Remove(sender.Parent)
            Case McLoginType.Legacy
                '离线
                Dim Names As New List(Of String)
                Names.AddRange(Settings.Get(Of String)("LoginLegacyName").ToString.Split("¨"))
                Names.Remove(sender.Tag)
                Settings.Set("LoginLegacyName", Names.Join("¨"c))
                FrmLoginLegacy.ComboName.ItemsSource = Names
                FrmLoginLegacy.ComboName.Text = If(Names.Any, Names(0), "")
            Case Else
                '第三方
                Dim Token As String = Settings.Get(Of McLoginType)("LoginType").ToString()
                Dim Dict As New Dictionary(Of String, String)
                Dim Names As New List(Of String)
                Dim Passs As New List(Of String)
                If Not Settings.Get(Of String)("Login" & Token & "Email") = "" Then Names.AddRange(Settings.Get(Of String)("Login" & Token & "Email").Split("¨"))
                If Not Settings.Get(Of String)("Login" & Token & "Pass") = "" Then Passs.AddRange(Settings.Get(Of String)("Login" & Token & "Pass").Split("¨"))
                For i = 0 To Names.Count - 1
                    Dict.Add(Names(i), Passs(i))
                Next
                Dict.Remove(sender.Tag)
                Settings.Set("Login" & Token & "Email", Dict.Keys.Join("¨"c))
                Settings.Set("Login" & Token & "Pass", Dict.Values.Join("¨"c))
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

    Public Shared ShowingTooltips As New List(Of ToolTip)
    Private Sub OnTooltipOpened(sender As ToolTip, e As RoutedEventArgs)
        ShowingTooltips.Add(sender)
    End Sub
    Private Sub OnTooltipClosed(sender As ToolTip, e As RoutedEventArgs)
        ShowingTooltips.Remove(sender)
    End Sub

End Class
