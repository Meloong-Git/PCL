Imports System.IO.Compression

Public Module ModMod
    Private Const LocalModCacheVersion As Integer = 11

    Public Class McMod

#Region "基础"

        ''' <summary>
        ''' Mod 文件的地址。
        ''' </summary>
        Public ReadOnly Path As String
        Public Sub New(Path As String)
            Me.Path = If(Path, "")
        End Sub
        ''' <summary>
        ''' Mod 的完整路径，去除最后的 .disabled 和 .old。
        ''' </summary>
        Public ReadOnly Property RawPath As String
            Get
                Return GetPathFromFullPath(Path) & RawFileName
            End Get
        End Property

        ''' <summary>
        ''' Mod 的完整文件名。
        ''' </summary>
        Public ReadOnly Property FileName As String
            Get
                Return GetFileNameFromPath(Path)
            End Get
        End Property

        ''' <summary>
        ''' Mod 的完整文件名，去除最后的 .disabled 和 .old。
        ''' </summary>
        Public ReadOnly Property RawFileName As String
            Get
                Return FileName.Replace(".disabled", "").Replace(".old", "")
            End Get
        End Property

        ''' <summary>
        ''' Mod 的状态。
        ''' </summary>
        Public ReadOnly Property State As McModState
            Get
                If Path.EndsWithF(".disabled", True) OrElse Path.EndsWithF(".old", True) Then
                    Return McModState.Disabled
                Else
                    Return McModState.Fine
                End If
            End Get
        End Property
        Public Enum McModState As Integer
            Fine = 0
            Disabled = 1
        End Enum

#End Region

#Region "信息项"

        ''' <summary>
        ''' 当任何信息在初始化后更新时触发。
        ''' </summary>
        Public Event OnUpdate(sender As McMod)

        ''' <summary>
        ''' Mod 的可读名称。
        ''' 可能为无扩展的文件名，但不会是 Nothing。
        ''' </summary>
        Public Property DisplayName As String
            Get
                If _Name Is Nothing Then _Name = CompFile?.DisplayName
                If _Name Is Nothing Then _Name = IO.Path.GetFileNameWithoutExtension(Path)
                Return _Name
            End Get
            Set(value As String)
                If _Name Is Nothing AndAlso value IsNot Nothing AndAlso Not value.Contains("modname") AndAlso
                   value.ToLower <> "name" AndAlso value.Count > 1 AndAlso Val(value).ToString <> value Then
                    _Name = value
                End If
            End Set
        End Property
        Private _Name As String = Nothing

        ''' <summary>
        ''' Mod 的描述信息。
        ''' 可能为 Nothing。
        ''' </summary>
        Public Property Description As String
            Get
                Return _Description
            End Get
            Set(value As String)
                If _Description Is Nothing AndAlso value IsNot Nothing AndAlso value.Count > 2 Then
                    _Description = value.ToString.Trim(vbLf)
                    '优化显示：若以 [a-zA-Z0-9] 结尾，加上小数点句号
                    If _Description.ToLower.LastIndexOfAny("qwertyuiopasdfghjklzxcvbnm0123456789") = _Description.Count - 1 Then _Description += "."
                End If
            End Set
        End Property
        Private _Description As String = Nothing

        ''' <summary>
        ''' Mod 的版本。
        ''' 不保证符合版本格式规范，且可能为 Nothing。
        ''' </summary>
        Public Property Version As String
            Get
                If _Version Is Nothing Then _Version = CompFile?.Version
                Return _Version
            End Get
            Set(value As String)
                If _Version IsNot Nothing AndAlso RegexCheck(_Version, "[0-9.\-]+") Then Return
                If value IsNot Nothing AndAlso value.ContainsF("version", True) Then value = "version" '需要修改的标识
                _Version = value
            End Set
        End Property
        Public _Version As String = Nothing

#End Region

#Region "从 JAR 获取信息"

        ''' <summary>
        ''' 从 JAR 文件中获取 Mod 信息。
        ''' </summary>
        Public Sub LoadMetadataFromJar()
            Static IsLoaded As Boolean = False
            If IsLoaded Then Return
            IsLoaded = True
            Dim Jar As ZipArchive = Nothing
            Try
                '基础可用性检查、打开 Jar 文件
                If Path.Length < 2 Then Throw New FileNotFoundException("错误的 Mod 文件路径（" & If(Path, "null") & "）")
                If Not File.Exists(Path) Then Throw New FileNotFoundException("未找到 Mod 文件（" & Path & "）")
                Jar = New ZipArchive(New FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                '信息获取
                LoadMetadataFromJar(Jar)
            Catch ex As UnauthorizedAccessException
                Log(ex, "Mod 文件由于无权限无法打开（" & Path & "）", LogLevel.Developer)
            Catch ex As Exception
                Log(ex, "Mod 文件无法打开（" & Path & "）", LogLevel.Developer)
            Finally
                If Jar IsNot Nothing Then Jar.Dispose()
            End Try
        End Sub
        Private Sub LoadMetadataFromJar(Jar As ZipArchive)

#Region "尝试使用 mcmod.info"
            Try
                '获取信息文件
                Dim InfoEntry As ZipArchiveEntry = Jar.GetEntry("mcmod.info")
                Dim InfoString As String = Nothing
                If InfoEntry IsNot Nothing Then
                    InfoString = ReadFile(InfoEntry.Open())
                    If InfoString.Length < 15 Then InfoString = Nothing
                End If
                If InfoString Is Nothing Then Exit Try
                '获取可用 Json 项
                Dim InfoObject As JObject
                Dim JsonObject = GetJson(InfoString)
                If JsonObject.Type = JTokenType.Array Then
                    InfoObject = JsonObject(0)
                Else
                    InfoObject = JsonObject("modList")(0)
                End If
                '从文件中获取 Mod 信息项
                DisplayName = InfoObject("name")
                Description = InfoObject("description")
                Version = InfoObject("version")
            Catch ex As Exception
                Log(ex, "读取 mcmod.info 时出现未知错误（" & Path & "）", LogLevel.Developer)
            End Try
#End Region
#Region "尝试使用 fabric.mod.json"
            Try
                '获取 fabric.mod.json 文件
                Dim FabricEntry As ZipArchiveEntry = Jar.GetEntry("fabric.mod.json")
                Dim FabricText As String = Nothing
                If FabricEntry IsNot Nothing Then
                    FabricText = ReadFile(FabricEntry.Open(), Encoding.UTF8)
                    If Not FabricText.Contains("schemaVersion") Then FabricText = Nothing
                End If
                If FabricText Is Nothing Then Exit Try
                Dim FabricObject As JObject = GetJson(FabricText)
GotFabric:
                '从文件中获取 Mod 信息项
                If FabricObject.ContainsKey("name") Then DisplayName = FabricObject("name")
                If FabricObject.ContainsKey("version") Then Version = FabricObject("version")
                If FabricObject.ContainsKey("description") Then Description = FabricObject("description")
                '加载成功
                GoTo Finished
            Catch ex As Exception
                Log(ex, "读取 fabric.mod.json 时出现未知错误（" & Path & "）", LogLevel.Developer)
            End Try
#End Region
#Region "尝试使用 mods.toml"
            Try
                '获取 mods.toml 文件
                Dim TomlEntry As ZipArchiveEntry = Jar.GetEntry("META-INF/mods.toml")
                Dim TomlText As String = Nothing
                If TomlEntry IsNot Nothing Then
                    TomlText = ReadFile(TomlEntry.Open())
                    If TomlText.Length < 15 Then TomlText = Nothing
                End If
                If TomlText Is Nothing Then Exit Try
                '文件标准化：统一换行符为 vbLf，去除注释、头尾的空格、空行
                Dim Lines As New List(Of String)
                For Each Line In TomlText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf) '统一换行符
                    If Line.StartsWithF("#") Then '去除注释
                        Continue For
                    ElseIf Line.Contains("#") Then
                        Line = Line.Substring(0, Line.IndexOfF("#"))
                    End If
                    Line = Line.Trim(New Char() {" "c, "	"c, "　"c}) '去除头尾的空格
                    If Line.Any Then Lines.Add(Line) '去除空行
                Next
                '读取文件数据
                Dim TomlData As New List(Of KeyValuePair(Of String, Dictionary(Of String, Object))) From {New KeyValuePair(Of String, Dictionary(Of String, Object))("", New Dictionary(Of String, Object))}
                For i = 0 To Lines.Count - 1
                    Dim Line As String = Lines(i)
                    If Line.StartsWithF("[") AndAlso Line.EndsWithF("]") Then
                        '段落标记
                        Dim Header = Line.Trim("[]".ToCharArray)
                        TomlData.Add(New KeyValuePair(Of String, Dictionary(Of String, Object))(Header, New Dictionary(Of String, Object)))
                    ElseIf Line.Contains("=") Then
                        '字段标记
                        Dim Key As String = Line.Substring(0, Line.IndexOfF("=")).TrimEnd(New Char() {" "c, "	"c, "　"c})
                        Dim RawValue As String = Line.Substring(Line.IndexOfF("=") + 1).TrimStart(New Char() {" "c, "	"c, "　"c})
                        Dim Value As Object
                        If RawValue.StartsWithF("""") AndAlso RawValue.EndsWithF("""") Then
                            '单行字符串
                            Value = RawValue.Trim("""")
                        ElseIf RawValue.StartsWithF("'''") Then
                            '多行字符串
                            Dim ValueLines As New List(Of String) From {RawValue.TrimStart("'")}
                            If ValueLines(0).EndsWithF("'''") Then '把多行字符串按单行写法写的错误处理（#2732）
                                ValueLines(0) = ValueLines(0).TrimEnd("'")
                            Else
                                Do Until i >= Lines.Count - 1
                                    i += 1
                                    Dim ValueLine As String = Lines(i)
                                    If ValueLine.EndsWithF("'''") Then
                                        ValueLines.Add(ValueLine.TrimEnd("'"))
                                        Exit Do
                                    Else
                                        ValueLines.Add(ValueLine)
                                    End If
                                Loop
                            End If
                            Value = Join(ValueLines, vbLf).Trim(vbLf).Replace(vbLf, vbCrLf)
                        ElseIf RawValue.ToLower = "true" OrElse RawValue.ToLower = "false" Then
                            '布尔型
                            Value = (RawValue.ToLower = "true")
                        ElseIf Val(RawValue).ToString = RawValue Then
                            '数字型
                            Value = Val(RawValue)
                        Else
                            '不知道是个啥玩意儿，直接存储
                            Value = RawValue
                        End If
                        TomlData.Last.Value(Key) = Value
                    Else
                        '不知道是个啥玩意儿
                        Exit Try
                    End If
                Next
                '从文件数据中获取信息
                Dim ModEntry As Dictionary(Of String, Object) = Nothing
                For Each TomlSubData In TomlData
                    If TomlSubData.Key = "mods" Then
                        ModEntry = TomlSubData.Value
                        Exit For
                    End If
                Next
                If ModEntry Is Nothing OrElse Not ModEntry.ContainsKey("modId") Then Exit Try
                If ModEntry.ContainsKey("displayName") Then DisplayName = ModEntry("displayName")
                If ModEntry.ContainsKey("description") Then Description = ModEntry("description")
                If ModEntry.ContainsKey("version") Then Version = ModEntry("version")
                '加载成功
                GoTo Finished
            Catch ex As Exception
                Log(ex, "读取 mods.toml 时出现未知错误（" & Path & "）", LogLevel.Developer)
            End Try
#End Region
#Region "尝试使用 fml_cache_annotation.json"
            Try
                '获取 fml_cache_annotation.json 文件
                Dim FmlEntry As ZipArchiveEntry = Jar.GetEntry("META-INF/fml_cache_annotation.json")
                Dim FmlText As String = Nothing
                If FmlEntry IsNot Nothing Then
                    FmlText = ReadFile(FmlEntry.Open(), Encoding.UTF8)
                    If Not FmlText.Contains("Lnet/minecraftforge/fml/common/Mod;") Then FmlText = Nothing
                End If
                If FmlText Is Nothing Then Exit Try
                Dim FmlJson As JObject = GetJson(FmlText)
                '获取可用 Json 项
                Dim FmlObject As JObject = Nothing
                For Each ModFilePair In FmlJson
                    Dim ModFileAnnos As JArray = ModFilePair.Value("annotations")
                    If ModFileAnnos IsNot Nothing Then
                        '先获取 Mod
                        For Each ModFileAnno In ModFileAnnos
                            Dim Name As String = If(ModFileAnno("name"), "")
                            If Name = "Lnet/minecraftforge/fml/common/Mod;" Then
                                FmlObject = ModFileAnno("values")
                                GoTo Got
                            End If
                        Next
                    End If
                Next
                Exit Try
Got:
                '从文件中获取 Mod 信息项
                If FmlObject.ContainsKey("name") Then DisplayName = FmlObject("name")("value")
                If FmlObject.ContainsKey("version") Then Version = FmlObject("version")("value")
                '加载成功
                GoTo Finished
            Catch ex As Exception
                Log(ex, "读取 fml_cache_annotation.json 时出现未知错误（" & Path & "）", LogLevel.Developer)
            End Try
#End Region
            Return '没能成功获取任何信息

Finished:
#Region "将 Version 代号转换为 META-INF 中的版本"
            If _Version = "version" Then
                Try
                    Dim MetaEntry As ZipArchiveEntry = Jar.GetEntry("META-INF/MANIFEST.MF")
                    If MetaEntry IsNot Nothing Then
                        Dim MetaString As String = ReadFile(MetaEntry.Open()).Replace(" :", ":").Replace(": ", ":")
                        If MetaString.Contains("Implementation-Version:") Then
                            MetaString = MetaString.Substring(MetaString.IndexOfF("Implementation-Version:") + "Implementation-Version:".Count)
                            MetaString = MetaString.Substring(0, MetaString.IndexOfAny(vbCrLf.ToCharArray)).Trim
                            Version = MetaString
                        End If
                    End If
                Catch ex As Exception
                    Log("获取 META-INF 中的版本信息失败（" & Path & "）", LogLevel.Developer)
                    Version = Nothing
                End Try
            End If
            If _Version IsNot Nothing AndAlso Not (_Version.Contains(".") OrElse _Version.Contains("-")) Then Version = Nothing
#End Region
            RaiseEvent OnUpdate(Me)

        End Sub

#End Region

#Region "网络信息"

        ''' <summary>
        ''' 该 Mod 关联的网络项目。
        ''' </summary>
        Public Property Comp As CompProject
            Get
                Return _Comp
            End Get
            Set(value As CompProject)
                _Comp = value
                RaiseEvent OnUpdate(Me)
            End Set
        End Property
        Private _Comp As CompProject

        ''' <summary>
        ''' 本地文件对应的联网文件信息。
        ''' </summary>
        Public CompFile As CompFile

        ''' <summary>
        ''' 该 Mod 对应的联网最新版本。
        ''' </summary>
        Public Property UpdateFile As CompFile
            Get
                Return _UpdateFile
            End Get
            Set(value As CompFile)
                _UpdateFile = value
                RaiseEvent OnUpdate(Me)
            End Set
        End Property
        Private _UpdateFile As CompFile

        ''' <summary>
        ''' 该 Mod 的更新日志网址。
        ''' </summary>
        Public ChangelogUrls As New List(Of String)
        ''' <summary>
        ''' 所有网络信息是否已成功加载。
        ''' </summary>
        Public CompLoaded As Boolean = False

        ''' <summary>
        ''' 将网络信息保存为 Json。
        ''' </summary>
        Public Function ToJson() As JObject
            Dim Json As New JObject
            If Comp IsNot Nothing Then Json.Add("Comp", Comp.ToJson())
            Json.Add("ChangelogUrls", New JArray(ChangelogUrls))
            Json.Add("CompLoaded", CompLoaded)
            If CompFile IsNot Nothing Then Json.Add("CompFile", CompFile.ToJson())
            If UpdateFile IsNot Nothing Then Json.Add("UpdateFile", UpdateFile.ToJson())
            Return Json
        End Function
        ''' <summary>
        ''' 从 Json 中读取网络信息。
        ''' </summary>
        Public Sub FromJson(Json As JObject)
            CompLoaded = Json("CompLoaded")
            If Json.ContainsKey("Comp") Then Comp = New CompProject(Json("Comp"))
            If Json.ContainsKey("ChangelogUrls") Then ChangelogUrls = Json("ChangelogUrls").ToObject(Of List(Of String))
            If Json.ContainsKey("CompFile") Then CompFile = New CompFile(Json("CompFile"), CompType.Mod)
            If Json.ContainsKey("UpdateFile") Then UpdateFile = New CompFile(Json("UpdateFile"), CompType.Mod)
        End Sub

        ''' <summary>
        ''' 该文件是否可以更新。
        ''' </summary>
        Public ReadOnly Property CanUpdate As Boolean
            Get
                Return Not Setup.Get("UiHiddenFunctionModUpdate") AndAlso Not Setup.Get("VersionAdvanceDisableModUpdate", Instance:=PageInstanceLeft.Instance) AndAlso UpdateFile IsNot Nothing
            End Get
        End Property

        ''' <summary>
        ''' 获取用于 CurseForge 信息获取的 Hash 值（MurmurHash2）。
        ''' </summary>
        Public ReadOnly Property CurseForgeHash As UInteger
            Get
                If _CurseForgeHash Is Nothing Then
                    '读取缓存
                    Dim Info As New FileInfo(Path)
                    Dim CacheKey As String = GetHash($"{RawPath}-{Info.LastWriteTime.ToLongTimeString}-{Info.Length}-C")
                    Dim Cached As String = ReadIni(PathTemp & "Cache\ModHash.ini", CacheKey)
                    If Cached <> "" AndAlso RegexCheck(Cached, "^\d+$") Then '#5062
                        _CurseForgeHash = Cached
                        Return _CurseForgeHash
                    End If
                    '读取文件
                    Dim data As New List(Of Byte)
                    For Each b As Byte In ReadFileBytes(Path)
                        If b = 9 OrElse b = 10 OrElse b = 13 OrElse b = 32 Then Continue For
                        data.Add(b)
                    Next
                    '计算 MurmurHash2
                    Dim length As Integer = data.Count
                    Dim h As UInteger = 1 Xor length '1 是种子
                    Dim i As Integer
                    For i = 0 To length - 4 Step 4
                        Dim k As UInteger = data(i) Or CUInt(data(i + 1)) << 8 Or CUInt(data(i + 2)) << 16 Or CUInt(data(i + 3)) << 24
                        k = (k * &H5BD1E995L) And &HFFFFFFFFL
                        k = k Xor (k >> 24)
                        k = (k * &H5BD1E995L) And &HFFFFFFFFL
                        h = (h * &H5BD1E995L) And &HFFFFFFFFL
                        h = h Xor k
                    Next
                    Select Case length - i
                        Case 3
                            h = h Xor (data(i) Or CUInt(data(i + 1)) << 8)
                            h = h Xor (CUInt(data(i + 2)) << 16)
                            h = (h * &H5BD1E995L) And &HFFFFFFFFL
                        Case 2
                            h = h Xor (data(i) Or CUInt(data(i + 1)) << 8)
                            h = (h * &H5BD1E995L) And &HFFFFFFFFL
                        Case 1
                            h = h Xor data(i)
                            h = (h * &H5BD1E995L) And &HFFFFFFFFL
                    End Select
                    h = h Xor (h >> 13)
                    h = (h * &H5BD1E995L) And &HFFFFFFFFL
                    h = h Xor (h >> 15)
                    _CurseForgeHash = h
                    '写入缓存
                    WriteIni(PathTemp & "Cache\ModHash.ini", CacheKey, h.ToString)
                End If
                Return _CurseForgeHash
            End Get
        End Property
        Private _CurseForgeHash As UInteger?

        ''' <summary>
        ''' 获取用于 Modrinth 信息获取的 Hash 值（SHA1）。
        ''' </summary>
        Public ReadOnly Property ModrinthHash As String
            Get
                If _ModrinthHash Is Nothing Then
                    '读取缓存
                    Dim Info As New FileInfo(Path)
                    Dim CacheKey As String = GetHash($"{RawPath}-{Info.LastWriteTime.ToLongTimeString}-{Info.Length}-M")
                    Dim Cached As String = ReadIni(PathTemp & "Cache\ModHash.ini", CacheKey)
                    If Cached <> "" Then
                        _ModrinthHash = Cached
                        Return _ModrinthHash
                    End If
                    '计算 SHA1
                    _ModrinthHash = GetFileSHA1(Path)
                    '写入缓存
                    WriteIni(PathTemp & "Cache\ModHash.ini", CacheKey, _ModrinthHash)
                End If
                Return _ModrinthHash
            End Get
        End Property
        Private _ModrinthHash As String

#End Region

#Region "API"

        Public Overrides Function ToString() As String
            Return $"{State} - {Path}"
        End Function
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim target = TryCast(obj, McMod)
            Return target IsNot Nothing AndAlso Path = target.Path
        End Function

#End Region

        ''' <summary>
        ''' 是否可能为前置 Mod。目前非常不准确。
        ''' </summary>
        Public Function IsPresetMod() As Boolean
            Return DisplayName IsNot Nothing AndAlso (DisplayName.ToLower.Contains("core") OrElse DisplayName.ToLower.Contains("lib"))
        End Function

        ''' <summary>
        ''' 根据完整文件路径的文件扩展名判断是否为 Mod 文件。
        ''' </summary>
        Public Shared Function IsModFile(Path As String)
            If Path Is Nothing OrElse Not Path.Contains(".") Then Return False
            Path = Path.ToLower
            If Path.EndsWithF(".jar", True) OrElse Path.EndsWithF(".zip", True) OrElse Path.EndsWithF(".litemod", True) OrElse
               Path.EndsWithF(".jar.disabled", True) OrElse Path.EndsWithF(".zip.disabled", True) OrElse Path.EndsWithF(".litemod.disabled", True) OrElse
               Path.EndsWithF(".jar.old", True) OrElse Path.EndsWithF(".zip.old", True) OrElse Path.EndsWithF(".litemod.old", True) Then Return True
            Return False
        End Function

    End Class

    '加载 Mod 列表
    Public McModLoader As New LoaderTask(Of String, List(Of McMod))("Mod List Loader", AddressOf McModLoad)
    Private Sub McModLoad(Loader As LoaderTask(Of String, List(Of McMod)))
        Try
            RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.ShowProgress = False)

            '等待 Mod 更新完成
            If PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input) Then
                Log($"[Mod] 等待 Mod 更新完成后才能继续加载 Mod 列表：" & Loader.Input)
                Try
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在更新 Mod")
                    Do Until Not PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input)
                        If Loader.IsAborted Then Return
                        Thread.Sleep(100)
                    Loop
                Finally
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在加载 Mod 列表")
                End Try
                FrmInstanceMod.LoaderRun(LoaderFolderRunType.UpdateOnly)
            End If

            '获取 Mod 文件夹下的可用文件列表
            Dim ModFileList As New List(Of FileInfo)
            If Directory.Exists(Loader.Input) Then
                Dim RawName As String = Loader.Input.ToLower
                For Each File As FileInfo In EnumerateFiles(Loader.Input)
                    If File.DirectoryName.ToLower & "\" <> RawName Then
                        '仅当 Forge 1.13- 且文件夹名与版本号相同时，才加载该子文件夹下的 Mod
                        If Not (PageInstanceLeft.Instance IsNot Nothing AndAlso PageInstanceLeft.Instance.Version.HasForge AndAlso
                                PageInstanceLeft.Instance.Version.Vanilla.Major < 13 AndAlso
                                File.Directory.Name = $"1.{PageInstanceLeft.Instance.Version.Vanilla.Major}.{PageInstanceLeft.Instance.Version.Vanilla.Build}") Then
                            Continue For
                        End If
                    End If
                    If McMod.IsModFile(File.FullName) Then ModFileList.Add(File)
                Next
            End If

            '获取本地文件缓存
            Dim CachePath As String = PathTemp & "Cache\LocalMod.json"
            Dim Cache As New JObject
            Try
                Dim CacheContent As String = ReadFile(CachePath)
                If Not String.IsNullOrWhiteSpace(CacheContent) Then
                    Cache = GetJson(CacheContent)
                    If Not Cache.ContainsKey("version") OrElse Cache("version").ToObject(Of Integer) <> LocalModCacheVersion Then
                        Log($"[Mod] 本地 Mod 信息缓存版本已过期，将弃用这些缓存信息", LogLevel.Debug)
                        Cache = New JObject
                    End If
                End If
            Catch ex As Exception
                Log(ex, "读取本地 Mod 信息缓存失败，已重置")
                Cache = New JObject
            End Try
            Cache("version") = LocalModCacheVersion

            '加载 Mod 列表
            Dim ModList As New List(Of McMod)
            For Each ModFile As FileInfo In ModFileList
                If Loader.IsAborted Then Return
                Dim ModEntry As New McMod(ModFile.FullName)
                Dim DumpMod As McMod = ModList.FirstOrDefault(Function(m) m.RawFileName = ModEntry.RawFileName) '存在两个文件，名称相同，但一个启用一个禁用
                If DumpMod IsNot Nothing Then
                    Dim DisabledMod As McMod = If(DumpMod.State = McMod.McModState.Disabled, DumpMod, ModEntry)
                    Log($"[Mod] 重复的 Mod 文件：{DumpMod.FileName} 与 {ModEntry.FileName}，已忽略 {DisabledMod.FileName}", LogLevel.Debug)
                    If DisabledMod Is ModEntry Then
                        Continue For
                    Else
                        ModList.Remove(DisabledMod)
                    End If
                End If
                ModList.Add(ModEntry)
            Next
            Log($"[Mod] 共发现 {ModList.Count} 个 Mod")

            '排序
            ModList = ModList.OrderBy(Function(m) m.FileName).ToList

            '回设
            If Loader.IsAborted Then Return
            Loader.Output = ModList

            '开始联网加载
            'TODO: 添加信息获取中提示
            McModDetailLoader.Start(New KeyValuePair(Of List(Of McMod), JObject)(ModList, Cache), IsForceRestart:=True)

        Catch ex As Exception
            Log(ex, "Mod 列表加载失败", LogLevel.Debug)
            Throw
        End Try
    End Sub
    '联网加载 Mod 详情
    Public McModDetailLoader As New LoaderTask(Of KeyValuePair(Of List(Of McMod), JObject), Integer)("Mod List Detail Loader", AddressOf McModDetailLoad)
    Private Sub McModDetailLoad(Loader As LoaderTask(Of KeyValuePair(Of List(Of McMod), JObject), Integer))
        Dim Mods As New List(Of McMod)
        Dim Cache As JObject = Loader.Input.Value
        '读取 Comp 缓存，获取需要更新的 Mod 列表
        For Each ModEntry As McMod In Loader.Input.Key
            If Loader.IsAborted Then Return
            Dim CacheKey = ModEntry.ModrinthHash & PageInstanceLeft.Instance.Version.VanillaName & GetTargetModLoaders().Join("")
            If Cache.ContainsKey(CacheKey) Then
                ModEntry.FromJson(Cache(CacheKey))
                '如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                If ModEntry.CompLoaded AndAlso Date.Now - Cache(CacheKey)("Comp")("CacheTime").ToObject(Of Date) < New TimeSpan(6, 0, 0) Then Continue For
            End If
            Mods.Add(ModEntry)
        Next
        Log($"[Mod] 有 {Mods.Where(Function(m) m.Comp Is Nothing).Count} 个 Mod 需要联网获取信息，{Mods.Where(Function(m) m.Comp IsNot Nothing).Count} 个 Mod 需要更新信息")
        If Not Mods.Any Then
            Loader.Input.Key.Where(Function(m) m.Version Is Nothing).ForEach(Sub(m) m.LoadMetadataFromJar())  '从 JAR 中获取缺失的版本信息（下面有另一个分支）
            Return
        End If
        '获取作为检查目标的加载器和版本
        '此处不应向下扩展检查的 MC 小版本，例如 Mod 在更新 1.16.5 后，对早期的 1.16.2 版本发布了修补补丁，这会导致 PCL 将 1.16.5 版本的 Mod 降级到 1.16.2
        Dim TargetMcVersion As McVersion = PageInstanceLeft.Instance.Version
        Dim ModLoaders = GetTargetModLoaders()
        Dim VanillaVersion = TargetMcVersion.VanillaName
        '开始网络获取
        Log($"[Mod] 目标加载器：{ModLoaders.Join("/")}，版本：{VanillaVersion}")
        Dim EndedThreadCount As Integer = 0, IsFailed As Boolean = False
        Dim CurrentTaskThread As Thread = Thread.CurrentThread
        '从 Modrinth 获取信息
        RunInNewThread(
        Sub()
            Try
                '步骤 1：获取 Hash 与对应的工程 ID
                Dim ModrinthHashes = Mods.Select(Function(m) m.ModrinthHash).ToList()
                Dim ModrinthVersion As JObject = DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                    $"{{""hashes"": [""{ModrinthHashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")
                Log($"[Mod] 从 Modrinth 获取到 {ModrinthVersion.Count} 个本地 Mod 的对应信息")
                '步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                If ModrinthVersion.Count = 0 Then Return
                Dim ModrinthMapping As New Dictionary(Of String, List(Of McMod))
                For Each Entry In Mods
                    If Not ModrinthVersion.ContainsKey(Entry.ModrinthHash) Then Continue For
                    If ModrinthVersion(Entry.ModrinthHash)("files")(0)("hashes")("sha1") <> Entry.ModrinthHash Then Continue For
                    Dim ProjectId = ModrinthVersion(Entry.ModrinthHash)("project_id").ToString
                    If CompProjectCache.ContainsKey(ProjectId) AndAlso Entry.Comp Is Nothing Then Entry.Comp = CompProjectCache(ProjectId) '读取已加载的缓存，加快结果出现速度
                    If Not ModrinthMapping.ContainsKey(ProjectId) Then ModrinthMapping(ProjectId) = New List(Of McMod)
                    ModrinthMapping(ProjectId).Add(Entry)
                    '记录对应的 CompFile
                    Dim File As New CompFile(ModrinthVersion(Entry.ModrinthHash), CompType.Mod)
                    If Entry.CompFile Is Nothing OrElse Entry.CompFile.ReleaseDate < File.ReleaseDate Then
                        Entry.CompFile = File
                    Else
                        Entry.CompFile.Version = File.Version '使用来自 Modrinth 的版本号
                    End If
                Next
                If Loader.IsAbortedWithThread(CurrentTaskThread) Then Return
                Log($"[Mod] 需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息")
                '步骤 3：获取工程信息
                If Not ModrinthMapping.Any() Then Return
                Dim ModrinthProject As JArray = DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthMapping.Keys.Join(""",""")}""]")
                For Each ProjectJson In ModrinthProject
                    Dim Project As New CompProject(ProjectJson)
                    For Each Entry In ModrinthMapping(Project.Id)
                        Entry.Comp = Project
                    Next
                Next
                Log($"[Mod] 已从 Modrinth 获取本地 Mod 信息，继续获取更新信息")
                '步骤 4：获取更新信息
                Dim ModrinthUpdate As JObject = DlModRequest("https://api.modrinth.com/v2/version_files/update", HttpMethod.Post,
                    $"{{""hashes"": [""{ModrinthMapping.SelectMany(Function(l) l.Value.Select(Function(m) m.ModrinthHash)).Join(""",""")}""], ""algorithm"": ""sha1"", 
                    ""loaders"": [""{ModLoaders.Join(""",""").ToLower}""],""game_versions"": [""{VanillaVersion}""]}}", "application/json")
                For Each Entry In Mods
                    If Not ModrinthUpdate.ContainsKey(Entry.ModrinthHash) OrElse Entry.CompFile Is Nothing Then Continue For
                    Dim UpdateFile As New CompFile(ModrinthUpdate(Entry.ModrinthHash), CompType.Mod)
                    If Not UpdateFile.Available Then Continue For
                    If ModeDebug Then Log($"[Mod] 本地文件 {Entry.CompFile.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}")
                    If Entry.CompFile.ReleaseDate >= UpdateFile.ReleaseDate OrElse Entry.CompFile.Hash = UpdateFile.Hash Then Continue For
                    '设置更新日志与更新文件
                    If Entry.UpdateFile IsNot Nothing AndAlso UpdateFile.Hash = Entry.UpdateFile.Hash Then '合并
                        Entry.ChangelogUrls.Add($"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={VanillaVersion}")
                        UpdateFile.DownloadUrls.AddRange(Entry.UpdateFile.DownloadUrls) '合并下载源
                        Entry.UpdateFile = UpdateFile '优先使用 Modrinth 的文件
                    ElseIf Entry.UpdateFile Is Nothing OrElse UpdateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate Then '替换
                        Entry.ChangelogUrls = New List(Of String) From {$"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={VanillaVersion}"}
                        Entry.UpdateFile = UpdateFile
                    End If
                Next
                Log($"[Mod] 从 Modrinth 获取本地 Mod 信息结束")
            Catch ex As Exception
                Log(ex, "从 Modrinth 获取本地 Mod 信息失败")
                IsFailed = True
            Finally
                EndedThreadCount += 1
            End Try
        End Sub, "Mod List Detail Loader Modrinth")
        '从 CurseForge 获取信息
        RunInNewThread(
        Sub()
            Try
                '步骤 1：获取 Hash 与对应的工程 ID
                Dim CurseForgeHashes As New List(Of UInteger)
                For Each Entry In Mods
                    CurseForgeHashes.Add(Entry.CurseForgeHash)
                    If Loader.IsAbortedWithThread(CurrentTaskThread) Then Return
                Next
                Dim CurseForgeRaw As JContainer = DlModRequest("https://api.curseforge.com/v1/fingerprints/432", HttpMethod.Post,
                    $"{{""fingerprints"": [{CurseForgeHashes.Join(",")}]}}", "application/json")("data")("exactMatches")
                Log($"[Mod] 从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地 Mod 的对应信息")
                '步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                If Not CurseForgeRaw.Any() Then Return
                Dim CurseForgeMapping As New Dictionary(Of Integer, List(Of McMod))
                For Each Project In CurseForgeRaw
                    Dim ProjectId = Project("id").ToString
                    Dim Hash As UInteger = Project("file")("fileFingerprint")
                    For Each Entry In Mods
                        If Entry.CurseForgeHash <> Hash Then Continue For
                        If CompProjectCache.ContainsKey(ProjectId) AndAlso Entry.Comp Is Nothing Then Entry.Comp = CompProjectCache(ProjectId) '读取已加载的缓存，加快结果出现速度
                        If Not CurseForgeMapping.ContainsKey(ProjectId) Then CurseForgeMapping(ProjectId) = New List(Of McMod)
                        CurseForgeMapping(ProjectId).Add(Entry)
                        '记录对应的 CompFile
                        Dim File As New CompFile(Project("file"), CompType.Mod)
                        If Entry.CompFile Is Nothing OrElse Entry.CompFile.ReleaseDate < File.ReleaseDate Then Entry.CompFile = File
                    Next
                Next
                If Loader.IsAbortedWithThread(CurrentTaskThread) Then Return
                Log($"[Mod] 需要从 CurseForge 获取 {CurseForgeMapping.Count} 个本地 Mod 的工程信息")
                '步骤 3：获取工程信息
                If Not CurseForgeMapping.Any() Then Return
                Dim CurseForgeProject = DlModRequest("https://api.curseforge.com/v1/mods", HttpMethod.Post,
                    $"{{""modIds"": [{CurseForgeMapping.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFileIds As New Dictionary(Of Integer, List(Of McMod)) 'FileId -> 本地 Mod 文件列表
                Dim FileIdToProjectSlug As New Dictionary(Of Integer, String)
                For Each ProjectJson In CurseForgeProject
                    If ProjectJson("isAvailable") IsNot Nothing AndAlso Not ProjectJson("isAvailable").ToObject(Of Boolean) Then Continue For
                    '设置 Entry 中的工程信息
                    Dim Project As New CompProject(ProjectJson)
                    For Each Entry In CurseForgeMapping(Project.Id) '倒查防止 CurseForge 返回的内容有漏
                        If Entry.Comp IsNot Nothing AndAlso Not Entry.Comp.FromCurseForge Then
                            Entry.Comp = Entry.Comp '再次触发修改事件
                            Continue For
                        End If
                        Entry.Comp = Project
                    Next
                    '查找或许版本更新的文件列表
                    If ModLoaders.Count = 1 Then
                        Dim NewestVersion As String = Nothing
                        Dim NewestFileIds As New List(Of Integer)
                        For Each IndexEntry In ProjectJson("latestFilesIndexes")
                            If IndexEntry("modLoader") Is Nothing OrElse ModLoaders.Single <> IndexEntry("modLoader").ToObject(Of Integer) Then Continue For 'ModLoader 唯一且匹配
                            Dim IndexVersion As String = IndexEntry("gameVersion")
                            If IndexVersion <> VanillaVersion Then Continue For 'MC 版本匹配
                            '由于 latestFilesIndexes 是按时间从新到老排序的，所以只需取第一个；如果需要检查多个 releaseType 下的文件，将 > -1 改为 = 1，但这应当并不会获取到更新的文件
                            If NewestVersion IsNot Nothing AndAlso CompareVersion(NewestVersion, IndexVersion) > -1 Then Continue For '只保留最新 MC 版本
                            If NewestVersion <> IndexVersion Then
                                NewestVersion = IndexVersion
                                NewestFileIds.Clear()
                            End If
                            NewestFileIds.Add(IndexEntry("fileId").ToObject(Of Integer))
                        Next
                        For Each FileId In NewestFileIds
                            If Not UpdateFileIds.ContainsKey(FileId) Then UpdateFileIds(FileId) = New List(Of McMod)
                            UpdateFileIds(FileId).AddRange(CurseForgeMapping(Project.Id))
                            FileIdToProjectSlug(FileId) = Project.Slug
                        Next
                    End If
                Next
                Log($"[Mod] 已从 CurseForge 获取本地 Mod 信息，需要获取 {UpdateFileIds.Count} 个用于检查更新的文件信息")
                '步骤 4：获取更新文件信息
                If Not UpdateFileIds.Any() Then Return
                Dim CurseForgeFiles = DlModRequest("https://api.curseforge.com/v1/mods/files", HttpMethod.Post,
                                    $"{{""fileIds"": [{UpdateFileIds.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFiles As New Dictionary(Of McMod, CompFile)
                For Each FileJson In CurseForgeFiles
                    Dim File As New CompFile(FileJson, CompType.Mod)
                    If Not File.Available Then Continue For
                    For Each Entry As McMod In UpdateFileIds(File.Id)
                        If UpdateFiles.ContainsKey(Entry) AndAlso UpdateFiles(Entry).ReleaseDate >= File.ReleaseDate Then Continue For
                        UpdateFiles(Entry) = File
                    Next
                Next
                For Each Pair In UpdateFiles
                    Dim Entry As McMod = Pair.Key
                    Dim UpdateFile As CompFile = Pair.Value
                    If ModeDebug Then Log($"[Mod] 本地文件 {Entry.CompFile.FileName} 在 CurseForge 上的最新版为 {UpdateFile.FileName}")
                    If Entry.CompFile.ReleaseDate >= UpdateFile.ReleaseDate OrElse Entry.CompFile.Hash = UpdateFile.Hash Then Continue For
                    '设置更新日志与更新文件
                    If Entry.UpdateFile IsNot Nothing AndAlso UpdateFile.Hash = Entry.UpdateFile.Hash Then '合并
                        Entry.ChangelogUrls.Add($"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug(UpdateFile.Id)}/files/{UpdateFile.Id}")
                        Entry.UpdateFile.DownloadUrls.AddRange(UpdateFile.DownloadUrls) '合并下载源
                    ElseIf Entry.UpdateFile Is Nothing OrElse UpdateFile.ReleaseDate > Entry.UpdateFile.ReleaseDate Then '替换
                        Entry.ChangelogUrls = New List(Of String) From {$"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug(UpdateFile.Id)}/files/{UpdateFile.Id}"}
                        Entry.UpdateFile = UpdateFile
                    End If
                Next
                Log($"[Mod] 从 CurseForge 获取 Mod 更新信息结束")
            Catch ex As Exception
                Log(ex, "从 CurseForge 获取本地 Mod 信息失败")
                IsFailed = True
            Finally
                EndedThreadCount += 1
            End Try
        End Sub, "Mod List Detail Loader CurseForge")
        '从 JAR 中获取缺失的版本信息（上面有另一个分支）
        Loader.Input.Key.Where(Function(m) m.Version Is Nothing).ForEach(Sub(m) m.LoadMetadataFromJar())
        '等待线程结束
        Do Until EndedThreadCount = 2
            Thread.Sleep(10)
            If Loader.IsAborted Then Return
        Loop
        '保存缓存
        Mods = Mods.Where(Function(m) m.Comp IsNot Nothing).ToList()
        Log($"[Mod] 联网获取本地 Mod 信息完成，为 {Mods.Count} 个 Mod 更新缓存")
        If Not Mods.Any() Then Return
        For Each Entry In Mods
            Entry.CompLoaded = Not IsFailed
            Cache(Entry.ModrinthHash & VanillaVersion & ModLoaders.Join("")) = Entry.ToJson()
        Next
        WriteFile(PathTemp & "Cache\LocalMod.json", Cache.ToString(If(ModeDebug, Newtonsoft.Json.Formatting.Indented, Newtonsoft.Json.Formatting.None)))
        '刷新边栏
        If Loader.IsAborted Then Return
        If FrmInstanceMod?.Filter = PageInstanceMod.FilterType.CanUpdate Then
            RunInUi(Sub() FrmInstanceMod?.RefreshUI()) '同步 “可更新” 列表 (#4677)
        Else
            RunInUi(Sub() FrmInstanceMod?.RefreshBars())
        End If
    End Sub
    Public Function GetTargetModLoaders() As List(Of CompModLoaderType)
        Dim ModLoaders As New List(Of CompModLoaderType)
        If PageInstanceLeft.Instance.Version.HasForge Then ModLoaders.Add(CompModLoaderType.Forge)
        If PageInstanceLeft.Instance.Version.HasNeoForge Then ModLoaders.Add(CompModLoaderType.NeoForge)
        If PageInstanceLeft.Instance.Version.HasFabric Then ModLoaders.Add(CompModLoaderType.Fabric)
        If PageInstanceLeft.Instance.Version.HasLiteLoader Then ModLoaders.Add(CompModLoaderType.LiteLoader)
        If Not ModLoaders.Any() Then ModLoaders.AddRange({CompModLoaderType.Forge, CompModLoaderType.NeoForge, CompModLoaderType.Fabric, CompModLoaderType.LiteLoader, CompModLoaderType.Quilt})
        Return ModLoaders
    End Function

End Module