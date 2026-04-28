''' <summary>
''' 本地的社区资源文件。
''' </summary>
Public Class ResourceFile

#Region "文件信息"

    ''' <summary>
    ''' 该文件的文件信息。
    ''' </summary>
    Public ReadOnly File As FileInfo
    Public Sub New(FileInfo As FileInfo)
        Me.File = FileInfo
    End Sub
    Public Sub New(FullName As String)
        Me.New(New FileInfo(FullName))
    End Sub

    ''' <summary>
    ''' 该文件是否没有被禁用。
    ''' </summary>
    Public ReadOnly Property IsEnabled As Boolean
        Get
            Return {".jar", ".zip", ".litemod"}.Contains(File.Extension.Lower)
        End Get
    End Property
    ''' <summary>
    ''' 假若该资源未被禁用时，文件的文件名。
    ''' </summary>
    Public ReadOnly Property EnabledName As String
        Get
            Return File.Name.Replace(".disabled", "").Replace(".old", "")
        End Get
    End Property
    ''' <summary>
    ''' 假若该资源未被禁用时，文件的完整路径。
    ''' </summary>
    Public ReadOnly Property EnabledFullName As String
        Get
            Return $"{File.DirectoryName}\{EnabledName}"
        End Get
    End Property

#End Region

#Region "信息项"

    ''' <summary>
    ''' 在初始化结束之后，任何信息被后续更新时触发此事件。
    ''' </summary>
    Public Event OnUpdate(sender As ResourceFile)

    ''' <summary>
    ''' 可读名称。
    ''' 可能为无扩展的文件名，但不会是 Nothing。
    ''' </summary>
    Public Property Display As String
        Get
            If _Display Is Nothing Then _Display = ProjectVersion?.Display
            If _Display Is Nothing Then _Display = Path.GetFileNameWithoutExtension(File.FullName)
            Return _Display
        End Get
        Set(value As String)
            If _Display Is Nothing AndAlso value IsNot Nothing AndAlso Not value.Contains("modname") AndAlso
               value.Lower <> "name" AndAlso value.Count > 1 AndAlso Val(value).ToString <> value Then
                _Display = value
            End If
        End Set
    End Property
    Private _Display As String = Nothing

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
                If _Description.Lower.LastIndexOfAny("qwertyuiopasdfghjklzxcvbnm0123456789") = _Description.Count - 1 Then _Description += "."
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
            If _Version Is Nothing Then _Version = ProjectVersion?.Version
            Return _Version
        End Get
        Set(value As String)
            If _Version IsNot Nothing AndAlso RegexCheck(_Version, "[0-9.\-]+") Then Return
            If value IsNot Nothing AndAlso value.ContainsIgnoreCase("version") Then value = "version" '需要修改的标识
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
            '基础可用性检查
            If File.FullName.Length < 2 Then Throw New FileNotFoundException("错误的 Mod 文件路径（" & If(File.FullName, "null") & "）")
            If Not IO.File.Exists(File.FullName) Then Throw New FileNotFoundException("未找到 Mod 文件（" & File.FullName & "）")
            '打开 Jar 文件
            Jar = New ZipArchive(New FileStream(File.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            '信息获取
            LoadMetadataFromJar(Jar)
        Catch ex As UnauthorizedAccessException
            Log(ex, $"Mod 文件由于无权限无法打开（{File.FullName}）", NotifyLevel.DevelopOnly)
        Catch ex As Exception
            Log(ex, $"Mod 文件无法打开（{File.FullName}）", NotifyLevel.DevelopOnly)
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
            Display = InfoObject("name")
            Description = InfoObject("description")
            Version = InfoObject("version")
        Catch ex As Exception
            Log(ex, $"读取 mcmod.info 时出现未知错误（{File.FullName}）", NotifyLevel.DevelopOnly)
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
            If FabricObject.ContainsKey("name") Then Display = FabricObject("name")
            If FabricObject.ContainsKey("version") Then Version = FabricObject("version")
            If FabricObject.ContainsKey("description") Then Description = FabricObject("description")
            '加载成功
            GoTo Finished
        Catch ex As Exception
            Log(ex, "读取 fabric.mod.json 时出现未知错误（" & File.FullName & "）", NotifyLevel.DevelopOnly)
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
            For Each Line In TomlText.ReplaceLineEndings(vbLf).Split(vbLf) '统一换行符
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
                        Value = ValueLines.Join(vbLf).Trim(vbLf).ReplaceLineEndings(vbCrLf)
                    ElseIf RawValue.Lower = "true" OrElse RawValue.Lower = "false" Then
                        '布尔型
                        Value = (RawValue.Lower = "true")
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
            If ModEntry.ContainsKey("displayName") Then Display = ModEntry("displayName")
            If ModEntry.ContainsKey("description") Then Description = ModEntry("description")
            If ModEntry.ContainsKey("version") Then Version = ModEntry("version")
            '加载成功
            GoTo Finished
        Catch ex As Exception
            Log(ex, $"读取 mods.toml 时出现未知错误（{File.FullName}）", NotifyLevel.DevelopOnly)
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
            If FmlObject.ContainsKey("name") Then Display = FmlObject("name")("value")
            If FmlObject.ContainsKey("version") Then Version = FmlObject("version")("value")
            '加载成功
            GoTo Finished
        Catch ex As Exception
            Log(ex, $"读取 fml_cache_annotation.json 时出现未知错误（{File.FullName}）", NotifyLevel.DevelopOnly)
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
                Log($"获取 META-INF 中的版本信息失败（{File.FullName}）", NotifyLevel.DevelopOnly)
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
    Public Property Project As ResourceProject
        Get
            Return _Project
        End Get
        Set(value As ResourceProject)
            _Project = value
            RaiseEvent OnUpdate(Me)
        End Set
    End Property
    Private _Project As ResourceProject

    ''' <summary>
    ''' 本地文件对应的联网文件信息。
    ''' </summary>
    Public ProjectVersion As ResourceVersion

    ''' <summary>
    ''' 该 Mod 对应的联网最新版本。
    ''' </summary>
    Public Property UpdateFile As ResourceVersion
        Get
            Return _UpdateFile
        End Get
        Set(value As ResourceVersion)
            _UpdateFile = value
            RaiseEvent OnUpdate(Me)
        End Set
    End Property
    Private _UpdateFile As ResourceVersion

    ''' <summary>
    ''' 该 Mod 的更新日志网址。
    ''' </summary>
    Public ChangelogUrls As New List(Of String)
    ''' <summary>
    ''' 所有网络信息是否已成功加载。
    ''' </summary>
    Public OnlineDataLoaded As Boolean = False

    ''' <summary>
    ''' 将网络信息保存为 JSON。
    ''' </summary>
    Public Function ToJson() As JObject
        Dim Json As New JObject
        If Project IsNot Nothing Then Json.Add("Project", Project.ToJson())
        Json.Add("ChangelogUrls", New JArray(ChangelogUrls))
        Json.Add("OnlineDataLoaded", OnlineDataLoaded)
        If ProjectVersion IsNot Nothing Then Json.Add("Version", ProjectVersion.ToCacheJson())
        If UpdateFile IsNot Nothing Then Json.Add("UpdateFile", UpdateFile.ToCacheJson())
        Return Json
    End Function
    ''' <summary>
    ''' 从 JSON 中读取网络信息。
    ''' </summary>
    Public Sub FromJson(Json As JObject)
        OnlineDataLoaded = Json("OnlineDataLoaded")
        If Json.ContainsKey("Project") Then Project = New ResourceProject(Json("Project"))
        If Json.ContainsKey("ChangelogUrls") Then ChangelogUrls = Json("ChangelogUrls").ToObject(Of List(Of String))
        If Json.ContainsKey("Version") Then ProjectVersion = ResourceVersion.FromCacheJson(Json("Version"))
        If Json.ContainsKey("UpdateFile") Then UpdateFile = ResourceVersion.FromCacheJson(Json("UpdateFile"))
    End Sub

    ''' <summary>
    ''' 该文件是否可以更新。
    ''' </summary>
    Public ReadOnly Property HasUpdate As Boolean
        Get
            Return Not Settings.Get("UiHiddenFunctionModUpdate") AndAlso Not Settings.Get("VersionAdvanceDisableModUpdate", Instance:=PageInstanceLeft.Instance) AndAlso UpdateFile IsNot Nothing
        End Get
    End Property

    ''' <summary>
    ''' 获取用于 CurseForge 信息获取的 Hash 值（MurmurHash2）。
    ''' </summary>
    Public ReadOnly Property CurseForgeHash As UInteger
        Get
            If _CurseForgeHash Is Nothing Then
                '读取缓存
                Dim CacheKey As String = GetHash($"{EnabledFullName}-{File.LastWriteTime.ToLongTimeString}-{File.Length}-C")
                Dim Cached As String = ReadIni(PathTemp & "Cache\ModHash.ini", CacheKey)
                If Cached <> "" AndAlso RegexCheck(Cached, "^\d+$") Then '#5062
                    _CurseForgeHash = Cached
                    Return _CurseForgeHash
                End If
                '读取文件
                Dim data As New List(Of Byte)
                For Each b As Byte In ReadFileBytes(File.FullName)
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
                Dim CacheKey As String = GetHash($"{EnabledFullName}-{File.LastWriteTime.ToLongTimeString}-{File.Length}-M")
                Dim Cached As String = ReadIni(PathTemp & "Cache\ModHash.ini", CacheKey)
                If Cached <> "" Then
                    _ModrinthHash = Cached
                    Return _ModrinthHash
                End If
                '计算 SHA1
                _ModrinthHash = GetFileSHA1(File.FullName)
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
        Return File.FullName
    End Function
    Public Overrides Function Equals(obj As Object) As Boolean
        Dim Target = TryCast(obj, ResourceFile)
        Return Target IsNot Nothing AndAlso File.FullName = Target.File.FullName
    End Function

#End Region

End Class
