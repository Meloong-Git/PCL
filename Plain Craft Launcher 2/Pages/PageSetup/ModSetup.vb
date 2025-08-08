Public Class ModSetup

    ''' <summary>
    ''' 设置的更新号。
    ''' </summary>
    Public Const VersionSetup As Integer = 1
    ''' <summary>
    ''' 设置列表。
    ''' </summary>
    Private ReadOnly SetupDict As New Dictionary(Of String, SetupEntry) From {
        {"Identify", New SetupEntry("", Source:=SetupSource.Registry)},
        {"WindowHeight", New SetupEntry(550)},
        {"WindowWidth", New SetupEntry(900)},
        {"HintDownloadThread", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintNotice", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"HintDownload", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"HintInstallBack", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintHide", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintHandInstall", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintBuy", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintClearRubbish", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"HintUpdateMod", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintCustomCommand", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintCustomWarn", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintMoreAdvancedSetup", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintIndieSetup", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"HintExportConfig", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"SystemEula", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"SystemCount", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"SystemLaunchCount", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"SystemLastVersionReg", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"SystemHighestSavedBetaVersionReg", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"SystemHighestBetaVersionReg", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"SystemHighestAlphaVersionReg", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"SystemSetupVersionReg", New SetupEntry(VersionSetup, Source:=SetupSource.Registry)},
        {"SystemSetupVersionIni", New SetupEntry(VersionSetup)},
        {"SystemHelpVersion", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"SystemDebugMode", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"SystemDebugAnim", New SetupEntry(9, Source:=SetupSource.Registry)},
        {"SystemDebugDelay", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"SystemDebugSkipCopy", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"SystemSystemCache", New SetupEntry("", Source:=SetupSource.Registry)},
        {"SystemSystemUpdate", New SetupEntry(0)},
        {"SystemSystemActivity", New SetupEntry(0)},
        {"CacheExportConfig", New SetupEntry("", Source:=SetupSource.Registry)},
        {"CacheSavedPageUrl", New SetupEntry("", Source:=SetupSource.Registry)},
        {"CacheSavedPageVersion", New SetupEntry("", Source:=SetupSource.Registry)},
        {"CacheMsOAuthRefresh", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsAccess", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsProfileJson", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsUuid", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsName", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsV2Migrated", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"CacheMsV2OAuthRefresh", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsV2Access", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsV2ProfileJson", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsV2Uuid", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheMsV2Name", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheNideAccess", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheNideClient", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheNideUuid", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheNideName", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheNideUsername", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheNidePass", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheNideServer", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthAccess", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthClient", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthUuid", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthName", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthUsername", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthPass", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthServerServer", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthServerName", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheAuthServerRegister", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"CacheDownloadFolder", New SetupEntry("", Source:=SetupSource.Registry)},
        {"CacheJavaListVersion", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"LoginRemember", New SetupEntry(True, Source:=SetupSource.Registry, Encoded:=True)},
        {"LoginLegacyName", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"LoginMsJson", New SetupEntry("{}", Source:=SetupSource.Registry, Encoded:=True)}, '{UserName: OAuthToken, ...}
        {"LoginNideEmail", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"LoginNidePass", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"LoginAuthEmail", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"LoginAuthPass", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"LoginType", New SetupEntry(McLoginType.Legacy, Source:=SetupSource.Registry)},
        {"LoginPageType", New SetupEntry(0)},
        {"LaunchSkinID", New SetupEntry("", Source:=SetupSource.Registry)},
        {"LaunchSkinType", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"LaunchSkinSlim", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"LaunchVersionSelect", New SetupEntry("")},
        {"LaunchFolderSelect", New SetupEntry("")},
        {"LaunchFolders", New SetupEntry("", Source:=SetupSource.Registry)},
        {"LaunchArgumentTitle", New SetupEntry("")},
        {"LaunchArgumentInfo", New SetupEntry("PCL")},
        {"LaunchArgumentJavaSelect", New SetupEntry("", Source:=SetupSource.Registry)},
        {"LaunchArgumentJavaAll", New SetupEntry("[]", Source:=SetupSource.Registry)},
        {"LaunchArgumentIndie", New SetupEntry(0)},
        {"LaunchArgumentIndieV2", New SetupEntry(4)},
        {"LaunchArgumentVisible", New SetupEntry(5, Source:=SetupSource.Registry)},
        {"LaunchArgumentPriority", New SetupEntry(1, Source:=SetupSource.Registry)},
        {"LaunchArgumentWindowWidth", New SetupEntry(854)},
        {"LaunchArgumentWindowHeight", New SetupEntry(480)},
        {"LaunchArgumentWindowType", New SetupEntry(1)},
        {"LaunchArgumentRam", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"LaunchAdvanceJvm", New SetupEntry("-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true")},
        {"LaunchAdvanceGame", New SetupEntry("")},
        {"LaunchAdvanceRun", New SetupEntry("")},
        {"LaunchAdvanceRunWait", New SetupEntry(True)},
        {"LaunchAdvanceDisableJLW", New SetupEntry(False)},
        {"LaunchAdvanceGraphicCard", New SetupEntry(True, Source:=SetupSource.Registry)},
        {"LaunchAdvanceNoJavaw", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"LaunchRamType", New SetupEntry(0)},
        {"LaunchRamCustom", New SetupEntry(15)},
        {"LinkEula", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"LinkName", New SetupEntry("", Source:=SetupSource.Registry)},
        {"LinkHiperCertLast", New SetupEntry("", Source:=SetupSource.Registry)},
        {"LinkHiperCertTime", New SetupEntry("", Source:=SetupSource.Registry)},
        {"LinkHiperCertWarn", New SetupEntry(True, Source:=SetupSource.Registry)},
        {"LinkIoiVersion", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"ToolHelpChinese", New SetupEntry(True, Source:=SetupSource.Registry)},
        {"ToolDownloadThread", New SetupEntry(63, Source:=SetupSource.Registry)},
        {"ToolDownloadSpeed", New SetupEntry(42, Source:=SetupSource.Registry)},
        {"ToolDownloadSource", New SetupEntry(1, Source:=SetupSource.Registry)},
        {"ToolDownloadVersion", New SetupEntry(1, Source:=SetupSource.Registry)},
        {"ToolDownloadTranslate", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"ToolDownloadTranslateV2", New SetupEntry(1, Source:=SetupSource.Registry)},
        {"ToolDownloadIgnoreQuilt", New SetupEntry(True, Source:=SetupSource.Registry)},
        {"ToolDownloadCert", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"ToolDownloadMod", New SetupEntry(1, Source:=SetupSource.Registry)},
        {"ToolModLocalNameStyle", New SetupEntry(0, Source:=SetupSource.Registry)},
        {"ToolUpdateAlpha", New SetupEntry(0, Source:=SetupSource.Registry, Encoded:=True)},
        {"ToolUpdateRelease", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"ToolUpdateSnapshot", New SetupEntry(False, Source:=SetupSource.Registry)},
        {"ToolUpdateReleaseLast", New SetupEntry("", Source:=SetupSource.Registry)},
        {"ToolUpdateSnapshotLast", New SetupEntry("", Source:=SetupSource.Registry)},
        {"UiLauncherTransparent", New SetupEntry(600)}, '避免与 PCL1 设置冲突（UiLauncherOpacity）
        {"UiLauncherHue", New SetupEntry(180)},
        {"UiLauncherSat", New SetupEntry(80)},
        {"UiLauncherDelta", New SetupEntry(90)},
        {"UiLauncherLight", New SetupEntry(20)},
        {"UiLauncherTheme", New SetupEntry(0)},
        {"UiLauncherThemeGold", New SetupEntry("", Source:=SetupSource.Registry, Encoded:=True)},
        {"UiLauncherThemeHide", New SetupEntry("0|1|2|3|4", Source:=SetupSource.Registry, Encoded:=True)},
        {"UiLauncherThemeHide2", New SetupEntry("0|1|2|3|4", Source:=SetupSource.Registry, Encoded:=True)},
        {"UiLauncherLogo", New SetupEntry(True)},
        {"UiLauncherEmail", New SetupEntry(False)},
        {"UiBackgroundColorful", New SetupEntry(True)},
        {"UiBackgroundOpacity", New SetupEntry(1000)},
        {"UiBackgroundBlur", New SetupEntry(0)},
        {"UiBackgroundSuit", New SetupEntry(0)},
        {"UiCustomType", New SetupEntry(0)},
        {"UiCustomPreset", New SetupEntry(0)},
        {"UiCustomNet", New SetupEntry("")},
        {"UiLogoType", New SetupEntry(1)},
        {"UiLogoText", New SetupEntry("")},
        {"UiLogoLeft", New SetupEntry(False)},
        {"UiMusicVolume", New SetupEntry(500)},
        {"UiMusicStop", New SetupEntry(False)},
        {"UiMusicStart", New SetupEntry(False)},
        {"UiMusicRandom", New SetupEntry(True)},
        {"UiMusicAuto", New SetupEntry(True)},
        {"UiHiddenPageDownload", New SetupEntry(False)},
        {"UiHiddenPageLink", New SetupEntry(False)},
        {"UiHiddenPageSetup", New SetupEntry(False)},
        {"UiHiddenPageOther", New SetupEntry(False)},
        {"UiHiddenFunctionSelect", New SetupEntry(False)},
        {"UiHiddenFunctionModUpdate", New SetupEntry(False)},
        {"UiHiddenFunctionHidden", New SetupEntry(False)},
        {"UiHiddenSetupLaunch", New SetupEntry(False)},
        {"UiHiddenSetupUi", New SetupEntry(False)},
        {"UiHiddenSetupLink", New SetupEntry(False)},
        {"UiHiddenSetupSystem", New SetupEntry(False)},
        {"UiHiddenOtherHelp", New SetupEntry(False)},
        {"UiHiddenOtherFeedback", New SetupEntry(False)},
        {"UiHiddenOtherVote", New SetupEntry(False)},
        {"UiHiddenOtherAbout", New SetupEntry(False)},
        {"UiHiddenOtherTest", New SetupEntry(False)},
        {"VersionAdvanceJvm", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionAdvanceGame", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionAdvanceAssets", New SetupEntry(0, Source:=SetupSource.Version)},
        {"VersionAdvanceAssetsV2", New SetupEntry(False, Source:=SetupSource.Version)},
        {"VersionAdvanceJava", New SetupEntry(False, Source:=SetupSource.Version)},
        {"VersionAdvanceRun", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionAdvanceRunWait", New SetupEntry(True, Source:=SetupSource.Version)},
        {"VersionAdvanceDisableJLW", New SetupEntry(False, Source:=SetupSource.Version)},
        {"VersionRamType", New SetupEntry(2, Source:=SetupSource.Version)},
        {"VersionRamCustom", New SetupEntry(15, Source:=SetupSource.Version)},
        {"VersionRamOptimize", New SetupEntry(0, Source:=SetupSource.Version)},
        {"VersionArgumentTitle", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionArgumentInfo", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionArgumentIndie", New SetupEntry(-1, Source:=SetupSource.Version)},
        {"VersionArgumentIndieV2", New SetupEntry(False, Source:=SetupSource.Version)},
        {"VersionArgumentJavaSelect", New SetupEntry("使用全局设置", Source:=SetupSource.Version)},
        {"VersionServerEnter", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionServerLogin", New SetupEntry(0, Source:=SetupSource.Version)},
        {"VersionServerNide", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionServerAuthRegister", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionServerAuthName", New SetupEntry("", Source:=SetupSource.Version)},
        {"VersionServerAuthServer", New SetupEntry("", Source:=SetupSource.Version)}
    }

#Region "基础"

    Private Enum SetupSource
        Normal
        Registry
        Version
    End Enum
    Private Class SetupEntry

        Public Encoded As Boolean
        Public DefaultValue
        Public DefaultValueEncoded
        Public Value
        Public Source As SetupSource

        ''' <summary>
        ''' 加载状态：0/未读取  1/已读取未处理  2/已处理
        ''' 我也不知道当年写这坨的时候为啥没用 Enum……
        ''' </summary>
        Public State As Byte = 0
        Public Type As Type

        Public Sub New(Value, Optional Source = SetupSource.Normal, Optional Encoded = False)
            Try
                Me.DefaultValue = Value
                Me.Encoded = Encoded
                Me.Value = Value
                Me.Source = Source
                Me.Type = If(Value, New Object).GetType
                Me.DefaultValueEncoded = If(Encoded, SecretEncrypt(Value, "PCL" & UniqueAddress), Value)
            Catch ex As Exception
                Log(ex, "初始化 SetupEntry 失败", LogLevel.Feedback) '#5095 的 fallback
            End Try
        End Sub

    End Class

    ''' <summary>
    ''' 改变某个设置项的值。
    ''' </summary>
    Public Sub [Set](Key As String, Value As Object, Optional ForceReload As Boolean = False, Optional Version As McVersion = Nothing)
        [Set](Key, Value, SetupDict(Key), ForceReload, Version)
    End Sub
    Private Sub [Set](Key As String, Value As Object, E As SetupEntry, ForceReload As Boolean, Version As McVersion)
        Try

            Value = CTypeDynamic(Value, E.Type)
            If E.State = 2 Then
                '如果已应用，且值相同，则无需再次更改
                If E.Value = Value AndAlso Not ForceReload Then Return
            Else
                '如果未应用，则直接更改并应用
                If E.Source <> SetupSource.Version Then E.State = 2
            End If
            '设置新值
            E.Value = Value
            '写入值
            If E.Encoded Then
                Try
                    If Value Is Nothing Then Value = ""
                    Value = SecretEncrypt(Value, "PCL" & UniqueAddress)
                Catch ex As Exception
                    Log(ex, "加密设置失败：" & Key, LogLevel.Developer)
                End Try
            End If
            Select Case E.Source
                Case SetupSource.Normal
                    WriteIni("Setup", Key, Value)
                Case SetupSource.Registry
                    WriteReg(Key, Value)
                Case SetupSource.Version
                    If Version Is Nothing Then Throw New Exception($"更改版本设置 {Key} 时未提供目标版本")
                    WriteIni(Version.Path & "PCL\Setup.ini", Key, Value)
            End Select
            '应用
            '例如 VersionServerLogin 要求在设置之后再引发事件
            Dim Method As Reflection.MethodInfo = GetType(ModSetup).GetMethod(Key)
            If Method IsNot Nothing Then Method.Invoke(Me, {Value})

        Catch ex As Exception
            Log(ex, "设置设置项时出错（" & Key & ", " & Value & "）", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 应用某个设置项的值。
    ''' </summary>
    Public Function Load(Key As String, Optional ForceReload As Boolean = False, Optional Version As McVersion = Nothing)
        Return Load(Key, SetupDict(Key), ForceReload, Version)
    End Function
    Private Function Load(Key As String, E As SetupEntry, ForceReload As Boolean, Version As McVersion)
        '如果已经应用过，则什么也不干
        If E.State = 2 AndAlso Not ForceReload Then Return E.Value
        '读取，应用并设置状态
        Read(Key, E, Version)
        If E.Source <> SetupSource.Version Then E.State = 2
        Dim Method As Reflection.MethodInfo = GetType(ModSetup).GetMethod(Key)
        If Method IsNot Nothing Then Method.Invoke(Me, {E.Value})
        Return E.Value
    End Function

    ''' <summary>
    ''' 获取某个设置项的值。
    ''' </summary>
    Public Function [Get](Key As String, Optional Version As McVersion = Nothing)
        If Not SetupDict.ContainsKey(Key) Then Throw New KeyNotFoundException("未找到设置项：" & Key) With {.Source = Key}
        Return [Get](Key, SetupDict(Key), Version)
    End Function
    Private Function [Get](Key As String, E As SetupEntry, Version As McVersion)
        '获取强制值
        Dim Force As String = ForceValue(Key)
        If Force IsNot Nothing Then
            E.Value = CTypeDynamic(Force, E.Type)
            E.State = 1
        End If
        '如果尚未读取过，则读取
        If E.State = 0 Then
            Read(Key, E, Version)
            If E.Source <> SetupSource.Version Then E.State = 1
        End If
        '返回现在的值
        Return E.Value
    End Function

    ''' <summary>
    ''' 初始化某个设置项的值。
    ''' </summary>
    Public Sub Reset(Key As String, Optional ForceReload As Boolean = False, Optional Version As McVersion = Nothing)
        Dim E As SetupEntry = SetupDict(Key)
        [Set](Key, E.DefaultValue, E, ForceReload, Version)
        Select Case SetupDict(Key).Source
            Case SetupSource.Normal
                DeleteIniKey("Setup", Key)
            Case SetupSource.Registry
                DeleteReg(Key)
            Case Else 'SetupSource.Version
                If Version Is Nothing Then Throw New Exception($"重置版本设置 {Key} 时未提供目标版本")
                DeleteIniKey(Version.Path & "PCL\Setup.ini", Key)
        End Select
    End Sub
    ''' <summary>
    ''' 获取某个设置项的默认值。
    ''' </summary>
    Public Function GetDefault(Key As String) As String
        Return SetupDict(Key).DefaultValue
    End Function
    ''' <summary>
    ''' 某个设置项是否从未被设置过。
    ''' </summary>
    Public Function IsUnset(Key As String, Optional Version As McVersion = Nothing) As Boolean
        Select Case SetupDict(Key).Source
            Case SetupSource.Normal
                Return Not HasIniKey("Setup", Key)
            Case SetupSource.Registry
                Return Not HasReg(Key)
            Case Else 'SetupSource.Version
                If Version Is Nothing Then Throw New Exception($"判断版本设置 {Key} 是否存在时未提供目标版本")
                Return Not HasIniKey(Version.Path & "PCL\Setup.ini", Key)
        End Select
    End Function

    ''' <summary>
    ''' 读取设置。
    ''' </summary>
    Private Sub Read(Key As String, ByRef E As SetupEntry, Version As McVersion)
        Try
            If Not E.State = 0 Then Return
            Dim SourceValue As String = Nothing '先用 String 储存，避免类型转换
            Select Case E.Source
                Case SetupSource.Normal
                    SourceValue = ReadIni("Setup", Key, E.DefaultValueEncoded)
                Case SetupSource.Registry
                    SourceValue = ReadReg(Key, E.DefaultValueEncoded)
                Case SetupSource.Version
                    If Version Is Nothing Then
                        Throw New Exception("读取版本设置 " & Key & " 时未提供目标版本")
                    Else
                        SourceValue = ReadIni(Version.Path & "PCL\Setup.ini", Key, E.DefaultValueEncoded)
                    End If
            End Select
            If E.Encoded Then
                If SourceValue.Equals(E.DefaultValueEncoded) Then
                    SourceValue = E.DefaultValue
                Else
                    Try
                        SourceValue = SecretDecrypt(SourceValue, "PCL" & UniqueAddress)
                    Catch ex As Exception
                        Log(ex, "解密设置失败：" & Key, LogLevel.Developer)
                        SourceValue = E.DefaultValue
                        Setup.Set(Key, E.DefaultValue, True)
                    End Try
                End If
            End If
            E.Value = CTypeDynamic(SourceValue, E.Type)
        Catch ex As Exception
            Log(ex, "读取设置失败：" & Key, LogLevel.Hint)
            E.Value = CTypeDynamic(E.DefaultValue, E.Type)
        End Try
    End Sub

    '对部分设置强制赋值
    Private Function ForceValue(Key As String) As String
#If BETA Then
        If Key = "UiLauncherTheme" Then Return "0"
#End If
        If Key = "UiHiddenPageLink" Then Return True
        If Key = "UiHiddenSetupLink" Then Return True
        Return Nothing
    End Function

#End Region

#Region "Launch"

    '切换选择
    Public Sub LaunchVersionSelect(Value As String)
        Log("[Setup] 当前选择的 Minecraft 版本：" & Value)
        WriteIni(PathMcFolder & "PCL.ini", "Version", If(IsNothing(McVersionCurrent), "", McVersionCurrent.Name))
    End Sub
    Public Sub LaunchFolderSelect(Value As String)
        Log("[Setup] 当前选择的 Minecraft 文件夹：" & Value.ToString.Replace("$", Path))
        PathMcFolder = Value.ToString.Replace("$", Path)
    End Sub

    '游戏内存
    Public Sub LaunchRamType(Type As Integer)
        If FrmSetupLaunch Is Nothing Then Return
        FrmSetupLaunch.RamType(Type)
    End Sub

    '离线皮肤
    Public Sub LaunchSkinType(Value As Integer)
        RunInUi(Sub()
                    If Not IsNothing(FrmSetupLaunch) Then
                        Select Case Value
                            Case 0, 1, 2 '默认
                                FrmSetupLaunch.PanSkinID.Visibility = Visibility.Collapsed
                                FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Collapsed
                            Case 3 '正版
                                FrmSetupLaunch.PanSkinID.Visibility = Visibility.Visible
                                FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Collapsed
                            Case 4 '自定义
                                FrmSetupLaunch.PanSkinID.Visibility = Visibility.Collapsed
                                FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Visible
                        End Select
                        FrmSetupLaunch.CardSkin.TriggerForceResize()
                    End If
                    PageLaunchLeft.SkinLegacy.Start()
                End Sub)
    End Sub
    Public Sub LaunchSkinID(Value As String)
        PageLaunchLeft.SkinLegacy.Start()
    End Sub

#End Region

#Region "Tool"

    Public Sub ToolDownloadThread(Value As Integer)
        NetTaskThreadLimit = Value + 1
    End Sub
    Public Sub ToolDownloadCert(Value As Boolean)
        ServicePointManager.ServerCertificateValidationCallback =
        Function(Sender, Certificate, Chain, Failure)
            Dim Request As HttpWebRequest = TryCast(Sender, HttpWebRequest)
            If Failure = Net.Security.SslPolicyErrors.None Then Return True '已通过验证
            '基于 #3018 和 #5879，只在访问正版登录 API 时跳过证书验证
            Log($"[System] 未通过 SSL 证书验证（{Failure}），提供的证书为 {Certificate?.Subject}，URL：{Request?.Address}", LogLevel.Debug)
            If Request Is Nothing Then
                Return Not Value
            ElseIf Request.Address.Host.Contains("xboxlive") OrElse Request.Address.Host.Contains("minecraftservices") Then
                Return Not Value '根据设置决定是否忽略错误
            Else
                Return False
            End If
        End Function
    End Sub
    Public Sub ToolDownloadSpeed(Value As Integer)
        If Value <= 14 Then
            NetTaskSpeedLimitHigh = (Value + 1) * 0.1 * 1024 * 1024L
        ElseIf Value <= 31 Then
            NetTaskSpeedLimitHigh = (Value - 11) * 0.5 * 1024 * 1024L
        ElseIf Value <= 41 Then
            NetTaskSpeedLimitHigh = (Value - 21) * 1024 * 1024L
        Else
            NetTaskSpeedLimitHigh = -1
        End If
    End Sub

#End Region

#Region "UI"

    '启动器
    Public Sub UiLauncherTransparent(Value As Integer)
        FrmMain.Opacity = Value / 1000 + 0.4
    End Sub
    Public Sub UiLauncherTheme(Value As Integer)
        ThemeRefresh(Value)
    End Sub
    Public Sub UiBackgroundColorful(Value As Boolean)
        ThemeRefresh()
    End Sub

    '背景图片
    Public Sub UiBackgroundOpacity(Value As Integer)
        FrmMain.ImgBack.Opacity = Value / 1000
    End Sub
    Public Sub UiBackgroundBlur(Value As Integer)
        If Value = 0 Then
            FrmMain.ImgBack.Effect = Nothing
        Else
            FrmMain.ImgBack.Effect = New Effects.BlurEffect With {.Radius = Value + 1}
        End If
        FrmMain.ImgBack.Margin = New Thickness(-(Value + 1) / 1.8)
    End Sub
    Public Sub UiBackgroundSuit(Value As Integer)
        If IsNothing(FrmMain.ImgBack.Background) Then Return
        Dim Width As Double = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
        Dim Height As Double = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
        If Value = 0 Then
            '智能：当图片较小时平铺，较大时适应
            If Width < FrmMain.PanMain.ActualWidth / 2 AndAlso Height < FrmMain.PanMain.ActualHeight / 2 Then
                Value = 4 '平铺
            Else
                Value = 2 '适应
            End If
        End If
        CType(FrmMain.ImgBack.Background, ImageBrush).TileMode = TileMode.None
        CType(FrmMain.ImgBack.Background, ImageBrush).Viewport = New Rect(0, 0, 1, 1)
        CType(FrmMain.ImgBack.Background, ImageBrush).ViewportUnits = BrushMappingMode.RelativeToBoundingBox
        Select Case Value
            Case 1 '居中
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Center
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Center
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 2 '适应
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.UniformToFill
                FrmMain.ImgBack.Width = Double.NaN
                FrmMain.ImgBack.Height = Double.NaN
            Case 3 '拉伸
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.Fill
                FrmMain.ImgBack.Width = Double.NaN
                FrmMain.ImgBack.Height = Double.NaN
            Case 4 '平铺
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                CType(FrmMain.ImgBack.Background, ImageBrush).TileMode = TileMode.Tile
                CType(FrmMain.ImgBack.Background, ImageBrush).Viewport = New Rect(0, 0, CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width, CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height)
                CType(FrmMain.ImgBack.Background, ImageBrush).ViewportUnits = BrushMappingMode.Absolute
                FrmMain.ImgBack.Width = Double.NaN
                FrmMain.ImgBack.Height = Double.NaN
            Case 5 '左上
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 6 '右上
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 7 '左下
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 8 '右下
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
        End Select
    End Sub

    '主页
    Public Sub UiCustomType(Value As Integer)
        If FrmSetupUI Is Nothing Then Return
        Select Case Value
            Case 0 '无
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
            Case 1 '本地
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。"
                FrmSetupUI.HintCustom.EventType = ""
                FrmSetupUI.HintCustom.EventData = ""
            Case 2 '联网
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！"
                FrmSetupUI.HintCustom.EventType = "打开网页"
                FrmSetupUI.HintCustom.EventData = "https://github.com/Meloong-Git/PCL/discussions/2528"
            Case 3 '预设
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
        End Select
        FrmSetupUI.CardCustom.TriggerForceResize()
    End Sub

    '顶部栏
    Public Sub UiLogoType(Value As Integer)
        Select Case Value
            Case 0 '无
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Visible
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed
                End If
            Case 1 '默认
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Visible
                FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed
                End If
            Case 2 '文本
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.LabTitleLogo.Visibility = Visibility.Visible
                FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Visible
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed
                End If
                Setup.Load("UiLogoText", True)
            Case 3 '图片
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.ImageTitleLogo.Visibility = Visibility.Visible
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Visible
                End If
                Try
                    FrmMain.ImageTitleLogo.Source = Path & "PCL\Logo.png"
                Catch ex As Exception
                    FrmMain.ImageTitleLogo.Source = Nothing
                    Log(ex, "显示标题栏图片失败", LogLevel.Msgbox)
                End Try
        End Select
        Setup.Load("UiLogoLeft", True)
        If Not IsNothing(FrmSetupUI) Then FrmSetupUI.CardLogo.TriggerForceResize()
    End Sub
    Public Sub UiLogoText(Value As String)
        FrmMain.LabTitleLogo.Text = Value
    End Sub
    Public Sub UiLogoLeft(Value As Boolean)
        FrmMain.PanTitleMain.ColumnDefinitions(0).Width = New GridLength(If(Value AndAlso (Setup.Get("UiLogoType") = 0), 0, 1), GridUnitType.Star)
    End Sub

    '功能隐藏
    Public Sub UiHiddenPageLink(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenPageDownload(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenPageSetup(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenPageOther(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenFunctionSelect(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenFunctionModUpdate(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenFunctionHidden(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenSetupLaunch(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenSetupUi(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenSetupLink(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenSetupSystem(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherHelp(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherFeedback(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherVote(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherAbout(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherTest(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub


#End Region

#Region "System"

    '调试选项
    Public Sub SystemDebugMode(Value As Boolean)
        ModeDebug = Value
    End Sub
    Public Sub SystemDebugAnim(Value As Integer)
        AniSpeed = If(Value >= 30, 200, MathClamp(Value * 0.1 + 0.1, 0.1, 3))
    End Sub

#End Region

#Region "Version"

    '游戏内存
    Public Sub VersionRamType(Type As Integer)
        If FrmVersionSetup Is Nothing Then Return
        FrmVersionSetup.RamType(Type)
    End Sub

    '服务器
    Public Sub VersionServerLogin(Type As Integer)
        If FrmVersionSetup Is Nothing Then Return
        '为第三方登录清空缓存以更新描述
        WriteIni(PathMcFolder & "PCL.ini", "VersionCache", "")
        If PageVersionLeft.Version Is Nothing Then Return
        PageVersionLeft.Version = New McVersion(PageVersionLeft.Version.Name).Load()
        LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub

#End Region

End Class
