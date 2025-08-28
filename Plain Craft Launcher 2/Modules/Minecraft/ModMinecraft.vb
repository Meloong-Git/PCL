Imports System.IO.Compression
Imports PCL.Core.ProgramSetup
Imports NEWSetup = PCL.Core.ProgramSetup.Setup
Imports System.Text.Json.Nodes
Imports PCL.Core.IO
Imports PCL.Core.Utils

Public Module ModMinecraft

#Region "文件夹"

    ''' <summary>
    ''' 当前的 Minecraft 文件夹路径，以“\”结尾。
    ''' </summary>
    Public PathMcFolder As String
    ''' <summary>
    ''' 当前的 Minecraft 文件夹列表。
    ''' </summary>
    Public McFolderList As New List(Of McFolder)

    Public Class McFolder '必须是 Class，否则不是引用类型，在 ForEach 中不会得到刷新
        Public Name As String
        Public Path As String
        Public Type As McFolderType
        Public Overrides Function Equals(obj As Object) As Boolean
            If Not (TypeOf obj Is McFolder) Then Return False
            Dim folder = DirectCast(obj, McFolder)
            Return Name = folder.Name AndAlso Path = folder.Path AndAlso Type = folder.Type
        End Function
        Public Overrides Function ToString() As String
            Return Path
        End Function
    End Class
    Public Enum McFolderType
        Original
        RenamedOriginal
        Custom
    End Enum

    ''' <summary>
    ''' 加载 Minecraft 文件夹列表。
    ''' </summary>
    Public McFolderListLoader As New LoaderTask(Of Integer, Integer)("Minecraft Folder List", AddressOf McFolderListLoadSub, Priority:=ThreadPriority.AboveNormal)
    Private Sub McFolderListLoadSub()
        Try
            '初始化
            Dim cacheMcFolderList = New List(Of McFolder)

#Region "读取自定义（Custom）文件夹，可能没有结果"

            '格式：TMZ 12>C://xxx/xx/|Test>D://xxx/xx/|名称>路径
            For Each folder As String In Setup.Get("LaunchFolders").Split("|")
                If folder = "" Then Continue For
                If Not folder.Contains(">") OrElse Not folder.EndsWithF("\") Then
                    Hint("无效的 Minecraft 文件夹：" & folder, HintType.Critical)
                    Continue For
                End If
                Dim name As String = folder.Split(">")(0)
                Dim path As String = folder.Split(">")(1)
                Try
                    Files.CheckPermissionWithException(path)
                    cacheMcFolderList.Add(New McFolder With {.Name = name, .Path = path, .Type = McFolderType.Custom})
                Catch ex As Exception
                    MyMsgBox("失效的 Minecraft 文件夹：" & vbCrLf & path & vbCrLf & vbCrLf & GetExceptionSummary(ex), "Minecraft 文件夹失效", IsWarn:=True)
                    Log(ex, $"无法访问 Minecraft 文件夹 {path}")
                End Try
            Next

#End Region

#Region "读取默认（Original）文件夹，即当前、官启文件夹，可能没有结果"

            Dim currentMcFolderList = New List(Of McFolder)
            Dim originalMcFolderList = New List(Of McFolder)
            '扫描当前文件夹
            Try
                If Directory.Exists(Path & "versions\") Then originalMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Path = Path, .Type = McFolderType.Original})
                For Each folder As DirectoryInfo In New DirectoryInfo(Path).GetDirectories
                    If Directory.Exists(folder.FullName & "versions\") OrElse folder.Name = ".minecraft" Then
                        Dim newCurrentFolder As New McFolder With {.Name = folder.Name, .Path = folder.FullName & "\", .Type = McFolderType.Original}
                        originalMcFolderList.Add(newCurrentFolder)
                        currentMcFolderList.Add(newCurrentFolder)
                    End If
                Next
            Catch ex As Exception
                Log(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败")
            End Try

            '扫描官启文件夹
            Dim MojangPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\"
            If (Not currentMcFolderList.Any OrElse MojangPath <> currentMcFolderList(0).Path) AndAlso '当前文件夹不是官启文件夹
                Directory.Exists(MojangPath & "versions\") Then '具有权限且存在 versions 文件夹
                originalMcFolderList.Add(New McFolder With {.Name = "官方启动器文件夹", .Path = MojangPath, .Type = McFolderType.Original})
            End If

            Log(cacheMcFolderList.Count & " 个自定义文件夹，" & originalMcFolderList.Count & " 个原始文件夹")

            Dim unAdded = False
            For Each newOriginalFolder As McFolder In originalMcFolderList
                For Each cacheFolder As McFolder In cacheMcFolderList
                    If cacheFolder.Path = newOriginalFolder.Path Then
                        If cacheFolder.Name <> newOriginalFolder.Name Then
                            cacheFolder.Type = McFolderType.RenamedOriginal
                        Else
                            cacheFolder.Type = McFolderType.Original
                        End If
                        unAdded = True
                    End If
                Next
                If Not unAdded Then cacheMcFolderList.Add(newOriginalFolder) '如果没有重命名，则添加当前文件夹
            Next

#End Region

#Region "读取自定义文件夹情况并写入设置"

            '将自定义文件夹情况同步到设置
            Dim NewSetup As New List(Of String)
            For Each Folder As McFolder In cacheMcFolderList
                NewSetup.Add(Folder.Name & ">" & Folder.Path)
            Next
            If Not NewSetup.Any() Then NewSetup.Add("") '防止 0 元素 Join 返回 Nothing
            Setup.Set("LaunchFolders", Join(NewSetup, "|"))

#End Region

            '若没有可用文件夹，则创建 .minecraft
            If Not cacheMcFolderList.Any() Then
                Directory.CreateDirectory(Path & ".minecraft\versions\")
                cacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Path = Path & ".minecraft\", .Type = McFolderType.Original})
            End If

            For Each Folder As McFolder In cacheMcFolderList
#Region "更新 launcher_profiles.json"
                McFolderLauncherProfilesJsonCreate(Folder.Path)
#End Region
            Next
            If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomInteger(200, 2000))

            '回设
            McFolderList = cacheMcFolderList

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

#Region "实例处理"

    Public Const McInstanceCacheVersion As Integer = 30

    Private _McInstanceCurrent As McInstance
    Private _McInstanceLast = 0 '为 0 以保证与 Nothing 不相同，使得 UI 显示可以正常初始化
    ''' <summary>
    ''' 当前的 Minecraft 版本。
    ''' </summary>
    Public Property McInstanceCurrent As McInstance
        Get
            Return _McInstanceCurrent
        End Get
        Set(value As McInstance)
            If ReferenceEquals(_McInstanceLast, value) Then Return
            _McInstanceCurrent = value '由于有可能是 Nothing，导致无法初始化，才得这样弄一圈
            _McInstanceLast = value
            If value Is Nothing Then Return
            '重置缓存的 Mod 文件夹
            PageDownloadCompDetail.CachedFolder.Clear()
        End Set
    End Property

    Public Class McInstance

        ''' <summary>
        ''' 该实例的实例文件夹，以“\”结尾。
        ''' </summary>
        Public ReadOnly Property Path As String
        ''' <summary>
        ''' 应用版本隔离后，该实例所对应的 Minecraft 根文件夹，以“\”结尾。
        ''' </summary>
        Public ReadOnly Property PathIndie As String
            Get
                If Setup.IsUnset("VersionArgumentIndieV2", instance:=Me) Then
                    If Not IsLoaded Then Load()
                    '决定该实例是否应该被隔离
                    Dim ShouldBeIndie =
                    Function() As Boolean
                        '从老的实例独立设置中迁移：-1 未决定，0 使用全局设置，1 手动开启，2 手动关闭
                        If Not Setup.IsUnset("VersionArgumentIndie", instance:=Me) AndAlso Setup.Get("VersionArgumentIndie", instance:=Me) > 0 Then
                            Log($"[Minecraft] 版本隔离初始化（{Name}）：从老的实例独立设置中迁移")
                            Return Setup.Get("VersionArgumentIndie", instance:=Me) = 1
                        End If
                        '若实例文件夹下包含 mods 或 saves 文件夹，则自动开启版本隔离
                        Dim ModFolder As New DirectoryInfo(Path & "mods\")
                        Dim SaveFolder As New DirectoryInfo(Path & "saves\")
                        If (ModFolder.Exists AndAlso ModFolder.EnumerateFiles.Any) OrElse (SaveFolder.Exists AndAlso SaveFolder.EnumerateDirectories.Any) Then
                            Log($"[Minecraft] 版本隔离初始化（{Name}）：实例文件夹下存在 mods 或 saves 文件夹，自动开启")
                            Return True
                        End If
                        '根据全局的默认设置决定是否隔离
                        Dim IsRelease As Boolean = State <> McInstanceState.Fool AndAlso State <> McInstanceState.Old AndAlso State <> McInstanceState.Snapshot
                        Log($"[Minecraft] 版本隔离初始化（{Name}）：从全局默认设置中（{Setup.Get("LaunchArgumentIndieV2")}）判断，State {GetStringFromEnum(State)}，IsRelease {IsRelease}，Modable {Modable}")
                        Select Case Setup.Get("LaunchArgumentIndieV2")
                            Case 0 '关闭
                                Return False
                            Case 1 '仅隔离可安装 Mod 的实例
                                Return Version.HasLabyMod OrElse Modable
                            Case 2 '仅隔离非正式版
                                Return Not IsRelease
                            Case 3 '隔离非正式版与可安装 Mod 的实例
                                Return Version.HasLabyMod OrElse Modable OrElse Not IsRelease
                            Case Else '隔离所有实例
                                Return True
                        End Select
                    End Function
                    Setup.Set("VersionArgumentIndieV2", ShouldBeIndie(), instance:=Me)
                End If

                Return If(Setup.Get("VersionArgumentIndieV2", instance:=Me), Path, PathMcFolder)
            End Get
        End Property

        ''' <summary>
        ''' 该实例的实例文件夹名称。
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                If _Name Is Nothing AndAlso Not Path = "" Then _Name = GetFolderNameFromPath(Path)
                Return _Name
            End Get
        End Property
        Private _Name As String = Nothing

        ''' <summary>
        ''' 显示的描述文本。
        ''' </summary>
        Public Info As String = "该实例未被加载，请向作者反馈此问题"
        ''' <summary>
        ''' 该实例的列表检查原始结果，不受自定义影响。
        ''' </summary>
        Public State As McInstanceState = McInstanceState.Error
        ''' <summary>
        ''' 显示的实例图标。
        ''' </summary>
        Public Logo As String
        ''' <summary>
        ''' 是否为收藏的实例。
        ''' </summary>
        Public IsStar As Boolean = False
        ''' <summary>
        ''' 强制实例分类，0 为未启用，1 为隐藏，2 及以上为其他普通分类。
        ''' </summary>
        Public DisplayType As McInstanceCardType = McInstanceCardType.Auto
        ''' <summary>
        ''' 该实例是否可以安装 Mod。
        ''' </summary>
        Public ReadOnly Property Modable As Boolean
            Get
                If Not IsLoaded Then Load()
                Return Version.HasFabric OrElse Version.HasLegacyFabric OrElse Version.HasQuilt OrElse Version.HasForge OrElse Version.HasLiteLoader OrElse Version.HasNeoForge OrElse Version.HasCleanroom OrElse
                    DisplayType = McInstanceCardType.API '#223
            End Get
        End Property
        ''' <summary>
        ''' 实例信息。
        ''' </summary>
        Public Property Version As McInstanceInfo
            Get
                If _Version Is Nothing Then
                    _Version = New McInstanceInfo
#Region "获取游戏版本"
                    Try

                        '获取发布时间并判断是否为老版本
                        Try
                            If JsonObject("releaseTime") Is Nothing Then
                                ReleaseTime = New Date(1970, 1, 1, 15, 0, 0) '未知版本也可能显示为 1970 年
                            Else
                                ReleaseTime = JsonObject("releaseTime").ToObject(Of Date)
                            End If
                            If ReleaseTime.Year > 2000 AndAlso ReleaseTime <= New DateTime(2011, 11, 16) Then ' 2000 年到 2011 年 11 月 16 日之间
                                _Version.McName = "Old"
                                GoTo VersionSearchFinish
                            End If
                        Catch
                            ReleaseTime = New Date(1970, 1, 1, 15, 0, 0)
                        End Try
                        '实验性快照
                        If If(JsonObject("type"), "") = "pending" Then
                            _Version.McName = "pending"
                            GoTo VersionSearchFinish
                        End If
                        '从 PCL 下载的版本信息中获取版本号
                        If JsonObject("clientVersion") IsNot Nothing Then
                            _Version.McName = JsonObject("clientVersion")
                            GoTo VersionSearchFinish
                        End If
                        '从 HMCL 下载的版本信息中获取版本号
                        If JsonObject("patches") IsNot Nothing Then
                            For Each Patch As JObject In JsonObject("patches")
                                If If(Patch("id"), "").ToString = "game" AndAlso Patch("version") IsNot Nothing Then
                                    _Version.McName = Patch("version").ToString
                                    GoTo VersionSearchFinish
                                End If
                            Next
                        End If
                        '从 Forge / NeoForge / LabyMod Arguments 中获取版本号
                        If JsonObject("arguments") IsNot Nothing Then
                            If JsonObject("arguments")("game") IsNot Nothing Then
                                Dim Mark As Boolean = False
                                For Each Argument In JsonObject("arguments")("game")
                                    If Mark Then
                                        _Version.McName = Argument.ToString
                                        GoTo VersionSearchFinish
                                    End If
                                    If Argument.ToString = "--fml.mcVersion" Then Mark = True
                                Next
                            End If
                            If JsonObject("arguments")("jvm") IsNot Nothing Then
                                For Each Argument In JsonObject("arguments")("game")
                                    Dim RegexArgument = RegexSeek(Argument.ToString, "(?<=-Dnet.labymod.running-version=)1.[0-9+.]+")
                                    If RegexArgument IsNot Nothing Then
                                        _Version.McName = RegexArgument
                                        GoTo VersionSearchFinish
                                    End If
                                Next
                            End If
                        End If
                        '从继承实例中获取版本号
                        If Not InheritInstance = "" Then
                            _Version.McName = If(JsonObject("jar"), "").ToString 'LiteLoader 优先使用 Jar
                            If _Version.McName = "" Then _Version.McName = InheritInstance
                            GoTo VersionSearchFinish
                        End If
                        '从下载地址中获取版本号
                        Dim Regex As String = RegexSeek(If(JsonObject("downloads"), "").ToString, "(?<=launcher.mojang.com/mc/game/)[^/]*")
                        If Regex IsNot Nothing Then
                            _Version.McName = Regex
                            GoTo VersionSearchFinish
                        End If
                        '从 Forge 版本中获取版本号
                        Dim LibrariesString As String = JsonObject("libraries").ToString
                        Regex = If(RegexSeek(LibrariesString, "(?<=net.minecraftforge:forge:)1.[0-9+.]+"), RegexSeek(LibrariesString, "(?<=net.minecraftforge:fmlloader:)1.[0-9+.]+"))
                        If Regex IsNot Nothing Then
                            _Version.McName = Regex
                            GoTo VersionSearchFinish
                        End If
                        '从 OptiFine 版本中获取版本号
                        Regex = RegexSeek(LibrariesString, "(?<=optifine:OptiFine:)1.[0-9+.]+")
                        If Regex IsNot Nothing Then
                            _Version.McName = Regex
                            GoTo VersionSearchFinish
                        End If
                        '从 Fabric / Quilt / Legacy Fabric 版本中获取版本号
                        Regex = RegexSeek(LibrariesString, "(?<=((fabricmc)|(quiltmc)|(legacyfabric)):intermediary:)[^""]*")
                        If Regex IsNot Nothing Then
                            _Version.McName = Regex
                            GoTo VersionSearchFinish
                        End If
                        '从 jar 项中获取版本号
                        If JsonObject("jar") IsNot Nothing Then
                            _Version.McName = JsonObject("jar").ToString
                            GoTo VersionSearchFinish
                        End If
                        '从 jar 文件的 version.json 中获取版本号
                        If JsonVersion?("name") IsNot Nothing Then
                            Dim JsonVerName As String = JsonVersion("name").ToString
                            If JsonVerName.Length < 32 Then '因为 wiki 说这玩意儿可能是个 hash，虽然我没发现
                                _Version.McName = JsonVerName
                                Log("[Minecraft] 从版本 jar 中的 version.json 获取到版本号：" & JsonVerName)
                                GoTo VersionSearchFinish
                            End If
                        End If
                        '非准确的版本判断警告
                        Log("[Minecraft] 无法完全确认 MC 版本号的实例：" & Name)
                        '从文件夹名中获取
                        Regex = RegexSeek(Name, "([0-9w]{5}[a-z]{1})|(1\.[0-9]+(\.[0-9]+)?(-(pre|rc)[1-9]?| Pre-Release( [1-9]{1})?)?)", RegularExpressions.RegexOptions.IgnoreCase)
                        If Regex IsNot Nothing Then
                            _Version.McName = Regex
                            GoTo VersionSearchFinish
                        End If
                        '从 Json 出现的版本号中获取
                        Dim JsonRaw As JObject = JsonObject.DeepClone()
                        JsonRaw.Remove("libraries")
                        Dim JsonRawText As String = JsonRaw.ToString
                        Regex = RegexSeek(JsonRawText, "([0-9w]{5}[a-z]{1})|(1\.[0-9]+(\.[0-9]+)?(-(pre|rc)[1-9]?| Pre-Release( [1-9]{1})?)?)", RegularExpressions.RegexOptions.IgnoreCase)
                        If Regex IsNot Nothing Then
                            _Version.McName = Regex
                            GoTo VersionSearchFinish
                        End If
                        '无法获取
                        _Version.McName = "Unknown"
                        Info = "PCL 无法识别该实例的 MC 版本号"
                    Catch ex As Exception
                        Log(ex, "识别 Minecraft 版本时出错")
                        _Version.McName = "Unknown"
                        Info = "无法识别：" & ex.Message
                    End Try
VersionSearchFinish:
                    '获取版本号
                    If _Version.McName.StartsWithF("1.") Then
                        Dim SplitVersion = _Version.McName.Split(" "c, "_"c, "-"c, "."c)
                        Dim SplitResult As String
                        '分割获取信息
                        SplitResult = If(SplitVersion.Count >= 2, SplitVersion(1), "0")
                        _Version.McCodeMain = If(SplitResult.Length <= 2, Val(SplitResult), "0")
                        SplitResult = If(SplitVersion.Count >= 3, SplitVersion(2), "0")
                        _Version.McCodeSub = If(SplitResult.Length <= 2, Val(SplitResult), "0")
                    ElseIf (Not IsVersionNameLikeRelease(_Version.McName)) OrElse _Version.McName = "pending" Then
                        _Version.McCodeMain = 99
                        _Version.McCodeSub = 99
                    End If
#End Region
                End If
                Return _Version
            End Get
            Set(value As McInstanceInfo)
                _Version = value
            End Set
        End Property
        Private _Version As McInstanceInfo = Nothing

        ''' <summary>
        ''' 实例的发布时间。
        ''' </summary>
        Public ReleaseTime As New Date(1970, 1, 1, 15, 0, 0)

        ''' <summary>
        ''' 该实例的 JSON 文本。
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
                    Dim JsonPath As String = Path & Name & ".json"
                    If Not File.Exists(JsonPath) Then
                        '如果文件夹下只有一个 JSON 文件，则将其作为实例 JSON
                        Dim JsonFiles As String() = Directory.GetFiles(Path, "*.json")
                        If JsonFiles.Count = 1 Then
                            JsonPath = JsonFiles(0)
                            Log("[Minecraft] 未找到同名实例 JSON，自动换用 " & JsonPath, LogLevel.Debug)
                        Else
                            Throw New Exception($"未找到实例 JSON 文件：{Path}{Name}.json")
                        End If
                    End If
                    _JsonText = ReadFile(JsonPath)
                    '如果 ReadFile 失败会返回空字符串；这可能是由于文件被临时占用，故延时后重试
                    If Not FastJsonCheck(_JsonText) Then
                        If RunInUi() Then
                            Log("[Minecraft] 实例 JSON 文件为空或有误，由于代码在主线程运行，将不再进行重试", LogLevel.Debug)
                            GetJson(_JsonText) '触发异常
                        Else
                            Log($"[Minecraft] 实例 JSON 文件为空或有误，将在 2s 后重试读取（{JsonPath}）", LogLevel.Debug)
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
        ''' 该实例的 JSON 对象。
        ''' 若 JSON 存在问题，在获取该属性时即会抛出异常。
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
                            '合并 JSON
                            'Dim HasOptiFine As Boolean = False, HasForge As Boolean = False
                            Dim CurrentObject As JObject = Nothing
                            Dim SubjsonList As New List(Of JObject)
                            For Each Subjson As JObject In _JsonObject("patches")
                                SubjsonList.Add(Subjson)
                            Next
                            SubjsonList.Sort(Function(left, right) Val(If(left("priority"), "0").ToString) < Val(If(right("priority"), "0").ToString))
                            For Each Subjson As JObject In SubjsonList
                                Dim Id As String = Subjson("id")
                                If Id IsNot Nothing Then
                                    '合并 JSON
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
                        '与继承实例合并
                        Dim InheritInstance = Nothing
                        Try
                            InheritInstance = If(_JsonObject("inheritsFrom") Is Nothing, "", _JsonObject("inheritsFrom").ToString)
                            If InheritInstance = Name Then
                                Log("[Minecraft] 自引用的继承实例：" & Name, LogLevel.Debug)
                                InheritInstance = ""
                                Exit Try
                            End If
Recheck:
                            If InheritInstance <> "" Then
                                Dim Inherit As New McInstance(InheritInstance)
                                '继续循环
                                If Inherit.InheritInstance = InheritInstance Then Throw New Exception("版本依赖项出现嵌套：" & InheritInstance)
                                InheritInstance = Inherit.InheritInstance
                                '合并
                                Inherit.JsonObject.Merge(_JsonObject)
                                _JsonObject = Inherit.JsonObject
                                GoTo Recheck
                            End If
                        Catch ex As Exception
                            Log(ex, "合并实例依赖项 JSON 失败（" & If(InheritInstance, "null").ToString & "）")
                        End Try
                    Catch ex As Exception
                        Throw New Exception("初始化实例 JSON 时失败（" & If(Name, "null") & "）", ex)
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
        ''' 是否为旧版 JSON 格式。
        ''' </summary>
        Public ReadOnly Property IsOldJson As Boolean
            Get
                Return JsonObject("minecraftArguments") IsNot Nothing AndAlso JsonObject("minecraftArguments") <> ""
            End Get
        End Property
        ''' <summary>
        ''' JSON 是否为 HMCL 格式。
        ''' </summary>
        Public Property IsHmclFormatJson As Boolean = False

        ''' <summary>
        ''' 实例 JAR 中的 version.json 文件对象。
        ''' 若没有则返回 Nothing。
        ''' </summary>
        Public ReadOnly Property JsonVersion As JObject
            Get
                If Not JsonVersionInited Then
                    JsonVersionInited = True
                    If File.Exists(Path & Name & ".jar") Then
                        Try
                            Using JarArchive As New ZipArchive(New FileStream(Path & Name & ".jar", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                Dim VersionJson As ZipArchiveEntry = JarArchive.GetEntry("version.json")
                                If VersionJson IsNot Nothing Then
                                    Using VersionJsonStream As New StreamReader(VersionJson.Open)
                                        _JsonVersion = GetJson(VersionJsonStream.ReadToEnd)
                                    End Using
                                End If
                            End Using
                        Catch ex As Exception
                            Log(ex, "从实例 JAR 中读取 version.json 失败")
                        End Try
                    End If
                End If
                Return _JsonVersion
            End Get
        End Property
        Private JsonVersionInited As Boolean = False
        Private _JsonVersion As JObject = Nothing

        ''' <summary>
        ''' 该实例的依赖实例。若无依赖实例则为空字符串。
        ''' </summary>
        Public ReadOnly Property InheritInstance As String
            Get
                If _InheritVersion Is Nothing Then
                    _InheritVersion = If(JsonObject("inheritsFrom"), "").ToString
                    '由于过老的 LiteLoader 中没有 Inherits（例如 1.5.2），需要手动判断以获取真实继承实例
                    '此外，由于这里的加载早于实例种类判断，所以需要手动判断是否为 LiteLoader
                    '如果实例提供了不同的 JAR，代表所需的 JAR 可能已被更改，则跳过 Inherit 替换
                    If JsonText.Contains("liteloader") AndAlso Version.McName <> Name AndAlso Not JsonText.Contains("logging") Then
                        If If(JsonObject("jar"), Version.McName).ToString = Version.McName Then _InheritVersion = Version.McName
                    End If
                    'HMCL 实例无 JSON
                    If IsHmclFormatJson Then _InheritVersion = ""
                End If
                Return _InheritVersion
            End Get
        End Property
        Private _InheritVersion As String = Nothing

        ''' <summary></summary>
        ''' <param name="Path">实例名，或实例文件夹的完整路径（不规定是否以 \ 结尾）。</param>
        Public Sub New(Path As String)
            Me.Path = If(Path.Contains(":"), "", PathMcFolder & "versions\") & '补全完整路径
                      Path &
                      If(Path.EndsWithF("\"), "", "\") '补全右划线
        End Sub

        ''' <summary>
        ''' 检查 Minecraft 版本，若检查通过 State 则为 Original 且返回 True。
        ''' </summary>
        Public Function Check() As Boolean

            '检查文件夹
            If Not Directory.Exists(Path) Then
                State = McInstanceState.Error
                Info = "未找到实例 " & Name
                Return False
            End If
            '检查权限
            Try
                Directory.CreateDirectory(Path & "PCL\")
                Files.CheckPermissionWithException(Path & "PCL\")
            Catch ex As Exception
                State = McInstanceState.Error
                Info = "PCL 没有对该文件夹的访问权限，请右键以管理员身份运行 PCL"
                Log(ex, "没有访问实例文件夹的权限")
                Return False
            End Try
            '确认 JSON 可用性
            Try
                Dim JsonObjCheck = JsonObject
            Catch ex As Exception
                Log(ex, "实例 JSON 可用性检查失败（" & Path & "）")
                JsonText = ""
                JsonObject = Nothing
                Info = ex.Message
                State = McInstanceState.Error
                Return False
            End Try
            '检查依赖实例
            Try
                If Not InheritInstance = "" Then
                    If Not File.Exists(GetPathFromFullPath(Path) & InheritInstance & "\" & InheritInstance & ".json") Then
                        State = McInstanceState.Error
                        Info = "需要安装 " & InheritInstance & " 作为前置实例"
                        Return False
                    End If
                End If
            Catch ex As Exception
                Log(ex, "依赖实例检查出错（" & Name & "）")
                State = McInstanceState.Error
                Info = "未知错误：" & GetExceptionSummary(ex)
                Return False
            End Try

            State = McInstanceState.Original
            Return True
        End Function
        ''' <summary>
        ''' 加载 Minecraft 实例的详细信息。不使用其缓存，且会更新缓存。
        ''' </summary>
        Public Function Load() As McInstance
            Try
                '检查实例，若出错则跳过数据确定阶段
                If Not Check() Then GoTo ExitDataLoad
#Region "确定实例分类"
                Select Case Version.McName '在获取 Version.Original 对象时会完成它的加载
                    Case "Unknown"
                        State = McInstanceState.Error
                    Case "Old"
                        State = McInstanceState.Old
                    Case Else '根据 API 进行筛选
                        Dim RealJson As String = If(JsonObject, JsonText).ToString
                        '愚人节与快照版本
                        If If(JsonObject("type"), "").ToString = "fool" OrElse GetMcFoolName(Version.McName) <> "" Then
                            State = McInstanceState.Fool
                        ElseIf Version.McName.ContainsF("w", True) OrElse Name.ContainsF("combat", True) OrElse Version.McName.ContainsF("rc", True) OrElse Version.McName.ContainsF("pre", True) OrElse Version.McName.ContainsF("experimental", True) OrElse If(JsonObject("type"), "").ToString = "snapshot" OrElse If(JsonObject("type"), "").ToString = "pending" Then
                            State = McInstanceState.Snapshot
                        End If
                        'OptiFine
                        If RealJson.Contains("optifine") Then
                            State = McInstanceState.OptiFine
                            Version.HasOptiFine = True
                            Version.OptiFineVersion = If(RegexSeek(RealJson, "(?<=HD_U_)[^"":/]+"), "未知版本")
                        End If
                        'LiteLoader
                        If RealJson.Contains("liteloader") Then
                            State = McInstanceState.LiteLoader
                            Version.HasLiteLoader = True
                        End If
                        'Fabric、Forge、Quilt、LabyMod、Legacy Fabric
                        If RealJson.Contains("labymod_data") Then
                            State = McInstanceState.LabyMod
                            Version.HasLabyMod = True
                            Version.LabyModVersion = JsonObject("labymod_data")("version")
                        ElseIf RealJson.Contains("net.legacyfabric:intermediary") Then
                            State = McInstanceState.LegacyFabric
                            Version.HasLegacyFabric = True
                            Version.LegacyFabricVersion = If(RegexSeek(RealJson, "(?<=(net.fabricmc:fabric-loader:))[0-9\.]+(\+build.[0-9]+)?"), "未知版本").Replace("+build", "")
                        ElseIf RealJson.Contains("net.fabricmc:fabric-loader") Then
                            State = McInstanceState.Fabric
                            Version.HasFabric = True
                            Version.FabricVersion = If(RegexSeek(RealJson, "(?<=(net.fabricmc:fabric-loader:))[0-9\.]+(\+build.[0-9]+)?"), "未知版本").Replace("+build", "")
                        ElseIf RealJson.Contains("org.quiltmc:quilt-loader") Then
                            State = McInstanceState.Quilt
                            Version.HasQuilt = True
                            Version.QuiltVersion = If(RegexSeek(RealJson, "(?<=(org.quiltmc:quilt-loader:))[0-9\.]+(\+build.[0-9]+)?((-beta.)[0-9]([0-9]?))"), "未知版本").Replace("+build", "")
                        ElseIf RealJson.Contains("com.cleanroommc:cleanroom:") Then
                            State = McInstanceState.Cleanroom
                            Version.HasCleanroom = True
                            Version.CleanroomVersion = If(RegexSeek(RealJson, "(?<=(com.cleanroommc:cleanroom:))[0-9\.]+(\+build.[0-9]+)?(-alpha)?"), "未知版本").Replace("+build", "")
                        ElseIf RealJson.Contains("minecraftforge") AndAlso Not RealJson.Contains("net.neoforge") Then
                            State = McInstanceState.Forge
                            Version.HasForge = True
                            Version.ForgeVersion = RegexSeek(RealJson, "(?<=forge:[0-9\.]+(_pre[0-9]*)?\-)[0-9\.]+")
                            If Version.ForgeVersion Is Nothing Then Version.ForgeVersion = RegexSeek(RealJson, "(?<=net\.minecraftforge:minecraftforge:)[0-9\.]+")
                            If Version.ForgeVersion Is Nothing Then Version.ForgeVersion = If(RegexSeek(RealJson, "(?<=net\.minecraftforge:fmlloader:[0-9\.]+-)[0-9\.]+"), "未知版本")
                        ElseIf RealJson.Contains("net.neoforge") Then
                            '1.20.1 JSON 范例："--fml.forgeVersion", "47.1.99"
                            '1.20.2+ JSON 范例："--fml.neoForgeVersion", "20.6.119-beta"
                            State = McInstanceState.NeoForge
                            Version.HasNeoForge = True
                            Version.NeoForgeVersion = If(RegexSeek(RealJson, "(?<=orgeVersion"",[^""]*?"")[^""]+(?="",)"), "未知版本")
                        End If
                        Version.IsApiLoaded = True
                End Select
#End Region
ExitDataLoad:
                '确定实例图标
                Logo = NEWSetup.Instance.LogoPath(Path)
                If Logo = "" OrElse Not NEWSetup.Instance.IsLogoCustom(Path) Then
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
                        Case McInstanceState.Cleanroom
                            Logo = PathImage & "Blocks/Cleanroom.png"
                        Case McInstanceState.Fabric
                            Logo = PathImage & "Blocks/Fabric.png"
                        Case McInstanceState.LegacyFabric
                            Logo = PathImage & "Blocks/Fabric.png"
                        Case McInstanceState.Quilt
                            Logo = PathImage & "Blocks/Quilt.png"
                        Case McInstanceState.OptiFine
                            Logo = PathImage & "Blocks/GrassPath.png"
                        Case McInstanceState.LiteLoader
                            Logo = PathImage & "Blocks/Egg.png"
                        Case McInstanceState.Fool
                            Logo = PathImage & "Blocks/GoldBlock.png"
                        Case McInstanceState.LabyMod
                            Logo = PathImage & "Blocks/LabyMod.png"
                        Case Else
                            Logo = PathImage & "Blocks/RedstoneBlock.png"
                    End Select
                End If
                '确定实例描述
                Dim CustomInfo As String = NEWSetup.Instance.CustomInfo(Path)
                Info = If(CustomInfo <> "", CustomInfo, GetDefaultDescription())
                '确定实例收藏状态
                IsStar = NEWSetup.Instance.Starred(Path)
                '确定实例显示种类
                DisplayType = NEWSetup.Instance.DisplayType(Path)
                '写入缓存
                If Directory.Exists(Path) Then
                    NEWSetup.Instance.State(Path) = State
                    NEWSetup.Instance.Info(Path) = Info
                    NEWSetup.Instance.LogoPath(Path) = Logo
                End If
                If State <> McInstanceState.Error Then
                    NEWSetup.Instance.ReleaseTime(Path) = ReleaseTime.ToString("yyyy'-'MM'-'dd HH':'mm")
                    NEWSetup.Instance.FabricVersion(Path) = Version.FabricVersion
                    NEWSetup.Instance.LegacyFabricVersion(Path) = Version.LegacyFabricVersion
                    NEWSetup.Instance.QuiltVersion(Path) = Version.QuiltVersion
                    NEWSetup.Instance.LabyModVersion(Path) = Version.LabyModVersion
                    NEWSetup.Instance.OptiFineVersion(Path) = Version.OptiFineVersion
                    NEWSetup.Instance.HasLiteLoader(Path) = Version.HasLiteLoader
                    NEWSetup.Instance.ForgeVersion(Path) = Version.ForgeVersion
                    NEWSetup.Instance.NeoForgeVersion(Path) = Version.NeoForgeVersion
                    NEWSetup.Instance.CleanroomVersion(Path) = Version.CleanroomVersion
                    NEWSetup.Instance.SortCode(Path) = Version.SortCode
                    NEWSetup.Instance.McVersion(Path) = Version.McName
                    NEWSetup.Instance.VersionMajor(Path) = Version.McCodeMain
                    NEWSetup.Instance.VersionMinor(Path) = Version.McCodeSub
                End If
            Catch ex As Exception
                Info = "未知错误：" & GetExceptionSummary(ex)
                Logo = PathImage & "Blocks/RedstoneBlock.png"
                State = McInstanceState.Error
                Log(ex, "加载实例失败（" & Name & "）", LogLevel.Feedback)
            Finally
                IsLoaded = True
            End Try
            Return Me
        End Function
        ''' <summary>
        ''' 获取实例的默认描述。
        ''' </summary>
        Public Function GetDefaultDescription() As String
            Dim Info As String = ""
            Select Case State
                Case McInstanceState.Snapshot
                    If Version.McName.ContainsF("pre", True) Then
                        Info = "预发布版 " & Version.McName
                    ElseIf Version.McName.ContainsF("rc", True) Then
                        Info = "发布候选 " & Version.McName
                    ElseIf Version.McName.Contains("experimental") OrElse Version.McName = "pending" Then
                        Info = "实验性快照"
                    Else
                        Info = "快照 " & Version.McName
                    End If
                Case McInstanceState.Old
                    Info = "远古版本"
                Case McInstanceState.Original, McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.Fabric, McInstanceState.LegacyFabric, McInstanceState.Quilt, McInstanceState.LabyMod, McInstanceState.OptiFine, McInstanceState.LiteLoader, McInstanceState.Cleanroom
                    Info = Version.ToString
                Case McInstanceState.Fool
                    Info = GetMcFoolName(Version.McName)
                Case McInstanceState.Error
                    Return Me.Info '已有错误信息
                Case Else
                    Info = "发生了未知错误，请向作者反馈此问题"
            End Select
            Return Info
        End Function

        Public IsLoaded As Boolean = False

        Public Overrides Function Equals(obj As Object) As Boolean
            Dim version = TryCast(obj, McInstance)
            Return version IsNot Nothing AndAlso Path = version.Path
        End Function
        Public Shared Operator =(a As McInstance, b As McInstance) As Boolean
            If a Is Nothing AndAlso b Is Nothing Then Return True
            If a Is Nothing OrElse b Is Nothing Then Return False
            Return a.Path = b.Path
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
        LegacyFabric
        Quilt
        Cleanroom
        LabyMod
    End Enum

    ''' <summary>
    ''' 某个 Minecraft 实例的版本名、附加组件信息。
    ''' </summary>
    Public Class McInstanceInfo

        ''' <summary>
        ''' 实例的 API 信息是否已加载。
        ''' </summary>
        Public IsApiLoaded As Boolean = False

        '原版

        ''' <summary>
        ''' 原版版本名。如 1.12.2，16w01a。
        ''' </summary>
        Public McName As String
        ''' <summary>
        ''' 原版主版本号，如 12（For 1.12.2），快照则固定为 99。不可用则为 -1。
        ''' </summary>
        Public McCodeMain As Integer = -1
        ''' <summary>
        ''' 原版次版本号，如 2（For 1.12.2），快照则固定为 99。不可用则为 -1。
        ''' </summary>
        Public McCodeSub As Integer = -1
        ''' <summary>
        ''' 是否为非快照版，且读取到了一个有效的原版版本号。
        ''' </summary>
        Public ReadOnly Property IsStandardVersion As Boolean
            Get
                Return McCodeMain > -1 AndAlso McCodeMain < 99 AndAlso McCodeSub > -1 AndAlso McCodeSub < 99
            End Get
        End Property
        ''' <summary>
        ''' 标准的原版版本号。
        ''' 若为快照版或没有有效版本号，则返回 0。
        ''' </summary>
        Public ReadOnly Property McInstance As Version
            Get
                If Not IsStandardVersion Then Return New Version(0, 0, 0)
                Return New Version(1, McCodeMain, McCodeSub)
            End Get
        End Property

        'OptiFine

        ''' <summary>
        ''' 该实例是否通过 JSON 安装了 OptiFine。
        ''' </summary>
        Public HasOptiFine As Boolean = False
        ''' <summary>
        ''' OptiFine 版本号，如 C8、C9_pre10。
        ''' </summary>
        Public OptiFineVersion As String = ""

        'Forge

        ''' <summary>
        ''' 该实例是否安装了 Forge。
        ''' </summary>
        Public HasForge As Boolean = False
        ''' <summary>
        ''' Forge 版本号，如 31.1.2、14.23.5.2847。
        ''' </summary>
        Public ForgeVersion As String = ""

        'NeoForge

        ''' <summary>
        ''' 该实例是否安装了 NeoForge。
        ''' </summary>
        Public HasNeoForge As Boolean = False
        ''' <summary>
        ''' NeoForge 版本号，如 21.0.2-beta、47.1.79。
        ''' </summary>
        Public NeoForgeVersion As String = ""

        'Cleanroom

        ''' <summary>
        ''' 该实例是否安装了 Cleanroom。
        ''' </summary>
        Public HasCleanroom As Boolean = False
        ''' <summary>
        ''' Cleanroom 版本号，如 0.2.4-alpha。
        ''' </summary>
        Public CleanroomVersion As String = ""

        'Fabric

        ''' <summary>
        ''' 该实例是否安装了 Fabric。
        ''' </summary>
        Public HasFabric As Boolean = False
        ''' <summary>
        ''' Fabric 版本号，如 0.7.2.175。
        ''' </summary>
        Public FabricVersion As String = ""

        'LegacyFabric

        ''' <summary>
        ''' 该实例是否安装了 Fabric。
        ''' </summary>
        Public HasLegacyFabric As Boolean = False
        ''' <summary>
        ''' Fabric 版本号，如 0.7.2.175。
        ''' </summary>
        Public LegacyFabricVersion As String = ""


        'Quilt

        ''' <summary>
        ''' 该实例是否安装了 Quilt。
        ''' </summary>
        Public HasQuilt As Boolean = False
        ''' <summary>
        ''' Quilt 版本号，如 0.26.1-beta.1、0.26.0。
        ''' </summary>
        Public QuiltVersion As String = ""

        'LabyMod

        ''' <summary>
        ''' 该实例是否安装了 LabyMod。
        ''' </summary>
        Public HasLabyMod As Boolean = False
        ''' <summary>
        ''' LabyMod 版本号，如 4.2.59。
        ''' </summary>
        Public LabyModVersion As String = ""

        'LiteLoader

        ''' <summary>
        ''' 该实例是否安装了 LiteLoader。
        ''' </summary>
        Public HasLiteLoader As Boolean = False

        'API

        ''' <summary>
        ''' 生成对此实例信息的用户友好的描述性字符串。
        ''' </summary>
        Public Overrides Function ToString() As String
            ToString = ""
            If HasForge Then ToString += ", Forge" & If(ForgeVersion = "未知版本", "", " " & ForgeVersion)
            If HasNeoForge Then ToString += ", NeoForge" & If(NeoForgeVersion = "未知版本", "", " " & NeoForgeVersion)
            If HasCleanroom Then ToString += ", Cleanroom" & If(CleanroomVersion = "未知版本", "", " " & CleanroomVersion)
            If HasFabric Then ToString += ", Fabric" & If(FabricVersion = "未知版本", "", " " & FabricVersion)
            If HasLegacyFabric Then ToString += ", LegacyFabric" & If(LegacyFabricVersion = "未知版本", "", " " & LegacyFabricVersion)
            If HasQuilt Then ToString += ", Quilt" & If(QuiltVersion = "未知版本", "", " " & QuiltVersion)
            If HasLabyMod Then ToString += ", LabyMod" & If(LabyModVersion = "未知版本", "", " " & LabyModVersion)
            If HasOptiFine Then ToString += ", OptiFine" & If(OptiFineVersion = "未知版本", "", " " & OptiFineVersion)
            If HasLiteLoader Then ToString += ", LiteLoader"
            If ToString = "" Then
                Return "原版 " & McName
            Else
                Return McName & ToString & If(ModeDebug, " (" & SortCode & "#)", "")
            End If
        End Function

        ''' <summary>
        ''' 用于排序比较的编号。
        ''' </summary>
        Public Property SortCode As Integer
            Get
                If _SortCode = -2 Then
                    '初始化
                    Try
                        If HasLegacyFabric Then
                            If LegacyFabricVersion = "未知版本" Then Return 0
                            Dim SubVersions = FabricVersion.Split(".")
                            If SubVersions.Length >= 3 Then
                                _SortCode = Val(SubVersions(0)) * 10000 + Val(SubVersions(1)) * 100 + Val(SubVersions(2))
                            Else
                                Throw New Exception("无效的 LegacyFabric 版本：" & LegacyFabricVersion)
                            End If
                        ElseIf HasFabric Then
                            If FabricVersion = "未知版本" Then Return 0
                            Dim SubVersions = FabricVersion.Split(".")
                            If SubVersions.Length >= 3 Then
                                _SortCode = Val(SubVersions(0)) * 10000 + Val(SubVersions(1)) * 100 + Val(SubVersions(2))
                            Else
                                Throw New Exception("无效的 Fabric 版本：" & FabricVersion)
                            End If
                        ElseIf HasQuilt Then
                            If QuiltVersion = "未知版本" Then Return 0
                            Dim IsBeta As Boolean = QuiltVersion.Contains("-beta")
                            Dim SubVersions = QuiltVersion.Replace("-beta", "").Split(".")
                            If SubVersions.Length >= 3 Then
                                _SortCode = Val(SubVersions(0)) * 10000 + Val(SubVersions(1)) * 100 + Val(SubVersions(2)) + IsBeta
                            Else
                                Throw New Exception("无效的 Quilt 版本：" & QuiltVersion)
                            End If
                        ElseIf HasCleanroom Then
                            If CleanroomVersion = "未知版本" Then Return 0
                            Dim IsAlpha As Boolean = CleanroomVersion.Contains("-alpha")
                            Dim SubVersions = CleanroomVersion.Replace("-alpha", "").Split(".")
                            If SubVersions.Length >= 3 Then
                                _SortCode = Val(SubVersions(0)) * 10000 + Val(SubVersions(1)) * 100 + Val(SubVersions(2)) + IsAlpha
                            Else
                                Throw New Exception("无效的 Cleanroom 版本：" & CleanroomVersion)
                            End If
                        ElseIf HasForge OrElse HasNeoForge Then
                            If ForgeVersion = "未知版本" AndAlso NeoForgeVersion = "未知版本" Then Return 0
                            Dim SubVersions = If(HasForge, ForgeVersion.Split("."), NeoForgeVersion.Split("."))
                            If SubVersions.Length = 4 Then
                                _SortCode = Val(SubVersions(0)) * 1000000 + Val(SubVersions(1)) * 10000 + Val(SubVersions(3))
                            ElseIf SubVersions.Length = 3 Then
                                _SortCode = Val(SubVersions(0)) * 1000000 + Val(SubVersions(1)) * 10000 + Val(SubVersions(2))
                            Else
                                Throw New Exception("无效的 Neo/Forge 版本：" & ForgeVersion)
                            End If
                        ElseIf HasLabyMod Then
                            If LabyModVersion = "未知版本" Then Return 0
                            Dim SubVersions = LabyModVersion.Split(".")
                            If SubVersions.Length = 4 Then
                                _SortCode = Val(SubVersions(0)) * 1000000 + Val(SubVersions(1)) * 10000 + Val(SubVersions(3))
                            ElseIf SubVersions.Length = 3 Then
                                _SortCode = Val(SubVersions(0)) * 1000000 + Val(SubVersions(1)) * 10000 + Val(SubVersions(2))
                            Else
                                Throw New Exception("无效的 LabyMod 版本：" & LabyModVersion)
                            End If
                        ElseIf HasOptiFine Then
                            If OptiFineVersion = "未知版本" Then Return 0
                            '由对应原版次级版本号（2 位）、字母（2 位）、末尾数字（2 位）、测试标记（2 位，正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）组成
                            _SortCode =
                                If(McCodeSub >= 0, McCodeSub, 0) * 1000000 +                    '第一段：原版次级版本号（2 位）
                                (Asc(CType(Left(OptiFineVersion.ToUpper, 1), Char)) - Asc("A"c) + 1) * 10000 + '第二段：字母编号（2 位），如 G2 中的 G（7）
                                Val(RegexSeek(Right(OptiFineVersion, OptiFineVersion.Length - 1), "[0-9]+")) * 100         '第三段：末尾数字（2 位），如 C5 beta4 中的 5
                            '第三段：测试标记
                            If OptiFineVersion.ContainsF("pre", True) Then _SortCode += 50
                            If OptiFineVersion.ContainsF("pre", True) OrElse OptiFineVersion.ContainsF("beta", True) Then
                                If Val(Right(OptiFineVersion, 1)) = 0 AndAlso Right(OptiFineVersion, 1) <> "0" Then
                                    _SortCode += 1 '为 pre 或 beta 结尾，视作 1
                                Else
                                    _SortCode += Val(RegexSeek(OptiFineVersion.ToLower, "(?<=((pre)|(beta)))[0-9]+"))
                                End If
                            Else
                                _SortCode += 99
                            End If
                        Else
                            _SortCode = -1
                        End If
                    Catch ex As Exception
                        _SortCode = -1
                        Log(ex, "获取 API 版本信息失败：" & ToString())
                    End Try
                End If
                Return _SortCode
            End Get
            Set(value As Integer)
                _SortCode = value
            End Set
        End Property
        Private _SortCode As Integer = -2

    End Class

    ''' <summary>
    ''' 根据版本名获取对应的愚人节版本描述。非愚人节版本会返回空字符串。
    ''' </summary>
    Public Function GetMcFoolName(Name As String) As String
        Name = Name.ToLower
        If Name.StartsWithF("2.0") OrElse Name.StartsWithF("2point0") Then
            Dim Tag = ""
            If Name.EndsWith("red") Then
                Tag = "（红色版本）"
            ElseIf Name.EndsWith("blue") Then
                Tag = "（蓝色版本）"
            ElseIf Name.EndsWith("purple") Then
                Tag = "（紫色版本）"
            End If
            Return "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！" & Tag
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

#Region "实例列表加载"

    ''' <summary>
    ''' 是否要求本次加载强制刷新实例列表。
    ''' </summary>
    Public McInstanceListForceRefresh As Boolean = False
    ''' <summary>
    ''' 加载 Minecraft 文件夹的实例列表。
    ''' </summary>
    Public McInstanceListLoader As New LoaderTask(Of String, Integer)("Minecraft Version List", AddressOf McInstanceListLoad) With {.ReloadTimeout = 1}

    ''' <summary>
    ''' 是否为本次打开 PCL 后第一次加载实例列表。
    ''' 这会清理所有 .pclignore 文件，而非跳过这些对应实例。
    ''' </summary>
    Private IsFirstMcInstanceListLoad As Boolean = True

    ''' <summary>
    ''' 开始加载当前 Minecraft 文件夹的实例列表。
    ''' </summary>
    Private Sub McInstanceListLoad(Loader As LoaderTask(Of String, Integer))
        '开始加载
        Dim Path As String = Loader.Input
        Try
            '初始化
            McInstanceList = New Dictionary(Of McInstanceCardType, List(Of McInstance))

            '检测缓存是否需要更新
            Dim FolderList As New List(Of String)
            If Directory.Exists(Path & "versions") Then '不要使用 CheckPermission，会导致写入时间改变，从而使得文件夹被强制刷新
                Try
                    For Each Folder As DirectoryInfo In New DirectoryInfo(Path & "versions").GetDirectories
                        FolderList.Add(Folder.Name)
                    Next
                Catch ex As Exception
                    Throw New Exception("无法读取实例文件夹，可能是由于没有权限（" & Path & "versions）", ex)
                End Try
            End If
            '不可用
            If Not FolderList.Any() Then
                WriteIni(Path & "PCL.ini", "InstanceCache", "") '清空缓存
                GoTo OnLoaded
            End If
            '有可用实例
            Dim FolderListCheck As Integer = GetHash(McInstanceCacheVersion & "#" & Join(FolderList.ToArray, "#")) Mod (Integer.MaxValue - 1) '根据文件夹名列表生成辨识码
            If Not McInstanceListForceRefresh AndAlso Val(ReadIni(Path & "PCL.ini", "InstanceCache")) = FolderListCheck Then
                '可以使用缓存
                Dim Result = McInstanceListLoadCache(Path)
                If Result Is Nothing Then
                    GoTo Reload
                Else
                    McInstanceList = Result
                End If
            Else
                '文件夹列表不符
Reload:
                McInstanceListForceRefresh = False
                Log("[Minecraft] 文件夹列表变更，重载所有实例")
                WriteIni(Path & "PCL.ini", "InstanceCache", FolderListCheck)
                McInstanceList = McInstanceListLoadNoCache(Path)
            End If
            IsFirstMcInstanceListLoad = False

            '改变当前选择的实例
OnLoaded:
            If Loader.IsAborted Then Return
            If McInstanceList.Any(Function(v) v.Key <> McInstanceCardType.Error) Then
                '尝试读取已储存的选择
                Dim SavedSelection As String = ReadIni(Path & "PCL.ini", "Version")
                If SavedSelection <> "" Then
                    For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList
                        For Each Instance As McInstance In Card.Value
                            If Instance.Name = SavedSelection AndAlso Not Instance.State = McInstanceState.Error Then
                                '使用已储存的选择
                                McInstanceCurrent = Instance
                                Setup.Set("LaunchInstanceSelect", McInstanceCurrent.Name)
                                Log("[Minecraft] 选择该文件夹储存的 Minecraft 实例：" & McInstanceCurrent.Path)
                                Return
                            End If
                        Next
                    Next
                End If
                If Not McInstanceList.First.Value(0).State = McInstanceState.Error Then
                    '自动选择第一项
                    McInstanceCurrent = McInstanceList.First.Value(0)
                    Setup.Set("LaunchInstanceSelect", McInstanceCurrent.Name)
                    Log("[Launch] 自动选择 Minecraft 实例：" & McInstanceCurrent.Path)
                End If
            Else
                McInstanceCurrent = Nothing
                Setup.Set("LaunchInstanceSelect", "")
                Log("[Minecraft] 未找到可用 Minecraft 实例")
            End If
            If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomInteger(200, 3000))
        Catch ex As ThreadInterruptedException
        Catch ex As Exception
            WriteIni(Path & "PCL.ini", "InstanceCache", "") '要求下次重新加载
            Log(ex, "加载 .minecraft 实例列表失败", LogLevel.Feedback)
        End Try
    End Sub

    '获取实例列表
    Private Function McInstanceListLoadCache(Path As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim ResultInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        Try
            Dim CardCount As Integer = ReadIni(Path & "PCL.ini", "CardCount", -1)
            If CardCount = -1 Then Return Nothing
            For i = 0 To CardCount - 1
                Dim CardType As McInstanceCardType = ReadIni(Path & "PCL.ini", "CardKey" & (i + 1), ":")
                Dim InstanceList As New List(Of McInstance)

                '循环读取实例
                For Each Folder As String In ReadIni(Path & "PCL.ini", "CardValue" & (i + 1), ":").Split(":")
                    If Folder = "" Then Continue For
                    Dim VersionFolder As String = $"{Path}versions\{Folder}\"
                    If File.Exists(VersionFolder & ".pclignore") Then
                        If IsFirstMcInstanceListLoad Then
                            Log("[Minecraft] 清理残留的忽略项目：" & VersionFolder) '#2781
                            File.Delete(VersionFolder & ".pclignore")
                        Else
                            Log("[Minecraft] 跳过要求忽略的项目：" & VersionFolder)
                            Continue For
                        End If
                    End If
                    Try

                        '读取单个实例
                        Dim Instance As New McInstance(VersionFolder)
                        InstanceList.Add(Instance)
                        Instance.Info = NEWSetup.Instance.CustomInfo(Instance.Path)

                        If Instance.Info = "" Then Instance.Info = NEWSetup.Instance.Info(Instance.Path)
                        If Not SetupService.IsUnset(SetupEntries.Instance.LogoPath, Instance.Path) Then _
                            Instance.Logo = NEWSetup.Instance.LogoPath(Instance.Path)
                        If Not SetupService.IsUnset(SetupEntries.Instance.ReleaseTime, Instance.Path) Then _
                            Instance.ReleaseTime = NEWSetup.Instance.ReleaseTime(Instance.Path)
                        If Not SetupService.IsUnset(SetupEntries.Instance.State, Instance.Path) Then _
                            Instance.State = NEWSetup.Instance.State(Instance.Path)
                        Instance.IsStar = NEWSetup.Instance.Starred(Instance.Path)
                        Instance.DisplayType = NEWSetup.Instance.DisplayType(Instance.Path)
                        If Instance.State <> McInstanceState.Error AndAlso
                           Not SetupService.IsUnset(SetupEntries.Instance.McVersion, Instance.Path) Then '旧版本可能没有这一项，导致 Instance 不加载（#643）
                            Dim InstanceInfo As New McInstanceInfo With {
                                .FabricVersion = NEWSetup.Instance.FabricVersion(Instance.Path),
                                .LegacyFabricVersion = NEWSetup.Instance.LegacyFabricVersion(Instance.Path),
                                .QuiltVersion = NEWSetup.Instance.QuiltVersion(Instance.Path),
                                .ForgeVersion = NEWSetup.Instance.ForgeVersion(Instance.Path),
                                .LabyModVersion = NEWSetup.Instance.LabyModVersion(Instance.Path),
                                .NeoForgeVersion = NEWSetup.Instance.NeoForgeVersion(Instance.Path),
                                .CleanroomVersion = NEWSetup.Instance.CleanroomVersion(Instance.Path),
                                .OptiFineVersion = NEWSetup.Instance.OptiFineVersion(Instance.Path),
                                .HasLiteLoader = NEWSetup.Instance.HasLiteLoader(Instance.Path),
                                .SortCode = NEWSetup.Instance.SortCode(Instance.Path),
                                .McName = NEWSetup.Instance.McVersion(Instance.Path),
                                .McCodeMain = NEWSetup.Instance.VersionMajor(Instance.Path),
                                .McCodeSub = NEWSetup.Instance.VersionMinor(Instance.Path),
                                .IsApiLoaded = True
                            }
                            InstanceInfo.HasFabric = InstanceInfo.FabricVersion.Any()
                            InstanceInfo.HasLegacyFabric = InstanceInfo.LegacyFabricVersion.Any()
                            InstanceInfo.HasQuilt = InstanceInfo.QuiltVersion.Any()
                            InstanceInfo.HasForge = InstanceInfo.ForgeVersion.Any()
                            InstanceInfo.HasNeoForge = InstanceInfo.NeoForgeVersion.Any()
                            InstanceInfo.HasCleanroom = InstanceInfo.CleanroomVersion.Any()
                            InstanceInfo.HasOptiFine = InstanceInfo.OptiFineVersion.Any()
                            Instance.Version = InstanceInfo
                        End If

                        '重新检查错误实例
                        If Instance.State = McInstanceState.Error Then
                            '重新获取实例错误信息
                            Dim OldDesc As String = Instance.Info
                            Instance.State = McInstanceState.Original
                            Instance.Check()
                            '校验错误原因是否改变
                            Dim CustomInfo As String = NEWSetup.Instance.CustomInfo(Instance.Path)
                            If Instance.State = McInstanceState.Original OrElse (CustomInfo = "" AndAlso Not OldDesc = Instance.Info) Then
                                Log("[Minecraft] 实例 " & Instance.Name & " 的错误状态已变更，新的状态为：" & Instance.Info)
                                Return Nothing
                            End If
                        End If

                        '校验未加载的实例
                        If Instance.Logo = "" Then
                            Log("[Minecraft] 实例 " & Instance.Name & " 未被加载")
                            Return Nothing
                        End If

                    Catch ex As Exception
                        Log(ex, "读取实例加载缓存失败（" & Folder & "）", LogLevel.Debug)
                        Return Nothing
                    End Try
                Next

                If InstanceList.Any Then ResultInstanceList.Add(CardType, InstanceList)
            Next
            Return ResultInstanceList
        Catch ex As Exception
            Log(ex, "读取实例缓存失败")
            Return Nothing
        End Try
    End Function
    Private Function McInstanceListLoadNoCache(Path As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim InstanceList As New List(Of McInstance)

#Region "循环加载每个实例的信息"
        For Each Folder As DirectoryInfo In New DirectoryInfo(Path & "versions").GetDirectories
            If Not Folder.Exists OrElse Not Folder.EnumerateFiles.Any Then
                Log("[Minecraft] 跳过空文件夹：" & Folder.FullName)
                Continue For
            End If
            If (Folder.Name = "cache" OrElse Folder.Name = "BLClient" OrElse Folder.Name = "PCL") AndAlso Not File.Exists(Folder.FullName & "\" & Folder.Name & ".json") Then
                Log("[Minecraft] 跳过可能不是实例文件夹的项目：" & Folder.FullName)
                Continue For
            End If
            Dim InstanceFolder As String = Folder.FullName & "\"
            If File.Exists(InstanceFolder & ".pclignore") Then
                If IsFirstMcInstanceListLoad Then
                    Log("[Minecraft] 清理残留的忽略项目：" & InstanceFolder) '#2781
                    Try
                        File.Delete(InstanceFolder & ".pclignore")
                    Catch ex As Exception
                        Log(ex, "清理残留的忽略项目失败（" & InstanceFolder & "）", LogLevel.Hint)
                    End Try
                Else
                    Log("[Minecraft] 跳过要求忽略的项目：" & InstanceFolder)
                    Continue For
                End If
            End If
            Dim Instance As New McInstance(InstanceFolder)
            InstanceList.Add(Instance)
            Instance.Load()
        Next
#End Region

        Dim ResultInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))

#Region "将实例分类到各个卡片"
        Try

            '未经过自定义的实例列表
            Dim InstanceListOriginal As New Dictionary(Of McInstanceCardType, List(Of McInstance))

            '单独列出收藏的实例
            Dim StaredInstances As New List(Of McInstance)
            For Each Instance As McInstance In InstanceList.ToList
                If Instance.IsStar AndAlso Not Instance.DisplayType = McInstanceCardType.Hidden Then
                    StaredInstances.Add(Instance)
                    InstanceList.Remove(Instance)
                End If
            Next
            If StaredInstances.Any Then InstanceListOriginal.Add(McInstanceCardType.Star, StaredInstances)

            '预先筛选出愚人节和错误的实例
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstanceState.Error}, McInstanceCardType.Error)
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstanceState.Fool}, McInstanceCardType.Fool)

            '筛选 API 实例
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.LiteLoader, McInstanceState.Fabric, McInstanceState.LegacyFabric, McInstanceState.Quilt, McInstanceState.Cleanroom, McInstanceState.LabyMod}, McInstanceCardType.API)

            '将老实例预先分类入不常用，只剩余原版、快照、OptiFine
            Dim InstanceUseful As New List(Of McInstance)
            Dim InstanceRubbish As New List(Of McInstance)
            McInstanceFilter(InstanceList, {McInstanceState.Old}, InstanceRubbish)

            '确认最新实例，若为快照则加入常用列表
            Dim InstanceLatestVersion As McInstance = Nothing '使用最新版本的实例
            For Each Instance As McInstance In InstanceList
                If Instance.State = McInstanceState.Original OrElse Instance.State = McInstanceState.Snapshot Then
                    If InstanceLatestVersion Is Nothing OrElse Instance.ReleaseTime > InstanceLatestVersion.ReleaseTime Then InstanceLatestVersion = Instance
                End If
            Next
            If InstanceLatestVersion IsNot Nothing AndAlso InstanceLatestVersion.State = McInstanceState.Snapshot Then
                InstanceUseful.Add(InstanceLatestVersion)
                InstanceList.Remove(InstanceLatestVersion)
            End If

            '将剩余的快照全部拖进不常用列表
            McInstanceFilter(InstanceList, {McInstanceState.Snapshot}, InstanceRubbish)

            '获取每个大版本下最新的原版与 OptiFine
            Dim NewerInstance As New Dictionary(Of String, McInstance)
            Dim ExistInstance As New List(Of Integer)
            For Each Instance As McInstance In InstanceList
                If Instance.Version.McCodeMain < 2 Then Continue For '未获取成功的实例
                If Not ExistInstance.Contains(Instance.Version.McCodeMain) Then ExistInstance.Add(Instance.Version.McCodeMain)
                If NewerInstance.ContainsKey(Instance.Version.McCodeMain & "-" & Instance.State) Then
                    If Instance.Version.HasOptiFine Then
                        'OptiFine 根据排序识别号判断
                        If Instance.Version.SortCode > NewerInstance(Instance.Version.McCodeMain & "-" & Instance.State).Version.SortCode Then NewerInstance(Instance.Version.McCodeMain & "-" & Instance.State) = Instance
                    Else
                        '原版根据发布时间判断
                        If Instance.ReleaseTime > NewerInstance(Instance.Version.McCodeMain & "-" & Instance.State).ReleaseTime Then NewerInstance(Instance.Version.McCodeMain & "-" & Instance.State) = Instance
                    End If
                Else
                    NewerInstance.Add(Instance.Version.McCodeMain & "-" & Instance.State, Instance)
                End If
            Next

            '将每个大版本下的最常规版本加入
            For Each Code As Integer In ExistInstance
                If NewerInstance.ContainsKey(Code & "-" & McInstanceState.OptiFine) AndAlso NewerInstance.ContainsKey(Code & "-" & McInstanceState.Original) Then
                    '同时存在 OptiFine 与原版
                    Dim OriginalVersion As McInstance = NewerInstance(Code & "-" & McInstanceState.Original)
                    Dim OptiFineVersion As McInstance = NewerInstance(Code & "-" & McInstanceState.OptiFine)
                    If OriginalVersion.Version.McCodeSub > OptiFineVersion.Version.McCodeSub Then
                        '仅在原版比 OptiFine 更新时才加入原版
                        InstanceUseful.Add(OriginalVersion)
                        InstanceList.Remove(OriginalVersion)
                    End If
                    InstanceUseful.Add(OptiFineVersion)
                    InstanceList.Remove(OptiFineVersion)
                ElseIf NewerInstance.ContainsKey(Code & "-" & McInstanceState.OptiFine) Then
                    '没有原版，直接加入 OptiFine
                    InstanceUseful.Add(NewerInstance(Code & "-" & McInstanceState.OptiFine))
                    InstanceList.Remove(NewerInstance(Code & "-" & McInstanceState.OptiFine))
                ElseIf NewerInstance.ContainsKey(Code & "-" & McInstanceState.Original) Then
                    '没有 OptiFine，直接加入原版
                    InstanceUseful.Add(NewerInstance(Code & "-" & McInstanceState.Original))
                    InstanceList.Remove(NewerInstance(Code & "-" & McInstanceState.Original))
                End If
            Next

            '将剩余的东西添加进去
            InstanceRubbish.AddRange(InstanceList)
            If InstanceUseful.Any Then InstanceListOriginal.Add(McInstanceCardType.OriginalLike, InstanceUseful)
            If InstanceRubbish.Any Then InstanceListOriginal.Add(McInstanceCardType.Rubbish, InstanceRubbish)

            '按照自定义实例分类重新添加
            For Each VersionPair In InstanceListOriginal
                For Each Version As McInstance In VersionPair.Value
                    Dim RealType = If(Version.DisplayType = 0 OrElse VersionPair.Key = McInstanceCardType.Star, VersionPair.Key, Version.DisplayType)
                    If Not ResultInstanceList.ContainsKey(RealType) Then ResultInstanceList.Add(RealType, New List(Of McInstance))
                    ResultInstanceList(RealType).Add(Version)
                Next
            Next

        Catch ex As Exception
            ResultInstanceList.Clear()
            Log(ex, "分类实例列表失败", LogLevel.Feedback)
        End Try
#End Region

#Region "对卡片与实例进行排序"

        '对卡片进行整体排序
        Dim SortedInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        For Each SortRule As String In {McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike, McInstanceCardType.Rubbish, McInstanceCardType.Fool, McInstanceCardType.Error, McInstanceCardType.Hidden}
            If ResultInstanceList.ContainsKey(SortRule) Then SortedInstanceList.Add(SortRule, ResultInstanceList(SortRule))
        Next
        ResultInstanceList = SortedInstanceList

        '常规实例：快照放在最上面，此后按版本号从高到低排序
        If ResultInstanceList.ContainsKey(McInstanceCardType.OriginalLike) Then
            Dim OldList As List(Of McInstance) = ResultInstanceList(McInstanceCardType.OriginalLike)
            '提取快照
            Dim Snapshot As McInstance = Nothing
            For Each Instance As McInstance In OldList
                If Instance.State = McInstanceState.Snapshot Then
                    Snapshot = Instance
                    Exit For
                End If
            Next
            If Not IsNothing(Snapshot) Then OldList.Remove(Snapshot)
            '按版本号排序
            Dim NewList As List(Of McInstance) = OldList.OrderByDescending(Function(v) v.Version.McCodeMain).ToList
            '回设
            If Not IsNothing(Snapshot) Then NewList.Insert(0, Snapshot)
            ResultInstanceList(McInstanceCardType.OriginalLike) = NewList
        End If

        '不常用实例：按发布时间新旧排序，如果不可用则按名称排序
        If ResultInstanceList.ContainsKey(McInstanceCardType.Rubbish) Then
            ResultInstanceList(McInstanceCardType.Rubbish).Sort(
            Function(Left As McInstance, Right As McInstance)
                Dim LeftYear As Integer = Left.ReleaseTime.Year '+ If(Left.State = McInstanceState.Original OrElse Left.Version.HasOptiFine, 100, 0)
                Dim RightYear As Integer = Right.ReleaseTime.Year '+ If(Right.State = McInstanceState.Original OrElse Left.Version.HasOptiFine, 100, 0)
                If LeftYear > 2000 AndAlso RightYear > 2000 Then
                    If LeftYear <> RightYear Then
                        Return LeftYear > RightYear
                    Else
                        Return Left.ReleaseTime > Right.ReleaseTime
                    End If
                ElseIf LeftYear > 2000 AndAlso RightYear < 2000 Then
                    Return True
                ElseIf LeftYear < 2000 AndAlso RightYear > 2000 Then
                    Return False
                Else
                    Return Left.Name > Right.Name
                End If
            End Function)
        End If

        'API 实例：优先按版本排序，此后【先放 Fabric / Quilt / Legacy Fabric，再放 Neo/Forge（按版本号从高到低排序），然后放 Cleanroom / LabyMod，最后放 LiteLoader（按名称排序）】
        If ResultInstanceList.ContainsKey(McInstanceCardType.API) Then
            ResultInstanceList(McInstanceCardType.API).Sort(
            Function(Left As McInstance, Right As McInstance)
                Dim Basic = VersionSortInteger(Left.Version.McName, Right.Version.McName)
                If Basic <> 0 Then
                    Return Basic > 0
                Else
                    If Left.Version.HasFabric Xor Right.Version.HasFabric Then
                        Return Left.Version.HasFabric
                    ElseIf Left.Version.HasQuilt Xor Right.Version.HasQuilt Then
                        Return Left.Version.HasQuilt
                    ElseIf Left.Version.HasLegacyFabric Xor Right.Version.HasLegacyFabric Then
                        Return Left.Version.HasQuilt
                    ElseIf Left.Version.HasNeoForge Xor Right.Version.HasNeoForge Then
                        Return Left.Version.HasNeoForge
                    ElseIf Left.Version.HasForge Xor Right.Version.HasForge Then
                        Return Left.Version.HasForge
                    ElseIf Left.Version.HasCleanroom Xor Right.Version.HasCleanroom Then
                        Return Left.Version.HasCleanroom
                    ElseIf Left.Version.HasLabyMod Xor Right.Version.HasLabyMod Then
                        Return Left.Version.HasLabyMod
                    ElseIf Not Left.Version.SortCode <> Right.Version.SortCode Then
                        Return Left.Version.SortCode > Right.Version.SortCode
                    Else
                        Return Left.Name > Right.Name
                    End If
                End If
            End Function)
        End If

#End Region

#Region "保存卡片缓存"
        WriteIni(Path & "PCL.ini", "CardCount", ResultInstanceList.Count)
        For i = 0 To ResultInstanceList.Count - 1
            WriteIni(Path & "PCL.ini", "CardKey" & (i + 1), ResultInstanceList.Keys(i))
            Dim Value As String = ""
            For Each Instance As McInstance In ResultInstanceList.Values(i)
                Value += Instance.Name & ":"
            Next
            WriteIni(Path & "PCL.ini", "CardValue" & (i + 1), Value)
        Next
#End Region

        Return ResultInstanceList
    End Function
    ''' <summary>
    ''' 筛选特定种类的实例，并直接添加为卡片。
    ''' </summary>
    ''' <param name="InstanceList">用于筛选的列表。</param>
    ''' <param name="Formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    ''' <param name="CardType">卡片的名称。</param>
    Private Sub McInstanceFilter(ByRef InstanceList As List(Of McInstance), ByRef Target As Dictionary(Of McInstanceCardType, List(Of McInstance)), Formula As McInstanceState(), CardType As McInstanceCardType)
        Dim KeepList = InstanceList.Where(Function(v) Formula.Contains(v.State)).ToList
        '加入实例列表，并从剩余中删除
        If KeepList.Any Then
            Target.Add(CardType, KeepList)
            For Each Instance As McInstance In KeepList
                InstanceList.Remove(Instance)
            Next
        End If
    End Sub
    ''' <summary>
    ''' 筛选特定种类的实例，并增加入一个已有列表中。
    ''' </summary>
    ''' <param name="InstanceList">用于筛选的列表。</param>
    ''' <param name="Formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    ''' <param name="KeepList">传入需要增加入的列表。</param>
    Private Sub McInstanceFilter(ByRef InstanceList As List(Of McInstance), Formula As McInstanceState(), ByRef KeepList As List(Of McInstance))
        KeepList.AddRange(InstanceList.Where(Function(v) Formula.Contains(v.State)))
        '加入实例列表，并从剩余中删除
        If KeepList.Any Then
            For Each Instance As McInstance In KeepList
                InstanceList.Remove(Instance)
            Next
        End If
    End Sub
    Public Enum McInstanceCardType
        Star = -1
        Auto = 0 '仅用于强制实例分类的自动
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
        Dim FileName As String = SelectFile("皮肤文件(*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp", "选择皮肤文件")

        '验证有效性
        If FileName = "" Then Return New McSkinInfo With {.IsVaild = False}
        Try
            Dim Image As New MyBitmap(FileName)
            If Image.Pic.Width <> 64 OrElse Not (Image.Pic.Height = 32 OrElse Image.Pic.Height = 64) Then
                Hint("皮肤图片大小应为 64x32 像素或 64x64 像素！", HintType.Critical)
                Return New McSkinInfo With {.IsVaild = False}
            End If
            Dim FileInfo As New FileInfo(FileName)
            If FileInfo.Length > 24 * 1024 Then
                Hint("皮肤文件大小需小于 24 KB，而所选文件大小为 " & Math.Round(FileInfo.Length / 1024, 2) & " KB", HintType.Critical)
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
    Public Function McSkinGetAddress(Uuid As String, Type As String) As String
        If Uuid = "" Then Throw New Exception("Uuid 为空。")
        If Uuid.StartsWithF("00000") Then Throw New Exception("离线 Uuid 无正版皮肤文件。")
        '尝试读取缓存
        Dim CacheSkinAddress As String = ReadIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", Uuid)
        If Not CacheSkinAddress = "" Then Return CacheSkinAddress
        '获取皮肤地址
        Dim Url As String
        Select Case Type
            Case "Mojang", "Ms"
                Url = "https://sessionserver.mojang.com/session/minecraft/profile/"
            Case "Auth"
                Dim AuthUrl = SelectedProfile.Server
                Url = AuthUrl.Replace("/authserver", "") & "/sessionserver/session/minecraft/profile/"
            Case Else
                Throw New ArgumentException("皮肤地址种类无效：" & If(Type, "null"))
        End Select
        Dim SkinString = NetGetCodeByRequestRetry(Url & Uuid)
        If SkinString = "" Then Throw New Exception("皮肤返回值为空，可能是未设置自定义皮肤的用户")
        '处理皮肤地址
        Dim SkinValue As String
        Try
            For Each SkinProperty In GetJson(SkinString)("properties")
                If SkinProperty("name") = "textures" Then
                    SkinValue = SkinProperty("value").ToString()
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
        WriteIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", Uuid, SkinValue)
        Log("[Skin] UUID " & Uuid & " 对应的皮肤文件为 " & SkinValue)
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
                NetDownloadByClient(Address, FileAddress & NetDownloadEnd).GetAwaiter().GetResult()
                File.Delete(FileAddress)
                FileSystem.Rename(FileAddress & NetDownloadEnd, FileAddress)
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

#Region "支持库文件（Libraries）"

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
        ''' 是否为纯本地文件，若是则不尝试联网下载。
        ''' </summary>
        Public IsLocal As Boolean = False
        ''' <summary>
        ''' 由 JSON 提供的 URL，若没有则为 Nothing。
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
        ''' 原 JSON 中 Name 项除去版本号部分的较前部分。可能为 Nothing。
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
        ''' 原 JSON 中的 Name 项。
        ''' </summary>
        Public OriginalName As String

        Public Overrides Function ToString() As String
            Return If(IsNatives, "[Native] ", "") & GetString(Size) & " | " & LocalPath
        End Function
    End Class

    ''' <summary>
    ''' 检查是否符合 JSON 中的 Rules。
    ''' </summary>
    ''' <param name="RuleToken">JSON 中的 "rules" 项目。</param>
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
    Private OSVersion As String = Environment.OSVersion.Version.ToString()

    ''' <summary>
    ''' 递归获取 Minecraft 某一实例的完整支持库列表。
    ''' </summary>
    Public Function McLibListGet(Instance As McInstance, IncludeInstanceJar As Boolean) As List(Of McLibToken)

        '获取当前支持库列表
        Log("[Minecraft] 获取支持库列表：" & Instance.Name)
        Dim result = McLibListGetWithJson(Instance.JsonObject)

        '需要添加原版 Jar
        If IncludeInstanceJar Then
            Dim RealInstance As McInstance
            Dim RequiredJar As String = Instance.JsonObject("jar")?.ToString
            If Instance.IsHmclFormatJson OrElse RequiredJar Is Nothing Then
                'HMCL 项直接使用自身的 Jar
                '根据 Inherit 获取最深层实例
                Dim OriginalInstance As McInstance = Instance
                '1.17+ 的 Forge 不寻找 Inherit
                If Not ((Instance.Version.HasForge OrElse Instance.Version.HasNeoForge) AndAlso Instance.Version.McCodeMain >= 17) Then
                    Do Until OriginalInstance.InheritInstance = ""
                        If OriginalInstance.InheritInstance = OriginalInstance.Name Then Exit Do
                        OriginalInstance = New McInstance(PathMcFolder & "versions\" & OriginalInstance.InheritInstance & "\")
                    Loop
                End If
                '需要新建对象，否则后面的 Check 会导致 McInstanceCurrent 的 State 变回 Original
                '复现：启动一个 Snapshot 实例
                RealInstance = New McInstance(OriginalInstance.Path)
            Else
                'Json 已提供 Jar 字段，使用该字段的信息
                RealInstance = New McInstance(RequiredJar)
            End If
            Dim ClientUrl As String, ClientSHA1 As String
            '判断需求的实例是否存在
            '不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
            If Not File.Exists(RealInstance.Path & RealInstance.Name & ".json") Then
                RealInstance = Instance
                Log("[Minecraft] 可能缺少前置实例 " & RealInstance.Name & "，找不到对应的 JSON 文件", LogLevel.Debug)
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
            result.Add(New McLibToken With {.LocalPath = RealInstance.Path & RealInstance.Name & ".jar", .Size = 0, .IsNatives = False, .Url = ClientUrl, .SHA1 = ClientSHA1})
        End If
        Return result
    End Function
    ''' <summary>
    ''' 获取 Minecraft 某一实例忽视继承的支持库列表，即结果中没有继承项。
    ''' </summary>
    Public Function McLibListGetWithJson(JsonObject As JObject, Optional KeepSameNameDifferentVersionResult As Boolean = False, Optional CustomMcFolder As String = Nothing, Optional TargetInstance As McInstance = Nothing) As List(Of McLibToken)
        CustomMcFolder = If(CustomMcFolder, PathMcFolder)
        Dim BasicArray As New List(Of McLibToken)

        '添加基础 Json 项
        Dim AllLibs As JArray = JsonObject("libraries")

        '转换为 LibToken
        For Each Library As JObject In AllLibs.Children

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

            '是否为纯本地项
            Dim Hint As String = Library("hint")
            Dim IsLocal As Boolean = If(Hint IsNot Nothing, Hint = "local", False)

            '根据是否本地化处理（Natives）
            If Library("natives") Is Nothing Then '没有 Natives
                Dim LocalPath As String
                If IsLocal AndAlso TargetInstance IsNot Nothing Then '纯本地项
                    LocalPath = TargetInstance.Path & "libraries\" & Library("name").ToString.AfterFirst(":").Replace(":", "-") & ".jar"
                Else
                    LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder)
                End If
                Try
                    If Library("downloads") IsNot Nothing AndAlso Library("downloads")("artifact") IsNot Nothing Then
                        BasicArray.Add(New McLibToken With {
                            .OriginalName = Library("name"),
                            .Url = If(RootUrl, Library("downloads")("artifact")("url")),
                            .LocalPath = If(Library("downloads")("artifact")("path") Is Nothing, McLibGet(Library("name"),
                                CustomMcFolder:=CustomMcFolder), CustomMcFolder & "libraries\" & Library("downloads")("artifact")("path").ToString.Replace("/", "\")),
                            .Size = Val(Library("downloads")("artifact")("size").ToString),
                            .IsNatives = False,
                            .SHA1 = Library("downloads")("artifact")("sha1")?.ToString,
                            .IsLocal = IsLocal})
                    Else
                        BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = LocalPath, .Size = 0, .IsNatives = False, .SHA1 = Nothing, .IsLocal = IsLocal})
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
                                 McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).Replace(".jar", "-" & Library("natives")("windows").ToString & ".jar").Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")),
                                 CustomMcFolder & "libraries\" & Library("downloads")("classifiers")("natives-windows")("path").ToString.Replace("/", "\")),
                             .Size = Val(Library("downloads")("classifiers")("natives-windows")("size").ToString),
                             .IsNatives = True,
                             .SHA1 = Library("downloads")("classifiers")("natives-windows")("sha1").ToString,
                             .IsLocal = IsLocal})
                    Else
                        BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).Replace(".jar", "-" & Library("natives")("windows").ToString & ".jar").Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")), .Size = 0, .IsNatives = True, .SHA1 = Nothing, .IsLocal = IsLocal})
                    End If
                Catch ex As Exception
                    Log(ex, "处理实际支持库列表失败（有 Natives，" & If(Library("name"), "Nothing").ToString & "）")
                    BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).Replace(".jar", "-" & Library("natives")("windows").ToString & ".jar").Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")), .Size = 0, .IsNatives = True, .SHA1 = Nothing, .IsLocal = False})
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
                    If VersionSortBoolean(BasicArrayVersion, ResultArrayVersion) Then
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
    ''' 获取实例缺失的支持库文件所对应的 NetTaskFile。
    ''' </summary>
    Public Function McLibFix(Instance As McInstance) As List(Of NetFile)
        If Not Instance.IsLoaded Then Instance.Load()
        Dim Result As New List(Of NetFile)

        '更新此方法时需要同步更新 Forge 新版自动安装方法！

        '主 Jar 文件
        Try
            Dim MainJar As NetFile = DlClientJarGet(Instance, True)
            If MainJar IsNot Nothing Then Result.Add(MainJar)
        Catch ex As Exception
            Log(ex, "实例缺失主 Jar 文件所必须的信息", LogLevel.Developer)
        End Try

        'Library 文件
        Result.AddRange(McLibFixFromLibToken(McLibListGet(Instance, False)))

        'Authlib-Injector 文件
        Dim AuthlibTargetFile = PathPure & "\authlib-injector.jar"
        Dim AuthlibDownloadInfo As JObject = Nothing
        '获取下载信息
        Try
            Log("[Minecraft] 开始获取 Authlib-Injector 下载信息")
            AuthlibDownloadInfo = GetJson(NetGetCodeByLoader({
                        "https://authlib-injector.yushi.moe/artifact/latest.json",
                        "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                    }, IsJson:=True))
        Catch ex As Exception
            Log(ex, "获取 Authlib-Injector 下载信息失败")
        End Try
        '校验文件
        If AuthlibDownloadInfo IsNot Nothing Then
            Dim Checker As New FileChecker(Hash:=AuthlibDownloadInfo("checksums")("sha256").ToString)
            If Checker.Check(AuthlibTargetFile) IsNot Nothing Then
                '开始下载
                Dim DownloadAddress As String = AuthlibDownloadInfo("download_url").ToString.
                            Replace("bmclapi2.bangbang93.com/mirrors/authlib-injector", "authlib-injector.yushi.moe")
                Log("[Minecraft] Authlib-Injector 需要更新：" & DownloadAddress, LogLevel.Developer)
                Result.Add(New NetFile({
                        DownloadAddress,
                        DownloadAddress.Replace("authlib-injector.yushi.moe", "bmclapi2.bangbang93.com/mirrors/authlib-injector")
                    }, AuthlibTargetFile, New FileChecker(Hash:=AuthlibDownloadInfo("checksums")("sha256").ToString)))
            End If
        End If

        '修改渲染器
        Dim MesaLoaderWindowsVersion = "25.1.7"
        Dim MesaLoaderWindowsTargetFile = PathPure & "\mesa-loader-windows\" & MesaLoaderWindowsVersion & "\Loader.jar"
        If Setup.Get("VersionAdvanceRenderer", Instance) <> 0 AndAlso Not File.Exists(MesaLoaderWindowsTargetFile) Then
            Dim DownloadAddress As String = "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/org/glavo/mesa-loader-windows/" & MesaLoaderWindowsVersion & "/mesa-loader-windows-" & MesaLoaderWindowsVersion & "-" & If(ModBase.Is32BitSystem, "x86", If(ModBase.IsArm64System, "arm64", "x64")) & ".jar"
            Result.Add(New NetFile({DownloadAddress}, MesaLoaderWindowsTargetFile))
        End If

        'LabyMod Assets 文件
        If Instance.Version.HasLabyMod Then
            If Instance.PathIndie = Instance.Path Then
                If Directory.Exists(Instance.Path & "labymod-neo") Then Directory.Delete(Instance.Path & "labymod-neo", True)
                CreateSymbolicLink(Instance.Path & "labymod-neo", PathMcFolder & "labymod-neo", &H2)
            End If
            Try
                Dim ChannelType = Instance.JsonObject("labymod_data")("channelType").ToString()
                Directory.CreateDirectory($"{PathMcFolder}labymod-neo\libraries")
                Log("[Minecraft] 开始获取 LabyMod 信息")
                Dim labyManifest As JObject = NetGetCodeByRequestRetry($"https://releases.r2.labymod.net/api/v1/manifest/{ChannelType}/latest.json", IsJson:=True)
                Dim LabyAssets As JObject = labyManifest("assets")
                Dim LabyModCommitRef As String = labyManifest("commitReference").ToString()
                For Each Asset In LabyAssets
                    Dim AssetName As String = Asset.Key
                    Dim AssetSHA1 As String = Asset.Value.ToString()
                    Dim AssetPath As String = $"{PathMcFolder}labymod-neo\assets\{AssetName}.jar"
                    Dim AssetUrl As String = $"https://releases.r2.labymod.net/api/v1/download/assets/labymod4/{ChannelType}/{LabyModCommitRef}/{AssetName}/{AssetSHA1}.jar"
                    Dim Checker = New FileChecker(Hash:=AssetSHA1)
                    If Checker.Check(AssetPath) Is Nothing Then Continue For
                    Result.Add(New NetFile(
                           {AssetUrl},
                           AssetPath,
                           Checker))
                Next
            Catch ex As Exception
                Log(ex, "获取 LabyMod 信息失败，跳过检查")
            End Try
        End If

        '跳过校验
        If ShouldIgnoreFileCheck(Instance) Then
            Log("[Minecraft] 用户要求尽量忽略文件检查，这可能会保留有误的文件")
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
    ''' 将 McLibToken 列表转换为 NetFile。无需下载的文件会被自动过滤。
    ''' </summary>
    Public Function McLibFixFromLibToken(Libs As List(Of McLibToken), Optional CustomMcFolder As String = Nothing) As List(Of NetFile)
        CustomMcFolder = If(CustomMcFolder, PathMcFolder)
        Dim Result As New List(Of NetFile)
        '获取
        For Each Token As McLibToken In Libs
            '检查文件
            Dim Checker As New FileChecker(ActualSize:=If(Token.Size = 0, -1, Token.Size), Hash:=Token.SHA1)
            If Checker.Check(Token.LocalPath) Is Nothing Then Continue For
            If Token.IsLocal Then
                Log("[Download] 已跳过被标记为本地文件的支持库: " & Token.OriginalName)
                Continue For
            End If
            '文件不符合，添加下载
            Dim Urls As New List(Of String)
            If Token.Url Is Nothing AndAlso Token.Name = "net.minecraftforge:forge:universal" Then
                '特判修复 Forge 部分 universal 文件缺失 URL（#5455）
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
                If Not File.Exists(Token.LocalPath) Then WriteFile(Token.LocalPath, GetResourceStream("Resources/transformer.jar"))
                Log("[Download] 已自动释放 Transformer Discovery Service", LogLevel.Developer)
                Continue For
            ElseIf Token.LocalPath.Contains("optifine\OptiFine") Then
                'OptiFine 主 Jar
                Dim OptiFineBase As String = Token.LocalPath.Replace(CustomMcFolder & "libraries\optifine\OptiFine\", "").Split("_")(0) & "/" & GetFileNameFromPath(Token.LocalPath).Replace("-", "_")
                OptiFineBase = "/maven/com/optifine/" & OptiFineBase
                If OptiFineBase.Contains("_pre") Then OptiFineBase = OptiFineBase.Replace("com/optifine/", "com/optifine/preview_")
                Urls.Add("https://bmclapi2.bangbang93.com" & OptiFineBase)
            ElseIf Token.Name.Contains("LabyMod") Then
                'LabyMod 只有一个下载源
                Urls.Add(Token.Url)
                Log($"[Download] 获取到 LabyMod 主要库文件的 Size = {Token.Size},SHA1 = {Token.SHA1}，由于 LabyMod 乱写 Size，已忽略 Size")
                Checker = New FileChecker(Hash:=Token.SHA1) '只校验 SHA1
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
        CustomMcFolder = If(CustomMcFolder, PathMcFolder)
        Dim Splited = Original.Split(":")
        McLibGet = If(WithHead, CustomMcFolder & "libraries\", "") &
                   Splited(0).Replace(".", "\") & "\" & Splited(1) & "\" & Splited(2) & "\" & Splited(1) & "-" & Splited(2) & ".jar"
        '判断 OptiFine 是否应该使用 installer
        If McLibGet.Contains("optifine\OptiFine\1.") AndAlso Splited(2).Split(".").Count > 1 Then
            Dim MajorVersion As Integer = Val(Splited(2).Split(".")(1).BeforeFirst("_"))
            Dim MinorVersion As Integer = If(Splited(2).Split(".").Count > 2, Val(Splited(2).Split(".")(2).BeforeFirst("_")), 0)
            If (MajorVersion = 12 OrElse (MajorVersion = 20 AndAlso MinorVersion >= 4) OrElse MajorVersion >= 21) AndAlso '仅在 1.12 (无法追溯) 和 1.20.4+ (#5376) 遇到此问题
                File.Exists($"{CustomMcFolder}libraries\{Splited(0).Replace(".", "\")}\{Splited(1)}\{Splited(2)}\{Splited(1)}-{Splited(2)}-installer.jar") Then
                McLaunchLog("已将 " & Original & " 替换为对应的 Installer 文件")
                McLibGet = McLibGet.Replace(".jar", "-installer.jar")
            End If
        End If
    End Function

    ''' <summary>
    ''' 检查设置，是否应当忽略文件检查？
    ''' </summary>
    Public Function ShouldIgnoreFileCheck(Version As McInstance)
        Return Setup.Get("VersionAdvanceAssetsV2", instance:=Version) OrElse (Setup.Get("VersionAdvanceAssets", instance:=Version) = 2)
    End Function

#End Region

#Region "资源文件（Assets）"

    '获取索引
    ''' <summary>
    ''' 获取某实例资源文件索引的对应 Json 项，详见实例 Json 中的 assetIndex 项。失败会抛出异常。
    ''' </summary>
    Public Function McAssetsGetIndex(Instance As McInstance, Optional ReturnLegacyOnError As Boolean = False, Optional CheckURLEmpty As Boolean = False) As JToken
        Dim AssetsName As String
        Try
            Do While True
                Dim Index As JToken = Instance.JsonObject("assetIndex")
                If Index IsNot Nothing AndAlso Index("id") IsNot Nothing Then Return Index
                If Instance.JsonObject("assets") IsNot Nothing Then AssetsName = Instance.JsonObject("assets").ToString
                If CheckURLEmpty AndAlso Index("url") IsNot Nothing Then Return Index
                '下一个实例
                If Instance.InheritInstance = "" Then Exit Do
                Instance = New McInstance(PathMcFolder & "versions\" & Instance.InheritInstance)
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
            Throw New Exception("该实例不存在资源文件索引信息")
        End If
    End Function
    ''' <summary>
    ''' 获取某实例资源文件索引名，优先使用 assetIndex，其次使用 assets。失败会返回 legacy。
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
                If Instance.InheritInstance = "" Then Exit Do
                Instance = New McInstance(PathMcFolder & "versions\" & Instance.InheritInstance)
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
            If Not File.Exists($"{PathMcFolder}assets\indexes\{IndexName}.json") Then Throw New FileNotFoundException("未找到 Asset Index", PathMcFolder & "assets\indexes\" & IndexName & ".json")
            Dim Result As New List(Of McAssetsToken)
            Dim Json As JsonObject = JsonObject.Parse(ReadFile($"{PathMcFolder}assets\indexes\{IndexName}.json"))

            '读取列表
            For Each File As KeyValuePair(Of String,JsonNode) In Json("objects").AsObject()
                Dim LocalPath As String
                If Json("map_to_resources") IsNot Nothing AndAlso Json("map_to_resources").GetValue(Of Boolean) Then
                    'Remap
                    LocalPath = Instance.PathIndie & "resources\" & File.Key.Replace("/", "\")
                ElseIf Json("virtual") IsNot Nothing AndAlso Json("virtual").GetValue(Of Boolean) Then
                    'Virtual
                    LocalPath = PathMcFolder & "assets\virtual\legacy\" & File.Key.Replace("/", "\")
                Else
                    '正常
                    LocalPath = PathMcFolder & "assets\objects\" & Left(File.Value("hash").ToString, 2) & "\" & File.Value("hash").ToString
                End If
                Result.Add(New McAssetsToken With {
                    .LocalPath = LocalPath,
                    .SourcePath = File.Key,
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

    '获取缺失列表
    ''' <summary>
    ''' 获取实例缺失的资源文件所对应的 NetTaskFile。
    ''' </summary>
    Public Function McAssetsFixList(Instance As McInstance, CheckHash As Boolean, Optional ByRef ProgressFeed As LoaderBase = Nothing) As List(Of NetFile)
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
                If File.Exists AndAlso (Token.Size = 0 OrElse Token.Size = File.Length) AndAlso
                    (Not CheckHash OrElse Token.Hash Is Nothing OrElse Token.Hash = GetFileSHA1(Token.LocalPath)) Then Continue For
                '文件不存在，添加下载
                Result.Add(New NetFile(DlSourceAssetsGet($"https://resources.download.minecraft.net/{Left(Token.Hash, 2)}/{Token.Hash}"), Token.LocalPath, New FileChecker(ActualSize:=If(Token.Size = 0, -1, Token.Size), Hash:=Token.Hash)))
            Next
        Catch ex As Exception
            Log(ex, "获取实例缺失的资源文件下载列表失败")
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
                If((Date.Now - Time).TotalDays > 1, "更新时间：" & Time.ToString, "更新于：" & TimeUtils.GetTimeSpanString(Time - Date.Now, False))
            Dim MsgResult = MyMsgBox(MsgBoxText, "Minecraft 更新提示", "确定", "下载", If((Date.Now - Time).TotalHours > 3, "更新日志", ""),
                Button3Action:=Sub() McUpdateLogShow(Version))
            '弹窗结果
            If MsgResult = 2 Then
                '下载
                RunInUi(
                Sub()
                    PageDownloadInstall.McVersionWaitingForSelect = VersionName
                    FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
                End Sub)
            End If

        Catch ex As Exception
            Log(ex, "Minecraft 更新提示发送失败（" & If(VersionName, "Nothing") & "）", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 比较两个版本名的排序，若 Left 较新或相同则返回 True（Left >= Right）。无法比较两个 Pre 的大小。
    ''' 支持的格式范例：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    ''' </summary>
    Public Function VersionSortBoolean(Left As String, Right As String) As Boolean
        Return VersionSortInteger(Left, Right) >= 0
    End Function
    ''' <summary>
    ''' 比较两个版本名的排序，若 Left 较新则返回 1，相同则返回 0，Right 较新则返回 -1。
    ''' 支持的格式范例：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    ''' </summary>
    Public Function VersionSortInteger(Left As String, Right As String) As Integer
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
                If Left > Right Then
                    Return 1
                ElseIf Left < Right Then
                    Return -1
                Else
                    Return 0
                End If
            End If
            '确定两边的数值
            Dim LeftValue As String = If(Lefts.Count - 1 < i, "-1", Lefts(i))
            Dim RightValue As String = If(Rights.Count - 1 < i, "-1", Rights(i))
            If LeftValue = RightValue Then GoTo NextEntry
            If LeftValue = "pre" OrElse LeftValue = "snapshot" Then LeftValue = "-3"
            If LeftValue = "rc" Then LeftValue = "-2"
            If LeftValue = "experimental" Then LeftValue = "-4"
            Dim LeftValValue = Val(LeftValue)
            If RightValue = "pre" OrElse RightValue = "snapshot" Then RightValue = "-3"
            If RightValue = "rc" Then RightValue = "-2"
            If RightValue = "experimental" Then RightValue = "-4"
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
            Return VersionSortInteger(x, y)
        End Function
    End Class

    ''' <summary>
    ''' 判断版本名是否类似正式版。
    ''' </summary>
    Public Function IsVersionNameLikeRelease(VerName As String) As Boolean
        Return VerName.StartsWithF("1.") AndAlso Not (VerName.Contains("w") OrElse VerName.Contains("pre") OrElse VerName.Contains("rc") OrElse VerName.Contains("-"))
    End Function

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
        Dim UserProfile As String = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        Dim UserName As String = UserProfile.Split("\").Last
        Dim MaskedProfile = UserProfile.Replace(UserName, New String(FilterChar, UserName.Length))
        Return Raw.Replace(UserProfile, MaskedProfile)
    End Function

End Module
