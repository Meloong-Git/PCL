
Imports System.IO.Compression
Imports System.Text.Json.Nodes
Imports PCL.Core.Minecraft
Imports PCL.Core.Utils

Public Module ModLaunch

#Region "开始"

    Public IsLaunching As Boolean = False
    Public CurrentLaunchOptions As McLaunchOptions = Nothing
    Public Class McLaunchOptions
        ''' <summary>
        ''' 强制指定在启动后进入的服务器 IP。
        ''' 默认值：Nothing。使用实例设置的值。
        ''' </summary>
        Public ServerIp As String = Nothing
        ''' <summary>
        ''' 指定在启动之后进入的存档名称。
        ''' 默认值：Nothing。使用实例设置的值。
        ''' </summary>
        Public WorldName As String = Nothing
        ''' <summary>
        ''' 将启动脚本保存到该地址，然后取消启动。这同时会改变启动时的提示等。
        ''' 默认值：Nothing。不保存。
        ''' </summary>
        Public SaveBatch As String = Nothing
        ''' <summary>
        ''' 强行指定启动的 MC 实例。
        ''' 默认值：Nothing。使用 McInstanceCurrent。
        ''' </summary>
        Public Version As McInstance = Nothing
        ''' <summary>
        ''' 额外的启动参数。
        ''' </summary>
        Public ExtraArgs As New List(Of String)
        ''' <summary>
        ''' 是否为 “测试游戏” 按钮启动的游戏。
        ''' 如果是，则显示游戏实时日志。
        ''' </summary>
        Public Test As Boolean = False
    End Class
    ''' <summary>
    ''' 尝试启动 Minecraft。必须在 UI 线程调用。
    ''' 返回是否实际开始了启动（如果没有，则一定弹出了错误提示）。
    ''' </summary>
    Public Function McLaunchStart(Optional Options As McLaunchOptions = Nothing) As Boolean
        IsLaunching = True
        CurrentLaunchOptions = If(Options, New McLaunchOptions)
        '预检查
        If Not RunInUi() Then Throw New Exception("McLaunchStart 必须在 UI 线程调用！")
        If McLaunchLoader.State = LoadState.Loading Then
            Hint("已有游戏正在启动中！", HintType.Critical)
            IsLaunching = False
            Return False
        End If
        '强制切换需要启动的实例
        If CurrentLaunchOptions.Version IsNot Nothing AndAlso McInstanceCurrent <> CurrentLaunchOptions.Version Then
            McLaunchLog("在启动前切换到实例 " & CurrentLaunchOptions.Version.Name)
            '检查实例
            CurrentLaunchOptions.Version.Load()
            If CurrentLaunchOptions.Version.State = McInstanceState.Error Then
                Hint("无法启动 Minecraft：" & CurrentLaunchOptions.Version.Info, HintType.Critical)
                IsLaunching = False
                Return False
            End If
            '切换实例
            McInstanceCurrent = CurrentLaunchOptions.Version
            Setup.Set("LaunchInstanceSelect", McInstanceCurrent.Name)
            FrmLaunchLeft.RefreshButtonsUI()
            FrmLaunchLeft.RefreshPage(False)
        End If
        FrmMain.AprilGiveup()
        '禁止进入实例选择页面（否则就可以在启动中切换 McInstanceCurrent 了）
        FrmMain.PageStack = FrmMain.PageStack.Where(Function(p) p.Page <> FormMain.PageType.InstanceSelect).ToList
        '实际启动加载器
        McLaunchLoader.Start(Options, IsForceRestart:=True)
        Return True
    End Function

    ''' <summary>
    ''' 记录启动日志。
    ''' </summary>
    Public Sub McLaunchLog(Text As String)
        Text = FilterUserName(FilterAccessToken(Text, "*"), "*")
        RunInUi(Sub() FrmLaunchRight.LabLog.Text += vbCrLf & "[" & TimeUtils.GetTimeNow() & "] " & Text)
        Log("[Launch] " & Text)
    End Sub

    '启动状态切换
    Public McLaunchLoader As New LoaderTask(Of McLaunchOptions, Object)("Loader Launch", AddressOf McLaunchStart) With {.OnStateChanged = AddressOf McLaunchState}
    Public McLaunchLoaderReal As LoaderCombo(Of Object)
    Public McLaunchProcess As Process
    Public McLaunchWatcher As Watcher
    Private Sub McLaunchState(Loader As LoaderTask(Of McLaunchOptions, Object))
        Select Case McLaunchLoader.State
            Case LoadState.Finished, LoadState.Failed, LoadState.Waiting, LoadState.Aborted
                FrmLaunchLeft.PageChangeToLogin()
            Case LoadState.Loading
                '在预检测结束后再触发动画
                FrmLaunchRight.LabLog.Text = ""
        End Select
    End Sub
    ''' <summary>
    ''' 指定启动中断时的提示文本。若不为 Nothing 则会显示为绿色。
    ''' </summary>
    Private AbortHint As String = Nothing

    '实际的启动方法
    Private Sub McLaunchStart(Loader As LoaderTask(Of McLaunchOptions, Object))
        '开始动画
        RunInUiWait(AddressOf FrmLaunchLeft.PageChangeToLaunching)
        '预检测（预检测的错误将直接抛出）
        Try
            McLaunchPrecheck()
            McLaunchLog("预检测已通过")
        Catch ex As Exception
            If Not ex.Message.StartsWithF("$$") Then Hint(ex.Message, HintType.Critical)
            Throw
        End Try
        '正式加载
        Try
            '构造主加载器
            Dim Loaders As New List(Of LoaderBase) From {
                New LoaderTask(Of Integer, Integer)("获取 Java", AddressOf McLaunchJava) With {.ProgressWeight = 4, .Block = False},
                McLoginLoader, '.ProgressWeight = 15, .Block = False
                New LoaderCombo(Of String)("补全文件", DlClientFix(McInstanceCurrent, False, AssetsIndexExistsBehaviour.DownloadInBackground)) With {.ProgressWeight = 15, .Show = False},
                New LoaderTask(Of String, List(Of McLibToken))("获取启动参数", AddressOf McLaunchArgumentMain) With {.ProgressWeight = 2},
                New LoaderTask(Of List(Of McLibToken), Integer)("解压文件", AddressOf McLaunchNatives) With {.ProgressWeight = 2},
                New LoaderTask(Of Integer, Integer)("预启动处理", AddressOf McLaunchPrerun) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Integer)("执行自定义命令", AddressOf McLaunchCustom) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Process)("启动进程", AddressOf McLaunchRun) With {.ProgressWeight = 2},
                New LoaderTask(Of Process, Integer)("等待游戏窗口出现", AddressOf McLaunchWait) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Integer)("结束处理", AddressOf McLaunchEnd) With {.ProgressWeight = 1}
            }
            '内存优化
            Select Case Setup.Get("VersionRamOptimize", instance:=McInstanceCurrent)
                Case 0 '全局
                    If Setup.Get("LaunchArgumentRam") Then '使用全局设置
                        CType(Loaders(2), LoaderCombo(Of String)).Block = False
                        Loaders.Insert(3, New LoaderTask(Of Integer, Integer)("内存优化", AddressOf McLaunchMemoryOptimize) With {.ProgressWeight = 30})
                    End If
                Case 1 '开启
                    CType(Loaders(2), LoaderCombo(Of String)).Block = False
                    Loaders.Insert(3, New LoaderTask(Of Integer, Integer)("内存优化", AddressOf McLaunchMemoryOptimize) With {.ProgressWeight = 30})
                Case 2 '关闭
            End Select
            Dim LaunchLoader As New LoaderCombo(Of Object)("Minecraft 启动", Loaders) With {.Show = False}
            If McLoginLoader.State = LoadState.Finished Then McLoginLoader.State = LoadState.Waiting '要求重启登录主加载器，它会自行决定是否启动副加载器
            '等待加载器执行并更新 UI
            McLaunchLoaderReal = LaunchLoader
            AbortHint = Nothing
            LaunchLoader.Start()
            '任务栏进度条
            LoaderTaskbarAdd(LaunchLoader)
            Do While LaunchLoader.State = LoadState.Loading
                FrmLaunchLeft.Dispatcher.Invoke(AddressOf FrmLaunchLeft.LaunchingRefresh)
                Thread.Sleep(200)
            Loop
            FrmLaunchLeft.Dispatcher.Invoke(AddressOf FrmLaunchLeft.LaunchingRefresh)
            '成功与失败处理
            Select Case LaunchLoader.State
                Case LoadState.Finished
                    Hint(McInstanceCurrent.Name & " 启动成功！", HintType.Finish)
                Case LoadState.Aborted
                    If AbortHint Is Nothing Then
                        Hint(If(CurrentLaunchOptions?.SaveBatch Is Nothing, "已取消启动！", "已取消导出启动脚本！"), HintType.Info)
                    Else
                        Hint(AbortHint, HintType.Finish)
                    End If
                Case LoadState.Failed
                    Throw LaunchLoader.Error
                Case Else
                    Throw New Exception("错误的状态改变：" & GetStringFromEnum(CType(LaunchLoader.State, [Enum])))
            End Select
            IsLaunching = False
        Catch ex As Exception
            Dim CurrentEx = ex
NextInner:
            If CurrentEx.Message.StartsWithF("$") Then
                '若有以 $ 开头的错误信息，则以此为准显示提示
                '若错误信息为 $$，则不提示
                If Not CurrentEx.Message = "$$" Then MyMsgBox(CurrentEx.Message.TrimStart("$"), If(CurrentLaunchOptions?.SaveBatch Is Nothing, "启动失败", "导出启动脚本失败"))
                Throw
            ElseIf CurrentEx.InnerException IsNot Nothing Then
                '检查下一级错误
                CurrentEx = CurrentEx.InnerException
                GoTo NextInner
            Else
                '没有特殊处理过的错误信息
                McLaunchLog("错误：" & GetExceptionDetail(ex))
                Log(ex,
                    If(CurrentLaunchOptions?.SaveBatch Is Nothing, "Minecraft 启动失败", "导出启动脚本失败"), LogLevel.Msgbox,
                    If(CurrentLaunchOptions?.SaveBatch Is Nothing, "启动失败", "导出启动脚本失败"))
                Throw
            End If
        End Try
    End Sub

#End Region

#Region "内存优化"

    Private Sub McLaunchMemoryOptimize(Loader As LoaderTask(Of Integer, Integer))
        McLaunchLog("内存优化开始")
        Dim Finished As Boolean = False
        RunInNewThread(
        Sub()
            PageOtherTest.MemoryOptimize(False)
            Finished = True
        End Sub, "Launch Memory Optimize")
        Do While Not Finished AndAlso Not Loader.IsAborted
            If Loader.Progress < 0.7 Then
                Loader.Progress += 0.007 '10s
            Else
                Loader.Progress += (0.95 - Loader.Progress) * 0.02 '最快 += 0.005
            End If
            Thread.Sleep(100)
        Loop
    End Sub

#End Region

#Region "预检测"

    Private Sub McLaunchPrecheck()
        If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomInteger(100, 2000))
        '检查路径
        If McInstanceCurrent.PathIndie.Contains("!") OrElse McInstanceCurrent.PathIndie.Contains(";") Then Throw New Exception("游戏路径中不可包含 ! 或 ;（" & McInstanceCurrent.PathIndie & "）")
        If McInstanceCurrent.Path.Contains("!") OrElse McInstanceCurrent.Path.Contains(";") Then Throw New Exception("游戏路径中不可包含 ! 或 ;（" & McInstanceCurrent.Path & "）")
        If IsUtf8CodePage() AndAlso Not Setup.Get("HintDisableGamePathCheckTip") AndAlso Not McInstanceCurrent.Path.IsASCII() Then
            Dim userChoice = MyMsgBox(
                $"欲启动实例 ""{McInstanceCurrent.Name}"" 的路径中存在可能影响游戏正常运行的字符（非 ASCII 字符），是否仍旧启动游戏？{vbCrLf}{vbCrLf}如果不清楚具体作用，你可以先选择 ""继续""，发现游戏在启动后很快出现崩溃的情况后再尝试修改游戏路径等操作",
                "游戏路径检查",
                "继续",
                "返回处理",
                "不再提示")
            If userChoice = 2 Then
                Throw New Exception("$$")
            End If
            If userChoice = 3 Then
                Setup.Set("HintDisableGamePathCheckTip", True)
            End If
        End If
        '检查实例
        If McInstanceCurrent Is Nothing Then Throw New Exception("未选择 Minecraft 实例！")
        McInstanceCurrent.Load()
        If McInstanceCurrent.State = McInstanceState.Error Then Throw New Exception("Minecraft 存在问题：" & McInstanceCurrent.Info)
        '检查输入信息
        Dim CheckResult As String = ""
        RunInUiWait(Sub() CheckResult = IsProfileVaild())
        If SelectedProfile Is Nothing Then '没选档案
            CheckResult = "请先选择一个档案再启动游戏！"
        ElseIf McInstanceCurrent.Version.HasLabyMod OrElse Setup.Get("VersionServerLoginRequire", McInstanceCurrent) = 1 Then '要求正版验证
            If Not SelectedProfile.Type = McLoginType.Ms Then
                CheckResult = "当前实例要求使用正版验证，请使用正版验证档案启动游戏！"
            End If
        ElseIf Setup.Get("VersionServerLoginRequire", McInstanceCurrent) = 2 Then '要求第三方验证
            If Not SelectedProfile.Type = McLoginType.Auth Then
                CheckResult = "当前实例要求使用第三方验证，请使用第三方验证档案启动游戏！"
            ElseIf Not SelectedProfile.Server.BeforeLast("/authserver") = Setup.Get("VersionServerAuthServer", McInstanceCurrent) Then
                CheckResult = "当前档案使用的第三方验证服务器与实例要求使用的不一致，请使用符合要求的档案启动游戏！"
            End If
        ElseIf Setup.Get("VersionServerLoginRequire", McInstanceCurrent) = 3 Then '要求正版验证或第三方验证
            If SelectedProfile.Type = McLoginType.Legacy Then
                CheckResult = "当前实例要求使用正版验证或第三方验证，请使用符合要求的档案启动游戏！"
            ElseIf SelectedProfile.Type = McLoginType.Auth AndAlso Not SelectedProfile.Server.BeforeLast("/authserver") = Setup.Get("VersionServerAuthServer", McInstanceCurrent) Then
                CheckResult = "当前档案使用的第三方验证服务器与实例要求使用的不一致，请使用符合要求的档案启动游戏！"
            End If
        End If
        If CheckResult <> "" Then Throw New ArgumentException(CheckResult)
#If BETA Then
        '求赞助
        If CurrentLaunchOptions?.SaveBatch Is Nothing Then '保存脚本时不提示
            RunInNewThread(
            Sub()
                Select Case Setup.Get("SystemLaunchCount")
                    Case 10, 20, 40, 60, 80, 100, 120, 150, 200, 250, 300, 350, 400, 500, 600, 700, 800, 900, 1000, 1200, 1400, 1600, 1800, 2000
                        If MyMsgBox("PCL 已经为你启动了 " & Setup.Get("SystemLaunchCount") & " 次游戏啦！" & vbCrLf &
                                    "如果 PCL 还算好用的话，也许可以考虑赞助一下 PCL 原作者……" & vbCrLf &
                                    "如果没有大家的支持，PCL 很难在免费、无任何广告的情况下维持数年的更新（磕头）……！",
                                    Setup.Get("SystemLaunchCount") & " 次启动！", "支持一下！", "但是我拒绝") = 1 Then
                            OpenWebsite("https://afdian.com/a/LTCat")
                        End If
                End Select
            End Sub, "Donate")
        End If
#End If
        '正版购买提示
        If CurrentLaunchOptions?.SaveBatch Is Nothing AndAlso '保存脚本时不提示
           Not Setup.Get("HintBuy") AndAlso SelectedProfile.Type <> McLoginType.Ms Then
            If IsRestrictedFeatAllowed Then
                RunInNewThread(
                Sub()
                    Select Case Setup.Get("SystemLaunchCount")
                        Case 3, 8, 15, 30, 50, 70, 90, 110, 130, 180, 220, 280, 330, 380, 450, 550, 660, 750, 880, 950, 1100, 1300, 1500, 1700, 1900
                            If MyMsgBox("你已经启动了 " & Setup.Get("SystemLaunchCount") & " 次 Minecraft 啦！" & vbCrLf &
                                "如果觉得 Minecraft 还不错，可以购买正版支持一下，毕竟开发游戏也真的很不容易……不要一直白嫖啦。" & vbCrLf & vbCrLf &
                                "在登录一次正版账号后，就不会再出现这个提示了！",
                                "考虑一下正版？", "支持正版游戏！", "下次一定") = 1 Then
                                OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj")
                            End If
                    End Select
                End Sub, "Buy Minecraft")
            Else
                Select Case MyMsgBox("你必须先登录正版账号才能启动游戏！", "正版验证", "购买正版", "试玩", "返回",
                    Button1Action:=Sub() OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj"))
                    Case 2
                        Hint("游戏将以试玩模式启动！", HintType.Critical)
                        CurrentLaunchOptions.ExtraArgs.Add("--demo")
                    Case 3
                        Throw New Exception("$$")
                End Select
            End If
        End If
    End Sub

#End Region

#Region "档案验证"

#Region "主模块"

    '登录方式
    Public Enum McLoginType
        Legacy = 1
        Auth = 2
        Ms = 3
    End Enum

    '各个登录方式的对应数据
    Public MustInherit Class McLoginData
        ''' <summary>
        ''' 登录方式。
        ''' </summary>
        Public Type As McLoginType
        Public Overrides Function Equals(obj As Object) As Boolean
            Return obj IsNot Nothing AndAlso obj.GetHashCode() = GetHashCode()
        End Function
    End Class

#Region "第三方验证类型"
    Public Class McLoginServer
        Inherits McLoginData

        ''' <summary>
        ''' 登录用户名。
        ''' </summary>
        Public UserName As String
        ''' <summary>
        ''' 登录密码。
        ''' </summary>
        Public Password As String
        ''' <summary>
        ''' 登录服务器基础地址。
        ''' </summary>
        Public BaseUrl As String
        ''' <summary>
        ''' 登录方式的描述字符串，如 “正版”、“统一通行证”。
        ''' </summary>
        Public Description As String
        ''' <summary>
        ''' 是否在本次登录中强制要求玩家重新选择角色，目前仅对 Authlib-Injector 生效。
        ''' </summary>
        Public ForceReselectProfile As Boolean = False
        ''' <summary>
        ''' 是否已经存在该验证信息，用于判断是否为新增档案。
        ''' </summary>
        Public IsExist As Boolean = False

        Public Sub New(Type As McLoginType)
            Me.Type = Type
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return GetHash(UserName & Password & BaseUrl & Type) Mod Integer.MaxValue
        End Function

    End Class
#End Region

#Region "正版验证类型"
    Public Class McLoginMs
        Inherits McLoginData

        ''' <summary>
        ''' 缓存的 OAuth RefreshToken。若没有则为空字符串。
        ''' </summary>
        Public OAuthRefreshToken As String = ""
        Public AccessToken As String = ""
        Public Uuid As String = ""
        Public UserName As String = ""
        Public ProfileJson As String = ""

        Public Sub New()
            Type = McLoginType.Ms
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return GetHash(OAuthRefreshToken & AccessToken & Uuid & UserName & ProfileJson) Mod Integer.MaxValue
        End Function
    End Class
#End Region

#Region "离线验证类型"
    Public Class McLoginLegacy
        Inherits McLoginData
        ''' <summary>
        ''' 登录用户名。
        ''' </summary>
        Public UserName As String
        ''' <summary>
        ''' 皮肤种类。
        ''' </summary>
        Public SkinType As Integer
        ''' <summary>
        ''' 若采用正版皮肤，则为该皮肤名。
        ''' </summary>
        Public SkinName As String
        ''' <summary>
        ''' UUID。
        ''' </summary>
        Public Uuid As String

        Public Sub New()
            Type = McLoginType.Legacy
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return GetHash(UserName & SkinType & SkinName & Type) Mod Integer.MaxValue
        End Function
    End Class
#End Region

    '登录返回结果
    Public Structure McLoginResult
        Public Name As String
        Public Uuid As String
        Public AccessToken As String
        Public Type As String
        Public ClientToken As String
        ''' <summary>
        ''' 进行微软登录时返回的 profile 信息。
        ''' </summary>
        Public ProfileJson As String
    End Structure

    '登录主模块加载器
    Public McLoginLoader As New LoaderTask(Of McLoginData, McLoginResult)("登录", AddressOf McLoginStart, AddressOf McLoginInput, ThreadPriority.BelowNormal) With {.ReloadTimeout = 1, .ProgressWeight = 15, .Block = False}
    Public Function McLoginInput() As McLoginData
        Dim LoginData As McLoginData = Nothing
        Try
            LoginData = GetLoginData()
        Catch ex As Exception
            Log(ex, "获取登录输入信息失败", LogLevel.Feedback)
        End Try
        Return LoginData
    End Function
    Private Sub McLoginStart(Data As LoaderTask(Of McLoginData, McLoginResult))
        Log("[Profile] 开始加载选定档案")
        '校验登录信息
        Dim CheckResult As String = IsProfileVaild()
        If Not CheckResult = "" Then Throw New ArgumentException(CheckResult)
        '获取对应加载器
        Dim Loader As LoaderBase = Nothing
        Select Case Data.Input.Type
            Case McLoginType.Ms
                Loader = McLoginMsLoader
            Case McLoginType.Legacy
                Loader = McLoginLegacyLoader
            Case McLoginType.Auth
                Loader = McLoginAuthLoader
        End Select
        '尝试加载
        Loader.WaitForExit(Data.Input, McLoginLoader, Data.IsForceRestarting)
        Data.Output = CType(Loader, Object).Output
        RunInUi(Sub() FrmLaunchLeft.RefreshPage(False)) '刷新自动填充列表
        Log("[Profile] 选定档案加载完成")
    End Sub

#End Region

    '各个登录方式的主对象与输入构造
    Public McLoginMsLoader As New LoaderTask(Of McLoginMs, McLoginResult)("Loader Login Ms", AddressOf McLoginMsStart) With {.ReloadTimeout = 1}
    Public McLoginLegacyLoader As New LoaderTask(Of McLoginLegacy, McLoginResult)("Loader Login Legacy", AddressOf McLoginLegacyStart)
    Public McLoginAuthLoader As New LoaderTask(Of McLoginServer, McLoginResult)("Loader Login Auth", AddressOf McLoginServerStart) With {.ReloadTimeout = 1000 * 60 * 10}

    '主加载函数，返回所有需要的登录信息
    Private McLoginMsRefreshTime As Long = 0 '上次刷新登录的时间

#Region "正版验证"
    Private Sub McLoginMsStart(Data As LoaderTask(Of McLoginMs, McLoginResult))
        Dim Input As McLoginMs = Data.Input
        Dim LogUsername As String = Input.UserName
        Dim IsNewProfile As Boolean = True
        ProfileLog("验证方式：正版（" & If(String.IsNullOrEmpty(LogUsername), "尚未登录", LogUsername) & "）")
        Data.Progress = 0.05
        '检查是否已经登录完成
        If Not Data.IsForceRestarting AndAlso '不要求强行重启
           Not String.IsNullOrEmpty(Input.AccessToken) AndAlso '已经登录过了
           (McLoginMsRefreshTime > 0 AndAlso TimeUtils.GetTimeTick() - McLoginMsRefreshTime < 1000 * 60 * 10) Then '完成时间在 10 分钟内
            Data.Output = New McLoginResult With
                {.AccessToken = Input.AccessToken, .Name = Input.UserName, .Uuid = Input.Uuid, .Type = "Microsoft", .ClientToken = Input.Uuid, .ProfileJson = Input.ProfileJson}
            GoTo SkipLogin
        End If
        '尝试登录
        Dim IsSkipAuth As Boolean = False
        Dim OAuthTokens As String()
        Dim OAuthAccessToken As String
        Dim OAuthRefreshToken As String
        If String.IsNullOrEmpty(Input.OAuthRefreshToken) Then
Relogin:
            OAuthTokens = MsLoginStep1New(Data)
        Else
            OAuthTokens = MsLoginStep1Refresh(Input.OAuthRefreshToken)
            If OAuthTokens(0) = "Relogin" Then GoTo Relogin
        End If
        If OAuthTokens(0) = "Ignore" Then GoTo SkipLogin
        OAuthAccessToken = OAuthTokens(0)
        OAuthRefreshToken = OAuthTokens(1)
        If Data.IsAborted Then Throw New ThreadInterruptedException
        Data.Progress = 0.25
        If Data.IsAborted Then Throw New ThreadInterruptedException
        'Step 2
        Dim XBLToken As String = MsLoginStep2(OAuthAccessToken)
        If XBLToken = "Ignore" Then GoTo SkipLogin
        Data.Progress = 0.4
        If Data.IsAborted Then Throw New ThreadInterruptedException
        'Step 3
        Dim Tokens = MsLoginStep3(XBLToken)
        If Tokens(1) = "Ignore" Then GoTo SkipLogin
        Data.Progress = 0.55
        If Data.IsAborted Then Throw New ThreadInterruptedException
        'Step 4
        Dim AccessToken As String = MsLoginStep4(Tokens)
        If AccessToken = "Ignore" Then GoTo SkipLogin
        Data.Progress = 0.7
        If Data.IsAborted Then Throw New ThreadInterruptedException
        'Step 5
        MsLoginStep5(AccessToken)
        Data.Progress = 0.85
        If Data.IsAborted Then Throw New ThreadInterruptedException
        'Step 6
        Dim Result = MsLoginStep6(AccessToken)
        If Result(2) = "Ignore" Then GoTo SkipLogin
        Data.Progress = 0.98

        For Each Profile In ProfileList
            If Profile.Type = McLoginType.Ms AndAlso Profile.Username = Result(1) AndAlso Profile.Uuid = Result(0) Then
                IsNewProfile = False
                If IsCreatingProfile Then
                    Dim ProfileIndex = ProfileList.IndexOf(Profile)
                    ProfileList(ProfileIndex).Username = Result(1)
                    ProfileList(ProfileIndex).AccessToken = AccessToken
                    ProfileList(ProfileIndex).RefreshToken = OAuthRefreshToken
                    Hint("你已经添加了这个档案...")
                    GoTo SkipLogin
                End If
            End If
        Next
        '输出登录结果
        If IsNewProfile Then
            Dim NewProfile = New McProfile With {
                .Type = McLoginType.Ms,
                .Uuid = Result(0),
                .Username = Result(1),
                .AccessToken = AccessToken,
                .RefreshToken = OAuthRefreshToken,
                .Expires = 1743779140286,
                .Desc = "",
                .RawJson = Result(2)
            }
            ProfileList.Add(NewProfile)
            SelectedProfile = NewProfile
            IsCreatingProfile = False
        Else
            Dim ProfileIndex = ProfileList.IndexOf(SelectedProfile)
            ProfileList(ProfileIndex).Username = Result(1)
            ProfileList(ProfileIndex).AccessToken = AccessToken
            ProfileList(ProfileIndex).RefreshToken = OAuthRefreshToken
        End If
        SaveProfile()
        Data.Output = New McLoginResult With {
            .AccessToken = AccessToken,
            .Name = Result(1),
            .Uuid = Result(0),
            .Type = "Microsoft",
            .ClientToken = Result(0),
            .ProfileJson = Result(2)}
SkipLogin:
        '结束
        McLoginMsRefreshTime = TimeUtils.GetTimeTick()
        ProfileLog("正版验证完成")
        Setup.Set("HintBuy", True) '关闭正版购买提示
        If IsSkipAuth Then
            Data.Progress = 0.99
            Data.Output = New McLoginResult With {.AccessToken = SelectedProfile.AccessToken,
                    .Name = SelectedProfile.Username, .Uuid = SelectedProfile.Uuid,
                    .Type = "Microsoft"}
            Exit Sub
        End If
    End Sub
    ''' <summary>
    ''' 正版验证步骤 1：通过设备代码流获取账号信息
    ''' </summary>
    ''' <returns>OAuth 验证完成的返回结果</returns>
    Private Function MsLoginStep1New(Data As LoaderTask(Of McLoginMs, McLoginResult)) As String()
        '参考：https://learn.microsoft.com/zh-cn/entra/identity-platform/v2-oauth2-device-code

        '初始请求
Retry:
        McLaunchLog("开始正版验证 Step 1/6（原始登录）")
        Dim PrepareJson As JObject = GetJson(NetRequestRetry("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", "POST",
            $"client_id={OAuthClientId}&tenant=/consumers&scope=XboxLive.signin%20offline_access", "application/x-www-form-urlencoded"))
        McLaunchLog("网页登录地址：" & PrepareJson("verification_uri").ToString)

        '弹窗
        Dim Converter As New MyMsgBoxConverter With {.Content = PrepareJson, .ForceWait = True, .Type = MyMsgBoxType.Login}
        WaitingMyMsgBox.Add(Converter)
        While Converter.Result Is Nothing
            Thread.Sleep(100)
        End While
        If TypeOf Converter.Result Is RestartException Then
            If MyMsgBox($"请在登录时选择 {vbLQ}其他登录方法{vbRQ}，然后选择 {vbLQ}使用我的密码{vbRQ}。{vbCrLf}如果没有该选项，请选择 {vbLQ}设置密码{vbRQ}，设置完毕后再登录。",
                "需要使用密码登录", "重新登录", "设置密码", "取消",
                Button2Action:=Sub() OpenWebsite("https://account.live.com/password/Change")) = 1 Then
                GoTo Retry
            Else
                Throw New Exception("$$")
            End If
        ElseIf TypeOf Converter.Result Is Exception Then
            Throw CType(Converter.Result, Exception)
        Else
            Return Converter.Result
        End If
    End Function
    ''' <summary>
    ''' 正版验证步骤 1，刷新登录：从 OAuth Code 或 OAuth RefreshToken 获取 {OAuth AccessToken, OAuth RefreshToken}
    ''' </summary>
    ''' <param name="Code"></param>
    ''' <returns></returns>
    Private Function MsLoginStep1Refresh(Code As String) As String()
        McLaunchLog("开始正版验证 Step 1/6（刷新登录）")
        If String.IsNullOrEmpty(Code) Then Throw New ArgumentException("传入的 Code 为空", NameOf(Code))
        Dim Result As String = Nothing
        Try
            Result = NetRequestRetry("https://login.live.com/oauth20_token.srf", "POST",
                $"client_id={OAuthClientId}&refresh_token={Uri.EscapeDataString(Code)}&grant_type=refresh_token&scope=XboxLive.signin%20offline_access",
                "application/x-www-form-urlencoded", 2)
        Catch ex As ThreadInterruptedException
            Log(ex, "加载线程已终止")
        Catch ex As Exception
            If ex.Message.ContainsF("must sign in again", True) OrElse ex.Message.ContainsF("password expired", True) OrElse
               (ex.Message.Contains("refresh_token") AndAlso ex.Message.Contains("is not valid")) Then '#269
                Return {"Relogin", ""}
            Else
                ProfileLog("正版验证 Step 1/6 获取 OAuth Token 失败：" & ex.ToString())
                Dim IsIgnore As Boolean = False
                RunInUiWait(Sub()
                                If Not IsLaunching Then Exit Sub
                                If MyMsgBox($"启动器在尝试刷新账号信息时遇到了网络错误。{vbCrLf}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。", "账号信息获取失败", "继续", "取消") = 1 Then IsIgnore = True
                            End Sub)
                If IsIgnore Then
                    Return {"Ignore", ""}
                    Exit Function
                End If
            End If
        End Try

        Dim ResultJson As JObject = GetJson(Result)
        Dim AccessToken As String = ResultJson("access_token").ToString
        Dim RefreshToken As String = ResultJson("refresh_token").ToString
        Return {AccessToken, RefreshToken}
    End Function

    ''' <summary>
    ''' 正版验证步骤 2：从 OAuth AccessToken 获取 XBLToken
    ''' </summary>
    ''' <param name="AccessToken">OAuth AccessToken</param>
    ''' <returns>XBLToken</returns>
    Private Function MsLoginStep2(AccessToken As String) As String
        ProfileLog("开始正版验证 Step 2/6: 获取 XBLToken")
        If String.IsNullOrEmpty(AccessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(AccessToken))
        Dim Request As String = New JObject(
                                        New JProperty("Properties", New JObject(
                                            New JProperty("AuthMethod", "RPS"),
                                            New JProperty("SiteName", "user.auth.xboxlive.com"),
                                            New JProperty("RpsTicket", If(AccessToken.StartsWith("d="), AccessToken, $"d={AccessToken}"))
                                        )),
                                    New JProperty("RelyingParty", "http://auth.xboxlive.com"),
                                    New JProperty("TokenType", "JWT")
                                ).ToString(Newtonsoft.Json.Formatting.None)
        Dim Result As String = Nothing
        Try
            Result = NetRequestRetry(
                "https://user.auth.xboxlive.com/user/authenticate",
                "POST",
                Request,
                "application/json",
                False)
        Catch ex As Exception
            ProfileLog("正版验证 Step 2/6 获取 XBLToken 失败：" & ex.ToString())
            Dim IsIgnore As Boolean = False
            RunInUiWait(Sub()
                            If Not IsLaunching Then Exit Sub
                            If MyMsgBox($"启动器在尝试刷新账号信息时(Step 2)遇到了网络错误。{vbCrLf}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。", "账号信息获取失败", "继续", "取消") = 1 Then IsIgnore = True
                        End Sub)
            If IsIgnore Then
                Return "Ignore"
                Exit Function
            End If
        End Try

        Dim ResultJson As JObject = GetJson(Result)
        Dim XBLToken As String = ResultJson("Token").ToString
        Return XBLToken
    End Function
    ''' <summary>
    ''' 正版验证步骤 3：从 XBLToken 获取 {XSTSToken, UHS}
    ''' </summary>
    ''' <returns>包含 XSTSToken 与 UHS 的字符串组</returns>
    Private Function MsLoginStep3(XBLToken As String) As String()
        ProfileLog("开始正版验证 Step 3/6: 获取 XSTSToken")
        If String.IsNullOrEmpty(XBLToken) Then Throw New ArgumentException("XBLToken 为空，无法获取数据", NameOf(XBLToken))
        Dim Request As String = New JObject(
                                    New JProperty("Properties", New JObject(
                                        New JProperty("SandboxId", "RETAIL"),
                                        New JProperty("UserTokens", New JArray(XBLToken))
                                    )),
                                New JProperty("RelyingParty", "rp://api.minecraftservices.com/"),
                                New JProperty("TokenType", "JWT")
                            ).ToString(Newtonsoft.Json.Formatting.None)
        Dim Result As String
        Try
            Result = NetRequestRetry(
                "https://xsts.auth.xboxlive.com/xsts/authorize",
                "POST",
                Request,
                "application/json",
                False)
        Catch ex As WebException
            '参考 https://github.com/PrismarineJS/prismarine-auth/blob/master/src/common/Constants.js
            If ex.Message.Contains("2148916227") Then
                MyMsgBox("该账号似乎已被微软封禁，无法登录。", "登录失败", "我知道了", IsWarn:=True)
                Throw New Exception("$$")
            ElseIf ex.Message.Contains("2148916233") Then
                If MyMsgBox("你尚未注册 Xbox 账户，请在注册后再登录。", "登录提示", "注册", "取消") = 1 Then
                    OpenWebsite("https://signup.live.com/signup")
                End If
                Throw New Exception("$$")
            ElseIf ex.Message.Contains("2148916235") Then
                MyMsgBox($"你的网络所在的国家或地区无法登录微软账号。{vbCrLf}请使用加速器或 VPN。", "登录失败", "我知道了")
                Throw New Exception("$$")
            ElseIf ex.Message.Contains("2148916238") Then
                If MyMsgBox("该账号年龄不足，你需要先修改出生日期，然后才能登录。" & vbCrLf &
                            "该账号目前填写的年龄是否在 13 岁以上？", "登录提示", "13 岁以上", "12 岁以下", "我不知道") = 1 Then
                    OpenWebsite("https://account.live.com/editprof.aspx")
                    MyMsgBox("请在打开的网页中修改账号的出生日期（至少改为 18 岁以上）。" & vbCrLf &
                             "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！", "登录提示")
                Else
                    OpenWebsite("https://support.microsoft.com/zh-cn/account-billing/如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a")
                    MyMsgBox("请根据打开的网页的说明，修改账号的出生日期（至少改为 18 岁以上）。" & vbCrLf &
                             "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！", "登录提示")
                End If
                Throw New Exception("$$")
            Else
                ProfileLog("正版验证 Step 3/6 获取 XSTSToken 失败：" & ex.ToString())
                Dim IsIgnore As Boolean = False
                RunInUiWait(Sub()
                                If Not IsLaunching Then Exit Sub
                                If MyMsgBox($"启动器在尝试刷新账号信息时(Step 3)遇到了网络错误。{vbCrLf}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。", "账号信息获取失败", "继续", "取消") = 1 Then IsIgnore = True
                            End Sub)
                If IsIgnore Then
                    Return {SelectedProfile.AccessToken, "Ignore"}
                    Exit Function
                End If
                Throw
            End If
        End Try

        Dim ResultJson As JObject = GetJson(Result)
        Dim XSTSToken As String = ResultJson("Token").ToString
        Dim UHS As String = ResultJson("DisplayClaims")("xui")(0)("uhs").ToString
        Return {XSTSToken, UHS}
    End Function
    ''' <summary>
    ''' 正版验证步骤 4：从 {XSTSToken, UHS} 获取 Minecraft AccessToken
    ''' </summary>
    ''' <param name="Tokens">包含 XSTSToken 与 UHS 的字符串组</param>
    ''' <returns>Minecraft AccessToken</returns>
    Private Function MsLoginStep4(Tokens As String()) As String
        ProfileLog("开始正版验证 Step 4/6: 获取 Minecraft AccessToken")
        If Tokens.Length < 2 OrElse String.IsNullOrEmpty(Tokens.ElementAt(0)) OrElse String.IsNullOrEmpty(Tokens.ElementAt(1)) Then Throw New ArgumentException("传入的 XSTSToken 或者 UHS 错误", NameOf(Tokens))
        Dim Request As String = New JObject(New JProperty("identityToken", $"XBL3.0 x={Tokens(1)};{Tokens(0)}")).ToString(0)
        Dim Result As String
        Try
            Result = NetRequestRetry(
                "https://api.minecraftservices.com/authentication/login_with_xbox",
                "POST",
                Request,
                "application/json",
                False)
        Catch ex As PCL.ModNet.HttpWebException
            Dim Message As String = GetExceptionSummary(ex)
            If CType(ex.StatusCode, Integer) = 429 Then
                Log(ex, "正版验证 Step 4 汇报 429")
                Throw New Exception("$登录尝试太过频繁，请等待几分钟后再试！")
            ElseIf ex.StatusCode = HttpStatusCode.NotFound Then
                Log(ex, "正版验证 Step 4 汇报 403")
                Throw New Exception("$当前 IP 的登录尝试异常。" & vbCrLf & "如果你使用了 VPN 或加速器，请把它们关掉或更换节点后再试！")
            Else
                ProfileLog("正版验证 Step 4/6 获取 MC AccessToken 失败：" & ex.ToString())
                Dim IsIgnore As Boolean = False
                RunInUiWait(Sub()
                                If Not IsLaunching Then Exit Sub
                                If MyMsgBox($"启动器在尝试刷新账号信息时(Step 4)遇到了网络错误。{vbCrLf}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。", "账号信息获取失败", "继续", "取消") = 1 Then IsIgnore = True
                            End Sub)
                If IsIgnore Then
                    Return "Ignore"
                    Exit Function
                End If
                Throw
            End If
        End Try

        Dim ResultJson As JObject = GetJson(Result)
        Dim AccessToken As String = ResultJson("access_token").ToString()
        If String.IsNullOrWhiteSpace(AccessToken) Then Throw New Exception("获取到的 Minecraft AccessToken 为空，登录流程异常！")
        Return AccessToken
    End Function
    ''' <summary>
    ''' 正版验证步骤 5：验证微软账号是否持有 MC，这也会刷新 XGP
    ''' </summary>
    ''' <param name="AccessToken">Minecraft AccessToken</param>
    Private Sub MsLoginStep5(AccessToken As String)
        ProfileLog("开始正版验证 Step 5/6: 验证账户是否持有 MC")
        If String.IsNullOrEmpty(AccessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(AccessToken))
        Dim Result As String = NetRequestRetry(
            "https://api.minecraftservices.com/entitlements",
            "GET",
            Nothing,
            "application/json",
            False,
            New Dictionary(Of String, String) From {{"Authorization", $"Bearer {AccessToken}"}})
        Try
            Dim ResultJson As JObject = GetJson(Result)
            If Not (ResultJson.ContainsKey("items") AndAlso ResultJson("items").Any(Function(x) x("name")?.ToString() = "product_minecraft" OrElse x("name")?.ToString() = "game_minecraft")) Then
                Select Case MyMsgBox($"暂时无法获取到此账户信息，此账户可能没有购买 Minecraft Java Edition 或者账户的 Xbox Game Pass 已过期", "登录失败", "购买 Minecraft", "取消")
                    Case 1
                        OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj")
                End Select
                Throw New Exception("$$")
            End If
        Catch ex As Exception
            Log(ex, "正版验证 Step 5 异常：" & Result)
            Throw
        End Try
    End Sub
    ''' <summary>
    ''' 正版验证步骤 6：从 Minecraft AccessToken 获取 {UUID, UserName, ProfileJson}
    ''' </summary>
    ''' <param name="AccessToken">Minecraft AccessToken</param>
    ''' <returns>包含 UUID, UserName 和 ProfileJson 的字符串组</returns>
    Private Function MsLoginStep6(AccessToken As String) As String()
        ProfileLog("开始正版验证 Step 6/6: 获取玩家 ID 与 UUID 等相关信息")
        If String.IsNullOrEmpty(AccessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(AccessToken))
        Dim Result As String
        Try
            Result = NetRequestRetry(
                "https://api.minecraftservices.com/minecraft/profile",
                "GET",
                "",
                "application/json",
                False,
                New Dictionary(Of String, String) From {{"Authorization", $"Bearer {AccessToken}"}})
        Catch ex As PCL.ModNet.HttpWebException
            Dim Message As String = GetExceptionSummary(ex)
            If CType(ex.StatusCode, Integer) = 429 Then '微软！我的 TooManyRequests 枚举呢？
                Log(ex, "正版验证 Step 6 汇报 429")
                Throw New Exception("$登录尝试太过频繁，请等待几分钟后再试！")
            ElseIf ex.StatusCode = HttpStatusCode.NotFound Then
                Log(ex, "正版验证 Step 6 汇报 404")
                RunInNewThread(
                Sub()
                    Select Case MyMsgBox("请先创建 Minecraft 玩家档案，然后再重新登录。", "登录失败", "创建档案", "取消")
                        Case 1
                            OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile")
                    End Select
                End Sub, "Login Failed: Create Profile")
                Throw New Exception("$$")
            Else
                ProfileLog("正版验证 Step 6/6 获取玩家档案信息失败：" & ex.ToString())
                Dim IsIgnore As Boolean = False
                RunInUiWait(Sub()
                                If Not IsLaunching Then Exit Sub
                                If MyMsgBox($"启动器在尝试刷新账号信息时(Step 6)遇到了网络错误。{vbCrLf}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。", "账号信息获取失败", "继续", "取消") = 1 Then IsIgnore = True
                            End Sub)
                If IsIgnore Then
                    Return {SelectedProfile.Uuid, SelectedProfile.Username, "Ignore"}
                    Exit Function
                End If
                Throw
            End If
        End Try
        Dim ResultJson As JObject = GetJson(Result)
        Dim UUID As String = ResultJson("id").ToString
        Dim UserName As String = ResultJson("name").ToString
        Return {UUID, UserName, Result}
    End Function
#End Region

#Region "第三方验证"
    Private Sub McLoginServerStart(Data As LoaderTask(Of McLoginServer, McLoginResult))
        Dim Input As McLoginServer = Data.Input
        Dim NeedRefresh As Boolean = False, WasRefreshed As Boolean = False
        ProfileLog("验证方式：" & Input.Description)
        Data.Progress = 0.05
        '尝试登录
        If (Not Data.Input.ForceReselectProfile) AndAlso (Not IsCreatingProfile) Then
            '尝试验证登录
            Try
                If Data.IsAborted Then Throw New ThreadInterruptedException
                McLoginRequestValidate(Data)
                GoTo LoginFinish
            Catch ex As HttpWebException
                Dim AllMessage = GetExceptionDetail(ex)
                ProfileLog("验证登录失败：" & AllMessage)
                If (AllMessage.Contains("超时") OrElse AllMessage.Contains("imeout")) AndAlso Not AllMessage.Contains("403") Then
                    ProfileLog("已触发超时登录失败")
                    Throw New Exception("$登录失败：连接登录服务器超时。" & vbCrLf & "请检查你的网络状况是否良好，或尝试使用 VPN！" & vbCrLf & vbCrLf & "详细信息：" & ex.InnerHttpException.WebResponse)
                End If
            Catch ex As Exception
                Dim AllMessage = GetExceptionDetail(ex)
                ProfileLog("验证登录失败：" & AllMessage)
                Throw
            End Try
            Data.Progress = 0.25
            '尝试刷新登录
Refresh:
            Try
                If Data.IsAborted Then Throw New ThreadInterruptedException
                McLoginRequestRefresh(Data, NeedRefresh)
                GoTo LoginFinish
            Catch ex As Exception
                ProfileLog("刷新登录失败：" & GetExceptionDetail(ex))
                If WasRefreshed Then Throw New Exception("二轮刷新登录失败", ex)
            End Try
            Data.Progress = If(NeedRefresh, 0.85, 0.45)
        End If
        '尝试普通登录
        Try
            If Data.IsAborted Then Throw New ThreadInterruptedException
            NeedRefresh = McLoginRequestLogin(Data)
        Catch ex As HttpWebException
            ProfileLog("验证失败：" & GetExceptionDetail(ex))
            Dim message As String = Nothing
            Dim responseText = ex.InnerHttpException.WebResponse
            Try
                Dim err = JsonNode.Parse(responseText)("errorMessage")
                If err IsNot Nothing Then message = "登录失败：" & err.ToString()
            Catch
                '忽略
            End Try
            If message Is Nothing Then message = "第三方验证登录失败，请检查你的网络状况是否良好。" & vbCrLf & vbCrLf & "详细信息：" & responseText
            Throw New Exception("$" & message)
        Catch ex As Exception
            ProfileLog("验证失败：" & GetExceptionDetail(ex))
            Throw New Exception("$第三方验证登录失败" & vbCrLf & vbCrLf & "详细信息：" & ex.ToString())
        End Try
        If NeedRefresh Then
            ProfileLog("重新进行刷新登录")
            WasRefreshed = True
            Data.Progress = 0.65
            GoTo Refresh
        End If
LoginFinish:
        Data.Progress = 0.95
    End Sub
    'Server 登录：三种验证方式的请求
    Private Sub McLoginRequestValidate(ByRef Data As LoaderTask(Of McLoginServer, McLoginResult))
        ProfileLog("验证登录开始（Validate, Authlib")
        '提前缓存信息，否则如果在登录请求过程中退出登录，设置项目会被清空，导致输出存在空值
        Dim AccessToken As String = ""
        Dim ClientToken As String = ""
        Dim Uuid As String = ""
        Dim Name As String = ""
        If SelectedProfile IsNot Nothing Then
            AccessToken = SelectedProfile.AccessToken
            ClientToken = SelectedProfile.ClientToken
            Uuid = SelectedProfile.Uuid
            Name = SelectedProfile.Username
        End If
        '发送登录请求
        Dim RequestData As New JObject(
            New JProperty("accessToken", AccessToken), New JProperty("clientToken", ClientToken))
        NetRequestRetry(
            Url:=Data.Input.BaseUrl & "/validate",
            Method:="POST",
            Data:=RequestData.ToString(0),
            Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
            ContentType:="application/json") '没有返回值的
        '将登录结果输出
        Data.Output.AccessToken = AccessToken
        Data.Output.ClientToken = ClientToken
        Data.Output.Uuid = Uuid
        Data.Output.Name = Name
        Data.Output.Type = "Auth"
        '不更改缓存，直接结束
        ProfileLog("验证登录成功（Validate, Authlib")
    End Sub
    Private Sub McLoginRequestRefresh(ByRef Data As LoaderTask(Of McLoginServer, McLoginResult), RequestUser As Boolean)
        Dim RefreshInfo As New JObject
        Dim SelectProfile As New JObject From {
            {"name", SelectedProfile.Username},
            {"id", SelectedProfile.Uuid}
        }
        RefreshInfo.Add("selectedProfile", SelectProfile)
        RefreshInfo.Add(New JProperty("accessToken", SelectedProfile.AccessToken))
        RefreshInfo.Add(New JProperty("requestUser", True))
        ProfileLog("刷新登录开始（Refresh, Authlib")
        Dim LoginJson As JObject = GetJson(NetRequestRetry(
               Url:=Data.Input.BaseUrl & "/refresh",
               Method:="POST",
               Data:=RefreshInfo.ToString(0),
               Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
               ContentType:="application/json"))
        '将登录结果输出
        If LoginJson("selectedProfile") Is Nothing Then Throw New Exception("选择的角色 " & SelectedProfile.Username & " 无效！")
        Data.Output.AccessToken = LoginJson("accessToken").ToString
        Data.Output.ClientToken = LoginJson("clientToken").ToString
        Data.Output.Uuid = LoginJson("selectedProfile")("id").ToString
        Data.Output.Name = LoginJson("selectedProfile")("name").ToString
        Data.Output.Type = "Auth"
        '保存缓存
        Dim ProfileIndex = ProfileList.IndexOf(SelectedProfile)
        ProfileList(ProfileIndex).Username = Data.Output.Name
        ProfileList(ProfileIndex).AccessToken = Data.Output.AccessToken
        ProfileList(ProfileIndex).ClientToken = Data.Output.ClientToken
        ProfileList(ProfileIndex).Uuid = Data.Output.Uuid
        ProfileList(ProfileIndex).Name = Data.Input.UserName
        ProfileList(ProfileIndex).Password = Data.Input.Password
        ProfileLog("刷新登录成功（Refresh, Authlib）")
    End Sub
    Private Function McLoginRequestLogin(ByRef Data As LoaderTask(Of McLoginServer, McLoginResult)) As Boolean
        Try
            Dim NeedRefresh As Boolean = False
            ProfileLog("登录开始（Login, Authlib）")
            Dim RequestData As New JObject(
                New JProperty("agent", New JObject(New JProperty("name", "Minecraft"), New JProperty("version", 1))),
                New JProperty("username", Data.Input.UserName),
                New JProperty("password", Data.Input.Password),
                New JProperty("requestUser", True))
            Dim LoginJson As JObject = GetJson(NetRequestRetry(
                Url:=Data.Input.BaseUrl & "/authenticate",
                Method:="POST",
                Data:=RequestData.ToString(0),
                Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
                ContentType:="application/json"))
            '检查登录结果
            If LoginJson("availableProfiles").Count = 0 Then
                If Data.Input.ForceReselectProfile Then Hint("你还没有创建角色，无法更换！", HintType.Critical)
                Throw New Exception("$你还没有创建角色，请在创建角色后再试！")
            ElseIf Data.Input.ForceReselectProfile AndAlso LoginJson("availableProfiles").Count = 1 Then
                Hint("你的账户中只有一个角色，无法更换！", HintType.Critical)
            End If
            Dim SelectedName As String = Nothing
            Dim SelectedId As String = Nothing
            If (LoginJson("selectedProfile") Is Nothing OrElse Data.Input.ForceReselectProfile) AndAlso LoginJson("availableProfiles").Count > 1 Then
                '要求选择档案；优先从缓存读取
                NeedRefresh = True
                Dim CacheId As String = If(SelectedProfile IsNot Nothing, SelectedProfile.Uuid, "")
                For Each Profile In LoginJson("availableProfiles")
                    If Profile("id").ToString = CacheId Then
                        SelectedName = Profile("name").ToString
                        SelectedId = Profile("id").ToString
                        ProfileLog("根据缓存选择的角色：" & SelectedName)
                    End If
                Next
                '缓存无效，要求玩家选择
                If SelectedName Is Nothing Then
                    ProfileLog("要求玩家选择角色")
                    RunInUiWait(
                                            Sub()
                                                Dim SelectionControl As New List(Of IMyRadio)
                                                Dim SelectionJson As New List(Of JToken)
                                                For Each Profile In LoginJson("availableProfiles")
                                                    SelectionControl.Add(New MyRadioBox With {.Text = Profile("name").ToString})
                                                    SelectionJson.Add(Profile)
                                                Next
                                                Dim SelectedIndex As Integer = MyMsgBoxSelect(SelectionControl, "选择使用的角色")
                                                SelectedName = SelectionJson(SelectedIndex)("name").ToString
                                                SelectedId = SelectionJson(SelectedIndex)("id").ToString
                                            End Sub)

                    ProfileLog("玩家选择的角色：" & SelectedName)
                End If
            Else
                SelectedName = LoginJson("selectedProfile")("name").ToString
                SelectedId = LoginJson("selectedProfile")("id").ToString
            End If
            '将登录结果输出
            Data.Output.AccessToken = LoginJson("accessToken").ToString
            Data.Output.ClientToken = LoginJson("clientToken").ToString
            Data.Output.Name = SelectedName
            Data.Output.Uuid = SelectedId
            Data.Output.Type = "Auth"
            '获取服务器信息
            Dim Response As String = NetGetCodeByRequestRetry(Data.Input.BaseUrl.Replace("/authserver", ""), Encoding.UTF8)
            Dim ServerName As String = JObject.Parse(Response)("meta")("serverName").ToString()
            '保存缓存
            If Data.Input.IsExist Then
                Dim ProfileIndex = ProfileList.IndexOf(SelectedProfile)
                ProfileList(ProfileIndex).Username = Data.Output.Name
                ProfileList(ProfileIndex).Uuid = Data.Output.Uuid
                ProfileList(ProfileIndex).ServerName = ServerName
                ProfileList(ProfileIndex).AccessToken = Data.Output.AccessToken
                ProfileList(ProfileIndex).ClientToken = Data.Output.ClientToken
            Else
                Dim NewProfile As New McProfile With {
                    .Type = McLoginType.Auth,
                    .Uuid = Data.Output.Uuid,
                    .Username = Data.Output.Name,
                    .Server = Data.Input.BaseUrl,
                    .ServerName = ServerName,
                    .Name = Data.Input.UserName,
                    .Password = Data.Input.Password,
                    .AccessToken = Data.Output.AccessToken,
                    .ClientToken = Data.Output.ClientToken,
                    .Expires = 1743779140286,
                    .Desc = ""
                }
                ProfileList.Add(NewProfile)
                SelectedProfile = NewProfile
                IsCreatingProfile = False
            End If
            SaveProfile()
            ProfileLog("登录成功（Login, Authlib）")
            Return NeedRefresh
        Catch ex As HttpWebException
            Throw
        Catch ex As Exception
            Dim AllMessage As String = GetExceptionSummary(ex)
            ProfileLog("第三方验证失败: " & ex.ToString())
            If ex.Message.StartsWithF("$") Then
                Throw
            Else
                Throw New Exception("登录失败：" & ex.Message, ex)
            End If
        End Try
    End Function
#End Region

#Region "离线验证"
    Private Sub McLoginLegacyStart(Data As LoaderTask(Of McLoginLegacy, McLoginResult))
        Dim Input As McLoginLegacy = Data.Input
        ProfileLog($"验证方式：离线（{Input.UserName}, {Input.Uuid}）")
        Data.Progress = 0.1
        With Data.Output
            .Name = Input.UserName
            .Uuid = SelectedProfile.Uuid
            .Type = "Legacy"
        End With
        '将结果扩展到所有项目中
        Data.Output.AccessToken = Data.Output.Uuid
        Data.Output.ClientToken = Data.Output.Uuid
    End Sub
#End Region

#End Region

#Region "Java 处理"

    Public McLaunchJavaSelected As Java = Nothing
    Private Sub McLaunchJava(Task As LoaderTask(Of Integer, Integer))
        Dim MinVer As New Version(0, 0, 0, 0), MaxVer As New Version(999, 999, 999, 999)

        'MC 大版本检测
        If (Not McInstanceCurrent.Version.IsStandardVersion AndAlso McInstanceCurrent.ReleaseTime >= New Date(2024, 4, 2)) OrElse
           (McInstanceCurrent.Version.IsStandardVersion AndAlso McInstanceCurrent.Version.McInstance >= New Version(1, 20, 5)) Then
            '1.20.5+（24w14a+）：至少 Java 21
            MinVer = New Version(21, 0, 0, 0)
        ElseIf (Not McInstanceCurrent.Version.IsStandardVersion AndAlso McInstanceCurrent.ReleaseTime >= New Date(2021, 11, 16)) OrElse
            (McInstanceCurrent.Version.IsStandardVersion AndAlso McInstanceCurrent.Version.McInstance >= New Version(1, 18)) Then
            '1.18 pre2+：至少 Java 17
            MinVer = New Version(17, 0, 0, 0)
        ElseIf (Not McInstanceCurrent.Version.IsStandardVersion AndAlso McInstanceCurrent.ReleaseTime >= New Date(2021, 5, 11)) OrElse
           (McInstanceCurrent.Version.IsStandardVersion AndAlso McInstanceCurrent.Version.McInstance >= New Version(1, 17)) Then
            '1.17+ (21w19a+)：至少 Java 16
            MinVer = New Version(16, 0, 0, 0)
        ElseIf McInstanceCurrent.ReleaseTime.Year >= 2017 Then 'Minecraft 1.12 与 1.11 的分界线正好是 2017 年，太棒了
            '1.12+：至少 Java 8
            MinVer = New Version(1, 8, 0, 0)
        ElseIf McInstanceCurrent.ReleaseTime <= New Date(2013, 5, 1) AndAlso McInstanceCurrent.ReleaseTime.Year >= 2001 Then '避免某些版本写个 1960 年
            '1.5.2-：最高 Java 12
            MaxVer = New Version(12, 999, 999, 999)
        End If
        If McInstanceCurrent.JsonVersion?("java_version") IsNot Nothing Then
            Dim RecommendedJava As Integer = McInstanceCurrent.JsonVersion("java_version").ToObject(Of Integer)
            McLaunchLog("Mojang 推荐使用 Java " & RecommendedJava)
            If RecommendedJava >= 22 Then MinVer = New Version(RecommendedJava, 0, 0, 0) '潜在的向后兼容
        End If

        'OptiFine 检测
        If McInstanceCurrent.Version.HasOptiFine AndAlso McInstanceCurrent.Version.IsStandardVersion Then '不管非标准版本
            If McInstanceCurrent.Version.McInstance < New Version(1, 7) Then
                '<1.7：至多 Java 8
                MaxVer = New Version(8, 999, 999, 999)
            ElseIf McInstanceCurrent.Version.McInstance >= New Version(1, 8) AndAlso McInstanceCurrent.Version.McInstance < New Version(1, 12) Then
                '1.8 - 1.11：必须恰好 Java 8
                MinVer = New Version(1, 8, 0, 0) : MaxVer = New Version(8, 999, 999, 999)
            ElseIf McInstanceCurrent.Version.McCodeMain = 12 Then
                '1.12：最高 Java 8
                MaxVer = New Version(8, 999, 999, 999)
            End If
        End If

        If McInstanceCurrent.Version.HasLiteLoader AndAlso McInstanceCurrent.Version.IsStandardVersion Then
            '最高 Java 8
            MaxVer = If(New Version(8, 999, 999, 999) < MaxVer, New Version(8, 999, 999, 999), MaxVer)
        End If

        'Forge 检测
        If McInstanceCurrent.Version.HasForge Then
            If McInstanceCurrent.Version.McInstance >= New Version(1, 6, 1) AndAlso McInstanceCurrent.Version.McInstance <= New Version(1, 7, 2) Then
                '1.6.1 - 1.7.2：必须 Java 7
                MinVer = If(New Version(1, 7, 0, 0) > MinVer, New Version(1, 7, 0, 0), MinVer)
                MaxVer = If(New Version(1, 7, 999, 999) < MaxVer, New Version(1, 7, 999, 999), MaxVer)
            ElseIf McInstanceCurrent.Version.McCodeMain <= 12 OrElse Not McInstanceCurrent.Version.IsStandardVersion Then '非标准版本
                '<=1.12：Java 8
                MaxVer = New Version(8, 999, 999, 999)
            ElseIf McInstanceCurrent.Version.McCodeMain <= 14 Then
                '1.13 - 1.14：Java 8 - 10
                MinVer = If(New Version(1, 8, 0, 0) > MinVer, New Version(1, 8, 0, 0), MinVer)
                MaxVer = If(New Version(10, 999, 999, 999) < MaxVer, New Version(10, 999, 999, 999), MaxVer)
            ElseIf McInstanceCurrent.Version.McCodeMain = 15 Then
                '1.15：Java 8 - 15
                MinVer = If(New Version(1, 8, 0, 0) > MinVer, New Version(1, 8, 0, 0), MinVer)
                MaxVer = If(New Version(15, 999, 999, 999) < MaxVer, New Version(15, 999, 999, 999), MaxVer)
            ElseIf VersionSortBoolean(McInstanceCurrent.Version.ForgeVersion, "34.0.0") AndAlso VersionSortBoolean("36.2.25", McInstanceCurrent.Version.ForgeVersion) Then
                '1.16，Forge 34.X ~ 36.2.25：最高 Java 8u320
                MaxVer = If(New Version(1, 8, 0, 320) < MaxVer, New Version(1, 8, 0, 320), MaxVer)
            ElseIf McInstanceCurrent.Version.McCodeMain >= 18 AndAlso McInstanceCurrent.Version.McCodeMain < 19 AndAlso McInstanceCurrent.Version.HasOptiFine Then '#305
                '1.18：若安装了 OptiFine，最高 Java 18
                MaxVer = If(New Version(18, 999, 999, 999) < MaxVer, New Version(18, 999, 999, 999), MaxVer)
            End If
        End If

        'Cleanroom 检测
        If McInstanceCurrent.Version.HasCleanroom Then
            '需要至少 Java 21
            MinVer = If(New Version(21, 0, 0, 0) > MinVer, New Version(21, 0, 0, 0), MinVer)
        End If

        'Fabric 检测
        If McInstanceCurrent.Version.HasFabric AndAlso McInstanceCurrent.Version.IsStandardVersion Then '不管非标准版本
            If McInstanceCurrent.Version.McCodeMain >= 15 AndAlso McInstanceCurrent.Version.McCodeMain <= 16 Then
                '1.15 - 1.16：Java 8+
                MinVer = If(New Version(1, 8, 0, 0) > MinVer, New Version(1, 8, 0, 0), MinVer)
            ElseIf McInstanceCurrent.Version.McCodeMain >= 18 Then
                '1.18+：Java 17+
                MinVer = If(New Version(17, 0, 0, 0) > MinVer, New Version(17, 0, 0, 0), MinVer)
            End If
        End If

        'LabyMod 检测
        If McInstanceCurrent.Version.HasLabyMod Then
            MinVer = If(New Version(21, 0, 0, 0) > MinVer, New Version(21, 0, 0, 0), MinVer)
            MaxVer = New Version(999, 999, 999, 999)
        End If

        'JSON 中要求的版本
        If McInstanceCurrent.JsonObject("javaVersion") IsNot Nothing Then
            MinVer = If(New Version(McInstanceCurrent.JsonObject("javaVersion")("majorVersion"), 0, 0, 0) > MinVer, New Version(McInstanceCurrent.JsonObject("javaVersion")("majorVersion"), 0, 0, 0), MinVer)
            If MaxVer < MinVer Then MaxVer = New Version(999, 999, 999, 999)
        End If

        SyncLock JavaLock

            '选择 Java
            McLaunchLog("Java 版本需求：最低 " & MinVer.ToString & "，最高 " & MaxVer.ToString)
            McLaunchJavaSelected = JavaSelect("$$", MinVer, MaxVer, McInstanceCurrent)
            If Task.IsAborted Then Return
            If McLaunchJavaSelected IsNot Nothing Then
                McLaunchLog("选择的 Java：" & McLaunchJavaSelected.ToString)
                Return
            End If

            '无合适的 Java
            If Task.IsAborted Then Return '中断加载会导致 JavaSelect 异常地返回空值，误判找不到 Java
            McLaunchLog("无合适的 Java，需要确认是否自动下载")
            Dim JavaCode As String
            If MinVer >= New Version(22, 0) Then '潜在的向后兼容
                JavaCode = MinVer.Minor
                If Not JavaDownloadConfirm("Java " & JavaCode) Then Throw New Exception("$$")
            ElseIf MinVer >= New Version(21, 0) Then
                JavaCode = 21
                If Not JavaDownloadConfirm("Java 21") Then Throw New Exception("$$")
            ElseIf MinVer >= New Version(1, 9) Then
                JavaCode = 17
                If Not JavaDownloadConfirm("Java 17") Then Throw New Exception("$$")
            ElseIf MaxVer < New Version(1, 8) Then
                JavaCode = 7
                If McInstanceCurrent.Version.HasForge Then
                    MyMsgBox("你需要先安装 LegacyJavaFixer Mod，或自行安装 Java 7，然后才能启动该版本。", "未找到 Java")
                Else
                    If Not JavaDownloadConfirm("Java 7", True) Then Throw New Exception("$$")
                End If
            ElseIf MinVer > New Version(1, 8, 0, 140) AndAlso MaxVer < New Version(1, 8, 0, 321) Then
                JavaCode = "8u141"
                If Not JavaDownloadConfirm("Java 8.0.141 ~ 8.0.320", True) Then Throw New Exception("$$")
            ElseIf MinVer > New Version(1, 8, 0, 140) Then
                JavaCode = "8u141"
                If Not JavaDownloadConfirm("Java 8.0.141 或更高版本的 Java 8", True) Then Throw New Exception("$$")
            ElseIf MaxVer < New Version(1, 8, 0, 321) Then
                JavaCode = 8
                If Not JavaDownloadConfirm("Java 8.0.320 或更低版本的 Java 8") Then Throw New Exception("$$")
            Else
                JavaCode = 8
                If Not JavaDownloadConfirm("Java 8") Then Throw New Exception("$$")
            End If

            '开始自动下载
            Dim JavaLoader = JavaFixLoaders(JavaCode)
            Try
                JavaLoader.Start(JavaCode, IsForceRestart:=True)
                Do While JavaLoader.State = LoadState.Loading AndAlso Not Task.IsAborted
                    Task.Progress = JavaLoader.Progress
                    Thread.Sleep(10)
                Loop
            Finally
                JavaLoader.Abort() '确保取消时中止 Java 下载
            End Try

            '检查下载结果
            McLaunchJavaSelected = JavaSelect("$$", MinVer, MaxVer, McInstanceCurrent)
            If Task.IsAborted Then Return
            If McLaunchJavaSelected IsNot Nothing Then
                McLaunchLog("选择的 Java：" & McLaunchJavaSelected.ToString())
            Else
                Hint("没有可用的 Java，已取消启动！", HintType.Critical)
                Throw New Exception("$$")
            End If

        End SyncLock
    End Sub

#End Region

#Region "启动参数"

    Public Class LaunchArgument
        Private _features As New List(Of String)
        Public Sub New(Minecraft As McInstance)
            Dim curArgu As String = String.Empty
            If Minecraft.IsOldJson Then
                _features = Minecraft.JsonObject("minecraftArguments").ToString.Split(" "c).ToList()
            Else
                For Each item In Minecraft.JsonObject("arguments")("game")
                    If item.Type = JTokenType.String Then
                        _features.Add(item.ToString)
                    ElseIf item.Type = JTokenType.Object Then
                        _features.AddRange(item("value").Select(Function(x) x.ToString))
                    End If
                Next
            End If
        End Sub

        Public Function HasArguments(key As String)
            Return _features.Contains(key)
        End Function
    End Class

    Private McLaunchArgument As String

    ''' <summary>
    ''' 释放 Java Wrapper 并返回完整文件路径。
    ''' </summary>
    Public Function ExtractJavaWrapper() As String
        Dim WrapperPath As String = PathPure & "JavaWrapper.jar"
        Log("[Java] 选定的 Java Wrapper 路径：" & WrapperPath)
        SyncLock ExtractJavaWrapperLock '避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
            Try
                WriteJavaWrapper(WrapperPath)
            Catch ex As Exception
                If File.Exists(WrapperPath) Then
                    '因为未知原因 Java Wrapper 可能变为只读文件（#4243）
                    Log(ex, "Java Wrapper 文件释放失败，但文件已存在，将在删除后尝试重新生成", LogLevel.Developer)
                    Try
                        File.Delete(WrapperPath)
                        WriteJavaWrapper(WrapperPath)
                    Catch ex2 As Exception
                        Log(ex2, "Java Wrapper 文件重新释放失败，将尝试更换文件名重新生成", LogLevel.Developer)
                        WrapperPath = PathPure & "JavaWrapper2.jar"
                        Try
                            WriteJavaWrapper(WrapperPath)
                        Catch ex3 As Exception
                            Throw New FileNotFoundException("释放 Java Wrapper 最终尝试失败", ex3)
                        End Try
                    End Try
                Else
                    Throw New FileNotFoundException("释放 Java Wrapper 失败", ex)
                End If
            End Try
        End SyncLock
        Return WrapperPath
    End Function
    Private ExtractJavaWrapperLock As New Object
    Private Sub WriteJavaWrapper(Path As String)
        WriteFile(Path, GetResourceStream("Resources/java-wrapper.jar"))
    End Sub

    ''' <summary>
    ''' 释放 linkd 并返回完整文件路径。
    ''' </summary>
    Public Function ExtractLinkD() As String
        Dim LinkDPath As String = PathPure & "linkd.exe"
        SyncLock ExtractLinkDLock '避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
            Try
                WriteLinkD(LinkDPath)
            Catch ex As Exception
                If File.Exists(LinkDPath) Then
                    Log(ex, "linkd 文件释放失败，但文件已存在，将在删除后尝试重新生成", LogLevel.Developer)
                    Try
                        File.Delete(LinkDPath)
                        WriteLinkD(LinkDPath)
                    Catch ex2 As Exception
                        Throw New FileNotFoundException("释放 linkd 失败", ex2)
                    End Try
                Else
                    Throw New FileNotFoundException("释放 linkd 失败", ex)
                End If
            End Try
        End SyncLock
        Return LinkDPath
    End Function
    Private ExtractLinkDLock As New Object
    Private Sub WriteLinkD(Path As String)
        WriteFile(Path, GetResourceStream("Resources/linkd.exe"))
    End Sub

    ''' <summary>
    ''' 判断是否使用 RetroWrapper。
    ''' </summary>
    Private Function McLaunchNeedsRetroWrapper(Mc As McInstance) As Boolean
        Return (Mc.ReleaseTime >= New Date(2013, 6, 25) AndAlso Mc.Version.McCodeMain = 99) OrElse
            (Mc.Version.McCodeMain < 6 AndAlso Mc.Version.McCodeMain <> 99) AndAlso
            Not Setup.Get("LaunchAdvanceDisableRW") AndAlso
            Not Setup.Get("VersionAdvanceDisableRW", Mc) '<1.6
    End Function


    '主方法，合并 Jvm、Game、Replace 三部分的参数数据
    Private Sub McLaunchArgumentMain(Loader As LoaderTask(Of String, List(Of McLibToken)))
        McLaunchLog("开始获取 Minecraft 启动参数")
        '获取基准字符串与参数信息
        Dim Arguments As String
        If McInstanceCurrent.JsonObject("arguments") IsNot Nothing AndAlso McInstanceCurrent.JsonObject("arguments")("jvm") IsNot Nothing Then
            McLaunchLog("获取新版 JVM 参数")
            Arguments = McLaunchArgumentsJvmNew(McInstanceCurrent)
            McLaunchLog("新版 JVM 参数获取成功：")
            McLaunchLog(Arguments)
        Else
            McLaunchLog("获取旧版 JVM 参数")
            Arguments = McLaunchArgumentsJvmOld(McInstanceCurrent)
            McLaunchLog("旧版 JVM 参数获取成功：")
            McLaunchLog(Arguments)
        End If
        If Not String.IsNullOrEmpty(McInstanceCurrent.JsonObject("minecraftArguments")) Then '有的实例 JSON 中是空字符串
            McLaunchLog("获取旧版 Game 参数")
            Arguments += " " & McLaunchArgumentsGameOld(McInstanceCurrent)
            McLaunchLog("旧版 Game 参数获取成功")
        End If
        If McInstanceCurrent.JsonObject("arguments") IsNot Nothing AndAlso McInstanceCurrent.JsonObject("arguments")("game") IsNot Nothing Then
            McLaunchLog("获取新版 Game 参数")
            Arguments += " " & McLaunchArgumentsGameNew(McInstanceCurrent)
            McLaunchLog("新版 Game 参数获取成功")
        End If
        '编码参数（#4700、#5892、#5909）
        If McLaunchJavaSelected.JavaMajorVersion > 8 Then
            If Not Arguments.Contains("-Dstdout.encoding=") Then Arguments = "-Dstdout.encoding=UTF-8 " & Arguments
            If Not Arguments.Contains("-Dstderr.encoding=") Then Arguments = "-Dstderr.encoding=UTF-8 " & Arguments
        End If
        If McLaunchJavaSelected.JavaMajorVersion >= 18 Then
            If Not Arguments.Contains("-Dfile.encoding=") Then Arguments = "-Dfile.encoding=COMPAT " & Arguments
        End If
        'MJSB
        Arguments = Arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=""Windows 10""")
        '全屏
        If Setup.Get("LaunchArgumentWindowType") = 0 Then Arguments += " --fullscreen"
        '由 Option 传入的额外参数
        For Each Arg In CurrentLaunchOptions.ExtraArgs
            Arguments += " " & Arg.Trim
        Next
        '自定义参数
        Dim ArgumentGame As String = Setup.Get("VersionAdvanceGame", instance:=McInstanceCurrent)
        Arguments += " " & If(ArgumentGame = "", Setup.Get("LaunchAdvanceGame"), ArgumentGame)
        '替换参数
        Dim ReplaceArguments = McLaunchArgumentsReplace(McInstanceCurrent, Loader)
        If String.IsNullOrWhiteSpace(ReplaceArguments("${version_type}")) Then
            '若自定义信息为空，则去掉该部分
            Arguments = Arguments.Replace(" --versionType ${version_type}", "")
            ReplaceArguments("${version_type}") = """"""
        End If
        Dim FinalArguments As String = ""
        For Each Argument In Arguments.Split(" ")
            For Each Entry As KeyValuePair(Of String, String) In ReplaceArguments
                Argument = Argument.Replace(Entry.Key, Entry.Value)
            Next
            If (Argument.Contains(" ") OrElse Argument.Contains(":\")) AndAlso Not Argument.EndsWithF("""") Then Argument = $"""{Argument}"""
            FinalArguments += Argument & " "
        Next
        FinalArguments = FinalArguments.TrimEnd()
        '进存档
        Dim WorldName As String = CurrentLaunchOptions.WorldName
        If WorldName IsNot Nothing Then
            FinalArguments += $" --quickPlaySingleplayer ""{WorldName}"""
        End If
        '进服
        Dim Server As String = If(String.IsNullOrEmpty(CurrentLaunchOptions.ServerIp), Setup.Get("VersionServerEnter", McInstanceCurrent), CurrentLaunchOptions.ServerIp)
        If String.IsNullOrWhiteSpace(WorldName) AndAlso Not String.IsNullOrWhiteSpace(Server) Then
            If McInstanceCurrent.ReleaseTime > New Date(2023, 4, 4) Then
                'QuickPlay
                FinalArguments += $" --quickPlayMultiplayer ""{Server}"""
            Else
                '老版本
                If Server.Contains(":") Then
                    '包含端口号
                    FinalArguments += " --server " & Server.Split(":")(0) & " --port " & Server.Split(":")(1)
                Else
                    '不包含端口号
                    FinalArguments += " --server " & Server & " --port 25565"
                End If
                If McInstanceCurrent.Version.HasOptiFine Then Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", HintType.Critical)
            End If
        End If
        '输出
        McLaunchLog("Minecraft 启动参数：")
        McLaunchLog(FinalArguments)
        McLaunchArgument = FinalArguments
    End Sub

    'Jvm 部分（第一段）
    Private Function McLaunchArgumentsJvmOld(instance As McInstance) As String
        '存储以空格为间隔的启动参数列表
        Dim DataList As New List(Of String)

        '输出固定参数
        DataList.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump")
        Dim ArgumentJvm As String = Setup.Get("VersionAdvanceJvm", instance:=McInstanceCurrent)
        If ArgumentJvm = "" Then ArgumentJvm = Setup.Get("LaunchAdvanceJvm")
        If Not ArgumentJvm.Contains("-Dlog4j2.formatMsgNoLookups=true") Then ArgumentJvm += " -Dlog4j2.formatMsgNoLookups=true"
        ArgumentJvm = ArgumentJvm.Replace(" -XX:MaxDirectMemorySize=256M", "") '#3511 的清理
        DataList.Insert(0, ArgumentJvm) '可变 JVM 参数
        DataList.Add("-Xmn" & Math.Floor(PageInstanceSetup.GetRam(McInstanceCurrent, Not McLaunchJavaSelected.Is64Bit) * 1024 * 0.15) & "m")
        DataList.Add("-Xmx" & Math.Floor(PageInstanceSetup.GetRam(McInstanceCurrent, Not McLaunchJavaSelected.Is64Bit) * 1024) & "m")
        DataList.Add("""-Djava.library.path=" & GetNativesFolder() & """")
        DataList.Add("-cp ${classpath}") '把支持库添加进启动参数表

        'Authlib-Injector
        If McLoginLoader.Output.Type = "Auth" Then
            If McLaunchJavaSelected.JavaMajorVersion >= 6 Then DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT") '信任系统根证书（Meloong-Git/#5252）
            Dim Server As String = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "")
            Try
                Dim Response As String = NetGetCodeByRequestRetry(Server, Encoding.UTF8)
                DataList.Insert(0, "-javaagent:""" & PathPure & "authlib-injector.jar""=" & Server &
                              " -Dauthlibinjector.side=client" &
                              " -Dauthlibinjector.yggdrasil.prefetched=" & Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)))
            Catch ex As HttpWebException
                Throw New Exception($"无法连接到第三方登录服务器（{If(Server, Nothing)}）{vbCrLf}详细信息：" & ex.InnerHttpException.WebResponse, ex)
            Catch ex As Exception
                Throw New Exception($"无法连接到第三方登录服务器（{If(Server, Nothing)}）", ex)
            End Try
        End If

        '渲染器
        Dim Renderer = Setup.Get("VersionAdvanceRenderer", instance:=McInstanceCurrent)
        Dim MesaLoaderWindowsVersion = "25.1.7"
        Dim MesaLoaderWindowsTargetFile = PathPure & "\mesa-loader-windows\" & MesaLoaderWindowsVersion & "\Loader.jar"

        If Renderer <> 0 Then
            DataList.Insert(0, "-javaagent:""" & MesaLoaderWindowsTargetFile & """=" & If(Renderer = 1, "llvmpipe", If(Renderer = 2, "d3d12", "zink")))
        End If

        '设置代理
        If Setup.Get("VersionAdvanceUseProxyV2", instance:=McInstanceCurrent) IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(Setup.Get("SystemHttpProxy")) Then
            Dim ProxyAddress As New Uri(Setup.Get("SystemHttpProxy"))
            DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyHost={ProxyAddress.AbsoluteUri}")
            DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyPort={ProxyAddress.Port}")
        End If
        '添加 Java Wrapper 作为主 Jar
        If IsUtf8CodePage() AndAlso Not Setup.Get("LaunchAdvanceDisableJLW") AndAlso Not Setup.Get("VersionAdvanceDisableJLW", McInstanceCurrent) Then
            If McLaunchJavaSelected.JavaMajorVersion >= 9 Then DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED")
            DataList.Add("-Doolloo.jlw.tmpdir=""" & PathPure.TrimEnd("\") & """")
            DataList.Add("-jar """ & ExtractJavaWrapper() & """")
        End If

        '添加 MainClass
        If instance.JsonObject("mainClass") Is Nothing Then
            Throw New Exception("实例 JSON 中没有 mainClass 项！")
        Else
            DataList.Add(instance.JsonObject("mainClass"))
        End If

        Return Join(DataList, " ")
    End Function
    Private Function McLaunchArgumentsJvmNew(instance As McInstance) As String
        Dim DataList As New List(Of String)

        '获取 Json 中的 DataList
        Dim currentInstance As McInstance = instance
NextInstance:
        If currentInstance.JsonObject("arguments") IsNot Nothing AndAlso currentInstance.JsonObject("arguments")("jvm") IsNot Nothing Then
            For Each SubJson As JToken In currentInstance.JsonObject("arguments")("jvm")
                If SubJson.Type = JTokenType.String Then
                    '字符串类型
                    DataList.Add(SubJson.ToString)
                Else
                    '非字符串类型
                    If McJsonRuleCheck(SubJson("rules")) Then
                        '满足准则
                        If SubJson("value").Type = JTokenType.String Then
                            DataList.Add(SubJson("value").ToString)
                        Else
                            For Each value As JToken In SubJson("value")
                                DataList.Add(value.ToString)
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If currentInstance.InheritInstance <> "" Then
            currentInstance = New McInstance(currentInstance.InheritInstance)
            GoTo NextInstance
        End If

        '内存、Log4j 防御参数等
        SecretLaunchJvmArgs(DataList)

        'Authlib-Injector
        If McLoginLoader.Output.Type = "Auth" Then
            If McLaunchJavaSelected.JavaMajorVersion >= 6 Then DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT") '信任系统根证书（Meloong-Git/#5252）
            Dim Server As String = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "")
            Try
                Dim Response As String = NetGetCodeByRequestRetry(Server, Encoding.UTF8)
                DataList.Insert(0, "-javaagent:""" & PathPure & "authlib-injector.jar""=" & Server &
                              " -Dauthlibinjector.side=client" &
                              " -Dauthlibinjector.yggdrasil.prefetched=" & Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)))
            Catch ex As Exception
                Throw New Exception("无法连接到第三方登录服务器（" & If(Server, Nothing) & "）", ex)
            End Try
        End If

        '渲染器
        Dim Renderer = Setup.Get("VersionAdvanceRenderer", instance:=McInstanceCurrent)
        Dim MesaLoaderWindowsVersion = "25.1.7"
        Dim MesaLoaderWindowsTargetFile = PathPure & "\mesa-loader-windows\" & MesaLoaderWindowsVersion & "\Loader.jar"

        If Renderer <> 0 Then
            DataList.Insert(0, "-javaagent:""" & MesaLoaderWindowsTargetFile & """=" & If(Renderer = 1, "llvmpipe", If(Renderer = 2, "d3d12", "zink")))
        End If

        '设置代理
        If Setup.Get("VersionAdvanceUseProxyV2", instance:=McInstanceCurrent) IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(Setup.Get("SystemHttpProxy")) Then
            Dim ProxyAddress As New Uri(Setup.Get("SystemHttpProxy"))
            DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyHost={ProxyAddress.AbsoluteUri}")
            DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyPort={ProxyAddress.Port}")
        End If
        '添加 RetroWrapper 相关参数
        If McLaunchNeedsRetroWrapper(instance) Then
            'https://github.com/NeRdTheNed/RetroWrapper/wiki/RetroWrapper-flags
            DataList.Add("-Dretrowrapper.doUpdateCheck=false")
        End If
        '添加 Java Wrapper 作为主 Jar
        If IsUtf8CodePage() AndAlso Not Setup.Get("LaunchAdvanceDisableJLW") AndAlso Not Setup.Get("VersionAdvanceDisableJLW", McInstanceCurrent) Then
            If McLaunchJavaSelected.JavaMajorVersion >= 9 Then DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED")
            DataList.Add("-Doolloo.jlw.tmpdir=""" & PathPure.TrimEnd("\") & """")
            DataList.Add("-jar """ & ExtractJavaWrapper() & """")
        End If


        '将 "-XXX" 与后面 "XXX" 合并到一起
        '如果不合并，会导致 Forge 1.17 启动无效，它有两个 --add-exports，进一步导致其中一个在后面被去重
        Dim DeDuplicateDataList As New List(Of String)
        For i = 0 To DataList.Count - 1
            Dim CurrentEntry As String = DataList(i)
            If DataList(i).StartsWithF("-") Then
                Do While i < DataList.Count - 1
                    If DataList(i + 1).StartsWithF("-") Then
                        Exit Do
                    Else
                        i += 1
                        CurrentEntry += " " + DataList(i)
                    End If
                Loop
            End If
            DeDuplicateDataList.Add(CurrentEntry.Trim.Replace("McEmu= ", "McEmu="))
        Next

        '#3511 的清理
        DeDuplicateDataList.Remove("-XX:MaxDirectMemorySize=256M")

        '去重
        Dim Result As String = Join(DeDuplicateDataList.Distinct.ToList, " ")

        '添加 MainClass
        If instance.JsonObject("mainClass") Is Nothing Then
            Throw New Exception("实例 JSON 中没有 mainClass 项！")
        Else
            Result += " " & instance.JsonObject("mainClass").ToString
        End If

        Return Result
    End Function

    'Game 部分（第二段）
    Private Function McLaunchArgumentsGameOld(Version As McInstance) As String
        Dim DataList As New List(Of String)

        '添加 RetroWrapper 相关参数
        If McLaunchNeedsRetroWrapper(Version) Then
            DataList.Add("--tweakClass com.zero.retrowrapper.RetroTweaker")
        End If

        '本地化 Minecraft 启动信息
        Dim BasicString As String = Version.JsonObject("minecraftArguments").ToString
        If Not BasicString.Contains("--height") Then BasicString += " --height ${resolution_height} --width ${resolution_width}"
        DataList.Add(BasicString)

        Dim Result As String = Join(DataList, " ")

        '特别改变 OptiFineTweaker
        If (Version.Version.HasForge OrElse Version.Version.HasLiteLoader) AndAlso Version.Version.HasOptiFine Then
            '把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            If Result.Contains("--tweakClass optifine.OptiFineForgeTweaker") Then
                Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" & Result)
                Result = Result.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "").Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
            End If
            If Result.Contains("--tweakClass optifine.OptiFineTweaker") Then
                Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" & Result)
                Result = Result.Replace(" --tweakClass optifine.OptiFineTweaker", "").Replace("--tweakClass optifine.OptiFineTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
                Try
                    WriteFile(Version.Path & Version.Name & ".json", ReadFile(Version.Path & Version.Name & ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"))
                Catch ex As Exception
                    Log(ex, "替换 OptiFineForge TweakClass 失败")
                End Try
            End If
        End If

        Return Result
    End Function
    Private Function McLaunchArgumentsGameNew(instance As McInstance) As String
        Dim dataList As New List(Of String)

        '获取 Json 中的 DataList
        Dim currentInstance As McInstance = instance
NextInstance:
        If currentInstance.JsonObject("arguments") IsNot Nothing AndAlso currentInstance.JsonObject("arguments")("game") IsNot Nothing Then
            For Each SubJson As JToken In currentInstance.JsonObject("arguments")("game")
                If SubJson.Type = JTokenType.String Then
                    '字符串类型
                    DataList.Add(SubJson.ToString)
                Else
                    '非字符串类型
                    If McJsonRuleCheck(SubJson("rules")) Then
                        '满足准则
                        If SubJson("value").Type = JTokenType.String Then
                            DataList.Add(SubJson("value").ToString)
                        Else
                            For Each value As JToken In SubJson("value")
                                DataList.Add(value.ToString)
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If currentInstance.InheritInstance <> "" Then
            currentInstance = New McInstance(currentInstance.InheritInstance)
            GoTo NextInstance
        End If

        '将 "-XXX" 与后面 "XXX" 合并到一起
        '如果不进行合并 Impact 会启动无效，它有两个 --tweakclass
        Dim DeDuplicateDataList As New List(Of String)
        For i = 0 To DataList.Count - 1
            Dim CurrentEntry As String = DataList(i)
            If DataList(i).StartsWithF("-") Then
                Do While i < DataList.Count - 1
                    If DataList(i + 1).StartsWithF("-") Then
                        Exit Do
                    Else
                        i += 1
                        CurrentEntry += " " + DataList(i)
                    End If
                Loop
            End If
            DeDuplicateDataList.Add(CurrentEntry)
        Next
        '去重
        McLaunchArgumentsGameNew = Join(DeDuplicateDataList.Distinct.ToList, " ")

        '特别改变 OptiFineTweaker
        If (instance.Version.HasForge OrElse instance.Version.HasLiteLoader) AndAlso instance.Version.HasOptiFine Then
            '把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            If McLaunchArgumentsGameNew.Contains("--tweakClass optifine.OptiFineForgeTweaker") Then
                Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" & McLaunchArgumentsGameNew)
                McLaunchArgumentsGameNew = McLaunchArgumentsGameNew.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "").Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
            End If
            If McLaunchArgumentsGameNew.Contains("--tweakClass optifine.OptiFineTweaker") Then
                Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" & McLaunchArgumentsGameNew)
                McLaunchArgumentsGameNew = McLaunchArgumentsGameNew.Replace(" --tweakClass optifine.OptiFineTweaker", "").Replace("--tweakClass optifine.OptiFineTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
                Try
                    WriteFile(instance.Path & instance.Name & ".json", ReadFile(instance.Path & instance.Name & ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"))
                Catch ex As Exception
                    Log(ex, "替换 OptiFineForge TweakClass 失败")
                End Try
            End If
        End If
    End Function

    '替换 Arguments
    Private Function McLaunchArgumentsReplace(instance As McInstance, ByRef loader As LoaderTask(Of String, List(Of McLibToken))) As Dictionary(Of String, String)
        Dim GameArguments As New Dictionary(Of String, String)

        '基础参数
        GameArguments.Add("${classpath_separator}", ";")
        GameArguments.Add("${natives_directory}", ShortenPath(GetNativesFolder()))
        GameArguments.Add("${library_directory}", ShortenPath(PathMcFolder & "libraries"))
        GameArguments.Add("${libraries_directory}", ShortenPath(PathMcFolder & "libraries"))
        GameArguments.Add("${launcher_name}", "PCLCE")
        GameArguments.Add("${launcher_version}", VersionCode)
        GameArguments.Add("${version_name}", instance.Name)
        Dim ArgumentInfo As String = Setup.Get("VersionArgumentInfo", instance:=McInstanceCurrent)
        GameArguments.Add("${version_type}", If(ArgumentInfo = "", Setup.Get("LaunchArgumentInfo"), ArgumentInfo))
        GameArguments.Add("${game_directory}", ShortenPath(Left(McInstanceCurrent.PathIndie, McInstanceCurrent.PathIndie.Count - 1)))
        GameArguments.Add("${assets_root}", ShortenPath(PathMcFolder & "assets"))
        GameArguments.Add("${user_properties}", "{}")
        GameArguments.Add("${auth_player_name}", McLoginLoader.Output.Name)
        GameArguments.Add("${auth_uuid}", McLoginLoader.Output.Uuid)
        GameArguments.Add("${auth_access_token}", McLoginLoader.Output.AccessToken)
        GameArguments.Add("${access_token}", McLoginLoader.Output.AccessToken)
        GameArguments.Add("${auth_session}", McLoginLoader.Output.AccessToken)
        GameArguments.Add("${user_type}", "msa") '#1221

        '窗口尺寸参数
        Dim GameSize As Size
        Select Case Setup.Get("LaunchArgumentWindowType")
            Case 2 '与启动器尺寸一致
                Dim Result As Size
                RunInUiWait(Sub() Result = New Size(GetPixelSize(FrmMain.PanForm.ActualWidth), GetPixelSize(FrmMain.PanForm.ActualHeight)))
                GameSize = Result
                GameSize.Height -= 29.5 * DPI / 96 '标题栏高度
            Case 3 '自定义
                GameSize = New Size(Math.Max(100, Setup.Get("LaunchArgumentWindowWidth")), Math.Max(100, Setup.Get("LaunchArgumentWindowHeight")))
            Case Else
                GameSize = New Size(854, 480)
        End Select
        If McInstanceCurrent.Version.McCodeMain <= 12 AndAlso
            McLaunchJavaSelected.JavaMajorVersion <= 8 AndAlso McLaunchJavaSelected.Version.Revision >= 200 AndAlso McLaunchJavaSelected.Version.Revision <= 321 AndAlso
            Not McInstanceCurrent.Version.HasOptiFine AndAlso Not McInstanceCurrent.Version.HasForge Then
            '修复 #3463：1.12.2-，JRE 8u200~321 下窗口大小为设置大小的 DPI% 倍
            McLaunchLog($"已应用窗口大小过大修复（{McLaunchJavaSelected.Version.Revision}）")
            GameSize.Width /= DPI / 96
            GameSize.Height /= DPI / 96
        End If
        GameArguments.Add("${resolution_width}", Math.Round(GameSize.Width))
        GameArguments.Add("${resolution_height}", Math.Round(GameSize.Height))

        'Assets 相关参数
        GameArguments.Add("${game_assets}", ShortenPath(PathMcFolder & "assets\virtual\legacy")) '1.5.2 的 pre-1.6 资源索引应与 legacy 合并
        GameArguments.Add("${assets_index_name}", McAssetsGetIndexName(instance))

        '支持库参数
        Dim LibList As List(Of McLibToken) = McLibListGet(instance, True)
        loader.Output = LibList
        Dim CpStrings As New List(Of String)
        Dim OptiFineCp As String = Nothing

        'RetroWrapper 释放
        If McLaunchNeedsRetroWrapper(instance) Then
            Dim WrapperPath As String = PathMcFolder & "libraries\retrowrapper\RetroWrapper.jar"
            Try
                WriteFile(WrapperPath, GetResourceStream("Resources/retro-wrapper.jar"))
                CpStrings.Add(WrapperPath)
            Catch ex As Exception
                Log(ex, "RetroWrapper 释放失败")
            End Try
        End If

        For Each Library As McLibToken In LibList
            If Library.IsNatives Then Continue For
            If Library.Name IsNot Nothing AndAlso Library.Name.Contains("com.cleanroommc:cleanroom:0.2") Then 'Cleanroom 的主 Jar 必须放在 ClassPath 第一位
                CpStrings.Insert(0, Library.LocalPath)
            End If
            If Library.Name IsNot Nothing AndAlso Library.Name = "optifine:OptiFine" Then
                OptiFineCp = Library.LocalPath
            Else
                CpStrings.Add(Library.LocalPath)
            End If
        Next
        If OptiFineCp IsNot Nothing Then CpStrings.Insert(CpStrings.Count - 2, OptiFineCp) 'OptiFine 的总是需要放到倒数第二位
        GameArguments.Add("${classpath}", Join(CpStrings.Select(Function(c) ShortenPath(c)), ";"))

        Return GameArguments
    End Function

#End Region

#Region "解压 Natives"

    Private Sub McLaunchNatives(Loader As LoaderTask(Of List(Of McLibToken), Integer))

        '创建文件夹
        Dim Target As String = GetNativesFolder() & "\"
        Directory.CreateDirectory(Target)

        '解压文件
        McLaunchLog("正在解压 Natives 文件")
        Dim ExistFiles As New List(Of String)
        For Each Native As McLibToken In Loader.Input
            If Not Native.IsNatives Then Continue For
            Dim Zip As ZipArchive
            Try
                Zip = New ZipArchive(New FileStream(Native.LocalPath, FileMode.Open))
            Catch ex As InvalidDataException
                Log(ex, "打开 Natives 文件失败（" & Native.LocalPath & "）")
                File.Delete(Native.LocalPath)
                Throw New Exception("无法打开 Natives 文件（" & Native.LocalPath & "），该文件可能已损坏，请重新尝试启动游戏")
            End Try
            For Each Entry In Zip.Entries
                Dim FileName As String = Entry.FullName
                If FileName.EndsWithF(".dll", True) Then
                    '实际解压文件的步骤
                    Dim FilePath As String = Target & FileName
                    ExistFiles.Add(FilePath)
                    Dim OriginalFile As New FileInfo(FilePath)
                    If OriginalFile.Exists Then
                        If OriginalFile.Length = Entry.Length Then
                            If ModeDebug Then McLaunchLog("无需解压：" & FilePath)
                            Continue For
                        End If
                        '删除原文件
                        Try
                            File.Delete(FilePath)
                        Catch ex As UnauthorizedAccessException
                            McLaunchLog("删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" & FilePath)
                            McLaunchLog("实际的错误信息：" & GetExceptionSummary(ex))
                            Exit For
                        End Try
                    End If
                    '解压新文件
                    WriteFile(FilePath, Entry.Open)
                    McLaunchLog("已解压：" & FilePath)
                End If
            Next
            If Zip IsNot Nothing Then Zip.Dispose()
        Next

        '删除多余文件
        For Each FileName As String In Directory.GetFiles(Target)
            If ExistFiles.Contains(FileName) Then Continue For
            Try
                McLaunchLog("删除：" & FileName)
                File.Delete(FileName)
            Catch ex As UnauthorizedAccessException
                McLaunchLog("删除多余文件访问被拒绝，跳过删除步骤")
                McLaunchLog("实际的错误信息：" & GetExceptionSummary(ex))
                Return
            End Try
        Next

    End Sub
    ''' <summary>
    ''' 获取 Natives 文件夹路径，不以 \ 结尾。
    ''' </summary>
    Private Function GetNativesFolder() As String
        Dim Result As String = McInstanceCurrent.Path & McInstanceCurrent.Name & "-natives"
        If IsGBKEncoding OrElse Result.IsASCII() Then Return Result
        Result = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\bin\natives"
        If Result.IsASCII() Then Return Result
        Return OsDrive & "ProgramData\PCL\natives"
    End Function

#End Region

#Region "启动与前后处理"

    Private Sub McLaunchPrerun()

        '要求 Java 使用高性能显卡
        Try
            SetGPUPreference(McLaunchJavaSelected.JavawExePath, Setup.Get("LaunchAdvanceGraphicCard"))
            SetGPUPreference(PathWithName, Setup.Get("LaunchAdvanceGraphicCard"))
        Catch ex As Exception
            If IsAdmin() Then
                Log(ex, "直接调整显卡设置失败")
            Else
                Log(ex, "直接调整显卡设置失败，将以管理员权限重启 PCL 再次尝试")
                Try
                    If RunAsAdmin($"--gpu ""{McLaunchJavaSelected.JavawExePath}""") = ProcessReturnValues.TaskDone Then
                        McLaunchLog("以管理员权限重启 PCL 并调整显卡设置成功")
                    Else
                        Throw New Exception("调整过程中出现异常")
                    End If
                Catch exx As Exception
                    Log(exx, "调整显卡设置失败，Minecraft 可能会使用默认显卡运行", LogLevel.Hint)
                End Try
            End If
        End Try

        '更新 launcher_profiles.json
        Try
            '确保可用
            If Not McLoginLoader.Output.Type = "Microsoft" Then Exit Try
            McFolderLauncherProfilesJsonCreate(PathMcFolder)
            '构建需要替换的 Json 对象
            Dim ReplaceJsonString As String = "
            {
              ""authenticationDatabase"": {
                ""00000111112222233333444445555566"": {
                  ""username"": """ & McLoginLoader.Output.Name.Replace("""", "-") & """,
                  ""profiles"": {
                    ""66666555554444433333222221111100"": {
                        ""displayName"": """ & McLoginLoader.Output.Name & """
                    }
                  }
                }
              },
              ""clientToken"": """ & McLoginLoader.Output.ClientToken & """,
              ""selectedUser"": {
                ""account"": ""00000111112222233333444445555566"", 
                ""profile"": ""66666555554444433333222221111100""
              }
            }"
            Dim ReplaceJson As JObject = GetJson(ReplaceJsonString)
            '更新文件
            Dim Profiles As JObject = GetJson(ReadFile(PathMcFolder & "launcher_profiles.json"))
            Profiles.Merge(ReplaceJson)
            WriteFile(PathMcFolder & "launcher_profiles.json", Profiles.ToString, Encoding:=Encoding.GetEncoding("GB18030"))
            McLaunchLog("已更新 launcher_profiles.json")
        Catch ex As Exception
            Log(ex, "更新 launcher_profiles.json 失败，将在删除文件后重试")
            Try
                File.Delete(PathMcFolder & "launcher_profiles.json")
                McFolderLauncherProfilesJsonCreate(PathMcFolder)
                '构建需要替换的 Json 对象
                Dim ReplaceJsonString As String = "
                    {
                      ""authenticationDatabase"": {
                        ""00000111112222233333444445555566"": {
                          ""username"": """ & McLoginLoader.Output.Name.Replace("""", "-") & """,
                          ""profiles"": {
                            ""66666555554444433333222221111100"": {
                                ""displayName"": """ & McLoginLoader.Output.Name & """
                            }
                          }
                        }
                      },
                      ""clientToken"": """ & McLoginLoader.Output.ClientToken & """,
                      ""selectedUser"": {
                        ""account"": ""00000111112222233333444445555566"", 
                        ""profile"": ""66666555554444433333222221111100""
                      }
                    }"
                Dim ReplaceJson As JObject = GetJson(ReplaceJsonString)
                '更新文件
                Dim Profiles As JObject = GetJson(ReadFile(PathMcFolder & "launcher_profiles.json"))
                Profiles.Merge(ReplaceJson)
                WriteFile(PathMcFolder & "launcher_profiles.json", Profiles.ToString, Encoding:=Encoding.GetEncoding("GB18030"))
                McLaunchLog("已在删除后更新 launcher_profiles.json")
            Catch exx As Exception
                Log(exx, "更新 launcher_profiles.json 失败", LogLevel.Feedback)
            End Try
        End Try

        '更新 options.txt
        Dim SetupFileAddress As String = McInstanceCurrent.PathIndie & "options.txt"
        If Not File.Exists(SetupFileAddress) Then
            'Yosbr Mod 兼容（#2385）：https://www.curseforge.com/minecraft/mc-mods/yosbr
            Dim YosbrFileAddress As String = McInstanceCurrent.PathIndie & "config\yosbr\options.txt"
            If File.Exists(YosbrFileAddress) Then
                McLaunchLog("将修改 Yosbr Mod 中的 options.txt")
                SetupFileAddress = YosbrFileAddress
                WriteIni(SetupFileAddress, "lang", "none") '忽略默认语言
            End If
        End If
        Try
            '语言
            '1.0-     ：没有语言选项
            '1.1 ~ 5  ：zh_CN 时正常，zh_cn 时崩溃（最后两位字母必须大写，否则将会 NPE 崩溃）
            '1.6 ~ 10 ：zh_CN 时正常，zh_cn 时自动切换为英文
            '1.11 ~ 12：zh_cn 时正常，zh_CN 时虽然显示了中文但语言设置会错误地显示选择英文
            '1.13+    ：zh_cn 时正常，zh_CN 时自动切换为英文
            Dim CurrentLang As String = ReadIni(SetupFileAddress, "lang", "none")
            Dim RequiredLang As String = If(CurrentLang = "none" OrElse Not Directory.Exists(McInstanceCurrent.PathIndie & "saves"), '#3844，整合包可能已经自带了 options.txt
                If(Setup.Get("ToolHelpChinese"), "zh_cn", "en_us"), CurrentLang.ToLower)
            If McInstanceCurrent.Version.McCodeMain < 12 Then '注意老版本（包含 MC 1.1）的 McCodeMain 可能为 -1
                '将最后两位改为大写，前面的部分保留
                RequiredLang = RequiredLang.Substring(0, RequiredLang.Length - 2) & RequiredLang.Substring(RequiredLang.Length - 2).ToUpper
            End If
            If CurrentLang = RequiredLang Then
                McLaunchLog($"需要的语言为 {RequiredLang}，当前语言为 {CurrentLang}，无需修改")
            Else
                WriteIni(SetupFileAddress, "lang", "-") '触发缓存更改，避免删除后重新下载残留缓存
                WriteIni(SetupFileAddress, "lang", RequiredLang)
                McLaunchLog($"已将语言从 {CurrentLang} 修改为 {RequiredLang}")
            End If
            ''如果是初次设置，一并修改 forceUnicodeFont
            'If Setup.Get("ToolHelpChinese") AndAlso (CurrentLang = "none" OrElse Not Directory.Exists(McInstanceCurrent.PathIndie & "saves")) Then
            '    WriteIni(SetupFileAddress, "forceUnicodeFont", "true")
            '    McLaunchLog("已开启 forceUnicodeFont")
            'End If
            '窗口
            Select Case Setup.Get("LaunchArgumentWindowType")
                Case 0 '全屏
                    WriteIni(SetupFileAddress, "fullscreen", "true")
                Case 1 '默认
                Case Else '其他
                    WriteIni(SetupFileAddress, "fullscreen", "false")
            End Select
        Catch ex As Exception
            Log(ex, "更新 options.txt 失败", LogLevel.Hint)
        End Try

    End Sub
    Private Sub McLaunchCustom(Loader As LoaderTask(Of Integer, Integer))

        '获取自定义命令
        Dim CustomCommandGlobal As String = Setup.Get("LaunchAdvanceRun")
        If CustomCommandGlobal <> "" Then CustomCommandGlobal = ArgumentReplace(CustomCommandGlobal, True)
        Dim CustomCommandVersion As String = Setup.Get("VersionAdvanceRun", instance:=McInstanceCurrent)
        If CustomCommandVersion <> "" Then CustomCommandVersion = ArgumentReplace(CustomCommandVersion, True)

        '输出 bat
        Try
            Dim CmdString As String =
                $"{If(McLaunchJavaSelected.JavaMajorVersion > 8, "chcp 65001>nul" & vbCrLf, "")}" &
                "@echo off" & vbCrLf &
                $"title 启动 - {McInstanceCurrent.Name}" & vbCrLf &
                "echo 游戏正在启动，请稍候。" & vbCrLf &
                $"cd /D ""{ShortenPath(McInstanceCurrent.PathIndie)}""" & vbCrLf &
                CustomCommandGlobal & vbCrLf &
                CustomCommandVersion & vbCrLf &
                $"""{McLaunchJavaSelected.JavaExePath}"" {McLaunchArgument}" & vbCrLf &
                "echo 游戏已退出。" & vbCrLf &
                "pause"
            WriteFile(If(CurrentLaunchOptions.SaveBatch, Path & "PCL\LatestLaunch.bat"), FilterAccessToken(CmdString, "F"),
                      Encoding:=If(McLaunchJavaSelected.JavaMajorVersion > 8, Encoding.UTF8, Encoding.Default))
            If CurrentLaunchOptions.SaveBatch IsNot Nothing Then
                McLaunchLog("导出启动脚本完成，强制结束启动过程")
                AbortHint = "导出启动脚本成功！"
                OpenExplorer(CurrentLaunchOptions.SaveBatch)
                Loader.Parent.Abort()
                Return '导出脚本完成
            End If
        Catch ex As Exception
            Log(ex, "输出启动脚本失败")
            If CurrentLaunchOptions.SaveBatch IsNot Nothing Then Throw '直接触发启动失败
        End Try

        '执行自定义命令
        If CustomCommandGlobal <> "" Then
            McLaunchLog("正在执行全局自定义命令：" & CustomCommandGlobal)
            Dim CustomProcess As New Process
            Try
                CustomProcess.StartInfo.FileName = "cmd.exe"
                CustomProcess.StartInfo.Arguments = "/c """ & CustomCommandGlobal & """"
                CustomProcess.StartInfo.WorkingDirectory = ShortenPath(PathMcFolder)
                CustomProcess.StartInfo.UseShellExecute = False
                CustomProcess.StartInfo.CreateNoWindow = True
                CustomProcess.Start()
                If Setup.Get("LaunchAdvanceRunWait") Then
                    Do Until CustomProcess.HasExited OrElse Loader.IsAborted
                        Thread.Sleep(10)
                    Loop
                End If
            Catch ex As Exception
                Log(ex, "执行全局自定义命令失败", LogLevel.Hint)
            Finally
                If Not CustomProcess.HasExited AndAlso Loader.IsAborted Then
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程") '#1183
                    CustomProcess.Kill()
                End If
            End Try
        End If
        If CustomCommandVersion <> "" Then
            McLaunchLog("正在执行实例自定义命令：" & CustomCommandVersion)
            Dim CustomProcess As New Process
            Try
                CustomProcess.StartInfo.FileName = "cmd.exe"
                CustomProcess.StartInfo.Arguments = "/c """ & CustomCommandVersion & """"
                CustomProcess.StartInfo.WorkingDirectory = ShortenPath(PathMcFolder)
                CustomProcess.StartInfo.UseShellExecute = False
                CustomProcess.StartInfo.CreateNoWindow = True
                CustomProcess.Start()
                If Setup.Get("VersionAdvanceRunWait", instance:=McInstanceCurrent) Then
                    Do Until CustomProcess.HasExited OrElse Loader.IsAborted
                        Thread.Sleep(10)
                    Loop
                End If
            Catch ex As Exception
                Log(ex, "执行实例自定义命令失败", LogLevel.Hint)
            Finally
                If Not CustomProcess.HasExited AndAlso Loader.IsAborted Then
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程") '#1183
                    CustomProcess.Kill()
                End If
            End Try
        End If

    End Sub
    Private Sub McLaunchRun(Loader As LoaderTask(Of Integer, Process))
        Dim noJavaw As Boolean = Setup.Get("LaunchAdvanceNoJavaw")

        '启动信息
        Dim GameProcess = New Process()
        Dim StartInfo As New ProcessStartInfo(If(noJavaw, McLaunchJavaSelected.JavaExePath, McLaunchJavaSelected.JavawExePath))

        '设置环境变量
        Dim Paths As New List(Of String)(StartInfo.EnvironmentVariables("Path").Split(";"))
        Paths.Add(ShortenPath(McLaunchJavaSelected.JavaFolder))
        StartInfo.EnvironmentVariables("Path") = Join(Paths.Distinct.ToList, ";")
        StartInfo.EnvironmentVariables("appdata") = ShortenPath(PathMcFolder)

        '设置其他参数
        StartInfo.WorkingDirectory = ShortenPath(McInstanceCurrent.PathIndie)
        StartInfo.UseShellExecute = False
        StartInfo.RedirectStandardOutput = True
        StartInfo.RedirectStandardError = True
        StartInfo.CreateNoWindow = noJavaw
        StartInfo.Arguments = McLaunchArgument
        GameProcess.StartInfo = StartInfo

        '开始进程
        GameProcess.Start()
        McLaunchLog("已启动游戏进程：" & McLaunchJavaSelected.JavawExePath)
        If Loader.IsAborted Then
            McLaunchLog("由于取消启动，已强制结束游戏进程") '#1631
            GameProcess.Kill()
            Return
        End If
        Loader.Output = GameProcess
        McLaunchProcess = GameProcess
        '进程优先级处理
        Try
            GameProcess.PriorityBoostEnabled = True
            Select Case Setup.Get("LaunchArgumentPriority")
                Case 0 '高
                    GameProcess.PriorityClass = ProcessPriorityClass.AboveNormal
                Case 2 '低
                    GameProcess.PriorityClass = ProcessPriorityClass.BelowNormal
                Case Else '中
            End Select
        Catch ex As Exception
            Log(ex, "设置进程优先级失败", LogLevel.Feedback)
        End Try

    End Sub
    Private Sub McLaunchWait(Loader As LoaderTask(Of Process, Integer))

        '输出信息
        McLaunchLog("")
        McLaunchLog("~ 基础参数 ~")
        McLaunchLog("PCL 版本：" & VersionBaseName & " (" & VersionCode & ")")
        McLaunchLog("游戏版本：" & McInstanceCurrent.Version.ToString & "（识别为 1." & McInstanceCurrent.Version.McCodeMain & "." & McInstanceCurrent.Version.McCodeSub & "）")
        McLaunchLog("资源版本：" & McAssetsGetIndexName(McInstanceCurrent))
        McLaunchLog("实例继承：" & If(McInstanceCurrent.InheritInstance = "", "无", McInstanceCurrent.InheritInstance))
        McLaunchLog("分配的内存：" & PageInstanceSetup.GetRam(McInstanceCurrent, Not McLaunchJavaSelected.Is64Bit) & " GB（" & Math.Round(PageInstanceSetup.GetRam(McInstanceCurrent, Not McLaunchJavaSelected.Is64Bit) * 1024) & " MB）")
        McLaunchLog("MC 文件夹：" & PathMcFolder)
        McLaunchLog("实例文件夹：" & McInstanceCurrent.Path)
        McLaunchLog("版本隔离：" & (McInstanceCurrent.PathIndie = McInstanceCurrent.Path))
        McLaunchLog("HMCL 格式：" & McInstanceCurrent.IsHmclFormatJson)
        McLaunchLog("Java 信息：" & If(McLaunchJavaSelected IsNot Nothing, McLaunchJavaSelected.ToString, "无可用 Java"))
        'McLaunchLog("环境变量：" & If(McLaunchJavaSelected IsNot Nothing, If(McLaunchJavaSelected.HasEnvironment, "已设置", "未设置"), "未设置"))
        McLaunchLog("Natives 文件夹：" & GetNativesFolder())
        McLaunchLog("")
        McLaunchLog("~ 档案参数 ~")
        McLaunchLog("玩家用户名：" & McLoginLoader.Output.Name)
        McLaunchLog("AccessToken：" & McLoginLoader.Output.AccessToken)
        McLaunchLog("ClientToken：" & McLoginLoader.Output.ClientToken)
        McLaunchLog("UUID：" & McLoginLoader.Output.Uuid)
        McLaunchLog("验证方式：" & McLoginLoader.Output.Type)
        McLaunchLog("")

        '获取窗口标题
        Dim WindowTitle As String = Setup.Get("VersionArgumentTitle", instance:=McInstanceCurrent)
        If WindowTitle = "" AndAlso Not Setup.Get("VersionArgumentTitleEmpty", instance:=McInstanceCurrent) Then WindowTitle = Setup.Get("LaunchArgumentTitle")
        WindowTitle = ArgumentReplace(WindowTitle, False)

        'JStack 路径
        Dim JStackPath As String = McLaunchJavaSelected.JavaFolder & "\jstack.exe"

        '初始化等待
        Dim Watcher As New Watcher(Loader, McInstanceCurrent, WindowTitle, If(File.Exists(JStackPath), JStackPath, ""), CurrentLaunchOptions.Test)
        McLaunchWatcher = Watcher

        '显示实时日志
        If CurrentLaunchOptions.Test Then
            If FrmLogLeft Is Nothing Then RunInUiWait(Sub() FrmLogLeft = New PageLogLeft)
            If FrmLogRight Is Nothing Then RunInUiWait(Sub()
                                                           AniControlEnabled += 1
                                                           FrmLogRight = New PageLogRight
                                                           AniControlEnabled -= 1
                                                       End Sub)
            FrmLogLeft.Add(Watcher)
            McLaunchLog("已显示游戏实时日志")
        End If

        '等待
        Do While Watcher.State = Watcher.MinecraftState.Loading
            Thread.Sleep(100)
        Loop
        If Watcher.State = Watcher.MinecraftState.Crashed Then
            Throw New Exception("$$")
        End If

    End Sub
    Private Sub McLaunchEnd()
        McLaunchLog("开始启动结束处理")

        '暂停或开始音乐播放
        If Setup.Get("UiMusicStop") Then
            RunInUi(Sub() If MusicPause() Then Log("[Music] 已根据设置，在启动后暂停音乐播放"))
        ElseIf Setup.Get("UiMusicStart") Then
            RunInUi(Sub() If MusicResume() Then Log("[Music] 已根据设置，在启动后开始音乐播放"))
        End If
        '暂停视频背景播放
        ModVideoBack.IsGaming = True
        VideoPause()
        '启动器可见性
        McLaunchLog("启动器可见性：" & Setup.Get("LaunchArgumentVisible"))
        Select Case Setup.Get("LaunchArgumentVisible")
            Case 0
                '直接关闭
                McLaunchLog("已根据设置，在启动后关闭启动器")
                RunInUi(Sub() FrmMain.EndProgram(False))
            Case 2, 3
                '隐藏
                McLaunchLog("已根据设置，在启动后隐藏启动器")
                RunInUi(Sub() FrmMain.Hidden = True)
            Case 4
                '最小化
                McLaunchLog("已根据设置，在启动后最小化启动器")
                RunInUi(Sub() FrmMain.WindowState = WindowState.Minimized)
            Case 5
                '啥都不干
        End Select

        '启动计数
        Setup.Set("SystemLaunchCount", Setup.Get("SystemLaunchCount") + 1)

        Setup.Set("VersionLaunchCount", Setup.Get("VersionLaunchCount", McInstanceCurrent) + 1, instance:=McInstanceCurrent)

    End Sub

    ''' <summary>
    ''' 在启动结束时，对 PCL 约定的替换标记进行处理。
    ''' </summary>
    Private Function ArgumentReplace(Raw As String, ReplaceTimeAndDate As Boolean) As String
        If Raw Is Nothing Then Return Nothing
        '路径替换
        Raw = Raw.Replace("{minecraft}", PathMcFolder)
        Raw = Raw.Replace("{verpath}", McInstanceCurrent.Path)
        Raw = Raw.Replace("{verindie}", McInstanceCurrent.PathIndie)
        Raw = Raw.Replace("{java}", McLaunchJavaSelected.JavaFolder)
        '普通替换
        Raw = Raw.Replace("{user}", McLoginLoader.Output.Name)
        Raw = Raw.Replace("{uuid}", McLoginLoader.Output.Uuid)
        If ReplaceTimeAndDate Then '设置窗口标题时需要动态替换日期和时间
            Raw = Raw.Replace("{date}", Date.Now.ToString("yyyy/M/d"))
            Raw = Raw.Replace("{time}", Date.Now.ToString("HH:mm:ss"))
        End If
        Select Case McLoginLoader.Input.Type
            Case McLoginType.Legacy
                Raw = Raw.Replace("{login}", "离线")
            Case McLoginType.Ms
                Raw = Raw.Replace("{login}", "正版")
            Case McLoginType.Auth
                Raw = Raw.Replace("{login}", "Authlib-Injector")
        End Select
        Raw = Raw.Replace("{name}", McInstanceCurrent.Name)
        If {"unknown", "old", "pending"}.Contains(McInstanceCurrent.Version.McName.ToLower) Then
            Raw = Raw.Replace("{version}", McInstanceCurrent.Name)
        Else
            Raw = Raw.Replace("{version}", McInstanceCurrent.Version.McName)
        End If
        Raw = Raw.Replace("{path}", Path)
        Return Raw
    End Function

#End Region

End Module
