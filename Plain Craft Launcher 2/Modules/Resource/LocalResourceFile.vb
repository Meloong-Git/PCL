''' <summary>
''' 本地的社区资源文件。
''' </summary>
Public Class LocalResourceFile

#Region "文件信息"

    ''' <summary>
    ''' 该文件的文件信息。
    ''' </summary>
    Public ReadOnly File As FileInfo
    Public Sub New(FileInfo As FileInfo)
        Me.File = FileInfo
    End Sub
    Public Sub New(FullName As String)
        Me.New(FileUtils.GetInfo(FullName))
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
    Public Event OnUpdate(sender As LocalResourceFile)

    ''' <summary>
    ''' 可读名称。
    ''' 可能为无扩展的文件名，但不会是 Nothing。
    ''' </summary>
    Public Property Display As String
        Get
            If _Display Is Nothing Then _Display = ProjectVersion?.Display
            If _Display Is Nothing Then _Display = PathUtils.GetFileNameWithoutExtension(File.FullName)
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
            If _Version IsNot Nothing AndAlso _Version.RegexCheck("[0-9.\-]+") Then Return
            If value IsNot Nothing AndAlso value.ContainsIgnoreCase("version") Then value = "version" '需要修改的标识
            _Version = value
        End Set
    End Property
    Public _Version As String = Nothing

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
            Return Not Settings.Get(Of Boolean)("UiHiddenFunctionModUpdate") AndAlso Not Settings.Get(Of Boolean)("VersionAdvanceDisableModUpdate", Instance:=PageInstanceLeft.Instance) AndAlso UpdateFile IsNot Nothing
        End Get
    End Property

    ''' <summary>
    ''' 获取用于 CurseForge 信息获取的 Hash 值（MurmurHash2）。
    ''' </summary>
    Public ReadOnly Property CurseForgeHash As UInteger
        Get
            If _CurseForgeHash Is Nothing Then
                '读取缓存
                Dim CacheKey As String = $"{EnabledFullName}-{File.LastWriteTime.ToLongTimeString}-{File.Length}-C".GetStableHashCode()
                Dim Cached As String = ReadIni(PathTemp & "Cache\ModHash.ini", CacheKey)
                If Cached <> "" AndAlso Cached.RegexCheck("^\d+$") Then '#5062
                    _CurseForgeHash = Cached
                    Return _CurseForgeHash
                End If
                '读取文件
                Dim data As New List(Of Byte)
                For Each b As Byte In FileUtils.ReadAsBytes(File.FullName)
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
                Dim CacheKey As String = $"{EnabledFullName}-{File.LastWriteTime.ToLongTimeString}-{File.Length}-M".GetStableHashCode()
                Dim Cached As String = ReadIni(PathTemp & "Cache\ModHash.ini", CacheKey)
                If Cached <> "" Then
                    _ModrinthHash = Cached
                    Return _ModrinthHash
                End If
                '计算 SHA1
                _ModrinthHash = CryptographyUtils.ComputeFileHash(File.FullName, CryptographyUtils.HashMethod.Sha1)
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
        Dim Target = TryCast(obj, LocalResourceFile)
        Return Target IsNot Nothing AndAlso File.FullName = Target.File.FullName
    End Function

#End Region

End Class
