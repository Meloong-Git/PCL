''' <summary>
''' 对 lwjgl3ify（https://github.com/GTNewHorizons/lwjgl3ify）的支持。
''' lwjgl3ify 可以让 1.7.10 的 Forge 实例在 Java 17 及以上版本中通过 LWJGL3 运行，但它要求启动器：
''' 把 LWJGL2 支持库换成 LWJGL3、追加前置 Jar（RetroFuturaBootstrap）、更换主类，并追加一批 --add-opens 之类的 JVM 参数。
''' 这些信息全部随 Mod 打包在其启动元数据中（Jar 内的 me/eigenraven/lwjgl3ify/relauncher/version.json），
''' 因此这里直接读取已安装的 Mod 自带的元数据，既能保证启动配置与 Mod 版本完全匹配，也不需要额外联网。
''' </summary>
Public Module ModLwjgl3ify

    ''' <summary>
    ''' lwjgl3ify 支持的最低 Java 大版本（3.x 支持 Java 17、19、20、21 及更高版本）。
    ''' </summary>
    Public Const RequiredJavaMajor As Integer = 17

    Private Const ModFileNameHead As String = "lwjgl3ify"
    Private Const ProfileEntryPath As String = "me/eigenraven/lwjgl3ify/relauncher/version.json"
    Private Const ForgePatchesEntryPath As String = "me/eigenraven/lwjgl3ify/relauncher/forgePatches.zip"

    ''' <summary>
    ''' 会被 lwjgl3ify 启动配置整体替换掉的支持库组。
    ''' 这些支持库由启动元数据重新提供（例如 LWJGL2 会被 LWJGL3 替换，launchwrapper 与 ASM 会被 RFB 替换），
    ''' 若继续保留版本中原有的同组支持库，就会与替换后的版本冲突。
    ''' </summary>
    Private ReadOnly ReplacedLibraryGroups As String() = {
        "net.minecraft", "net.minecraftforge", "com.mojang", "org.lwjgl", "net.java", "tv.twitch", "com.paulscode",
        "java3d", "lzma", "net.sf", "com.google", "commons-", "org.apache", "io.netty", "com.typesafe",
        "org.scala-lang", "com.ibm.icu", "org.ow2", "org.yaml", "org.jetbrains"}

    ''' <summary>
    ''' lwjgl3ify 的启动信息。
    ''' </summary>
    Public Class Lwjgl3ifyInfo
        ''' <summary>
        ''' 该信息对应的版本名称。
        ''' </summary>
        Public Property InstanceName As String
        ''' <summary>
        ''' lwjgl3ify 的 Mod 文件路径。
        ''' </summary>
        Public Property JarPath As String
        ''' <summary>
        ''' lwjgl3ify 的版本号，如 3.1.1。
        ''' </summary>
        Public Property Version As String
        ''' <summary>
        ''' Mod 自带的启动元数据。
        ''' </summary>
        Public Property Profile As JObject
        ''' <summary>
        ''' 需要在启动时追加的 JVM 参数。
        ''' </summary>
        Public Property JvmArguments As List(Of String)
        ''' <summary>
        ''' 启动元数据预期的 Forge 版本。
        ''' </summary>
        Public Property ExpectedForgeVersion As String
    End Class

    ''' <summary>
    ''' 当前启动中已应用的 lwjgl3ify 信息。Nothing 代表未应用。
    ''' </summary>
    Private ActiveInfo As Lwjgl3ifyInfo = Nothing
    ''' <summary>
    ''' 上一次被打过启动补丁的版本名称，用于在下次启动前还原版本 JSON。
    ''' </summary>
    Private LastPatchedInstance As String = Nothing

    ''' <summary>
    ''' 当前启动中已应用的 lwjgl3ify 版本号。未应用则为 Nothing。
    ''' </summary>
    Public ReadOnly Property ActiveVersion As String
        Get
            Return ActiveInfo?.Version
        End Get
    End Property

#Region "检测"

    Private Class ModJarCacheEntry
        Public Stamp As Date
        Public JarPath As String
    End Class
    Private ReadOnly ModJarCache As New ConcurrentDictionary(Of String, ModJarCacheEntry)

    ''' <summary>
    ''' 获取版本 mods 文件夹中的 lwjgl3ify Mod。未安装则返回 Nothing。
    ''' </summary>
    Public Function GetModJar(Instance As McInstance) As String
        If Instance Is Nothing Then Return Nothing
        Dim ModsFolder As String = Instance.PathIndie & "mods\"
        Dim ModsInfo As DirectoryInfo = DirectoryUtils.GetInfo(ModsFolder)
        Dim Stamp As Date = If(ModsInfo.Exists, ModsInfo.LastWriteTimeUtc, Date.MinValue)
        Dim CacheKey As String = PathUtils.ForCompare(ModsFolder)
        Dim Cached As ModJarCacheEntry = Nothing
        If ModJarCache.TryGetValue(CacheKey, Cached) AndAlso Cached.Stamp = Stamp Then Return Cached.JarPath
        Dim Found As String = Nothing
        If ModsInfo.Exists Then
            Try
                Found = DirectoryUtils.EnumerateFiles(ModsFolder, searchPattern:="*.jar").
                    Where(Function(FilePath) IsLwjgl3ifyFileName(PathUtils.GetLastPart(FilePath))).
                    OrderByDescending(Function(FilePath) PathUtils.GetLastPart(FilePath), StringComparer.OrdinalIgnoreCase).
                    FirstOrDefault()
            Catch ex As Exception
                Logger.Warn(ex, $"搜索 mods 文件夹中的 lwjgl3ify 时出错（{ModsFolder}）")
            End Try
        End If
        ModJarCache(CacheKey) = New ModJarCacheEntry With {.Stamp = Stamp, .JarPath = Found}
        Return Found
    End Function

    Private Function IsLwjgl3ifyFileName(FileName As String) As Boolean
        If Not FileName.StartsWithF(ModFileNameHead, True) Then Return False
        If Not FileName.EndsWithF(".jar", True) Then Return False
        '排除源码、API 之类的附属文件
        Return Not (FileName.ContainsIgnoreCase("-sources") OrElse FileName.ContainsIgnoreCase("-javadoc") OrElse
                    FileName.ContainsIgnoreCase("-dev") OrElse FileName.ContainsIgnoreCase("-api"))
    End Function

    ''' <summary>
    ''' 该版本是否安装了 lwjgl3ify（1.7.10 Forge + mods 文件夹中有对应的 Mod）。
    ''' </summary>
    Public Function IsEnabled(Instance As McInstance) As Boolean
        If Instance Is Nothing Then Return False
        Try
            Dim TargetVersion As McVersion = Instance.Version
            If TargetVersion Is Nothing OrElse Not TargetVersion.Valid Then Return False
            If TargetVersion.Vanilla <> New Version(1, 7, 10) Then Return False 'lwjgl3ify 只支持 1.7.10
            If Not TargetVersion.ModLoaders.HasFlagF(ModLoaders.Forge) Then Return False
            Return GetModJar(Instance) IsNot Nothing
        Catch ex As Exception
            Logger.Warn(ex, "检测 lwjgl3ify 时出错")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 当前启动中是否已对指定版本应用了 lwjgl3ify 的启动配置。
    ''' </summary>
    Public Function IsActive(Instance As McInstance) As Boolean
        Return ActiveInfo IsNot Nothing AndAlso Instance IsNot Nothing AndAlso ActiveInfo.InstanceName = Instance.Name
    End Function

    ''' <summary>
    ''' 获取当前启动中已应用的 JVM 参数。未应用则返回空列表。
    ''' </summary>
    Public Function GetActiveJvmArguments() As List(Of String)
        If ActiveInfo Is Nothing OrElse ActiveInfo.JvmArguments Is Nothing Then Return New List(Of String)
        Return ActiveInfo.JvmArguments
    End Function

#End Region

#Region "应用"

    ''' <summary>
    ''' 在启动前把 lwjgl3ify 的启动配置合并进版本 JSON（仅内存，不会写回磁盘）。
    ''' 必须在选择 Java 之后、补全文件与生成启动参数之前调用。
    ''' </summary>
    ''' <param name="Instance">要启动的版本。</param>
    ''' <param name="TargetJava">本次启动实际使用的 Java。</param>
    ''' <returns>是否应用了启动配置。</returns>
    Public Function Prepare(Instance As McInstance, TargetJava As Java) As Boolean
        '丢弃上一次启动可能打上的补丁，保证每次都从磁盘上的原始版本信息开始
        ResetPatchedInstance(Instance)
        ActiveInfo = Nothing
        If Not IsEnabled(Instance) Then Return False

        'Java 版本过低时不打补丁，交给 lwjgl3ify 自带的重启机制（Relauncher）处理
        Dim JavaMajor As Integer = If(TargetJava?.Version?.Major, 0)
        If JavaMajor < RequiredJavaMajor Then
            Logger.Info($"lwjgl3ify 需要使用 Java {RequiredJavaMajor} 或更高版本，本次选择的 Java {If(TargetJava, Nothing)} 版本过低，" &
                        "将使用 lwjgl3ify 自带的重启机制")
            Return False
        End If

        '读取 Mod 自带的启动元数据
        Dim JarPath As String = GetModJar(Instance)
        Dim Profile As JObject = Nothing
        Try
            Profile = ReadProfile(JarPath)
        Catch ex As Exception
            Logger.Warn(ex, $"读取 lwjgl3ify 的启动配置失败，将按原版方式启动（{JarPath}）")
        End Try
        If Profile Is Nothing Then Return False
        If Profile("libraries") Is Nothing OrElse Profile("mainClass") Is Nothing Then
            Logger.Warn($"lwjgl3ify 的启动配置不完整，将按原版方式启动（{JarPath}）")
            Return False
        End If

        Dim Info As New Lwjgl3ifyInfo With {
            .InstanceName = Instance.Name,
            .JarPath = JarPath,
            .Profile = Profile}
        Info.Version = GetProfileVersion(Profile, JarPath)
        Info.JvmArguments = ReadProfileJvmArguments(Profile)
        Info.ExpectedForgeVersion = GetProfileForgeVersion(Profile)

        '合并支持库
        Dim Libraries As JArray = BuildLibraries(Instance, Info)
        If Libraries Is Nothing OrElse Libraries.Count = 0 Then
            Logger.Warn("lwjgl3ify 的启动配置中没有可用的支持库，将按原版方式启动")
            Return False
        End If

        '写入版本 JSON（仅内存）
        Dim Json As JObject = Instance.JsonObject
        If Json Is Nothing Then Return False
        Json("libraries") = Libraries
        Json("mainClass") = Profile("mainClass").ToString
        '注意：不复制启动元数据中的 javaVersion 项，Java 版本由 PCL 按 Java 17+ 自行选择
        ActiveInfo = Info
        LastPatchedInstance = Instance.Name
        Logger.Info($"已为 {Instance.Name} 应用 lwjgl3ify {Info.Version} 的启动配置：支持库 {Libraries.Count} 项，主类 {Profile("mainClass")}")

        'Forge 版本检查
        Dim InstanceForge As String = Nothing
        If Instance.Version.ModLoaderVersions.ContainsKey(ModLoaders.Forge) Then
            InstanceForge = Instance.Version.ModLoaderVersions(ModLoaders.Forge)
        End If
        If Not String.IsNullOrEmpty(Info.ExpectedForgeVersion) AndAlso Not String.IsNullOrEmpty(InstanceForge) AndAlso
           InstanceForge <> "未知版本" AndAlso Not InstanceForge.Contains(Info.ExpectedForgeVersion) Then
            Logger.Warn($"该版本的 Forge 版本（{InstanceForge}）与 lwjgl3ify 预期的 Forge 版本（{Info.ExpectedForgeVersion}）不一致，游戏可能无法正常启动")
        End If

        '前置检查
        If Not HasUniMixins(Instance) Then
            Logger.Warn("未在 mods 文件夹中找到 UniMixins，而 lwjgl3ify 需要它才能启动，请安装 UniMixins 后再试", LogBehavior.Toast)
        End If
        Return True
    End Function

    ''' <summary>
    ''' 若该版本在上一次启动中被打过补丁，则还原它的版本 JSON 缓存。
    ''' </summary>
    Private Sub ResetPatchedInstance(Instance As McInstance)
        If Instance Is Nothing Then Return
        If LastPatchedInstance Is Nothing OrElse LastPatchedInstance <> Instance.Name Then Return
        Try
            Instance.JsonObject = Nothing '清除内存中的补丁，下次访问时会重新从磁盘读取
        Catch ex As Exception
            Logger.Warn(ex, $"还原版本 JSON 缓存失败（{Instance.Name}）")
        End Try
        LastPatchedInstance = Nothing
    End Sub

    ''' <summary>
    ''' 从 Mod 的 Jar 中读取启动元数据。
    ''' </summary>
    Private Function ReadProfile(JarPath As String) As JObject
        Using Archive As ZipArchive = FileUtils.OpenZip(JarPath)
            Dim Entry As ZipArchiveEntry = Archive.GetEntry(ProfileEntryPath)
            If Entry Is Nothing Then
                Entry = Archive.Entries.FirstOrDefault(
                    Function(E) E.FullName.Equals(ProfileEntryPath, StringComparison.OrdinalIgnoreCase))
            End If
            If Entry Is Nothing Then
                Logger.Warn($"lwjgl3ify 的 Jar 中没有启动元数据（{ProfileEntryPath}），该 Mod 版本可能过旧")
                Return Nothing
            End If
            Using EntryStream As Stream = Entry.Open()
                Dim Content As String = EntryStream.ReadString(Encoding.UTF8).TrimStart(ChrW(&HFEFF))
                Return Content.DeserializeJson()
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 从启动元数据中获取 lwjgl3ify 的版本号。
    ''' </summary>
    Private Function GetProfileVersion(Profile As JObject, JarPath As String) As String
        Dim Id As String = If(Profile("id")?.ToString, "")
        Const Head As String = "lwjgl3ify-"
        Dim Pos As Integer = Id.IndexOf(Head, StringComparison.OrdinalIgnoreCase)
        If Pos >= 0 Then Return Id.Substring(Pos + Head.Length)
        Return PathUtils.GetFileNameWithoutExtension(PathUtils.GetLastPart(JarPath))
    End Function

    ''' <summary>
    ''' 从启动元数据的版本 ID 中获取预期的 Forge 版本。
    ''' </summary>
    Private Function GetProfileForgeVersion(Profile As JObject) As String
        Dim Id As String = If(Profile("id")?.ToString, "")
        Const Head As String = "Forge"
        Dim Pos As Integer = Id.IndexOf(Head, StringComparison.OrdinalIgnoreCase)
        If Pos < 0 Then Return Nothing
        Dim Rest As String = Id.Substring(Pos + Head.Length)
        Dim EndPos As Integer = Rest.IndexOf("-"c)
        If EndPos >= 0 Then Rest = Rest.Substring(0, EndPos)
        Return Rest
    End Function

    ''' <summary>
    ''' 从启动元数据中获取需要追加的 JVM 参数（去掉由 PCL 负责的 classpath 与 natives 参数）。
    ''' </summary>
    Private Function ReadProfileJvmArguments(Profile As JObject) As List(Of String)
        Dim Result As New List(Of String)
        Dim Arguments As JToken = Profile("arguments")?("jvm")
        If Arguments Is Nothing Then Return Result
        For Each Token As JToken In Arguments
            If Token.Type <> JTokenType.String Then Continue For
            Dim Argument As String = Token.ToString.Trim
            If Argument = "" Then Continue For
            '这些由 PCL 自行添加
            If Argument = "-cp" OrElse Argument = "${classpath}" OrElse Argument.StartsWithF("-Djava.library.path=") Then Continue For
            If Result.Contains(Argument) Then Continue For
            Result.Add(Argument)
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 合并出 lwjgl3ify 需要的支持库列表：以启动元数据为准，保留版本自己额外安装的、不会被替换的支持库。
    ''' </summary>
    Private Function BuildLibraries(Instance As McInstance, Info As Lwjgl3ifyInfo) As JArray
        Dim Result As New JArray
        Dim Added As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim ForgePatches As JObject = Nothing

        '启动元数据中的支持库
        For Each Token As JToken In Info.Profile("libraries")
            If Token.Type <> JTokenType.Object Then Continue For
            Dim Library As JObject = CType(Token.DeepClone(), JObject)
            Dim LibraryName As String = If(Library("name")?.ToString, "")
            If LibraryName = "" Then Continue For
            If Not IsLibraryForThisPlatform(LibraryName) Then Continue For
            If IsForgePatchesLibrary(LibraryName) Then
                '前置 Jar 必须排在支持库列表的最前面，它需要覆盖掉 Forge 与 launchwrapper 的部分类
                If ForgePatches Is Nothing Then
                    ForgePatches = Library
                    Added.Add(GetLibraryIdentity(LibraryName))
                End If
                Continue For
            End If
            If Not Added.Add(GetLibraryIdentity(LibraryName)) Then Continue For
            Result.Add(Library)
        Next

        '保留版本自己额外安装的、不会被替换掉的支持库（例如整合包手动塞进版本 JSON 的自定义库）
        Dim ExistingLibraries As JArray = Nothing
        Try
            ExistingLibraries = TryCast(Instance.JsonObject("libraries"), JArray)
        Catch ex As Exception
            Logger.Warn(ex, "读取版本原有的支持库列表失败")
        End Try
        If ExistingLibraries IsNot Nothing Then
            For Each Token As JToken In ExistingLibraries
                If Token.Type <> JTokenType.Object Then Continue For
                Dim Library As JObject = CType(Token.DeepClone(), JObject)
                Dim LibraryName As String = If(Library("name")?.ToString, "")
                If LibraryName = "" Then Continue For
                If IsReplacedLibrary(LibraryName) Then Continue For
                If Not Added.Add(GetLibraryIdentity(LibraryName)) Then Continue For
                Result.Add(Library)
            Next
        End If

        '处理前置 Jar
        If ForgePatches IsNot Nothing Then
            Try
                ExtractForgePatches(Info, ForgePatches)
            Catch ex As Exception
                Logger.Warn(ex, "从 lwjgl3ify 的 Jar 中提取前置 Jar 失败，将改为从网络下载")
            End Try
            Result.Insert(0, ForgePatches)
        Else
            Logger.Warn("lwjgl3ify 的启动配置中没有找到前置 Jar，游戏可能无法启动")
        End If

        Return Result
    End Function

    ''' <summary>
    ''' 把 Mod 中自带的前置支持库提取到支持库文件夹，避免再从网络下载。
    ''' </summary>
    Private Sub ExtractForgePatches(Info As Lwjgl3ifyInfo, Library As JObject)
        Dim LibraryName As String = If(Library("name")?.ToString, "")
        If LibraryName = "" Then Return
        Dim Target As String = McLibGet(LibraryName)
        Dim Downloads As JObject = TryCast(Library("downloads"), JObject)
        Dim Artifact As JObject = If(Downloads Is Nothing, Nothing, TryCast(Downloads("artifact"), JObject))
        Dim ExpectedSha1 As String = If(Artifact Is Nothing, "", If(Artifact("sha1")?.ToString, ""))
        If FileUtils.Exists(Target) AndAlso
           (String.IsNullOrEmpty(ExpectedSha1) OrElse CryptographyUtils.ComputeFileHash(Target, CryptographyUtils.HashMethod.Sha1) = ExpectedSha1) Then Return

        Using Archive As ZipArchive = FileUtils.OpenZip(Info.JarPath)
            Dim Entry As ZipArchiveEntry = Archive.GetEntry(ForgePatchesEntryPath)
            If Entry Is Nothing Then Return
            DirectoryUtils.Create(PathUtils.RemoveLastPart(Target))
            Entry.ExtractToFile(Target, True)
        End Using
        If Artifact IsNot Nothing Then
            '按实际文件更新校验信息，避免 PCL 认为文件损坏而重新下载
            Artifact("sha1") = CryptographyUtils.ComputeFileHash(Target, CryptographyUtils.HashMethod.Sha1)
            Artifact("size") = FileUtils.GetInfo(Target).Length
        End If
        Logger.Info($"已从 lwjgl3ify 的 Jar 中提取前置支持库：{Target}")
    End Sub

    ''' <summary>
    ''' 该支持库是否属于会被 lwjgl3ify 启动配置替换掉的组。
    ''' </summary>
    Private Function IsReplacedLibrary(LibraryName As String) As Boolean
        Dim Group As String = LibraryName.Split(":"c)(0)
        Return ReplacedLibraryGroups.Any(Function(Head) Group.StartsWithF(Head, True))
    End Function

    ''' <summary>
    ''' 该支持库在当前平台上是否需要下载。
    ''' 启动元数据里包含了所有平台、所有架构的 LWJGL3 natives，这里只保留 Windows 上可能用到的部分。
    ''' </summary>
    Private Function IsLibraryForThisPlatform(LibraryName As String) As Boolean
        Dim Parts As String() = LibraryName.Split(":"c)
        If Parts.Length < 2 Then Return True
        Dim Segments As String() = Parts(1).Split("-"c)
        Dim NativeIndex As Integer = -1
        Dim SegmentIndex As Integer
        For SegmentIndex = 0 To Segments.Length - 1
            If Segments(SegmentIndex).Equals("natives", StringComparison.OrdinalIgnoreCase) Then NativeIndex = SegmentIndex
        Next
        If NativeIndex < 0 Then Return True '不是与平台相关的支持库
        Dim OsName As String = If(NativeIndex + 1 < Segments.Length, Segments(NativeIndex + 1).ToLower(), "")
        If OsName <> "windows" Then Return False
        Dim Arch As String = If(NativeIndex + 2 < Segments.Length, Segments(NativeIndex + 2).ToLower(), "")
        If Arch = "arm64" Then Return IsArm64Windows()
        Return True '保留 x64 与 x86，以同时兼容 64 位与 32 位 Java
    End Function

    Private Function IsArm64Windows() As Boolean
        Dim Arch As String = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
        Return String.Equals(Arch, "ARM64", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsForgePatchesLibrary(LibraryName As String) As Boolean
        Return LibraryName.EndsWithF(":forgePatches", True)
    End Function

    ''' <summary>
    ''' 获取支持库的组与名称，用于去重。
    ''' </summary>
    Private Function GetLibraryIdentity(LibraryName As String) As String
        Dim Parts As String() = LibraryName.Split(":"c)
        If Parts.Length >= 2 Then Return Parts(0) & ":" & Parts(1)
        Return LibraryName
    End Function

    ''' <summary>
    ''' 是否已安装 UniMixins（lwjgl3ify 的前置）。
    ''' </summary>
    Private Function HasUniMixins(Instance As McInstance) As Boolean
        Try
            Dim ModsFolder As String = Instance.PathIndie & "mods\"
            If Not DirectoryUtils.Exists(ModsFolder) Then Return False
            Dim ModFiles As List(Of String) = DirectoryUtils.EnumerateFiles(ModsFolder, searchPattern:="*.jar").ToList()
            Return ModFiles.Any(Function(FilePath) PathUtils.GetLastPart(FilePath).ContainsIgnoreCase("unimixins"))
        Catch ex As Exception
            Logger.Warn(ex, "检查 UniMixins 时出错")
            Return True '检查失败时不做提示，避免误报
        End Try
    End Function

#End Region

End Module
