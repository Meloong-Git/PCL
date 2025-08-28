Imports System.ComponentModel
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Management
Imports System.Runtime.InteropServices
Imports PCL.Core.IO
Imports PCL.Core.UI
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Utils.OS
Imports PCL.Core.Utils.Secret

Friend Module ModSecret

#Region "杂项"

#If DEBUG Then
    Public Const RegFolder As String = "PCLCEDebug" '社区开发版的注册表与社区常规版的注册表隔离，以防数据冲突
#Else
    Public Const RegFolder As String = "PCLCE" 'PCL 社区版的注册表与 PCL 的注册表隔离，以防数据冲突
#End If
    '用于微软登录的 ClientId
    Public ReadOnly OAuthClientId As String = EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly := True).ReplaceNullOrEmpty()
    'CurseForge API Key
    Public ReadOnly CurseForgeAPIKey As String = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly := True).ReplaceNullOrEmpty()
    '遥测鉴权密钥
    Public ReadOnly TelemetryKey As String = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly := True).ReplaceNullOrEmpty()
    'Natayark ID Client Id
    Public ReadOnly NatayarkClientId As String = EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly := True).ReplaceNullOrEmpty()
    'Natayark ID Client Secret，需要经过 PASSWORD HASH 处理（https://uutool.cn/php-password/）
    Public ReadOnly NatayarkClientSecret As String = EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly := True).ReplaceNullOrEmpty()
    '联机服务根地址
    Public ReadOnly LinkServers As String() = EnvironmentInterop.GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly := True).ReplaceNullOrEmpty().Split(";")

    Friend Sub SecretOnApplicationStart()
        '提升 UI 线程优先级
        Thread.CurrentThread.Priority = ThreadPriority.Highest
        '确保 .NET Framework 版本
        Try
            Dim VersionTest As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
        Catch ex As UriFormatException '修复 #3555
            Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User)
            Dim VersionTest As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
        End Try
        '检测当前文件夹权限
        Dim dataPath = FileService.DataPath
        Try
            Directory.CreateDirectory(dataPath)
        Catch ex As Exception
            MsgBox($"PCL 无法创建 PCL 文件夹（{dataPath}），请尝试：" & vbCrLf &
                  "1. 将 PCL 移动到其他文件夹" & If(Path.StartsWithF("C:", True), "，例如 C 盘和桌面以外的其他位置。", "。") & vbCrLf &
                  "2. 删除当前目录中的 PCL 文件夹，然后再试。" & vbCrLf &
                  "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。",
                MsgBoxStyle.Critical, "运行环境错误")
            Environment.[Exit](ProcessReturnValues.Cancel)
        End Try
        If Not Files.CheckPermission(Path & "PCL") Then
            MsgBox("PCL 没有对当前文件夹的写入权限，请尝试：" & vbCrLf &
                  "1. 将 PCL 移动到其他文件夹" & If(Path.StartsWithF("C:", True), "，例如 C 盘和桌面以外的其他位置。", "。") & vbCrLf &
                  "2. 删除当前目录中的 PCL 文件夹，然后再试。" & vbCrLf &
                  "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。",
                MsgBoxStyle.Critical, "运行环境错误")
            Environment.[Exit](ProcessReturnValues.Cancel)
        End If
        '社区版提示
        If Setup.Get("UiLauncherCEHint") Then
            Dim count As Integer = Setup.Get("UiLauncherCEHintCount")
            If count <= 0 Then
                ShowCEAnnounce()
                count = 11
            End If
            Setup.Set("UiLauncherCEHintCount", count - 1)
        End If
    End Sub
    ''' <summary>
    ''' 展示社区版提示
    ''' </summary>
    ''' <param name="IsUpdate">是否为更新时启动</param>
    Public Sub ShowCEAnnounce(Optional IsUpdate As Boolean = False)
        MyMsgBox($"你正在使用来自 PCL-Community 的 PCL 社区版本，遇到问题请不要向官方仓库反馈！
PCL-Community 及其成员与龙腾猫跃无从属关系，且均不会为您的使用做担保。

如果你是意外下载的社区版，建议下载官方版 PCL 使用。

该版本与官方版本的特性区别：
- 联网通知：暂时没有，在做了在做了.jpg
- 主题切换：不会制作，这是需要赞助解锁的纪念性质的功能
- 百宝箱：部分内容更改和缺失，主线分支没有提供相关内容{If(IsUpdate, $"{vbCrLf}{vbCrLf}该提示总会在更新启动器时展示一次。", "")}", "社区版本说明", "我知道了")
    End Sub

    ''' <summary>
    ''' 获取原始的设备标识码
    ''' </summary>
    ''' <returns></returns>
    Friend Function SecretGetRawCode() As String
        Return Identify.RawCode
    End Function

    ''' <summary>
    ''' 获取设备的短标识码
    ''' </summary>
    Friend Function SecretGetUniqueAddress() As String
        Return Identify.LaunchId
    End Function

    Private _EncryptKeyCache As String = Nothing
    Private ReadOnly _cacheEncryptKeyLock As New Object()
    ''' <summary>
    ''' 获取 AES 加密密钥
    ''' </summary>
    ''' <returns></returns>
    Friend Function SecretGetEncryptKey() As String
        SyncLock _cacheEncryptKeyLock
            If _EncryptKeyCache IsNot Nothing Then Return _EncryptKeyCache
            Dim rawCode = SecretGetRawCode()
            Using SHA512 As SHA512 = SHA512.Create()
                Dim hash As Byte() = SHA512.ComputeHash(Encoding.UTF8.GetBytes(rawCode))
                Dim key As String = BitConverter.ToString(hash).Replace("-", "")
                key = key.Substring(4, 32)
                _EncryptKeyCache = key
                Return key
            End Using
        End SyncLock
    End Function

    Friend Sub SecretLaunchJvmArgs(ByRef DataList As List(Of String))
        Dim DataJvmCustom As String = Setup.Get("VersionAdvanceJvm", instance:=McInstanceCurrent)
        DataList.Insert(0, If(DataJvmCustom = "", Setup.Get("LaunchAdvanceJvm"), DataJvmCustom)) '可变 JVM 参数
        Select Case Setup.Get("LaunchPreferredIpStack")
            Case 0
                DataList.Add("-Djava.net.preferIPv4Stack=true")
            Case 2
                DataList.Add("-Djava.net.preferIPv6Stack=true")
        End Select
        McLaunchLog("当前剩余内存：" & Math.Round(KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10) / 10 & "G")
        DataList.Add("-Xmn" & Math.Floor(PageInstanceSetup.GetRam(McInstanceCurrent) * 1024 * 0.15) & "m")
        DataList.Add("-Xmx" & Math.Floor(PageInstanceSetup.GetRam(McInstanceCurrent) * 1024) & "m")
        If Not DataList.Any(Function(d) d.Contains("-Dlog4j2.formatMsgNoLookups=true")) Then DataList.Add("-Dlog4j2.formatMsgNoLookups=true")
    End Sub

#End Region

#Region "网络鉴权"



    Friend Function SecretCdnSign(UrlWithMark As String)
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Client As HttpRequestMessage, Optional UseBrowserUserAgent As Boolean = False, Optional CustomUserAgent As String = "")
        If Url.Contains("api.curseforge.com") Then Client.Headers.Add("x-api-key", CurseForgeAPIKey)
        Dim userAgent As String = If(Not String.IsNullOrEmpty(CustomUserAgent),
                                     CustomUserAgent,
                                         If(UseBrowserUserAgent,
                                             $"PCL2/{UpstreamVersion}.{VersionBranchCode} PCLCE/{VersionStandardCode} Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0",
                                             $"PCL2/{UpstreamVersion}.{VersionBranchCode} PCLCE/{VersionStandardCode}"
                                         )
                                     )
        Client.Headers.Add("User-Agent", userAgent)

        Client.Headers.Add("Referer", "http://" & VersionCode & ".ce.open.pcl2.server/")
        If Url.Contains("pcl2ce.pysio.online/post") AndAlso Not String.IsNullOrEmpty(TelemetryKey) Then Client.Headers.Add("Authorization", TelemetryKey)
    End Sub

#End Region

#Region "字符串加解密"

    Friend Function SecretDecrptyOld(SourceString As String) As String
        Dim Key = "00000000"
        Dim btKey As Byte() = Encoding.UTF8.GetBytes(Key)
        Dim btIV As Byte() = Encoding.UTF8.GetBytes("87160295")
        Using des As DES = DES.Create()
            Using MS As New MemoryStream
                Dim inData As Byte() = Convert.FromBase64String(SourceString)
                Using cs As New CryptoStream(MS, des.CreateDecryptor(btKey, btIV), CryptoStreamMode.Write)
                    cs.Write(inData, 0, inData.Length)
                    cs.FlushFinalBlock()
                    Return Encoding.UTF8.GetString(MS.ToArray())
                End Using
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 加密字符串（优化版）。
    ''' </summary>
    Friend Function SecretEncrypt(SourceString As String) As String
        If SourceString = "" Then Return ""
        If String.IsNullOrWhiteSpace(SourceString) Then Return Nothing
        Dim Key = SecretGetEncryptKey()

        Using aes = AesCng.Create()
            aes.KeySize = 256
            aes.BlockSize = 128
            aes.Mode = CipherMode.CBC
            aes.Padding = PaddingMode.PKCS7

            Dim salt As Byte() = New Byte(31) {}
            Using rng = RandomNumberGenerator.Create()
                rng.GetBytes(salt)
            End Using

            Using deriveBytes = New Rfc2898DeriveBytes(Key, salt, 1000, HashAlgorithmName.SHA1)
                aes.Key = deriveBytes.GetBytes(aes.KeySize \ 8)
                aes.GenerateIV()
            End Using

            Using ms = New MemoryStream()
                ms.Write(salt, 0, salt.Length)
                ms.Write(aes.IV, 0, aes.IV.Length)

                Using cs = New CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write)
                    Dim data = Encoding.UTF8.GetBytes(SourceString)
                    cs.Write(data, 0, data.Length)
                End Using

                Return Convert.ToBase64String(ms.ToArray())
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 解密字符串。
    ''' </summary>
    Friend Function SecretDecrypt(SourceString As String) As String
        If SourceString = "" Then Return ""
        If String.IsNullOrWhiteSpace(SourceString) Then Return Nothing
        Dim Key = SecretGetEncryptKey()
        Dim encryptedData = Convert.FromBase64String(SourceString)

        Using aes = AesCng.Create()
            aes.KeySize = 256
            aes.BlockSize = 128
            aes.Mode = CipherMode.CBC
            aes.Padding = PaddingMode.PKCS7

            Dim salt = New Byte(31) {}
            Array.Copy(encryptedData, 0, salt, 0, salt.Length)

            Dim iv = New Byte(aes.BlockSize \ 8 - 1) {}
            Array.Copy(encryptedData, salt.Length, iv, 0, iv.Length)
            aes.IV = iv

            If encryptedData.Length < salt.Length + iv.Length Then
                Throw New ArgumentException("加密数据格式无效或已损坏")
            End If

            Using deriveBytes = New Rfc2898DeriveBytes(Key, salt, 1000, HashAlgorithmName.SHA1)
                aes.Key = deriveBytes.GetBytes(aes.KeySize \ 8)
            End Using

            Dim cipherTextLength = encryptedData.Length - salt.Length - iv.Length
            Using ms = New MemoryStream(encryptedData, salt.Length + iv.Length, cipherTextLength)
                Using cs = New CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read)
                    Using sr = New StreamReader(cs, Encoding.UTF8)
                        Return sr.ReadToEnd()
                    End Using
                End Using
            End Using
        End Using
    End Function

#End Region

#Region "主题"

#If DEBUG Then
    Public ReadOnly EnableCustomTheme As Boolean = Environment.GetEnvironmentVariable("PCL_CUSTOM_THEME") IsNot Nothing
    Private ReadOnly EnvThemeHue = Environment.GetEnvironmentVariable("PCL_THEME_HUE") '0 ~ 359
    Private ReadOnly EnvThemeSat = Environment.GetEnvironmentVariable("PCL_THEME_SAT") '0 ~ 100
    Private ReadOnly EnvThemeLight = Environment.GetEnvironmentVariable("PCL_THEME_LIGHT") '-20 ~ 20
    Private ReadOnly EnvThemeHueDelta = Environment.GetEnvironmentVariable("PCL_THEME_HUE_DELTA") '-90 ~ 90
    Private ReadOnly CustomThemeHue = If(EnvThemeHue Is Nothing, Nothing, CType(Integer.Parse(EnvThemeHue), Integer?))
    Private ReadOnly CustomThemeSat = If(EnvThemeSat Is Nothing, Nothing, CType(Integer.Parse(EnvThemeSat), Integer?))
    Private ReadOnly CustomThemeLight = If(EnvThemeLight Is Nothing, Nothing, CType(Integer.Parse(EnvThemeLight), Integer?))
    Private ReadOnly CustomThemeHueDelta = If(EnvThemeHueDelta Is Nothing, Nothing, CType(Integer.Parse(EnvThemeHueDelta), Integer?))
#End If

    Public IsDarkMode As Boolean = False

    Public ReadOnly Property ColorGray1 As MyColor
        Get
            Return If(StaticColors?.Gray1, LightStaticColors.Gray1)
        End Get
    End Property

    Public ReadOnly Property ColorGray4 As MyColor
        Get
            Return If(StaticColors?.Gray4, LightStaticColors.Gray4)
        End Get
    End Property

    Public ReadOnly Property ColorGray5 As MyColor
        Get
            Return If(StaticColors?.Gray5, LightStaticColors.Gray5)
        End Get
    End Property

    Public ReadOnly Property ColorSemiTransparent As MyColor
        Get
            Return DynamicColors.SemiTransparent
        End Get
    End Property

    Private ReadOnly Property NewColor As MyColor
        Get
            Return New MyColor()
        End Get
    End Property

    Public Class ThemeStyleStaticColors
        Public ReadOnly Gray1 As Color
        Public ReadOnly Gray2 As Color
        Public ReadOnly Gray3 As Color
        Public ReadOnly Gray4 As Color
        Public ReadOnly Gray5 As Color
        Public ReadOnly Gray6 As Color
        Public ReadOnly Gray7 As Color
        Public ReadOnly Gray8 As Color
        Public ReadOnly White As Color
        Public ReadOnly HalfWhite As Color
        Public ReadOnly SemiWhite As Color
        Public ReadOnly Transparent As Color
        Public ReadOnly Memory As Color
        Public ReadOnly Tooltip As Color
        Public ReadOnly BackgroundTransparentSidebar As Color

        Public ReadOnly Gray1Brush As SolidColorBrush
        Public ReadOnly Gray2Brush As SolidColorBrush
        Public ReadOnly Gray3Brush As SolidColorBrush
        Public ReadOnly Gray4Brush As SolidColorBrush
        Public ReadOnly Gray5Brush As SolidColorBrush
        Public ReadOnly Gray6Brush As SolidColorBrush
        Public ReadOnly Gray7Brush As SolidColorBrush
        Public ReadOnly Gray8Brush As SolidColorBrush
        Public ReadOnly WhiteBrush As SolidColorBrush
        Public ReadOnly HalfWhiteBrush As SolidColorBrush
        Public ReadOnly SemiWhiteBrush As SolidColorBrush
        Public ReadOnly TransparentBrush As SolidColorBrush
        Public ReadOnly MemoryBrush As SolidColorBrush
        Public ReadOnly TooltipBrush As SolidColorBrush
        Public ReadOnly BackgroundTransparentSidebarBrush As SolidColorBrush

        Public Sub New(style As GrayProfile)
            Gray1 = NewColor.FromHSL2(0, 0, style.L1)
            Gray2 = NewColor.FromHSL2(0, 0, style.L2)
            Gray3 = NewColor.FromHSL2(0, 0, style.L3)
            Gray4 = NewColor.FromHSL2(0, 0, style.L4)
            Gray5 = NewColor.FromHSL2(0, 0, style.L5)
            Gray6 = NewColor.FromHSL2(0, 0, style.L6)
            Gray7 = NewColor.FromHSL2(0, 0, style.L7)
            Gray8 = NewColor.FromHSL2(0, 0, style.L8)
            White = NewColor.FromHSL2(0, 0, style.G2)
            HalfWhite = NewColor.FromHSL2(0, 0, style.G2).Alpha(style.Ahw)
            SemiWhite = NewColor.FromHSL2(0, 0, style.G2).Alpha(style.Asw)
            Transparent = NewColor.FromHSL2(0, 0, style.L8).Alpha(style.At)
            Memory = NewColor.FromHSL2(0, 0, style.G3)
            Tooltip = NewColor.FromHSL2(0, 0, style.G2).Alpha(style.Atb)
            BackgroundTransparentSidebar = NewColor.FromHSL2(0, 0, style.G1).Alpha(style.Asb)

            Gray1Brush = New SolidColorBrush(Gray1)
            Gray2Brush = New SolidColorBrush(Gray2)
            Gray3Brush = New SolidColorBrush(Gray3)
            Gray4Brush = New SolidColorBrush(Gray4)
            Gray5Brush = New SolidColorBrush(Gray5)
            Gray6Brush = New SolidColorBrush(Gray6)
            Gray7Brush = New SolidColorBrush(Gray7)
            Gray8Brush = New SolidColorBrush(Gray8)
            WhiteBrush = New SolidColorBrush(White)
            HalfWhiteBrush = New SolidColorBrush(HalfWhite)
            SemiWhiteBrush = New SolidColorBrush(SemiWhite)
            TransparentBrush = New SolidColorBrush(Transparent)
            MemoryBrush = New SolidColorBrush(Memory)
            TooltipBrush = New SolidColorBrush(Tooltip)
            BackgroundTransparentSidebarBrush = New SolidColorBrush(BackgroundTransparentSidebar)
        End Sub
    End Class

    '基于对数分布的亮度调整（看起来很高级，实际上对比线性分布性能稀烂）
    Private Const HighestLight = 95
    Private Const LowestLight = 10
    Private Const LogLightBase = 1 - LowestLight
    Private ReadOnly LogLightBaseRate = Math.Log(HighestLight + 1)
    Public Function AdjustLight(origin As Integer, adjust As Integer, Optional style As GrayProfile = Nothing) As Integer
        If origin < 0 Then Return 0 '保证不炸定义域（虽然不会有人传个负的亮度过来吧，应该...不会吧）
        If adjust = 0 Then Return origin '节省性能
        If origin > HighestLight Or origin < LowestLight Then Return origin '亮度阈值
        If style Is Nothing Then style = CurrentProfile()
        adjust *= If(adjust > 0, style.LaP, style.LaN) '根据当前 style 调整 adjust 值
        '对数分布 -> 线性分布
        Dim originF = Math.Log(origin + LogLightBase) / LogLightBaseRate '源 [0,1]
        Dim adjustF = adjust / 20.0 '参数 [-1,1]
        Dim resultF = originF + adjustF * If(adjustF > 0, 1 - originF, originF) '线性插值
        '线性分布 -> 对数分布
        Dim result As Integer = Math.Exp(resultF * LogLightBaseRate) - LogLightBase
        Return result
    End Function

    Public Class ThemeStyleDynamicColors
        Public ReadOnly Color1 As Color
        Public ReadOnly Color2 As Color
        Public ReadOnly Color3 As Color
        Public ReadOnly Color4 As Color
        Public ReadOnly Color5 As Color
        Public ReadOnly Color6 As Color
        Public ReadOnly Color7 As Color
        Public ReadOnly Color8 As Color
        Public ReadOnly ColorBg0 As Color
        Public ReadOnly ColorBg1 As Color
        Public ReadOnly SemiTransparent As Color

        Public ReadOnly Color1Brush As SolidColorBrush
        Public ReadOnly Color2Brush As SolidColorBrush
        Public ReadOnly Color3Brush As SolidColorBrush
        Public ReadOnly Color4Brush As SolidColorBrush
        Public ReadOnly Color5Brush As SolidColorBrush
        Public ReadOnly Color6Brush As SolidColorBrush
        Public ReadOnly Color7Brush As SolidColorBrush
        Public ReadOnly Color8Brush As SolidColorBrush
        Public ReadOnly ColorBg0Brush As SolidColorBrush
        Public ReadOnly ColorBg1Brush As SolidColorBrush
        Public ReadOnly SemiTransparentBrush As SolidColorBrush

        Public Sub New(style As GrayProfile, hue As Integer, sat As Integer, lightAdjust As Integer)
            Dim sat0 = sat * style.Sa0
            Dim sat1 = sat * style.Sa1

            Color1 = NewColor.FromHSL2(hue, sat0 * 0.2, style.L1)
            Color2 = NewColor.FromHSL2(hue, sat0, AdjustLight(style.L2, lightAdjust, style))
            Color3 = NewColor.FromHSL2(hue, sat0, AdjustLight(style.L3, lightAdjust, style))
            Color4 = NewColor.FromHSL2(hue, sat0, AdjustLight(style.L4, lightAdjust, style))
            Color5 = NewColor.FromHSL2(hue, sat1, AdjustLight(style.L5, lightAdjust, style))
            Color6 = NewColor.FromHSL2(hue, sat1, AdjustLight(style.L6, lightAdjust, style))
            Color7 = NewColor.FromHSL2(hue, sat1, AdjustLight(style.L7, lightAdjust, style))
            Color8 = NewColor.FromHSL2(hue, sat1, AdjustLight(style.L8, lightAdjust, style))
            ColorBg0 = NewColor.FromHSL2(hue, sat, AdjustLight(style.Lb0, lightAdjust, style))
            ColorBg1 = NewColor.FromHSL2(hue, sat, AdjustLight(style.Lb1, lightAdjust, style)).Alpha(style.Ab)
            SemiTransparent = NewColor.FromHSL2(hue, sat, AdjustLight(style.L8, lightAdjust, style)).Alpha(style.Ast)

            Color1Brush = New SolidColorBrush(Color1)
            Color2Brush = New SolidColorBrush(Color2)
            Color3Brush = New SolidColorBrush(Color3)
            Color4Brush = New SolidColorBrush(Color4)
            Color5Brush = New SolidColorBrush(Color5)
            Color6Brush = New SolidColorBrush(Color6)
            Color7Brush = New SolidColorBrush(Color7)
            Color8Brush = New SolidColorBrush(Color8)
            ColorBg0Brush = New SolidColorBrush(ColorBg0)
            ColorBg1Brush = New SolidColorBrush(ColorBg1)
            SemiTransparentBrush = New SolidColorBrush(SemiTransparent)
        End Sub
    End Class

    Public Property GrayProfile As GrayProfileConfig = Nothing

    Public Property LightStaticColors As ThemeStyleStaticColors = Nothing

    Public Property DarkStaticColors As ThemeStyleStaticColors = Nothing

    Public Function CurrentProfile() As GrayProfile
        Return GrayProfile.CurrentProfile(IsDarkMode)
    End Function

    Public Property StaticColors As ThemeStyleStaticColors = Nothing

    Public Property DynamicColors As ThemeStyleDynamicColors = Nothing

    Public ThemeNow As Integer = -1
    'Public ColorHue As Integer = If(IsDarkMode, 200, 210), ColorSat As Integer = If(IsDarkMode, 100, 85), ColorLightAdjust As Integer = If(IsDarkMode, 15, 0), ColorHueTopbarDelta As Object = 0
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As Object = 0
    Public ThemeDontClick As Integer = 0

    '深色模式事件

    ' 定义自定义事件
    Public Event ThemeChanged As EventHandler(Of Boolean)

    ' 触发事件的函数
    Public Sub RaiseThemeChanged(isDarkMode As Boolean)
        RaiseEvent ThemeChanged("", isDarkMode)
    End Sub

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        ThemeRefreshColor()
        RaiseThemeChanged(IsDarkMode)
        ThemeRefreshMain()
    End Sub

    Public Function GetDarkThemeLight(OriginalLight As Double) As Double
        If IsDarkMode Then
            Return OriginalLight * 0.1
        Else
            Return OriginalLight
        End If
    End Function

    Private ReadOnly HueList As Integer() = {200, 210, 225}
    Private ReadOnly SatList As Integer() = {100, 85, 70}
    Private ReadOnly LightList As Integer() = {7, 0, -2}

    Public Sub ThemeRefreshColor()
#If DEBUG Then
        If EnableCustomTheme Then
            If CustomThemeHue IsNot Nothing Then ColorHue = CustomThemeHue
            If CustomThemeSat IsNot Nothing Then ColorSat = CustomThemeSat
            If CustomThemeLight IsNot Nothing Then ColorLightAdjust = CustomThemeLight
            If CustomThemeHueDelta IsNot Nothing Then ColorHueTopbarDelta = CustomThemeHueDelta
        Else
#End If
            Dim colorIndex As Integer = If(IsDarkMode, Setup.Get("UiDarkColor"), Setup.Get("UiLightColor"))
            ColorHue = HueList(colorIndex)
            ColorSat = SatList(colorIndex)
            ColorLightAdjust = LightList(colorIndex)
            ColorHueTopbarDelta = 0
#If DEBUG Then
        End If
#End If

        If GrayProfile Is Nothing Then
            Dim result As AnyType = FileService.WaitForResult(PredefinedFileItems.GrayProfile)
            If result Is Nothing Then
                Log("[Theme] 无法获取灰阶配置", LogLevel.Debug)
            ElseIf result.Type.IsSubclassOf(GetType(Exception)) Then
                Log(result.Value(Of Exception), "[Theme] 获取灰阶配置时出错", LogLevel.Hint)
            Else
                GrayProfile = result.Value(Of GrayProfileConfig)
            End If
            If GrayProfile Is Nothing OrElse Not GrayProfile.IsEnabled Then
                Log("[Theme] 正在使用默认灰阶配置")
                GrayProfile = New GrayProfileConfig With {.IsEnabled = True}
            Else
                Log("[Theme] 生效灰阶配置: " & PredefinedFileItems.GrayProfile.TargetPath)
            End If
            LightStaticColors = New ThemeStyleStaticColors(GrayProfile.Light)
            DarkStaticColors = New ThemeStyleStaticColors(GrayProfile.Dark)
        End If

        Dim res = Application.Current.Resources
        StaticColors = If(IsDarkMode, DarkStaticColors, LightStaticColors)
        DynamicColors = New ThemeStyleDynamicColors(CurrentProfile, ColorHue, ColorSat, ColorLightAdjust)

        res("ColorObjectGray1") = StaticColors.Gray1
        res("ColorObjectGray2") = StaticColors.Gray2
        res("ColorObjectGray3") = StaticColors.Gray3
        res("ColorObjectGray4") = StaticColors.Gray4
        res("ColorObjectGray5") = StaticColors.Gray5
        res("ColorObjectGray6") = StaticColors.Gray6
        res("ColorObjectGray7") = StaticColors.Gray7
        res("ColorObjectGray8") = StaticColors.Gray8

        res("ColorBrushGray1") = StaticColors.Gray1Brush
        res("ColorBrushGray2") = StaticColors.Gray2Brush
        res("ColorBrushGray3") = StaticColors.Gray3Brush
        res("ColorBrushGray4") = StaticColors.Gray4Brush
        res("ColorBrushGray5") = StaticColors.Gray5Brush
        res("ColorBrushGray6") = StaticColors.Gray6Brush
        res("ColorBrushGray7") = StaticColors.Gray7Brush
        res("ColorBrushGray8") = StaticColors.Gray8Brush

        res("ColorObject1") = DynamicColors.Color1
        res("ColorObject2") = DynamicColors.Color2
        res("ColorObject3") = DynamicColors.Color3
        res("ColorObject4") = DynamicColors.Color4
        res("ColorObject5") = DynamicColors.Color5
        res("ColorObject6") = DynamicColors.Color6
        res("ColorObject7") = DynamicColors.Color7
        res("ColorObject8") = DynamicColors.Color8
        res("ColorObjectBg0") = DynamicColors.ColorBg0
        res("ColorObjectBg1") = DynamicColors.ColorBg1

        res("ColorBrush1") = DynamicColors.Color1Brush
        res("ColorBrush2") = DynamicColors.Color2Brush
        res("ColorBrush3") = DynamicColors.Color3Brush
        res("ColorBrush4") = DynamicColors.Color4Brush
        res("ColorBrush5") = DynamicColors.Color5Brush
        res("ColorBrush6") = DynamicColors.Color6Brush
        res("ColorBrush7") = DynamicColors.Color7Brush
        res("ColorBrush8") = DynamicColors.Color8Brush
        res("ColorBrushBg0") = DynamicColors.ColorBg0Brush
        res("ColorBrushBg1") = DynamicColors.ColorBg1Brush

        res("ColorBrushWhite") = StaticColors.WhiteBrush
        res("ColorBrushHalfWhite") = StaticColors.HalfWhiteBrush
        res("ColorBrushSemiWhite") = StaticColors.SemiWhiteBrush
        res("ColorBrushBackgroundTransparentSidebar") = StaticColors.BackgroundTransparentSidebarBrush
        res("ColorBrushTransparent") = StaticColors.TransparentBrush
        res("ColorBrushSemiTransparent") = DynamicColors.SemiTransparentBrush
        res("ColorBrushToolTip") = StaticColors.TooltipBrush
        res("ColorBrushMemory") = StaticColors.MemoryBrush
        res("ColorBrushMsgBox") = StaticColors.WhiteBrush
        res("ColorBrushMsgBoxText") = res("ColorBrush1")
    End Sub

    Public Sub ThemeRefreshMain()
#If DEBUG Then
        If EnableCustomTheme Then ThemeNow = 14
#End If
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim lightAdjust = ColorLightAdjust * 1.2
            If ThemeNow = 5 Then
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 25)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 15)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 25)})
                FrmMain.PanTitle.Background = Brush
                FrmMain.PanTitle.Background.Freeze()
            ElseIf Not (ThemeNow = 12 OrElse ThemeDontClick = 2) Then
                If TypeOf ColorHueTopbarDelta Is Integer Then
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue - ColorHueTopbarDelta, ColorSat, AdjustLight(48, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, AdjustLight(54, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta, ColorSat, AdjustLight(48, lightAdjust))})
                Else
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta(0), ColorSat, AdjustLight(48, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta(1), ColorSat, AdjustLight(54, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta(2), ColorSat, AdjustLight(48, lightAdjust))})
                End If
                FrmMain.PanTitle.Background = Brush
                FrmMain.PanTitle.Background.Freeze()
            Else
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue - 21, ColorSat, AdjustLight(53, lightAdjust))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.33, .Color = New MyColor().FromHSL2(ColorHue - 7, ColorSat, AdjustLight(47, lightAdjust))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.67, .Color = New MyColor().FromHSL2(ColorHue + 7, ColorSat, AdjustLight(47, lightAdjust))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + 21, ColorSat, AdjustLight(53, lightAdjust))})
                FrmMain.PanTitle.Background = Brush
            End If
            '主页面背景
            If Setup.Get("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Dim hue1, hue2 As Integer
                If ThemeNow = 14 AndAlso TypeOf ColorHueTopbarDelta Is Integer Then
                    hue1 = ColorHue + ColorHueTopbarDelta
                    hue2 = ColorHue - ColorHueTopbarDelta
                Else
                    hue1 = ColorHue - 15
                    hue2 = ColorHue + 15
                End If
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(hue1, ColorSat * 0.8, GetDarkThemeLight(80))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, GetDarkThemeLight(90))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(hue2, ColorSat * 0.8, GetDarkThemeLight(80))})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor(If(IsDarkMode, 20, 245), If(IsDarkMode, 20, 245), If(IsDarkMode, 20, 245))
            End If
            FrmMain.PanForm.Background.Freeze()

            ' 通用ContextMenu主题刷新
            RefreshAllContextMenuThemes()
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return False
    End Function
    Friend Function ThemeCheckGold(Optional Code As String = Nothing) As Boolean
        Return False
    End Function
    Friend Function DonateCodeInput() As Boolean?
        Return Nothing
    End Function

#End Region

#Region "更新"

    Public IsCheckingUpdates As Boolean = False
    Public IsUpdateWaitingRestart As Boolean = False
    Public RemoteServer As New UpdatesWrapperModel({
        New UpdatesMirrorChyanModel(),
        New UpdatesRandomModel({
                New UpdatesMinioModel("https://s3.pysio.online/pcl2-ce/", "Pysio"),
                New UpdatesMinioModel("https://staticassets.naids.com/resources/pclce/", "Naids")
            }),
        New UpdatesMinioModel("https://github.com/PCL-Community/PCL2_CE_Server/raw/main/", "GitHub")
    })
    Public ReadOnly Property IsUpdBetaChannel
        Get
            If VersionBaseName.Contains("beta") Then Return True
            Return Setup.Get("SystemSystemUpdateBranch") = 1
        End Get
    End Property

    Public Sub UpdateCheckByButton()
        If IsCheckingUpdates Then
            Hint("正在检查更新中，请稍后再试……")
            Exit Sub
        End If
        Hint("正在获取更新信息...")
        RunInNewThread(Sub()
                           Try
                               NoticeUserUpdate()
                           Catch ex As Exception
                               Log(ex, "[Update] 获取启动器更新信息失败", LogLevel.Hint)
                               Hint("获取启动器更新信息失败，请检查网络连接", HintType.Critical)
                           End Try
                       End Sub)
    End Sub
    Public Function IsVerisonLatest() As Boolean
        Try
            Return RemoteServer.IsLatest(
            If(IsUpdBetaChannel, UpdateChannel.beta, UpdateChannel.stable),
            If(IsArm64System, UpdateArch.arm64, UpdateArch.x64),
            SemVer.Parse(VersionBaseName),
            VersionCode)
        Catch ex As Exception
            Log(ex, "无法获取最新版本信息，请检查网络连接", LogLevel.Hint)
            Return False
        End Try
    End Function
    Public Sub NoticeUserUpdate(Optional Silent As Boolean = False)
        If Not IsVerisonLatest() Then
            Dim latest As VersionDataModel = Nothing
            Dim checkUpdateEx As Exception = Nothing
            RunInNewThread(
                Sub()
                    Try
                        latest = RemoteServer.GetLatestVersion(
                            If(IsUpdBetaChannel, UpdateChannel.beta, UpdateChannel.stable),
                            If(IsArm64System, UpdateArch.arm64, UpdateArch.x64))
                    Catch ex As Exception
                        checkUpdateEx = ex
                    End Try
                End Sub
            ).Join()
            If latest Is Nothing Then
                Log(checkUpdateEx, "[Update] 检查更新失败", LogLevel.Msgbox)
                Exit Sub
            End If
            If Not latest.VersionName.StartsWithF("2.12.") AndAlso Not ShellAndGetOutput("cmd", "/c dotnet --list-runtimes").ContainsF("Microsoft.WindowsDesktop.App 8.0.", True) Then
                MyMsgBox($"发现了启动器更新（版本 2.13.0），但是新版本要求你的电脑安装 .NET 8 才可以运行。{vbCrLf}你需要先安装 .NET 8 才可以继续更新。{vbCrLf}{vbCrLf}点击下方按钮打开网页，然后选择 ⌈.NET 桌面运行时⌋ 中的 {If(IsArm64System, "Arm64", "x64")} 选项下载。", "启动器更新 - 缺少运行环境",
                         "下载 .NET 8 运行时", "取消", Button1Action:=Sub() OpenWebsite($"https://get.dot.net/8"), ForceWait:=True)
            ElseIf Not Val(Environment.OSVersion.Version.ToString().Split(".")(2)) >= 19042 AndAlso Not latest.VersionName.StartsWithF("2.9.") Then
                If MyMsgBox($"发现了启动器更新（版本 {latest.VersionName}），但是由于你的 Windows 版本过低，不满足新版本要求。{vbCrLf}你需要更新到 Windows 10 20H2 或更高版本才可以继续更新。", "启动器更新 - 系统版本过低", "升级 Windows 10", "取消", IsWarn:=True, ForceWait:=True) = 1 Then OpenWebsite("https://www.microsoft.com/zh-cn/software-download/windows10")
                Exit Sub
            End If
            If MyMsgBoxMarkdown($"启动器有新版本可用（｛VersionBaseName｝ -> {latest.VersionName}){vbCrLf}是否立即更新？{vbCrLf}{vbCrLf}{latest.Changelog}", "启动器更新", "更新", "取消") = 1 Then
                UpdateStart(False)
            End If
        Else
            If Not Silent Then Hint("启动器已是最新版 " + VersionBaseName + "，无须更新啦！", HintType.Finish)
        End If
    End Sub

    Public Sub UpdateStart(Slient As Boolean, Optional ReceivedKey As String = Nothing, Optional ForceValidated As Boolean = False)
        Dim DlTargetPath As String = Path + "PCL\Plain Craft Launcher Community Edition.exe"
        RunInNewThread(Sub()
                           Try
                               Dim version = RemoteServer.GetLatestVersion(
                               If(IsUpdBetaChannel, UpdateChannel.beta, UpdateChannel.stable),
                               If(IsArm64System, UpdateArch.arm64, UpdateArch.x64))
                               WriteFile($"{PathTemp}CEUpdateLog.md", version.Changelog)
                               '构造步骤加载器
                               Dim Loaders As New List(Of LoaderBase)
                               '下载
                               Loaders.AddRange(RemoteServer.GetDownloadLoader(
                                                If(IsUpdBetaChannel, UpdateChannel.beta, UpdateChannel.stable),
                                                If(IsArm64System, UpdateArch.arm64, UpdateArch.x64), DlTargetPath))
                               Loaders.Add(New LoaderTask(Of Integer, Integer)("校验更新", Sub()
                                                                                           Dim curHash = GetFileSHA256(DlTargetPath)
                                                                                           If curHash <> version.SHA256 Then
                                                                                               Throw New Exception($"更新文件 SHA256 不正确，应该为 {version.SHA256}，实际为 {curHash}")
                                                                                           End If
                                                                                       End Sub))
                               If Not Slient Then
                                   Loaders.Add(New LoaderTask(Of Integer, Integer)("安装更新", Sub() UpdateRestart(True)))
                               End If
                               '启动
                               Dim Loader As New LoaderCombo(Of JObject)("启动器更新", Loaders)
                               Loader.Start()
                               If Slient Then
                                   IsUpdateWaitingRestart = True
                               Else
                                   LoaderTaskbarAdd(Loader)
                                   FrmMain.BtnExtraDownload.ShowRefresh()
                                   FrmMain.BtnExtraDownload.Ribble()
                               End If
                           Catch ex As Exception
                               Log(ex, "[Update] 下载启动器更新文件失败", LogLevel.Hint)
                               Hint("下载启动器更新文件失败，请检查网络连接", HintType.Critical)
                           End Try
                       End Sub)
    End Sub
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
        Try
            Dim fileName As String = Path + "PCL\Plain Craft Launcher Community Edition.exe"
            If Not File.Exists(fileName) Then
                Log("[System] 更新失败：未找到更新文件")
                Exit Sub
            End If
            ' id old new restart
            Dim text As String = $"update {Process.GetCurrentProcess().Id} ""{PathWithName}"" ""{fileName}"" true"
            Log("[System] 更新程序启动，参数：" + text, LogLevel.Normal, "出现错误")
            Process.Start(New ProcessStartInfo(fileName) With {.WindowStyle = ProcessWindowStyle.Hidden, .CreateNoWindow = True, .Arguments = text})
            If TriggerRestartAndByEnd Then
                FrmMain.EndProgram(False)
                Log("[System] 已由于更新强制结束程序", LogLevel.Normal, "出现错误")
            End If
        Catch ex As Win32Exception
            Log(ex, "自动更新时触发 Win32 错误，疑似被拦截", LogLevel.Debug, "出现错误")
            If MyMsgBox(String.Format("由于被 Windows 安全中心拦截，或者存在权限问题，导致 PCL 无法更新。{0}请将 PCL 所在文件夹加入白名单，或者手动用 {1}PCL\Plain Craft Launcher Community Edition.exe 替换当前文件！", vbCrLf, ModBase.Path), "更新失败", "查看帮助", "确定", "", True, True, False, Nothing, Nothing, Nothing) = 1 Then
                TryStartEvent("打开帮助", "启动器/Microsoft Defender 添加排除项.json")
            End If
        End Try
    End Sub
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
        Try
            Dim ps = Process.GetProcessById(ProcessId)
            If Not ps.HasExited Then
                ps.Kill()
            End If
        Catch ex As Exception
        End Try
        Dim ex2 As Exception = Nothing
        Dim num As Integer = 0
        Do
            Try
                If File.Exists(OldFileName) Then
                    File.Delete(OldFileName)
                End If
                If Not File.Exists(OldFileName) Then
                    Exit Try
                End If
            Catch ex3 As Exception
                ex2 = ex3
            Finally
                Thread.Sleep(500)
            End Try
            num += 1
        Loop While num <= 4
        If (Not File.Exists(OldFileName)) AndAlso File.Exists(NewFileName) Then
            Try
                CopyFile(NewFileName, OldFileName)
            Catch ex4 As UnauthorizedAccessException
                MsgBox("PCL 更新失败：权限不足。请手动复制 PCL 文件夹下的新版本程序。" & vbCrLf & "若 PCL 位于桌面或 C 盘，你可以尝试将其挪到其他文件夹，这可能可以解决权限问题。" & vbCrLf + GetExceptionSummary(ex4), MsgBoxStyle.Critical, "更新失败")
            Catch ex5 As Exception
                MsgBox("PCL 更新失败：无法复制新文件。请手动复制 PCL 文件夹下的新版本程序。" & vbCrLf + GetExceptionSummary(ex5), MsgBoxStyle.Critical, "更新失败")
                Return
            End Try
            If TriggerRestart Then
                Try
                    Process.Start(OldFileName)
                Catch ex6 As Exception
                    MsgBox("PCL 更新失败：无法重新启动。" & vbCrLf + GetExceptionSummary(ex6), MsgBoxStyle.Critical, "更新失败")
                End Try
            End If
            Return
        End If
        If TypeOf ex2 Is UnauthorizedAccessException Then
            MsgBox(String.Concat(New String() {"由于权限不足，PCL 无法完成更新。请尝试：" & vbCrLf,
                                 If((Path.StartsWithF(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), False) OrElse Path.StartsWithF(Environment.GetFolderPath(Environment.SpecialFolder.Personal), False)),
                                 " - 将 PCL 文件移动到桌面、文档以外的文件夹（这或许可以一劳永逸地解决权限问题）" & vbCrLf, ""),
                                 If(Path.StartsWithF("C", True),
                                 " - 将 PCL 文件移动到 C 盘以外的文件夹（这或许可以一劳永逸地解决权限问题）" & vbCrLf, ""),
                                 " - 右键以管理员身份运行 PCL" & vbCrLf & " - 手动复制已下载到 PCL 文件夹下的新版本程序，覆盖原程序" & vbCrLf & vbCrLf,
                                 GetExceptionSummary(ex2)}), MsgBoxStyle.Critical, "更新失败")
            Return
        End If
        MsgBox("PCL 更新失败：无法删除原文件。请手动复制已下载到 PCL 文件夹下的新版本程序覆盖原程序。" & vbCrLf + GetExceptionSummary(ex2), MsgBoxStyle.Critical, "更新失败")
    End Sub
    ''' <summary>
    ''' 确保 PathTemp 下的 Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
        Dim LatestPCLPath As String = PathTemp & "CE-Latest.exe"
        Dim target = RemoteServer.GetLatestVersion(UpdateChannel.stable, If(IsArm64System, UpdateArch.arm64, UpdateArch.x64))
        If target Is Nothing Then Throw New Exception("无法获取更新")
        If File.Exists(LatestPCLPath) AndAlso GetFileSHA256(LatestPCLPath) = target.SHA256 Then
            Log("[System] 最新版 PCL 已存在，跳过下载")
            Exit Sub
        End If
        If GetFileSHA256(PathWithName) = target.SHA256 Then '正在使用的版本符合要求，直接拿来用
            CopyFile(PathWithName, LatestPCLPath)
            Exit Sub
        End If

        Dim loaders = RemoteServer.GetDownloadLoader(UpdateChannel.stable, If(IsArm64System, UpdateArch.arm64, UpdateArch.x64), LatestPCLPath)
        Dim loader As New LoaderCombo(Of Integer)("下载最新稳定版", loaders)
        loader.Start()
        loader.WaitForExit()
    End Sub

#End Region

#Region "联网通知"

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 服务", AddressOf LoadOnlineInfo, Priority:=ThreadPriority.BelowNormal)

    Private Sub LoadOnlineInfo()
        Dim UpdateDesire = Setup.Get("SystemSystemUpdate")
        Dim AnnouncementDesire = Setup.Get("SystemSystemActivity")
        Select Case UpdateDesire
            Case 0
                If Not IsVerisonLatest() Then
                    UpdateStart(True) '静默更新
                End If
            Case 1
                NoticeUserUpdate(True)
            Case 2, 3
                Exit Sub
        End Select
        If AnnouncementDesire <= 1 Then
            Dim ShowedAnnounced = Setup.Get("SystemSystemAnnouncement").ToString().Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList()
            Dim ShowAnnounce = RemoteServer.GetAnnouncementList().content.Where(Function(x) Not ShowedAnnounced.Contains(x.id)).ToList()
            Log("[System] 需要展示的公告数量：" + ShowAnnounce.Count.ToString())
            RunInNewThread(Sub()
                               For Each item In ShowAnnounce
                                   Dim SelectedBtn = MyMsgBox(
                                   item.detail,
                                   item.title,
                                   If(item.btn1 Is Nothing, "", item.btn1.text),
                                   If(item.btn2 Is Nothing, "", item.btn2.text),
                                   "关闭",
                                   Button1Action:=Sub()
                                                      TryStartEvent(item.btn1.command, item.btn1.command_paramter)
                                                  End Sub,
                                   Button2Action:=Sub()
                                                      TryStartEvent(item.btn2.command, item.btn2.command_paramter)
                                                  End Sub
                                    )
                               Next
                           End Sub)
            ShowedAnnounced.AddRange(ShowAnnounce.Select(Function(x) x.id).ToList())
            ShowedAnnounced = ShowedAnnounced.Distinct().ToList()
            Setup.Set("SystemSystemAnnouncement", ShowedAnnounced.Join("|"))
        End If
    End Sub

#End Region

#Region "遥测"
    ''' <summary>
    ''' 发送遥测数据，需要在非 UI 线程运行
    ''' </summary>
    Public Sub SendTelemetry()
        If String.IsNullOrWhiteSpace(TelemetryKey) Then Exit Sub
        Dim NetResult = ModLink.NetTest()
        Dim Data = New JObject From {
            {"Tag", "Telemetry"},
            {"Id", UniqueAddress},
            {"OS", Environment.OSVersion.Version.Build},
            {"Is64Bit", Not Is32BitSystem},
            {"IsARM64", IsArm64System},
            {"Launcher", VersionCode},
            {"LauncherBranch", If(IsUpdBetaChannel, "Fast Ring", "Slow Ring")},
            {"UsedOfficialPCL", ReadReg("SystemEula", Nothing, "PCL") IsNot Nothing},
            {"UsedHMCL", Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.hmcl")},
            {"UsedBakaXL", Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\BakaXL")},
            {"Memory", SystemMemorySize},
            {"NatType", NetResult(0)},
            {"IPv6Status", NetResult(1)}
        }
        Dim SendData = New JObject From {{"data", Data}}
        Try
            Dim Result As String = NetRequestRetry("https://pcl2ce.pysio.online/post", "POST", SendData.ToString(), "application/json")
            If Result.Contains("数据已成功保存") Then
                Log("[Telemetry] 软硬件调查数据已发送")
            Else
                Log("[Telemetry] 软硬件调查数据发送失败，原始返回内容: " + Result)
            End If
        Catch ex As Exception
            Log(ex, "[Telemetry] 软硬件调查数据发送失败", LogLevel.Normal)
        End Try
    End Sub
#End Region

#Region "系统信息"
    Friend CPUName As String = Nothing
    ''' <summary>
    ''' 系统 GPU 信息
    ''' </summary>
    Friend GPUs As New List(Of GPUInfo)
    ''' <summary>
    ''' 已安装物理内存大小，单位 MB
    ''' </summary>
    Friend SystemMemorySize As Long = KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024
    ''' <summary>
    ''' 系统信息描述，例如 Microsoft Windows 11 专业工作站版 10.0.22635.0
    ''' </summary>
    Public OSInfo As String = RuntimeInformation.OSDescription & " " & Environment.OSVersion.Version.ToString()
    Class GPUInfo
        Friend Name As String
        ''' <summary>
        ''' 显存大小，单位 MB
        ''' </summary>
        Friend Memory As Long
        Friend DriverVersion As String
    End Class
    ''' <summary>
    ''' 获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    ''' </summary>
    Friend Sub GetSystemInfo()
        'CPU
        Try
            Dim searcher As New ManagementObjectSearcher("root\CIMV2", "SELECT * FROM Win32_Processor")

            For Each queryObj As ManagementObject In searcher.Get()
                CPUName = queryObj("Name").ToString().Trim()
                Exit For '通常只需要第一个CPU的信息
            Next
        Catch ex As Exception
            Log(ex, "获取 CPU 信息时出错", LogLevel.Normal)
        End Try

        'GPU
        Try
            Dim searcher As New ManagementObjectSearcher("root\CIMV2", "SELECT * FROM Win32_VideoController")

            For Each queryObj As ManagementObject In searcher.Get()
                Dim gpuInfo As New GPUInfo

                If queryObj("Name") IsNot Nothing Then
                    gpuInfo.Name = queryObj("Name")
                End If
                If queryObj("AdapterRAM") IsNot Nothing Then
                    Dim ramMB As Long = CLng(queryObj("AdapterRAM")) \ (1024 * 1024)
                    gpuInfo.Memory = ramMB
                End If
                If queryObj("DriverVersion") IsNot Nothing Then
                    gpuInfo.DriverVersion = queryObj("DriverVersion")
                End If

                GPUs.Add(gpuInfo)
            Next

            Log("已获取系统环境信息")
        Catch ex As Exception
            Log(ex, "获取 GPU 信息时出错", LogLevel.Normal)
        End Try
    End Sub
#End Region

#Region "主题"

    ''' <summary>
    ''' 通用的ContextMenu主题刷新方法
    ''' </summary>
    Private Sub RefreshAllContextMenuThemes()
        Try
            ' 注册全局的ContextMenu主题刷新事件处理器
            EventManager.RegisterClassHandler(GetType(ContextMenu), ContextMenu.OpenedEvent, New RoutedEventHandler(AddressOf OnContextMenuOpened))
            
            ' 刷新当前打开的ContextMenu
            RunInUi(Sub()
                        ' 获取当前应用程序中所有的窗口
                        For Each window As Window In Application.Current.Windows
                            RefreshContextMenusInElement(window)
                        Next
                    End Sub)
        Catch ex As Exception
            Log(ex, "刷新ContextMenu主题时出错", LogLevel.Debug)
        End Try
    End Sub

    ''' <summary>
    ''' ContextMenu打开事件处理器，确保在显示时应用正确主题
    ''' </summary>
    Private Sub OnContextMenuOpened(sender As Object, e As RoutedEventArgs)
        Try
            If TypeOf sender Is ContextMenu Then
                Dim contextMenu As ContextMenu = CType(sender, ContextMenu)
                ' 强制重新应用样式
                contextMenu.ClearValue(FrameworkElement.StyleProperty)
                contextMenu.UpdateDefaultStyle()
            End If
        Catch ex As Exception
            ' 忽略个别错误
        End Try
    End Sub

    ''' <summary>
    ''' 递归刷新元素及其子元素中的ContextMenu
    ''' </summary>
    Private Sub RefreshContextMenusInElement(element As DependencyObject)
        If element Is Nothing Then Return
        
        Try
            ' 检查当前元素是否有ContextMenu
            If TypeOf element Is FrameworkElement Then
                Dim fe As FrameworkElement = CType(element, FrameworkElement)
                If fe.ContextMenu IsNot Nothing Then
                    ' 强制重新应用样式
                    fe.ContextMenu.ClearValue(FrameworkElement.StyleProperty)
                    fe.ContextMenu.UpdateDefaultStyle()
                End If
            End If
            
            ' 递归处理子元素
            Dim childrenCount As Integer = VisualTreeHelper.GetChildrenCount(element)
            For i As Integer = 0 To childrenCount - 1
                Dim child As DependencyObject = VisualTreeHelper.GetChild(element, i)
                RefreshContextMenusInElement(child)
            Next
        Catch ex As Exception
            ' 忽略个别元素的错误，继续处理其他元素
        End Try
    End Sub

#End Region

End Module
