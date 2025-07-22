Imports System.IO.Compression

Public Module ModMod
    Private Const LocalModCacheVersion As Integer = 7

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
                Load()
                If Not IsFileAvailable Then
                    Return McModState.Unavailable
                ElseIf Path.EndsWithF(".disabled", True) OrElse Path.EndsWithF(".old", True) Then
                    Return McModState.Disabled
                Else
                    Return McModState.Fine
                End If
            End Get
        End Property
        Public Enum McModState As Integer
            Fine = 0
            Disabled = 1
            Unavailable = 2
        End Enum

#End Region

#Region "信息项"

        ''' <summary>
        ''' Mod 的名称。若不可用则为 ModID 或无扩展的文件名。
        ''' </summary>
        Public Property Name As String
            Get
                If _Name Is Nothing Then Load()
                If _Name Is Nothing Then _Name = _ModId
                If _Name Is Nothing Then _Name = GetFileNameWithoutExtentionFromPath(Path)
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
        ''' </summary>
        Public Property Description As String
            Get
                If _Description Is Nothing Then Load()
                If _Description Is Nothing AndAlso FileUnavailableReason IsNot Nothing Then _Description = FileUnavailableReason.Message
                'If _Description Is Nothing Then _Description = Path
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
        ''' Mod 的版本，不保证符合版本格式规范。
        ''' </summary>
        Public Property Version As String
            Get
                If _Version Is Nothing Then Load()
                Return _Version
            End Get
            Set(value As String)
                If _Version IsNot Nothing AndAlso RegexCheck(_Version, "[0-9.\-]+") Then Return
                If value?.ContainsF("version", True) Then value = "version" '需要修改的标识
                _Version = value
            End Set
        End Property
        Public _Version As String = Nothing

        ''' <summary>
        ''' 用于依赖检查的 ModID。
        ''' </summary>
        Public Property ModId As String
            Get
                If _ModId Is Nothing Then Load()
                Return _ModId
            End Get
            Set(value As String)
                If value Is Nothing Then Return
                value = RegexSeek(value, "[0-9a-zA-Z_-]+")
                If value Is Nothing OrElse value.Count <= 1 OrElse Val(value).ToString = value Then Return
                If value.ContainsF("name", True) OrElse value.ContainsF("modid", True) Then Return
                If Not PossibleModId.Contains(value) Then PossibleModId.Add(value)
                If _ModId Is Nothing Then _ModId = value
            End Set
        End Property
        Private _ModId As String = Nothing
        ''' <summary>
        ''' 其他可能的 ModID。
        ''' </summary>
        Public PossibleModId As New List(Of String)

        ''' <summary>
        ''' Mod 的主页。
        ''' </summary>
        Public Property Url As String
            Get
                If _Url Is Nothing Then Load()
                Return _Url
            End Get
            Set(value As String)
                If _Url Is Nothing AndAlso value IsNot Nothing AndAlso value.StartsWithF("http") Then
                    _Url = value
                End If
            End Set
        End Property
        Private _Url As String = Nothing

        ''' <summary>
        ''' Mod 的作者列表。
        ''' </summary>
        Public Property Authors As String
            Get
                If _Authors Is Nothing Then Load()
                Return _Authors
            End Get
            Set(value As String)
                If _Authors Is Nothing AndAlso Not String.IsNullOrWhiteSpace(value) Then
                    _Authors = value
                End If
            End Set
        End Property
        Private _Authors As String = Nothing

        ''' <summary>
        ''' 依赖项，其中包括了 Minecraft 的版本要求。格式为 ModID - VersionRequirement，若无版本要求则为 Nothing。
        ''' </summary>
        Public ReadOnly Property Dependencies As Dictionary(Of String, String)
            Get
                Load()
                Return _Dependencies
            End Get
        End Property
        Private _Dependencies As New Dictionary(Of String, String)
        Private Sub AddDependency(ModID As String, Optional VersionRequirement As String = Nothing)
            '确保信息正确
            If ModID Is Nothing OrElse ModID.Count < 2 Then Return
            ModID = ModID.ToLower
            If ModID = "name" OrElse Val(ModID).ToString = ModID Then Return '跳过 name 与纯数字 id
            If VersionRequirement Is Nothing OrElse ((Not VersionRequirement.Contains(".")) AndAlso (Not VersionRequirement.Contains("-"))) OrElse VersionRequirement.Contains("$") Then
                VersionRequirement = Nothing
            Else
                If (Not VersionRequirement.StartsWithF("[")) AndAlso (Not VersionRequirement.StartsWithF("(")) AndAlso (Not VersionRequirement.EndsWithF("]")) AndAlso (Not VersionRequirement.EndsWithF(")")) Then VersionRequirement = "[" & VersionRequirement & ",)"
            End If
            '向依赖项中添加
            If _Dependencies.ContainsKey(ModID) Then
                If _Dependencies(ModID) Is Nothing Then _Dependencies(ModID) = VersionRequirement
            Else
                _Dependencies.Add(ModID, VersionRequirement)
            End If
        End Sub

#End Region

#Region "加载步骤标记"

        '1. 进行文件可用性检查
        '   成功：继续第二步。
        '   失败：标记 FileUnavailableReason， 并停止后续加载。
        ''' <summary>
        ''' 是否已进行 Mod 文件的基础加载。（这包括第一步和第二步）
        ''' </summary>
        Private IsLoaded As Boolean = False
        ''' <summary>
        ''' Mod 文件是否可被正常读取。
        ''' </summary>
        Public ReadOnly Property IsFileAvailable As Boolean
            Get
                Load()
                Return FileUnavailableReason Is Nothing
            End Get
        End Property
        ''' <summary>
        ''' Mod 文件出错的原因。若无错误，则为 Nothing。
        ''' </summary>
        Public ReadOnly Property FileUnavailableReason As Exception
            Get
                Load()
                Return _FileUnavailableReason
            End Get
        End Property
        Private _FileUnavailableReason As Exception = Nothing

        '2. 进行 .class 以外的信息获取
        '   成功：标记 IsInfoWithoutClassAvailable。
        '   失败：什么也不干。如果需要补充信息的话，检测到 IsInfoWithoutClassAvailable 为 False，会自动继续加载。
        ''' <summary>
        ''' 是否已在不获取 .class 文件的前提下完成了所需信息的加载。
        ''' </summary>
        Private IsInfoWithoutClassAvailable As Boolean = False

        '3. 尝试从 .class 文件中获取信息
        '   成功：标记 IsInfoWithClassAvailable。
        '   失败：什么也不干。
        ''' <summary>
        ''' 是否已进行 .class 文件的信息获取。
        ''' </summary>
        Private IsInfoWithClassLoaded As Boolean = False
        ''' <summary>
        ''' 是否已在 .class 文件中完成了所需信息的加载。
        ''' </summary>
        Private IsInfoWithClassAvailable As Boolean = False

#End Region

#Region "加载"

        ''' <summary>
        ''' 初始化所有数据。
        ''' </summary>
        Private Sub Init()
            _Name = Nothing
            _Description = Nothing
            _Version = Nothing
            _ModId = Nothing
            PossibleModId = New List(Of String)
            _Dependencies = New Dictionary(Of String, String)
            IsLoaded = False
            _FileUnavailableReason = Nothing
            IsInfoWithClassLoaded = False
            IsInfoWithClassAvailable = False
        End Sub

        ''' <summary>
        ''' 进行文件可用性检查与 .class 以外的信息获取。
        ''' </summary>
        Public Sub Load(Optional ForceReload As Boolean = False)
            If IsLoaded AndAlso Not ForceReload Then Return
            '初始化
            Init()
            Dim Jar As ZipArchive = Nothing
            Try
                '基础可用性检查、打开 Jar 文件
                If Path.Length < 2 Then Throw New FileNotFoundException("错误的 Mod 文件路径（" & If(Path, "null") & "）")
                If Not File.Exists(Path) Then Throw New FileNotFoundException("未找到 Mod 文件（" & Path & "）")
                Jar = New ZipArchive(New FileStream(Path, FileMode.Open))
                '信息获取
                LookupMetadata(Jar)
            Catch ex As UnauthorizedAccessException
                Log(ex, "Mod 文件由于无权限无法打开（" & Path & "）", LogLevel.Developer)
                _FileUnavailableReason = New UnauthorizedAccessException("没有读取此文件的权限，请尝试右键以管理员身份运行 PCL", ex)
            Catch ex As Exception
                Log(ex, "Mod 文件无法打开（" & Path & "）", LogLevel.Developer)
                _FileUnavailableReason = ex
            Finally
                If Jar IsNot Nothing Then Jar.Dispose()
            End Try
            '完成标记
            IsLoaded = True
        End Sub

        ''' <summary>
        ''' 从 jar 文件中获取 Mod 信息。
        ''' </summary>
        Private Sub LookupMetadata(Jar As ZipArchive)

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
                Name = InfoObject("name")
                Description = InfoObject("description")
                Version = InfoObject("version")
                Url = InfoObject("url")
                ModId = InfoObject("modid")
                Dim AuthorJson As JArray = InfoObject("authorList")
                If AuthorJson IsNot Nothing Then
                    Dim Author As New List(Of String)
                    For Each Token In AuthorJson
                        Author.Add(Token.ToString)
                    Next
                    If Author.Any Then Authors = Join(Author, ", ")
                End If
                Dim Reqs As JArray = InfoObject("requiredMods")
                If Reqs IsNot Nothing Then
                    For Each Token As String In Reqs
                        If Not String.IsNullOrEmpty(Token) Then
                            Token = Token.Substring(Token.IndexOfF(":") + 1)
                            If Token.Contains("@") Then
                                AddDependency(Token.Split("@")(0), Token.Split("@")(1))
                            Else
                                AddDependency(Token)
                            End If
                        End If
                    Next
                End If
                Reqs = InfoObject("dependancies")
                If Reqs IsNot Nothing Then
                    For Each Token As String In Reqs
                        If Not String.IsNullOrEmpty(Token) Then
                            Token = Token.Substring(Token.IndexOfF(":") + 1)
                            If Token.Contains("@") Then
                                AddDependency(Token.Split("@")(0), Token.Split("@")(1))
                            Else
                                AddDependency(Token)
                            End If
                        End If
                    Next
                End If
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
                If FabricObject.ContainsKey("name") Then Name = FabricObject("name")
                If FabricObject.ContainsKey("version") Then Version = FabricObject("version")
                If FabricObject.ContainsKey("description") Then Description = FabricObject("description")
                If FabricObject.ContainsKey("id") Then ModId = FabricObject("id")
                If FabricObject.ContainsKey("contact") Then Url = If(FabricObject("contact")("homepage"), "")
                Dim AuthorJson As JArray = FabricObject("authors")
                If AuthorJson IsNot Nothing Then
                    Dim Author As New List(Of String)
                    For Each Token In AuthorJson
                        Author.Add(Token.ToString)
                    Next
                    If Author.Any Then Authors = Join(Author, ", ")
                End If
                'If (Not FabricObject.ContainsKey("serverSideOnly")) OrElse FabricObject("serverSideOnly")("value").ToObject(Of Boolean) = False Then
                '    '添加 Minecraft 依赖
                '    Dim DepMinecraft As String = If(If(FabricObject("acceptedMinecraftVersions") IsNot Nothing, FabricObject("acceptedMinecraftVersions")("value"), ""), "")
                '    If DepMinecraft <> "" Then AddDependency("minecraft", DepMinecraft)
                '    '添加其他依赖
                '    Dim Deps As String = If(If(FabricObject("dependencies") IsNot Nothing, FabricObject("dependencies")("value"), ""), "")
                '    If Deps <> "" Then
                '        For Each Dep In Deps.Split(";")
                '            If Dep = "" OrElse Not Dep.StartsWithF("required-") Then Continue For
                '            Dep = Dep.Substring(Dep.IndexOfF(":") + 1)
                '            If Dep.Contains("@") Then
                '                AddDependency(Dep.Split("@")(0), Dep.Split("@")(1))
                '            Else
                '                AddDependency(Dep)
                '            End If
                '        Next
                '    End If
                'End If
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
                ModId = ModEntry("modId")
                If _ModId Is Nothing Then Exit Try '设置了无效的 ModID
                If ModEntry.ContainsKey("displayName") Then Name = ModEntry("displayName")
                If ModEntry.ContainsKey("description") Then Description = ModEntry("description")
                If ModEntry.ContainsKey("version") Then Version = ModEntry("version")
                If TomlData(0).Value.ContainsKey("displayURL") Then Url = TomlData(0).Value("displayURL")
                If TomlData(0).Value.ContainsKey("authors") Then Authors = TomlData(0).Value("authors")
                For Each TomlSubData In TomlData
                    If TomlSubData.Key.ToLower = $"dependencies.{ModId.ToLower}" Then
                        Dim DepEntry As Dictionary(Of String, Object) = TomlSubData.Value
                        If DepEntry.ContainsKey("modId") AndAlso DepEntry.ContainsKey("mandatory") AndAlso DepEntry("mandatory") AndAlso
                           DepEntry.ContainsKey("side") AndAlso Not DepEntry("side").ToString.ToLower = "server" Then
                            AddDependency(DepEntry("modId"), If(DepEntry.ContainsKey("versionRange"), DepEntry("versionRange"), Nothing))
                        End If
                    End If
                Next
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
                If FmlObject.ContainsKey("useMetadata") AndAlso If(FmlObject("useMetadata")("value"), "").ToString.ToLower = "true" Then
                    '要求使用 mcmod.info 中的信息
                    Dim value As String = FmlObject("modid")("value")
                    If value Is Nothing Then Exit Try
                    value = RegexSeek(value.ToLower, "[0-9a-z_]+")
                    If value IsNot Nothing AndAlso value.ToLower <> "name" AndAlso value.Count > 1 AndAlso Val(value).ToString <> value Then
                        If Not PossibleModId.Contains(value) Then PossibleModId.Add(value)
                    End If
                    Exit Try
                End If
                If FmlObject.ContainsKey("name") Then Name = FmlObject("name")("value")
                If FmlObject.ContainsKey("version") Then Version = FmlObject("version")("value")
                If FmlObject.ContainsKey("modid") Then ModId = FmlObject("modid")("value")
                If (Not FmlObject.ContainsKey("serverSideOnly")) OrElse FmlObject("serverSideOnly")("value").ToObject(Of Boolean) = False Then
                    '添加 Minecraft 依赖
                    Dim DepMinecraft As String = If(If(FmlObject("acceptedMinecraftVersions") IsNot Nothing, FmlObject("acceptedMinecraftVersions")("value"), ""), "")
                    If DepMinecraft <> "" Then AddDependency("minecraft", DepMinecraft)
                    '添加其他依赖
                    Dim Deps As String = If(If(FmlObject("dependencies") IsNot Nothing, FmlObject("dependencies")("value"), ""), "")
                    If Deps <> "" Then
                        For Each Dep In Deps.Split(";")
                            If Dep = "" OrElse Not Dep.StartsWithF("required-") Then Continue For
                            Dep = Dep.Substring(Dep.IndexOfF(":") + 1)
                            If Dep.Contains("@") Then
                                AddDependency(Dep.Split("@")(0), Dep.Split("@")(1))
                            Else
                                AddDependency(Dep)
                            End If
                        Next
                    End If
                End If
            Catch ex As Exception
                Log(ex, "读取 fml_cache_annotation.json 时出现未知错误（" & Path & "）", LogLevel.Developer)
            End Try
#End Region
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

        End Sub

#End Region

#Region "网络信息"

        ''' <summary>
        ''' 当任何网络信息更新时触发。
        ''' </summary>
        Public Event OnCompUpdate(sender As McMod)

        ''' <summary>
        ''' 该 Mod 关联的网络项目。
        ''' </summary>
        Public Property Comp As CompProject
            Get
                Return _Comp
            End Get
            Set(value As CompProject)
                _Comp = value
                RaiseEvent OnCompUpdate(Me)
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
                RaiseEvent OnCompUpdate(Me)
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
                Return Not Setup.Get("UiHiddenFunctionModUpdate") AndAlso ChangelogUrls.Any()
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
        ''' 是否可能为前置 Mod。
        ''' </summary>
        Public Function IsPresetMod() As Boolean
            Return Not Dependencies.Any() AndAlso Name IsNot Nothing AndAlso (Name.ToLower.Contains("core") OrElse Name.ToLower.Contains("lib"))
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
            RunInUiWait(Sub() If FrmVersionMod IsNot Nothing Then FrmVersionMod.Load.ShowProgress = False)

            '等待 Mod 更新完成
            If PageVersionMod.UpdatingVersions.Contains(Loader.Input) Then
                Log($"[Mod] 等待 Mod 更新完成后才能继续加载 Mod 列表：" & Loader.Input)
                Try
                    RunInUiWait(Sub() If FrmVersionMod IsNot Nothing Then FrmVersionMod.Load.Text = "正在更新 Mod")
                    Do Until Not PageVersionMod.UpdatingVersions.Contains(Loader.Input)
                        If Loader.IsAborted Then Return
                        Thread.Sleep(100)
                    Loop
                Finally
                    RunInUiWait(Sub() If FrmVersionMod IsNot Nothing Then FrmVersionMod.Load.Text = "正在加载 Mod 列表")
                End Try
                FrmVersionMod.LoaderRun(LoaderFolderRunType.UpdateOnly)
            End If

            '获取 Mod 文件夹下的可用文件列表
            Dim ModFileList As New List(Of FileInfo)
            If Directory.Exists(Loader.Input) Then
                Dim RawName As String = Loader.Input.ToLower
                For Each File As FileInfo In EnumerateFiles(Loader.Input)
                    If File.DirectoryName.ToLower & "\" <> RawName Then
                        '仅当 Forge 1.13- 且文件夹名与版本号相同时，才加载该子文件夹下的 Mod
                        If Not (PageVersionLeft.Version IsNot Nothing AndAlso PageVersionLeft.Version.Version.HasForge AndAlso
                                PageVersionLeft.Version.Version.McCodeMain < 13 AndAlso
                                File.Directory.Name = $"1.{PageVersionLeft.Version.Version.McCodeMain}.{PageVersionLeft.Version.Version.McCodeSub}") Then
                            Continue For
                        End If
                    End If
                    If McMod.IsModFile(File.FullName) Then ModFileList.Add(File)
                Next
            End If

            '确定是否显示进度
            Loader.Progress = 0.05
            If ModFileList.Count > 50 Then RunInUi(Sub() If FrmVersionMod IsNot Nothing Then FrmVersionMod.Load.ShowProgress = True)

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
            Dim ModUpdateList As New List(Of McMod)
            For Each ModFile As FileInfo In ModFileList
                Loader.Progress += 0.94 / ModFileList.Count
                If Loader.IsAborted Then Return
                '加载 McMod 对象
                Dim ModEntry As New McMod(ModFile.FullName)
                ModEntry.Load()
                Dim DumpMod As McMod = ModList.FirstOrDefault(Function(m) m.RawFileName = ModEntry.RawFileName)
                If DumpMod IsNot Nothing Then
                    Dim DisabledMod As McMod = If(DumpMod.State = McMod.McModState.Disabled, DumpMod, ModEntry)
                    Log($"[Mod] 重复的 Mod 文件：{DumpMod.FileName} 与 {ModEntry.FileName}，已忽略 {DisabledMod.FileName}", LogLevel.Debug)
                    If DisabledMod Is ModEntry Then
                        Continue For
                    Else
                        ModList.Remove(DisabledMod)
                        ModUpdateList.Remove(DisabledMod)
                    End If
                End If
                ModList.Add(ModEntry)
                '读取 Comp 缓存
                If ModEntry.State = McMod.McModState.Unavailable Then Continue For
                Dim CacheKey = ModEntry.ModrinthHash & PageVersionLeft.Version.Version.McName & GetTargetModLoaders().Join("")
                If Cache.ContainsKey(CacheKey) Then
                    ModEntry.FromJson(Cache(CacheKey))
                    '如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                    If ModEntry.CompLoaded AndAlso Date.Now - Cache(CacheKey)("Comp")("CacheTime").ToObject(Of Date) < New TimeSpan(6, 0, 0) Then Continue For
                End If
                ModUpdateList.Add(ModEntry)
            Next
            Loader.Progress = 0.99
            Log($"[Mod] 共有 {ModList.Count} 个 Mod，其中 {ModUpdateList.Where(Function(m) m.Comp Is Nothing).Count} 个需要联网获取信息，{ModUpdateList.Where(Function(m) m.Comp IsNot Nothing).Count} 个需要更新信息")

            '排序
            ModList = ModList.Sort(
            Function(Left As McMod, Right As McMod) As Boolean
                If (Left.State = McMod.McModState.Unavailable) <> (Right.State = McMod.McModState.Unavailable) Then
                    Return Left.State = McMod.McModState.Unavailable
                Else
                    Return Not Right.FileName.CompareTo(Left.FileName)
                End If
            End Function)

            '回设
            If Loader.IsAborted Then Return
            Loader.Output = ModList

            '开始联网加载
            If ModUpdateList.Any() Then
                'TODO: 添加信息获取中提示
                McModDetailLoader.Start(New KeyValuePair(Of List(Of McMod), JObject)(ModUpdateList, Cache), IsForceRestart:=True)
            End If

        Catch ex As Exception
            Log(ex, "Mod 列表加载失败", LogLevel.Debug)
            Throw
        End Try
    End Sub
    '联网加载 Mod 详情
    Public McModDetailLoader As New LoaderTask(Of KeyValuePair(Of List(Of McMod), JObject), Integer)("Mod List Detail Loader", AddressOf McModDetailLoad)
    Private Sub McModDetailLoad(Loader As LoaderTask(Of KeyValuePair(Of List(Of McMod), JObject), Integer))
        Dim Mods As List(Of McMod) = Loader.Input.Key
        Dim Cache As JObject = Loader.Input.Value
        '获取作为检查目标的加载器和版本
        '此处不应向下扩展检查的 MC 小版本，例如 Mod 在更新 1.16.5 后，对早期的 1.16.2 版本发布了修补补丁，这会导致 PCL 将 1.16.5 版本的 Mod 降级到 1.16.2
        Dim TargetMcVersion As McVersionInfo = PageVersionLeft.Version.Version
        Dim ModLoaders = GetTargetModLoaders()
        Dim McVersion = TargetMcVersion.McName
        '开始网络获取
        Log($"[Mod] 目标加载器：{ModLoaders.Join("/")}，版本：{McVersion}")
        Dim EndedThreadCount As Integer = 0, IsFailed As Boolean = False
        Dim CurrentTaskThread As Thread = Thread.CurrentThread
        '从 Modrinth 获取信息
        RunInNewThread(
        Sub()
            Try
                '步骤 1：获取 Hash 与对应的工程 ID
                Dim ModrinthHashes = Mods.Select(Function(m) m.ModrinthHash).ToList()
                Dim ModrinthVersion = CType(GetJson(DlModRequest("https://api.modrinth.com/v2/version_files", "POST",
                    $"{{""hashes"": [""{ModrinthHashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")), JObject)
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
                    If Entry.CompFile Is Nothing OrElse Entry.CompFile.ReleaseDate < File.ReleaseDate Then Entry.CompFile = File
                Next
                If Loader.IsAbortedWithThread(CurrentTaskThread) Then Return
                Log($"[Mod] 需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息")
                '步骤 3：获取工程信息
                If Not ModrinthMapping.Any() Then Return
                Dim ModrinthProject = CType(GetJson(DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthMapping.Keys.Join(""",""")}""]",
                    "GET", "", "application/json")), JArray)
                For Each ProjectJson In ModrinthProject
                    Dim Project As New CompProject(ProjectJson)
                    For Each Entry In ModrinthMapping(Project.Id)
                        Entry.Comp = Project
                    Next
                Next
                Log($"[Mod] 已从 Modrinth 获取本地 Mod 信息，继续获取更新信息")
                '步骤 4：获取更新信息
                Dim ModrinthUpdate = CType(GetJson(DlModRequest("https://api.modrinth.com/v2/version_files/update", "POST",
                    $"{{""hashes"": [""{ModrinthMapping.SelectMany(Function(l) l.Value.Select(Function(m) m.ModrinthHash)).Join(""",""")}""], ""algorithm"": ""sha1"", 
                    ""loaders"": [""{ModLoaders.Join(""",""").ToLower}""],""game_versions"": [""{McVersion}""]}}", "application/json")), JObject)
                For Each Entry In Mods
                    If Not ModrinthUpdate.ContainsKey(Entry.ModrinthHash) OrElse Entry.CompFile Is Nothing Then Continue For
                    Dim UpdateFile As New CompFile(ModrinthUpdate(Entry.ModrinthHash), CompType.Mod)
                    If Not UpdateFile.Available Then Continue For
                    If ModeDebug Then Log($"[Mod] 本地文件 {Entry.CompFile.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}")
                    If Entry.CompFile.ReleaseDate >= UpdateFile.ReleaseDate OrElse Entry.CompFile.Hash = UpdateFile.Hash Then Continue For
                    '设置更新日志与更新文件
                    If Entry.UpdateFile IsNot Nothing AndAlso UpdateFile.Hash = Entry.UpdateFile.Hash Then '合并
                        Entry.ChangelogUrls.Add($"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={McVersion}")
                        UpdateFile.DownloadUrls.AddRange(Entry.UpdateFile.DownloadUrls) '合并下载源
                        Entry.UpdateFile = UpdateFile '优先使用 Modrinth 的文件
                    ElseIf Entry.UpdateFile Is Nothing OrElse UpdateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate Then '替换
                        Entry.ChangelogUrls = New List(Of String) From {$"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={McVersion}"}
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
                Dim CurseForgeRaw = CType(CType(GetJson(DlModRequest("https://api.curseforge.com/v1/fingerprints/432", "POST",
                    $"{{""fingerprints"": [{CurseForgeHashes.Join(",")}]}}", "application/json")), JObject)("data")("exactMatches"), JContainer)
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
                Dim CurseForgeProject = CType(GetJson(DlModRequest("https://api.curseforge.com/v1/mods", "POST",
                    $"{{""modIds"": [{CurseForgeMapping.Keys.Join(",")}]}}", "application/json")), JObject)("data")
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
                            If IndexVersion <> McVersion Then Continue For 'MC 版本匹配
                            '由于 latestFilesIndexes 是按时间从新到老排序的，所以只需取第一个；如果需要检查多个 releaseType 下的文件，将 > -1 改为 = 1，但这应当并不会获取到更新的文件
                            If NewestVersion IsNot Nothing AndAlso VersionSortInteger(NewestVersion, IndexVersion) > -1 Then Continue For '只保留最新 MC 版本
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
                Dim CurseForgeFiles = CType(GetJson(DlModRequest("https://api.curseforge.com/v1/mods/files", "POST",
                                    $"{{""fileIds"": [{UpdateFileIds.Keys.Join(",")}]}}", "application/json")), JObject)("data")
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
        '等待线程结束
        Do Until EndedThreadCount = 2
            If Loader.IsAborted Then Return
            Thread.Sleep(10)
        Loop
        '保存缓存
        Mods = Mods.Where(Function(m) m.Comp IsNot Nothing).ToList()
        Log($"[Mod] 联网获取本地 Mod 信息完成，为 {Mods.Count} 个 Mod 更新缓存")
        If Not Mods.Any() Then Return
        For Each Entry In Mods
            Entry.CompLoaded = Not IsFailed
            Cache(Entry.ModrinthHash & McVersion & ModLoaders.Join("")) = Entry.ToJson()
        Next
        WriteFile(PathTemp & "Cache\LocalMod.json", Cache.ToString(If(ModeDebug, Newtonsoft.Json.Formatting.Indented, Newtonsoft.Json.Formatting.None)))
        '刷新边栏
        If FrmVersionMod?.Filter = PageVersionMod.FilterType.CanUpdate Then
            RunInUi(Sub() FrmVersionMod?.RefreshUI()) '同步 “可更新” 列表 (#4677)
        Else
            RunInUi(Sub() FrmVersionMod?.RefreshBars())
        End If
    End Sub
    Public Function GetTargetModLoaders() As List(Of CompModLoaderType)
        Dim ModLoaders As New List(Of CompModLoaderType)
        If PageVersionLeft.Version.Version.HasForge Then ModLoaders.Add(CompModLoaderType.Forge)
        If PageVersionLeft.Version.Version.HasNeoForge Then ModLoaders.Add(CompModLoaderType.NeoForge)
        If PageVersionLeft.Version.Version.HasFabric Then ModLoaders.Add(CompModLoaderType.Fabric)
        If PageVersionLeft.Version.Version.HasLiteLoader Then ModLoaders.Add(CompModLoaderType.LiteLoader)
        If Not ModLoaders.Any() Then ModLoaders.AddRange({CompModLoaderType.Forge, CompModLoaderType.NeoForge, CompModLoaderType.Fabric, CompModLoaderType.LiteLoader, CompModLoaderType.Quilt})
        Return ModLoaders
    End Function

#If DEBUG Then
    ''' <summary>
    ''' 检查 Mod 列表中存在的错误，返回错误信息的集合。
    ''' </summary>
    Public Function McModCheck(Version As McVersion, Mods As List(Of McMod)) As List(Of String)
        Dim Result As New List(Of String)
        '令所有 Mod 进行基础检查，并归纳需要检查的 Mod
        Dim CurrentModList As New List(Of McMod)
        For Each ModEntity In Mods
            If Not ModEntity.IsFileAvailable Then
                Result.Add("无法读取的 Mod 文件。" & vbCrLf & " - " & ModEntity.Path)
                Continue For
            End If
            If ModEntity.State = McMod.McModState.Fine AndAlso ModEntity.ModId IsNot Nothing Then CurrentModList.Add(ModEntity)
        Next
        '添加默认依赖
        Dim CurrentDependencies As New Dictionary(Of String, String()) '{DependencyVersion, Path}
        If Version.State = McVersionState.Forge Then CurrentDependencies.Add("forge", {Version.Version.ForgeVersion, "Forge"})
        CurrentDependencies.Add("minecraft", {Version.Version.McName, "Minecraft"})
        '检查重复的 Mod，并添加对应的依赖
        For Each ModEntity In CurrentModList
            For Each PossibleModId In ModEntity.PossibleModId
                If CurrentDependencies.ContainsKey(PossibleModId) Then
                    If CurrentDependencies(PossibleModId)(2) = 1 Then
                        Result.Add("重复添加了相同的 Mod，请尝试删除其中一个（ModID：" & PossibleModId & "）。" & vbCrLf &
                            " - " & ModEntity.FileName & vbCrLf &
                            " - " & CurrentDependencies(PossibleModId)(1))
                    Else
                        Log("[Minecraft] 由于可能有多个 ModID，跳过疑似的重复项（ModID：" & PossibleModId & "）。" & vbCrLf &
                            " - " & ModEntity.FileName & vbCrLf &
                            " - " & CurrentDependencies(PossibleModId)(1), LogLevel.Developer)
                    End If
                Else
                    CurrentDependencies.Add(PossibleModId, {ModEntity.Version, ModEntity.FileName, ModEntity.PossibleModId.Count})
                End If
            Next
        Next
        '检查依赖
        For Each ModEntity In CurrentModList
            Try
                For Each Dependency In ModEntity.Dependencies
                    Dim ReqId As String = Dependency.Key
                    If ReqId.Count < 2 Then Continue For '确保正常
                    If ReqId = ModEntity.ModId Then Continue For '跳过自体引用
                    If ReqId = "forgemultipartcbe" Then Continue For '跳过莫名其妙的引用
                    If Dependency.Value IsNot Nothing Then
                        '获取分段后的详细版本信息
                        Dim ReqVersion As String = Dependency.Value
                        Dim ReqVersionHeadCanEqual As Boolean = ReqVersion.StartsWithF("[")
                        Dim ReqVersionTailCanEqual As Boolean = ReqVersion.EndsWithF("]")
                        Dim ReqVersionHead As String
                        Dim ReqVersionTail As String
                        If ReqVersion.Contains(",") Then
                            ReqVersionHead = ReqVersion.Split(",")(0).Trim("([ ".ToCharArray())
                            ReqVersionTail = ReqVersion.Split(",")(1).Trim("]) ".ToCharArray())
                        Else
                            ReqVersionHead = ReqVersion.Trim("([]) ".ToCharArray())
                            ReqVersionTail = ReqVersionHead
                            If ReqId = "minecraft" AndAlso ReqVersionHead.Split(".").Count = 2 Then
                                ReqVersionTail = ReqVersionHead.Split(".")(0) & "." & (Val(ReqVersionHead.Split(".")(1)) + 1)
                                ReqVersionTailCanEqual = False
                            End If
                        End If
                        If ReqVersionHead.StartsWithF("1.") AndAlso ReqVersionHead.Contains("-") Then ReqVersionHead = ReqVersionHead.Substring(ReqVersionHead.LastIndexOfF("-") + 1)
                        If ReqVersionTail.StartsWithF("1.") AndAlso ReqVersionTail.Contains("-") Then ReqVersionTail = ReqVersionTail.Substring(ReqVersionTail.LastIndexOfF("-") + 1)
                        '获取报错描述文本
                        Dim VersionRequire As String
                        If ReqVersionHead = ReqVersionTail Then
                            VersionRequire = "应为 " & ReqVersionHead
                        ElseIf ReqVersionHead.Contains(".") AndAlso ReqVersionTail.Contains(".") Then
                            VersionRequire = "应为 " & ReqVersionHead & " 至 " & ReqVersionTail
                        ElseIf ReqVersionHead.Contains(".") Then
                            If ReqVersionHeadCanEqual Then
                                VersionRequire = "最低应为 " & ReqVersionHead
                            Else
                                VersionRequire = "应高于 " & ReqVersionHead
                            End If
                        ElseIf ReqVersionTail.Contains(".") Then
                            If ReqVersionTailCanEqual Then
                                VersionRequire = "最高应为 " & ReqVersionHead
                            Else
                                VersionRequire = "应低于 " & ReqVersionHead
                            End If
                        Else
                            VersionRequire = ""
                        End If
                        '检查前置 Mod 是否存在，并获取其版本
                        If Not CurrentDependencies.ContainsKey(ReqId) Then
                            Result.Add("缺少前置 Mod：" & ReqId & If(VersionRequire = "", "", "，其版本" & VersionRequire) & "。" & vbCrLf & " - " & ModEntity.FileName)
                            Continue For
                        End If
                        Dim CurrentVersion As String = If(CurrentDependencies(ReqId)(0), "0.0")
                        If CurrentVersion.StartsWithF("1.") AndAlso CurrentVersion.Contains("-") Then CurrentVersion = CurrentVersion.Substring(CurrentVersion.LastIndexOfF("-") + 1)
                        '对比前置 Mod 头部版本
                        If ReqVersionHead.Contains(".") Then
                            If VersionSortInteger(ReqVersionHead, CurrentVersion) > If(ReqVersionHeadCanEqual, 0, -1) Then
                                Result.Add(ReqId.Substring(0, 1).ToUpper & ReqId.Substring(1) & " 版本过低，其版本" & VersionRequire & "，而当前版本为 " & CurrentVersion & "。" & vbCrLf &
                                           " - " & ModEntity.FileName & If(ReqId <> "minecraft" AndAlso ReqId <> "forge", vbCrLf & " - 前置：" & CurrentDependencies(ReqId)(1), ""))
                                Continue For
                            End If
                        End If
                        '对比前置 Mod 尾部版本
                        If ReqVersionTail.Contains(".") Then
                            If VersionSortInteger(CurrentVersion, ReqVersionTail) > If(ReqVersionTailCanEqual, 0, -1) Then
                                Result.Add(ReqId.Substring(0, 1).ToUpper & ReqId.Substring(1) & " 版本过高，其版本" & VersionRequire & "，而当前版本为 " & CurrentVersion & "。" & vbCrLf &
                                           " - " & ModEntity.FileName & If(ReqId <> "minecraft" AndAlso ReqId <> "forge", vbCrLf & " - 前置：" & CurrentDependencies(ReqId)(1), ""))
                                Continue For
                            End If
                        End If
                    Else
                        If Not CurrentDependencies.ContainsKey(Dependency.Key) Then
                            Result.Add("缺少前置 Mod：" & Dependency.Key & "。" & vbCrLf & " - " & ModEntity.FileName)
                            Continue For
                        End If
                    End If
                Next
            Catch ex As Exception
                Result.Add("检查 Mod 时出错：" & GetExceptionSummary(ex) & vbCrLf & " - " & ModEntity.FileName)
                Log(ex, "检查 Mod 时出错")
            End Try
        Next
        If Not Result.Any() Then
            Log("[Minecraft] Mod 检查未发现异常")
        Else
            Log("[Minecraft] Mod 检查异常结果：" & vbCrLf & Join(Result, vbCrLf))
        End If
        Return Result
    End Function
#End If

End Module