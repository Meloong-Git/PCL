Public Module ModComp

    Public Enum CompType
        ''' <summary>
        ''' 允许任意种类，或种类未知。
        ''' </summary>
        Any = -1
        ''' <summary> 
        ''' Mod。
        ''' </summary>
        [Mod] = 0
        ''' <summary>
        ''' 整合包。
        ''' </summary>
        ModPack = 1
        ''' <summary>
        ''' 资源包。
        ''' </summary>
        ResourcePack = 2
        ''' <summary>
        ''' 光影包。
        ''' </summary>
        Shader = 3
        ''' <summary>
        ''' CurseForge：数据包。
        ''' Modrinth：数据包，或数据包与 Mod 的混合。
        ''' </summary>
        DataPack = 4
        ''' <summary>
        ''' 服务端插件。
        ''' </summary>
        Plugin = 5
    End Enum
    Public Enum CompModLoaderType
        'https://docs.curseforge.com/?http#tocS_ModLoaderType
        Any = 0
        Forge = 1
        LiteLoader = 3
        Fabric = 4
        Quilt = 5
        NeoForge = 6
    End Enum
    <Flags> Public Enum CompSourceType
        CurseForge = 1
        Modrinth = 2
        Any = CurseForge Or Modrinth
    End Enum

#Region "CompDatabase | Mod 数据库"

    Private _CompDatabase As List(Of CompDatabaseEntry) = Nothing
    Private ReadOnly Property CompDatabase As List(Of CompDatabaseEntry)
        Get
            If _CompDatabase IsNot Nothing Then Return _CompDatabase
            '初始化数据库
            _CompDatabase = New List(Of CompDatabaseEntry)
            Dim i As Integer = 0
            For Each Line In DecodeBytes(GetResources("ModData")).Replace(vbCrLf, vbLf).Replace(vbCr, "").Split(vbLf)
                i += 1
                If Line = "" Then Continue For
                For Each EntryData As String In Line.Split("¨")
                    Dim Entry = New CompDatabaseEntry
                    Dim SplitedLine = EntryData.Split("|")
                    If SplitedLine(0).StartsWithF("@") Then
                        Entry.CurseForgeSlug = Nothing
                        Entry.ModrinthSlug = SplitedLine(0).Replace("@", "")
                    ElseIf SplitedLine(0).EndsWithF("@") Then
                        Entry.CurseForgeSlug = SplitedLine(0).TrimEnd("@")
                        Entry.ModrinthSlug = Entry.CurseForgeSlug
                    ElseIf SplitedLine(0).Contains("@") Then
                        Entry.CurseForgeSlug = SplitedLine(0).Split("@")(0)
                        Entry.ModrinthSlug = SplitedLine(0).Split("@")(1)
                    Else
                        Entry.CurseForgeSlug = SplitedLine(0)
                        Entry.ModrinthSlug = Nothing
                    End If
                    Entry.WikiId = i
                    If SplitedLine.Count >= 2 Then
                        Entry.ChineseName = SplitedLine(1)
                        If Entry.ChineseName.Contains("*") Then '处理 *
                            Entry.ChineseName = Entry.ChineseName.Replace("*",
                                $" ({If(Entry.CurseForgeSlug, Entry.ModrinthSlug).Replace("-", " ").Capitalize})")
                        End If
                    End If
                    _CompDatabase.Add(Entry)
                Next
            Next
            Return _CompDatabase
        End Get
    End Property

    Private Class CompDatabaseEntry
        ''' <summary>
        ''' McMod 的对应 ID。
        ''' </summary>
        Public WikiId As Integer
        ''' <summary>
        ''' 中文译名。空字符串代表没有翻译。
        ''' </summary>
        Public ChineseName As String = ""
        ''' <summary>
        ''' CurseForge Slug（例如 advanced-solar-panels）。
        ''' </summary>
        Public CurseForgeSlug As String = Nothing
        ''' <summary>
        ''' Modrinth Slug（例如 advanced-solar-panels）。
        ''' </summary>
        Public ModrinthSlug As String = Nothing

        Public Overrides Function ToString() As String
            Return If(CurseForgeSlug, "") & "&" & If(ModrinthSlug, "") & "|" & WikiId & "|" & ChineseName
        End Function
    End Class

#End Region

#Region "CompProject | 工程信息"

    '类定义

    Public Class CompProject

        '源信息

        ''' <summary>
        ''' 该工程信息来自 CurseForge 还是 Modrinth。
        ''' </summary>
        Public ReadOnly FromCurseForge As Boolean
        ''' <summary>
        ''' 工程的种类。
        ''' 由于 Modrinth 混合使用 Mod 和数据包，结果不一定准确。
        ''' </summary>
        Public ReadOnly Type As CompType
        ''' <summary>
        ''' 工程的短名。例如 technical-enchant。
        ''' </summary>
        Public ReadOnly Slug As String
        ''' <summary>
        ''' CurseForge 工程的数字 ID。Modrinth 工程的乱码 ID。
        ''' </summary>
        Public ReadOnly Id As String
        ''' <summary>
        ''' CurseForge 文件列表的数字 ID。Modrinth 工程的此项无效。
        ''' </summary>
        Public ReadOnly CurseForgeFileIds As List(Of Integer)

        '描述性信息

        ''' <summary>
        ''' 原始的英文名称。
        ''' </summary>
        Public ReadOnly RawName As String
        ''' <summary>
        ''' 英文描述。
        ''' </summary>
        Public ReadOnly Description As String
        ''' <summary>
        ''' 来源网站的工程页面网址。确保格式一定标准。
        ''' CurseForge：https://www.curseforge.com/minecraft/mc-mods/jei
        ''' Modrinth：https://modrinth.com/mod/technical-enchant
        ''' </summary>
        Public ReadOnly Website As String
        ''' <summary>
        ''' 最后一次更新的时间。可能为 Nothing。
        ''' </summary>
        Public ReadOnly LastUpdate As Date? = Nothing
        ''' <summary>
        ''' 下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量！
        ''' </summary>
        Public ReadOnly DownloadCount As Integer
        ''' <summary>
        ''' 支持的 Mod 加载器列表。可能为空。
        ''' </summary>
        Public ReadOnly ModLoaders As List(Of CompModLoaderType)
        ''' <summary>
        ''' 描述性标签的内容。已转换为中文。
        ''' </summary>
        Public ReadOnly Tags As List(Of String)
        ''' <summary>
        ''' Logo 图片的下载地址。若为 Nothing 则没有。
        ''' </summary>
        Public LogoUrl As String = Nothing
        ''' <summary>
        ''' 游戏大版本列表。例如：18, 16, 15……
        ''' </summary>
        Public ReadOnly GameVersions As List(Of Integer)

        '数据库信息

        Private LoadedDatabase As Boolean = False
        Private _DatabaseEntry As CompDatabaseEntry = Nothing
        ''' <summary>
        ''' 关联的数据库条目。若为 Nothing 则没有。
        ''' </summary>
        Private Property DatabaseEntry As CompDatabaseEntry
            Get
                If Not LoadedDatabase Then
                    LoadedDatabase = True
                    If Type = CompType.Mod OrElse Type = CompType.DataPack Then
                        _DatabaseEntry = CompDatabase.FirstOrDefault(Function(c) If(FromCurseForge, c.CurseForgeSlug, c.ModrinthSlug) = Slug)
                    End If
                End If
                Return _DatabaseEntry
            End Get
            Set(value As CompDatabaseEntry)
                LoadedDatabase = True
                _DatabaseEntry = value
            End Set
        End Property
        ''' <summary>
        ''' MC 百科的页面 ID。若为 0 则没有。
        ''' </summary>
        Public ReadOnly Property WikiId As Integer
            Get
                Return If(DatabaseEntry Is Nothing, 0, DatabaseEntry.WikiId)
            End Get
        End Property
        ''' <summary>
        ''' 翻译后的中文名。若数据库没有则等同于 RawName。
        ''' </summary>
        Public ReadOnly Property TranslatedName As String
            Get
                Return If(DatabaseEntry Is Nothing OrElse DatabaseEntry.ChineseName = "", RawName, DatabaseEntry.ChineseName)
            End Get
        End Property

        '实例化

        ''' <summary>
        ''' 从工程 Json 中初始化实例。若出错会抛出异常。
        ''' </summary>
        Public Sub New(Data As JObject)
            If Data.ContainsKey("Tags") Then
#Region "CompJson"
                FromCurseForge = Data("DataSource") = "CurseForge"
                Type = Data("Type").ToObject(Of Integer)
                Slug = Data("Slug")
                Id = Data("Id")
                If Data.ContainsKey("CurseForgeFileIds") Then CurseForgeFileIds = CType(Data("CurseForgeFileIds"), JArray).Select(Function(t) t.ToObject(Of Integer)).ToList
                RawName = Data("RawName")
                Description = Data("Description")
                Website = Data("Website")
                If Data.ContainsKey("LastUpdate") Then LastUpdate = Data("LastUpdate")
                DownloadCount = Data("DownloadCount")
                If Data.ContainsKey("ModLoaders") Then
                    ModLoaders = CType(Data("ModLoaders"), JArray).Select(Function(t) CType(t.ToObject(Of Integer), CompModLoaderType)).ToList
                Else
                    ModLoaders = New List(Of CompModLoaderType)
                End If
                Tags = CType(Data("Tags"), JArray).Select(Function(t) t.ToString).ToList
                If Data.ContainsKey("LogoUrl") Then LogoUrl = Data("LogoUrl")
                If Data.ContainsKey("GameVersions") Then
                    GameVersions = CType(Data("GameVersions"), JArray).Select(Function(t) t.ToObject(Of Integer)).ToList
                Else
                    GameVersions = New List(Of Integer)
                End If
#End Region
            Else
                FromCurseForge = Data.ContainsKey("summary")
                If FromCurseForge Then
#Region "CurseForge"
                    '简单信息
                    Id = Data("id")
                    Slug = Data("slug")
                    RawName = Data("name")
                    Description = Data("summary")
                    Website = Data("links")("websiteUrl").ToString.TrimEnd("/")
                    LastUpdate = Data("dateReleased") '#1194
                    DownloadCount = Data("downloadCount")
                    If Data("logo").Count > 0 Then
                        If Data("logo")("thumbnailUrl") Is Nothing OrElse Data("logo")("thumbnailUrl") = "" Then
                            LogoUrl = Data("logo")("url")
                        Else
                            LogoUrl = Data("logo")("thumbnailUrl")
                        End If
                    End If
                    'Type
                    If Website.Contains("/mc-mods/") OrElse Website.Contains("/mod/") Then
                        Type = CompType.Mod
                    ElseIf Website.Contains("/modpacks/") Then
                        Type = CompType.ModPack
                    ElseIf Website.Contains("/resourcepacks/") Then
                        Type = CompType.ResourcePack
                    ElseIf Website.Contains("/texture-packs/") Then
                        Type = CompType.ResourcePack
                    ElseIf Website.Contains("/shaders/") Then
                        Type = CompType.Shader
                    Else
                        Type = CompType.DataPack
                    End If
                    'FileIndexes / GameVersions / ModLoaders
                    ModLoaders = New List(Of CompModLoaderType)
                    Dim Files As New List(Of KeyValuePair(Of Integer, List(Of String))) 'FileId, GameVersions
                    For Each File In If(Data("latestFiles"), New JArray)
                        Dim NewFile As New CompFile(File, Type)
                        If Not NewFile.Available Then Continue For
                        ModLoaders.AddRange(NewFile.ModLoaders)
                        Dim GameVersions = File("gameVersions").ToObject(Of List(Of String))
                        If Not GameVersions.Any(Function(v) v.StartsWithF("1.")) Then Continue For
                        Files.Add(New KeyValuePair(Of Integer, List(Of String))(File("id"), GameVersions))
                    Next
                    For Each File In If(Data("latestFilesIndexes"), New JArray) '这俩玩意儿包含的文件不一样，见 #3599
                        If Not File("gameVersion").ToString.StartsWithF("1.") Then Continue For
                        Files.Add(New KeyValuePair(Of Integer, List(Of String))(File("fileId"), {File("gameVersion").ToString}.ToList))
                    Next
                    CurseForgeFileIds = Files.Select(Function(f) f.Key).Distinct.ToList
                    GameVersions = Files.SelectMany(Function(f) f.Value).Where(Function(v) v.StartsWithF("1.")).
                        Select(Function(v) CInt(Val(v.Split(".")(1).BeforeFirst("-")))).Where(Function(v) v > 0).
                        Distinct.OrderByDescending(Function(v) v).ToList
                    ModLoaders = ModLoaders.Distinct.OrderBy(Of Integer)(Function(t) t).ToList
                    'Tags
                    Tags = New List(Of String)
                    For Each Category In If(Data("categories"), New JArray). '镜像源 API 可能丢失此字段 (4267#issuecomment-2254590831)
                        Select(Of Integer)(Function(t) t("id")).Distinct.OrderByDescending(Function(c) c)
                        Select Case Category
                            'Mod
                            Case 406 : Tags.Add("世界元素")
                            Case 407 : Tags.Add("生物群系")
                            Case 410 : Tags.Add("维度")
                            Case 408 : Tags.Add("矿物/资源")
                            Case 409 : Tags.Add("天然结构")
                            Case 412 : Tags.Add("科技")
                            Case 415 : Tags.Add("管道/物流")
                            Case 4843 : Tags.Add("自动化")
                            Case 417 : Tags.Add("能源")
                            Case 4558 : Tags.Add("红石")
                            Case 436 : Tags.Add("食物/烹饪")
                            Case 416 : Tags.Add("农业")
                            Case 414 : Tags.Add("运输")
                            Case 420 : Tags.Add("仓储")
                            Case 419 : Tags.Add("魔法")
                            Case 422 : Tags.Add("冒险")
                            Case 424 : Tags.Add("装饰")
                            Case 411 : Tags.Add("生物")
                            Case 434 : Tags.Add("装备")
                            Case 423 : Tags.Add("信息显示")
                            Case 435 : Tags.Add("服务器")
                            Case 5191 : Tags.Add("改良")
                            Case 421 : Tags.Add("支持库")
                            '整合包
                            Case 4484 : Tags.Add("多人")
                            Case 4479 : Tags.Add("硬核")
                            Case 4483 : Tags.Add("战斗")
                            Case 4478 : Tags.Add("任务")
                            Case 4472 : Tags.Add("科技")
                            Case 4473 : Tags.Add("魔法")
                            Case 4475 : Tags.Add("冒险")
                            Case 4476 : Tags.Add("探索")
                            Case 4477 : Tags.Add("小游戏")
                            Case 4471 : Tags.Add("科幻")
                            Case 4736 : Tags.Add("空岛")
                            Case 5128 : Tags.Add("原版改良")
                            Case 4487 : Tags.Add("FTB")
                            Case 4480 : Tags.Add("基于地图")
                            Case 4481 : Tags.Add("轻量")
                            Case 4482 : Tags.Add("大型")
                            '资源包
                            Case 403 : Tags.Add("原版风")
                            Case 400 : Tags.Add("写实风")
                            Case 401 : Tags.Add("现代风")
                            Case 402 : Tags.Add("中世纪")
                            Case 399 : Tags.Add("蒸汽朋克")
                            Case 5244 : Tags.Add("含字体")
                            Case 404 : Tags.Add("动态效果")
                            Case 4465 : Tags.Add("兼容 Mod")
                            Case 393 : Tags.Add("16x")
                            Case 394 : Tags.Add("32x")
                            Case 395 : Tags.Add("64x")
                            Case 396 : Tags.Add("128x")
                            Case 397 : Tags.Add("256x")
                            Case 398 : Tags.Add("超高清")
                            Case 5193 : Tags.Add("数据包") '有这个 Tag 的项会从资源包请求中被移除
                            '光影包
                            Case 6553 : Tags.Add("写实风")
                            Case 6554 : Tags.Add("幻想风")
                            Case 6555 : Tags.Add("原版风")
                            '数据包
                            Case 6948 : Tags.Add("冒险")
                            Case 6949 : Tags.Add("幻想")
                            Case 6950 : Tags.Add("支持库")
                            Case 6952 : Tags.Add("魔法")
                            Case 6946 : Tags.Add("Mod 相关")
                            Case 6951 : Tags.Add("科技")
                            Case 6953 : Tags.Add("实用")
                        End Select
                    Next
#End Region
                Else
#Region "Modrinth"
                    '简单信息
                    Id = If(Data("project_id"), Data("id")) '两个 API 会返回的 key 不一样
                    Slug = Data("slug")
                    RawName = Data("title")
                    Description = Data("description")
                    LastUpdate = Data("date_modified")
                    DownloadCount = Data("downloads")
                    LogoUrl = Data("icon_url")
                    If LogoUrl = "" Then LogoUrl = Nothing
                    Website = $"https://modrinth.com/{Data("project_type")}/{Slug}"
                    'GameVersions
                    '搜索结果的键为 versions，获取特定工程的键为 game_versions
                    GameVersions = If(CType(If(Data("game_versions"), Data("versions")), JArray), New JArray).
                                       Select(Function(v) v.ToString).Where(Function(v) v.StartsWithF("1.")).
                                       Select(Of Integer)(Function(v) Val(v.Split(".")(1).BeforeFirst("-"))).Where(Function(v) v > 0).
                                       Distinct.OrderByDescending(Function(v) v).ToList
                    'Type
                    Select Case Data("project_type").ToString
                        Case "modpack" : Type = CompType.ModPack
                        Case "resourcepack" : Type = CompType.ResourcePack
                        Case "shader" : Type = CompType.Shader
                        Case Else : Type = CompType.Mod 'Modrinth 将数据包标为 Mod
                    End Select
                    'Tags & ModLoaders
                    Tags = New List(Of String)
                    ModLoaders = New List(Of CompModLoaderType)
                    If Data.ContainsKey("loaders") Then
                        For Each Category In Data("loaders").Select(Function(t) t.ToString)
                            Select Case Category
                                Case "forge" : ModLoaders.Add(CompModLoaderType.Forge)
                                Case "fabric" : ModLoaders.Add(CompModLoaderType.Fabric)
                                Case "quilt" : ModLoaders.Add(CompModLoaderType.Quilt)
                                Case "neoforge" : ModLoaders.Add(CompModLoaderType.NeoForge)
                            End Select
                        Next
                    End If
                    For Each Category In Data("categories").Select(Function(t) t.ToString)
                        Select Case Category
                            '加载器
                            Case "forge" : ModLoaders.Add(CompModLoaderType.Forge)
                            Case "fabric" : ModLoaders.Add(CompModLoaderType.Fabric)
                            Case "quilt" : ModLoaders.Add(CompModLoaderType.Quilt)
                            Case "neoforge" : ModLoaders.Add(CompModLoaderType.NeoForge)
                            Case "datapack" : Type = CompType.DataPack '若包含数据包版本，则优先标为 DataPack
                            '共用
                            Case "technology" : Tags.Add("科技")
                            Case "magic" : Tags.Add("魔法")
                            Case "adventure" : Tags.Add("冒险")
                            Case "utility" : Tags.Add("实用")
                            Case "optimization" : Tags.Add("性能优化")
                            Case "vanilla-like" : Tags.Add("原版风")
                            Case "realistic" : Tags.Add("写实风")
                            'Mod/数据包
                            Case "worldgen" : Tags.Add("世界元素")
                            Case "food" : Tags.Add("食物/烹饪")
                            Case "game-mechanics" : Tags.Add("游戏机制")
                            Case "transportation" : Tags.Add("运输")
                            Case "storage" : Tags.Add("仓储")
                            Case "decoration" : If Type <> CompType.ResourcePack Then Tags.Add("装饰")
                            Case "mobs" : If Type <> CompType.ResourcePack Then Tags.Add("生物")
                            Case "equipment" : If Type <> CompType.ResourcePack Then Tags.Add("装备")
                            Case "social" : Tags.Add("服务器")
                            Case "library" : Tags.Add("支持库")
                            '整合包
                            Case "multiplayer" : Tags.Add("多人")
                            Case "challenging" : Tags.Add("硬核")
                            Case "combat" : Tags.Add("战斗")
                            Case "quests" : Tags.Add("任务")
                            Case "kitchen-sink" : Tags.Add("水槽包")
                            Case "lightweight" : Tags.Add("轻量")
                            '资源包
                            Case "simplistic" : Tags.Add("简洁")
                            Case "combat" : Tags.Add("战斗")
                            Case "tweaks" : Tags.Add("改良")

                            Case "8x-" : Tags.Add("极简")
                            Case "16x" : Tags.Add("16x")
                            Case "32x" : Tags.Add("32x")
                            Case "48x" : Tags.Add("48x")
                            Case "64x" : Tags.Add("64x")
                            Case "128x" : Tags.Add("128x")
                            Case "256x" : Tags.Add("256x")
                            Case "512x+" : Tags.Add("超高清")

                            Case "audio" : Tags.Add("含声音")
                            Case "fonts" : Tags.Add("含字体")
                            Case "models" : Tags.Add("含模型")
                            Case "gui" : Tags.Add("含 UI")
                            Case "locale" : Tags.Add("含语言")
                            Case "core-shaders" : Tags.Add("核心着色器")
                            Case "modded" : Tags.Add("兼容 Mod")
                            '光影包
                            Case "fantasy" : Tags.Add("幻想风")
                            Case "semi-realistic" : Tags.Add("半写实风")
                            Case "cartoon" : Tags.Add("卡通风")
                            '暂时不添加性能负荷 Tag
                            'Case "potato" : Tags.Add("极低")
                            'Case "low" : Tags.Add("低")
                            'Case "medium" : Tags.Add("中")
                            'Case "high" : Tags.Add("高")
                            Case "colored-lighting" : Tags.Add("彩色光照")
                            Case "path-tracing" : Tags.Add("路径追踪")
                            Case "pbr" : Tags.Add("PBR")
                            Case "reflections" : Tags.Add("反射")

                            Case "iris" : Tags.Add("Iris")
                            Case "optifine" : Tags.Add("OptiFine")
                            Case "vanilla" : Tags.Add("原版可用")
                        End Select
                    Next
#End Region
                End If
                If Not Tags.Any() Then Tags.Add("其他")
                Tags.Sort()
                ModLoaders.Sort()
            End If
            '保存缓存
            CompProjectCache(Id) = Me
        End Sub
        ''' <summary>
        ''' 将当前实例转为可用于保存缓存的 Json。
        ''' </summary>
        Public Function ToJson() As JObject
            Dim Json As New JObject
            Json("DataSource") = If(FromCurseForge, "CurseForge", "Modrinth")
            Json("Type") = CInt(Type)
            Json("Slug") = Slug
            Json("Id") = Id
            If CurseForgeFileIds IsNot Nothing Then Json("CurseForgeFileIds") = New JArray(CurseForgeFileIds)
            Json("RawName") = RawName
            Json("Description") = Description
            Json("Website") = Website
            If LastUpdate IsNot Nothing Then Json("LastUpdate") = LastUpdate
            Json("DownloadCount") = DownloadCount
            If ModLoaders IsNot Nothing AndAlso ModLoaders.Any Then Json("ModLoaders") = New JArray(ModLoaders.Select(Function(m) CInt(m)))
            Json("Tags") = New JArray(Tags)
            If Not String.IsNullOrEmpty(LogoUrl) Then Json("LogoUrl") = LogoUrl
            If GameVersions.Any Then Json("GameVersions") = New JArray(GameVersions)
            Json("CacheTime") = Date.Now '用于检查缓存时间
            Return Json
        End Function
        ''' <summary>
        ''' 将当前工程信息实例化为控件。
        ''' </summary>
        Public Function ToCompItem(ShowMcVersionDesc As Boolean, ShowLoaderDesc As Boolean) As MyCompItem
            '获取版本描述
            Dim GameVersionDescription As String
            If GameVersions Is Nothing OrElse Not GameVersions.Any() Then
                GameVersionDescription = "仅快照版本" '#5412
            Else
                Dim SpaVersions As New List(Of String)
                Dim IsOld As Boolean = False
                For i = 0 To GameVersions.Count - 1 '版本号一定为降序
                    '获取当前连续的版本号段
                    Dim StartVersion As Integer = GameVersions(i), EndVersion As Integer = GameVersions(i)
                    If StartVersion < 10 Then '如果支持新版本，则不显示 1.9-
                        If SpaVersions.Any() AndAlso Not IsOld Then
                            Exit For
                        Else
                            IsOld = True
                        End If
                    End If
                    For ii = i + 1 To GameVersions.Count - 1
                        If GameVersions(ii) <> EndVersion - 1 Then Exit For
                        EndVersion = GameVersions(ii)
                        i = ii
                    Next
                    '将版本号段转为描述文本
                    If StartVersion = EndVersion Then
                        SpaVersions.Add("1." & StartVersion)
                    ElseIf McVersionHighest > -1 AndAlso StartVersion >= McVersionHighest Then
                        If EndVersion < 10 Then
                            SpaVersions.Clear()
                            SpaVersions.Add("全版本")
                            Exit For
                        Else
                            SpaVersions.Add("1." & EndVersion & "+")
                        End If
                    ElseIf EndVersion < 10 Then
                        SpaVersions.Add("1." & StartVersion & "-")
                        Exit For
                    ElseIf StartVersion - EndVersion = 1 Then
                        SpaVersions.Add("1." & StartVersion & ", 1." & EndVersion)
                    Else
                        SpaVersions.Add("1." & StartVersion & "~1." & EndVersion)
                    End If
                Next
                GameVersionDescription = SpaVersions.Join(", ")
            End If
            '获取 Mod 加载器描述
            Dim ModLoaderDescriptionFull As String, ModLoaderDescriptionPart As String
            Dim ModLoadersForDesc As New List(Of CompModLoaderType)(ModLoaders)
            If Setup.Get("ToolDownloadIgnoreQuilt") Then ModLoadersForDesc.Remove(CompModLoaderType.Quilt)
            Select Case ModLoadersForDesc.Count
                Case 0
                    If ModLoaders.Count = 1 Then
                        ModLoaderDescriptionFull = "仅 " & ModLoaders.Single.ToString
                        ModLoaderDescriptionPart = ModLoaders.Single.ToString
                    Else
                        ModLoaderDescriptionFull = "未知"
                        ModLoaderDescriptionPart = ""
                    End If
                Case 1
                    ModLoaderDescriptionFull = "仅 " & ModLoadersForDesc.Single.ToString
                    ModLoaderDescriptionPart = ModLoadersForDesc.Single.ToString
                Case Else
                    Dim MaxVersion As Integer = If(GameVersions.Any, GameVersions.Max, 99)
                    If ModLoaders.Contains(CompModLoaderType.Forge) AndAlso
                       (MaxVersion < 14 OrElse ModLoaders.Contains(CompModLoaderType.Fabric)) AndAlso
                       (MaxVersion < 20 OrElse ModLoaders.Contains(CompModLoaderType.NeoForge)) AndAlso
                       (MaxVersion < 14 OrElse ModLoaders.Contains(CompModLoaderType.Quilt) OrElse Setup.Get("ToolDownloadIgnoreQuilt")) Then
                        ModLoaderDescriptionFull = "任意"
                        ModLoaderDescriptionPart = ""
                    Else
                        ModLoaderDescriptionFull = ModLoadersForDesc.Join(" / ")
                        ModLoaderDescriptionPart = ModLoadersForDesc.Join(" / ")
                    End If
            End Select
            '实例化 UI
            Dim NewItem As New MyCompItem With {.Tag = Me, .Logo = GetControlLogo()}
            Dim Title = GetControlTitle(True)
            NewItem.Title = Title.Key
            If Title.Value = "" Then
                CType(NewItem.LabTitleRaw.Parent, StackPanel).Children.Remove(NewItem.LabTitleRaw)
            Else
                NewItem.SubTitle = Title.Value
            End If
            NewItem.Tags = Tags
            NewItem.Description = Description.Replace(vbCr, "").Replace(vbLf, "")
            '下边栏
            If Not ShowMcVersionDesc AndAlso Not ShowLoaderDesc Then
                '全部隐藏
                CType(NewItem.PathVersion.Parent, Grid).Children.Remove(NewItem.PathVersion)
                CType(NewItem.LabVersion.Parent, Grid).Children.Remove(NewItem.LabVersion)
                NewItem.ColumnVersion1.Width = New GridLength(0)
                NewItem.ColumnVersion2.MaxWidth = 0
                NewItem.ColumnVersion3.Width = New GridLength(0)
            ElseIf ShowMcVersionDesc AndAlso ShowLoaderDesc Then
                '全部显示
                NewItem.LabVersion.Text = If(ModLoaderDescriptionPart = "", "", ModLoaderDescriptionPart & " ") & GameVersionDescription
            ElseIf ShowMcVersionDesc Then
                '仅显示版本
                NewItem.LabVersion.Text = GameVersionDescription
            Else
                '仅显示 Mod 加载器
                NewItem.LabVersion.Text = ModLoaderDescriptionFull
            End If
            NewItem.LabSource.Text = If(FromCurseForge, "CurseForge", "Modrinth")
            If LastUpdate IsNot Nothing Then
                NewItem.LabTime.Text = GetTimeSpanString(LastUpdate - Date.Now, True)
            Else
                NewItem.LabTime.Visibility = Visibility.Collapsed
                NewItem.ColumnTime1.Width = New GridLength(0)
                NewItem.ColumnTime2.Width = New GridLength(0)
                NewItem.ColumnTime3.Width = New GridLength(0)
            End If
            NewItem.LabDownload.Text =
                If(DownloadCount > 100000000, Math.Round(DownloadCount / 100000000, 2) & " 亿",
                    If(DownloadCount > 100000, Math.Floor(DownloadCount / 10000) & " 万", DownloadCount))
            Return NewItem
        End Function
        Public Function GetControlLogo() As String
            If String.IsNullOrEmpty(LogoUrl) Then
                Return PathImage & "Icons/NoIcon.png"
            Else
                Return LogoUrl
            End If
        End Function
        Public Function GetControlTitle(HasModLoaderDescription As Boolean) As KeyValuePair(Of String, String)
            '检查下列代码时可以参考 #1567 的测试例
            Dim Title As String = RawName
            Dim SubtitleList As List(Of String)
            If TranslatedName = RawName Then
                '没有中文翻译
                '将所有名称分段
                Dim NameLists = TranslatedName.Split({" | ", " - ", "(", ")", "[", "]", "{", "}"}, StringSplitOptions.RemoveEmptyEntries).
                    Select(Function(s) s.Trim(" /\".ToCharArray)).Where(Function(w) Not String.IsNullOrEmpty(w)).ToList
                If NameLists.Count = 1 Then GoTo NoSubtitle
                '查找其中的缩写、Forge/Fabric 等版本标记
                SubtitleList = New List(Of String)
                Dim NormalNameList = New List(Of String)
                For Each Name In NameLists
                    Dim LowerName As String = Name.ToLower
                    If Name.ToUpper = Name AndAlso Name <> "FPS" AndAlso Name <> "HUD" Then
                        '缩写
                        SubtitleList.Add(Name)
                    ElseIf (LowerName.Contains("forge") OrElse LowerName.Contains("fabric") OrElse LowerName.Contains("quilt")) AndAlso
                        Not RegexCheck(LowerName.Replace("forge", "").Replace("fabric", "").Replace("quilt", ""), "[a-z]+") Then '去掉关键词后没有其他字母
                        'Forge/Fabric 等版本标记
                        SubtitleList.Add(Name)
                    Else
                        '其他部分
                        NormalNameList.Add(Name)
                    End If
                Next
                '根据分类后的结果处理
                If Not NormalNameList.Any() OrElse Not SubtitleList.Any() Then GoTo NoSubtitle
                '同时包含 NormalName 和 Subtitle
                Title = NormalNameList.Join(" - ")
            Else
                '有中文翻译
                '尝试将文本分为三段：Title (EnglishName) - Suffix
                '检查时注意 Carpet：它没有中文译名，但有 Suffix
                Title = TranslatedName.BeforeFirst(" (").BeforeFirst(" - ")
                Dim Suffix As String = ""
                If TranslatedName.AfterLast(")").Contains(" - ") Then Suffix = TranslatedName.AfterLast(")").AfterLast(" - ")
                Dim EnglishName As String = TranslatedName
                If Suffix <> "" Then EnglishName = EnglishName.Replace(" - " & Suffix, "")
                EnglishName = EnglishName.Replace(Title, "").Trim("("c, ")"c, " "c)
                '中段的额外信息截取
                SubtitleList = EnglishName.Split({" | ", " - ", "(", ")", "[", "]", "{", "}"}, StringSplitOptions.RemoveEmptyEntries).
                        Select(Function(s) s.Trim(" /".ToCharArray)).Where(Function(w) Not String.IsNullOrEmpty(w)).ToList
                If SubtitleList.Count > 1 AndAlso
                   Not SubtitleList.Any(Function(s) s.ToLower.Contains("forge") OrElse s.ToLower.Contains("fabric") OrElse s.ToLower.Contains("quilt")) AndAlso '不是标注 XX 版
                   Not (SubtitleList.Count = 2 AndAlso SubtitleList.Last.ToUpper = SubtitleList.Last) Then '不是缩写
                    SubtitleList = New List(Of String) From {EnglishName} '使用原名
                End If
                '添加后缀
                If Suffix <> "" Then SubtitleList.Add(Suffix)
            End If
            SubtitleList = SubtitleList.Distinct.ToList()
            '设置标题与描述
            Dim Subtitle As String = ""
            If SubtitleList.Any Then
                For Each Ex In SubtitleList
                    Dim IsModLoaderDescription As Boolean =
                        Ex.ToLower.Contains("forge") OrElse Ex.ToLower.Contains("fabric") OrElse Ex.ToLower.Contains("quilt")
                    '是否显示 ModLoader 信息
                    If Not HasModLoaderDescription AndAlso IsModLoaderDescription Then Continue For
                    '去除 “Forge/Fabric” 这一无意义提示
                    If Ex.Length < 16 AndAlso Ex.ToLower.Contains("fabric") AndAlso Ex.ToLower.Contains("forge") Then Continue For
                    '将 “Forge” 等提示改为 “Forge 版”
                    If IsModLoaderDescription AndAlso Not Ex.Contains("版") AndAlso
                        Ex.ToLower.Replace("forge", "").Replace("fabric", "").Replace("quilt", "").Length <= 3 Then
                        Ex = Ex.Replace("Edition", "").Replace("edition", "").Trim.Capitalize & " 版"
                    End If
                    '将 “forge” 等词语的首字母大写
                    Ex = Ex.Replace("forge", "Forge").Replace("neo", "Neo").Replace("fabric", "Fabric").Replace("quilt", "Quilt")
                    Subtitle &= "  |  " & Ex.Trim
                Next
            Else
NoSubtitle:
                Subtitle = ""
            End If
            Return New KeyValuePair(Of String, String)(Title, Subtitle)
        End Function

        '辅助函数

        ''' <summary>
        ''' 检查是否与某个 Project 是相同的工程，只是在不同的网站。
        ''' </summary>
        Public Function IsLike(Project As CompProject) As Boolean
            If Id = Project.Id Then Return True '相同实例
            '提取字符串中的字母和数字
            Dim GetRaw =
            Function(Data As String) As String
                Dim Result As New StringBuilder()
                For Each r As Char In Data.Where(Function(c) Char.IsLetterOrDigit(c))
                    Result.Append(r)
                Next
                Return Result.ToString.ToLower
            End Function
            '来自不同的网站
            If FromCurseForge = Project.FromCurseForge Then Return False
            'Mod 加载器一致
            If ModLoaders.Count <> Project.ModLoaders.Count OrElse ModLoaders.Except(Project.ModLoaders).Any() Then Return False
            '若不为光影，则要求 MC 版本一致
            If Type <> CompType.Shader AndAlso (GameVersions.Count <> Project.GameVersions.Count OrElse GameVersions.Except(Project.GameVersions).Any()) Then Return False
            '最近更新时间差距在一周以内
            If LastUpdate IsNot Nothing AndAlso Project.LastUpdate IsNot Nothing AndAlso
               Math.Abs((LastUpdate - Project.LastUpdate).Value.TotalDays) > 7 Then Return False
            'MCMOD 翻译名 / 原名 / 描述文本 / Slug 的英文部分相同
            If TranslatedName = Project.TranslatedName OrElse
               RawName = Project.RawName OrElse Description = Project.Description OrElse
               GetRaw(Slug) = GetRaw(Project.Slug) Then
                Log($"[Comp] 将 {RawName} ({Slug}) 与 {Project.RawName} ({Project.Slug}) 认定为相似工程")
                '如果只有一个有 DatabaseEntry，设置给另外一个
                If DatabaseEntry Is Nothing AndAlso Project.DatabaseEntry IsNot Nothing Then DatabaseEntry = Project.DatabaseEntry
                If DatabaseEntry IsNot Nothing AndAlso Project.DatabaseEntry Is Nothing Then Project.DatabaseEntry = DatabaseEntry
                Return True
            End If
            Return False
        End Function

        Public Overrides Function ToString() As String
            Return $"{Id} ({Slug}): {RawName}"
        End Function
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim project = TryCast(obj, CompProject)
            Return project IsNot Nothing AndAlso Id = project.Id
        End Function
        Public Shared Operator =(left As CompProject, right As CompProject) As Boolean
            Return EqualityComparer(Of CompProject).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As CompProject, right As CompProject) As Boolean
            Return Not left = right
        End Operator

    End Class

    '输入与输出

    Public Class CompProjectRequest

        '结果要求

        ''' <summary>
        ''' 加载后应输出到的结果存储器。
        ''' </summary>
        Public Storage As CompProjectStorage
        ''' <summary>
        ''' 应当尽量达成的结果数量。
        ''' </summary>
        Public TargetResultCount As Integer
        ''' <summary>
        ''' 根据加载位置记录，是否还可以继续获取内容。
        ''' </summary>
        Public ReadOnly Property CanContinue As Boolean
            Get
                If Tag.NotSupportCf OrElse Not Source.HasFlag(CompSourceType.CurseForge) Then Storage.CurseForgeTotal = 0
                If Tag.NotSupportMr OrElse Not Source.HasFlag(CompSourceType.Modrinth) Then Storage.ModrinthTotal = 0
                If Storage.CurseForgeTotal = -1 OrElse Storage.ModrinthTotal = -1 Then Return True
                Return Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal
            End Get
        End Property

        '输入内容

        ''' <summary>
        ''' 筛选资源种类。
        ''' </summary>
        Public Type As CompType
        ''' <summary>
        ''' 筛选资源标签。
        ''' </summary>
        Public Tag As PageComp.FilterTag
        ''' <summary>
        ''' 筛选 Mod 加载器类别。
        ''' </summary>
        Public ModLoader As CompModLoaderType = CompModLoaderType.Any
        ''' <summary>
        ''' 筛选 MC 版本。
        ''' </summary>
        Public GameVersion As String = Nothing
        ''' <summary>
        ''' 搜索的文本内容。
        ''' </summary>
        Public SearchText As String = Nothing
        ''' <summary>
        ''' 允许的来源。
        ''' </summary>
        Public Source As CompSourceType = CompSourceType.Any
        ''' <summary>
        ''' 构造函数。
        ''' </summary>
        Public Sub New(Type As CompType, Storage As CompProjectStorage, TargetResultCount As Integer)
            Me.Type = Type
            Me.Storage = Storage
            Me.TargetResultCount = TargetResultCount
        End Sub

        '构造请求

        ''' <summary>
        ''' 获取对应的 CurseForge API 请求链接。若返回 Nothing 则为不进行 CurseForge 请求。
        ''' </summary>
        Public Function GetCurseForgeAddress() As String
            If Not Source.HasFlag(CompSourceType.CurseForge) Then Return Nothing
            If Tag.NotSupportCf Then Storage.CurseForgeTotal = 0
            If Storage.CurseForgeTotal > -1 AndAlso Storage.CurseForgeTotal <= Storage.CurseForgeOffset Then Return Nothing
            '应用筛选参数
            Dim Address As String = $"https://api.curseforge.com/v1/mods/search?gameId=432&sortField=2&sortOrder=desc&pageSize={CompPageSize}"
            Select Case Type
                Case CompType.Mod
                    Address += "&classId=6"
                Case CompType.ModPack
                    Address += "&classId=4471"
                Case CompType.DataPack
                    Address += "&classId=6945"
                Case CompType.Shader
                    Address += "&classId=6552"
                Case CompType.ResourcePack
                    Address += "&classId=12"
            End Select
            Address += "&categoryId=" & If(Not Tag.HasFilter, "0", Tag.CfValue)
            If ModLoader <> CompModLoaderType.Any Then Address += "&modLoaderType=" & CType(ModLoader, Integer)
            If Not String.IsNullOrEmpty(GameVersion) Then Address += "&gameVersion=" & GameVersion
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&searchFilter=" & Net.WebUtility.UrlEncode(SearchText)
            If Storage.CurseForgeOffset > 0 Then Address += "&index=" & Storage.CurseForgeOffset
            Return Address
        End Function
        ''' <summary>
        ''' 获取对应的 Modrinth API 请求链接。若返回 Nothing 则为不进行 Modrinth 请求。
        ''' </summary>
        Public Function GetModrinthAddress() As String
            If Not Source.HasFlag(CompSourceType.Modrinth) Then Return Nothing
            If Tag.NotSupportMr Then Storage.ModrinthTotal = 0
            If Storage.ModrinthTotal > -1 AndAlso Storage.ModrinthTotal <= Storage.ModrinthOffset Then Return Nothing
            '应用筛选参数
            Dim Address As String = $"https://api.modrinth.com/v2/search?limit={CompPageSize}&index=relevance"
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&query=" & Net.WebUtility.UrlEncode(SearchText)
            If Storage.ModrinthOffset > 0 Then Address += "&offset=" & Storage.ModrinthOffset
            'facets=[["categories:'game-mechanics'"],["categories:'forge'"],["versions:1.19.3"],["project_type:mod"]]
            Dim Facets As New List(Of String)
            Facets.Add($"[""project_type:{GetStringFromEnum(Type).ToLower}""]")
            If Tag.HasFilter Then Facets.Add($"[""categories:'{Tag.MrValue}'""]")
            If ModLoader <> CompModLoaderType.Any Then Facets.Add($"[""categories:'{GetStringFromEnum(ModLoader).ToLower}'""]")
            If Not String.IsNullOrEmpty(GameVersion) Then Facets.Add($"[""versions:'{GameVersion}'""]")
            Address += "&facets=[" & String.Join(",", Facets) & "]"
            Return Address
        End Function

        '相同判断
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim request = TryCast(obj, CompProjectRequest)
            Return request IsNot Nothing AndAlso
                Type = request.Type AndAlso TargetResultCount = request.TargetResultCount AndAlso
                Tag.Equals(request.Tag) AndAlso ModLoader = request.ModLoader AndAlso Source = request.Source AndAlso
                GameVersion = request.GameVersion AndAlso SearchText = request.SearchText
        End Function
        Public Shared Operator =(left As CompProjectRequest, right As CompProjectRequest) As Boolean
            Return EqualityComparer(Of CompProjectRequest).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As CompProjectRequest, right As CompProjectRequest) As Boolean
            Return Not left = right
        End Operator

    End Class
    Public Class CompProjectStorage

        '加载位置记录

        Public CurseForgeOffset As Integer = 0
        Public CurseForgeTotal As Integer = -1

        Public ModrinthOffset As Integer = 0
        Public ModrinthTotal As Integer = -1

        '结果列表

        ''' <summary>
        ''' 可供展示的所有工程的列表。
        ''' </summary>
        Public Results As New List(Of CompProject)
        ''' <summary>
        ''' 当前的错误信息。如果没有则为 Nothing。
        ''' </summary>
        Public ErrorMessage As String = Nothing

    End Class

    '实际的获取

    Private Const CompPageSize = 40
    ''' <summary>
    ''' 已知工程信息的缓存。
    ''' </summary>
    Public CompProjectCache As New Dictionary(Of String, CompProject)
    ''' <summary>
    ''' 根据搜索请求获取一系列的工程列表。需要基于加载器运行。
    ''' </summary>
    Public Sub CompProjectsGet(Task As LoaderTask(Of CompProjectRequest, Integer))
        Dim Request As CompProjectRequest = Task.Input
        Dim Storage = Request.Storage '避免多线程问题

        If Request.Storage.Results.Count >= Request.TargetResultCount Then
            Log($"[Comp] 已有 {Request.Storage.Results.Count} 个结果，多于所需的 {Request.TargetResultCount} 个结果，结束处理")
            Return
        ElseIf Not Request.CanContinue Then
            If Not Request.Storage.Results.Any() Then
                Throw New Exception("没有符合条件的结果")
            Else
                Log($"[Comp] 已有 {Request.Storage.Results.Count} 个结果，少于所需的 {Request.TargetResultCount} 个结果，但无法继续获取，结束处理")
                Return
            End If
        End If

#Region "拒绝 1.13- Quilt（这个版本根本没有 Quilt）"

        If Request.ModLoader = CompModLoaderType.Quilt AndAlso VersionSortInteger(If(Request.GameVersion, "1.15"), "1.14") = -1 Then
            Throw New Exception("Quilt 不支持 Minecraft " & Request.GameVersion)
        End If

#End Region

#Region "处理搜索文本，赋值回 Task.Input.SearchText"

        Dim RawFilter As String = If(Request.SearchText, "").Trim
        Request.SearchText = RawFilter
        RawFilter = RawFilter.ToLower
        Log("[Comp] 工程列表搜索原始文本：" & RawFilter)

        '中文请求关键字处理
        Dim IsChineseSearch As Boolean = RegexCheck(RawFilter, "[\u4e00-\u9fbb]") AndAlso Not String.IsNullOrEmpty(RawFilter)
        If IsChineseSearch AndAlso (Request.Type = CompType.Mod OrElse Request.Type = CompType.DataPack) Then
            '构造搜索请求
            Dim SearchEntries As New List(Of SearchEntry(Of CompDatabaseEntry))
            For Each Entry In CompDatabase
                If Entry.ChineseName.Contains("动态的树") Then Continue For '这玩意儿附属太多了
                SearchEntries.Add(New SearchEntry(Of CompDatabaseEntry) With {
                    .Item = Entry,
                    .SearchSource = New List(Of KeyValuePair(Of String, Double)) From {
                        New KeyValuePair(Of String, Double)(Entry.ChineseName & If(Entry.CurseForgeSlug, "") & If(Entry.ModrinthSlug, ""), 1)
                    }
                })
            Next
            '获取搜索结果
            Dim SearchResults = Search(SearchEntries, Request.SearchText, 3)
            If Not SearchResults.Any() Then Throw New Exception("无搜索结果，请尝试搜索英文名称")
            Dim SearchResult As String = ""
            For i = 0 To Math.Min(4, SearchResults.Count - 1) '就算全是准确的，也最多只要 5 个
                If Not SearchResults(i).AbsoluteRight AndAlso i >= Math.Min(2, SearchResults.Count - 1) Then Exit For '把 3 个结果拼合以提高准确度
                If SearchResults(i).Item.CurseForgeSlug IsNot Nothing Then SearchResult += SearchResults(i).Item.CurseForgeSlug.Replace("-", " ").Replace("/", " ") & " "
                If SearchResults(i).Item.ModrinthSlug IsNot Nothing Then SearchResult += SearchResults(i).Item.ModrinthSlug.Replace("-", " ").Replace("/", " ") & " "
                SearchResult += SearchResults(i).Item.ChineseName.AfterLast(" (").TrimEnd(") ").BeforeFirst(" - ").
                    Replace(":", "").Replace("(", "").Replace(")", "").ToLower.Replace("/", " ") & " "
            Next
            Log("[Comp] 中文搜索原始关键词：" & SearchResult, LogLevel.Developer)
            '去除常见连接词
            Dim RealFilter As String = ""
            For Each Word In SearchResult.Split(" ")
                If {"the", "of", "a", "mod", "and"}.Contains(Word.ToLowerInvariant) OrElse Val(Word) > 0 Then Continue For
                If SearchResult.Split(" ").Count > 3 AndAlso {"ftb"}.Contains(Word.ToLower) Then Continue For
                RealFilter += Word.TrimStart("{[(").TrimEnd("}])") & " "
            Next
            Request.SearchText = RealFilter
            Log("[Comp] 中文搜索最终关键词：" & RealFilter, LogLevel.Developer)
        End If

        '驼峰英文请求关键字处理
        Dim SpacedKeywords = Request.SearchText.RegexReplace("([A-Z]+|[a-z]+?)(?=[A-Z]+[a-z]+[a-z ]*)", "$& ")
        Dim ConnectedKeywords = Request.SearchText.Replace(" ", "")
        Dim AllPossibleKeywords = (SpacedKeywords & " " & If(IsChineseSearch, Request.SearchText, ConnectedKeywords & " " & RawFilter)).ToLower

        '最终处理关键字：分割、去重
        Dim RightKeywords As New List(Of String)
        For Each Keyword In AllPossibleKeywords.Split(" ")
            Keyword = Keyword.Trim("["c, "]"c)
            If Keyword = "" Then Continue For
            If {"forge", "fabric", "for", "mod", "quilt"}.Contains(Keyword) Then '#208
                Log("[Comp] 已跳过搜索关键词：" & Keyword, LogLevel.Developer)
                Continue For
            End If
            RightKeywords.Add(Keyword)
        Next
        If RawFilter.Length > 0 AndAlso Not RightKeywords.Any() Then
            Request.SearchText = RawFilter '全都被过滤掉了
        Else
            Request.SearchText = Join(RightKeywords.Distinct.ToList, " ").ToLower
        End If

        '例外项：OptiForge、OptiFabric（拆词后因为包含 Forge/Fabric 导致无法搜到实际的 Mod）
        If RawFilter.Replace(" ", "").ContainsF("optiforge", True) Then Request.SearchText = "optiforge"
        If RawFilter.Replace(" ", "").ContainsF("optifabric", True) Then Request.SearchText = "optifabric"
        Log("[Comp] 工程列表搜索最终文本：" & Request.SearchText, LogLevel.Debug)
        Task.Progress = 0.1

#End Region

        Dim RealResults As New List(Of CompProject)
Retry:
        Dim RawResults As New List(Of CompProject)
        Dim [Error] As Exception = Nothing

#Region "从 CurseForge 和 Modrinth 获取结果列表，存储于 RawResults"

        Dim CurseForgeThread As Thread = Nothing
        Dim ModrinthThread As Thread = Nothing
        Dim ResultsLock As New Object

        '在 1.14-，部分老 Mod 没有设置支持的加载器，因此添加 Forge 筛选就会出现遗漏
        '所以，在发起请求时不筛选加载器，然后在返回的结果中自行筛除不是 Forge 的 Mod
        Dim IsOldForgeRequest = Request.ModLoader = CompModLoaderType.Forge AndAlso Request.GameVersion?.Contains(".") AndAlso Val(Request.GameVersion.Split(".")(1)) < 14
        If IsOldForgeRequest Then Request.ModLoader = CompModLoaderType.Any
        Dim CurseForgeUrl As String = Request.GetCurseForgeAddress()
        Dim ModrinthUrl As String = Request.GetModrinthAddress()
        If IsOldForgeRequest Then Request.ModLoader = CompModLoaderType.Forge '设置回去

        Try

            '启动 CurseForge 线程
            Dim CurseForgeFailed As Boolean = False
            If CurseForgeUrl IsNot Nothing Then
                CurseForgeThread = RunInNewThread(
                Sub()
                    Try
                        '获取工程列表
                        Log("[Comp] 开始从 CurseForge 获取工程列表：" & CurseForgeUrl)
                        Dim RequestResult As JObject = DlModRequest(CurseForgeUrl, IsJson:=True)
                        Task.Progress += 0.2
                        Dim ProjectList As New List(Of CompProject)
                        For Each JsonEntry As JObject In RequestResult("data")
                            Dim Project As New CompProject(JsonEntry)
                            If Request.Type = CompType.ResourcePack AndAlso Project.Tags.Contains("数据包") Then Continue For 'CurseForge 将一些数据包分类成了资源包
                            ProjectList.Add(Project)
                        Next
                        '更新结果
                        SyncLock ResultsLock
                            RawResults.AddRange(ProjectList)
                        End SyncLock
                        Storage.CurseForgeOffset += ProjectList.Count
                        Storage.CurseForgeTotal = RequestResult("pagination")("totalCount").ToObject(Of Integer)
                        Log($"[Comp] 从 CurseForge 获取到了 {ProjectList.Count} 个工程（已获取 {Storage.CurseForgeOffset} 个，共 {Storage.CurseForgeTotal} 个）")
                    Catch ex As Exception
                        Log(ex, "从 CurseForge 获取工程列表失败")
                        Storage.CurseForgeTotal = -1 'Storage.CurseForgeOffset
                        [Error] = ex
                        CurseForgeFailed = True
                    End Try
                End Sub, "CurseForge Project Request")
            End If

            '启动 Modrinth 线程
            Dim ModrinthFailed As Boolean = False
            If ModrinthUrl IsNot Nothing Then
                ModrinthThread = RunInNewThread(
                Sub()
                    Try
                        Log("[Comp] 开始从 Modrinth 获取工程列表：" & ModrinthUrl)
                        Dim RequestResult As JObject = DlModRequest(ModrinthUrl, IsJson:=True)
                        Task.Progress += 0.2
                        Dim ProjectList As New List(Of CompProject)
                        For Each JsonEntry As JObject In RequestResult("hits")
                            ProjectList.Add(New CompProject(JsonEntry))
                        Next
                        '更新结果
                        SyncLock ResultsLock
                            For Each Project In ProjectList
                                RawResults.Add(Project)
                            Next
                        End SyncLock
                        Storage.ModrinthOffset += ProjectList.Count
                        Storage.ModrinthTotal = RequestResult("total_hits").ToObject(Of Integer)
                        Log($"[Comp] 从 Modrinth 获取到了 {ProjectList.Count} 个工程（已获取 {Storage.ModrinthOffset} 个，共 {Storage.ModrinthTotal} 个）")
                    Catch ex As Exception
                        Log(ex, "从 Modrinth 获取工程列表失败")
                        Storage.ModrinthTotal = -1 'Storage.ModrinthOffset
                        [Error] = ex
                        ModrinthFailed = True
                    End Try
                End Sub, "Modrinth Project Request")
            End If

            '等待线程结束
            If CurseForgeThread IsNot Nothing Then CurseForgeThread.Join()
            If Task.IsAborted Then Return '会自动触发 Finally
            If ModrinthThread IsNot Nothing Then ModrinthThread.Join()
            If Task.IsAborted Then Return

            '确保存在结果
            Storage.ErrorMessage = Nothing
            If Not RawResults.Any() Then
                If [Error] IsNot Nothing Then
                    Throw [Error]
                Else
                    If IsChineseSearch AndAlso Not (Request.Type = CompType.Mod OrElse Request.Type = CompType.DataPack) Then
                        Throw New Exception("没有搜索结果，请尝试使用英文搜索")
                    ElseIf Request.Source = CompSourceType.CurseForge AndAlso Request.Tag.NotSupportCf Then
                        Throw New Exception("CurseForge 不兼容所选的类型")
                    ElseIf Request.Source = CompSourceType.Modrinth AndAlso Request.Tag.NotSupportMr Then
                        Throw New Exception("Modrinth 不兼容所选的类型")
                    Else
                        Throw New Exception("没有搜索结果")
                    End If
                End If
            ElseIf [Error] IsNot Nothing Then
                '有结果但是有错误
                If CurseForgeFailed Then
                    Storage.ErrorMessage = $"无法连接到 CurseForge，所以目前仅显示了来自 Modrinth 的内容，搜索结果可能不全。{vbCrLf}请稍后重试，或使用 VPN 以改善网络环境。"
                Else
                    Storage.ErrorMessage = $"无法连接到 Modrinth，所以目前仅显示了来自 CurseForge 的内容，搜索结果可能不全。{vbCrLf}请稍后重试，或使用 VPN 以改善网络环境。"
                End If
            End If

        Finally
            CurseForgeThread?.Interrupt()
            ModrinthThread?.Interrupt()
        End Try

        '筛除不是 Forge 的 Mod
        If IsOldForgeRequest Then
            RawResults = RawResults.Where(Function(p) Not p.ModLoaders.Any() OrElse p.ModLoaders.Contains(CompModLoaderType.Forge)).ToList
        End If

#End Region

#Region "提取非重复项，存储于 RealResults"

        '将 Modrinth 排在 CurseForge 的前面，避免加载结束顺序不同导致排名不同
        '这样做的话，去重后将优先保留 CurseForge 内容
        RawResults = RawResults.Where(Function(x) Not x.FromCurseForge).Concat(RawResults.Where(Function(x) x.FromCurseForge)).ToList
        'RawResults 去重
        RawResults = RawResults.Distinct(Function(a, b) a.IsLike(b))
        '已有内容去重
        RawResults = RawResults.Where(Function(r) Not RealResults.Any(Function(b) r.IsLike(b)) AndAlso
                                                  Not Storage.Results.Any(Function(b) r.IsLike(b))).ToList
        '加入列表
        RealResults.AddRange(RawResults)
        Log($"[Comp] 去重、筛选后累计新增结果 {RealResults.Count} 个")

#End Region

#Region "检查结果数量，如果不足且可继续，会继续加载下一页"

        If RealResults.Count + Storage.Results.Count < Request.TargetResultCount Then
            Log($"[Comp] 总结果数需求最少 {Request.TargetResultCount} 个，仅获得了 {RealResults.Count + Storage.Results.Count} 个")
            If Request.CanContinue AndAlso [Error] Is Nothing Then '如果有下载源失败则不再重试，这时候重试可能导致无限循环
                Log("[Comp] 将继续尝试加载下一页")
                GoTo Retry
            Else
                Log("[Comp] 无法继续加载，将强制结束")
            End If
        End If

#End Region

#Region "将结果排序并添加"

        Dim Scores As New Dictionary(Of CompProject, Double) '排序分
        Dim GetDownloadCountMult =
        Function(Project As CompProject) As Double
            Select Case Request.Type
                Case CompType.Mod, CompType.ModPack
                    Return If(Project.FromCurseForge, 1, 7)
                Case CompType.DataPack
                    Return If(Project.FromCurseForge, 10, 1)
                Case CompType.ResourcePack, CompType.Shader
                    Return If(Project.FromCurseForge, 1, 5)
                Case Else
                    Return 1
            End Select
        End Function
        If String.IsNullOrEmpty(Request.SearchText) Then
            '如果没有搜索文本，按下载量将结果排序
            For Each Result As CompProject In RealResults
                Scores.Add(Result, Result.DownloadCount * GetDownloadCountMult(Result))
            Next
        Else
            '如果有搜索文本，按关联度将结果排序
            '排序分 = 搜索相对相似度 (1) + 下载量权重 (对数，10 亿时为 1) + 有中文名 (0.2)
            Dim Entry As New List(Of SearchEntry(Of CompProject))
            For Each Result As CompProject In RealResults
                Scores.Add(Result, If(Result.WikiId > 0, 0.2, 0) +
                           Math.Log10(Math.Max(Result.DownloadCount, 1) * GetDownloadCountMult(Result)) / 9)
                Entry.Add(New SearchEntry(Of CompProject) With {.Item = Result, .SearchSource = New List(Of KeyValuePair(Of String, Double)) From {
                          New KeyValuePair(Of String, Double)(If(IsChineseSearch, Result.TranslatedName, Result.RawName), 1),
                          New KeyValuePair(Of String, Double)(Result.Description, 0.05)}})
            Next
            Dim SearchResult = Search(Entry, RawFilter, 101, -1)
            For Each OneResult In SearchResult
                Scores(OneResult.Item) += OneResult.Similarity / SearchResult(0).Similarity '最高 1 分的相似度分
            Next
        End If
        '根据排序分得出结果并添加
        Storage.Results.AddRange(
            Scores.OrderByDescending(Function(s) s.Value).Select(Function(r) r.Key))

#End Region

    End Sub

#End Region

#Region "CompFile | 文件信息"

    '类定义

    Public Enum CompFileStatus
        Release = 1 '枚举值来源：https://docs.curseforge.com/#tocS_FileReleaseType
        Beta = 2
        Alpha = 3
    End Enum
    Public Class CompFile

        '源信息

        ''' <summary>
        ''' 文件的种类。
        ''' </summary>
        Public ReadOnly Type As CompType
        ''' <summary>
        ''' 该文件来自 CurseForge 还是 Modrinth。
        ''' </summary>
        Public ReadOnly FromCurseForge As Boolean
        ''' <summary>
        ''' 用于唯一性鉴别该文件的 ID。CurseForge 中为 123456 的大整数，Modrinth 中为英文乱码的 Version 字段。
        ''' </summary>
        Public ReadOnly Id As String

        '描述性信息

        ''' <summary>
        ''' 文件描述名（并非文件名，是自定义的字段）。对很多 Mod，这会给出 Mod 版本号。
        ''' </summary>
        Public DisplayName As String
        ''' <summary>
        ''' 发布时间。
        ''' </summary>
        Public ReadOnly ReleaseDate As Date
        ''' <summary>
        ''' 下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量，且 CurseForge 可能错误地返回 0。
        ''' </summary>
        Public ReadOnly DownloadCount As Integer
        ''' <summary>
        ''' 支持的 Mod 加载器列表。可能为空。
        ''' </summary>
        Public ReadOnly ModLoaders As List(Of CompModLoaderType)
        ''' <summary>
        ''' 支持的游戏版本列表。类型包括："1.18.5"，"1.18"，"1.18 预览版"，"21w15a"，"未知版本"。
        ''' </summary>
        Public ReadOnly GameVersions As List(Of String)
        ''' <summary>
        ''' 发布状态：Release/Beta/Alpha。
        ''' </summary>
        Public ReadOnly Status As CompFileStatus
        ''' <summary>
        ''' 发布状态的友好描述。例如："正式版"，"Beta 版"。
        ''' </summary>
        Public ReadOnly Property StatusDescription As String
            Get
                Select Case Status
                    Case CompFileStatus.Release
                        Return "正式版"
                    Case CompFileStatus.Beta
                        Return If(ModeDebug, "Beta 版", "测试版")
                    Case Else
                        Return If(ModeDebug, "Alpha 版", "早期测试版")
                End Select
            End Get
        End Property

        '下载信息
        ''' <summary>
        ''' 下载信息是否可用。
        ''' </summary>
        Public ReadOnly Property Available As Boolean
            Get
                Return FileName IsNot Nothing AndAlso DownloadUrls IsNot Nothing
            End Get
        End Property
        ''' <summary>
        ''' 下载的文件名。
        ''' </summary>
        Public ReadOnly FileName As String = Nothing
        ''' <summary>
        ''' 文件所有可能的下载源。
        ''' </summary>
        Public DownloadUrls As List(Of String)
        ''' <summary>
        ''' 文件的 SHA1 或 MD5。
        ''' </summary>
        Public ReadOnly Hash As String = Nothing
        ''' <summary>
        ''' 该文件的所有依赖工程的原始 ID。
        ''' 这些 ID 可能没有加载，在加载后会添加到 Dependencies 中（主要是因为 Modrinth 返回的是字符串 ID 而非 Slug，导致 Project.Id 查询不到）。
        ''' </summary>
        Public ReadOnly RawDependencies As New List(Of String)
        ''' <summary>
        ''' 该文件的所有依赖工程的 Project.Id。
        ''' </summary>
        Public ReadOnly Dependencies As New List(Of String)
        ''' <summary>
        ''' 获取下载信息。
        ''' </summary>
        ''' <param name="LocalAddress">目标本地文件夹，或完整的文件路径。会自动判断类型。</param>
        Public Function ToNetFile(LocalAddress As String) As NetFile
            Return New NetFile(DownloadUrls, LocalAddress & If(LocalAddress.EndsWithF("\"), FileName, ""), New FileChecker(Hash:=Hash), UseBrowserUserAgent:=True)
        End Function

        '实例化

        ''' <summary>
        ''' 从文件 Json 中初始化实例。若出错会抛出异常。
        ''' </summary>
        Public Sub New(Data As JObject, DefaultType As CompType)
            Type = DefaultType
            If Data.ContainsKey("FromCurseForge") Then
#Region "CompJson"
                FromCurseForge = Data("FromCurseForge").ToObject(Of Boolean)
                Id = Data("Id").ToString
                DisplayName = Data("DisplayName").ToString
                ReleaseDate = Data("ReleaseDate").ToObject(Of Date)
                DownloadCount = Data("DownloadCount").ToObject(Of Integer)
                Status = CType(Data("Status").ToObject(Of Integer), CompFileStatus)
                If Data.ContainsKey("FileName") Then FileName = Data("FileName").ToString
                If Data.ContainsKey("DownloadUrls") Then DownloadUrls = Data("DownloadUrls").ToObject(Of List(Of String))
                If Data.ContainsKey("ModLoaders") Then ModLoaders = Data("ModLoaders").ToObject(Of List(Of CompModLoaderType))
                If Data.ContainsKey("Hash") Then Hash = Data("Hash").ToString
                If Data.ContainsKey("GameVersions") Then GameVersions = Data("GameVersions").ToObject(Of List(Of String))
                If Data.ContainsKey("RawDependencies") Then RawDependencies = Data("RawDependencies").ToObject(Of List(Of String))
                If Data.ContainsKey("Dependencies") Then Dependencies = Data("Dependencies").ToObject(Of List(Of String))
#End Region
            Else
                FromCurseForge = Data.ContainsKey("gameId")
                If FromCurseForge Then
#Region "CurseForge"
                    '简单信息
                    Id = Data("id")
                    DisplayName = Data("displayName").ToString.Replace("	", "").Trim(" ")
                    ReleaseDate = Data("fileDate")
                    Status = CType(Data("releaseType").ToObject(Of Integer), CompFileStatus)
                    DownloadCount = Data("downloadCount")
                    FileName = Data("fileName")
                    Hash = CType(Data("hashes"), JArray).ToList.FirstOrDefault(Function(s) s("algo").ToObject(Of Integer) = 1)?("value")
                    If Hash Is Nothing Then Hash = CType(Data("hashes"), JArray).ToList.FirstOrDefault(Function(s) s("algo").ToObject(Of Integer) = 2)?("value")
                    'DownloadAddress
                    Dim Url = Data("downloadUrl").ToString
                    If Url = "" Then Url = $"https://media.forgecdn.net/files/{CInt(Id.ToString.Substring(0, 4))}/{CInt(Id.ToString.Substring(4))}/{FileName}"
                    Url = Url.Replace(FileName, Net.WebUtility.UrlEncode(FileName)) '对文件名进行编码
                    DownloadUrls = HandleCurseForgeDownloadUrls(Url) '对脑残 CurseForge 的下载地址进行多种修正
                    DownloadUrls.AddRange(DownloadUrls.Select(Function(u) DlSourceModGet(u)).ToList) '添加镜像源，这个写法是为了让镜像源排在后面
                    DownloadUrls = DownloadUrls.Distinct.ToList '最终去重
                    'Dependencies
                    If Data.ContainsKey("dependencies") Then
                        RawDependencies = Data("dependencies").
                            Where(Function(d) d("relationType").ToObject(Of Integer) = 3 AndAlso '种类为依赖
                                              d("modId").ToObject(Of Integer) <> 306612 AndAlso d("modId").ToObject(Of Integer) <> 634179). '排除 Fabric API 和 Quilt API
                            Select(Function(d) d("modId").ToString).ToList
                    End If
                    'GameVersions
                    Dim RawVersions As List(Of String) = Data("gameVersions").Select(Function(t) t.ToString.Trim.ToLower).ToList
                    GameVersions = RawVersions.Where(Function(v) v.StartsWithF("1.")).Select(Function(v) v.Replace("-snapshot", " 预览版")).ToList
                    If GameVersions.Count > 1 Then
                        GameVersions = GameVersions.Sort(AddressOf VersionSortBoolean).ToList
                        If Type = CompType.ModPack Then GameVersions = New List(Of String) From {GameVersions(0)} '整合包理应只 “支持” 一个版本
                    ElseIf GameVersions.Count = 1 Then
                        GameVersions = GameVersions.ToList
                    Else
                        GameVersions = New List(Of String) From {"未知版本"}
                    End If
                    'ModLoaders
                    ModLoaders = New List(Of CompModLoaderType)
                    If RawVersions.Contains("forge") Then ModLoaders.Add(CompModLoaderType.Forge)
                    If RawVersions.Contains("fabric") Then ModLoaders.Add(CompModLoaderType.Fabric)
                    If RawVersions.Contains("quilt") Then ModLoaders.Add(CompModLoaderType.Quilt)
                    If RawVersions.Contains("neoforge") Then ModLoaders.Add(CompModLoaderType.NeoForge)
#End Region
                Else
#Region "Modrinth"
                    '简单信息
                    Id = Data("id")
                    DisplayName = Data("name").ToString.Replace("	", "").Trim(" ")
                    ReleaseDate = Data("date_published")
                    Status = If(Data("version_type").ToString = "release", CompFileStatus.Release, If(Data("version_type").ToString = "beta", CompFileStatus.Beta, CompFileStatus.Alpha))
                    DownloadCount = Data("downloads")
                    If CType(Data("files"), JArray).Any() Then '可能为空
                        Dim File As JToken = Data("files")(0)
                        FileName = File("filename")
                        DownloadUrls = New List(Of String) From {File("url"), DlSourceModGet(File("url"))}.Distinct.ToList '同时添加了镜像源
                        Hash = File("hashes")("sha1")
                    End If
                    'ModLoaders
                    '结果可能混杂着 Mod、数据包和服务端插件
                    Dim RawLoaders As List(Of String) = Data("loaders").Select(Function(v) v.ToString).ToList
                    ModLoaders = New List(Of CompModLoaderType)
                    If Type = CompType.Mod Then '以尽量宽容的方式检测加载器，以免同时兼容两种的项被删除
                        If RawLoaders.Intersect({"bukkit", "folia", "paper", "purpur", "spigot"}).Any() Then Type = CompType.Plugin 'Veinminer Enchantment 同时支持服务端与 Fabric
                        If RawLoaders.Contains("datapack") Then Type = CompType.DataPack
                        If RawLoaders.Contains("forge") Then ModLoaders.Add(CompModLoaderType.Forge) : Type = CompType.Mod
                        If RawLoaders.Contains("neoforge") Then ModLoaders.Add(CompModLoaderType.NeoForge) : Type = CompType.Mod
                        If RawLoaders.Contains("fabric") Then ModLoaders.Add(CompModLoaderType.Fabric) : Type = CompType.Mod
                        If RawLoaders.Contains("quilt") Then ModLoaders.Add(CompModLoaderType.Quilt) : Type = CompType.Mod
                    ElseIf Type = CompType.DataPack Then
                        If RawLoaders.Intersect({"bukkit", "folia", "paper", "purpur", "spigot"}).Any() Then Type = CompType.Plugin
                        If RawLoaders.Contains("forge") Then ModLoaders.Add(CompModLoaderType.Forge) : Type = CompType.Mod
                        If RawLoaders.Contains("neoforge") Then ModLoaders.Add(CompModLoaderType.NeoForge) : Type = CompType.Mod
                        If RawLoaders.Contains("fabric") Then ModLoaders.Add(CompModLoaderType.Fabric) : Type = CompType.Mod
                        If RawLoaders.Contains("quilt") Then ModLoaders.Add(CompModLoaderType.Quilt) : Type = CompType.Mod
                        If RawLoaders.Contains("datapack") Then Type = CompType.DataPack
                    End If
                    'Dependencies
                    If Data.ContainsKey("dependencies") Then
                        RawDependencies = Data("dependencies").
                            Where(Function(d) d("dependency_type") = "required" AndAlso '种类为依赖
                                              d("project_id") <> "P7dR8mSH" AndAlso d("project_id") <> "qvIfYCYJ" AndAlso '排除 Fabric API 和 Quilt API
                                              d("project_id").ToString.Length > 0). '有时候真的会空……
                            Select(Function(d) d("project_id").ToString).ToList
                    End If
                    'GameVersions
                    Dim RawVersions As List(Of String) = Data("game_versions").Select(Function(t) t.ToString.Trim.ToLower).ToList
                    GameVersions = RawVersions.Where(Function(v) v.StartsWithF("1.") OrElse v.StartsWithF("b1.")).
                                               Select(Function(v) If(v.Contains("-"), v.BeforeFirst("-") & " 预览版", If(v.StartsWithF("b1."), "远古版本", v))).ToList
                    If GameVersions.Count > 1 Then
                        GameVersions = GameVersions.Sort(AddressOf VersionSortBoolean).ToList
                        If Type = CompType.ModPack Then GameVersions = New List(Of String) From {GameVersions(0)} '整合包理应只 “支持” 一个版本
                    ElseIf GameVersions.Count = 1 Then
                        '无需处理
                    ElseIf RawVersions.Any(Function(v) RegexCheck(v, "[0-9]{2}w[0-9]{2}[a-z]{1}")) Then
                        GameVersions = RawVersions.Where(Function(v) RegexCheck(v, "[0-9]{2}w[0-9]{2}[a-z]{1}")).ToList
                    Else
                        GameVersions = New List(Of String) From {"未知版本"}
                    End If
#End Region
                End If
            End If
        End Sub

        ''' <summary>
        ''' 重新整理 CurseForge 的下载地址。
        ''' </summary>
        Public Shared Function HandleCurseForgeDownloadUrls(Url As String) As List(Of String)
            Return {
                Url.Replace("-service.overwolf.wtf", ".forgecdn.net").Replace("://edge.", "://mediafilez.").Replace("://media.", "://mediafilez."),
                Url.Replace("://edge.", "://mediafilez.").Replace("://media.", "://mediafilez."),
                Url.Replace("-service.overwolf.wtf", ".forgecdn.net").Replace("://edge.", "://media."),
                Url.Replace("-service.overwolf.wtf", ".forgecdn.net"),
                Url.Replace("://edge.", "://media."),
                Url
            }.Distinct.ToList
        End Function

        ''' <summary>
        ''' 将当前实例转为可用于保存缓存的 Json。
        ''' </summary>
        Public Function ToJson() As JObject
            Dim Json As New JObject
            Json.Add("FromCurseForge", FromCurseForge)
            Json.Add("Id", Id)
            Json.Add("DisplayName", DisplayName)
            Json.Add("ReleaseDate", ReleaseDate)
            Json.Add("DownloadCount", DownloadCount)
            Json.Add("ModLoaders", New JArray(ModLoaders.Select(Function(m) CInt(m))))
            Json.Add("GameVersions", New JArray(GameVersions))
            Json.Add("Status", CInt(Status))
            If FileName IsNot Nothing Then Json.Add("FileName", FileName)
            If DownloadUrls IsNot Nothing Then Json.Add("DownloadUrls", New JArray(DownloadUrls))
            If Hash IsNot Nothing Then Json.Add("Hash", Hash)
            Json.Add("RawDependencies", New JArray(RawDependencies))
            Json.Add("Dependencies", New JArray(Dependencies))
            Return Json
        End Function
        ''' <summary>
        ''' 将当前文件信息实例化为控件。
        ''' </summary>
        Public Function ToListItem(OnClick As MyListItem.ClickEventHandler, Optional OnSaveClick As MyIconButton.ClickEventHandler = Nothing,
                                   Optional BadDisplayName As Boolean = False) As MyListItem

            '获取描述信息
            Dim Title As String = If(BadDisplayName, FileName, DisplayName)
            Dim Info As New List(Of String)
            If Title <> FileName.BeforeLast(".") Then Info.Add(FileName.BeforeLast("."))
            If Dependencies.Any Then Info.Add(Dependencies.Count & " 项前置")
            If GameVersions.All(Function(v) Not IsVersionNameLikeRelease(v)) Then Info.Add($"游戏版本 {Join(GameVersions, "、")}")
            If DownloadCount > 0 Then 'CurseForge 的下载次数经常错误地返回 0
                Info.Add("下载 " & If(DownloadCount > 100000, Math.Round(DownloadCount / 10000) & " 万次", DownloadCount & " 次"))
            End If
            Info.Add("更新于 " & GetTimeSpanString(ReleaseDate - Date.Now, False))
            If Status <> CompFileStatus.Release Then Info.Add(StatusDescription)

            '建立控件
            Dim NewItem As New MyListItem With {
                .Title = Title,
                .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Me,
                .Info = Info.Join("，")
            }
            Select Case Status
                Case CompFileStatus.Release
                    NewItem.Logo = PathImage & "Icons/R.png"
                Case CompFileStatus.Beta
                    NewItem.Logo = PathImage & "Icons/B.png"
                Case Else 'Alpha
                    NewItem.Logo = PathImage & "Icons/A.png"
            End Select
            AddHandler NewItem.Click, OnClick

            '建立另存为按钮
            If OnSaveClick IsNot Nothing Then
                Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = "另存为"}
                ToolTipService.SetPlacement(BtnSave, Primitives.PlacementMode.Center)
                ToolTipService.SetVerticalOffset(BtnSave, 30)
                ToolTipService.SetHorizontalOffset(BtnSave, 2)
                AddHandler BtnSave.Click, OnSaveClick
                NewItem.Buttons = {BtnSave}
            End If

            '结束
            Return NewItem
        End Function

        '辅助函数

        Public Overrides Function ToString() As String
            Return $"{Id}: {FileName}"
        End Function

    End Class

    '获取

    ''' <summary>
    ''' 已知文件信息的缓存。
    ''' </summary>
    Public CompFilesCache As New Dictionary(Of String, List(Of CompFile))
    ''' <summary>
    ''' 获取某个工程下的全部文件列表。
    ''' 必须在工作线程执行，失败会抛出异常。
    ''' </summary>
    Public Function CompFilesGet(ProjectId As String, FromCurseForge As Boolean) As List(Of CompFile)
        '获取工程对象
        Dim TargetProject As CompProject
        If CompProjectCache.ContainsKey(ProjectId) Then '存在缓存
            TargetProject = CompProjectCache(ProjectId)
        ElseIf FromCurseForge Then 'CurseForge
            TargetProject = New CompProject(DlModRequest("https://api.curseforge.com/v1/mods/" & ProjectId, IsJson:=True)("data"))
        Else 'Modrinth
            TargetProject = New CompProject(DlModRequest("https://api.modrinth.com/v2/project/" & ProjectId, IsJson:=True))
        End If
        '获取工程对象的文件列表
        If Not CompFilesCache.ContainsKey(ProjectId) Then '有缓存也不能直接返回，这时候前置可能没获取（#5173）
            Log("[Comp] 开始获取文件列表：" & ProjectId)
            Dim ResultJsonArray As JArray
            If FromCurseForge Then
                'CurseForge
                'HMCL 一次性请求了 10000 个文件，虽然不知道会不会出问题但先这样吧……（#5522）
                ResultJsonArray = DlModRequest($"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=10000", IsJson:=True)("data")
                '之前只请求一部分文件的方法备份如下：
                'If TargetProject.Type = CompType.Mod Then 'Mod 使用每个版本最新的文件
                '    ResultJsonArray = GetJson(DlModRequest("https://api.curseforge.com/v1/mods/files", "POST", "{""fileIds"": [" & Join(TargetProject.CurseForgeFileIds, ",") & "]}", "application/json"))("data")
                'Else '否则使用全部文件
                '    ResultJsonArray = DlModRequest($"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=999", IsJson:=True)("data")
                'End If
            Else
                'Modrinth
                ResultJsonArray = DlModRequest($"https://api.modrinth.com/v2/project/{ProjectId}/version", IsJson:=True)
            End If
            CompFilesCache(ProjectId) = ResultJsonArray.Select(Function(a) New CompFile(a, TargetProject.Type)).
                Where(Function(a) a.Available).ToList.
                Distinct(Function(a, b) a.Id = b.Id) 'CurseForge 可能会重复返回相同项（#1330）
        End If
        '获取前置列表
        Dim Deps As List(Of String) = CompFilesCache(ProjectId).SelectMany(Function(f) f.RawDependencies).Distinct().ToList
        Dim UndoneDeps = Deps.Where(Function(f) Not CompProjectCache.ContainsKey(f)).ToList
        '获取前置工程信息
        If UndoneDeps.Any Then
            Log($"[Comp] {ProjectId} 文件列表中还需要获取信息的前置：{Join(UndoneDeps, "，")}")
            Dim Projects As JArray
            If TargetProject.FromCurseForge Then
                Projects = GetJson(DlModRequest("https://api.curseforge.com/v1/mods",
                    "POST", "{""modIds"": [" & Join(UndoneDeps, ",") & "]}", "application/json"))("data")
            Else
                Projects = DlModRequest($"https://api.modrinth.com/v2/projects?ids=[""{Join(UndoneDeps, """,""")}""]", IsJson:=True)
            End If
            For Each Project In Projects
                Dim Unused As New CompProject(Project) '在 New 的时候会添加缓存以便之后读取
            Next
        End If
        '更新前置信息
        If Deps.Any Then
            For Each DepProject In Deps.Where(Function(id) CompProjectCache.ContainsKey(id)).Select(Function(id) CompProjectCache(id))
                For Each File In CompFilesCache(ProjectId)
                    If File.RawDependencies.Contains(DepProject.Id) AndAlso DepProject.Id <> ProjectId Then
                        If Not File.Dependencies.Contains(DepProject.Id) Then File.Dependencies.Add(DepProject.Id)
                    End If
                Next
            Next
        End If
        Return CompFilesCache(ProjectId)
    End Function

    ''' <summary>
    ''' 预载包含大量 CompFile 的卡片，添加必要的元素和前置列表。
    ''' </summary>
    Public Sub CompFilesCardPreload(Stack As StackPanel, Files As List(Of CompFile))
        '获取卡片对应的前置 ID
        '如果为整合包就不会有 Dependencies 信息，所以不用管
        Dim Deps As List(Of String) = Files.SelectMany(Function(f) f.Dependencies).Distinct.ToList()
        Deps.Sort()
        If Not Deps.Any() Then Return
        Deps = Deps.Where(
        Function(dep)
            If Not CompProjectCache.ContainsKey(dep) Then Log($"[Comp] 未找到 ID {dep} 的前置信息", LogLevel.Debug)
            Return CompProjectCache.ContainsKey(dep)
        End Function).ToList
        '添加开头间隔
        Stack.Children.Add(New TextBlock With {.Text = "前置资源", .FontSize = 14, .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(6, 2, 0, 5)})
        '添加前置列表
        For Each Dep In Deps
            Dim Item = CompProjectCache(Dep).ToCompItem(False, False)
            Stack.Children.Add(Item)
        Next
        '添加结尾间隔
        Stack.Children.Add(New TextBlock With {.Text = "版本列表", .FontSize = 14, .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(6, 12, 0, 5)})
    End Sub

#End Region

End Module
