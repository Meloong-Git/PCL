' lwjgl3ify 原生启动支持
'
' lwjgl3ify 是一个让 Minecraft 1.7.10 使用 LWJGL3 与 Java 17+ 运行的 Mod。
' 原版 Mod 的启动方式是先用 Java 8 启动游戏，再由 Mod 内的重启器打开一个新窗口、
' 用新版 Java 重新启动游戏；这不仅会多出一个窗口，还会让 PCL 误以为游戏已经退出。
'
' PCL 的原生支持会读取 Mod 自带的启动清单（me/eigenraven/lwjgl3ify/relauncher/version.json），
' 直接以 Java 17+ 启动，并自动补全 LWJGL3 等支持库，跳过重启器窗口。

Friend Module ModLwjgl3ify

    ''' <summary>
    ''' lwjgl3ify 要求的最低 Java 版本。
    ''' </summary>
    Public ReadOnly MinJavaVersion As New Version(17, 0, 6)

    ''' <summary>
    ''' lwjgl3ify 的启动信息。
    ''' </summary>
    Public Class Lwjgl3ifyInfo
        ''' <summary>
        ''' 是否检测到可用的 lwjgl3ify。
        ''' </summary>
        Public Enabled As Boolean = False
        ''' <summary>
        ''' Mod Jar 的完整路径。
        ''' </summary>
        Public JarPath As String = Nothing
        ''' <summary>
        ''' Mod 的版本号，例如 3.0.39。
        ''' </summary>
        Public ModVersion As String = Nothing
        ''' <summary>
        ''' 启动入口，由 Mod 的启动清单提供。
        ''' </summary>
        Public MainClass As String = Nothing
        ''' <summary>
        ''' 需要添加到 classpath 的支持库（包含 LWJGL3 及其它新版依赖）。
        ''' </summary>
        Public Libraries As New List(Of McLibEntry)
        ''' <summary>
        ''' 需要附加的 JVM 参数（RFB 系统类加载器、--add-opens 等）。
        ''' </summary>
        Public JvmArgs As New List(Of String)
        ''' <summary>
        ''' 从 Mod 中释放的 forgePatches Jar 路径。
        ''' </summary>
        Public ForgePatchesPath As String = Nothing

        Public Overrides Function ToString() As String
            Return $"lwjgl3ify {If(ModVersion, "?")}（{If(JarPath, "未找到")}）"
        End Function
    End Class

    Private ReadOnly InfoCache As New Dictionary(Of String, Lwjgl3ifyInfo)
    Private ReadOnly InfoCacheLock As New Object
    Private ReadOnly OldVersionWarned As New HashSet(Of String)

    ''' <summary>
    ''' 获取指定版本的 lwjgl3ify 启动信息。若未安装则返回 Enabled = False 的空信息。
    ''' </summary>
    Public Function Lwjgl3ifyGet(Instance As McInstance) As Lwjgl3ifyInfo
        Dim EmptyInfo As New Lwjgl3ifyInfo
        If Instance Is Nothing Then Return EmptyInfo
        Try
            If Not Instance.IsLoaded Then Instance.Load()
            Dim JarPath As String = Lwjgl3ifyJarGet(Instance)
            If JarPath Is Nothing Then Return EmptyInfo
            Dim JarInfo As FileInfo = FileUtils.GetInfo(JarPath)
            Dim CacheKey As String = JarPath.ToLower() & "|" & JarInfo.Length & "|" & JarInfo.LastWriteTimeUtc.Ticks
            SyncLock InfoCacheLock
                Dim Cached As Lwjgl3ifyInfo = Nothing
                If InfoCache.TryGetValue(CacheKey, Cached) Then Return Cached
            End SyncLock
            Dim Info As Lwjgl3ifyInfo = Lwjgl3ifyParse(JarPath, JarInfo.Name)
            SyncLock InfoCacheLock
                If InfoCache.Count >= 32 Then InfoCache.Clear()
                InfoCache(CacheKey) = Info
            End SyncLock
            Return Info
        Catch ex As Exception
            Logger.Warn(ex, "获取 lwjgl3ify 信息失败")
            Return EmptyInfo
        End Try
    End Function

    ''' <summary>
    ''' 指定版本是否启用了 lwjgl3ify。
    ''' </summary>
    Public Function Lwjgl3ifyEnabled(Instance As McInstance) As Boolean
        Return Lwjgl3ifyGet(Instance).Enabled
    End Function

#Region "检测"

    ''' <summary>
    ''' 判断该版本是否为 Minecraft 1.7.10。部分整合包的版本号无法被正确识别，因此同时检查版本 Json。
    ''' </summary>
    Private Function Lwjgl3ifyIs1710(Instance As McInstance) As Boolean
        '明显是 1.8+ 的版本不处理；无法确认版本号的自定义整合包则继续检查
        If Instance.Version.Valid AndAlso Instance.Version.Vanilla.Major = 1 AndAlso Instance.Version.Vanilla.Minor >= 8 Then Return False
        If Instance.Version.Valid AndAlso Instance.Version.Vanilla = New Version(1, 7, 10) Then Return True
        Dim Json As JObject = Instance.JsonObject
        If Json IsNot Nothing Then
            For Each Key As String In {"id", "inheritsFrom", "jar"}
                Dim Value As String = Json(Key)?.ToString
                If Value IsNot Nothing AndAlso Value.Contains("1.7.10") Then Return True
            Next
        End If
        '无法识别版本号（例如整合包自定义了版本名）时仍尝试启用，lwjgl3ify 本身只支持 1.7.10
        Return True
    End Function

    ''' <summary>
    ''' 在版本的 mods 文件夹中查找 lwjgl3ify 的主 Jar。
    ''' </summary>
    Private Function Lwjgl3ifyJarGet(Instance As McInstance) As String
        'lwjgl3ify 仅支持 Minecraft 1.7.10
        If Not Lwjgl3ifyIs1710(Instance) Then Return Nothing
        Dim ModsFolder As String = Instance.PathIndie & "mods\"
        If Not DirectoryUtils.Exists(ModsFolder) Then Return Nothing
        Dim Best As FileInfo = Nothing
        For Each File As FileInfo In DirectoryUtils.GetInfo(ModsFolder).GetFiles("*.jar")
            Dim LowerName As String = File.Name.ToLower()
            If Not LowerName.Contains("lwjgl3ify") Then Continue For
            '排除同名的附属 Jar
            If LowerName.EndsWith("-api.jar") OrElse LowerName.EndsWith("-dev.jar") OrElse
               LowerName.EndsWith("-sources.jar") OrElse LowerName.EndsWith("-forgepatches.jar") Then Continue For
            If Not Lwjgl3ifyIsMainJar(File.FullName) Then
                SyncLock OldVersionWarned
                    If OldVersionWarned.Add(File.FullName) Then
                        Logger.Warn($"检测到 lwjgl3ify，但它不包含原生启动清单，可能版本过旧：{File.Name}。请更新到 3.0 或更高版本以使用原生启动。")
                    End If
                End SyncLock
                Continue For
            End If
            '优先选择文件名最短的那个，通常是主 Jar
            If Best Is Nothing OrElse File.Name.Length < Best.Name.Length Then Best = File
        Next
        Return If(Best?.FullName, Nothing)
    End Function

    ''' <summary>
    ''' 判断指定 Jar 是否为带有启动清单的 lwjgl3ify 主 Jar。
    ''' </summary>
    Private Function Lwjgl3ifyIsMainJar(JarPath As String) As Boolean
        Try
            Using Zip As ZipArchive = FileUtils.OpenZip(JarPath)
                Return Zip.GetEntry("me/eigenraven/lwjgl3ify/relauncher/version.json") IsNot Nothing
            End Using
        Catch ex As Exception
            Logger.Warn(ex, $"无法读取 Mod 文件以检查 lwjgl3ify：{JarPath}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 解析 lwjgl3ify 的启动清单。
    ''' </summary>
    Private Function Lwjgl3ifyParse(JarPath As String, FileName As String) As Lwjgl3ifyInfo
        Dim Info As New Lwjgl3ifyInfo With {.JarPath = JarPath}
        Dim VersionJson As JObject = Nothing
        Dim ForgePatchesLength As Long = -1
        Dim ModVersion As String = Nothing

        Using Zip As ZipArchive = FileUtils.OpenZip(JarPath)
            Dim JsonEntry As ZipArchiveEntry = Zip.GetEntry("me/eigenraven/lwjgl3ify/relauncher/version.json")
            If JsonEntry Is Nothing Then Return Info
            Using Reader As New StreamReader(JsonEntry.Open(), Encoding.UTF8, True)
                VersionJson = JObject.Parse(Reader.ReadToEnd())
            End Using
            Dim ForgePatchesZipEntry As ZipArchiveEntry = Zip.GetEntry("me/eigenraven/lwjgl3ify/relauncher/forgePatches.zip")
            If ForgePatchesZipEntry IsNot Nothing Then ForgePatchesLength = ForgePatchesZipEntry.Length
            '从 Manifest 或文件名中读取 Mod 版本号
            Try
                Dim ManifestEntry As ZipArchiveEntry = Zip.GetEntry("META-INF/MANIFEST.MF")
                If ManifestEntry IsNot Nothing Then
                    Using Reader As New StreamReader(ManifestEntry.Open(), Encoding.UTF8, True)
                        For Each Line As String In Reader.ReadToEnd().Replace(vbCrLf, vbLf).Split(vbLf)
                            If Line.StartsWithF("Implementation-Version:") Then
                                ModVersion = Line.AfterFirst(":").Trim()
                                Exit For
                            End If
                        Next
                    End Using
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取 lwjgl3ify 的版本号失败")
            End Try
        End Using

        If VersionJson Is Nothing Then Return Info
        Info.MainClass = VersionJson("mainClass")?.ToString
        If String.IsNullOrWhiteSpace(Info.MainClass) Then
            Logger.Warn("lwjgl3ify 的启动清单中没有 mainClass，已跳过原生启动")
            Return Info
        End If

        '支持库：直接复用 PCL 对版本 Json 的解析逻辑，再按 LWJGL3 的需要调整
        Info.Libraries = McLibListGetWithJson(VersionJson)
        For Each Library As McLibEntry In Info.Libraries
            Library.IsNatives = False 'LWJGL3 会自动从 classpath 中的 Natives Jar 解压，不需要 PCL 解压到 Natives 文件夹
        Next

        'JVM 参数：只取 RFB 所需的参数，-cp、-Djava.library.path 等由 PCL 自己处理
        Dim JvmTokens As New List(Of String)
        Dim JvmArray As JArray = TryCast(VersionJson("arguments")?("jvm"), JArray)
        If JvmArray IsNot Nothing Then
            For Each Token As JToken In JvmArray
                If Token.Type = JTokenType.String Then JvmTokens.Add(Token.ToString())
            Next
        End If
        For i = 0 To JvmTokens.Count - 1
            Dim Token As String = JvmTokens(i)
            Select Case Token
                Case "-Djava.system.class.loader=com.gtnewhorizons.retrofuturabootstrap.RfbSystemClassLoader"
                    Info.JvmArgs.Add(Token)
                Case "--enable-native-access", "--add-opens"
                    If i + 1 < JvmTokens.Count Then
                        Info.JvmArgs.Add(Token)
                        Info.JvmArgs.Add(JvmTokens(i + 1))
                        i += 1
                    End If
                Case Else
                    '忽略 -Dfile.encoding、-Xmx 等由 PCL 管理的参数
            End Select
        Next

        'forgePatches：优先从 Mod 中释放，避免从 GTNH 仓库额外下载
        Dim ForgePatches As McLibEntry = Nothing
        For Each Library As McLibEntry In Info.Libraries
            If Library.OriginalName IsNot Nothing AndAlso Library.OriginalName.Contains(":lwjgl3ify:") AndAlso
                    Library.OriginalName.EndsWith(":forgePatches") Then
                ForgePatches = Library
                Exit For
            End If
        Next
        If ForgePatches Is Nothing Then
            Logger.Warn("lwjgl3ify 的启动清单中缺少 forgePatches，原生启动可能失败")
        Else
            If ModVersion Is Nothing Then
                ModVersion = ForgePatches.OriginalName.Split(":"c)(2)
            End If
            If ForgePatchesLength >= 0 Then
                Dim Target As String = ForgePatches.LocalPath
                Dim NeedExtract As Boolean =
                    Not FileUtils.Exists(Target) OrElse FileUtils.GetInfo(Target).Length <> ForgePatchesLength
                If NeedExtract Then
                    DirectoryUtils.Create(PathUtils.RemoveLastPart(Target))
                    Lwjgl3ifyExtractForgePatches(JarPath, Target)
                    Logger.Info($"已从 lwjgl3ify 中释放 forgePatches：{Target}")
                End If
                '保留清单中的 URL 与 SHA1：若释放失败，PCL 仍会从 GTNH 仓库下载
                ForgePatches.Size = FileUtils.GetInfo(Target).Length
            End If
            'forgePatches 需要位于 classpath 的最前面
            Info.Libraries.Remove(ForgePatches)
            Info.Libraries.Insert(0, ForgePatches)
            Info.ForgePatchesPath = ForgePatches.LocalPath
        End If

        If ModVersion Is Nothing Then
            '从形如 lwjgl3ify-3.0.39.jar 的文件名中提取
            Dim NameParts As String() = FileName.BeforeLast(".").Split("-"c)
            If NameParts.Length >= 2 AndAlso NameParts.Last().Any Then ModVersion = NameParts.Last()
        End If
        Info.ModVersion = ModVersion
        Info.Enabled = True
        Logger.Info($"已启用 lwjgl3ify 原生启动：{Info}")
        Return Info
    End Function

    ''' <summary>
    ''' 从 Mod Jar 中释放其自带的 forgePatches。
    ''' </summary>
    Private Sub Lwjgl3ifyExtractForgePatches(JarPath As String, Target As String)
        Using Zip As ZipArchive = FileUtils.OpenZip(JarPath)
            Dim Entry As ZipArchiveEntry = Zip.GetEntry("me/eigenraven/lwjgl3ify/relauncher/forgePatches.zip")
            If Entry Is Nothing Then Throw New Exception($"lwjgl3ify 中缺少 forgePatches：{JarPath}")
            Using Source As Stream = Entry.Open()
                Using Destination As Stream = IO.File.Create(Target)
                    Source.CopyTo(Destination)
                End Using
            End Using
        End Using
    End Sub

#End Region

End Module
