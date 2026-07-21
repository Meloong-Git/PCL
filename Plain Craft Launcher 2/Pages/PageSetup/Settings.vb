Imports System.Security.Cryptography

Public Class Settings

    Public Shared ReadOnly Entries As Dictionary(Of String, Setting) = (New List(Of Setting) From {
        New Setting("Identify", "", Source:=Sources.Registry),
        New Setting("WindowHeight", 550),
        New Setting("WindowWidth", 900),
        New Setting("AprilYear", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("Potatoes", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("HintDownloadThread", False, Source:=Sources.Registry),
        New Setting("HintNotice", 0, Source:=Sources.Registry),
        New Setting("HintDownload", 0, Source:=Sources.Registry),
        New Setting("HintHide", False, Source:=Sources.Registry),
        New Setting("HintHandInstall", False, Source:=Sources.Registry),
        New Setting("HintBuy", False, Source:=Sources.Registry),
        New Setting("HintClearRubbish", 0, Source:=Sources.Registry),
        New Setting("HintUpdateMod", False, Source:=Sources.Registry),
        New Setting("HintCustomCommand", False, Source:=Sources.Registry),
        New Setting("HintCustomWarn", False, Source:=Sources.Registry),
        New Setting("HintMoreAdvancedSetup", False, Source:=Sources.Registry),
        New Setting("HintIndieSetup", False, Source:=Sources.Registry),
        New Setting("HintExportConfig", False, Source:=Sources.Registry),
        New Setting("HintSnapshot", False, Source:=Sources.Registry),
        New Setting("SystemEulaVersion", 0, Source:=Sources.Registry),
        New Setting("SystemCount", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemLaunchCount", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemLastVersionReg", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemHighestSavedBetaVersionReg", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemHighestBetaVersionReg", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemHighestAlphaVersionReg", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemHelpVersion", 0, Source:=Sources.Registry),
        New Setting("SystemDebugMode", False, Source:=Sources.Registry, OnChanged:=Sub() Logger.Instance.MinLevel = If(ModeDebug, LogLevel.Trace, LogLevel.Info)),
        New Setting("SystemDebugAnim", 9, Source:=Sources.Registry),
        New Setting("SystemDebugDelay", False, Source:=Sources.Registry),
        New Setting("SystemDebugSkipCopy", False, Source:=Sources.Registry),
        New Setting("SystemSystemCache", "", Source:=Sources.Registry),
        New Setting("SystemSystemUpdate", 0),
        New Setting("SystemSystemActivity", 0),
        New Setting("SystemSystemTelemetry", True, Source:=Sources.Registry),
        New Setting("CacheDrops", "", Source:=Sources.Registry),
        New Setting("CacheConfig", 0, Source:=Sources.Registry),
        New Setting("CacheExportConfig", "", Source:=Sources.Registry),
        New Setting("CacheSavedPageUrl", "", Source:=Sources.Registry),
        New Setting("CacheSavedPageVersion", "", Source:=Sources.Registry),
        New Setting("CacheMsOAuthRefresh", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsAccess", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsProfileJson", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsUuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Migrated", False, Source:=Sources.Registry),
        New Setting("CacheMsV2OAuthRefresh", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Access", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2ProfileJson", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Uuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Name", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Expires", 0L, Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideAccess", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideClient", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideUuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideUsername", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNidePass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideServer", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthAccess", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthClient", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthUuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthUsername", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthPass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthServerServer", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthServerName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthServerRegister", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheDownloadFolder", "", Source:=Sources.Registry),
        New Setting("CacheJavaListVersion", 0, Source:=Sources.Registry),
        New Setting("LoginRemember", True, Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginLegacyName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginMsJson", "{}", Source:=Sources.Registry, Encrypted:=True), '{UserName: OAuthToken, ...}
        New Setting("LoginNideEmail", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginNidePass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginAuthEmail", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginAuthPass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginType", McLoginType.Legacy, Source:=Sources.Registry),
        New Setting("LoginPageType", 0),
        New Setting("LaunchSkinID", "", Source:=Sources.Registry, OnChanged:=Sub() PageLaunchLeft.SkinLegacy.Start()),
        New Setting("LaunchSkinType", 0, Source:=Sources.Registry, OnChanged:=AddressOf PageSetupLaunch.UpdateSkinType),
        New Setting("LaunchSkinSlim", False, Source:=Sources.Registry),
        New Setting("LaunchFolderSelect", ""),
        New Setting("LaunchFolders", "", Source:=Sources.Registry),
        New Setting("LaunchArgumentTitle", ""),
        New Setting("LaunchArgumentInfo", "PCL"),
        New Setting("LaunchArgumentJavaSelect", "", Source:=Sources.Registry),
        New Setting("LaunchArgumentJavaAll", "[]", Source:=Sources.Registry),
        New Setting("LaunchArgumentIndie", 0),
        New Setting("LaunchArgumentIndieV2", 4),
        New Setting("LaunchArgumentVisible", 5, Source:=Sources.Registry),
        New Setting("LaunchArgumentPriority", 1, Source:=Sources.Registry),
        New Setting("LaunchArgumentWindowWidth", 854),
        New Setting("LaunchArgumentWindowHeight", 480),
        New Setting("LaunchArgumentWindowType", 1),
        New Setting("LaunchArgumentRam", False, Source:=Sources.Registry),
        New Setting("LaunchAdvanceJvm", "-XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=True -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True"),
        New Setting("LaunchAdvanceGame", ""),
        New Setting("LaunchAdvanceRun", ""),
        New Setting("LaunchAdvanceRunWait", True),
        New Setting("LaunchAdvanceDisableJLW", False),
        New Setting("LaunchAdvanceDisableLUA", False),
        New Setting("LaunchAdvanceGraphicCard", True, Source:=Sources.Registry),
        New Setting("LaunchAdvanceGC", 4),
        New Setting("LaunchRamType", 0, OnChanged:=AddressOf PageSetupLaunch.UpdateRamType),
        New Setting("LaunchRamCustom", 15),
        New Setting("LinkLastAutoJoinInviteCode", "", Source:=Sources.Registry),
        New Setting("LinkLatencyMode", 0, Source:=Sources.Registry),
        New Setting("LinkCustomPeer", ""),
        New Setting("LinkEasyTierVersion", -1, Source:=Sources.Registry),
        New Setting("ToolHelpChinese", True, Source:=Sources.Registry),
        New Setting("ToolDownloadThread", 63, Source:=Sources.Registry),
        New Setting("ToolDownloadSpeed", 42, Source:=Sources.Registry, OnChanged:=AddressOf ModNet.UpdateNetTaskSpeedLimitHigh),
        New Setting("ToolDownloadSource", 1, Source:=Sources.Registry),
        New Setting("ToolDownloadVersion", 1, Source:=Sources.Registry),
        New Setting("ToolDownloadTranslate", 0, Source:=Sources.Registry),
        New Setting("ToolDownloadTranslateV2", 1, Source:=Sources.Registry),
        New Setting("ToolDownloadIgnoreQuilt", True, Source:=Sources.Registry),
        New Setting("ToolDownloadCert", False, Source:=Sources.Registry, OnChanged:=AddressOf ModNet.ShouldValidateSslCertificateOnLogin),
        New Setting("ToolDownloadMod", 2, Source:=Sources.Registry),
        New Setting("ToolModLocalNameStyle", 0, Source:=Sources.Registry),
        New Setting("ToolUpdateRelease", False, Source:=Sources.Registry),
        New Setting("ToolUpdateSnapshot", False, Source:=Sources.Registry),
        New Setting("ToolUpdateReleaseLast", "", Source:=Sources.Registry),
        New Setting("ToolUpdateSnapshotLast", "", Source:=Sources.Registry),
        New Setting("UiLauncherTransparent", 600, OnChanged:=Sub(Value As Integer) If FrmMain IsNot Nothing Then FrmMain.Opacity = Value / 1000 + 0.4), '避免与 PCL1 设置冲突（UiLauncherOpacity）
        New Setting("UiLauncherHue", 180),
        New Setting("UiLauncherSat", 80),
        New Setting("UiLauncherDelta", 90),
        New Setting("UiLauncherLight", 20),
        New Setting("UiLauncherTheme", 0, OnChanged:=AddressOf ThemeRefresh),
        New Setting("UiLauncherThemeHide", "0|1|2|3|4", Source:=Sources.Registry, Encrypted:=True),
        New Setting("UiLauncherThemeHide2", "0|1|2|3|4", Source:=Sources.Registry, Encrypted:=True),
        New Setting("UiLauncherLogo", True),
        New Setting("UiLauncherEmail", False),
        New Setting("UiBackgroundColorful", True, OnChanged:=Sub() ThemeRefresh()), '不传入参数
        New Setting("UiBackgroundOpacity", 1000, OnChanged:=AddressOf FormMain.UpdateBackgroundAndTitleBar),
        New Setting("UiBackgroundBlur", 0, OnChanged:=AddressOf FormMain.UpdateBackgroundAndTitleBar),
        New Setting("UiBackgroundSuit", 0, OnChanged:=AddressOf FormMain.UpdateBackgroundAndTitleBar),
        New Setting("UiCustomType", 0, OnChanged:=AddressOf PageSetupUI.OnMainPageTypeChanged),
        New Setting("UiCustomPreset", 0),
        New Setting("UiCustomNet", ""),
        New Setting("UiLogoType", 1, OnChanged:=AddressOf FormMain.UpdateBackgroundAndTitleBar),
        New Setting("UiLogoText", "", OnChanged:=AddressOf FormMain.UpdateBackgroundAndTitleBar),
        New Setting("UiLogoLeft", False, OnChanged:=AddressOf FormMain.UpdateBackgroundAndTitleBar),
        New Setting("UiMusicVolume", 500),
        New Setting("UiMusicStop", False),
        New Setting("UiMusicStart", False),
        New Setting("UiMusicRandom", True),
        New Setting("UiMusicAuto", True),
        New Setting("UiHiddenPageDownload", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenPageLink", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenPageSetup", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenPageOther", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenFunctionSelect", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenFunctionModUpdate", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenFunctionHidden", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenSetupLaunch", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenSetupUi", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenSetupLink", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenSetupSystem", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenOtherHelp", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenOtherFeedback", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenOtherVote", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenOtherAbout", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("UiHiddenOtherTest", False, OnChanged:=AddressOf PageSetupUI.HiddenRefresh),
        New Setting("VersionAdvanceJvm", "", Source:=Sources.Instance),
        New Setting("VersionAdvanceGame", "", Source:=Sources.Instance),
        New Setting("VersionAdvanceAssets", 0, Source:=Sources.Instance),
        New Setting("VersionAdvanceAssetsV2", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceRun", "", Source:=Sources.Instance),
        New Setting("VersionAdvanceRunWait", True, Source:=Sources.Instance),
        New Setting("VersionAdvanceDisableJLW", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceDisableLUA", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceDisableModUpdate", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceGC", 0, Source:=Sources.Instance),
        New Setting("VersionRamType", 2, Source:=Sources.Instance, OnChanged:=AddressOf PageInstanceSetup.OnVersionRamTypeChanged),
        New Setting("VersionRamCustom", 15, Source:=Sources.Instance),
        New Setting("VersionRamOptimize", 0, Source:=Sources.Instance),
        New Setting("VersionArgumentTitle", "", Source:=Sources.Instance),
        New Setting("VersionArgumentInfo", "", Source:=Sources.Instance),
        New Setting("VersionArgumentIndie", -1, Source:=Sources.Instance),
        New Setting("VersionArgumentIndieV2", False, Source:=Sources.Instance),
        New Setting("VersionArgumentJavaSelect", "使用全局设置", Source:=Sources.Instance),
        New Setting("VersionArgumentJavaV2", 0, Source:=Sources.Instance),
        New Setting("VersionArgumentJavaRange", "", Source:=Sources.Instance),
        New Setting("VersionServerEnter", "", Source:=Sources.Instance),
        New Setting("VersionServerLogin", 0, Source:=Sources.Instance, OnChanged:=AddressOf PageInstanceSetup.OnVersionServerLoginChanged),
        New Setting("VersionServerNide", "", Source:=Sources.Instance),
        New Setting("VersionServerAuthRegister", "", Source:=Sources.Instance),
        New Setting("VersionServerAuthName", "", Source:=Sources.Instance),
        New Setting("VersionServerAuthServer", "", Source:=Sources.Instance)
    }).ToDictionary(Function(e) e.Key)

    Public Enum Sources
        Normal
        Registry
        Instance
    End Enum
    Public Class Setting
        Public Key As String
        Public Encrypted As Boolean
        Public DefaultValue As Object
        Public Source As Sources
        ''' <summary>
        ''' 当设置项的值实际被改变时触发，参数为新值。
        ''' </summary>
        Public OnChanged As Action(Of Object)
        ''' <summary>
        ''' 该设置的值的实际类型。
        ''' </summary>
        Public Type As Type
        Public Sub New(Key As String, Value As Object, Optional Source As Sources = Sources.Normal, Optional Encrypted As Boolean = False, Optional OnChanged As Action(Of Object) = Nothing)
            Try
                Me.Key = Key
                Me.DefaultValue = Value
                Me.Encrypted = Encrypted
                Me.Source = Source
                Me.Type = If(Value, "").GetType
                Me.OnChanged = OnChanged
            Catch ex As Exception
                Logger.Error(ex, "初始化设置项失败") '#5095 的 fallback
            End Try
        End Sub

        ''' <summary>
        ''' 读取过的设置的缓存，若从未读取过则为 Nothing。
        ''' 若为版本独立设置，键为版本路径；否则，键为空字符串。
        ''' </summary>
        Public ValueCache As New ConcurrentDictionary(Of String, Object)
        Public Function GetCache(Instance As McInstance)
            Dim Key As String = If(Source = Sources.Instance, Instance.PathVersion, "")
            Dim Result = Nothing
            Return If(ValueCache.TryGetValue(Key, Result), Result, Nothing)
        End Function
        Public Sub [Set](Value As Object, Instance As McInstance)
            Dim Key As String = If(Source = Sources.Instance, Instance.PathVersion, "")
            ValueCache(Key) = Value
        End Sub

        ''' <summary>
        ''' 立即将当前的 Value 写入对应的注册表或文件。
        ''' </summary>
        Public Sub Save(Optional Instance As McInstance = Nothing)
            Dim Value As String = GetCache(Instance)
            Logger.Trace($"保存设置：{Key} {If(Encrypted, "", "= " & Value)}{If(Instance Is Nothing, "", $"（实例：{Instance?.PathVersion}）")}")
            If Encrypted Then
                Try
                    If Value Is Nothing Then Value = ""
                    Value = DesEncrypt(Value, "PCL" & Identify)
                Catch ex As Exception
                    Logger.Warn(ex, $"加密设置失败：{Key}")
                End Try
            End If
            Select Case Source
                Case Sources.Normal
                    WriteIni("Setup", Key, Value)
                Case Sources.Registry
                    WriteReg(Key, Value)
                Case Sources.Instance
                    If Instance Is Nothing Then Throw New Exception($"保存版本独立设置 {Key} 时未提供目标版本")
                    WriteIni(Instance.PathVersion & "PCL\Setup.ini", Key, Value)
            End Select
        End Sub

    End Class

    ''' <summary>
    ''' 改变某个设置项的值。
    ''' </summary>
    Public Shared Sub [Set](Key As String, Value As Object, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        Try
            '判断是否一致
            Value = CTypeDynamic(Value, Entry.Type)
            If Entry.GetCache(Instance) Is Nothing Then [Get](Key, Instance)
            If Entry.GetCache(Instance) = Value Then Return
            '设置新值
            Entry.Set(Value, Instance)
            Entry.Save(Instance)
            '触发改变事件（必须在保存之后，以保证 VersionServerLogin 之类的在事件中读取到的是最新的值）
            If Entry.OnChanged IsNot Nothing Then Entry.OnChanged.Invoke(Value)
        Catch ex As Exception
            Logger.Error(ex, $"设置设置项时出错（{Key}, {Value}）")
        End Try
    End Sub
    ''' <summary>
    ''' 写入某个未经加密的设置项。
    ''' 若该设置项经过了加密，则会抛出异常。
    ''' </summary>
    Public Shared Sub SetSafe(Key As String, Value As Object, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        If Entry.Encrypted Then Throw New InvalidOperationException("禁止写入加密设置项：" & Key)
        [Set](Key, Value, Instance)
    End Sub

    ''' <summary>
    ''' 获取某个设置项的值。
    ''' </summary>
    Public Shared Function [Get](Key As String, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        Dim Value = Entry.GetCache(Instance)
        If Value IsNot Nothing Then Return Value
        '对部分设置强制赋值
        Static ForcedSettings As New Dictionary(Of String, Object) From {
            {"UiHiddenPageLink", True},
            {"UiHiddenSetupLink", True}
        }
        Dim ForcedSetting As Object = Nothing
        If ForcedSettings.TryGetValue(Key, ForcedSetting) Then
            Entry.Set(ForcedSetting, Instance)
            Return ForcedSetting
        End If
        '正常读取
        Try
            Dim GotValue As String = Nothing '先用 String 储存，避免类型转换
            Dim DefaultValue As String = If(Entry.Encrypted, DesEncrypt(Entry.DefaultValue, "PCL" & Identify), Entry.DefaultValue)
            Select Case Entry.Source
                Case Sources.Normal
                    GotValue = ReadIni("Setup", Key, DefaultValue)
                Case Sources.Registry
                    GotValue = ReadReg(Key, DefaultValue)
                Case Sources.Instance
                    If Instance Is Nothing Then
                        Throw New Exception($"读取版本设置 {Key} 时未提供目标版本")
                    Else
                        GotValue = ReadIni(Instance.PathVersion & "PCL\Setup.ini", Key, DefaultValue)
                    End If
            End Select
            If Entry.Encrypted Then
                If GotValue = DefaultValue Then
                    GotValue = Entry.DefaultValue
                Else
                    Try
                        GotValue = DesDecrypt(GotValue, "PCL" & Identify)
                    Catch ex As Exception
                        Logger.Warn(ex, $"解密设置失败：{Key}")
                        GotValue = Entry.DefaultValue
                        Entry.Set(Entry.DefaultValue, Instance)
                        Entry.Save(Instance)
                    End Try
                End If
            End If
            Entry.Set(CTypeDynamic(GotValue, Entry.Type), Instance)
        Catch ex As Exception
            Logger.Error(ex, $"读取设置失败：{Key}", LogBehavior.Toast)
            Entry.Set(CTypeDynamic(Entry.DefaultValue, Entry.Type), Instance)
        End Try
        Return Entry.GetCache(Instance)
    End Function
    Public Shared Function [Get](Of T)(Key As String, Optional Instance As McInstance = Nothing) As T
        Return [Get](Key, Instance)
    End Function
    ''' <summary>
    ''' 获取某个未经加密的设置项的值。
    ''' 若该设置项经过了加密，则会抛出异常。
    ''' </summary>
    Public Shared Function GetSafe(Key As String, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        If Entry.Encrypted Then Throw New InvalidOperationException("禁止读取加密设置项：" & Key)
        Return [Get](Key, Instance)
    End Function

    ''' <summary>
    ''' 初始化某个设置项的值。
    ''' </summary>
    Public Shared Sub Reset(Key As String, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        Try
            '判断是否一致
            If Entry.GetCache(Instance) Is Nothing Then [Get](Key, Instance)
            If Entry.GetCache(Instance) = Entry.DefaultValue Then Return
            '设置新值
            Entry.Set(Entry.DefaultValue, Instance)
            Select Case Entry.Source
                Case Sources.Normal
                    DeleteIniKey("Setup", Key)
                Case Sources.Registry
                    DeleteReg(Key)
                Case Sources.Instance
                    If Instance Is Nothing Then Throw New Exception($"重置版本设置 {Key} 时未提供目标版本")
                    DeleteIniKey(Instance.PathVersion & "PCL\Setup.ini", Key)
            End Select
            '触发改变事件
            If Entry.OnChanged IsNot Nothing Then Entry.OnChanged.Invoke(Entry.DefaultValue)
        Catch ex As Exception
            Logger.Error(ex, $"重置设置项时出错（{Key}）")
        End Try
    End Sub

    ''' <summary>
    ''' 获取某个设置项的默认值。
    ''' </summary>
    Public Shared Function GetDefault(Key As String) As String
        Return Entries(Key).DefaultValue
    End Function

    ''' <summary>
    ''' 对应的注册表或文件是否已经储存了某个设置项。
    ''' 若改成了和默认值一样的，在 2.12.3- 会储存更改，在 2.12.4+ 不会储存更改。
    ''' </summary>
    Public Shared Function HasSaved(Key As String, Optional Instance As McInstance = Nothing) As Boolean
        Select Case Entries(Key).Source
            Case Sources.Normal
                Return HasIniKey("Setup", Key)
            Case Sources.Registry
                Return HasReg(Key)
            Case Else 'Source.Instance
                If Instance Is Nothing Then Throw New Exception($"判断版本设置 {Key} 是否存在时未提供目标版本")
                Return HasIniKey(Instance.PathVersion & "PCL\Setup.ini", Key)
        End Select
    End Function

#Region "DES 加密"

    Private Shared ReadOnly desInitialVector As Byte() = Encoding.UTF8.GetBytes("95168702")

    ''' <summary>
    ''' 使用 DES 对称加密算法加密字符串。
    ''' </summary>
    Private Shared Function DesEncrypt(sourceString As String, Optional key As String = Nothing) As String
        key = If(key Is Nothing, "@;$ Abv2", key.GetStableHashCode().ToString().EnsureLength("X"c, 8).Substring(0, 8))
        Dim btKey As Byte() = Encoding.UTF8.GetBytes(key)
        Using des As New DESCryptoServiceProvider()
            Using ms As New MemoryStream()
                Using cs As New CryptoStream(ms, des.CreateEncryptor(btKey, desInitialVector), CryptoStreamMode.Write)
                    Dim inData As Byte() = Encoding.UTF8.GetBytes(sourceString)
                    cs.Write(inData, 0, inData.Length)
                    cs.FlushFinalBlock()
                    Return Convert.ToBase64String(ms.ToArray())
                End Using
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 使用 DES 对称加密算法解密字符串。
    ''' </summary>
    Private Shared Function DesDecrypt(encryptedString As String, Optional key As String = Nothing) As String
        key = If(key Is Nothing, "@;$ Abv2", key.GetStableHashCode().ToString().EnsureLength("X"c, 8).Substring(0, 8))
        Dim btKey As Byte() = Encoding.UTF8.GetBytes(key)
        Using des As New DESCryptoServiceProvider()
            Using ms As New MemoryStream()
                Using cs As New CryptoStream(ms, des.CreateDecryptor(btKey, desInitialVector), CryptoStreamMode.Write)
                    Dim inData As Byte() = Convert.FromBase64String(encryptedString)
                    cs.Write(inData, 0, inData.Length)
                    cs.FlushFinalBlock()
                    Return Encoding.UTF8.GetString(ms.ToArray())
                End Using
            End Using
        End Using
    End Function

#End Region

End Class
