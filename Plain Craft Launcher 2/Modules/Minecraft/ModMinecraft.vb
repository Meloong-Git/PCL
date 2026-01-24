Imports System.IO.Compression

Public Module ModMinecraft

#Region "文件夹"

    ''' <summary>
    ''' 当前的 Minecraft 文件夹路径，以“\”结尾。
    ''' </summary>
    Public McFolderSelected As String
    ''' <summary>
    ''' 当前的 Minecraft 文件夹列表。
    ''' </summary>
    Public McFolderList As New List(Of McFolder)

    Public Class McFolder
        Public Name As String
        ''' <summary>
        ''' 文件夹路径。
        ''' 以 \ 结尾，例如 "D:\Game\MC\.minecraft\"。
        ''' </summary>
        Public Location As String
        Public Type As Types
        Public Enum Types
            Original
            RenamedOriginal
            Custom
        End Enum

        Public Overrides Function Equals(obj As Object) As Boolean
            If Not (TypeOf obj Is McFolder) Then Return False
            Dim folder = DirectCast(obj, McFolder)
            Return Name = folder.Name AndAlso Location = folder.Location AndAlso Type = folder.Type
        End Function
        Public Overrides Function ToString() As String
            Return Location
        End Function
    End Class

    ''' <summary>
    ''' 加载 Minecraft 文件夹列表。
    ''' </summary>
    Public McFolderListLoader As New LoaderTask(Of Integer, Integer)("Minecraft Folder List", AddressOf McFolderListLoadSub, Priority:=ThreadPriority.AboveNormal)
    Private Sub McFolderListLoadSub()
        Try
            '初始化
            Dim CacheMcFolderList = New List(Of McFolder)

#Region "读取默认（Original）文件夹，即当前、官启文件夹，可能没有结果"

            '扫描当前文件夹
            Try
                If Directory.Exists(Path & "versions\") Then CacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = Path, .Type = McFolder.Types.Original})
                For Each Folder As DirectoryInfo In New DirectoryInfo(Path).GetDirectories
                    If Directory.Exists(Folder.FullName & "versions\") OrElse Folder.Name = ".minecraft" Then CacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = Folder.FullName & "\", .Type = McFolder.Types.Original})
                Next
            Catch ex As Exception
                Log(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败")
            End Try

            '扫描官启文件夹
            Dim MojangPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\"
            If (Not CacheMcFolderList.Any OrElse MojangPath <> CacheMcFolderList(0).Location) AndAlso '当前文件夹不是官启文件夹
                Directory.Exists(MojangPath & "versions\") Then '具有权限且存在 versions 文件夹
                CacheMcFolderList.Add(New McFolder With {.Name = "官方启动器文件夹", .Location = MojangPath, .Type = McFolder.Types.Original})
            End If

#End Region

#Region "读取自定义（Custom）文件夹，可能没有结果"

            '格式：TMZ 12>C://xxx/xx/|Test>D://xxx/xx/|名称>路径
            For Each Folder As String In Setup.Get("LaunchFolders").Split("|")
                If Folder = "" Then Continue For
                If Not Folder.Contains(">") OrElse Not Folder.EndsWithF("\") Then
                    Hint("无效的 Minecraft 文件夹：" & Folder, HintType.Red)
                    Continue For
                End If
                Dim Name As String = Folder.Split(">")(0)
                Dim Path As String = Folder.Split(">")(1)
                Try
                    CheckPermissionWithException(Path)
                    '若已有该文件夹，则直接重命名；没有则添加
                    Dim Renamed As Boolean = False
                    For Each OriginalFolder As McFolder In CacheMcFolderList
                        If OriginalFolder.Location = Path Then
                            OriginalFolder.Name = Name
                            OriginalFolder.Type = McFolder.Types.RenamedOriginal
                            Renamed = True
                        End If
                    Next
                    If Not Renamed Then CacheMcFolderList.Add(New McFolder With {.Name = Name, .Location = Path, .Type = McFolder.Types.Custom})
                Catch ex As Exception
                    MyMsgBox("失效的 Minecraft 文件夹：" & vbCrLf & Path & vbCrLf & vbCrLf & ex.GetBrief(), "Minecraft 文件夹失效", IsWarn:=True)
                    Log(ex, $"无法访问 Minecraft 文件夹 {Path}")
                End Try
            Next

            '将自定义文件夹情况同步到设置
            Dim NewSetup As New List(Of String)
            For Each Folder As McFolder In CacheMcFolderList
                If Not Folder.Type = McFolder.Types.Original Then NewSetup.Add(Folder.Name & ">" & Folder.Location)
            Next
            If Not NewSetup.Any() Then NewSetup.Add("") '防止 0 元素 Join 返回 Nothing
            Setup.Set("LaunchFolders", Join(NewSetup, "|"))

#End Region

            '若没有可用文件夹，则创建 .minecraft
            If Not CacheMcFolderList.Any() Then
                Directory.CreateDirectory(Path & ".minecraft\versions\")
                CacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = Path & ".minecraft\", .Type = McFolder.Types.Original})
            End If

            For Each Folder As McFolder In CacheMcFolderList
#Region "更新 launcher_profiles.json"
                McFolderLauncherProfilesJsonCreate(Folder.Location)
#End Region
            Next
            If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomInteger(200, 2000))

            '回设
            McFolderList = CacheMcFolderList

        Catch ex As Exception
            Log(ex, "加载 Minecraft 文件夹列表失败", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 为 Minecraft 文件夹创建 launcher_profiles.json 文件。
    ''' </summary>
    Public Sub McFolderLauncherProfilesJsonCreate(Folder As String)
        Try
            If File.Exists(Folder & "launcher_profiles.json") Then Return
            Dim ResultJson As String =
"{
    ""profiles"":  {
        ""PCL"": {
            ""icon"": ""Grass"",
            ""name"": ""PCL"",
            ""lastVersionId"": ""latest-release"",
            ""type"": ""latest-release"",
            ""lastUsed"": """ & Date.Now.ToString("yyyy'-'MM'-'dd") & "T" & Date.Now.ToString("HH':'mm':'ss") & ".0000Z""
        }
    },
    ""selectedProfile"": ""PCL"",
    ""clientToken"": ""23323323323323323323323323323333""
}"
            WriteFile(Folder & "launcher_profiles.json", ResultJson, Encoding:=Encoding.GetEncoding("GB18030"))
            Log("[Minecraft] 已创建 launcher_profiles.json：" & Folder)
        Catch ex As Exception
            Log(ex, "创建 launcher_profiles.json 失败（" & Folder & "）", LogLevel.Feedback)
        End Try
    End Sub

#End Region

#Region "版本处理"

    Public Const McInstanceCacheVersion As Integer = 33

    Private _McInstanceSelected As McInstance
    ''' <summary>
    ''' 当前的 Minecraft 版本。
    ''' </summary>
    Public Property McInstanceSelected As McInstance
        Get
            Return _McInstanceSelected
        End Get
        Set(value As McInstance)
            Static _McInstanceSelectedLast = 0 '为 0 以保证与 Nothing 不相同，使得 UI 显示可以正常初始化
            If ReferenceEquals(_McInstanceSelectedLast, value) Then Return
            _McInstanceSelected = value '由于有可能是 Nothing，导致无法初始化，才得这样弄一圈
            _McInstanceSelectedLast = value
            If value Is Nothing Then Return
            '重置缓存的下载文件夹
            PageDownloadCompDetail.CachedFolder.Clear()
            '统一通行证重判
            If AniControlEnabled = 0 AndAlso
               Setup.Get("VersionServerNide", Instance:=value) <> Setup.Get("CacheNideServer") AndAlso
               Setup.Get("VersionServerLogin", Instance:=value) = 3 Then
                Setup.Set("CacheNideAccess", "")
                Log("[Launch] 服务器改变，要求重新登录统一通行证")
            End If
            If Setup.Get("VersionServerLogin", Instance:=value) = 3 Then
                Setup.Set("CacheNideServer", Setup.Get("VersionServerNide", Instance:=value))
            End If
            'Authlib-Injector 重判
            If AniControlEnabled = 0 AndAlso
               Setup.Get("VersionServerAuthServer", Instance:=value) <> Setup.Get("CacheAuthServerServer") AndAlso
               Setup.Get("VersionServerLogin", Instance:=value) = 4 Then
                Setup.Set("CacheAuthAccess", "")
                Log("[Launch] 服务器改变，要求重新登录 Authlib-Injector")
            End If
            If Setup.Get("VersionServerLogin", Instance:=value) = 4 Then
                Setup.Set("CacheAuthServerServer", Setup.Get("VersionServerAuthServer", Instance:=value))
                Setup.Set("CacheAuthServerName", Setup.Get("VersionServerAuthName", Instance:=value))
                Setup.Set("CacheAuthServerRegister", Setup.Get("VersionServerAuthRegister", Instance:=value))
            End If
        End Set
    End Property

    Public Class McInstance

        ''' <summary>
        ''' 版本文件夹路径，以 \ 结尾。
        ''' </summary>
        Public ReadOnly Property PathVersion As String
        ''' <summary>
        ''' 应用版本隔离后，该版本所对应的 Minecraft 根文件夹，以 \ 结尾。
        ''' </summary>
        Public ReadOnly Property PathIndie As String
            Get
                If Setup.IsUnset("VersionArgumentIndieV2", Instance:=Me) Then
                    If Not IsLoaded Then Load()
                    '决定该版本是否应该被隔离
                    Dim ShouldBeIndie =
                    Function() As Boolean
                        '从老的版本独立设置中迁移：-1 未决定，0 使用全局设置，1 手动开启，2 手动关闭
                        If Not Setup.IsUnset("VersionArgumentIndie", Instance:=Me) AndAlso Setup.Get("VersionArgumentIndie", Instance:=Me) > 0 Then
                            Log($"[Minecraft] 版本隔离初始化（{Name}）：从老的版本独立设置中迁移")
                            Return Setup.Get("VersionArgumentIndie", Instance:=Me) = 1
                        End If
                        '若版本文件夹下包含 mods 或 saves 文件夹，则自动开启版本隔离
                        Dim ModFolder As New DirectoryInfo(PathVersion & "mods\")
                        Dim SaveFolder As New DirectoryInfo(PathVersion & "saves\")
                        If (ModFolder.Exists AndAlso ModFolder.EnumerateFiles.Any) OrElse (SaveFolder.Exists AndAlso SaveFolder.EnumerateDirectories.Any) Then
                            Log($"[Minecraft] 版本隔离初始化（{Name}）：版本文件夹下存在 mods 或 saves 文件夹，自动开启")
                            Return True
                        End If
                        '根据全局的默认设置决定是否隔离
                        Dim IsRelease As Boolean = State <> McInstanceState.Fool AndAlso State <> McInstanceState.Old AndAlso State <> McInstanceState.Snapshot
                        Log($"[Minecraft] 版本隔离初始化（{Name}）：从全局默认设置中（{Setup.Get("LaunchArgumentIndieV2")}）判断，State {GetStringFromEnum(State)}，IsRelease {IsRelease}，Modable {Modable}")
                        Select Case Setup.Get("LaunchArgumentIndieV2")
                            Case 0 '关闭
                                Return False
                            Case 1 '仅隔离可安装 Mod 的版本
                                Return Modable
                            Case 2 '仅隔离非正式版
                                Return Not IsRelease
                            Case 3 '隔离非正式版与可安装 Mod 的版本
                                Return Not IsRelease OrElse Modable
                            Case Else '隔离所有版本
                                Return True
                        End Select
                    End Function
                    Setup.Set("VersionArgumentIndieV2", ShouldBeIndie(), Instance:=Me)
                End If
                Return If(Setup.Get("VersionArgumentIndieV2", Instance:=Me), PathVersion, McFolderSelected)
            End Get
        End Property

        ''' <summary>
        ''' 该版本的版本文件夹名称。
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                If _Name Is Nothing AndAlso Not PathVersion = "" Then _Name = GetFolderNameFromPath(PathVersion)
                Return _Name
            End Get
        End Property
        Private _Name As String = Nothing

        ''' <summary>
        ''' 显示的描述文本。
        ''' </summary>
        Public Info As String = "该版本未被加载，请向作者反馈此问题"

        ''' <summary>
        ''' 该版本的列表检查原始结果，不受自定义影响。
        ''' </summary>
        Public State As McInstanceState = McInstanceState.Error
        ''' <summary>
        ''' 显示的版本图标。
        ''' </summary>
        Public Logo As String
        ''' <summary>
        ''' 是否为收藏的版本。
        ''' </summary>
        Public IsStar As Boolean = False
        ''' <summary>
        ''' 强制版本分类，0 为未启用，1 为隐藏，2 及以上为其他普通分类。
        ''' </summary>
        Public DisplayType As McInstanceCardType = McInstanceCardType.Auto
        ''' <summary>
        ''' 该版本是否可以安装 Mod。
        ''' </summary>
        Public ReadOnly Property Modable As Boolean
            Get
                If Not IsLoaded Then Load()
                Return Version.HasFabric OrElse Version.HasForgelike OrElse Version.HasLiteLoader OrElse
                    DisplayType = McInstanceCardType.API '#223
            End Get
        End Property
        ''' <summary>
        ''' 版本信息。
        ''' </summary>
        Public Property Version As McVersion
            Get
                If _Version IsNot Nothing Then Return _Version
                _Version = New McVersion
#Region "获取游戏版本"
                Try
                    '获取发布时间并判断是否为老版本
                    Try
                        If JsonObject("releaseTime") Is Nothing Then
                            ReleaseTime = New Date(1970, 1, 1, 15, 0, 0) '未知版本也可能显示为 1970 年
                        Else
                            ReleaseTime = JsonObject("releaseTime").ToObject(Of Date)
                        End If
                        If ReleaseTime.Year > 2000 AndAlso ReleaseTime.Year < 2013 Then
                            _Version.VanillaName = "Old"
                            GoTo VersionSearchFinish
                        End If
                    Catch
                        ReleaseTime = New Date(1970, 1, 1, 15, 0, 0)
                    End Try
                    '实验性快照
                    If If(JsonObject("type"), "") = "pending" Then
                        _Version.VanillaName = "pending"
                        GoTo VersionSearchFinish
                    End If
                    '从 PCL 下载的版本信息中获取版本号
                    If JsonObject("clientVersion") IsNot Nothing Then
                        _Version.VanillaName = JsonObject("clientVersion")
                        GoTo VersionSearchFinish
                    End If
                    '从 HMCL 下载的版本信息中获取版本号
                    If JsonObject("patches") IsNot Nothing Then
                        For Each Patch As JObject In JsonObject("patches")
                            If If(Patch("id"), "").ToString = "game" AndAlso Patch("version") IsNot Nothing Then
                                _Version.VanillaName = Patch("version").ToString
                                GoTo VersionSearchFinish
                            End If
                        Next
                    End If
                    '从 Forge / NeoForge Arguments 中获取版本号
                    If JsonObject("arguments") IsNot Nothing AndAlso JsonObject("arguments")("game") IsNot Nothing Then
                        Dim Mark As Boolean = False
                        For Each Argument In JsonObject("arguments")("game")
                            If Mark Then
                                _Version.VanillaName = Argument.ToString
                                GoTo VersionSearchFinish
                            End If
                            If Argument.ToString = "--fml.mcVersion" Then Mark = True
                        Next
                    End If
                    '从继承版本中获取版本号
                    If Not InheritName = "" Then
                        _Version.VanillaName = If(JsonObject("jar"), "").ToString 'LiteLoader 优先使用 Jar
                        If _Version.VanillaName = "" Then _Version.VanillaName = InheritName
                        GoTo VersionSearchFinish
                    End If
                    '从下载地址中获取版本号
                    Dim Regex As String = RegexSeek(If(JsonObject("downloads"), "").ToString, "(?<=launcher.mojang.com/mc/game/)[^/]*")
                    If Regex IsNot Nothing Then
                        _Version.VanillaName = Regex
                        GoTo VersionSearchFinish
                    End If
                    '从 Forge 版本中获取版本号
                    Dim LibrariesString As String = JsonObject("libraries").ToString
                    Regex = If(RegexSeek(LibrariesString, "(?<=net.minecraftforge:forge:)[0-9]{1,2}.[0-9+.]+"), RegexSeek(LibrariesString, "(?<=net.minecraftforge:fmlloader:)[0-9]{1,2}.[0-9+.]+"))
                    If Regex IsNot Nothing Then
                        _Version.VanillaName = Regex
                        GoTo VersionSearchFinish
                    End If
                    '从 OptiFine 版本中获取版本号
                    Regex = RegexSeek(LibrariesString, "(?<=optifine:OptiFine:)[0-9]{1,2}.[0-9+.]+")
                    If Regex IsNot Nothing Then
                        _Version.VanillaName = Regex
                        GoTo VersionSearchFinish
                    End If
                    '从 Fabric 版本中获取版本号
                    Regex = RegexSeek(LibrariesString, "(?<=((fabricmc)|(quiltmc)):intermediary:)[^""]*")
                    If Regex IsNot Nothing Then
                        _Version.VanillaName = Regex
                        GoTo VersionSearchFinish
                    End If
                    '从 jar 项中获取版本号
                    If JsonObject("jar") IsNot Nothing Then
                        _Version.VanillaName = JsonObject("jar").ToString
                        GoTo VersionSearchFinish
                    End If
                    '从 jar 文件的 version.json 中获取版本号
                    If JsonVersion?("name") IsNot Nothing Then
                        Dim JsonVerName As String = JsonVersion("name").ToString
                        If JsonVerName.Length < 32 Then '因为 wiki 说这玩意儿可能是个 hash，虽然我没发现
                            _Version.VanillaName = JsonVerName
                            Log("[Minecraft] 从版本 jar 中的 version.json 获取到版本号：" & JsonVerName)
                            GoTo VersionSearchFinish
                        End If
                    End If
                    '从 JSON 的 ID 中获取
                    Static Pattern = "(([1-9][0-9]w[0-9]{2}[a-g])|((1|[2-9][0-9])\.[0-9]+(\.[0-9]+)?(-(pre|rc|snapshot-?)[1-9]*| Pre-Release( [1-9])?)?))(_unobfuscated)?"
                    Regex = RegexSeek(JsonObject("id"), Pattern, RegularExpressions.RegexOptions.IgnoreCase)
                    If Regex IsNot Nothing Then
                        _Version.VanillaName = Regex
                        GoTo VersionSearchFinish
                    End If
                    '非准确的版本判断警告
                    Log("[Minecraft] 无法完全确认 MC 版本号的版本：" & Name)
                    _Version.Reliable = False
                    '从文件夹名中获取
                    Regex = RegexSeek(Name, Pattern, RegularExpressions.RegexOptions.IgnoreCase)
                    If Regex IsNot Nothing Then
                        _Version.VanillaName = Regex
                        GoTo VersionSearchFinish
                    End If
                    '从 JSON 出现的版本号中获取
                    Dim JsonRaw As JObject = JsonObject.DeepClone()
                    JsonRaw.Remove("libraries")
                    Dim JsonRawText As String = JsonRaw.ToString
                    Regex = RegexSeek(JsonRawText, Pattern, RegularExpressions.RegexOptions.IgnoreCase)
                    If Regex IsNot Nothing Then
                        _Version.VanillaName = Regex
                        GoTo VersionSearchFinish
                    End If
                    '无法获取
                    _Version.VanillaName = "Unknown"
                    Info = "PCL 无法识别该版本的 MC 版本号"
                Catch ex As Exception
                    Log(ex, "识别 Minecraft 版本时出错")
                    _Version.VanillaName = "Unknown"
                    Info = "无法识别：" & ex.Message
                End Try
#End Region
VersionSearchFinish:
                '获取版本号
                _Version.VanillaName = _Version.VanillaName.Replace("_unobfuscated", "").Replace(" Unobfuscated", "")
                If _Version.VanillaName.StartsWithF("1.") Then
                    Dim Segments = _Version.VanillaName.Split(" _-.".ToCharArray)
                    _Version.Vanilla = New Version(
                        Val(If(Segments.Count >= 2, Segments(1), "0")),
                        0,
                        Val(If(Segments.Count >= 3, Segments(2), "0")))
                ElseIf RegexCheck(_Version.VanillaName, "^[2-9][0-9]\.") Then
                    Dim Segments = _Version.VanillaName.Split(" _-.".ToCharArray)
                    _Version.Vanilla = New Version(
                        Val(Segments(0)),
                        Val(If(Segments.Count >= 2, Segments(1), "0")),
                        Val(If(Segments.Count >= 3, Segments(2), "0")))
                Else
                    _Version.Vanilla = New Version(9999, 0, 0)
                End If
                Return _Version
            End Get
            Set(value As McVersion)
                _Version = value
            End Set
        End Property
        Private _Version As McVersion = Nothing

        ''' <summary>
        ''' 版本的发布时间。
        ''' </summary>
        Public ReleaseTime As New Date(1970, 1, 1, 15, 0, 0)

        ''' <summary>
        ''' 获取该版本的 JSON 路径。
        ''' </summary>
        Public Function GetJsonPath() As String
            Dim JsonPath As String = PathVersion & Name & ".json"
            If Not File.Exists(JsonPath) Then
                '尝试寻找 JSON 文件
                JsonPath = Nothing
                For Each JsonCandidatePath In Directory.GetFiles(PathVersion, "*.json")
                    Try
                        Dim JsonCandidate As JObject = GetJson(ReadFile(JsonCandidatePath))
                        If Not JsonCandidate.ContainsKey("mainClass") Then Continue For
                        If Not JsonCandidate.ContainsKey("type") Then Continue For
                        If Not JsonCandidate.ContainsKey("id") Then Continue For
                    Catch
                    End Try
                    JsonPath = JsonCandidatePath
                    Exit For
                Next
                If JsonPath Is Nothing Then
                    Throw New Exception($"未找到版本 JSON 文件：{PathVersion}{Name}.json")
                Else
                    Log("[Minecraft] 未找到同名版本 JSON，自动换用 " & JsonPath, LogLevel.Debug)
                End If
            End If
            Return JsonPath
        End Function
        ''' <summary>
        ''' 该版本的 Json 文本。
        ''' </summary>
        Public Property JsonText As String
            Get
                '快速检查 JSON 是否以 { 开头、} 结尾；忽略空白字符
                Dim FastJsonCheck =
                Function(Json As String) As Boolean
                    Dim TrimedJson As String = Json.Trim()
                    Return TrimedJson.StartsWithF("{") AndAlso TrimedJson.EndsWithF("}")
                End Function
                If _JsonText Is Nothing Then
                    Dim JsonPath As String = GetJsonPath()
                    _JsonText = ReadFile(JsonPath)
                    '如果 ReadFile 失败会返回空字符串；这可能是由于文件被临时占用，故延时后重试
                    If Not FastJsonCheck(_JsonText) Then
                        If RunInUi() Then
                            Log("[Minecraft] 版本 JSON 文件为空或有误，由于代码在主线程运行，将不再进行重试", LogLevel.Debug)
                            GetJson(_JsonText) '触发异常
                        Else
                            Log($"[Minecraft] 版本 JSON 文件为空或有误，将在 2s 后重试读取（{JsonPath}）", LogLevel.Debug)
                            Thread.Sleep(2000)
                            _JsonText = ReadFile(JsonPath)
                            If Not FastJsonCheck(_JsonText) Then GetJson(_JsonText) '触发异常
                        End If
                    End If
                End If
                Return _JsonText
            End Get
            Set(value As String)
                _JsonText = value
            End Set
        End Property
        Private _JsonText As String = Nothing
        ''' <summary>
        ''' 该版本的 Json 对象。
        ''' 若 Json 存在问题，在获取该属性时即会抛出异常。
        ''' </summary>
        Public Property JsonObject As JObject
            Get
                If _JsonObject Is Nothing Then
                    Dim Text As String = JsonText '触发 JsonText 的 Get 事件
                    Try
                        _JsonObject = GetJson(Text)
                        '转换 HMCL 关键项
                        If _JsonObject.ContainsKey("patches") AndAlso Not _JsonObject.ContainsKey("time") Then
                            IsHmclFormatJson = True
                            '合并 Json
                            'Dim HasOptiFine As Boolean = False, HasForge As Boolean = False
                            Dim CurrentObject As JObject = Nothing
                            Dim SubjsonList As New List(Of JObject)
                            For Each Subjson As JObject In _JsonObject("patches")
                                SubjsonList.Add(Subjson)
                            Next
                            SubjsonList = SubjsonList.SortByComparison(
                                Function(Left, Right) Val(If(Left("priority"), "0").ToString) < Val(If(Right("priority"), "0").ToString))
                            For Each Subjson As JObject In SubjsonList
                                Dim Id As String = Subjson("id")
                                If Id IsNot Nothing Then
                                    '合并 Json
                                    Log("[Minecraft] 合并 HMCL 分支项：" & Id)
                                    If CurrentObject IsNot Nothing Then
                                        CurrentObject.Merge(Subjson)
                                    Else
                                        CurrentObject = Subjson
                                    End If
                                Else
                                    Log("[Minecraft] 存在为空的 HMCL 分支项")
                                End If
                            Next
                            _JsonObject = CurrentObject
                            '修改附加项
                            _JsonObject("id") = Name
                            If _JsonObject.ContainsKey("inheritsFrom") Then _JsonObject.Remove("inheritsFrom")
                        End If
                        '与继承版本合并
                        Dim InstanceNameInheritFrom = Nothing
                        Try
                            InstanceNameInheritFrom = If(_JsonObject("inheritsFrom") Is Nothing, "", _JsonObject("inheritsFrom").ToString)
                            If InstanceNameInheritFrom = Name Then
                                Log("[Minecraft] 自引用的继承版本：" & Name, LogLevel.Debug)
                                InstanceNameInheritFrom = ""
                                Exit Try
                            End If
Recheck:
                            If InstanceNameInheritFrom <> "" Then
                                Dim InheritInstance As New McInstance(InstanceNameInheritFrom)
                                '继续循环
                                If InheritInstance.InheritName = InstanceNameInheritFrom Then Throw New Exception("版本依赖项出现嵌套：" & InstanceNameInheritFrom)
                                InstanceNameInheritFrom = InheritInstance.InheritName
                                '合并 Libraries 项：子版本放在前面，父版本放在后面（5978#：如果多个 jar 包中含有相同的类，Java 8 和之前的版本按照 -cp 指定的顺序选择第一个）
                                Dim CurrentLib As JArray = _JsonObject("libraries").DeepClone()
                                For Each LibToken In InheritInstance.JsonObject("libraries")
                                    CurrentLib.Add(LibToken)
                                Next
                                '合并其他项：子版本优先于父版本
                                InheritInstance.JsonObject.Merge(_JsonObject)
                                _JsonObject = InheritInstance.JsonObject
                                _JsonObject("libraries") = CurrentLib
                                GoTo Recheck
                            End If
                        Catch ex As Exception
                            Log(ex, "合并版本依赖项 JSON 失败（" & If(InstanceNameInheritFrom, "null").ToString & "）")
                        End Try
                    Catch ex As Exception
                        Throw New Exception("初始化版本 JSON 时失败（" & If(Name, "null") & "）", ex)
                    End Try
                End If
                Return _JsonObject
            End Get
            Set(value As JObject)
                _JsonObject = value
            End Set
        End Property
        Private _JsonObject As JObject = Nothing
        ''' <summary>
        ''' 是否为旧版 Json 格式。
        ''' </summary>
        Public ReadOnly Property IsOldJson As Boolean
            Get
                Return JsonObject("minecraftArguments") IsNot Nothing AndAlso JsonObject("minecraftArguments") <> ""
            End Get
        End Property
        ''' <summary>
        ''' Json 是否为 HMCL 格式。
        ''' </summary>
        Public Property IsHmclFormatJson As Boolean = False

        ''' <summary>
        ''' 版本 jar 中的 version.json 文件对象。
        ''' 若没有则返回 Nothing。
        ''' </summary>
        Public ReadOnly Property JsonVersion As JObject
            Get
                Static JsonVersionInited As Boolean = False
                If Not JsonVersionInited Then
                    JsonVersionInited = True
                    Try
                        If Not File.Exists($"{PathVersion}{Name}.jar") Then Exit Try
                        Using JarArchive As New ZipArchive(New FileStream(PathVersion & Name & ".jar", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            Dim VersionJson As ZipArchiveEntry = JarArchive.GetEntry("version.json")
                            If VersionJson Is Nothing Then Exit Try
                            Using VersionJsonStream As New StreamReader(VersionJson.Open)
                                _JsonVersion = GetJson(VersionJsonStream.ReadToEnd)
                            End Using
                        End Using
                    Catch ex As Exception
                        Log(ex, $"从版本 jar 中读取 version.json 失败（{PathVersion}{Name}.jar）")
                    End Try
                End If
                Return _JsonVersion
            End Get
        End Property
        Private _JsonVersion As JObject = Nothing

        ''' <summary>
        ''' 该版本的依赖版本。若无依赖版本则为空字符串。
        ''' </summary>
        Public ReadOnly Property InheritName As String
            Get
                If _InheritName Is Nothing Then
                    _InheritName = If(JsonObject("inheritsFrom"), "").ToString
                    '由于过老的 LiteLoader 中没有 Inherits（例如 1.5.2），需要手动判断以获取真实继承版本
                    '此外，由于这里的加载早于版本种类判断，所以需要手动判断是否为 LiteLoader
                    '如果版本提供了不同的 Jar，代表所需的 Jar 可能已被更改，则跳过 Inherit 替换
                    If JsonText.Contains("liteloader") AndAlso Version.VanillaName <> Name AndAlso Not JsonText.Contains("logging") Then
                        If If(JsonObject("jar"), Version.VanillaName).ToString = Version.VanillaName Then _InheritName = Version.VanillaName
                    End If
                    'HMCL 版本无 Json
                    If IsHmclFormatJson Then _InheritName = ""
                End If
                Return _InheritName
            End Get
        End Property
        Private _InheritName As String = Nothing

        ''' <summary>
        ''' 从版本名，或版本文件夹的完整路径初始化（不规定是否以 \ 结尾）。
        ''' </summary>
        Public Sub New(Name As String)
            PathVersion = If(Name.Contains(":"), "", McFolderSelected & "versions\") & '补全完整路径
                      Name &
                      If(Name.EndsWithF("\"), "", "\") '补全右划线
        End Sub

        ''' <summary>
        ''' 检查 Minecraft 版本，若检查通过 State 则为 Original 且返回 True。
        ''' </summary>
        Public Function Check() As Boolean

            '检查文件夹
            If Not Directory.Exists(PathVersion) Then
                State = McInstanceState.Error
                Info = "未找到版本 " & Name
                Return False
            End If
            '检查权限
            Try
                Directory.CreateDirectory(PathVersion & "PCL\")
                CheckPermissionWithException(PathVersion & "PCL\")
            Catch ex As Exception
                State = McInstanceState.Error
                Info = "PCL 没有对该文件夹的访问权限，请右键以管理员身份运行 PCL"
                Log(ex, "没有访问版本文件夹的权限")
                Return False
            End Try
            '确认 Json 可用性
            Try
                Dim JsonObjCheck = JsonObject
            Catch ex As Exception
                Log(ex, "版本 JSON 可用性检查失败（" & PathVersion & "）")
                JsonText = ""
                JsonObject = Nothing
                Info = ex.Message
                State = McInstanceState.Error
                Return False
            End Try
            '检查版本号获取
            Try
                If String.IsNullOrEmpty(Version.VanillaName) Then Throw New Exception("无法获取版本号，结果为空")
            Catch ex As Exception
                Log(ex, "版本号获取失败（" & Name & "）")
                State = McInstanceState.Error
                Info = "版本号获取失败：" & ex.GetBrief()
                Return False
            End Try
            '检查依赖版本
            Try
                If Not InheritName = "" Then
                    If Not File.Exists(GetPathFromFullPath(PathVersion) & InheritName & "\" & InheritName & ".json") Then
                        State = McInstanceState.Error
                        Info = "需要安装 " & InheritName & " 作为前置版本"
                        Return False
                    End If
                End If
            Catch ex As Exception
                Log(ex, "依赖版本检查出错（" & Name & "）")
                State = McInstanceState.Error
                Info = "未知错误：" & ex.GetBrief()
                Return False
            End Try

            State = McInstanceState.Original
            Return True
        End Function
        ''' <summary>
        ''' 加载 Minecraft 版本的详细信息。不使用其缓存，且会更新缓存。
        ''' </summary>
        Public Function Load() As McInstance
            Try
                '检查版本，若出错则跳过数据确定阶段
                If Not Check() Then GoTo ExitDataLoad
#Region "确定版本分类"
                Select Case Version.VanillaName '在获取 Version.Original 对象时会完成它的加载
                    Case "Unknown"
                        State = McInstanceState.Error
                    Case "Old"
                        State = McInstanceState.Old
                    Case Else '根据 API 进行筛选
                        Dim RealJson As String = If(JsonObject, JsonText).ToString
                        '愚人节与快照版本
                        If If(JsonObject("type"), "").ToString = "fool" OrElse GetMcFoolName(Version.VanillaName) <> "" Then
                            State = McInstanceState.Fool
                        ElseIf IsSnapshot() Then
                            State = McInstanceState.Snapshot
                        End If
                        'OptiFine
                        If RealJson.Contains("optifine") Then
                            State = McInstanceState.OptiFine
                            Version.HasOptiFine = True
                            Version.OptiFine = If(RegexSeek(RealJson, "(?<=HD_U_)[^"":/]+"), "未知版本")
                        End If
                        'LiteLoader
                        If RealJson.Contains("liteloader") Then
                            State = McInstanceState.LiteLoader
                            Version.HasLiteLoader = True
                        End If
                        'Fabric、Forge
                        If RealJson.Contains("net.fabricmc:fabric-loader") OrElse RealJson.Contains("org.quiltmc:quilt-loader") Then
                            State = McInstanceState.Fabric
                            Version.HasFabric = True
                            Version.Fabric = If(RegexSeek(RealJson, "(?<=(net.fabricmc:fabric-loader:)|(org.quiltmc:quilt-loader:))[0-9\.]+(\+build.[0-9]+)?"), "未知版本").Replace("+build", "")
                        ElseIf RealJson.Contains("minecraftforge") AndAlso Not RealJson.Contains("net.neoforge") Then
                            State = McInstanceState.Forge
                            Version.HasForge = True
                            Version.Forge = RegexSeek(RealJson, "(?<=forge:[0-9\.]+(_pre[0-9]*)?\-)[0-9\.]+")
                            If Version.Forge Is Nothing Then Version.Forge = RegexSeek(RealJson, "(?<=net\.minecraftforge:minecraftforge:)[0-9\.]+")
                            If Version.Forge Is Nothing Then Version.Forge = If(RegexSeek(RealJson, "(?<=net\.minecraftforge:fmlloader:[0-9\.]+-)[0-9\.]+"), "未知版本")
                        ElseIf RealJson.Contains("net.neoforge") Then
                            '1.20.1 JSON 范例："--fml.forgeVersion", "47.1.99"
                            '1.20.2+ JSON 范例："--fml.neoForgeVersion", "20.6.119-beta"
                            State = McInstanceState.NeoForge
                            Version.HasNeoForge = True
                            Version.NeoForge = If(RegexSeek(RealJson, "(?<=orgeVersion"",[^""]*?"")[^""]+(?="",)"), "未知版本")
                        End If
                End Select
#End Region
ExitDataLoad:
                '确定版本图标
                Logo = ReadIni(PathVersion & "PCL\Setup.ini", "Logo", "")
                If Logo = "" OrElse Not CType(ReadIni(PathVersion & "PCL\Setup.ini", "LogoCustom", False), Boolean) Then
                    Select Case State
                        Case McInstanceState.Original
                            Logo = PathImage & "Blocks/Grass.png"
                        Case McInstanceState.Snapshot
                            Logo = PathImage & "Blocks/CommandBlock.png"
                        Case McInstanceState.Old
                            Logo = PathImage & "Blocks/CobbleStone.png"
                        Case McInstanceState.Forge
                            Logo = PathImage & "Blocks/Anvil.png"
                        Case McInstanceState.NeoForge
                            Logo = PathImage & "Blocks/NeoForge.png"
                        Case McInstanceState.Fabric
                            Logo = PathImage & "Blocks/Fabric.png"
                        Case McInstanceState.OptiFine
                            Logo = PathImage & "Blocks/GrassPath.png"
                        Case McInstanceState.LiteLoader
                            Logo = PathImage & "Blocks/Egg.png"
                        Case McInstanceState.Fool
                            Logo = PathImage & "Blocks/GoldBlock.png"
                        Case Else
                            Logo = PathImage & "Blocks/RedstoneBlock.png"
                    End Select
                End If
                '确定版本描述
                Dim CustomInfo As String = ReadIni(PathVersion & "PCL\Setup.ini", "CustomInfo")
                If CustomInfo <> "" Then
                    Info = CustomInfo
                Else
                    Info = VersionDisplayName()
                    If State = McInstanceState.Fool Then
                        Info = GetMcFoolName(Version.VanillaName)
                    ElseIf State <> McInstanceState.Error Then
                        If Setup.Get("VersionServerLogin", Instance:=Me) = 3 Then Info += ", 统一通行证验证"
                        If Setup.Get("VersionServerLogin", Instance:=Me) = 4 Then Info += ", Authlib 验证"
                    End If
                End If
                '确定版本收藏状态
                IsStar = ReadIni(PathVersion & "PCL\Setup.ini", "IsStar", False)
                '确定版本显示种类
                DisplayType = ReadIni(PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
                '写入缓存
                If Directory.Exists(PathVersion) Then
                    WriteIni(PathVersion & "PCL\Setup.ini", "State", State)
                    WriteIni(PathVersion & "PCL\Setup.ini", "Info", Info)
                    WriteIni(PathVersion & "PCL\Setup.ini", "Logo", Logo)
                End If
                If State <> McInstanceState.Error AndAlso Version.Reliable Then
                    WriteIni(PathVersion & "PCL\Setup.ini", "ReleaseTime", ReleaseTime.ToString("yyyy'-'MM'-'dd HH':'mm"))
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionFabric", Version.Fabric)
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionOptiFine", Version.OptiFine)
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionLiteLoader", Version.HasLiteLoader)
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionForge", Version.Forge)
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionNeoForge", Version.NeoForge)
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionVanillaName", Version.VanillaName)
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionVanilla", Version.Vanilla.ToString)
                End If
            Catch ex As Exception
                Info = "未知错误：" & ex.GetBrief()
                Logo = PathImage & "Blocks/RedstoneBlock.png"
                State = McInstanceState.Error
                Log(ex, "加载版本失败（" & Name & "）", LogLevel.Feedback)
            Finally
                IsLoaded = True
            End Try
            Return Me
        End Function
        Private Function IsSnapshot() As Boolean
            Return {"w", "snapshot", "rc", "pre", "experimental", "-"}.Any(Function(s) Version.VanillaName.ContainsF(s, True)) OrElse
                Name.ContainsF("combat", True) OrElse
                If(JsonObject("type"), "").ToString = "snapshot" OrElse If(JsonObject("type"), "").ToString = "pending"
        End Function

        Public IsLoaded As Boolean = False
        ''' <summary>
        ''' 获取对版本信息的简短描述。
        ''' 例如 “快照 16w01a”、“原版 1.12.2, Forge 1.2.3”、“愚人节版本 2.0”。
        ''' </summary>
        Public Function VersionDisplayName() As String
            'Mod Loader 信息
            Dim ModLoaderInfo As String = ""
            If Version.HasForge Then ModLoaderInfo += ", Forge" & If(Version.Forge = "未知版本", "", " " & Version.Forge)
            If Version.HasNeoForge Then ModLoaderInfo += ", NeoForge" & If(Version.NeoForge = "未知版本", "", " " & Version.NeoForge)
            If Version.HasFabric Then ModLoaderInfo += ", Fabric" & If(Version.Fabric = "未知版本", "", " " & Version.Fabric)
            If Version.HasOptiFine Then ModLoaderInfo += ", OptiFine" & If(Version.OptiFine = "未知版本", "", " " & Version.OptiFine.Replace("-", " ").Replace("_", " "))
            If Version.HasLiteLoader Then ModLoaderInfo += ", LiteLoader"
            '基础信息
            Dim Info As String
            Select Case State
                Case McInstanceState.Snapshot, McInstanceState.Original, McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.Fabric, McInstanceState.OptiFine, McInstanceState.LiteLoader
                    If Version.VanillaName.ContainsF("pre", True) Then
                        Info = "预发布版 " & Version.VanillaName
                    ElseIf Version.VanillaName.ContainsF("rc", True) Then
                        Info = "发布候选 " & Version.VanillaName
                    ElseIf Version.VanillaName.Contains("experimental") Then
                        Info = "实验性快照" & Version.VanillaName
                    ElseIf Version.VanillaName = "pending" Then
                        Info = "实验性快照"
                    ElseIf IsSnapshot Then
                        Info = If(Version.Reliable, "快照版 " & Version.VanillaName.Replace("-snapshot", ""), "快照版")
                    Else
                        Info = If(Version.Reliable, "正式版 " & Version.VanillaName, "正式版")
                    End If
                Case McInstanceState.Old
                    Info = "远古版本"
                Case McInstanceState.Fool
                    Info = "愚人节版本 " & Version.VanillaName
                Case McInstanceState.Error
                    Return Me.Info '已有错误信息
                Case Else
                    Return "发生了未知错误，请向作者反馈此问题"
            End Select
            Return (Info & ModLoaderInfo).Replace("_", "-")
        End Function

        Public Function ToListItem(Optional ContentHandler As Action(Of MyListItem, EventArgs) = Nothing) As MyVirtualizingElement(Of MyListItem)
            Return New MyVirtualizingElement(Of MyListItem)(
            Function()
                Dim NewItem As New MyListItem With {
                    .Info = Info, .Height = 42, .Tag = Me, .SnapsToDevicePixels = True, .Type = MyListItem.CheckType.Clickable,
                    .ContentHandler = ContentHandler
                }
                '标题
                NewItem.Inlines.Clear()
                NewItem.Inlines.Add(New Run(Name))
                If ReadIni(PathVersion & "PCL\Setup.ini", "CustomInfo") <> "" Then '如果版本设置了自定义描述，在标题后面以淡灰色显示其版本号
                    NewItem.Inlines.Add(New Run("  |  " & VersionDisplayName()) With {.Foreground = New MyColor(215, 215, 215), .FontSize = 12})
                End If
                'Logo
                Try
                    If Logo.EndsWith("PCL\Logo.png") Then
                        NewItem.Logo = PathVersion & "PCL\Logo.png" '修复老版本中，存储的自定义 Logo 使用完整路径，导致移动后无法加载的 Bug
                    Else
                        NewItem.Logo = Logo
                    End If
                Catch ex As Exception
                    Log(ex, "加载版本图标失败", LogLevel.Hint)
                    NewItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png"
                End Try
                Return NewItem
            End Function) With {.Height = 42}
        End Function

        '运算符支持
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim version = TryCast(obj, McInstance)
            Return version IsNot Nothing AndAlso PathVersion = version.PathVersion
        End Function
        Public Shared Operator =(a As McInstance, b As McInstance) As Boolean
            If a Is Nothing AndAlso b Is Nothing Then Return True
            If a Is Nothing OrElse b Is Nothing Then Return False
            Return a.PathVersion = b.PathVersion
        End Operator
        Public Shared Operator <>(a As McInstance, b As McInstance) As Boolean
            Return Not (a = b)
        End Operator
    End Class
    Public Enum McInstanceState
        [Error]
        Original
        Snapshot
        Fool
        OptiFine
        Old
        Forge
        NeoForge
        LiteLoader
        Fabric
    End Enum

    ''' <summary>
    ''' Minecraft 版本号和附加组件信息。
    ''' </summary>
    Public Class McVersion

        '原版

        ''' <summary>
        ''' 原版版本名。
        ''' 如 26.1，26.1-snapshot-1，1.12.2，16w01a。
        ''' </summary>
        Public VanillaName As String
        ''' <summary>
        ''' 可比较的三段式原版版本号。
        ''' 对老版本格式，例如 1.20.3，会被转换为 20.0.3。
        ''' 若没有版本号，例如旧快照，则为 9999.0.0。
        ''' </summary>
        Public Vanilla As Version
        ''' <summary>
        ''' 指示原版版本号是否可靠（不是通过猜测获取）。
        ''' </summary>
        Public Reliable As Boolean = True
        ''' <summary>
        ''' 原版版本号是否有效。
        ''' </summary>
        Public ReadOnly Property Vaild As Boolean
            Get
                Return Vanilla.Major < 1000
            End Get
        End Property
        ''' <summary>
        ''' 可供比较的原版 Drop 序数。
        ''' 例如 26.3.2 为 263，1.21.5 为 210。
        ''' 若没有版本号，例如旧快照，则直接指定为 209。
        ''' </summary>
        Public ReadOnly Property Drop As Integer
            Get
                Return If(Vaild, Vanilla.Major * 10 + Vanilla.Minor, 209)
            End Get
        End Property

        'OptiFine

        ''' <summary>
        ''' 该版本是否通过 JSON 安装了 OptiFine。
        ''' </summary>
        Public HasOptiFine As Boolean = False
        ''' <summary>
        ''' OptiFine 版本名，如 C8、C9_pre10。
        ''' </summary>
        Public OptiFine As String = ""
        ''' <summary>
        ''' 可供比较的 OptiFine 版本序数。
        ''' </summary>
        Public ReadOnly Property OptiFineCode As Integer
            Get
                If String.IsNullOrEmpty(OptiFine) OrElse OptiFine = "未知版本" Then Return 0
                '字母编号，如 G2 中的 G（7）
                Dim Result As Integer = Asc(OptiFine.ToUpper.First) - Asc("A"c) + 1
                '末尾数字，如 C5 beta4 中的 5
                Result *= 100
                Result += Val(RegexSeek(Right(OptiFine, OptiFine.Length - 1), "[0-9]+"))
                '测试标记（正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）
                Result *= 100
                If OptiFine.ContainsF("pre", True) Then Result += 50
                If OptiFine.ContainsF("pre", True) OrElse OptiFine.ContainsF("beta", True) Then
                    If Val(Right(OptiFine, 1)) = 0 AndAlso Right(OptiFine, 1) <> "0" Then
                        Result += 1 '为 pre 或 beta 结尾，视作 1
                    Else
                        Result += Val(RegexSeek(OptiFine.ToLower, "(?<=((pre)|(beta)))[0-9]+"))
                    End If
                Else
                    Result += 99
                End If
                Return Result
            End Get
        End Property

        'Forge/NeoForge

        ''' <summary>
        ''' 该版本是否安装了 Forge。
        ''' </summary>
        Public HasForge As Boolean = False
        ''' <summary>
        ''' 该版本是否安装了 NeoForge。
        ''' </summary>
        Public HasNeoForge As Boolean = False
        ''' <summary>
        ''' 该版本是否安装了 Forge 或 NeoForge。
        ''' </summary>
        Public ReadOnly Property HasForgelike As Boolean
            Get
                Return HasForge OrElse HasNeoForge
            End Get
        End Property
        ''' <summary>
        ''' Forge 版本名，如 31.1.2、14.23.5.2847。
        ''' </summary>
        Public Forge As String = ""
        ''' <summary>
        ''' NeoForge 版本名，如 21.0.2-beta、47.1.79。
        ''' </summary>
        Public NeoForge As String = ""
        ''' <summary>
        ''' 可供比较的类 Forge 版本序数。
        ''' </summary>
        Public ReadOnly Property ForgelikeCode As Integer
            Get
                If Not HasForgelike Then Return 0
                If (String.IsNullOrEmpty(Forge) OrElse Forge = "未知版本") AndAlso
                   (String.IsNullOrEmpty(NeoForge) OrElse NeoForge = "未知版本") Then Return 0
                Dim Segments = RegexSearch(If(HasForge, Forge, NeoForge), "\d+")
                Select Case Segments.Count
                    Case Is > 4
                        Return Val(Segments(0)) * 1000000 + Val(Segments(1)) * 10000 + Val(Segments(3))
                    Case 3
                        Return Val(Segments(0)) * 1000000 + Val(Segments(1)) * 10000 + Val(Segments(2))
                    Case 2
                        Return Val(Segments(0)) * 1000000 + Val(Segments(1)) * 10000
                    Case Else
                        Return Val(Segments(0)) * 1000000
                End Select
            End Get
        End Property

        'Fabric

        ''' <summary>
        ''' 该版本是否安装了 Fabric。
        ''' </summary>
        Public HasFabric As Boolean = False
        ''' <summary>
        ''' Fabric 版本名，如 0.7.2.175。
        ''' </summary>
        Public Fabric As String = ""

        'LiteLoader

        ''' <summary>
        ''' 该版本是否安装了 LiteLoader。
        ''' </summary>
        Public HasLiteLoader As Boolean = False

        'Helpers

        ''' <summary>
        ''' 版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。
        ''' </summary>
        Public Shared Function IsFormatFit(Version As String) As Boolean
            If Version Is Nothing Then Return False
            If RegexCheck(Version, "^1\.\d") Then Return True
            If Val(RegexSeek(Version, "^[2-9]\d\.\d+")) > 25 Then Return True
            Return False
        End Function
        ''' <summary>
        ''' 尝试将版本字符串转换为 Drop 序数。
        ''' 若无法转换则返回 0。
        ''' </summary>
        Public Shared Function VersionToDrop(Version As String, Optional AllowSnapshot As Boolean = False) As Integer
            If Not AllowSnapshot AndAlso Version.Contains("-") Then Return 0
            If Version Is Nothing Then Return 0
            Dim Segments = Version.BeforeFirst("-").Split(".")
            If Segments.Length < 2 Then Return 0
            Dim Major As Integer = Val(Segments(0))
            Dim Minor As Integer = Val(Segments(1))
            If Major = 1 Then
                Return Minor * 10
            ElseIf Major < 25 Then
                Return 0
            Else
                Return Major * 10 + Minor
            End If
        End Function
        ''' <summary>
        ''' 将 Drop 序数转换为版本字符串。
        ''' </summary>
        Public Shared Function DropToVersion(Drop As Integer) As String
            If Drop >= 250 Then
                Return $"{Drop \ 10}.{Drop Mod 10}"
            Else
                Return $"1.{Drop \ 10}"
            End If
        End Function

    End Class

    ''' <summary>
    ''' 根据版本名获取对应的愚人节版本描述。非愚人节版本会返回空字符串。
    ''' </summary>
    Public Function GetMcFoolName(Name As String) As String
        Name = Name.ToLower
        If Name.StartsWithF("2.0") Then
            Return "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！"
        ElseIf Name = "15w14a" Then
            Return "2015 | 作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。"
        ElseIf Name = "1.rv-pre1" Then
            Return "2016 | 是时候将现代科技带入 Minecraft 了！"
        ElseIf Name = "3d shareware v1.34" Then
            Return "2019 | 我们从地下室的废墟里找到了这个开发于 1994 年的杰作！"
        ElseIf Name.StartsWithF("20w14inf") OrElse Name = "20w14∞" Then
            Return "2020 | 我们加入了 20 亿个新的维度，让无限的想象变成了现实！"
        ElseIf Name = "22w13oneblockatatime" Then
            Return "2022 | 一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！"
        ElseIf Name = "23w13a_or_b" Then
            Return "2023 | 研究表明：玩家喜欢作出选择——越多越好！"
        ElseIf Name = "24w14potato" Then
            Return "2024 | 毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！"
        ElseIf Name = "25w14craftmine" Then
            Return "2025 | 你可以合成任何东西——包括合成你的世界！"
        Else
            Return ""
        End If
    End Function

    ''' <summary>
    ''' 当前按卡片分类的所有版本列表。
    ''' </summary>
    Public McInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))

#End Region

#Region "版本列表加载"

    ''' <summary>
    ''' 是否要求本次加载强制刷新版本列表。
    ''' </summary>
    Public McInstanceListForceRefresh As Boolean = False
    ''' <summary>
    ''' 是否为本次打开 PCL 后第一次加载版本列表。
    ''' 这会清理所有 .pclignore 文件，而非跳过这些对应版本。
    ''' </summary>
    Private IsFirstMcInstanceListLoad As Boolean = True

    ''' <summary>
    ''' 加载 Minecraft 文件夹的版本列表。
    ''' </summary>
    Public McInstanceListLoader As New LoaderTask(Of String, Integer)("Minecraft Instance List", AddressOf InitMcInstanceList) With {.ReloadTimeout = 1}
    Private Sub InitMcInstanceList(Loader As LoaderTask(Of String, Integer))
        '开始加载
        Dim PathMc As String = Loader.Input
        Try
            '初始化
            McInstanceList = New Dictionary(Of McInstanceCardType, List(Of McInstance))

            '检测缓存是否需要更新
            Dim FolderList As New List(Of String)
            If Directory.Exists(PathMc & "versions") Then '不要使用 CheckPermission，会导致写入时间改变，从而使得文件夹被强制刷新
                Try
                    For Each Folder As DirectoryInfo In New DirectoryInfo(PathMc & "versions").GetDirectories
                        FolderList.Add(Folder.Name)
                    Next
                Catch ex As Exception
                    Throw New Exception("无法读取版本文件夹，可能是由于没有权限（" & PathMc & "versions）", ex)
                End Try
            End If
            '不可用
            If Not FolderList.Any() Then
                WriteIni(PathMc & "PCL.ini", "InstanceCache", "") '清空缓存
                GoTo OnLoaded
            End If
            '有可用版本
            Dim FolderListCheck As Integer = GetHash(McInstanceCacheVersion & "#" & Join(FolderList.ToArray, "#")) Mod (Integer.MaxValue - 1) '根据文件夹名列表生成辨识码
            If Not McInstanceListForceRefresh AndAlso Val(ReadIni(PathMc & "PCL.ini", "InstanceCache")) = FolderListCheck Then
                '可以使用缓存
                Dim Result = InitMcInstanceListWithCache(PathMc)
                If Result Is Nothing Then
                    GoTo Reload
                Else
                    McInstanceList = Result
                End If
            Else
                '文件夹列表不符
Reload:
                McInstanceListForceRefresh = False
                Log("[Minecraft] 文件夹列表变更，重载所有版本")
                WriteIni(PathMc & "PCL.ini", "InstanceCache", FolderListCheck)
                McInstanceList = InitMcInstanceListWithoutCache(PathMc)
            End If
            IsFirstMcInstanceListLoad = False

            '改变当前选择的版本
OnLoaded:
            If Loader.IsAborted Then Return
            If McInstanceList.Any(Function(v) v.Key <> McInstanceCardType.Error) Then
                '尝试读取已储存的选择
                Dim SavedSelection As String = ReadIni(PathMc & "PCL.ini", "Version")
                If SavedSelection <> "" Then
                    For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList
                        For Each Instance As McInstance In Card.Value
                            If Instance.Name = SavedSelection AndAlso Not Instance.State = McInstanceState.Error Then
                                '使用已储存的选择
                                McInstanceSelected = Instance
                                Setup.Set("LaunchVersionSelect", McInstanceSelected.Name)
                                Log("[Minecraft] 选择该文件夹储存的 Minecraft 版本：" & McInstanceSelected.PathVersion)
                                Return
                            End If
                        Next
                    Next
                End If
                If Not McInstanceList.First.Value(0).State = McInstanceState.Error Then
                    '自动选择第一项
                    McInstanceSelected = McInstanceList.First.Value(0)
                    Setup.Set("LaunchVersionSelect", McInstanceSelected.Name)
                    Log("[Launch] 自动选择 Minecraft 版本：" & McInstanceSelected.PathVersion)
                End If
            Else
                McInstanceSelected = Nothing
                Setup.Set("LaunchVersionSelect", "")
                Log("[Minecraft] 未找到可用 Minecraft 版本")
            End If
            If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomInteger(200, 3000))
        Catch ex As ThreadInterruptedException
        Catch ex As Exception
            If Loader.IsAborted Then Return '#5617
            WriteIni(PathMc & "PCL.ini", "InstanceCache", "") '要求下次重新加载
            Log(ex, "加载 .minecraft 版本列表失败", LogLevel.Feedback)
        End Try
    End Sub

    '获取版本列表
    Private Function InitMcInstanceListWithCache(Path As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim Results As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        Try
            Dim CardCount As Integer = ReadIni(Path & "PCL.ini", "CardCount", -1)
            If CardCount = -1 Then Return Nothing
            For i = 0 To CardCount - 1
                Dim CardType As McInstanceCardType = ReadIni(Path & "PCL.ini", "CardKey" & (i + 1), ":")
                Dim InstanceList As New List(Of McInstance)

                '循环读取版本
                For Each Folder As String In ReadIni(Path & "PCL.ini", "CardValue" & (i + 1), ":").Split(":")
                    If Folder = "" Then Continue For
                    Dim FolderVersions As String = $"{Path}versions\{Folder}\"
                    If File.Exists(FolderVersions & ".pclignore") Then
                        If IsFirstMcInstanceListLoad Then
                            Log("[Minecraft] 清理残留的忽略项目：" & FolderVersions) '#2781
                            File.Delete(FolderVersions & ".pclignore")
                        Else
                            Log("[Minecraft] 跳过要求忽略的项目：" & FolderVersions)
                            Continue For
                        End If
                    End If
                    Try

                        '读取单个版本
                        Dim Instance As New McInstance(FolderVersions)
                        InstanceList.Add(Instance)
                        Instance.Info = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "CustomInfo", "")
                        If Instance.Info = "" Then Instance.Info = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "Info", Instance.Info)
                        Instance.Logo = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "Logo", Instance.Logo)
                        Instance.ReleaseTime = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "ReleaseTime", Instance.ReleaseTime)
                        Instance.State = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "State", Instance.State)
                        Instance.IsStar = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "IsStar", False)
                        Instance.DisplayType = ReadIni(Path & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
                        If Instance.State <> McInstanceState.Error AndAlso
                           ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionVanillaName", "Unknown") <> "Unknown" Then
                            '读取版本信息
                            Dim VersionInfo As New McVersion With {
                                .Fabric = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionFabric", ""),
                                .Forge = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionForge", ""),
                                .NeoForge = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionNeoForge", ""),
                                .OptiFine = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionOptiFine", ""),
                                .HasLiteLoader = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionLiteLoader", False),
                                .VanillaName = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionVanillaName", "Unknown"),
                                .Vanilla = New Version(ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionVanilla", "0.0.0"))
                            }
                            VersionInfo.HasFabric = VersionInfo.Fabric.Any()
                            VersionInfo.HasForge = VersionInfo.Forge.Any()
                            VersionInfo.HasNeoForge = VersionInfo.NeoForge.Any()
                            VersionInfo.HasOptiFine = VersionInfo.OptiFine.Any()
                            Instance.Version = VersionInfo
                        Else
                            Instance.Load()
                        End If

                        '重新检查错误版本
                        If Instance.State = McInstanceState.Error Then
                            '重新尝试获取版本信息
                            Dim OldDesc As String = Instance.Info
                            Instance.State = McInstanceState.Original
                            Instance.Check()
                            '校验错误原因是否改变
                            Dim CustomInfo As String = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "CustomInfo")
                            If Instance.State = McInstanceState.Original OrElse (CustomInfo = "" AndAlso Not OldDesc = Instance.Info) Then
                                Log("[Minecraft] 版本 " & Instance.Name & " 的错误状态已变更，新的状态为：" & Instance.Info)
                                Return Nothing
                            End If
                        End If

                        '校验未加载的版本
                        If Instance.Logo = "" Then
                            Log("[Minecraft] 版本 " & Instance.Name & " 未被加载")
                            Return Nothing
                        End If

                    Catch ex As Exception
                        Log(ex, "读取版本加载缓存失败（" & Folder & "）", LogLevel.Debug)
                        Return Nothing
                    End Try
                Next

                If InstanceList.Any Then Results.Add(CardType, InstanceList)
            Next
            Return Results
        Catch ex As Exception
            Log(ex, "读取版本缓存失败")
            Return Nothing
        End Try
    End Function
    Private Function InitMcInstanceListWithoutCache(Path As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim InstanceList As New List(Of McInstance)

#Region "循环加载每个版本的信息"
        For Each Folder As DirectoryInfo In New DirectoryInfo(Path & "versions").GetDirectories
            If Not Folder.Exists OrElse Not Folder.EnumerateFiles.Any Then
                Log("[Minecraft] 跳过空文件夹：" & Folder.FullName)
                Continue For
            End If
            If (Folder.Name = "cache" OrElse Folder.Name = "BLClient" OrElse Folder.Name = "PCL") AndAlso Not File.Exists(Folder.FullName & "\" & Folder.Name & ".json") Then
                Log("[Minecraft] 跳过可能不是版本文件夹的项目：" & Folder.FullName)
                Continue For
            End If
            Dim VersionFolder As String = Folder.FullName & "\"
            If File.Exists(VersionFolder & ".pclignore") Then
                If IsFirstMcInstanceListLoad Then
                    Log("[Minecraft] 清理残留的忽略项目：" & VersionFolder) '#2781
                    File.Delete(VersionFolder & ".pclignore")
                Else
                    Log("[Minecraft] 跳过要求忽略的项目：" & VersionFolder)
                    Continue For
                End If
            End If
            Dim Instance As New McInstance(VersionFolder)
            InstanceList.Add(Instance)
            Instance.Load()
        Next
#End Region

        Dim Results As New Dictionary(Of McInstanceCardType, List(Of McInstance))

#Region "将版本分类到各个卡片"
        Try

            '未经过自定义的版本列表
            Dim InstanceListOriginal As New Dictionary(Of McInstanceCardType, List(Of McInstance))

            '单独列出收藏的版本
            Dim InstanceStar As New List(Of McInstance)
            For Each Instance As McInstance In InstanceList.ToList
                If Not Instance.IsStar Then Continue For
                If Instance.DisplayType = McInstanceCardType.Hidden Then Continue For
                InstanceStar.Add(Instance)
                InstanceList.Remove(Instance)
            Next
            If InstanceStar.Any Then InstanceListOriginal.Add(McInstanceCardType.Star, InstanceStar)

            '预先筛选出愚人节和错误的版本
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstanceState.Error}, McInstanceCardType.Error)
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstanceState.Fool}, McInstanceCardType.Fool)

            '筛选 API 版本
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.LiteLoader, McInstanceState.Fabric}, McInstanceCardType.API)

            '将老版本预先分类入不常用，只剩余原版、快照、OptiFine
            Dim InstanceUseful As New List(Of McInstance)
            Dim InstanceRubbish As New List(Of McInstance)
            McInstanceFilter(InstanceList, {McInstanceState.Old}, InstanceRubbish)

            '确认最新版本，若为快照则加入常用列表
            Dim LargestInstance As McInstance = InstanceList.
                Where(Function(v) v.State = McInstanceState.Original OrElse v.State = McInstanceState.Snapshot).
                MaxOf(Function(v) v.ReleaseTime)
            If LargestInstance IsNot Nothing AndAlso LargestInstance.State = McInstanceState.Snapshot Then
                InstanceUseful.Add(LargestInstance)
                InstanceList.Remove(LargestInstance)
            End If

            '将剩余的快照全部拖进不常用列表
            McInstanceFilter(InstanceList, {McInstanceState.Snapshot}, InstanceRubbish)

            '获取每个 Drop 下最新的原版与 OptiFine
            Dim NewerInstance As New Dictionary(Of String, McInstance)
            Dim ExistDrops As New List(Of Integer)
            For Each Instance As McInstance In InstanceList
                If Not Instance.Version.Vaild Then Continue For
                If Not ExistDrops.Contains(Instance.Version.Drop) Then ExistDrops.Add(Instance.Version.Drop)
                Dim Key As String = Instance.Version.Drop & "-" & Instance.State
                If Not NewerInstance.ContainsKey(Key) Then
                    NewerInstance.Add(Key, Instance)
                    Continue For
                End If
                If Instance.Version.HasOptiFine Then
                    If Instance.Version.OptiFineCode > NewerInstance(Key).Version.OptiFineCode Then NewerInstance(Key) = Instance 'OptiFine 根据版本号判断
                Else
                    If Instance.ReleaseTime > NewerInstance(Key).ReleaseTime Then NewerInstance(Key) = Instance '原版根据发布时间判断
                End If
            Next

            '将每个 Drop 下的最常规版本加入
            For Each Drop As Integer In ExistDrops
                If NewerInstance.ContainsKey(Drop & "-" & McInstanceState.OptiFine) AndAlso NewerInstance.ContainsKey(Drop & "-" & McInstanceState.Original) Then
                    '同时存在 OptiFine 与原版
                    Dim VanillaInstance As McInstance = NewerInstance(Drop & "-" & McInstanceState.Original)
                    Dim OptiFineInstance As McInstance = NewerInstance(Drop & "-" & McInstanceState.OptiFine)
                    If VanillaInstance.Version.Drop > OptiFineInstance.Version.Drop Then
                        '仅在原版比 OptiFine 更新时才加入原版
                        InstanceUseful.Add(VanillaInstance)
                        InstanceList.Remove(VanillaInstance)
                    End If
                    InstanceUseful.Add(OptiFineInstance)
                    InstanceList.Remove(OptiFineInstance)
                ElseIf NewerInstance.ContainsKey(Drop & "-" & McInstanceState.OptiFine) Then
                    '没有原版，直接加入 OptiFine
                    InstanceUseful.Add(NewerInstance(Drop & "-" & McInstanceState.OptiFine))
                    InstanceList.Remove(NewerInstance(Drop & "-" & McInstanceState.OptiFine))
                ElseIf NewerInstance.ContainsKey(Drop & "-" & McInstanceState.Original) Then
                    '没有 OptiFine，直接加入原版
                    InstanceUseful.Add(NewerInstance(Drop & "-" & McInstanceState.Original))
                    InstanceList.Remove(NewerInstance(Drop & "-" & McInstanceState.Original))
                End If
            Next

            '将剩余的东西添加进去
            InstanceRubbish.AddRange(InstanceList)
            If InstanceUseful.Any Then InstanceListOriginal.Add(McInstanceCardType.OriginalLike, InstanceUseful)
            If InstanceRubbish.Any Then InstanceListOriginal.Add(McInstanceCardType.Rubbish, InstanceRubbish)

            '按照自定义版本分类重新添加
            For Each InstancePair In InstanceListOriginal
                For Each Instance As McInstance In InstancePair.Value
                    Dim RealType = If(Instance.DisplayType = 0 OrElse InstancePair.Key = McInstanceCardType.Star, InstancePair.Key, Instance.DisplayType)
                    If Not Results.ContainsKey(RealType) Then Results.Add(RealType, New List(Of McInstance))
                    Results(RealType).Add(Instance)
                Next
            Next

        Catch ex As Exception
            Results.Clear()
            Log(ex, "分类版本列表失败", LogLevel.Feedback)
        End Try
#End Region
#Region "对卡片与版本进行排序"

        '卡片排序
        Dim SortedInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        For Each SortRule As String In {McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike, McInstanceCardType.Rubbish, McInstanceCardType.Fool, McInstanceCardType.Error, McInstanceCardType.Hidden}
            If Results.ContainsKey(SortRule) Then SortedInstanceList.Add(SortRule, Results(SortRule))
        Next
        Results = SortedInstanceList

        '版本排序
        For Each CardType In {McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike, McInstanceCardType.Rubbish, McInstanceCardType.Fool}
            If Not Results.ContainsKey(CardType) Then Continue For
            Dim GetComponentCode =
            Function(Instance As McInstance) As Integer
                If Instance.Version.ForgelikeCode > 0 Then Return Instance.Version.ForgelikeCode
                If Instance.Version.HasOptiFine Then Return Instance.Version.OptiFineCode
                Return 0
            End Function
            Results(CardType) = Results(CardType).SortByComparison(
            Function(Left As McInstance, Right As McInstance)
                '发布时间
                If (Left.ReleaseTime.Year >= 2000 OrElse Right.ReleaseTime.Year >= 2000) AndAlso
                    Left.ReleaseTime <> Right.ReleaseTime Then Return Left.ReleaseTime > Right.ReleaseTime
                '附加组件种类
                If Left.Version.HasFabric <> Right.Version.HasFabric Then Return Left.Version.HasFabric
                If Left.Version.HasNeoForge <> Right.Version.HasNeoForge Then Return Left.Version.HasNeoForge
                If Left.Version.HasForge <> Right.Version.HasForge Then Return Left.Version.HasForge
                If Left.Version.HasOptiFine <> Right.Version.HasOptiFine Then Return Left.Version.HasOptiFine
                If Left.Version.HasLiteLoader <> Right.Version.HasLiteLoader Then Return Left.Version.HasLiteLoader
                '附加组件版本
                If GetComponentCode(Left) <> GetComponentCode(Right) Then Return GetComponentCode(Left) > GetComponentCode(Right)
                '名称
                Return Left.Name > Right.Name
            End Function)
        Next

#End Region
#Region "保存卡片缓存"
        WriteIni(Path & "PCL.ini", "CardCount", Results.Count)
        For i = 0 To Results.Count - 1
            WriteIni(Path & "PCL.ini", "CardKey" & (i + 1), Results.Keys(i))
            Dim Value As String = ""
            For Each Instance As McInstance In Results.Values(i)
                Value += Instance.Name & ":"
            Next
            WriteIni(Path & "PCL.ini", "CardValue" & (i + 1), Value)
        Next
#End Region
        Return Results
    End Function
    ''' <summary>
    ''' 筛选特定种类的版本，并直接添加为卡片。
    ''' </summary>
    ''' <param name="InstanceList">用于筛选的列表。</param>
    ''' <param name="Formula">需要筛选出的版本类型。-2 代表隐藏的版本。</param>
    ''' <param name="CardType">卡片的名称。</param>
    Private Sub McInstanceFilter(ByRef InstanceList As List(Of McInstance), ByRef Target As Dictionary(Of McInstanceCardType, List(Of McInstance)), Formula As McInstanceState(), CardType As McInstanceCardType)
        Dim KeepList = InstanceList.Where(Function(v) Formula.Contains(v.State)).ToList
        '加入版本列表，并从剩余中删除
        If KeepList.Any Then
            Target.Add(CardType, KeepList)
            InstanceList = InstanceList.Except(KeepList).ToList()
        End If
    End Sub
    ''' <summary>
    ''' 筛选特定种类的版本，并增加入一个已有列表中。
    ''' </summary>
    ''' <param name="InstanceList">用于筛选的列表。</param>
    ''' <param name="Formula">需要筛选出的版本类型。-2 代表隐藏的版本。</param>
    ''' <param name="KeepList">传入需要增加入的列表。</param>
    Private Sub McInstanceFilter(ByRef InstanceList As List(Of McInstance), Formula As McInstanceState(), ByRef KeepList As List(Of McInstance))
        KeepList.AddRange(InstanceList.Where(Function(v) Formula.Contains(v.State)))
        '加入版本列表，并从剩余中删除
        If KeepList.Any Then
            InstanceList = InstanceList.Except(KeepList).ToList()
        End If
    End Sub
    Public Enum McInstanceCardType
        Star = -1
        Auto = 0 '仅用于强制版本分类的自动
        Hidden = 1
        API = 2
        OriginalLike = 3
        Rubbish = 4
        Fool = 5
        [Error] = 6
    End Enum

#End Region

#Region "皮肤"

    Public Structure McSkinInfo
        Public IsSlim As Boolean
        Public LocalFile As String
        Public IsVaild As Boolean
    End Structure
    ''' <summary>
    ''' 要求玩家选择一个皮肤文件，并进行相关校验。
    ''' </summary>
    Public Function McSkinSelect() As McSkinInfo
        Dim FileName As String = SelectFile("皮肤文件(*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp", "选择皮肤文件")

        '验证有效性
        If FileName = "" Then Return New McSkinInfo With {.IsVaild = False}
        Try
            Dim Image As New MyBitmap(FileName)
            If Image.Pic.Width <> 64 OrElse Not (Image.Pic.Height = 32 OrElse Image.Pic.Height = 64) Then
                Hint("皮肤图片大小应为 64x32 像素或 64x64 像素！", HintType.Red)
                Return New McSkinInfo With {.IsVaild = False}
            End If
            Dim FileInfo As New FileInfo(FileName)
            If FileInfo.Length > 24 * 1024 Then
                Hint("皮肤文件大小需小于 24 KB，而所选文件大小为 " & Math.Round(FileInfo.Length / 1024, 2) & " KB", HintType.Red)
                Return New McSkinInfo With {.IsVaild = False}
            End If
        Catch ex As Exception
            Log(ex, "皮肤文件存在错误", LogLevel.Hint)
            Return New McSkinInfo With {.IsVaild = False}
        End Try

        '获取皮肤种类
        Dim IsSlim As Integer = MyMsgBox("此皮肤为 Steve 模型（粗手臂）还是 Alex 模型（细手臂）？", "选择皮肤种类", "Steve 模型", "Alex 模型", "我不知道", HighLight:=False)
        If IsSlim = 3 Then
            Hint("请在皮肤下载页面确认皮肤种类后再使用此皮肤！")
            Return New McSkinInfo With {.IsVaild = False}
        End If

        Return New McSkinInfo With {.IsVaild = True, .IsSlim = IsSlim = 2, .LocalFile = FileName}
    End Function

    ''' <summary>
    ''' 获取 Uuid 对应的皮肤文件地址，失败将抛出异常。
    ''' </summary>
    Public Function McSkinGetAddress(UUID As String, Type As String) As String
        If UUID = "" Then Throw New Exception("UUID 为空。")
        If UUID.StartsWithF("00000") AndAlso Type <> "Auth" Then Throw New Exception("离线 UUID 无正版皮肤文件：" & UUID)
        '尝试读取缓存
        Dim CacheSkinAddress As String = ReadIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", UUID)
        If Not CacheSkinAddress = "" Then Return CacheSkinAddress
        '获取皮肤地址
        Dim Url As String
        Select Case Type
            Case "Mojang", "Ms"
                Url = "https://sessionserver.mojang.com/session/minecraft/profile/"
            Case "Nide"
                Url = "https://auth.mc-user.com:233/" & If(McInstanceSelected Is Nothing, Setup.Get("CacheNideServer"), Setup.Get("VersionServerNide", Instance:=McInstanceSelected)) & "/sessionserver/session/minecraft/profile/"
            Case "Auth"
                Url = If(McInstanceSelected Is Nothing, Setup.Get("CacheAuthServerServer"), Setup.Get("VersionServerAuthServer", Instance:=McInstanceSelected)) & "/sessionserver/session/minecraft/profile/"
            Case Else
                Throw New ArgumentException("皮肤地址种类无效：" & If(Type, "null"))
        End Select
        Dim SkinString = NetRequestByClientRetry(Url & UUID, RequireJson:=True)
        If SkinString = "" Then Throw New Exception("皮肤返回值为空，可能是未设置自定义皮肤的用户")
        '处理皮肤地址
        Dim SkinValue As String
        Try
            For Each SkinProperty In GetJson(SkinString)("properties")
                If SkinProperty("name") = "textures" Then
                    SkinValue = SkinProperty("value")
                    Exit Try
                End If
            Next
            Throw New Exception("未从皮肤返回值中找到符合条件的 Property")
        Catch ex As Exception
            Log(ex, "无法完成解析的皮肤返回值，可能是未设置自定义皮肤的用户：" & SkinString, LogLevel.Developer)
            Throw New Exception("皮肤返回值中不包含皮肤数据项，可能是未设置自定义皮肤的用户", ex)
        End Try
        SkinString = Encoding.GetEncoding("utf-8").GetString(Convert.FromBase64String(SkinValue))
        Dim SkinJson As JObject = GetJson(SkinString.ToLower)
        If SkinJson("textures") Is Nothing OrElse SkinJson("textures")("skin") Is Nothing OrElse SkinJson("textures")("skin")("url") Is Nothing Then
            Throw New Exception("用户未设置自定义皮肤")
        Else
            Dim SkinUrl As String = SkinJson("textures")("skin")("url").ToString
            SkinValue = If(SkinUrl.Contains("minecraft.net/"), SkinUrl.Replace("http://", "https://"), SkinUrl)
        End If
        '保存缓存
        WriteIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", UUID, SkinValue)
        Log("[Skin] UUID " & UUID & " 对应的皮肤文件为 " & SkinValue)
        Return SkinValue
    End Function

    Private ReadOnly McSkinDownloadLock As New Object
    ''' <summary>
    ''' 从 Url 下载皮肤。返回本地文件路径，失败将抛出异常。
    ''' </summary>
    Public Function McSkinDownload(Address As String) As String
        Dim SkinName As String = GetFileNameFromPath(Address)
        Dim FileAddress As String = PathTemp & "Cache\Skin\" & GetHash(Address) & ".png"
        SyncLock McSkinDownloadLock
            If Not File.Exists(FileAddress) Then
                NetDownloadByClient(Address, FileAddress & ".PCLDownloading")
                File.Delete(FileAddress)
                FileSystem.Rename(FileAddress & ".PCLDownloading", FileAddress)
                Log("[Minecraft] 皮肤下载成功：" & FileAddress)
            End If
            Return FileAddress
        End SyncLock
    End Function

    ''' <summary>
    ''' 获取 Uuid 对应的皮肤，返回“Steve”或“Alex”。
    ''' </summary>
    Public Function McSkinSex(Uuid As String) As String
        If Not Uuid.Length = 32 Then Return "Steve"
        Dim a = Integer.Parse(Uuid(7), Globalization.NumberStyles.AllowHexSpecifier)
        Dim b = Integer.Parse(Uuid(15), Globalization.NumberStyles.AllowHexSpecifier)
        Dim c = Integer.Parse(Uuid(23), Globalization.NumberStyles.AllowHexSpecifier)
        Dim d = Integer.Parse(Uuid(31), Globalization.NumberStyles.AllowHexSpecifier)
        Return If((a Xor b Xor c Xor d) Mod 2, "Alex", "Steve")
        'Math.floorMod(uuid.hashCode(), 18)

        'Public Function hashCode(ByVal str As String) As Integer
        'Dim hash As Integer = 0
        'Dim n As Integer = str.Length
        'If n = 0 Then
        '    Return hash
        'End If
        'For i As Integer = 0 To n - 1
        '    hash = hash + Asc(str(i)) * (1 << (n - i - 1))
        'Next
        'Return hash
        'End Function
    End Function

#End Region

#Region "支持库文件（Library）"

    Public Class McLibToken
        ''' <summary>
        ''' 文件的完整本地路径。
        ''' </summary>
        Public LocalPath As String
        ''' <summary>
        ''' 文件大小。若无有效数据即为 0。
        ''' </summary>
        Public Size As Long = 0
        ''' <summary>
        ''' 是否为 Natives 文件。
        ''' </summary>
        Public IsNatives As Boolean = False
        ''' <summary>
        ''' 文件的 SHA1。
        ''' </summary>
        Public SHA1 As String = Nothing
        ''' <summary>
        ''' 由 Json 提供的 URL，若没有则为 Nothing。
        ''' </summary>
        Public Property Url As String
            Get
                Return _Url
            End Get
            Set(value As String)
                '孤儿 Forge 作者喜欢把没有 URL 的写个空字符串
                _Url = If(String.IsNullOrWhiteSpace(value), Nothing, value)
            End Set
        End Property
        Private _Url As String
        ''' <summary>
        ''' 原 Json 中 Name 项除去版本号部分的较前部分。可能为 Nothing。
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                If OriginalName Is Nothing Then Return Nothing
                Dim Splited As New List(Of String)(OriginalName.Split(":"))
                Splited.RemoveAt(2) 'Java 的此格式下版本号固定为第三段，第四段可能包含架构、分包等其他信息
                Return Join(Splited, ":")
            End Get
        End Property
        ''' <summary>
        ''' 原 Json 中的 Name 项。
        ''' </summary>
        Public OriginalName As String

        Public Overrides Function ToString() As String
            Return If(IsNatives, "[Native] ", "") & GetString(Size) & " | " & LocalPath
        End Function
    End Class

    ''' <summary>
    ''' 检查是否符合 Json 中的 Rules。
    ''' </summary>
    ''' <param name="RuleToken">Json 中的 "rules" 项目。</param>
    Public Function McJsonRuleCheck(RuleToken As JToken) As Boolean
        If RuleToken Is Nothing Then Return True

        '初始化
        Dim Required As Boolean = False
        For Each Rule As JToken In RuleToken

            '单条条件验证
            Dim IsRightRule As Boolean = True '是否为正确的规则
            If Rule("os") IsNot Nothing Then '操作系统
                If Rule("os")("name") IsNot Nothing Then '操作系统名称
                    Dim OsName As String = Rule("os")("name").ToString
                    If OsName = "unknown" Then
                    ElseIf OsName = "windows" Then
                        If Rule("os")("version") IsNot Nothing Then '操作系统版本
                            Dim Cr As String = Rule("os")("version").ToString
                            IsRightRule = IsRightRule AndAlso RegexCheck(OSVersion, Cr)
                        End If
                    Else
                        IsRightRule = False
                    End If
                End If
                If Rule("os")("arch") IsNot Nothing Then '操作系统架构
                    IsRightRule = IsRightRule AndAlso ((Rule("os")("arch").ToString = "x86") = Is32BitSystem)
                End If
            End If
            If Not IsNothing(Rule("features")) Then '标签
                IsRightRule = IsRightRule AndAlso IsNothing(Rule("features")("is_demo_user")) '反选是否为 Demo 用户
                If CType(Rule("features"), JObject).Children.Any(Function(j As JProperty) j.Name.Contains("quick_play")) Then
                    IsRightRule = False '不开 Quick Play，让玩家自己加去
                End If
            End If

            '反选确认
            If Rule("action").ToString = "allow" Then
                If IsRightRule Then Required = True 'allow
            Else
                If IsRightRule Then Required = False 'disallow
            End If

        Next
        Return Required
    End Function
    Private OSVersion As String = My.Computer.Info.OSVersion

    ''' <summary>
    ''' 递归获取 Minecraft 某一版本的完整支持库列表。
    ''' </summary>
    Public Function McLibListGet(Instance As McInstance, IncludeMainJar As Boolean) As List(Of McLibToken)

        '获取当前支持库列表
        Log("[Minecraft] 获取支持库列表：" & Instance.Name)
        Dim Result = McLibListGetWithJson(Instance.JsonObject)

        '需要添加原版 Jar
        If IncludeMainJar Then
            Dim RealInstance As McInstance
            Dim RequiredJar As String = Instance.JsonObject("jar")?.ToString
            If Instance.IsHmclFormatJson OrElse RequiredJar Is Nothing Then
                'HMCL 项直接使用自身的 Jar
                '根据 Inherit 获取最深层版本
                Dim OriginalInstance As McInstance = Instance
                '1.17+ 的 Forge 不寻找 Inherit
                If Not (Instance.Version.HasForgelike AndAlso Instance.Version.Vanilla.Major >= 17) Then
                    Do Until OriginalInstance.InheritName = ""
                        If OriginalInstance.InheritName = OriginalInstance.Name Then Exit Do
                        OriginalInstance = New McInstance(McFolderSelected & "versions\" & OriginalInstance.InheritName & "\")
                    Loop
                End If
                '需要新建对象，否则后面的 Check 会导致 McInstanceCurrent 的 State 变回 Original
                '复现：启动一个 Snapshot 版本
                RealInstance = New McInstance(OriginalInstance.PathVersion)
            Else
                'Json 已提供 Jar 字段，使用该字段的信息
                RealInstance = New McInstance(RequiredJar)
            End If
            Dim ClientUrl As String, ClientSHA1 As String
            '判断需求的版本是否存在
            '不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
            If Not File.Exists(RealInstance.PathVersion & RealInstance.Name & ".json") Then
                RealInstance = Instance
                Log("[Minecraft] 可能缺少前置版本 " & RealInstance.Name & "，找不到对应的 json 文件", LogLevel.Debug)
            End If
            '获取详细下载信息
            If RealInstance.JsonObject("downloads") IsNot Nothing AndAlso RealInstance.JsonObject("downloads")("client") IsNot Nothing Then
                ClientUrl = RealInstance.JsonObject("downloads")("client")("url")
                ClientSHA1 = RealInstance.JsonObject("downloads")("client")("sha1")
            Else
                ClientUrl = Nothing
                ClientSHA1 = Nothing
            End If
            '把所需的原版 Jar 添加进去
            Result.Add(New McLibToken With {.LocalPath = RealInstance.PathVersion & RealInstance.Name & ".jar", .Size = 0, .IsNatives = False, .Url = ClientUrl, .SHA1 = ClientSHA1})
        End If

        Return Result
    End Function
    ''' <summary>
    ''' 获取 Minecraft 某一版本忽视继承的支持库列表，即结果中没有继承项。
    ''' </summary>
    Public Function McLibListGetWithJson(JsonObject As JObject, Optional KeepSameNameDifferentVersionResult As Boolean = False, Optional CustomMcFolder As String = Nothing) As List(Of McLibToken)
        CustomMcFolder = If(CustomMcFolder, McFolderSelected)

        '转换为 LibToken
        Dim BasicArray As New List(Of McLibToken)
        For Each Library As JObject In CType(JsonObject("libraries"), JArray).Children

            '清理 null 项（BakaXL 会把没有的项序列化为 null，但会被 Newtonsoft 转换为 JValue，导致 Is Nothing = false；这导致了 #409）
            For i = Library.Properties.Count - 1 To 0 Step -1
                If Library.Properties(i).Value.Type = JTokenType.Null Then Library.Remove(Library.Properties(i).Name)
            Next

            '检查是否需要（Rules）
            If Not McJsonRuleCheck(Library("rules")) Then Continue For

            '获取根节点下的 url
            Dim RootUrl As String = Library("url")
            If RootUrl IsNot Nothing Then
                RootUrl += McLibGet(Library("name"), False, True, CustomMcFolder).Replace("\", "/")
            End If

            '根据是否本地化处理（Natives）
            If Library("natives") Is Nothing Then '没有 Natives
                Dim LocalPath As String
                LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder)
                Try
                    If Library("downloads") IsNot Nothing AndAlso Library("downloads")("artifact") IsNot Nothing Then
                        BasicArray.Add(New McLibToken With {
                            .OriginalName = Library("name"),
                            .Url = If(RootUrl, Library("downloads")("artifact")("url")),
                            .LocalPath = If(Library("downloads")("artifact")("path") Is Nothing, McLibGet(Library("name"),
                                CustomMcFolder:=CustomMcFolder), CustomMcFolder & "libraries\" & Library("downloads")("artifact")("path").ToString.Replace("/", "\")),
                            .Size = Val(Library("downloads")("artifact")("size").ToString),
                            .IsNatives = False,
                            .SHA1 = Library("downloads")("artifact")("sha1")?.ToString})
                    Else
                        BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = LocalPath, .Size = 0, .IsNatives = False, .SHA1 = Nothing})
                    End If
                Catch ex As Exception
                    Log(ex, "处理实际支持库列表失败（无 Natives，" & If(Library("name"), "Nothing").ToString & "）")
                    BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = LocalPath, .Size = 0, .IsNatives = False, .SHA1 = Nothing})
                End Try
            ElseIf Library("natives")("windows") IsNot Nothing Then '有 Windows Natives
                Try
                    If Library("downloads") IsNot Nothing AndAlso Library("downloads")("classifiers") IsNot Nothing AndAlso Library("downloads")("classifiers")("natives-windows") IsNot Nothing Then
                        BasicArray.Add(New McLibToken With {
                             .OriginalName = Library("name"),
                             .Url = If(RootUrl, Library("downloads")("classifiers")("natives-windows")("url")),
                             .LocalPath = If(Library("downloads")("classifiers")("natives-windows")("path") Is Nothing,
                                 McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).
                                    Replace(".jar", "-" & Library("natives")("windows").ToString & ".jar").
                                    Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")),
                                    $"{CustomMcFolder}libraries\{Library("downloads")("classifiers")("natives-windows")("path").ToString.Replace("/", "\")}"),
                             .Size = Val(Library("downloads")("classifiers")("natives-windows")("size").ToString),
                             .IsNatives = True,
                             .SHA1 = Library("downloads")("classifiers")("natives-windows")("sha1").ToString})
                    Else
                        BasicArray.Add(
                        New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .Size = 0, .IsNatives = True, .SHA1 = Nothing,
                            .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).
                                 Replace(".jar", $"-{Library("natives")("windows")}.jar").
                                 Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32"))
                        })
                    End If
                Catch ex As Exception
                    Log(ex, "处理实际支持库列表失败（有 Natives，" & If(Library("name"), "Nothing").ToString & "）")
                    BasicArray.Add(
                        New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .Size = 0, .IsNatives = True, .SHA1 = Nothing,
                             .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).
                                 Replace(".jar", $"-{Library("natives")("windows")}.jar").
                                 Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32"))
                        })
                End Try
            End If

        Next

        '去重
        Dim ResultArray As New Dictionary(Of String, McLibToken)
        Dim GetVersion =
        Function(Token As McLibToken) As String
            '测试例：
            'D:\Minecraft\test\libraries\net\neoforged\mergetool\2.0.0\mergetool-2.0.0-api.jar
            'D:\Minecraft\test\libraries\org\apache\commons\commons-collections4\4.2\commons-collections4-4.2.jar
            'D:\Minecraft\test\libraries\com\google\guava\guava\31.1-jre\guava-31.1-jre.jar
            Return GetFolderNameFromPath(GetPathFromFullPath(Token.LocalPath))
        End Function
        For i = 0 To BasicArray.Count - 1
            Dim Key As String = BasicArray(i).Name & BasicArray(i).IsNatives.ToString
            If ResultArray.ContainsKey(Key) Then
                Dim BasicArrayVersion As String = GetVersion(BasicArray(i))
                Dim ResultArrayVersion As String = GetVersion(ResultArray(Key))
                If BasicArrayVersion <> ResultArrayVersion AndAlso KeepSameNameDifferentVersionResult Then
                    Log($"[Minecraft] 发现疑似重复的支持库：{BasicArray(i)} ({BasicArrayVersion}) 与 {ResultArray(Key)} ({ResultArrayVersion})")
                    ResultArray.Add(Key & GetUuid(), BasicArray(i))
                Else
                    Log($"[Minecraft] 发现重复的支持库：{BasicArray(i)} ({BasicArrayVersion}) 与 {ResultArray(Key)} ({ResultArrayVersion})，已忽略其中之一")
                    If CompareVersionGE(BasicArrayVersion, ResultArrayVersion) Then
                        ResultArray(Key) = BasicArray(i)
                    End If
                End If
            Else
                ResultArray.Add(Key, BasicArray(i))
            End If
        Next
        Return ResultArray.Values.ToList
    End Function

    ''' <summary>
    ''' 获取版本所需支持库文件的 NetFile。
    ''' </summary>
    Public Function McLibNetFilesFromInstance(Instance As McInstance) As List(Of NetFile)
        If Not Instance.IsLoaded Then Instance.Load()
        Dim Result As New List(Of NetFile)

        '更新此方法时需要同步更新 Forge 新版自动安装方法！

        '主 Jar 文件
        Try
            Dim MainJar As NetFile = DlClientJarGet(Instance, True)
            If MainJar IsNot Nothing Then Result.Add(MainJar)
        Catch ex As Exception
            Log(ex, "版本缺失主 jar 文件所必须的信息", LogLevel.Developer)
        End Try

        'Library 文件
        Result.AddRange(McLibNetFilesFromTokens(McLibListGet(Instance, False)))

        '统一通行证文件
        If Setup.Get("VersionServerLogin", Instance:=Instance) = 3 Then
            Dim TargetFile As String = PathAppdata & "nide8auth.jar"
            Try
                '测试链接：https://auth.mc-user.com:233/00000000000000000000000000000000/
                Log("[Minecraft] 开始获取统一通行证下载信息")
                Dim DownloadInfo = GetJson(NetRequestByClientRetry("https://auth.mc-user.com:233/" & Setup.Get("VersionServerNide", Instance:=Instance), RequireJson:=True))
                Result.Add(New NetFile(
                    Urls:={"https://login.mc-user.com:233/index/jar"},
                    LocalPath:=TargetFile,
                    Checker:=New FileChecker(Hash:=DownloadInfo("jarHash").ToString)))
            Catch ex As Exception
                If File.Exists(TargetFile) Then
                    Log(ex, "获取统一通行证下载信息失败")
                Else
                    Throw New Exception("获取统一通行证下载信息失败", ex)
                End If
            End Try
        End If

        'Authlib-Injector 文件
        If Setup.Get("VersionServerLogin", Instance:=Instance) = 4 Then
            Dim TargetFile = PathPure & "authlib-injector.jar"
            Try
                Log("[Minecraft] 开始获取 Authlib-Injector 下载信息")
                Dim DownloadInfo = GetJson(NetRequestByClientRetry(
                        "https://authlib-injector.yushi.moe/artifact/latest.json",
                        BackupUrl:="https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json", RequireJson:=True))
                Dim DownloadAddress As String = DownloadInfo("download_url").ToString.
                        Replace("bmclapi2.bangbang93.com/mirrors/authlib-injector", "authlib-injector.yushi.moe")
                Result.Add(New NetFile({
                    DownloadAddress,
                    DownloadAddress.Replace("authlib-injector.yushi.moe", "bmclapi2.bangbang93.com/mirrors/authlib-injector")
                }, TargetFile, New FileChecker(Hash:=DownloadInfo("checksums")("sha256").ToString)))
            Catch ex As Exception
                If File.Exists(TargetFile) Then
                    Log(ex, "获取 Authlib-Injector 下载信息失败")
                Else
                    Throw New Exception("获取 Authlib-Injector 下载信息失败", ex)
                End If
            End Try
        End If

        '跳过校验
        If ShouldIgnoreFileCheck(Instance) Then
            Log("[Minecraft] 设置要求尽量忽略文件检查，这可能会保留有误的文件", LogLevel.Debug)
            Result = Result.Where(
            Function(f)
                If File.Exists(f.LocalPath) Then
                    Log("[Minecraft] 跳过下载的支持库文件：" & f.LocalPath, LogLevel.Debug)
                    Return False
                Else
                    Return True
                End If
            End Function).ToList
        End If

        Return Result
    End Function
    ''' <summary>
    ''' 将 McLibToken 列表转换为 NetFile。
    ''' </summary>
    Public Function McLibNetFilesFromTokens(Libs As List(Of McLibToken), Optional CustomMcFolder As String = Nothing) As List(Of NetFile)
        CustomMcFolder = If(CustomMcFolder, McFolderSelected)
        Dim Result As New List(Of NetFile)
        '获取
        For Each Token As McLibToken In Libs
            '检查器
            Dim Checker As FileChecker
            If Token.Name.ContainsF("labymod") Then
                Checker = New FileChecker '不检查 LabyMod 的文件，它们提供的文件校验信息是错的（#3225）
            Else
                Checker = New FileChecker(ActualSize:=If(Token.Size = 0, -1, Token.Size), Hash:=Token.SHA1)
            End If
            'URL
            Dim Urls As New List(Of String)
            If Token.Url Is Nothing AndAlso Token.Name = "net.minecraftforge:forge:universal" Then '特判修复 Forge 部分 universal 文件缺失 URL（#5455）
                Token.Url = "https://maven.minecraftforge.net" & Token.LocalPath.Replace(CustomMcFolder & "libraries", "").Replace("\", "/")
            End If
            If Token.Url IsNot Nothing Then
                '获取 URL 的真实地址
                Urls.Add(Token.Url)
                If Token.Url.Contains("launcher.mojang.com/v1/objects") OrElse Token.Url.Contains("client.txt") OrElse
                   Token.Url.Contains(".tsrg") Then
                    Urls.AddRange(DlSourceLauncherOrMetaGet(Token.Url)) 'Mappings（#4425）
                End If
                If Token.Url.Contains("maven") Then
                    Dim BmclapiUrl As String =
                        Token.Url.Replace(Mid(Token.Url, 1, Token.Url.IndexOfF("maven")), "https://bmclapi2.bangbang93.com/").Replace("maven.fabricmc.net", "maven").Replace("maven.minecraftforge.net", "maven").Replace("maven.neoforged.net/releases", "maven")
                    If DlSourcePreferMojang Then
                        Urls.Add(BmclapiUrl) '官方源优先
                    Else
                        Urls.Insert(0, BmclapiUrl) '镜像源优先
                    End If
                End If
            End If
            If Token.LocalPath.Contains("transformer-discovery-service") Then
                'Transformer 文件释放
                If Not File.Exists(Token.LocalPath) Then WriteFile(Token.LocalPath, GetResources("Transformer"))
                Log("[Download] 已自动释放 Transformer Discovery Service", LogLevel.Developer)
                Continue For
            ElseIf Token.LocalPath.Contains("optifine\OptiFine") Then
                'OptiFine 主 Jar
                Dim OptiFineBase As String = Token.LocalPath.Replace(CustomMcFolder & "libraries\optifine\OptiFine\", "").Split("_")(0) & "/" & GetFileNameFromPath(Token.LocalPath).Replace("-", "_")
                OptiFineBase = "/maven/com/optifine/" & OptiFineBase
                If OptiFineBase.Contains("_pre") Then OptiFineBase = OptiFineBase.Replace("com/optifine/", "com/optifine/preview_")
                Urls.Add("https://bmclapi2.bangbang93.com" & OptiFineBase)
            ElseIf Urls.Count <= 2 Then
                '普通文件
                Urls.AddRange(DlSourceLibraryGet("https://libraries.minecraft.net" & Token.LocalPath.Replace(CustomMcFolder & "libraries", "").Replace("\", "/")))
            End If
            Result.Add(New NetFile(Urls.Distinct, Token.LocalPath, Checker))
        Next
        '去重并返回
        Return Result.Distinct(Function(a, b) a.LocalPath = b.LocalPath)
    End Function
    ''' <summary>
    ''' 获取对应的支持库文件地址。
    ''' </summary>
    ''' <param name="Original">原始地址，如 com.mumfrey:liteloader:1.12.2-SNAPSHOT。</param>
    ''' <param name="WithHead">是否包含 Lib 文件夹头部，若不包含，则会类似以 com\xxx\ 开头。</param>
    Public Function McLibGet(Original As String, Optional WithHead As Boolean = True, Optional IgnoreLiteLoader As Boolean = False, Optional CustomMcFolder As String = Nothing) As String
        CustomMcFolder = If(CustomMcFolder, McFolderSelected)
        '有时候原始的有四段，例如 net.minecraftforge:forge:1.21.4-54.0.34:client
        '文件夹名应该忽略最后一段（client），但文件名需要保留
        '参见 #7306（以及 #5376 也是因为这里没有处理好导致的）
        Dim Splited = Original.Split(":")
        McLibGet = If(WithHead, CustomMcFolder & "libraries\", "") &
            $"{Splited(0).Replace(".", "\")}\{Splited(1)}\{Splited(2)}\{Splited.Skip(1).Join("-")}.jar"
    End Function

    ''' <summary>
    ''' 检查设置，是否应当忽略文件检查？
    ''' </summary>
    Public Function ShouldIgnoreFileCheck(Instance As McInstance)
        Return Setup.Get("VersionAdvanceAssetsV2", Instance:=Instance) OrElse (Setup.Get("VersionAdvanceAssets", Instance:=Instance) = 2)
    End Function

#End Region

#Region "资源文件（Assets）"

    '获取索引
    ''' <summary>
    ''' 获取某版本资源文件索引的对应 Json 项，详见版本 Json 中的 assetIndex 项。失败会抛出异常。
    ''' </summary>
    Public Function McAssetsGetIndex(Instance As McInstance, Optional ReturnLegacyOnError As Boolean = False, Optional CheckURLEmpty As Boolean = False) As JToken
        Dim AssetsName As String
        Try
            Do While True
                Dim Index As JToken = Instance.JsonObject("assetIndex")
                If Index IsNot Nothing AndAlso Index("id") IsNot Nothing Then Return Index
                If Instance.JsonObject("assets") IsNot Nothing Then AssetsName = Instance.JsonObject("assets").ToString
                If CheckURLEmpty AndAlso Index("url") IsNot Nothing Then Return Index
                '下一个版本
                If Instance.InheritName = "" Then Exit Do
                Instance = New McInstance(McFolderSelected & "versions\" & Instance.InheritName)
            Loop
        Catch
        End Try
        '无法获取到下载地址
        If ReturnLegacyOnError Then
            '返回 assets 文件名会由于没有下载地址导致全局失败
            'If AssetsName IsNot Nothing AndAlso AssetsName <> "legacy" Then
            '    Log("[Minecraft] 无法获取资源文件索引下载地址，使用 assets 项提供的资源文件名：" & AssetsName)
            '    Return GetJson("{""id"": """ & AssetsName & """}")
            'Else
            Log("[Minecraft] 无法获取资源文件索引下载地址，使用默认的 legacy 下载地址")
            Return GetJson("{
                ""id"": ""legacy"",
                ""sha1"": ""c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729"",
                ""size"": 134284,
                ""url"": ""https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json"",
                ""totalSize"": 111220701
            }")
            'End If
        Else
            Throw New Exception("该版本不存在资源文件索引信息")
        End If
    End Function
    ''' <summary>
    ''' 获取某版本资源文件索引名，优先使用 assetIndex，其次使用 assets。失败会返回 legacy。
    ''' </summary>
    Public Function McAssetsGetIndexName(Instance As McInstance) As String
        Try
            Do While True
                If Instance.JsonObject("assetIndex") IsNot Nothing AndAlso Instance.JsonObject("assetIndex")("id") IsNot Nothing Then
                    Return Instance.JsonObject("assetIndex")("id").ToString
                End If
                If Instance.JsonObject("assets") IsNot Nothing Then
                    Return Instance.JsonObject("assets").ToString
                End If
                If Instance.InheritName = "" Then Exit Do
                Instance = New McInstance(McFolderSelected & "versions\" & Instance.InheritName)
            Loop
        Catch ex As Exception
            Log(ex, "获取资源文件索引名失败")
        End Try
        Return "legacy"
    End Function

    '获取列表
    Private Structure McAssetsToken
        ''' <summary>
        ''' 文件的完整本地路径。
        ''' </summary>
        Public LocalPath As String
        ''' <summary>
        ''' Json 中书写的源路径。例如 minecraft/sounds/mob/stray/death2.ogg 。
        ''' </summary>
        Public SourcePath As String
        ''' <summary>
        ''' 文件大小。若无有效数据即为 0。
        ''' </summary>
        Public Size As Long
        ''' <summary>
        ''' 文件的 Hash 校验码。
        ''' </summary>
        Public Hash As String

        Public Overrides Function ToString() As String
            Return GetString(Size) & " | " & LocalPath
        End Function
    End Structure
    ''' <summary>
    ''' 获取 Minecraft 的资源文件列表。失败会抛出异常。
    ''' </summary>
    Private Function McAssetsListGet(Instance As McInstance) As List(Of McAssetsToken)
        Dim IndexName = McAssetsGetIndexName(Instance)
        Try

            '初始化
            If Not File.Exists($"{McFolderSelected}assets\indexes\{IndexName}.json") Then Throw New FileNotFoundException("未找到 Asset Index", McFolderSelected & "assets\indexes\" & IndexName & ".json")
            Dim Result As New List(Of McAssetsToken)
            Dim Json As JObject = GetJson(ReadFile($"{McFolderSelected}assets\indexes\{IndexName}.json"))

            '读取列表
            For Each File As JProperty In Json("objects").Children
                Dim LocalPath As String
                If Json("map_to_resources") IsNot Nothing AndAlso Json("map_to_resources").ToObject(Of Boolean) Then
                    'Remap
                    LocalPath = $"{Instance.PathIndie}resources\{File.Name}"
                ElseIf Json("virtual") IsNot Nothing AndAlso Json("virtual").ToObject(Of Boolean) Then
                    'Virtual
                    LocalPath = $"{McFolderSelected}assets\virtual\legacy\{File.Name}"
                Else
                    '正常
                    LocalPath = $"{McFolderSelected}assets\objects\{Left(File.Value("hash").ToString, 2)}\{File.Value("hash")}"
                End If
                Result.Add(New McAssetsToken With {
                    .LocalPath = LocalPath.Replace("/", "\"),
                    .SourcePath = File.Name,
                    .Hash = File.Value("hash").ToString,
                    .Size = File.Value("size").ToString
                })
            Next
            Return Result

        Catch ex As Exception
            Log(ex, "获取资源文件列表失败：" & IndexName)
            Throw
        End Try
    End Function

    ''' <summary>
    ''' 获取版本资源文件所对应的 NetFile。
    ''' </summary>
    Public Function McAssetsFixList(Instance As McInstance, CheckHash As Boolean, Optional ByRef ProgressFeed As LoaderBase = Nothing) As List(Of NetFile)
        '如果需要检查 Hash，则留到下载时处理，以借助多线程加快检查速度
        If CheckHash Then
            Return McAssetsListGet(Instance).
                Select(Function(Token As McAssetsToken) New NetFile(
                    DlSourceAssetsGet($"https://resources.download.minecraft.net/{Left(Token.Hash, 2)}/{Token.Hash}"),
                    LocalPath:=Token.LocalPath,
                    Checker:=New FileChecker(ActualSize:=If(Token.Size = 0, -1, Token.Size),
                    Hash:=Token.Hash))).ToList
        End If
        '如果不检查 Hash，则立即处理
        Dim Result As New List(Of NetFile)
        Dim AssetsList As List(Of McAssetsToken)
        Try
            AssetsList = McAssetsListGet(Instance)
            Dim Token As McAssetsToken
            If ProgressFeed IsNot Nothing Then ProgressFeed.Progress = 0.04
            For i = 0 To AssetsList.Count - 1
                '初始化
                Token = AssetsList(i)
                If ProgressFeed IsNot Nothing Then ProgressFeed.Progress = 0.05 + 0.94 * i / AssetsList.Count
                '检查文件是否存在
                Dim File As New FileInfo(Token.LocalPath)
                If File.Exists AndAlso (Token.Size = 0 OrElse Token.Size = File.Length) Then Continue For
                '文件不存在，添加下载
                Result.Add(New NetFile(DlSourceAssetsGet($"https://resources.download.minecraft.net/{Left(Token.Hash, 2)}/{Token.Hash}"), Token.LocalPath, New FileChecker(ActualSize:=If(Token.Size = 0, -1, Token.Size), Hash:=Token.Hash)))
            Next
        Catch ex As Exception
            Log(ex, "获取版本缺失的资源文件下载列表失败")
        End Try
        If ProgressFeed IsNot Nothing Then ProgressFeed.Progress = 0.99
        Return Result
    End Function

#End Region

    ''' <summary>
    ''' 发送 Minecraft 更新提示。
    ''' </summary>
    Public Sub McDownloadClientUpdateHint(VersionName As String, Json As JObject)
        Try

            '获取对应版本
            Dim Version As JToken = Nothing
            For Each Token In Json("versions")
                If Token("id") IsNot Nothing AndAlso Token("id").ToString = VersionName Then
                    Version = Token
                    Exit For
                End If
            Next
            '进行提示
            If Version Is Nothing Then Return
            Dim Time As Date = Version("releaseTime")
            Dim MsgBoxText As String = $"新版本：{VersionName}{vbCrLf}" &
                If((Date.Now - Time).TotalDays > 1, "更新时间：" & Time.ToString, "更新于：" & GetTimeSpanString(Time - Date.Now, False))
            Dim MsgResult = MyMsgBox(MsgBoxText, "Minecraft 更新提示", "确定", "下载", If((Date.Now - Time).TotalHours > 3, "更新日志", ""),
                Button3Action:=Sub() McUpdateLogShow(Version))
            '弹窗结果
            If MsgResult = 2 Then
                '下载
                RunInUi(
                Sub()
                    PageDownloadInstall.VersionWaitingSelect = VersionName
                    FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
                End Sub)
            End If

        Catch ex As Exception
            Log(ex, "Minecraft 更新提示发送失败（" & If(VersionName, "Nothing") & "）", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 比较两个版本名；等同 Left >= Right。
    ''' 无法比较两个预发布版的大小。
    ''' 支持的格式：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    ''' </summary>
    Public Function CompareVersionGE(Left As String, Right As String) As Boolean
        Return CompareVersion(Left, Right) >= 0
    End Function
    ''' <summary>
    ''' 比较两个版本名，若 Left 较新则返回 1，相同则返回 0，Right 较新则返回 -1；等同 Left - Right。
    ''' 无法比较两个预发布版的大小。
    ''' 支持的格式：未知版本, 26.1-snapshot-1，1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    ''' </summary>
    Public Function CompareVersion(Left As String, Right As String) As Integer
        If Left = "未知版本" OrElse Right = "未知版本" Then
            If Left = "未知版本" AndAlso Right <> "未知版本" Then Return 1
            If Left = "未知版本" AndAlso Right = "未知版本" Then Return 0
            If Left <> "未知版本" AndAlso Right = "未知版本" Then Return -1
        End If
        Left = Left.ToLowerInvariant
        Right = Right.ToLowerInvariant
        Dim Lefts = RegexSearch(Left.Replace("快照", "snapshot").Replace("预览版", "pre"), "[a-z]+|[0-9]+")
        Dim Rights = RegexSearch(Right.Replace("快照", "snapshot").Replace("预览版", "pre"), "[a-z]+|[0-9]+")
        Dim i As Integer = 0
        While True
            '两边均缺失，感觉是一个东西
            If Lefts.Count - 1 < i AndAlso Rights.Count - 1 < i Then
                If Left > Right Then Return 1
                If Left < Right Then Return -1
                Return 0
            End If
            '确定两边的数值
            Dim LeftValue As String = If(Lefts.Count - 1 < i, 0, Lefts(i))
            Dim RightValue As String = If(Rights.Count - 1 < i, 0, Rights(i))
            If LeftValue = RightValue Then GoTo NextEntry
            If LeftValue = "rc" Then LeftValue = -1
            If LeftValue = "pre" Then LeftValue = -2
            If LeftValue = "snapshot" Then LeftValue = -3
            If LeftValue = "experimental" Then LeftValue = -4
            Dim LeftValValue = Val(LeftValue)
            If RightValue = "rc" Then RightValue = -1
            If RightValue = "pre" Then RightValue = -2
            If RightValue = "snapshot" Then RightValue = -3
            If RightValue = "experimental" Then RightValue = -4
            Dim RightValValue = Val(RightValue)
            If LeftValValue = 0 AndAlso RightValValue = 0 Then
                '如果没有数值则直接比较字符串
                If LeftValue > RightValue Then
                    Return 1
                ElseIf LeftValue < RightValue Then
                    Return -1
                End If
            Else
                '如果有数值则比较数值
                '这会使得一边是数字一边是字母时数字方更大
                If LeftValValue > RightValValue Then
                    Return 1
                ElseIf LeftValValue < RightValValue Then
                    Return -1
                End If
            End If
NextEntry:
            i += 1
        End While
        Return 0
    End Function
    ''' <summary>
    ''' 比较两个版本名的排序器。
    ''' </summary>
    Public Class VersionComparer
        Implements IComparer(Of String)
        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            Return CompareVersion(x, y)
        End Function
    End Class

    ''' <summary>
    ''' 打码字符串中的 AccessToken。
    ''' </summary>
    Public Function FilterAccessToken(Raw As String, FilterChar As Char) As String
        '打码 "accessToken " 后的内容
        If Raw.Contains("accessToken ") Then
            For Each Token In RegexSearch(Raw, "(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})")
                Raw = Raw.Replace(Token, New String(FilterChar, Token.Count))
            Next
        End If
        '打码当前登录的结果
        Dim AccessToken As String = McLoginLoader.Output.AccessToken
        If AccessToken IsNot Nothing AndAlso AccessToken.Length >= 10 AndAlso Raw.ContainsF(AccessToken, True) AndAlso
            McLoginLoader.Output.Uuid <> McLoginLoader.Output.AccessToken Then 'UUID 和 AccessToken 一样则不打码
            Raw = Raw.Replace(AccessToken, Left(AccessToken, 5) & New String(FilterChar, AccessToken.Length - 10) & Right(AccessToken, 5))
        End If
        Return Raw
    End Function
    ''' <summary>
    ''' 打码字符串中的 Windows 用户名。
    ''' </summary>
    Public Function FilterUserName(Raw As String, FilterChar As Char) As String
        If Raw.Contains(":\Users\") Then
            For Each Token In RegexSearch(Raw, "(?<=:\\Users\\)[^\\]+")
                Raw = Raw.Replace("\Users\" & Token, "\Users\" & New String(FilterChar, Token.Count))
            Next
        End If
        If Raw.Contains(":/Users/") Then
            For Each Token In RegexSearch(Raw, "(?<=:/Users/)[^/]+")
                Raw = Raw.Replace("/Users/" & Token, "/Users/" & New String(FilterChar, Token.Count))
            Next
        End If
        Return Raw
    End Function

End Module
