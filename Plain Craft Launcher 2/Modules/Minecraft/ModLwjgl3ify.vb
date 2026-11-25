Public Module ModLwjgl3ify

    Public Const RequiredJavaMajor As Integer = 17

    Private Const ModFileNameHead As String = "lwjgl3ify"
    Private Const ProfileEntryPath As String = "me/eigenraven/lwjgl3ify/relauncher/version.json"
    Private Const ForgePatchesEntryPath As String = "me/eigenraven/lwjgl3ify/relauncher/forgePatches.zip"

    Private ReadOnly ReplacedLibraryGroups As String() = {
        "net.minecraft", "net.minecraftforge", "com.mojang", "org.lwjgl", "net.java", "tv.twitch", "com.paulscode",
        "java3d", "lzma", "net.sf", "com.google", "commons-", "org.apache", "io.netty", "com.typesafe",
        "org.scala-lang", "com.ibm.icu", "org.ow2", "org.yaml", "org.jetbrains"}

    Public Class Lwjgl3ifyInfo
        Public Property InstanceName As String
        Public Property JarPath As String
        Public Property Version As String
        Public Property Profile As JObject
        Public Property JvmArguments As List(Of String)
        Public Property ExpectedForgeVersion As String
    End Class

    Private ActiveInfo As Lwjgl3ifyInfo = Nothing
    Private LastPatchedInstance As String = Nothing

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
        Return Not (FileName.ContainsIgnoreCase("-sources") OrElse FileName.ContainsIgnoreCase("-javadoc") OrElse
                    FileName.ContainsIgnoreCase("-dev") OrElse FileName.ContainsIgnoreCase("-api"))
    End Function

    Public Function IsEnabled(Instance As McInstance) As Boolean
        If Instance Is Nothing Then Return False
        Try
            Dim TargetVersion As McVersion = Instance.Version
            If TargetVersion Is Nothing OrElse Not TargetVersion.Valid Then Return False
            If TargetVersion.Vanilla <> New Version(1, 7, 10) Then Return False
            If Not TargetVersion.ModLoaders.HasFlagF(ModLoaders.Forge) Then Return False
            Return GetModJar(Instance) IsNot Nothing
        Catch ex As Exception
            Logger.Warn(ex, "检测 lwjgl3ify 时出错")
            Return False
        End Try
    End Function

    Public Function IsActive(Instance As McInstance) As Boolean
        Return ActiveInfo IsNot Nothing AndAlso Instance IsNot Nothing AndAlso ActiveInfo.InstanceName = Instance.Name
    End Function

    Public Function GetActiveJvmArguments() As List(Of String)
        If ActiveInfo Is Nothing OrElse ActiveInfo.JvmArguments Is Nothing Then Return New List(Of String)
        Return ActiveInfo.JvmArguments
    End Function

#End Region

#Region "应用"

    Public Function Prepare(Instance As McInstance, TargetJava As Java) As Boolean
        ResetPatchedInstance(Instance)
        ActiveInfo = Nothing
        If Not IsEnabled(Instance) Then Return False

        Dim JavaMajor As Integer = If(TargetJava?.Version?.Major, 0)
        If JavaMajor < RequiredJavaMajor Then
            Logger.Info($"lwjgl3ify 需要使用 Java {RequiredJavaMajor} 或更高版本，本次选择的 Java {If(TargetJava, Nothing)} 版本过低，" &
                        "将使用 lwjgl3ify 自带的重启机制")
            Return False
        End If

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

        Dim Libraries As JArray = BuildLibraries(Instance, Info)
        If Libraries Is Nothing OrElse Libraries.Count = 0 Then
            Logger.Warn("lwjgl3ify 的启动配置中没有可用的支持库，将按原版方式启动")
            Return False
        End If

        Dim Json As JObject = Instance.JsonObject
        If Json Is Nothing Then Return False
        Json("libraries") = Libraries
        Json("mainClass") = Profile("mainClass").ToString
        ActiveInfo = Info
        LastPatchedInstance = Instance.Name
        Logger.Info($"已为 {Instance.Name} 应用 lwjgl3ify {Info.Version} 的启动配置：支持库 {Libraries.Count} 项，主类 {Profile("mainClass")}")

        Dim InstanceForge As String = Nothing
        If Instance.Version.ModLoaderVersions.ContainsKey(ModLoaders.Forge) Then
            InstanceForge = Instance.Version.ModLoaderVersions(ModLoaders.Forge)
        End If
        If Not String.IsNullOrEmpty(Info.ExpectedForgeVersion) AndAlso Not String.IsNullOrEmpty(InstanceForge) AndAlso
           InstanceForge <> "未知版本" AndAlso Not InstanceForge.Contains(Info.ExpectedForgeVersion) Then
            Logger.Warn($"该版本的 Forge 版本（{InstanceForge}）与 lwjgl3ify 预期的 Forge 版本（{Info.ExpectedForgeVersion}）不一致，游戏可能无法正常启动")
        End If

        If Not HasUniMixins(Instance) Then
            Logger.Warn("未在 mods 文件夹中找到 UniMixins，而 lwjgl3ify 需要它才能启动，请安装 UniMixins 后再试", LogBehavior.Toast)
        End If
        Return True
    End Function

    Private Sub ResetPatchedInstance(Instance As McInstance)
        If Instance Is Nothing Then Return
        If LastPatchedInstance Is Nothing OrElse LastPatchedInstance <> Instance.Name Then Return
        Try
            Instance.JsonObject = Nothing
        Catch ex As Exception
            Logger.Warn(ex, $"还原版本 JSON 缓存失败（{Instance.Name}）")
        End Try
        LastPatchedInstance = Nothing
    End Sub

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

    Private Function GetProfileVersion(Profile As JObject, JarPath As String) As String
        Dim Id As String = If(Profile("id")?.ToString, "")
        Const Head As String = "lwjgl3ify-"
        Dim Pos As Integer = Id.IndexOf(Head, StringComparison.OrdinalIgnoreCase)
        If Pos >= 0 Then Return Id.Substring(Pos + Head.Length)
        Return PathUtils.GetFileNameWithoutExtension(PathUtils.GetLastPart(JarPath))
    End Function

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

    Private Function ReadProfileJvmArguments(Profile As JObject) As List(Of String)
        Dim Result As New List(Of String)
        Dim Arguments As JToken = Profile("arguments")?("jvm")
        If Arguments Is Nothing Then Return Result
        For Each Token As JToken In Arguments
            If Token.Type <> JTokenType.String Then Continue For
            Dim Argument As String = Token.ToString.Trim
            If Argument = "" Then Continue For
            If Argument = "-cp" OrElse Argument = "${classpath}" OrElse Argument.StartsWithF("-Djava.library.path=") Then Continue For
            If Result.Contains(Argument) Then Continue For
            Result.Add(Argument)
        Next
        Return Result
    End Function

    Private Function BuildLibraries(Instance As McInstance, Info As Lwjgl3ifyInfo) As JArray
        Dim Result As New JArray
        Dim Added As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim ForgePatches As JObject = Nothing

        For Each Token As JToken In Info.Profile("libraries")
            If Token.Type <> JTokenType.Object Then Continue For
            Dim Library As JObject = CType(Token.DeepClone(), JObject)
            Dim LibraryName As String = If(Library("name")?.ToString, "")
            If LibraryName = "" Then Continue For
            If Not IsLibraryForThisPlatform(LibraryName) Then Continue For
            If IsForgePatchesLibrary(LibraryName) Then
                If ForgePatches Is Nothing Then
                    ForgePatches = Library
                    Added.Add(GetLibraryIdentity(LibraryName))
                End If
                Continue For
            End If
            If Not Added.Add(GetLibraryIdentity(LibraryName)) Then Continue For
            Result.Add(Library)
        Next

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
            Artifact("sha1") = CryptographyUtils.ComputeFileHash(Target, CryptographyUtils.HashMethod.Sha1)
            Artifact("size") = FileUtils.GetInfo(Target).Length
        End If
        Logger.Info($"已从 lwjgl3ify 的 Jar 中提取前置支持库：{Target}")
    End Sub

    Private Function IsReplacedLibrary(LibraryName As String) As Boolean
        Dim Group As String = LibraryName.Split(":"c)(0)
        Return ReplacedLibraryGroups.Any(Function(Head) Group.StartsWithF(Head, True))
    End Function

    Private Function IsLibraryForThisPlatform(LibraryName As String) As Boolean
        Dim Parts As String() = LibraryName.Split(":"c)
        If Parts.Length < 2 Then Return True
        Dim Segments As String() = Parts(1).Split("-"c)
        Dim NativeIndex As Integer = -1
        Dim SegmentIndex As Integer
        For SegmentIndex = 0 To Segments.Length - 1
            If Segments(SegmentIndex).Equals("natives", StringComparison.OrdinalIgnoreCase) Then NativeIndex = SegmentIndex
        Next
        If NativeIndex < 0 Then Return True
        Dim OsName As String = If(NativeIndex + 1 < Segments.Length, Segments(NativeIndex + 1).ToLower(), "")
        If OsName <> "windows" Then Return False
        Dim Arch As String = If(NativeIndex + 2 < Segments.Length, Segments(NativeIndex + 2).ToLower(), "")
        If Arch = "arm64" Then Return IsArm64Windows()
        Return True
    End Function

    Private Function IsArm64Windows() As Boolean
        Dim Arch As String = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
        Return String.Equals(Arch, "ARM64", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsForgePatchesLibrary(LibraryName As String) As Boolean
        Return LibraryName.EndsWithF(":forgePatches", True)
    End Function

    Private Function GetLibraryIdentity(LibraryName As String) As String
        Dim Parts As String() = LibraryName.Split(":"c)
        If Parts.Length >= 2 Then Return Parts(0) & ":" & Parts(1)
        Return LibraryName
    End Function

    Private Function HasUniMixins(Instance As McInstance) As Boolean
        Try
            Dim ModsFolder As String = Instance.PathIndie & "mods\"
            If Not DirectoryUtils.Exists(ModsFolder) Then Return False
            Dim ModFiles As List(Of String) = DirectoryUtils.EnumerateFiles(ModsFolder, searchPattern:="*.jar").ToList()
            Return ModFiles.Any(Function(FilePath) PathUtils.GetLastPart(FilePath).ContainsIgnoreCase("unimixins"))
        Catch ex As Exception
            Logger.Warn(ex, "检查 UniMixins 时出错")
            Return True
        End Try
    End Function

#End Region

End Module
