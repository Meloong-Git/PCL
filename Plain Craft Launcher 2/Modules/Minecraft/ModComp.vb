Imports System.IO.Compression

Public Module ModComp

    <Flags> Public Enum CompType
        ''' <summary> 
        ''' Mod。
        ''' </summary>
        [Mod] = 1
        ''' <summary>
        ''' 整合包。
        ''' </summary>
        ModPack = 2
        ''' <summary>
        ''' 资源包。
        ''' </summary>
        ResourcePack = 4
        ''' <summary>
        ''' 光影包。
        ''' </summary>
        Shader = 8
        ''' <summary>
        ''' 数据包。
        ''' </summary>
        DataPack = 16
        ''' <summary>
        ''' 服务端插件。
        ''' </summary>
        Plugin = 32
        ''' <summary>
        ''' 同时包含数据包以及 Mod。
        ''' </summary>
        ModOrDataPack = [Mod] Or DataPack
        ''' <summary>
        ''' 允许任意种类，或种类未知。
        ''' </summary>
        Any = [Mod] Or ModPack Or ResourcePack Or Shader Or DataPack Or Plugin
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

    Private ReadOnly Property CompDatabase As List(Of CompDatabaseEntry)
        Get
            Static _CompDatabase As List(Of CompDatabaseEntry) = Nothing
            If _CompDatabase IsNot Nothing Then Return _CompDatabase
            '初始化数据库
            _CompDatabase = New List(Of CompDatabaseEntry)
            Dim i As Integer = 0
            Using Archive As New ZipArchive(New MemoryStream(GetResources("ModData")), ZipArchiveMode.Read)
                For Each Line In ReadFile(Archive.GetEntry("moddata.txt").Open(), Encoding.UTF8).Replace(vbCrLf, vbLf).Replace(vbCr, "").Split(vbLf)
                    i += 1
                    If Line = "" Then Continue For
                    For Each EntryData As String In Line.Split("¨")
                        Dim Entry = New CompDatabaseEntry
                        Dim Splited = EntryData.Split("|")
                        Entry.Popularity = Val(Splited(0))
                        If Splited(1).StartsWithF("@") Then
                            Entry.CurseForgeSlug = Nothing
                            Entry.ModrinthSlug = Splited(1).Replace("@", "")
                        ElseIf Splited(1).EndsWithF("@") Then
                            Entry.CurseForgeSlug = Splited(1).TrimEnd("@")
                            Entry.ModrinthSlug = Entry.CurseForgeSlug
                        ElseIf Splited(1).Contains("@") Then
                            Entry.CurseForgeSlug = Splited(1).Split("@")(0)
                            Entry.ModrinthSlug = Splited(1).Split("@")(1)
                        Else
                            Entry.CurseForgeSlug = Splited(1)
                            Entry.ModrinthSlug = Nothing
                        End If
                        Entry.WikiId = i
                        If Splited.Count >= 3 Then
                            Entry.ChineseName = Splited.Last
                            If Entry.ChineseName.Contains("*") Then '处理 *
                                Entry.ChineseName = Entry.ChineseName.Replace("*",
                                    $" ({If(Entry.CurseForgeSlug, Entry.ModrinthSlug).Replace("-", " ").Capitalize})")
                            End If
                        End If
                        _CompDatabase.Add(Entry)
                    Next
                Next
            End Using
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
        ''' <summary>
        ''' MC 百科的浏览量逆序排行，1 代表浏览量最低。
        ''' </summary>
        Public Popularity As Integer

        Public Overrides Function ToString() As String
            Return If(CurseForgeSlug, "") & "&" & If(ModrinthSlug, "") & "|" & WikiId & "|" & ChineseName & ", Rank " & Popularity
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
        ''' 工程中包含的文件种类。
        ''' 若为 Modrinth 工程，可能为 Mod 或 数据包。
        ''' </summary>
        Public ReadOnly Types As CompType
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
        ''' 最后一次更新的时间。
        ''' </summary>
        Public ReadOnly LastUpdate As Date
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
        ''' Logo 图片的下载地址。
        ''' 若为 Nothing 则没有，保证不为空字符串。
        ''' </summary>
        Public LogoUrl As String = Nothing
        ''' <summary>
        ''' 支持的 Drop 编号，从高到低排序，不为 Nothing。
        ''' 例如：261（26.1.x）、180（1.18.x）。
        ''' </summary>
        Public ReadOnly Drops As List(Of Integer)
        ''' <summary>
        ''' Modrinth API 返回的原始版本列表。
        ''' 仅用于 Modrinth 工程的二次筛选，不会被缓存，不会被预处理。
        ''' 若非从 Modrinth 获取则为 Nothing。
        ''' </summary>
        Public ReadOnly UnsafeGameVersions As List(Of String)

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
                    If Types.HasFlag(CompType.Mod) OrElse Types.HasFlag(CompType.DataPack) Then
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
                Types = Data("Types").ToObject(Of Integer)
                Slug = Data("Slug")
                Id = Data("Id")
                If Data.ContainsKey("CurseForgeFileIds") Then CurseForgeFileIds = CType(Data("CurseForgeFileIds"), JArray).Select(Function(t) t.ToObject(Of Integer)).ToList
                RawName = Data("RawName")
                Description = Data("Description")
                Website = Data("Website")
                LastUpdate = Data("LastUpdate")
                DownloadCount = Data("DownloadCount")
                If Data.ContainsKey("ModLoaders") Then
                    ModLoaders = CType(Data("ModLoaders"), JArray).Select(Function(t) CType(t.ToObject(Of Integer), CompModLoaderType)).ToList
                Else
                    ModLoaders = New List(Of CompModLoaderType)
                End If
                Tags = CType(Data("Tags"), JArray).Select(Function(t) t.ToString).ToList
                If Data.ContainsKey("LogoUrl") Then LogoUrl = Data("LogoUrl")
                If Data.ContainsKey("Drops") Then
                    Drops = CType(Data("Drops"), JArray).Select(Function(t) t.ToObject(Of Integer)).ToList
                Else
                    Drops = New List(Of Integer)
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
                    If LogoUrl = "" Then LogoUrl = Nothing
                    'Type
                    If Website.Contains("/mc-mods/") OrElse Website.Contains("/mod/") Then
                        Types = CompType.Mod
                    ElseIf Website.Contains("/modpacks/") Then
                        Types = CompType.ModPack
                    ElseIf Website.Contains("/resourcepacks/") Then
                        Types = CompType.ResourcePack
                    ElseIf Website.Contains("/texture-packs/") Then
                        Types = CompType.ResourcePack
                    ElseIf Website.Contains("/shaders/") Then
                        Types = CompType.Shader
                    Else
                        Types = CompType.DataPack
                    End If
                    'FileIndexes / VanillaMajorVersions / ModLoaders
                    ModLoaders = New List(Of CompModLoaderType)
                    Dim Files As New List(Of KeyValuePair(Of Integer, List(Of String))) 'FileId, GameVersions
                    For Each File In If(Data("latestFiles"), New JArray)
                        Dim NewFile As New CompFile(File, Types)
                        If Not NewFile.Available Then Continue For
                        ModLoaders.AddRange(NewFile.ModLoaders)
                        Dim GameVersions = File("gameVersions").ToObject(Of List(Of String))
                        If Not GameVersions.Any(Function(v) McVersion.IsFormatFit(v)) Then Continue For
                        Files.Add(New KeyValuePair(Of Integer, List(Of String))(File("id"), GameVersions))
                    Next
                    For Each File In If(Data("latestFilesIndexes"), New JArray) '这俩玩意儿包含的文件不一样，见 #3599
                        If Not McVersion.IsFormatFit(File("gameVersion")) Then Continue For
                        Files.Add(New KeyValuePair(Of Integer, List(Of String))(File("fileId"), {File("gameVersion").ToString}.ToList))
                    Next
                    CurseForgeFileIds = Files.Select(Function(f) f.Key).Distinct.ToList
                    Drops = Files.SelectMany(Function(f) f.Value).
                        Select(Function(v) McVersion.VersionToDrop(v)).Where(Function(v) v <> 209).Distinct.OrderByDescending(Function(v) v).ToList
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
                            Case 6814 : Tags.Add("性能优化")
                            Case 9026 : Tags.Add("创造模式")
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
                            Case 4474 : Tags.Add("科幻")
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
                    LastUpdate = If(Data("date_modified"), Data("updated"))
                    DownloadCount = Data("downloads")
                    LogoUrl = Data("icon_url")
                    If LogoUrl = "" Then LogoUrl = Nothing
                    Website = $"https://modrinth.com/{Data("project_type")}/{Slug}"
                    'GameVersions
                    '搜索结果的键为 versions，获取特定工程的键为 game_versions
                    UnsafeGameVersions = If(CType(If(Data("game_versions"), Data("versions")), JArray), New JArray).Select(Function(v) v.ToString).Distinct.ToList
                    Drops = UnsafeGameVersions.Select(Function(v) McVersion.VersionToDrop(v)).Where(Function(v) v <> 209).Distinct.OrderByDescending(Function(v) v).ToList
                    'Type
                    Select Case Data("project_type").ToString
                        Case "modpack" : Types = CompType.ModPack
                        Case "resourcepack" : Types = CompType.ResourcePack
                        Case "shader" : Types = CompType.Shader
                        Case Else : Types = CompType.Mod 'Modrinth 将数据包标为 Mod，但 categories 字段里有 datapack
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
                            Case "datapack" : Types = CompType.DataPack
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
                            Case "decoration" : If Types <> CompType.ResourcePack Then Tags.Add("装饰")
                            Case "mobs" : If Types <> CompType.ResourcePack Then Tags.Add("生物")
                            Case "equipment" : If Types <> CompType.ResourcePack Then Tags.Add("装备")
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
                    If Types = CompType.DataPack AndAlso ModLoaders.Any Then Types = CompType.DataPack Or CompType.Mod
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
            Json("Types") = CInt(Types)
            Json("Slug") = Slug
            Json("Id") = Id
            If CurseForgeFileIds IsNot Nothing Then Json("CurseForgeFileIds") = New JArray(CurseForgeFileIds)
            Json("RawName") = RawName
            Json("Description") = Description
            Json("Website") = Website
            Json("LastUpdate") = LastUpdate
            Json("DownloadCount") = DownloadCount
            If ModLoaders IsNot Nothing AndAlso ModLoaders.Any Then Json("ModLoaders") = New JArray(ModLoaders.Select(Function(m) CInt(m)))
            Json("Tags") = New JArray(Tags)
            If LogoUrl IsNot Nothing Then Json("LogoUrl") = LogoUrl
            If Drops.Any Then Json("Drops") = New JArray(Drops)
            Json("CacheTime") = Date.Now '用于检查缓存时间
            Return Json
        End Function
        ''' <summary>
        ''' 将当前工程信息实例化为控件。
        ''' </summary>
        Public Function ToCompItem(ShowMcVersionDesc As Boolean, ShowLoaderDesc As Boolean) As MyVirtualizingElement(Of MyCompItem)
            '获取版本描述
            Dim GameVersionDescription As String
            If Drops Is Nothing OrElse Not Drops.Any() Then
                GameVersionDescription = "仅快照版本" '#5412
            Else
                Dim Segments As New List(Of String)
                Dim IsOld As Boolean = False
                For i = 0 To Drops.Count - 1 '版本号一定为降序
                    '获取当前连续的版本号段
                    Dim StartDrop As Integer = Drops(i), EndDrop As Integer = Drops(i)
                    If StartDrop < 120 Then '如果支持新版本，则不显示 1.11-
                        If Segments.Any() AndAlso Not IsOld Then
                            Exit For
                        Else
                            IsOld = True
                        End If
                    End If
                    For ii = i + 1 To Drops.Count - 1
                        If AllDrops Is Nothing OrElse AllDrops.IndexOf(Drops(ii)) <> AllDrops.IndexOf(EndDrop) + 1 Then Exit For
                        EndDrop = Drops(ii)
                        i = ii
                    Next
                    '将版本号段转为描述文本
                    Dim StartName = McVersion.DropToVersion(StartDrop)
                    Dim EndName = McVersion.DropToVersion(EndDrop)
                    If StartDrop = EndDrop Then
                        Segments.Add(StartName)
                    ElseIf AllDrops IsNot Nothing AndAlso StartDrop >= AllDrops.First Then
                        If EndDrop < 120 Then
                            Segments.Clear()
                            Segments.Add("全版本")
                            Exit For
                        Else
                            Segments.Add(EndName & "+")
                        End If
                    ElseIf EndDrop < 120 Then
                        Segments.Add(StartName & "-")
                        Exit For
                    ElseIf AllDrops Is Nothing OrElse AllDrops.IndexOf(EndDrop) - AllDrops.IndexOf(StartDrop) = 1 Then
                        Segments.Add(StartName & ", " & EndName)
                    Else
                        Segments.Add(StartName & "~" & EndName)
                    End If
                Next
                GameVersionDescription = Segments.Join(", ")
            End If
            '获取 Mod 加载器描述
            Dim ModLoaderDescriptionFull As String, ModLoaderDescriptionPart As String
            Dim ModLoadersForDesc As New List(Of CompModLoaderType)(ModLoaders)
            If Settings.Get("ToolDownloadIgnoreQuilt") Then ModLoadersForDesc.Remove(CompModLoaderType.Quilt)
            Select Case ModLoadersForDesc.Count
                Case 0
                    If ModLoaders.IsSingle Then
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
                    Dim NewestDrop As Integer = If(Drops.Any, Drops.First, 9999)
                    If ModLoaders.Contains(CompModLoaderType.Forge) AndAlso
                       (NewestDrop < 140 OrElse ModLoaders.Contains(CompModLoaderType.Fabric)) AndAlso
                       (NewestDrop < 200 OrElse ModLoaders.Contains(CompModLoaderType.NeoForge)) AndAlso
                       (NewestDrop < 140 OrElse ModLoaders.Contains(CompModLoaderType.Quilt) OrElse Settings.Get("ToolDownloadIgnoreQuilt")) Then
                        ModLoaderDescriptionFull = "任意"
                        ModLoaderDescriptionPart = ""
                    Else
                        ModLoaderDescriptionFull = ModLoadersForDesc.Join(" / ")
                        ModLoaderDescriptionPart = ModLoadersForDesc.Join(" / ")
                    End If
            End Select
            '实例化 UI
            Return New MyVirtualizingElement(Of MyCompItem)(
            Function()
                Dim NewItem As New MyCompItem With {.Tag = Me}
                ApplyLogoToMyImage(NewItem.PathLogo)
                Dim Titles = GetControlTitle(True)
                NewItem.Title = Titles.Title
                If Titles.SubTitle = "" Then
                    CType(NewItem.LabTitleRaw.Parent, StackPanel).Children.Remove(NewItem.LabTitleRaw)
                Else
                    NewItem.SubTitle = Titles.SubTitle
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
                NewItem.LabTime.Text = GetTimeSpanString(LastUpdate - Date.Now, True)
                NewItem.LabDownload.Text =
                    If(DownloadCount > 100000000, Math.Round(DownloadCount / 100000000, 2) & " 亿",
                    If(DownloadCount > 100000, Math.Floor(DownloadCount / 10000) & " 万", DownloadCount))
                Return NewItem
            End Function) With {.Height = 64}
        End Function
        Public Sub ApplyLogoToMyImage(Img As MyImage)
            If String.IsNullOrEmpty(LogoUrl) Then
                Img.Source = PathImage & "Icons/NoIcon.png"
            Else
                Img.Source = LogoUrl
                Img.FallbackSource = DlSourceModGet(LogoUrl)
            End If
        End Sub
        Public Function GetControlTitle(HasModLoaderDescription As Boolean) As (Title As String, SubTitle As String)
            '检查下列代码时可以参考 #1567 的测试例
            Dim Title As String = RawName
            Dim SubtitleList As List(Of String)
            If TranslatedName = RawName Then
                '没有中文翻译
                '将所有名称分段
                Dim NameLists = TranslatedName.Split({" | ", " - ", "(", ")", "[", "]", "{", "}"}, StringSplitOptions.RemoveEmptyEntries).
                    Select(Function(s) s.Trim(" /\".ToCharArray)).Where(Function(w) Not String.IsNullOrEmpty(w))
                If NameLists.IsSingle Then GoTo NoSubtitle
                '查找其中的缩写、Forge/Fabric 等版本标记
                SubtitleList = New List(Of String)
                Dim NormalNameList = New List(Of String)
                For Each Name In NameLists
                    Dim LowerName As String = Name.Lower
                    If Name = Name.Upper AndAlso Name <> "FPS" AndAlso Name <> "HUD" Then
                        '缩写
                        SubtitleList.Add(Name)
                    ElseIf {"neoforge", "forge", "fabric", "quilt"}.Any(Function(l) LowerName.Contains(l)) AndAlso
                        Not RegexCheck(LowerName.Replace("neoforge", "").Replace("forge", "").Replace("fabric", "").Replace("quilt", ""), "[a-z]+") Then '去掉关键词后没有其他字母
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
                '检查时注意 Carpet（它没有中文译名，但有 Suffix）和 “汤姆存储 - 星的奇妙优化 (Tom's balabala)”
                Title = If(TranslatedName.Contains(" ("), TranslatedName.BeforeFirst(" ("), TranslatedName.BeforeLast(" - "))
                Dim Suffix As String = ""
                If TranslatedName.AfterLast(")").Contains(" - ") Then Suffix = TranslatedName.AfterLast(")").AfterLast(" - ")
                Dim EnglishName As String = TranslatedName
                If Suffix <> "" Then EnglishName = EnglishName.Replace(" - " & Suffix, "")
                EnglishName = EnglishName.Replace(Title, "").Trim("("c, ")"c, " "c)
                '中段的额外信息截取
                SubtitleList = EnglishName.Split({" | ", " - ", "(", ")", "[", "]", "{", "}"}, StringSplitOptions.RemoveEmptyEntries).
                        Select(Function(s) s.Trim(" /".ToCharArray)).Where(Function(w) Not String.IsNullOrEmpty(w)).ToList
                If SubtitleList.Count > 1 AndAlso
                   Not SubtitleList.Any(Function(s) s.Lower.Contains("forge") OrElse s.Lower.Contains("fabric") OrElse s.Lower.Contains("quilt")) AndAlso '不是标注 XX 版
                   Not (SubtitleList.Count = 2 AndAlso SubtitleList.Last.Upper = SubtitleList.Last) Then '不是缩写
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
                        Ex.Lower.Contains("neoforge") OrElse Ex.Lower.Contains("forge") OrElse Ex.Lower.Contains("fabric") OrElse Ex.Lower.Contains("quilt")
                    '是否显示 ModLoader 信息
                    If Not HasModLoaderDescription AndAlso IsModLoaderDescription Then Continue For
                    '去除 “Forge/Fabric” 这一无意义提示
                    If Ex.Length < 16 AndAlso Ex.Lower.Contains("fabric") AndAlso Ex.Lower.Contains("forge") Then Continue For
                    '将 “Forge” 等提示改为 “Forge 版”
                    If IsModLoaderDescription AndAlso Not Ex.Contains("版") AndAlso
                        Ex.Lower.Replace("neoforge", "").Replace("forge", "").Replace("fabric", "").Replace("quilt", "").Length <= 3 Then
                        Ex = Ex.Replace("Edition", "").Replace("edition", "").Trim.Capitalize & " 版"
                    End If
                    '将 “forge” 等词语的首字母大写
                    Ex = Ex.Replace("neoforge", "NeoForge").Replace("forge", "Forge").Replace("neo", "Neo").Replace("fabric", "Fabric").Replace("quilt", "Quilt")
                    Subtitle &= "  |  " & Ex.Trim
                Next
            Else
NoSubtitle:
                Subtitle = ""
            End If
            Return (Title, Subtitle)
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
                Return Result.ToString.Lower
            End Function
            '来自不同的网站
            If FromCurseForge = Project.FromCurseForge Then Return False
            'Mod 加载器一致
            If ModLoaders.Count <> Project.ModLoaders.Count OrElse ModLoaders.Except(Project.ModLoaders).Any() Then Return False
            '若不为光影，则要求 MC 版本一致
            If Types <> CompType.Shader AndAlso (Drops.Count <> Project.Drops.Count OrElse Drops.Except(Project.Drops).Any()) Then Return False
            '最近更新时间差距在一周以内
            If Math.Abs((LastUpdate - Project.LastUpdate).TotalDays) > 7 Then Return False
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
                If Tag.StartsWithF("/") OrElse Not Sources.HasFlag(CompSourceType.CurseForge) Then Storage.CurseForgeTotal = 0
                If Tag.EndsWithF("/") OrElse Not Sources.HasFlag(CompSourceType.Modrinth) Then Storage.ModrinthTotal = 0
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
        ''' 筛选资源标签。空字符串代表不限制。格式例如 "406/worldgen"，分别是 CurseForge 和 Modrinth 的 ID。
        ''' </summary>
        Public Tag As String = ""
        ''' <summary>
        ''' 筛选 Mod 加载器类别。
        ''' </summary>
        Public ModLoader As CompModLoaderType = CompModLoaderType.Any
        ''' <summary>
        ''' 筛选 MC 版本。
        ''' </summary>
        Public GameVersion As String = Nothing
        ''' <summary>
        ''' 搜索的初始内容。
        ''' </summary>
        Public SearchText As String = Nothing
        ''' <summary>
        ''' 允许的来源。
        ''' </summary>
        Public Sources As CompSourceType = CompSourceType.Any
        Public Sub New(Type As CompType, Storage As CompProjectStorage, TargetResultCount As Integer)
            Me.Type = Type
            Me.Storage = Storage
            Me.TargetResultCount = TargetResultCount
        End Sub

        '构造请求

        ''' <summary>
        ''' 获取对应的 CurseForge API 请求链接。
        ''' </summary>
        Public Function GetCurseForgeAddress(SearchText As String, IgnoreModLoaderFilter As Boolean) As String
            If Tag.StartsWithF("/") Then Storage.CurseForgeTotal = 0
            '应用筛选参数
            Dim Address As String = $"https://api.curseforge.com/v1/mods/search?gameId=432&sortField=2&sortOrder=desc&pageSize={COMP_PAGE_SIZE}"
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
            If Tag <> "" Then Address += "&categoryId=" & Tag.BeforeFirst("/")
            If ModLoader <> CompModLoaderType.Any AndAlso Not IgnoreModLoaderFilter Then Address += "&modLoaderType=" & CType(ModLoader, Integer)
            If Not String.IsNullOrEmpty(GameVersion) Then Address += "&gameVersion=" & GameVersion
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&searchFilter=" & Net.WebUtility.UrlEncode(SearchText)
            If Storage.CurseForgeOffset > 0 Then Address += "&index=" & Storage.CurseForgeOffset
            Return Address
        End Function
        ''' <summary>
        ''' 获取对应的 Modrinth API 请求链接。
        ''' </summary>
        Public Function GetModrinthAddress(SearchText As String, IgnoreModLoaderFilter As Boolean) As String
            If Tag.EndsWithF("/") Then Storage.ModrinthTotal = 0
            '应用筛选参数
            Dim Address As String = $"https://api.modrinth.com/v2/search?limit={COMP_PAGE_SIZE}&index=relevance"
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&query=" & Net.WebUtility.UrlEncode(SearchText)
            If Storage.ModrinthOffset > 0 Then Address += "&offset=" & Storage.ModrinthOffset
            'facets=[["categories:'game-mechanics'"],["categories:'forge'"],["versions:1.19.3"],["project_type:mod"]]
            Dim Facets As New List(Of String)
            Facets.Add($"[""project_type:{GetStringFromEnum(Type).Lower}""]")
            If Not String.IsNullOrEmpty(Tag) Then Facets.Add($"[""categories:'{Tag.AfterLast("/")}'""]")
            If ModLoader <> CompModLoaderType.Any AndAlso Not IgnoreModLoaderFilter Then Facets.Add($"[""categories:'{GetStringFromEnum(ModLoader).Lower}'""]")
            If Not String.IsNullOrEmpty(GameVersion) Then Facets.Add($"[""versions:'{GameVersion}'""]")
            Address += "&facets=[" & String.Join(",", Facets) & "]"
            Return Address
        End Function

        '相同判断
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim request = TryCast(obj, CompProjectRequest)
            Return request IsNot Nothing AndAlso
                Type = request.Type AndAlso TargetResultCount = request.TargetResultCount AndAlso
                Tag = request.Tag AndAlso ModLoader = request.ModLoader AndAlso Sources = request.Sources AndAlso
                GameVersion = request.GameVersion AndAlso SearchText = request.SearchText
        End Function
        Public Shared Operator =(left As CompProjectRequest, right As CompProjectRequest) As Boolean
            Return EqualityComparer(Of CompProjectRequest).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As CompProjectRequest, right As CompProjectRequest) As Boolean
            Return Not left = right
        End Operator
        Public Overrides Function GetHashCode() As Integer
            Return (Type, Tag, ModLoader, GameVersion, SearchText, Sources).GetHashCode()
        End Function

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

    Private Const COMP_PAGE_SIZE = 40
    Private Const MSG_NO_CHINESE_SEARCH_RESULT As String = "无搜索结果，请尝试搜索其英文名称"
    ''' <summary>
    ''' 已知工程信息的缓存。
    ''' </summary>
    Public CompProjectCache As New Dictionary(Of String, CompProject)
    ''' <summary>
    ''' 根据搜索请求获取一系列的工程列表。需要基于加载器运行。
    ''' </summary>
    Public Sub CompProjectsGet(Task As LoaderTask(Of CompProjectRequest, Integer))
        Dim Request As CompProjectRequest = Task.Input
        Dim Storage = Request.Storage '避免其他线程对 Request.Storage 重新进行了赋值

#Region "前置检查"

        If Storage.Results.Count >= Request.TargetResultCount Then
            Log($"[Comp] 已有 {Storage.Results.Count} 个结果，多于所需的 {Request.TargetResultCount} 个结果，结束处理")
            Return
        ElseIf Not Request.CanContinue Then
            If Not Storage.Results.Any() Then
                Throw New Exception("没有符合条件的结果")
            Else
                Log($"[Comp] 已有 {Storage.Results.Count} 个结果，少于所需的 {Request.TargetResultCount} 个结果，但无法继续获取，结束处理")
                Return
            End If
        End If

        '拒绝 1.13- Quilt（这个版本根本没有 Quilt）
        If Request.ModLoader = CompModLoaderType.Quilt AndAlso CompareVersion(If(Request.GameVersion, "1.15"), "1.14") = -1 Then
            Throw New Exception("Quilt 不支持 Minecraft " & Request.GameVersion)
        End If

#End Region

#Region "中文搜索"

        Dim RawSearchText As String = If(Request.SearchText, "").Trim
        RawSearchText = RawSearchText.Lower
        Log("[Comp] 工程列表搜索原始文本：" & RawSearchText)

        Dim IsChineseSearch As Boolean =
            RegexCheck(RawSearchText, "[\u4e00-\u9fbb]") AndAlso Not String.IsNullOrEmpty(RawSearchText) AndAlso
            (Request.Type = CompType.Mod OrElse Request.Type = CompType.DataPack) '目前仅对 Mod 和数据包进行中文搜索，注意整合包的名称可能已经有中文了
        Dim CurseForgeAltSearchText As String = Nothing, ModrinthAltSearchText As String = Nothing, ModrinthSlugs As New List(Of String) '从中文转为英文的替代搜索内容
        If IsChineseSearch Then
            '帮助方法：从搜索项提取可能的英文单词
            Dim ExtractWords =
            Function(Result As SearchEntry(Of CompDatabaseEntry), Source As CompSourceType) As IEnumerable(Of String)
                '从各个可能的来源提取候选
                Dim Candidates As New List(Of String)
                If Result.Item.CurseForgeSlug IsNot Nothing AndAlso Source = CompSourceType.CurseForge Then
                    Candidates.Add(Result.Item.CurseForgeSlug.Replace("-", " ").Replace("/", " "))
                End If
                If Result.Item.ModrinthSlug IsNot Nothing AndAlso Source = CompSourceType.Modrinth Then
                    Candidates.Add(Result.Item.ModrinthSlug.Replace("-", " ").Replace("/", " "))
                End If
                Candidates.Add(Result.Item.ChineseName.AfterLast(" (").TrimEnd(") ").BeforeFirst(" - ").
                                    Replace("-", " ").Replace("/", " ").Replace(":", " ").Replace("(", " ").Replace(")", ""))
                '分词、清洗、去重
                Candidates = Candidates.
                    SelectMany(Function(c) c.Split(" ")).
                    Select(Function(w) w.TrimStart("{[(").TrimEnd("}])").Lower).
                    Where(
                    Function(w)
                        If w.Length <= 1 Then Return False '单字或空白
                        If {"the", "of", "mod", "and", "forge", "fabric", "for", "quilt", "neoforge"}.Contains(w) Then Return False '常见词
                        If Val(w) > 0 Then Return False '数字
                        Return True
                    End Function).Distinct.ToList
                '如果一个词可以由其他词拼成，则去掉这个词（例如将 ender io enderio 的 enderio 剔除，只保留 ender io）
                Dim CanForm As Func(Of String, Boolean) = Nothing
                CanForm = Function(s) Candidates.Contains(s) OrElse Candidates.Any(Function(c) s.StartsWith(c) AndAlso CanForm(s.Substring(c.Length)))
                Candidates = Candidates.Where(Function(w) Not Candidates.Any(Function(c) c.Length < w.Length AndAlso w.StartsWith(c) AndAlso CanForm(w.Substring(c.Length)))).ToList()
                Return Candidates
            End Function
            'CurseForge
            If Request.Sources.HasFlag(CompSourceType.CurseForge) Then
                '数据库搜索
                Static CurseForgeSearchEntries As List(Of SearchEntry(Of CompDatabaseEntry)) =
                CompDatabase.Where(Function(Entry) Entry.CurseForgeSlug IsNot Nothing).Select(
                Function(Entry) New SearchEntry(Of CompDatabaseEntry) With {
                    .Item = Entry,
                    .SearchSource = New List(Of SearchSource) From {
                        New SearchSource(Entry.ChineseName.BeforeFirst(" (").Split({"/"c}, StringSplitOptions.RemoveEmptyEntries), 1), '部分 Mod 有别名
                        New SearchSource(Entry.ChineseName.AfterFirst(" (") & Entry.CurseForgeSlug, 0.5)}
                }).ToList
                Dim CurseForgeSearchResults = Search(CurseForgeSearchEntries, RawSearchText, 100, 0.25)
                If CurseForgeSearchResults.Any Then
                    '选取目标（CurseForge 要求每个词都必须匹配上，所以只能选择一个 Mod 进行搜索）
                    Dim CurseForgeTarget =
                        If(CurseForgeSearchResults.First.AbsoluteRight,
                            CurseForgeSearchResults.Where(Function(s) s.AbsoluteRight).ToList, '优先使用所有完全匹配的
                            CurseForgeSearchResults.MaxByAll(Function(s) s.Similarity)). '其次使用所有相似度最高的
                        MaxBy(Function(s) s.Item.Popularity) '然后从中选择最受欢迎的那一个
                    '后处理
                    CurseForgeAltSearchText = ExtractWords(CurseForgeTarget, CompSourceType.CurseForge).Join(" ")
                    Log("[Comp] 中文搜索关键词（CurseForge）：" & CurseForgeAltSearchText, LogLevel.Debug)
                End If
            End If
            'Modrinth
            If Request.Sources.HasFlag(CompSourceType.Modrinth) Then
                '数据库搜索
                Static ModrinthSearchEntries As List(Of SearchEntry(Of CompDatabaseEntry)) =
                CompDatabase.Where(Function(Entry) Entry.ModrinthSlug IsNot Nothing).Select(
                Function(Entry) New SearchEntry(Of CompDatabaseEntry) With {
                    .Item = Entry,
                    .SearchSource = New List(Of SearchSource) From {
                        New SearchSource(Entry.ChineseName.BeforeFirst(" (").Split({"/"c}, StringSplitOptions.RemoveEmptyEntries), 1), '部分 Mod 有别名
                        New SearchSource(Entry.ChineseName.AfterFirst(" (") & Entry.ModrinthSlug, 0.5)}
                }).ToList
                Dim ModrinthSearchResults = Search(ModrinthSearchEntries, RawSearchText, 100, 0.25)
                If ModrinthSearchResults.Any Then
                    '分词
                    Dim WordWeights As New Dictionary(Of String, Double) '各个单词及其出现的权重
                    For Each Result In ModrinthSearchResults
                        For Each Word In ExtractWords(Result, CompSourceType.Modrinth)
                            If Not WordWeights.ContainsKey(Word) Then WordWeights.Add(Word, 0)
                            Dim Similarity = If(Result.SearchSource.Any(Function(s) s.Aliases.Contains(RawSearchText)), 1000, Result.Similarity) '完全匹配为 1000
                            WordWeights(Word) += Similarity * Result.Item.Popularity '权重 += 相似度 * 受欢迎程度
                        Next
                    Next
                    ModrinthAltSearchText = WordWeights.MaxBy(Function(w) w.Value).Key
                    Log("[Comp] 中文搜索关键词（Modrinth）：" & ModrinthAltSearchText, LogLevel.Debug)
                    '直接请求工程
                    ModrinthSlugs = ModrinthSearchResults.Take(100).Select(Function(r) r.Item.ModrinthSlug).ToList
                End If
            End If
            '结束
            If String.IsNullOrEmpty(CurseForgeAltSearchText) AndAlso String.IsNullOrEmpty(ModrinthAltSearchText) AndAlso Not ModrinthSlugs.Any Then
                Throw New Exception(MSG_NO_CHINESE_SEARCH_RESULT)
            End If
        End If
        Task.Progress = 0.05

#End Region

        Dim RealResults As New List(Of CompProject)
NextPage:
        Dim RawResults As New SafeList(Of CompProject)

#Region "从 CurseForge 和 Modrinth 获取结果列表，存储于 RawResults"

        Dim WorkThreads As New List(Of Thread)
        Dim Errors As New SafeList(Of (Ex As Exception, Source As CompSourceType))

        '在 1.14-，部分老 Mod 没有设置支持的加载器，因此添加 Forge 筛选就会出现遗漏
        '所以，在发起请求时不筛选加载器，然后在返回的结果中自行筛除不是 Forge 的 Mod
        Dim IgnoreModLoaderFilter = Request.ModLoader = CompModLoaderType.Forge AndAlso McVersion.VersionToDrop(Request.GameVersion) < 140
        Try

            'CurseForge 搜索
            If Request.Sources.HasFlag(CompSourceType.CurseForge) AndAlso
               Not (Storage.CurseForgeTotal > -1 AndAlso Storage.CurseForgeTotal <= Storage.CurseForgeOffset) AndAlso '剩余的未显示的搜索结果不足
               (Not IsChineseSearch OrElse (IsChineseSearch AndAlso Not String.IsNullOrEmpty(CurseForgeAltSearchText))) Then '如果是中文搜索，就只在有对应搜索关键词的时候才继续
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        '获取工程列表
                        Dim CurseForgeUrl As String = Request.GetCurseForgeAddress(If(CurseForgeAltSearchText, RawSearchText), IgnoreModLoaderFilter)
                        Log("[Comp] 开始 CurseForge 搜索：" & CurseForgeUrl)
                        Dim RequestResult As JObject = DlModRequest(CurseForgeUrl)
                        Dim ProjectList As New List(Of CompProject)
                        For Each JsonEntry As JObject In RequestResult("data")
                            Dim Project As New CompProject(JsonEntry)
                            If Request.Type = CompType.ResourcePack AndAlso Project.Tags.Contains("数据包") Then Continue For 'CurseForge 将一些数据包分类成了资源包
                            ProjectList.Add(Project)
                        Next
                        '更新结果
                        ProjectList.ForEach(Sub(p) RawResults.Add(p))
                        Storage.CurseForgeOffset += RequestResult("data").Count
                        Storage.CurseForgeTotal = RequestResult("pagination")("totalCount").ToObject(Of Integer)
                        Log($"[Comp] 从 CurseForge 搜索到了 {ProjectList.Count} 个工程（总计已获取 {Storage.CurseForgeOffset} 个，共 {Storage.CurseForgeTotal} 个）")
                    Catch ex As Exception
                        Log(ex, "CurseForge 搜索失败")
                        Storage.CurseForgeTotal = -1
                        Errors.Add((ex, CompSourceType.CurseForge))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 '可能重复加载多页，所以不能直接给够
                End Sub, "CurseForge Search"))
            End If

            'Modrinth 搜索
            If Request.Sources.HasFlag(CompSourceType.Modrinth) AndAlso
               Not (Storage.ModrinthTotal > -1 AndAlso Storage.ModrinthTotal <= Storage.ModrinthOffset) AndAlso '剩余的未显示的搜索结果不足
               (Not IsChineseSearch OrElse (IsChineseSearch AndAlso Not String.IsNullOrEmpty(ModrinthAltSearchText))) Then '如果是中文搜索，就只在有对应搜索关键词的时候才继续
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        Dim ModrinthUrl As String = Request.GetModrinthAddress(If(ModrinthAltSearchText, RawSearchText), IgnoreModLoaderFilter)
                        Log("[Comp] 开始 Modrinth 搜索：" & ModrinthUrl)
                        Dim RequestResult As JObject = DlModRequest(ModrinthUrl)
                        Dim ProjectList As New List(Of CompProject)
                        For Each JsonEntry As JObject In RequestResult("hits")
                            ProjectList.Add(New CompProject(JsonEntry))
                        Next
                        '更新结果
                        ProjectList.ForEach(Sub(p) RawResults.Add(p))
                        Storage.ModrinthOffset += RequestResult("hits").Count
                        Storage.ModrinthTotal = RequestResult("total_hits").ToObject(Of Integer)
                        Log($"[Comp] 从 Modrinth 搜索到了 {ProjectList.Count} 个工程（总计已获取 {Storage.ModrinthOffset} 个，共 {Storage.ModrinthTotal} 个）")
                    Catch ex As Exception
                        Log(ex, "Modrinth 搜索失败")
                        Storage.ModrinthTotal = -1
                        Errors.Add((ex, CompSourceType.Modrinth))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 '可能重复加载多页，所以不能直接给够
                End Sub, "Modrinth Search"))
            End If

            'Modrinth 直接获取工程
            If Request.Sources.HasFlag(CompSourceType.Modrinth) AndAlso
               Not (Storage.ModrinthTotal > -1 AndAlso Storage.ModrinthTotal <= Storage.ModrinthOffset) AndAlso '剩余的未显示的搜索结果不足
               ModrinthSlugs.Any Then '有直接获取的 Slug
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        Dim ModrinthUrl As String = $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthSlugs.Join(""",""")}""]"
                        Log("[Comp] 开始 Modrinth 直接获取：" & ModrinthUrl)
                        Dim ProjectList As New List(Of CompProject)
                        For Each JsonEntry As JObject In DlModRequest(ModrinthUrl)
                            Dim Project As New CompProject(JsonEntry)
                            '应用筛选
                            If Request.Type <> CompType.Any AndAlso Not Project.Types.HasFlag(Request.Type) Then Continue For
                            If Not String.IsNullOrEmpty(Request.Tag) AndAlso
                                Not JsonEntry("categories").Any(Function(c) c.ToString = Request.Tag.AfterLast("/")) Then Continue For 'Project.Tags 已经转换成中文了，只能从 json 判
                            If Request.ModLoader <> CompModLoaderType.Any AndAlso Not IgnoreModLoaderFilter AndAlso
                                Not Project.ModLoaders.Any(Function(m) m = Request.ModLoader) Then Continue For
                            If Not String.IsNullOrEmpty(Request.GameVersion) AndAlso
                                Not Project.UnsafeGameVersions.Any(Function(d) d = Request.GameVersion) Then Continue For
                            ProjectList.Add(Project)
                        Next
                        '更新结果
                        ProjectList.ForEach(Sub(p) RawResults.Add(p))
                        Log($"[Comp] 从 Modrinth 直接获取到了 {ProjectList.Count} 个工程")
                        ModrinthSlugs.Clear() '防止重试/加载下一页时重复获取
                    Catch ex As Exception
                        Log(ex, "Modrinth 直接获取失败")
                        Errors.Add((ex, CompSourceType.Modrinth))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 '可能重复加载多页，所以不能直接给够
                End Sub, "Modrinth Get"))
            End If

            '等待线程结束
            For Each Thread In WorkThreads
                Thread.Join()
                If Task.IsInterrupted Then Return '会自动触发 Finally 以清理线程
            Next

            '筛除不是 Forge 的 Mod
            If IgnoreModLoaderFilter Then
                RawResults = RawResults.Where(Function(p) Not p.ModLoaders.Any() OrElse p.ModLoaders.Contains(CompModLoaderType.Forge)).ToList
            End If

            '确保存在结果
            Storage.ErrorMessage = Nothing
            If Not RawResults.Any() Then
                If Errors.Any() Then
                    Throw Errors.First.Ex
                Else
                    If IsChineseSearch AndAlso Not (Request.Type = CompType.Mod OrElse Request.Type = CompType.DataPack) Then
                        Throw New Exception(MSG_NO_CHINESE_SEARCH_RESULT)
                    ElseIf Request.Sources = CompSourceType.CurseForge AndAlso Request.Tag.StartsWithF("/") Then
                        Throw New Exception("CurseForge 不兼容所选的类型")
                    ElseIf Request.Sources = CompSourceType.Modrinth AndAlso Request.Tag.EndsWithF("/") Then
                        Throw New Exception("Modrinth 不兼容所选的类型")
                    Else
                        Throw New Exception("没有搜索结果")
                    End If
                End If
            ElseIf Errors.Any() Then
                '有结果但是有错误
                If Errors.Any(Function(e) e.Source = CompSourceType.CurseForge) Then
                    Storage.ErrorMessage = $"无法连接到 CurseForge，所以目前仅显示了来自 Modrinth 的内容，搜索结果可能不全。{vbCrLf}请稍后再试，或使用 VPN 改善网络环境。"
                Else
                    Storage.ErrorMessage = $"无法连接到 Modrinth，所以目前仅显示了来自 CurseForge 的内容，搜索结果可能不全。{vbCrLf}请稍后再试，或使用 VPN 改善网络环境。"
                End If
            End If

        Finally
            For Each Thread In WorkThreads
                If Thread.IsAlive Then Thread.Interrupt()
            Next
        End Try

#End Region

#Region "提取非重复项，存储于 RealResults"

        '将 CurseForge 排在 Modrinth 的前面，避免加载结束顺序不同导致排名不同
        RawResults = RawResults.Where(Function(x) x.FromCurseForge).Concat(RawResults.Where(Function(x) Not x.FromCurseForge)).ToList
        '去重
        RawResults = RawResults.Distinct(Function(a, b) a.IsLike(b))
        '已有内容去重
        RawResults = RawResults.Where(Function(r) Not RealResults.Any(Function(b) r.IsLike(b)) AndAlso
                                                  Not Storage.Results.Any(Function(b) r.IsLike(b))).ToList
        '加入列表
        RealResults.AddRange(RawResults)
        Log($"[Comp] 去重、筛选后累计新增结果 {RealResults.Count} 个（目前已有结果 {Storage.Results.Count} 个）")

#End Region

#Region "检查结果数量，如果不足且可继续，会继续加载下一页"

        If RealResults.Count + Storage.Results.Count < Request.TargetResultCount Then
            Log($"[Comp] 总结果数需求最少 {Request.TargetResultCount} 个，仅获得了 {RealResults.Count + Storage.Results.Count} 个")
            If Request.CanContinue AndAlso Not Errors.Any Then '如果有某个源失败则不再重试，这时候重试可能导致无限循环
                Log("[Comp] 将继续尝试加载下一页")
                GoTo NextPage
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
                    Return If(Project.FromCurseForge, 1, 5)
                Case CompType.DataPack
                    Return If(Project.FromCurseForge, 10, 1)
                Case CompType.ResourcePack, CompType.Shader
                    Return If(Project.FromCurseForge, 1, 4)
                Case Else
                    Return 1
            End Select
        End Function
        If String.IsNullOrEmpty(RawSearchText) Then
            '如果没有搜索文本，按下载量将结果排序
            For Each Result As CompProject In RealResults
                Scores(Result) = Result.DownloadCount * GetDownloadCountMult(Result)
            Next
        Else
            '如果有搜索文本，按关联度将结果排序
            '排序分 = 搜索相对相似度 (1) + 下载量权重 (对数，10 亿时为 1) + 有中文名 (0.2)
            Dim SearchEntries As New List(Of SearchEntry(Of CompProject))
            For Each Result As CompProject In RealResults
                Scores(Result) = If(Result.WikiId > 0, 0.2, 0) +
                           Math.Log10(Math.Max(Result.DownloadCount, 1) * GetDownloadCountMult(Result)) / 9
                SearchEntries.Add(New SearchEntry(Of CompProject) With {.Item = Result, .SearchSource = New List(Of SearchSource) From {
                    New SearchSource(If(IsChineseSearch, Result.TranslatedName, Result.RawName).Split({"/"c}, StringSplitOptions.RemoveEmptyEntries), 1),
                    New SearchSource(Result.Description, 0.05)}})
            Next
            Dim SearchResult = Search(SearchEntries, RawSearchText, 10000, -1)
            For Each OneResult In SearchResult
                Scores(OneResult.Item) +=
                    If(OneResult.AbsoluteRight, 10, OneResult.Similarity) /
                    If(SearchResult.First.AbsoluteRight, 10, SearchResult.First.Similarity) '最高 1 分的相似度分
            Next
        End If
        '根据排序分得出结果并添加
        If Task.IsInterrupted Then Throw New ThreadInterruptedException '#8246
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
        ''' Mod 版本号。
        ''' 不一定是标准格式。CurseForge 上默认为 Nothing。
        ''' </summary>
        Public Version As String
        ''' <summary>
        ''' 支持的 Mod 加载器列表。可能为空。
        ''' </summary>
        Public ReadOnly ModLoaders As List(Of CompModLoaderType)
        ''' <summary>
        ''' 支持的游戏版本列表。类型包括："26.1.5"，"26.1"，"26.1 预览版"，"1.18.5"，"1.18"，"1.18 预览版"，"21w15a"，"未知版本"。
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
        ''' 文件大小。不可用时为 -1。
        ''' </summary>
        Public ReadOnly Size As Integer = -1
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
            Return New NetFile(DownloadUrls, LocalAddress & If(LocalAddress.EndsWithF("\"), FileName, ""), New FileChecker(Hash:=Hash, ActualSize:=Size), SimulateBrowserHeaders:=True)
        End Function

        '实例化

        ''' <summary>
        ''' 从 json 中初始化实例。若出错会抛出异常。
        ''' </summary>
        Public Sub New(Data As JObject, DefaultType As CompType)
            Type = DefaultType
            If Data.ContainsKey("FromCurseForge") Then
#Region "CompJson"
                FromCurseForge = Data("FromCurseForge").ToObject(Of Boolean)
                Id = Data("Id").ToString
                DisplayName = Data("DisplayName").ToString
                If Data.ContainsKey("Version") Then Version = Data("Version").ToString
                ReleaseDate = Data("ReleaseDate").ToObject(Of Date)
                DownloadCount = Data("DownloadCount").ToObject(Of Integer)
                Status = CType(Data("Status").ToObject(Of Integer), CompFileStatus)
                If Data.ContainsKey("FileName") Then FileName = Data("FileName").ToString
                If Data.ContainsKey("DownloadUrls") Then DownloadUrls = Data("DownloadUrls").ToObject(Of List(Of String))
                If Data.ContainsKey("ModLoaders") Then ModLoaders = Data("ModLoaders").ToObject(Of List(Of CompModLoaderType))
                If Data.ContainsKey("Size") Then Size = Data("Size").ToObject(Of Integer)
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
                    Version = Nothing
                    ReleaseDate = Data("fileDate")
                    Status = CType(Data("releaseType").ToObject(Of Integer), CompFileStatus)
                    DownloadCount = Data("downloadCount")
                    FileName = Data("fileName")
                    Size = Data("fileLength")
                    Hash = CType(Data("hashes"), JArray).ToList.FirstOrDefault(Function(s) s("algo").ToObject(Of Integer) = 1)?("value")
                    If Hash Is Nothing Then Hash = CType(Data("hashes"), JArray).ToList.FirstOrDefault(Function(s) s("algo").ToObject(Of Integer) = 2)?("value")
                    'DownloadAddress
                    Dim Url = Data("downloadUrl").ToString
                    If Url = "" Then Url = $"https://edge.forgecdn.net/files/{CInt(Id.ToString.Substring(0, 4))}/{CInt(Id.ToString.Substring(4))}/{FileName}"
                    DownloadUrls = HandleCurseForgeDownloadUrls(Url.Replace(FileName, WebUtility.UrlEncode(FileName))) '对脑残 CurseForge 的下载地址进行多种修正
                    DownloadUrls.Add(DlSourceModGet(Url)) '添加镜像源；注意 MCIM 源不支持 URL 编码后的文件名，必须传入 URL 编码前的文件名
                    DownloadUrls = DownloadUrls.Distinct.ToList '最终去重
                    'Dependencies
                    If Data.ContainsKey("dependencies") Then
                        RawDependencies = Data("dependencies").
                            Where(Function(d) d("relationType").ToObject(Of Integer) = 3 AndAlso '种类为依赖
                                              d("modId").ToObject(Of Integer) <> 306612 AndAlso d("modId").ToObject(Of Integer) <> 634179). '排除 Fabric API 和 Quilt API
                            Select(Function(d) d("modId").ToString).ToList
                    End If
                    'GameVersions
                    Dim RawVersions As List(Of String) = Data("gameVersions").Select(Function(t) t.ToString.Trim.Lower).ToList
                    GameVersions = RawVersions.
                        Where(Function(v) McVersion.IsFormatFit(v)).
                        Select(Function(v) v.Replace("-snapshot", " 预览版")).
                        Distinct.ToList
                    If GameVersions.IsSingle Then
                        GameVersions = GameVersions.ToList
                    ElseIf GameVersions.Count > 1 Then
                        GameVersions = GameVersions.SortByComparison(AddressOf CompareVersionGE).ToList
                        If Type = CompType.ModPack Then GameVersions = New List(Of String) From {GameVersions(0)} '整合包理应只 “支持” 一个版本
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
                    Version = Data("version_number")
                    ReleaseDate = Data("date_published")
                    Status = If(Data("version_type").ToString = "release", CompFileStatus.Release, If(Data("version_type").ToString = "beta", CompFileStatus.Beta, CompFileStatus.Alpha))
                    DownloadCount = Data("downloads")
                    If CType(Data("files"), JArray).Any() Then '可能为空
                        Dim File As JToken = Data("files")(0)
                        FileName = File("filename")
                        DownloadUrls = New List(Of String) From {File("url"), DlSourceModGet(File("url"))}.Distinct.ToList '同时添加了镜像源
                        Size = File("size")
                        Hash = File("hashes")("sha1")
                    End If
                    'Loaders
                    '结果可能混杂着 Mod、数据包和服务端插件
                    Dim RawLoaders As List(Of String) = Data("loaders").Select(Function(v) v.ToString).ToList
                    ModLoaders = New List(Of CompModLoaderType)
                    If Type.HasFlag(CompType.Mod) OrElse Type.HasFlag(CompType.DataPack) Then
                        If RawLoaders.Intersect({"bukkit", "folia", "paper", "purpur", "spigot"}).Any() Then Type = CompType.Plugin 'Veinminer Enchantment 同时支持服务端与 Fabric
                        If RawLoaders.Contains("datapack") Then Type = CompType.DataPack
                        If RawLoaders.Contains("forge") Then ModLoaders.Add(CompModLoaderType.Forge) : Type = CompType.Mod
                        If RawLoaders.Contains("neoforge") Then ModLoaders.Add(CompModLoaderType.NeoForge) : Type = CompType.Mod
                        If RawLoaders.Contains("fabric") Then ModLoaders.Add(CompModLoaderType.Fabric) : Type = CompType.Mod
                        If RawLoaders.Contains("quilt") Then ModLoaders.Add(CompModLoaderType.Quilt) : Type = CompType.Mod
                    Else
                        '使用传入的类别，不作修改（#8377）
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
                    Dim RawVersions As List(Of String) = Data("game_versions").Select(Function(t) t.ToString.Trim.Lower).ToList
                    GameVersions = RawVersions.Where(Function(v) v.Contains(".")).
                                               Select(Function(v) If(v.Contains("-"), v.BeforeFirst("-") & " 预览版", If(v.StartsWithF("b1."), "远古版本", v))).
                                               Distinct.ToList
                    If GameVersions.IsSingle Then
                        '无需处理
                    ElseIf GameVersions.Count > 1 Then
                        GameVersions = GameVersions.SortByComparison(AddressOf CompareVersionGE).ToList
                        If Type = CompType.ModPack Then GameVersions = New List(Of String) From {GameVersions(0)} '整合包理应只 “支持” 一个版本
                    ElseIf RawVersions.Any(Function(v) RegexCheck(v, "[0-9]{2}w[0-9]{2}[a-z]")) Then
                        GameVersions = RawVersions.Where(Function(v) RegexCheck(v, "[0-9]{2}w[0-9]{2}[a-z]")).ToList
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
                Url.Replace("-service.overwolf.wtf", ".forgecdn.net"),
                Url.Replace("://media.", "://edge."),
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
            If Version IsNot Nothing Then Json.Add("Version", Version)
            Json.Add("DisplayName", DisplayName)
            Json.Add("ReleaseDate", ReleaseDate)
            Json.Add("DownloadCount", DownloadCount)
            Json.Add("ModLoaders", New JArray(ModLoaders.Select(Function(m) CInt(m))))
            Json.Add("GameVersions", New JArray(GameVersions))
            Json.Add("Status", CInt(Status))
            If FileName IsNot Nothing Then Json.Add("FileName", FileName)
            If DownloadUrls IsNot Nothing Then Json.Add("DownloadUrls", New JArray(DownloadUrls))
            If Hash IsNot Nothing Then Json.Add("Hash", Hash)
            If Size >= 0 Then Json.Add("Size", Size)
            Json.Add("RawDependencies", New JArray(RawDependencies))
            Json.Add("Dependencies", New JArray(Dependencies))
            Return Json
        End Function
        ''' <summary>
        ''' 将当前文件信息实例化为控件。
        ''' </summary>
        Public Function ToListItem(OnClick As MyListItem.ClickEventHandler, Optional OnSaveClick As MyIconButton.ClickEventHandler = Nothing,
                                   Optional BadDisplayName As Boolean = False) As MyVirtualizingElement(Of MyListItem)
            Return New MyVirtualizingElement(Of MyListItem)(
            Function()
                '获取描述信息
                Dim Title As String = If(BadDisplayName, FileName, DisplayName)
                Dim Info As New List(Of String)
                If Title <> FileName.BeforeLast(".") Then Info.Add(FileName.BeforeLast("."))
                If Dependencies.Any Then Info.Add(Dependencies.Count & " 项前置")
                If GameVersions.All(
                Function(VerName)
                    Return Not VerName.Contains(".") OrElse {"w", "snapshot", "rc", "pre", "experimental", "-"}.Any(Function(s) VerName.ContainsF(s, True))
                End Function) Then Info.Add($"游戏版本 {GameVersions.Join("、"c)}")
                If DownloadCount > 0 Then 'CurseForge 的下载次数经常错误地返回 0
                    Info.Add("下载 " & If(DownloadCount > 100000, Math.Round(DownloadCount / 10000) & " 万次", DownloadCount & " 次"))
                End If
                Info.Add("更新于 " & GetTimeSpanString(ReleaseDate - Date.Now, False))
                If Status <> CompFileStatus.Release Then Info.Add(StatusDescription)

                '建立控件
                Dim NewItem As New MyListItem With {
                    .Title = Title,
                    .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Me,
                    .Info = Info.Join("，"c)
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
                Return NewItem
            End Function) With {.Height = 42}
        End Function

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
            TargetProject = New CompProject(DlModRequest("https://api.curseforge.com/v1/mods/" & ProjectId)("data"))
        Else 'Modrinth
            TargetProject = New CompProject(DlModRequest("https://api.modrinth.com/v2/project/" & ProjectId))
        End If
        '获取工程对象的文件列表
        If Not CompFilesCache.ContainsKey(ProjectId) Then '有缓存也不能直接返回，这时候前置可能没获取（#5173）
            Log("[Comp] 开始获取文件列表：" & ProjectId)
            Dim ResultJsonArray As JArray = Nothing
            If FromCurseForge Then
                'CurseForge
                For RetryCount As Integer = 0 To 2
                    Dim ResultJson As JObject = DlModRequest($"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=" &
                        (10000 + RetryCount)) '每次重试多请求一个文件，以避免触发 CDN 缓存
                    'HMCL 一次性请求了 10000 个文件，虽然不知道会不会出问题但先这样吧……（#5522）
                    '之前只请求一部分文件的方法备份如下：
                    'If TargetProject.Type = CompType.Mod Then 'Mod 使用每个版本最新的文件
                    '    ResultJsonArray = DlModRequest("https://api.curseforge.com/v1/mods/files", HttpMethod.Post, "{""fileIds"": [" & Join(TargetProject.CurseForgeFileIds, ",") & "]}", "application/json")("data")
                    'Else '否则使用全部文件
                    '    ResultJsonArray = DlModRequest($"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=999")("data")
                    'End If
                    If ResultJson("pagination")("resultCount").ToObject(Of Integer) = ResultJson("pagination")("totalCount").ToObject(Of Integer) Then
                        ResultJsonArray = ResultJson("data")
                        Exit For
                    ElseIf RetryCount < 2 Then
                        Log($"[Comp] CurseForge 返回的文件列表存在缺失，即将进行第 {RetryCount + 1} 次重试", LogLevel.Debug) '#6224
                        Log($"[Comp] 返回的原始内容如下：{vbCrLf}{ResultJson}")
                    Else
                        Log($"[Comp] CurseForge 返回的文件列表存在缺失，返回的原始内容如下：{vbCrLf}{ResultJson}")
                        Throw New Exception("CurseForge 返回的文件列表存在缺失")
                    End If
                Next
            Else
                'Modrinth
                ResultJsonArray = DlModRequest($"https://api.modrinth.com/v2/project/{ProjectId}/version?include_changelog=false")
            End If
            CompFilesCache(ProjectId) = ResultJsonArray.Select(Function(a) New CompFile(a, TargetProject.Types)).
                Where(Function(a) a.Available).ToList.
                Distinct(Function(a, b) a.Id = b.Id) 'CurseForge 可能会重复返回相同项（#1330）
        End If
        '获取前置列表
        Dim Deps As List(Of String) = CompFilesCache(ProjectId).SelectMany(Function(f) f.RawDependencies).Distinct().ToList
        Dim UndoneDeps = Deps.Where(Function(f) Not CompProjectCache.ContainsKey(f)).ToList
        '获取前置工程信息
        If UndoneDeps.Any Then
            Log($"[Comp] {ProjectId} 文件列表中还需要获取信息的前置：{UndoneDeps.Join("，"c)}")
            Dim Projects As JArray
            If TargetProject.FromCurseForge Then
                Projects = DlModRequest("https://api.curseforge.com/v1/mods",
                    HttpMethod.Post, "{""modIds"": [" & UndoneDeps.Join(","c) & "]}", "application/json")("data")
            Else
                Projects = DlModRequest($"https://api.modrinth.com/v2/projects?ids=[""{UndoneDeps.Join(""",""")}""]")
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
