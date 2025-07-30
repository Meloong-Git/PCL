Public Class PageLaunchLeft

    '加载当前版本
    Private IsLoad As Boolean = False
    Private IsLoadFinished As Boolean = False
    Public Sub PageLaunchLeft_Loaded() Handles Me.Loaded
        If IsLoad Then RefreshPage(True, False)

        AprilPosTrans.X = 0
        AprilPosTrans.Y = 0

        If IsLoad Then Return
        IsLoad = True
        AniControlEnabled += 1

        '开始按钮
        AddHandler McVersionListLoader.LoadingStateChanged, AddressOf RefreshButtonsUI
        AddHandler McFolderListLoader.LoadingStateChanged, AddressOf RefreshButtonsUI
        RefreshButtonsUI()

        '加载版本
        RunInNewThread(
        Sub()
            '自动整合包安装：准备
            Dim PackInstallPath As String = Nothing
            If File.Exists(Path & "modpack.zip") Then PackInstallPath = Path & "modpack.zip"
            If File.Exists(Path & "modpack.mrpack") Then PackInstallPath = Path & "modpack.mrpack"
            If PackInstallPath IsNot Nothing Then
                Log("[Launch] 需自动安装整合包：" & PackInstallPath, LogLevel.Debug)
                Setup.Set("LaunchFolderSelect", "$.minecraft\")
                If Not Directory.Exists(Path & ".minecraft\") Then
                    Directory.CreateDirectory(Path & ".minecraft\")
                    Directory.CreateDirectory(Path & ".minecraft\versions\")
                    McFolderLauncherProfilesJsonCreate(Path & ".minecraft\")
                End If
                PageSelectLeft.AddFolder(Path & ".minecraft\", GetFolderNameFromPath(Path), False)
                McFolderListLoader.WaitForExit()
            End If
            '确认 Minecraft 文件夹存在
            PathMcFolder = Setup.Get("LaunchFolderSelect").ToString.Replace("$", Path)
            If PathMcFolder = "" OrElse Not Directory.Exists(PathMcFolder) Then
                '无效的文件夹
                If PathMcFolder = "" Then
                    Log("[Launch] 没有已储存的 Minecraft 文件夹")
                Else
                    Log("[Launch] Minecraft 文件夹无效，该文件夹已不存在：" & PathMcFolder, LogLevel.Debug)
                End If
                McFolderListLoader.WaitForExit(IsForceRestart:=True)
                Setup.Set("LaunchFolderSelect", McFolderList(0).Path.Replace(Path, "$"))
            End If
            Log("[Launch] Minecraft 文件夹：" & PathMcFolder)
            If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomInteger(500, 3000))
            '自动整合包安装
            If PackInstallPath IsNot Nothing Then
                Try
                    Dim InstallLoader = ModpackInstall(PackInstallPath)
                    Log("[Launch] 自动安装整合包已开始：" & PackInstallPath)
                    InstallLoader.WaitForExit()
                    If InstallLoader.State = LoadState.Finished Then
                        Log("[Launch] 自动安装整合包成功，清理安装包：" & PackInstallPath)
                        If File.Exists(PackInstallPath) Then File.Delete(PackInstallPath)
                    End If
                Catch ex As CancelledException
                    Log(ex, "自动安装整合包被用户取消：" & PackInstallPath)
                Catch ex As Exception
                    Log(ex, "自动安装整合包失败：" & PackInstallPath, LogLevel.Msgbox)
                End Try
            End If
            '确认 Minecraft 版本存在
            Dim Selection As String = Setup.Get("LaunchVersionSelect")
            Dim Version As McVersion = If(Selection = "", Nothing, New McVersion(Selection))
            If Version Is Nothing OrElse Not Version.Path.StartsWithF(PathMcFolder) OrElse Not Version.Check() Then
                '无效的版本
                Log("[Launch] 当前选择的 Minecraft 版本无效：" & If(Version Is Nothing, "null", Version.Path), If(IsNothing(Version), LogLevel.Normal, LogLevel.Debug))
                If Not McVersionListLoader.State = LoadState.Finished Then LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                If Not McVersionList.Any() OrElse McVersionList.First.Value(0).Logo.Contains("RedstoneBlock") Then
                    Version = Nothing
                    Setup.Set("LaunchVersionSelect", "")
                    Log("[Launch] 无可用 Minecraft 版本")
                Else
                    Version = McVersionList.First.Value(0)
                    Setup.Set("LaunchVersionSelect", Version.Name)
                    Log("[Launch] 自动选择 Minecraft 版本：" & Version.Path)
                End If
            End If
            RunInUi(
            Sub()
                McVersionCurrent = Version '绕这一圈是为了避免 McVersionCheck 触发第二次版本改变
                IsLoadFinished = True
                RefreshButtonsUI()
                RefreshPage(False, False) '有可能选择的版本变化了，需要重新刷新
                If McLoginAble() = "" Then McLoginLoader.Start() '自动登录
            End Sub)
        End Sub, "Version Check", ThreadPriority.AboveNormal)

        '改变页面
        Dim LoginType As McLoginType = Setup.Get("LoginType")
        If LoginType = McLoginType.Legacy OrElse LoginType = McLoginType.Ms Then CType(FindName("RadioLoginType" & LoginType), MyRadioButton).Checked = True
        RefreshPage(False, False)

        AniControlEnabled -= 1
    End Sub

#Region "切换大页面"

    ''' <summary>
    ''' 切换至启动中页面。
    ''' </summary>
    Public Sub PageChangeToLaunching()
        '修改登陆方式
        Select Case Setup.Get("LoginType")
            Case McLoginType.Legacy
                If PageLinkHiper.HiperState = LoadState.Finished Then
                    LabLaunchingMethod.Text = "联机离线登录"
                Else
                    LabLaunchingMethod.Text = "离线登录"
                End If
            Case McLoginType.Ms
                LabLaunchingMethod.Text = "正版登录"
            Case McLoginType.Nide
                LabLaunchingMethod.Text = "统一通行证"
            Case McLoginType.Auth
                LabLaunchingMethod.Text = "Authlib-Injector"
        End Select
        '初始化页面
        LabLaunchingName.Text = McVersionCurrent.Name
        LabLaunchingStage.Text = "初始化"
        LabLaunchingTitle.Text = If(CurrentLaunchOptions?.SaveBatch Is Nothing, "正在启动游戏", "正在导出启动脚本")
        LabLaunchingProgress.Text = "0.00 %"
        LabLaunchingProgress.Opacity = 1
        LabLaunchingDownload.Visibility = Visibility.Visible
        LabLaunchingProgressLeft.Opacity = 0.6
        LabLaunchingDownload.Visibility = Visibility.Visible
        LabLaunchingDownload.Text = "0 B/s"
        LabLaunchingDownload.Opacity = 0
        LabLaunchingDownload.Visibility = Visibility.Collapsed
        LabLaunchingDownloadLeft.Opacity = 0
        LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed
        ProgressLaunchingFinished.Width = New GridLength(0, GridUnitType.Star)
        ProgressLaunchingUnfinished.Width = New GridLength(1, GridUnitType.Star)
        PanLaunchingHint.Opacity = 0
        PanLaunchingHint.Visibility = Visibility.Collapsed
        PanLaunchingInfo.Width = Double.NaN '重置宽度改变动画
        McLaunchProcess = Nothing
        McLaunchWatcher = Nothing
        '获取 “你知道吗” 提示
        LabLaunchingHint.Text = PageOtherTest.GetRandomHint()
        '初始化其他页面
        PanInput.IsHitTestVisible = False
        PanLaunching.IsHitTestVisible = False
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Run
        PanLaunching.Visibility = Visibility.Visible
        AniStart({
                AaOpacity(PanInput, 0, 50), '略作延迟，这样如果预检测失败，不会出现奇怪的弹一下的动画
                AaOpacity(PanInput, -PanInput.Opacity, 110, , New AniEaseInFluent, True),
                AaScaleTransform(PanInput, 1.2 - CType(PanInput.RenderTransform, ScaleTransform).ScaleX, 160),
                AaOpacity(PanLaunching, 1 - PanLaunching.Opacity, 150, 100),
                AaScaleTransform(PanLaunching, 1 - CType(PanLaunching.RenderTransform, ScaleTransform).ScaleX, 500, 100, New AniEaseOutBack(AniEasePower.Weak)),
                AaCode(Sub() PanLaunching.IsHitTestVisible = True, 150)
            }, "Launch State Page")
    End Sub
    ''' <summary>
    ''' 切换至登录页面。
    ''' </summary>
    Public Sub PageChangeToLogin()
        PageGet(PageCurrent).Reload(KeepInput:=False)
        PanInput.IsHitTestVisible = False
        PanLaunching.IsHitTestVisible = False
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Stop
        PanInput.Visibility = Visibility.Visible
        AniStart({
            AaOpacity(PanLaunching, -PanLaunching.Opacity, 150),
            AaScaleTransform(PanLaunching, 0.8 - CType(PanLaunching.RenderTransform, ScaleTransform).ScaleX, 150,, New AniEaseOutFluent(AniEasePower.Weak)),
            AaOpacity(PanInput, 1 - PanInput.Opacity, 250, 50),
            AaScaleTransform(PanInput, 1 - CType(PanInput.RenderTransform, ScaleTransform).ScaleX, 300, 50, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(Sub() PanInput.IsHitTestVisible = True, 200)
        }, "Launch State Page", True)
    End Sub

#End Region

#Region "切换登录页面"

    Private Enum PageType
        None
        Legacy
        Nide
        NideSkin
        Auth
        AuthSkin
        Ms
        MsSkin
    End Enum
    ''' <summary>
    ''' 当前页面的种类。
    ''' </summary>
    Private PageCurrent As PageType = PageType.None

    Private Function PageGet(Type As PageType)
        Select Case Type
            Case PageType.Legacy
                If IsNothing(FrmLoginLegacy) Then FrmLoginLegacy = New PageLoginLegacy
                Return FrmLoginLegacy
            Case PageType.Nide
                If IsNothing(FrmLoginNide) Then FrmLoginNide = New PageLoginNide
                Return FrmLoginNide
            Case PageType.NideSkin
                If IsNothing(FrmLoginNideSkin) Then FrmLoginNideSkin = New PageLoginNideSkin
                Return FrmLoginNideSkin
            Case PageType.Auth
                If IsNothing(FrmLoginAuth) Then FrmLoginAuth = New PageLoginAuth
                Return FrmLoginAuth
            Case PageType.AuthSkin
                If IsNothing(FrmLoginAuthSkin) Then FrmLoginAuthSkin = New PageLoginAuthSkin
                Return FrmLoginAuthSkin
            Case PageType.Ms
                If IsNothing(FrmLoginMs) Then FrmLoginMs = New PageLoginMs
                Return FrmLoginMs
            Case PageType.MsSkin
                If IsNothing(FrmLoginMsSkin) Then FrmLoginMsSkin = New PageLoginMsSkin
                Return FrmLoginMsSkin
            Case Else
                Throw New ArgumentOutOfRangeException("Type", "即将切换的登录分页编号越界")
        End Select
    End Function
    ''' <summary>
    ''' 切换现有登录页面种类，返回新页面的实例。
    ''' </summary>
    ''' <param name="Type">新页面的种类。</param>
    ''' <param name="Anim">是否显示动画。</param>
    Private Function PageChange(Type As PageType, Anim As Boolean)
        Dim PageNew As Object = FrmLoginMs '初始化一个东西，避免在执行时出现异常导致雪崩
        Try

#Region "确定更改的页面实例并实例化"
            If PageCurrent = Type Then Return PageNew
            PageNew = PageGet(Type)
#End Region

#Region "切换页面"
            AniStop("FrmLogin PageChange")
            '清除页面关联性
            If Not IsNothing(PageNew) AndAlso Not IsNothing(PageNew.Parent) Then PageNew.SetValue(ContentPresenter.ContentProperty, Nothing)
            If Anim Then
                '动画
                Dispatcher.Invoke(Sub()
                                      '执行动画
                                      AniStart({
                                                     AaOpacity(PanLogin, -PanLogin.Opacity, 100,, New AniEaseOutFluent),
                                                     AaCode(Sub()
                                                                AniControlEnabled += 1
                                                                PanLogin.Children.Clear()
                                                                PanLogin.Children.Add(PageNew)
                                                                AniControlEnabled -= 1
                                                            End Sub, 100),
                                                     AaOpacity(PanLogin, 1, 100, 120, New AniEaseInFluent)
                                                 }, "FrmLogin PageChange")
                                  End Sub, Threading.DispatcherPriority.Render)
            Else
                '无动画
                AniControlEnabled += 1
                PanLogin.Children.Clear()
                PanLogin.Children.Add(PageNew)
                AniControlEnabled -= 1
            End If
#End Region

            PageCurrent = Type
            Return PageNew
        Catch ex As Exception
            Log(ex, "切换登录分页失败（" & GetStringFromEnum(CType(Type, [Enum])) & "）", LogLevel.Feedback)
            Return PageNew
        End Try
    End Function

    ''' <summary>
    ''' 确认当前显示的子页面正确，并刷新该页面。
    ''' </summary>
    Public Sub RefreshPage(KeepInput As Boolean, Anim As Boolean)
        '获取页面的可用种类并回写缓存
        Dim Type As PageType
        Dim LoginPageType As Integer
        If McVersionCurrent IsNot Nothing Then
            LoginPageType = Setup.Get("VersionServerLogin", Version:=McVersionCurrent)
            '缓存当前版本的页面种类，下一次打开 McVersionCurrent 为空时才能加载出正确的页面
            Setup.Set("LoginPageType", LoginPageType)
        Else
            LoginPageType = Setup.Get("LoginPageType")
        End If
        Select Case LoginPageType
            Case 0 '正版或离线
UnknownType:
                If RadioLoginType5.Checked Then
                    If Setup.Get("CacheMsV2Access") = "" Then
                        Type = PageType.Ms
                    Else
                        Type = PageType.MsSkin
                    End If
                    Setup.Set("LoginType", McLoginType.Ms)
                Else
                    Type = PageType.Legacy
                    Setup.Set("LoginType", McLoginType.Legacy)
                End If
                PanType.Visibility = Visibility.Visible
                PanTypeOne.Visibility = Visibility.Collapsed
                RadioLoginType5.Visibility = Visibility.Visible
                RadioLoginType0.Visibility = Visibility.Visible
            Case 1 '仅正版
                If Setup.Get("CacheMsV2Access") = "" Then
                    Type = PageType.Ms
                Else
                    Type = PageType.MsSkin
                End If
                Setup.Set("LoginType", McLoginType.Ms)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonShield)
                LabTypeOne.Text = "正版登录"
                RadioLoginType5.Visibility = Visibility.Visible
                RadioLoginType0.Visibility = Visibility.Collapsed
            Case 2 '仅离线
                Type = PageType.Legacy
                Setup.Set("LoginType", McLoginType.Legacy)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonOffline)
                LabTypeOne.Text = "离线登录"
            Case 3 '统一通行证
                If Setup.Get("CacheNideAccess") = "" Then
                    Type = PageType.Nide
                Else
                    Type = PageType.NideSkin
                End If
                Setup.Set("LoginType", McLoginType.Nide)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonCard)
                LabTypeOne.Text = "统一通行证登录"
            Case 4 'Authlib-Injector
                If Setup.Get("CacheAuthAccess") = "" Then
                    Type = PageType.Auth
                Else
                    Type = PageType.AuthSkin
                End If
                Setup.Set("LoginType", McLoginType.Auth)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonCard)
                LabTypeOne.Text = If(McVersionCurrent Is Nothing, Setup.Get("CacheAuthServerName"), Setup.Get("VersionServerAuthName", Version:=McVersionCurrent))
                If LabTypeOne.Text = "" Then LabTypeOne.Text = "第三方登录"
            Case Else
                Log("[Control] 未知的登录页面：" & LoginPageType, LogLevel.Hint)
                GoTo UnknownType
        End Select
        '刷新页面
        If PageCurrent = Type Then Return
        PageChange(Type, Anim).Reload(KeepInput)
        Dim Control As MyRadioButton = FindName("RadioLoginType" & Setup.Get("LoginType"))
        If Control IsNot Nothing Then Control.Checked = True
    End Sub
    Private Sub RadioLoginType_Change(sender As Object, raiseByMouse As Boolean) Handles RadioLoginType0.Check, RadioLoginType5.Check
        If raiseByMouse Then RefreshPage(True, True)
    End Sub

#End Region

#Region "皮肤"

    '微软正版皮肤
    Public Shared SkinMs As New LoaderTask(Of EqualableList(Of String), String)("Loader Skin Ms", AddressOf SkinMsLoad, AddressOf SkinMsInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinMsInput() As EqualableList(Of String)
        '获取名称
        Return New EqualableList(Of String) From {Setup.Get("CacheMsV2Name"), Setup.Get("CacheMsV2Uuid")}
    End Function
    Private Shared Sub SkinMsLoad(Data As LoaderTask(Of EqualableList(Of String), String))
        '清空已有皮肤
        '如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        RunInUi(Sub() If FrmLoginMsSkin IsNot Nothing AndAlso FrmLoginMsSkin.Skin IsNot Nothing Then FrmLoginMsSkin.Skin.Clear())
        '获取 Url
        Dim UserName As String = Data.Input(0)
        Dim Uuid As String = Data.Input(1)
        If UserName = "" Then
            Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
            Log("[Minecraft] 获取微软正版皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(Uuid, "Ms")
            If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.GetType.Name = "ThreadInterruptedException" Then
                Data.Output = ""
                Return
            ElseIf GetExceptionSummary(ex).Contains("429") Then
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Log("[Minecraft] 获取正版皮肤失败（" & UserName & "）：获取皮肤太过频繁，请 5 分钟后再试！", LogLevel.Hint)
            ElseIf GetExceptionSummary(ex).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Log(ex, "获取微软正版皮肤失败（" & UserName & "）", LogLevel.Hint)
            End If
        End Try
Finish:
        '刷新显示
        If FrmLoginMsSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginMsSkin.Skin.Load)
        ElseIf Not Data.IsAborted Then '如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = Nothing '清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
        End If
    End Sub

    '离线皮肤
    Public Shared SkinLegacy As New LoaderTask(Of EqualableList(Of String), String)("Loader Skin Legacy", AddressOf SkinLegacyLoad, AddressOf SkinLegacyInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinLegacyInput() As EqualableList(Of String)
        '根据类型判断输入
        Dim Type As Integer = Setup.Get("LaunchSkinType")
        Select Case Type
            Case 0
                If FrmLoginLegacy IsNot Nothing AndAlso FrmLoginLegacy.IsReloaded Then
                    Return New EqualableList(Of String) From {0, If(FrmLoginLegacy.ComboName.Text.Trim, "")}
                ElseIf Setup.Get("LoginLegacyName") = "" Then
                    Return New EqualableList(Of String) From {0, ""}
                Else
                    Return New EqualableList(Of String) From {0, If(Setup.Get("LoginLegacyName").ToString.BeforeFirst("¨"), "")}
                End If
            Case 3
                Return New EqualableList(Of String) From {3, Setup.Get("LaunchSkinID")}
            Case Else
                Return New EqualableList(Of String) From {Type}
        End Select
    End Function
    Private Shared Sub SkinLegacyLoad(Data As LoaderTask(Of EqualableList(Of String), String))
        '清空已有皮肤
        RunInUi(Sub() If FrmLoginLegacy IsNot Nothing AndAlso FrmLoginLegacy.Skin IsNot Nothing Then FrmLoginLegacy.Skin.Clear())
        '获取 Url
        Select Case Data.Input(0)
            Case 0 '默认
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(Data.Input(1))) & ".png"
            Case 1 'Steve
UseDefault:
                Data.Output = PathImage & "Skins/Steve.png"
            Case 2 'Alex
                Data.Output = PathImage & "Skins/Alex.png"
            Case 3 '正版
                Dim ID As String = Data.Input(1)
                Try
                    If ID.Count < 2 Then
                        Data.Output = PathImage & "Skins/Steve.png"
                    Else
                        Dim Result As String = McLoginMojangUuid(ID, True)
                        If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & ID)
                        Result = McSkinGetAddress(Result, "Mojang")
                        If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & ID)
                        Result = McSkinDownload(Result)
                        If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & ID)
                        Data.Output = Result
                    End If
                Catch ex As Exception
                    If ex.GetType.Name = "ThreadInterruptedException" Then
                        Data.Output = ""
                        Return
                    ElseIf GetExceptionSummary(ex).Contains("429") Then
                        Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(ID)) & ".png"
                        Log("获取离线登录使用的正版皮肤失败（" & ID & "）：获取皮肤太过频繁，请 5 分钟后再试！")
                    Else
                        Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(ID)) & ".png"
                        Log(ex, "获取离线登录使用的正版皮肤失败（" & ID & "）")
                    End If
                End Try
            Case 4 '自定义
                If Not File.Exists(PathAppdata & "CustomSkin.png") Then
                    Hint("未找到离线皮肤自定义文件，可能它已被删除。PCL 将使用默认的 Steve 皮肤！")
                    Setup.Set("LaunchSkinType", 1)
                    GoTo UseDefault
                End If
                Data.Output = PathAppdata & "CustomSkin.png"
        End Select
        '刷新显示
        If FrmLoginLegacy IsNot Nothing Then
            RunInUi(AddressOf FrmLoginLegacy.Skin.Load)
        ElseIf Not Data.IsAborted Then '如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = Nothing '清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
        End If
    End Sub

    '统一通行证皮肤
    Public Shared SkinNide As New LoaderTask(Of EqualableList(Of String), String)("Loader Skin Nide", AddressOf SkinNideLoad, AddressOf SkinNideInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinNideInput() As EqualableList(Of String)
        '获取名称
        Return New EqualableList(Of String) From {Setup.Get("CacheNideName"), Setup.Get("CacheNideUuid")}
    End Function
    Private Shared Sub SkinNideLoad(Data As LoaderTask(Of EqualableList(Of String), String))
        '清空已有皮肤
        '如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        RunInUi(Sub() If FrmLoginNideSkin IsNot Nothing AndAlso FrmLoginNideSkin.Skin IsNot Nothing Then FrmLoginNideSkin.Skin.Clear())
        '获取 Url
        Dim UserName As String = Data.Input(0)
        Dim Uuid As String = Data.Input(1)
        If UserName = "" Then
            Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
            Log("[Minecraft] 获取统一通行证皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(Uuid, "Nide")
            If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.GetType.Name = "ThreadInterruptedException" Then
                Data.Output = ""
                Return
            ElseIf GetExceptionSummary(ex).Contains("429") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Log("[Minecraft] 获取统一通行证皮肤失败（" & UserName & "）：获取皮肤太过频繁，请 5 分钟后再试！", LogLevel.Hint)
            ElseIf GetExceptionSummary(ex).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/Steve.png"
                Log(ex, "获取统一通行证皮肤失败（" & UserName & "）", LogLevel.Hint)
            End If
        End Try
Finish:
        '刷新显示
        If FrmLoginNideSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginNideSkin.Skin.Load)
        ElseIf Not Data.IsAborted Then '如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = Nothing '清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
        End If
    End Sub

    'Authlib-Injector 皮肤
    Public Shared SkinAuth As New LoaderTask(Of EqualableList(Of String), String)("Loader Skin Auth", AddressOf SkinAuthLoad, AddressOf SkinAuthInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinAuthInput() As EqualableList(Of String)
        '获取名称
        Return New EqualableList(Of String) From {Setup.Get("CacheAuthName"), Setup.Get("CacheAuthUuid")}
    End Function
    Private Shared Sub SkinAuthLoad(Data As LoaderTask(Of EqualableList(Of String), String))
        '清空已有皮肤
        '如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        RunInUi(Sub() If FrmLoginAuthSkin IsNot Nothing AndAlso FrmLoginAuthSkin.Skin IsNot Nothing Then FrmLoginAuthSkin.Skin.Clear())
        '获取 Url
        Dim UserName As String = Data.Input(0)
        Dim Uuid As String = Data.Input(1)
        If UserName = "" Then
            Data.Output = PathImage & "Skins/Steve.png"
            Log("[Minecraft] 获取 Authlib-Injector 皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(Uuid, "Auth")
            If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsAborted Then Throw New ThreadInterruptedException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.GetType.Name = "ThreadInterruptedException" Then
                Data.Output = ""
                Return
            ElseIf GetExceptionSummary(ex).Contains("429") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Log("[Minecraft] 获取 Authlib-Injector 皮肤失败（" & UserName & "）：获取皮肤太过频繁，请 5 分钟后再试！", LogLevel.Hint)
            ElseIf GetExceptionSummary(ex).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/Steve.png"
                Log(ex, "获取 Authlib-Injector 皮肤失败（" & UserName & "）", LogLevel.Hint)
            End If
        End Try
Finish:
        '刷新显示
        If FrmLoginAuthSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginAuthSkin.Skin.Load)
        ElseIf Not Data.IsAborted Then '如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = Nothing '清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
        End If
    End Sub

    '全部皮肤加载器
    '需要放在其中元素的后面，否则会因为它提前被加载而莫名其妙变成 Nothing
    Public Shared SkinLoaders As New List(Of LoaderTask(Of EqualableList(Of String), String)) From {SkinMs, SkinLegacy, SkinNide, SkinAuth}

#End Region

    '版本选择按钮
    Private Sub BtnVersion_Click(sender As Object, e As EventArgs) Handles BtnVersion.Click
        If McLaunchLoader.State = LoadState.Loading Then Return
        FrmMain.PageChange(FormMain.PageType.VersionSelect)
    End Sub
    '启动按钮
    Public Sub LaunchButtonClick() Handles BtnLaunch.Click
        If McLaunchLoader.State = LoadState.Loading OrElse Not BtnLaunch.IsEnabled OrElse
            （FrmMain.PageRight IsNot Nothing AndAlso FrmMain.PageRight.PageState <> MyPageRight.PageStates.ContentStay AndAlso FrmMain.PageRight.PageState <> MyPageRight.PageStates.ContentEnter） Then Return
        '愚人节处理
        If IsAprilEnabled AndAlso Not IsAprilGiveup Then
            ThemeUnlock(12, False, "隐藏主题 滑稽彩 已解锁！")
            IsAprilGiveup = True
            FrmLaunchLeft.AprilScaleTrans.ScaleX = 1
            FrmLaunchLeft.AprilScaleTrans.ScaleY = 1
            FrmLaunchLeft.AprilPosTrans.X = 0
            FrmLaunchLeft.AprilPosTrans.Y = 0
            FrmMain.BtnExtraApril.ShowRefresh()
        End If
        '实际的启动
        If BtnLaunch.Text = "启动游戏" Then
            McLaunchStart()
        ElseIf BtnLaunch.Text = "下载游戏" Then
            FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
        End If
    End Sub
    Private BtnLaunchState As Integer = 0
    Private BtnLaunchVersion As McVersion = Nothing
    Public Sub RefreshButtonsUI() Handles BtnLaunch.Loaded
        If Not BtnLaunch.IsLoaded Then Return
        '获取当前状态
        Dim CurrentState As Integer
        If (Not IsLoadFinished) OrElse McVersionListLoader.State = LoadState.Loading OrElse McFolderListLoader.State = LoadState.Loading Then
            CurrentState = 0
        Else
            If McVersionCurrent Is Nothing Then
                If Setup.Get("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow Then
                    CurrentState = 1
                Else
                    CurrentState = 2
                End If
            Else
                CurrentState = 3
            End If
        End If
        '更新状态
        If CurrentState = BtnLaunchState AndAlso
           If(McVersionCurrent Is Nothing, "", McVersionCurrent.Path) = If(BtnLaunchVersion Is Nothing, "", BtnLaunchVersion.Path) Then GoTo ExitRefresh
        BtnLaunchVersion = McVersionCurrent
        BtnLaunchState = CurrentState
        Select Case CurrentState
            Case 0
                Log("[Minecraft] 启动按钮：正在加载 Minecraft 版本")
                FrmLaunchLeft.BtnLaunch.Text = "正在加载"
                FrmLaunchLeft.BtnLaunch.IsEnabled = False
                FrmLaunchLeft.LabVersion.Text = "正在加载中，请稍候"
                FrmLaunchLeft.BtnVersion.IsEnabled = False
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 1
                Log("[Minecraft] 启动按钮：无 Minecraft 版本，下载已禁用")
                FrmLaunchLeft.BtnLaunch.Text = "启动游戏"
                FrmLaunchLeft.BtnLaunch.IsEnabled = False
                FrmLaunchLeft.LabVersion.Text = "未找到可用的游戏版本"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 2
                Log("[Minecraft] 启动按钮：无 Minecraft 版本，要求下载")
                FrmLaunchLeft.BtnLaunch.Text = "下载游戏"
                FrmLaunchLeft.BtnLaunch.IsEnabled = True
                FrmLaunchLeft.LabVersion.Text = "未找到可用的游戏版本"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 3
                Log("[Minecraft] 启动按钮：Minecraft 版本：" & McVersionCurrent.Path)
                FrmLaunchLeft.BtnLaunch.Text = "启动游戏"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnLaunch.IsEnabled = True
                FrmLaunchLeft.LabVersion.Text = McVersionCurrent.Name
                'FrmLaunchLeft.BtnMore.Visibility = Visibility.Visible '由功能隐藏设置修改
        End Select
ExitRefresh:
        '功能隐藏
        FrmLaunchLeft.BtnVersion.Visibility = If(Not PageSetupUI.HiddenForceShow AndAlso Setup.Get("UiHiddenFunctionSelect"), Visibility.Collapsed, Visibility.Visible)
        If CurrentState = 3 Then
            FrmLaunchLeft.BtnMore.Visibility = FrmLaunchLeft.BtnVersion.Visibility
        End If
    End Sub
    '取消按钮
    Private Sub BtnCancel_Click() Handles BtnCancel.Click
        If McLaunchLoaderReal IsNot Nothing Then
            McLaunchLoaderReal.Abort()
            McLaunchLog("已取消启动")
            Try
                If McLaunchWatcher IsNot Nothing Then
                    McLaunchWatcher.Kill()
                ElseIf McLaunchProcess IsNot Nothing Then
                    If Not McLaunchProcess.HasExited Then McLaunchProcess.Kill()
                End If
            Catch ex As Exception
                Log(ex, "取消启动结束进程失败", LogLevel.Hint)
            End Try
        End If
    End Sub
    '版本设置按钮
    Private Sub BtnMore_Click(sender As Object, e As EventArgs) Handles BtnMore.Click
        If McLaunchLoader.State = LoadState.Loading Then Return
        McVersionCurrent.Load()
        PageVersionLeft.Version = McVersionCurrent
        FrmMain.PageChange(FormMain.PageType.VersionSetup, 0)
    End Sub
    ''' <summary>
    ''' 每 0.2s 执行一次，刷新启动的数据 UI 显示。
    ''' </summary>
    Public Sub LaunchingRefresh()
        Try
            If McLaunchLoaderReal.State = LoadState.Aborted Then Return
            '阶段状态获取
            Dim IsLaunched As Boolean = False '是否已经启动游戏，只是在等待窗口
            Try
                For Each Loader In McLaunchLoaderReal.GetLoaderList(False)
                    If Loader.State = LoadState.Loading OrElse Loader.State = LoadState.Waiting Then
                        LabLaunchingStage.Text = Loader.Name
                        IsLaunched = Loader.Name = "等待游戏窗口出现" OrElse Loader.Name = "结束处理"
                        Exit Try
                    End If
                Next
                LabLaunchingStage.Text = "已完成"
            Catch ex As Exception
                Log(ex, "获取是否启动完成失败，可能是由于启动状态改变导致集合已修改")
                Return
            End Try
            If AniIsRun("Launch State Page") Then IsLaunched = False '等待页面切换动画完成
            '计算应显示的进度
            Dim ActualProgress = McLaunchLoaderReal.Progress
            If ActualProgress >= ShowProgress Then ShowProgress += (ActualProgress - ShowProgress) * 0.2 + 0.005 '向实际进度靠一点
            If ActualProgress <= ShowProgress Then ShowProgress = ActualProgress '原来或处理后变得比实际进度高，直接回退
            If IsLaunched Then ShowProgress = 1 '如果已经完成了，就不卖关子了
            '文本
            LabLaunchingTitle.Text = If(IsLaunched, "已启动游戏", If(CurrentLaunchOptions.SaveBatch Is Nothing, "正在启动游戏", "正在导出启动脚本"))
            LabLaunchingProgress.Text = StrFillNum(ShowProgress * 100, 2) & " %"
            Dim HasLaunchDownloader As Boolean = False
            Try
                For Each Loader In NetManager.Tasks
                    If Loader.RealParent IsNot Nothing AndAlso Loader.RealParent.Name = "Minecraft 启动" AndAlso Loader.State = LoadState.Loading Then HasLaunchDownloader = True
                Next
            Catch ex As Exception
                Log(ex, "获取 Minecraft 启动下载器失败，可能是因为启动被取消")
                HasLaunchDownloader = False
            End Try
            LabLaunchingDownload.Text = GetString(NetManager.Speed) & "/s"
            '进度改变动画
            Dim AnimList As New List(Of AniData) From {
                 AaGridLengthWidth(ProgressLaunchingFinished, ShowProgress - ProgressLaunchingFinished.Width.Value, 260,, New AniEaseOutFluent),
                 AaGridLengthWidth(ProgressLaunchingUnfinished, 1 - ShowProgress - ProgressLaunchingUnfinished.Width.Value, 260,, New AniEaseOutFluent)
            }
            Dim IsDownloadStateChanged As Boolean = HasLaunchDownloader = (LabLaunchingDownload.Visibility = Visibility.Collapsed)
            If IsDownloadStateChanged Then
                LabLaunchingDownload.Visibility = Visibility.Visible
                LabLaunchingDownloadLeft.Visibility = Visibility.Visible
                AnimList.AddRange({
                 AaOpacity(LabLaunchingDownload, If(HasLaunchDownloader, 1, 0) - LabLaunchingDownload.Opacity, 100),
                 AaOpacity(LabLaunchingDownloadLeft, If(HasLaunchDownloader, 0.5, 0) - LabLaunchingDownloadLeft.Opacity, 100),
                 AaCode(Sub()
                            If Not HasLaunchDownloader Then
                                LabLaunchingDownload.Visibility = Visibility.Collapsed
                                LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed
                            End If
                        End Sub, 110)
            })
            End If
            Dim IsProgressStateChanged As Boolean = (Not IsLaunched) = (LabLaunchingProgress.Visibility = Visibility.Collapsed)
            If IsProgressStateChanged Then
                LabLaunchingProgress.Visibility = Visibility.Visible
                LabLaunchingProgressLeft.Visibility = Visibility.Visible
                If IsLaunched Then
                    'IsWidthAnimating = True
                    PanLaunchingHint.Visibility = Visibility.Visible
                    'AniStop("Launching Info Width")
                    'PanLaunchingInfo.Width = 260
                    'ActualUsedWidth = 260
                    'IsWidthAnimating = False
                End If
                AnimList.AddRange({
                 AaOpacity(LabLaunchingProgress, If(Not IsLaunched, 1, 0) - LabLaunchingProgress.Opacity, 100),
                 AaOpacity(LabLaunchingProgressLeft, If(Not IsLaunched, 0.5, 0) - LabLaunchingProgressLeft.Opacity, 100),
                 AaOpacity(PanLaunchingHint, If(IsLaunched, 1, 0) - PanLaunchingHint.Opacity, 100)
            })
            End If
            AniStart(AnimList, "Launching Progress")
        Catch ex As Exception
            Log(ex, "刷新启动信息失败", LogLevel.Feedback)
        End Try
    End Sub
    Private ShowProgress As Double = 0
    '尺寸改变动画
    Private IsWidthAnimating As Boolean = False
    Private ActualUsedWidth As Double
    Private Sub PanLaunchingInfo_SizeChangedW(sender As Object, e As SizeChangedEventArgs) Handles PanLaunchingInfo.SizeChanged
        Dim DeltaWidth As Double = e.NewSize.Width - e.PreviousSize.Width
        If e.PreviousSize.Width = 0 OrElse IsWidthAnimating OrElse Math.Abs(DeltaWidth) < 1 OrElse PanLaunchingInfo.ActualWidth = 0 Then Return
        AniStart({
            AaWidth(PanLaunchingInfo, DeltaWidth, 180,, New AniEaseOutFluent),
            AaCode(Sub()
                       IsWidthAnimating = False
                       PanLaunchingInfo.Width = ActualUsedWidth
                   End Sub,, True)
        }, "Launching Info Width")
        IsWidthAnimating = True
        ActualUsedWidth = PanLaunchingInfo.Width
        PanLaunchingInfo.Width = e.PreviousSize.Width
    End Sub
    Private IsHeightAnimating As Boolean = False
    Private ActualUsedHeight As Double
    Private Sub PanLaunchingInfo_SizeChangedH(sender As Object, e As SizeChangedEventArgs) Handles PanLaunchingInfo.SizeChanged
        Dim DeltaHeight As Double = e.NewSize.Height - e.PreviousSize.Height
        If e.PreviousSize.Height = 0 OrElse IsHeightAnimating OrElse Math.Abs(DeltaHeight) < 1 OrElse PanLaunchingInfo.ActualHeight = 0 Then Return
        AniStart({
            AaHeight(PanLaunchingInfo, DeltaHeight, 180,, New AniEaseOutFluent),
            AaCode(Sub()
                       IsHeightAnimating = False
                       PanLaunchingInfo.Height = ActualUsedHeight
                   End Sub,, True)
        }, "Launching Info Height")
        IsHeightAnimating = True
        ActualUsedHeight = PanLaunchingInfo.Height
        PanLaunchingInfo.Height = e.PreviousSize.Height
    End Sub

End Class
