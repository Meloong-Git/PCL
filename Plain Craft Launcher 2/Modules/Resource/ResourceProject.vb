''' <summary>
''' 社区资源的项目信息。
''' </summary>
Public Class ResourceProject

    '源信息

    ''' <summary>
    ''' 该项目信息的所属平台。
    ''' </summary>
    Public ReadOnly Platform As ResourcePlatforms
    ''' <summary>
    ''' 工程中包含的文件种类。
    ''' 若为 Modrinth 工程，可能为 Mod 或 数据包。
    ''' </summary>
    Public ReadOnly Types As ResourceTypes
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
    Public ReadOnly Loaders As List(Of ModLoaderTypes)
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
    Private _WikiEntry As WikiEntry = Nothing
    ''' <summary>
    ''' 关联的数据库条目。若为 Nothing 则没有。
    ''' </summary>
    Private Property WikiEntry As WikiEntry
        Get
            If Not LoadedDatabase Then
                LoadedDatabase = True
                If Types.HasFlag(ResourceTypes.Mod) OrElse Types.HasFlag(ResourceTypes.DataPack) Then
                    _WikiEntry = WikiEntry.All.FirstOrDefault(Function(c) If(Platform = ResourcePlatforms.CurseForge, c.CurseForgeSlug, c.ModrinthSlug) = Slug)
                End If
            End If
            Return _WikiEntry
        End Get
        Set(value As WikiEntry)
            LoadedDatabase = True
            _WikiEntry = value
        End Set
    End Property
    ''' <summary>
    ''' MC 百科的页面 ID。若为 0 则没有。
    ''' </summary>
    Public ReadOnly Property WikiId As Integer
        Get
            Return If(WikiEntry Is Nothing, 0, WikiEntry.Id)
        End Get
    End Property
    ''' <summary>
    ''' 翻译后的中文名。若数据库没有则等同于 RawName。
    ''' </summary>
    Public ReadOnly Property TranslatedName As String
        Get
            Return If(WikiEntry Is Nothing OrElse WikiEntry.ChineseName = "", RawName, WikiEntry.ChineseName)
        End Get
    End Property

    '实例化

    ''' <summary>
    ''' 从工程 Json 中初始化实例。若出错会抛出异常。
    ''' </summary>
    Public Sub New(Data As JObject)
        If Data.ContainsKey("Tags") Then
#Region "缓存 JSON"
            Platform = Data("Platform").ToObject(Of Integer)
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
                Loaders = CType(Data("ModLoaders"), JArray).Select(Function(t) CType(t.ToObject(Of Integer), ModLoaderTypes)).ToList
            Else
                Loaders = New List(Of ModLoaderTypes)
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
            Platform = If(Data.ContainsKey("summary"), ResourcePlatforms.CurseForge, ResourcePlatforms.Modrinth)
            If Platform = ResourcePlatforms.CurseForge Then
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
                    Types = ResourceTypes.Mod
                ElseIf Website.Contains("/modpacks/") Then
                    Types = ResourceTypes.ModPack
                ElseIf Website.Contains("/resourcepacks/") Then
                    Types = ResourceTypes.ResourcePack
                ElseIf Website.Contains("/texture-packs/") Then
                    Types = ResourceTypes.ResourcePack
                ElseIf Website.Contains("/shaders/") Then
                    Types = ResourceTypes.Shader
                Else
                    Types = ResourceTypes.DataPack
                End If
                'FileIndexes / VanillaMajorVersions / ModLoaders
                Loaders = New List(Of ModLoaderTypes)
                Dim Files As New List(Of KeyValuePair(Of Integer, List(Of String))) 'FileId, GameVersions
                For Each File In If(Data("latestFiles"), New JArray)
                    Dim NewFile = ResourceVersion.FromPlatformJson(File, Types)
                    If Not NewFile.DownloadAvailable Then Continue For
                    Loaders.AddRange(NewFile.Loaders)
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
                Loaders = Loaders.Distinct.OrderBy(Of Integer)(Function(t) t).ToList
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
                    Case "modpack" : Types = ResourceTypes.ModPack
                    Case "resourcepack" : Types = ResourceTypes.ResourcePack
                    Case "shader" : Types = ResourceTypes.Shader
                    Case Else : Types = ResourceTypes.Mod 'Modrinth 将数据包标为 Mod，但 categories 字段里有 datapack
                End Select
                'Tags & ModLoaders
                Tags = New List(Of String)
                Loaders = New List(Of ModLoaderTypes)
                If Data.ContainsKey("loaders") Then
                    For Each Category In Data("loaders").Select(Function(t) t.ToString)
                        Select Case Category
                            Case "forge" : Loaders.Add(ModLoaderTypes.Forge)
                            Case "fabric" : Loaders.Add(ModLoaderTypes.Fabric)
                            Case "quilt" : Loaders.Add(ModLoaderTypes.Quilt)
                            Case "neoforge" : Loaders.Add(ModLoaderTypes.NeoForge)
                        End Select
                    Next
                End If
                For Each Category In Data("categories").Select(Function(t) t.ToString)
                    Select Case Category
                            '加载器
                        Case "forge" : Loaders.Add(ModLoaderTypes.Forge)
                        Case "fabric" : Loaders.Add(ModLoaderTypes.Fabric)
                        Case "quilt" : Loaders.Add(ModLoaderTypes.Quilt)
                        Case "neoforge" : Loaders.Add(ModLoaderTypes.NeoForge)
                        Case "datapack" : Types = ResourceTypes.DataPack
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
                        Case "decoration" : If Types <> ResourceTypes.ResourcePack Then Tags.Add("装饰")
                        Case "mobs" : If Types <> ResourceTypes.ResourcePack Then Tags.Add("生物")
                        Case "equipment" : If Types <> ResourceTypes.ResourcePack Then Tags.Add("装备")
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
                If Types = ResourceTypes.DataPack AndAlso Loaders.Any Then Types = ResourceTypes.DataPack Or ResourceTypes.Mod
#End Region
            End If
            If Not Tags.Any() Then Tags.Add("其他")
            Tags.Sort()
            Loaders.Sort()
        End If
        '保存缓存
        Cache(Id) = Me
    End Sub
    ''' <summary>
    ''' 从工程 ID 联网获取对应工程的信息。
    ''' </summary>
    Public Shared Function FromProjectId(ProjectId As String, Platform As ResourcePlatforms) As ResourceProject
        If Not Platform.Flags.IsSingle Then Throw New ArgumentException($"必须指定单一平台，当前值为 {Platform}")
        If Cache.ContainsKey(ProjectId) Then '存在缓存
            Return Cache(ProjectId)
        ElseIf Platform = ResourcePlatforms.CurseForge Then 'CurseForge
            Return New ResourceProject(DlModRequest("https://api.curseforge.com/v1/mods/" & ProjectId)("data"))
        Else 'Modrinth
            Return New ResourceProject(DlModRequest("https://api.modrinth.com/v2/project/" & ProjectId))
        End If
    End Function
    ''' <summary>
    ''' 从工程 ID 列表联网获取工程信息，结果不按原顺序排列。
    ''' </summary>
    Public Shared Iterator Function FromProjectIds(ProjectIds As IEnumerable(Of String), Platform As ResourcePlatforms) As IEnumerable(Of ResourceProject)
        If Not Platform.Flags.IsSingle Then Throw New ArgumentException($"必须指定单一平台，当前值为 {Platform}")
        '获取缓存
        Dim MissingProjectIds As New List(Of String)
        For Each ProjectId In ProjectIds
            If Cache.ContainsKey(ProjectId) Then
                Yield Cache(ProjectId)
            Else
                MissingProjectIds.Add(ProjectId)
            End If
        Next
        If Not MissingProjectIds.Any() Then Return
        '联网获取剩余的工程对象
        Dim Projects As JArray
        If Platform.HasFlag(ResourcePlatforms.CurseForge) Then
            Projects = DlModRequest("https://api.curseforge.com/v1/mods",
                HttpMethod.Post, "{""modIds"": [" & MissingProjectIds.Join(","c) & "]}", "application/json")("data")
        Else
            Projects = DlModRequest($"https://api.modrinth.com/v2/projects?ids=[""{MissingProjectIds.Join(""",""")}""]")
        End If
        For Each Project In Projects
            Yield New ResourceProject(Project)
        Next
    End Function

    ''' <summary>
    ''' 将当前实例转为可用于保存缓存的 Json。
    ''' </summary>
    Public Function ToJson() As JObject
        Dim Json As New JObject
        Json("Platform") = CInt(Platform)
        Json("Types") = CInt(Types)
        Json("Slug") = Slug
        Json("Id") = Id
        If CurseForgeFileIds IsNot Nothing Then Json("CurseForgeFileIds") = New JArray(CurseForgeFileIds)
        Json("RawName") = RawName
        Json("Description") = Description
        Json("Website") = Website
        Json("LastUpdate") = LastUpdate
        Json("DownloadCount") = DownloadCount
        If Loaders IsNot Nothing AndAlso Loaders.Any Then Json("ModLoaders") = New JArray(Loaders.Select(Function(m) CInt(m)))
        Json("Tags") = New JArray(Tags)
        If LogoUrl IsNot Nothing Then Json("LogoUrl") = LogoUrl
        If Drops.Any Then Json("Drops") = New JArray(Drops)
        Json("CacheTime") = Date.Now '用于检查缓存时间
        Return Json
    End Function

    ''' <summary>
    ''' 将当前工程信息实例化为控件。
    ''' </summary>
    Public Function ToResourceItem(ShowMcVersionDesc As Boolean, ShowLoaderDesc As Boolean) As MyVirtualizingElement(Of MyResourceItem)
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
        Dim ModLoadersForDesc As New List(Of ModLoaderTypes)(Loaders)
        If Settings.Get("ToolDownloadIgnoreQuilt") Then ModLoadersForDesc.Remove(ModLoaderTypes.Quilt)
        Select Case ModLoadersForDesc.Count
            Case 0
                If Loaders.IsSingle Then
                    ModLoaderDescriptionFull = "仅 " & Loaders.Single.ToString
                    ModLoaderDescriptionPart = Loaders.Single.ToString
                Else
                    ModLoaderDescriptionFull = "未知"
                    ModLoaderDescriptionPart = ""
                End If
            Case 1
                ModLoaderDescriptionFull = "仅 " & ModLoadersForDesc.Single.ToString
                ModLoaderDescriptionPart = ModLoadersForDesc.Single.ToString
            Case Else
                Dim NewestDrop As Integer = If(Drops.Any, Drops.First, 9999)
                If Loaders.Contains(ModLoaderTypes.Forge) AndAlso
                   (NewestDrop < 140 OrElse Loaders.Contains(ModLoaderTypes.Fabric)) AndAlso
                   (NewestDrop < 200 OrElse Loaders.Contains(ModLoaderTypes.NeoForge)) AndAlso
                   (NewestDrop < 140 OrElse Loaders.Contains(ModLoaderTypes.Quilt) OrElse Settings.Get("ToolDownloadIgnoreQuilt")) Then
                    ModLoaderDescriptionFull = "任意"
                    ModLoaderDescriptionPart = ""
                Else
                    ModLoaderDescriptionFull = ModLoadersForDesc.Join(" / ")
                    ModLoaderDescriptionPart = ModLoadersForDesc.Join(" / ")
                End If
        End Select
        '实例化 UI
        Return New MyVirtualizingElement(Of MyResourceItem)(
        Function()
            Dim NewItem As New MyResourceItem With {.Tag = Me}
            ApplyLogoToMyImage(NewItem.PathLogo)
            Dim Titles = GetControlTitle(True)
            NewItem.Title = Titles.Title
            If Titles.SubTitle = "" Then
                CType(NewItem.LabTitleRaw.Parent, StackPanel).Children.Remove(NewItem.LabTitleRaw)
            Else
                NewItem.SubTitle = Titles.SubTitle
            End If
            NewItem.Tags = Tags
            NewItem.Description = Description.ReplaceLineEndings("")
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
            NewItem.LabSource.Text = Platform.ToString
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

    ''' <summary>
    ''' 工程 ID 对应的工程实例的缓存。不会保存到文件。
    ''' </summary>
    Protected Friend Shared Cache As New Dictionary(Of String, ResourceProject)

    ''' <summary>
    ''' 检查是否与某个 Project 是相同的工程，只是在不同的网站。
    ''' </summary>
    Public Function IsLike(Project As ResourceProject) As Boolean
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
        If Platform = Project.Platform Then Return False
        'Mod 加载器一致
        If Loaders.Count <> Project.Loaders.Count OrElse Loaders.Except(Project.Loaders).Any() Then Return False
        '若不为光影，则要求 MC 版本一致
        If Types <> ResourceTypes.Shader AndAlso (Drops.Count <> Project.Drops.Count OrElse Drops.Except(Project.Drops).Any()) Then Return False
        '最近更新时间差距在一周以内
        If Math.Abs((LastUpdate - Project.LastUpdate).TotalDays) > 7 Then Return False
        'MCMOD 翻译名 / 原名 / 描述文本 / Slug 的英文部分相同
        If TranslatedName = Project.TranslatedName OrElse
           RawName = Project.RawName OrElse Description = Project.Description OrElse
           GetRaw(Slug) = GetRaw(Project.Slug) Then
            Log($"[Resource] 将 {RawName} ({Slug}) 与 {Project.RawName} ({Project.Slug}) 认定为相似工程")
            '如果只有一个有 DatabaseEntry，设置给另外一个
            If WikiEntry Is Nothing AndAlso Project.WikiEntry IsNot Nothing Then WikiEntry = Project.WikiEntry
            If WikiEntry IsNot Nothing AndAlso Project.WikiEntry Is Nothing Then Project.WikiEntry = WikiEntry
            Return True
        End If
        Return False
    End Function

    '辅助函数
    Public Overrides Function ToString() As String
        Return $"{Id} ({Slug}): {RawName}"
    End Function
    Public Overrides Function Equals(obj As Object) As Boolean
        Dim project = TryCast(obj, ResourceProject)
        Return project IsNot Nothing AndAlso Id = project.Id
    End Function
    Public Shared Operator =(left As ResourceProject, right As ResourceProject) As Boolean
        Return EqualityComparer(Of ResourceProject).Default.Equals(left, right)
    End Operator
    Public Shared Operator <>(left As ResourceProject, right As ResourceProject) As Boolean
        Return Not left = right
    End Operator

End Class
