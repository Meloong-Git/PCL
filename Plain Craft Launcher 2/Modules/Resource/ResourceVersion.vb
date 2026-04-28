''' <summary>
''' 社区资源的单个可下载版本。
''' </summary>
Public Class ResourceVersion

    '基础字段

    ''' <summary>
    ''' 用于唯一性鉴别该版本的 ID。CurseForge 中为 123456 的大整数，Modrinth 中为英文乱码的 Version 字段。
    ''' </summary>
    Public Id As String
    ''' <summary>
    ''' 该版本所对应的单一资源类别。
    ''' </summary>
    Public ResourceType As ResourceTypes

    '描述性字段

    ''' <summary>
    ''' 版本描述名。
    ''' 并非文件名或版本号，是由上传者完全自定义的字段。
    ''' </summary>
    Public Display As String
    ''' <summary>
    ''' 发布时间。
    ''' </summary>
    Public ReleaseDate As Date
    ''' <summary>
    ''' 下载量计数。
    ''' 注意，仅为一个平台的计数，无法反映多平台加起来的下载量，且 CurseForge 可能错误地返回 0。
    ''' </summary>
    Public DownloadCount As Integer
    ''' <summary>
    ''' 版本号。
    ''' 不一定是标准格式。CurseForge 默认为 Nothing。
    ''' </summary>
    Public Version As String
    ''' <summary>
    ''' 支持的 Mod 加载器列表。
    ''' 可能为空列表，不为 Nothing。
    ''' </summary>
    Public Loaders As List(Of ModLoaderTypes)
    ''' <summary>
    ''' 支持的游戏版本列表。类型包括："26.1.5"，"26.1"，"26.1 预览版"，"1.18.5"，"1.18"，"1.18 预览版"，"21w15a"，"未知版本"。
    ''' </summary>
    Public GameVersions As List(Of String)

    ''' <summary>
    ''' 发布状态。
    ''' </summary>
    Public ReleaseType As ReleaseTypes
    ''' <summary>
    ''' 发布状态的友好描述。
    ''' 例如："正式版"，"Beta 版"。
    ''' </summary>
    Public ReadOnly Property ReleaseTypeDisplay As String
        Get
            Select Case ReleaseType
                Case ReleaseTypes.Release
                    Return "正式版"
                Case ReleaseTypes.Beta
                    Return If(ModeDebug, "Beta 版", "测试版")
                Case Else
                    Return If(ModeDebug, "Alpha 版", "早期测试版")
            End Select
        End Get
    End Property
    Public Enum ReleaseTypes
        'https://docs.curseforge.com/rest-api/#tocS_FileReleaseType
        Release = 1
        Beta = 2
        Alpha = 3
    End Enum

    '下载信息

    ''' <summary>
    ''' 下载信息是否可用。
    ''' </summary>
    Public ReadOnly Property DownloadAvailable As Boolean
        Get
            Return FileName IsNot Nothing AndAlso DownloadUrls IsNot Nothing
        End Get
    End Property
    ''' <summary>
    ''' 文件名。
    ''' </summary>
    Public FileName As String = Nothing
    ''' <summary>
    ''' 对应文件的所有可能的下载 URL。
    ''' </summary>
    Public DownloadUrls As List(Of String)
    ''' <summary>
    ''' 对应文件的 SHA1 或 MD5。
    ''' </summary>
    Public Hash As String = Nothing
    ''' <summary>
    ''' 文件大小。不可用时为 -1。
    ''' </summary>
    Public Size As Integer = -1
    ''' <summary>
    ''' 作为该版本依赖项的项目 ID。
    ''' 它们可能没有加载，在加载后会添加到 Dependencies 中（主要是因为 Modrinth 返回的是字符串 ID 而非 Slug，导致 Project.Id 查询不到）。
    ''' </summary>
    Private RawDependencies As New List(Of String)
    ''' <summary>
    ''' 作为该版本依赖项的项目 ID。
    ''' </summary>
    Public Dependencies As New List(Of String)

    '初始化

    ''' <summary>
    ''' 从平台 API 返回的 JSON 中初始化实例。若出错会抛出异常。
    ''' </summary>
    Public Shared Function FromPlatformJson(Data As JObject, ExpectedResourceType As ResourceTypes) As ResourceVersion
        Dim Result As New ResourceVersion
        With Result
            .ResourceType = ExpectedResourceType
            If Data.ContainsKey("gameId") Then
#Region "CurseForge"
                '简单信息
                .Id = Data("id")
                .Display = Data("displayName").ToString.Replace("	", "").Trim(" ")
                .Version = Nothing
                .ReleaseDate = Data("fileDate")
                .ReleaseType = CType(Data("releaseType").ToObject(Of Integer), ReleaseTypes)
                .DownloadCount = Data("downloadCount")
                .FileName = Data("fileName")
                .Size = Data("fileLength")
                .Hash = CType(Data("hashes"), JArray).ToList.FirstOrDefault(Function(s) s("algo").ToObject(Of Integer) = 1)?("value")
                If .Hash Is Nothing Then .Hash = CType(Data("hashes"), JArray).ToList.FirstOrDefault(Function(s) s("algo").ToObject(Of Integer) = 2)?("value")
                'DownloadAddress
                Dim Url = Data("downloadUrl").ToString
                If Url = "" Then Url = $"https://edge.forgecdn.net/files/{CInt(.Id.ToString.Substring(0, 4))}/{CInt(.Id.ToString.Substring(4))}/{ .FileName}"
                .DownloadUrls = ParseCurseForgeDownloadUrls(Url.Replace(.FileName, EscapeUtils.UrlEscape(.FileName))) '对脑残 CurseForge 的下载地址进行多种修正
                .DownloadUrls.Add(DlSourceModGet(Url)) '添加镜像源；注意 MCIM 源不支持 URL 编码后的文件名，必须传入 URL 编码前的文件名
                .DownloadUrls = .DownloadUrls.Distinct.ToList '最终去重
                'Dependencies
                If Data.ContainsKey("dependencies") Then
                    .RawDependencies = Data("dependencies").
                            Where(Function(d) d("relationType").ToObject(Of Integer) = 3 AndAlso '种类为依赖
                                              d("modId").ToObject(Of Integer) <> 306612 AndAlso d("modId").ToObject(Of Integer) <> 634179). '排除 Fabric API 和 Quilt API
                            Select(Function(d) d("modId").ToString).ToList
                End If
                'GameVersions
                Dim RawVersions As List(Of String) = Data("gameVersions").Select(Function(t) t.ToString.Trim.Lower).ToList
                .GameVersions = RawVersions.
                        Where(Function(v) McVersion.IsFormatFit(v)).
                        Select(Function(v) v.Replace("-snapshot", " 预览版")).
                        Distinct.ToList
                If .GameVersions.IsSingle Then
                    .GameVersions = .GameVersions.ToList
                ElseIf .GameVersions.Count > 1 Then
                    .GameVersions = .GameVersions.SortByComparison(AddressOf CompareVersionGE).ToList
                    If .ResourceType = ResourceTypes.ModPack Then .GameVersions = { .GameVersions(0)}.ToList '整合包理应只 “支持” 一个版本
                Else
                    .GameVersions = New List(Of String) From {"未知版本"}
                End If
                'ModLoaders
                .Loaders = New List(Of ModLoaderTypes)
                If RawVersions.Contains("forge") Then .Loaders.Add(ModLoaderTypes.Forge)
                If RawVersions.Contains("fabric") Then .Loaders.Add(ModLoaderTypes.Fabric)
                If RawVersions.Contains("quilt") Then .Loaders.Add(ModLoaderTypes.Quilt)
                If RawVersions.Contains("neoforge") Then .Loaders.Add(ModLoaderTypes.NeoForge)
#End Region
            Else
#Region "Modrinth"
                '简单信息
                .Id = Data("id")
                .Display = Data("name").ToString.Replace("	", "").Trim(" ")
                .Version = Data("version_number")
                .ReleaseDate = Data("date_published")
                .ReleaseType = If(Data("version_type").ToString = "release", ReleaseTypes.Release, If(Data("version_type").ToString = "beta", ReleaseTypes.Beta, ReleaseTypes.Alpha))
                .DownloadCount = Data("downloads")
                If CType(Data("files"), JArray).Any() Then '可能为空
                    Dim File As JToken = Data("files")(0)
                    .FileName = File("filename")
                    .DownloadUrls = New List(Of String) From {File("url"), DlSourceModGet(File("url"))}.Distinct.ToList '同时添加了镜像源
                    .Size = File("size")
                    .Hash = File("hashes")("sha1")
                End If
                'Loaders
                '结果可能混杂着 Mod、数据包和服务端插件
                Dim RawLoaders As List(Of String) = Data("loaders").Select(Function(v) v.ToString).ToList
                .Loaders = New List(Of ModLoaderTypes)
                If .ResourceType.HasFlag(ResourceTypes.Mod) OrElse .ResourceType.HasFlag(ResourceTypes.DataPack) Then
                    If RawLoaders.Intersect({"bukkit", "folia", "paper", "purpur", "spigot"}).Any() Then .ResourceType = ResourceTypes.Plugin 'Veinminer Enchantment 同时支持服务端与 Fabric
                    If RawLoaders.Contains("datapack") Then .ResourceType = ResourceTypes.DataPack
                    If RawLoaders.Contains("forge") Then .Loaders.Add(ModLoaderTypes.Forge) : .ResourceType = ResourceTypes.Mod
                    If RawLoaders.Contains("neoforge") Then .Loaders.Add(ModLoaderTypes.NeoForge) : .ResourceType = ResourceTypes.Mod
                    If RawLoaders.Contains("fabric") Then .Loaders.Add(ModLoaderTypes.Fabric) : .ResourceType = ResourceTypes.Mod
                    If RawLoaders.Contains("quilt") Then .Loaders.Add(ModLoaderTypes.Quilt) : .ResourceType = ResourceTypes.Mod
                Else
                    '使用传入的类别，不作修改（#8377）
                End If
                'Dependencies
                If Data.ContainsKey("dependencies") Then
                    .RawDependencies = Data("dependencies").
                            Where(Function(d) d("dependency_type") = "required" AndAlso '种类为依赖
                                              d("project_id") <> "P7dR8mSH" AndAlso d("project_id") <> "qvIfYCYJ" AndAlso '排除 Fabric API 和 Quilt API
                                              d("project_id").ToString.Length > 0). '有时候真的会空……
                            Select(Function(d) d("project_id").ToString).ToList
                End If
                'GameVersions
                Dim RawVersions As List(Of String) = Data("game_versions").Select(Function(t) t.ToString.Trim.Lower).ToList
                .GameVersions = RawVersions.Where(Function(v) v.Contains(".")).
                                               Select(Function(v) If(v.Contains("-"), v.BeforeFirst("-") & " 预览版", If(v.StartsWithF("b1."), "远古版本", v))).
                                               Distinct.ToList
                If .GameVersions.IsSingle Then
                    '无需处理
                ElseIf .GameVersions.Count > 1 Then
                    .GameVersions = .GameVersions.SortByComparison(AddressOf CompareVersionGE).ToList
                    If .ResourceType = ResourceTypes.ModPack Then .GameVersions = New List(Of String) From { .GameVersions(0)} '整合包理应只 “支持” 一个版本
                ElseIf RawVersions.Any(Function(v) RegexCheck(v, "[0-9]{2}w[0-9]{2}[a-z]")) Then
                    .GameVersions = RawVersions.Where(Function(v) RegexCheck(v, "[0-9]{2}w[0-9]{2}[a-z]")).ToList
                Else
                    .GameVersions = New List(Of String) From {"未知版本"}
                End If
#End Region
            End If
        End With
        Return Result
    End Function
    ''' <summary>
    ''' 从项目 ID 获取其下的全部版本列表，并获取其前置项目信息。
    ''' </summary>
    Public Shared Function FromProjectId(ProjectId As String, Platform As ResourcePlatforms, Optional LoadDependencies As Boolean = True) As List(Of ResourceVersion)
        If Not Platform.Flags.IsSingle Then Throw New ArgumentException($"必须指定单一平台，当前值为 {Platform}")
        Dim TargetProject = ResourceProject.FromProjectId(ProjectId, Platform)
        '获取项目对象的文件列表
        If Not ProjectFilesCache.ContainsKey(ProjectId) Then
            Log("[Resource] 开始获取项目版本列表：" & ProjectId)
            Dim ResultJsonArray As JArray = Nothing
            If Platform = ResourcePlatforms.CurseForge Then
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
                        Log($"[Resource] CurseForge 返回的资源版本列表存在缺失，即将进行第 {RetryCount + 1} 次重试", NotifyLevel.DebugModeOnly) '#6224
                        Log($"[Resource] 返回的原始内容如下：{vbCrLf}{ResultJson}")
                    Else
                        Log($"[Resource] CurseForge 返回的资源版本列表存在缺失，返回的原始内容如下：{vbCrLf}{ResultJson}")
                        Throw New Exception("CurseForge 返回的资源版本列表存在缺失")
                    End If
                Next
            Else 'Modrinth
                ResultJsonArray = DlModRequest($"https://api.modrinth.com/v2/project/{ProjectId}/version?include_changelog=false")
            End If
            ProjectFilesCache(ProjectId) = ResultJsonArray.Select(Function(a) ResourceVersion.FromPlatformJson(a, TargetProject.Types)).
                Where(Function(a) a.DownloadAvailable).
                DistinctBy(Function(a) a.Id).ToList 'CurseForge 可能会重复返回相同项（#1330）
        End If
        Dim Result As List(Of ResourceVersion) = ProjectFilesCache(ProjectId)
        If Not LoadDependencies Then Return Result
        '加载前置信息
        Dim Dependencies As List(Of String) = Result.SelectMany(Function(f) f.RawDependencies).Distinct().ToList
        If Dependencies.Any Then
            Log($"[Resource] 项目 {ProjectId} 中需要加载的前置：{Dependencies.Join("，")}")
            For Each DependencyProject In ResourceProject.FromProjectIds(Dependencies, TargetProject.Platform)
                For Each File In Result
                    If File.RawDependencies.Contains(DependencyProject.Id) AndAlso DependencyProject.Id <> ProjectId AndAlso Not File.Dependencies.Contains(DependencyProject.Id) Then
                        File.Dependencies.Add(DependencyProject.Id)
                    End If
                Next
            Next
        End If
        Return Result
    End Function
    ''' <summary>
    ''' 项目 ID 对应的版本信息的缓存。不会保存到文件。
    ''' </summary>
    Protected Friend Shared ProjectFilesCache As New Dictionary(Of String, List(Of ResourceVersion))

    '序列化

    ''' <summary>
    ''' 从缓存 JSON 读取字段数据。
    ''' </summary>
    Public Shared Function FromCacheJson(Data As JObject) As ResourceVersion
        Dim Result As New ResourceVersion
        With Result
            .ResourceType = CType(Data("ResourceType").ToObject(Of Integer), ResourceTypes)
            .Id = Data("Id").ToString
            .Display = Data("DisplayName").ToString
            If Data.ContainsKey("Version") Then .Version = Data("Version").ToString
            .ReleaseDate = Data("ReleaseDate").ToObject(Of Date)
            .DownloadCount = Data("DownloadCount").ToObject(Of Integer)
            .ReleaseType = CType(Data("ReleaseType").ToObject(Of Integer), ReleaseTypes)
            If Data.ContainsKey("FileName") Then .FileName = Data("FileName").ToString
            If Data.ContainsKey("DownloadUrls") Then .DownloadUrls = Data("DownloadUrls").ToObject(Of List(Of String))
            If Data.ContainsKey("ModLoaders") Then .Loaders = Data("ModLoaders").ToObject(Of List(Of ModLoaderTypes))
            If Data.ContainsKey("Size") Then .Size = Data("Size").ToObject(Of Integer)
            If Data.ContainsKey("Hash") Then .Hash = Data("Hash").ToString
            If Data.ContainsKey("GameVersions") Then .GameVersions = Data("GameVersions").ToObject(Of List(Of String))
            If Data.ContainsKey("RawDependencies") Then .RawDependencies = Data("RawDependencies").ToObject(Of List(Of String))
            If Data.ContainsKey("Dependencies") Then .Dependencies = Data("Dependencies").ToObject(Of List(Of String))
        End With
        Return Result
    End Function
    ''' <summary>
    ''' 转为缓存 JSON。
    ''' </summary>
    Public Function ToCacheJson() As JObject
        Dim Result As New JObject
        With Result
            .Add("ResourceType", ResourceType)
            .Add("Id", Id)
            If Version IsNot Nothing Then .Add("Version", Version)
            .Add("DisplayName", Display)
            .Add("ReleaseDate", ReleaseDate)
            .Add("DownloadCount", DownloadCount)
            .Add("ModLoaders", New JArray(Loaders.Select(Function(m) CInt(m))))
            .Add("GameVersions", New JArray(GameVersions))
            .Add("ReleaseType", CInt(ReleaseType))
            If FileName IsNot Nothing Then .Add("FileName", FileName)
            If DownloadUrls IsNot Nothing Then .Add("DownloadUrls", New JArray(DownloadUrls))
            If Hash IsNot Nothing Then .Add("Hash", Hash)
            If Size >= 0 Then .Add("Size", Size)
            .Add("RawDependencies", New JArray(RawDependencies))
            .Add("Dependencies", New JArray(Dependencies))
        End With
        Return Result
    End Function

    '杂项

    ''' <summary>
    ''' 转为列表项。
    ''' </summary>
    Public Function ToListItem(ClickHandler As MyListItem.ClickEventHandler, Optional SaveAsButtonHandler As MyIconButton.ClickEventHandler = Nothing,
                               Optional IsBadDisplay As Boolean = False) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            Dim Result As New MyListItem
            With Result

                '基础属性
                .SnapsToDevicePixels = True
                .Height = 42
                .Type = MyListItem.CheckType.Clickable
                .Tag = Me
                .Title = If(IsBadDisplay, FileName, Display)
                .Logo = $"{PathImage}ReleaseTypes/{ReleaseType}.png"
                AddHandler .Click, ClickHandler

                '描述
                .Info =
                Iterator Function()
                    If .Title <> FileName.BeforeLast(".") Then Yield FileName.BeforeLast(".")
                    If Dependencies.Any Then Yield $"{Dependencies.Count} 项前置"
                    If GameVersions.All(Function(v) Not v.Contains(".") OrElse {"w", "snapshot", "rc", "pre", "experimental", "-"}.Any(Function(s) v.ContainsIgnoreCase(s))) Then Yield $"游戏版本 {GameVersions.Join("、"c)}"
                    If DownloadCount > 0 Then Yield $"下载 {If(DownloadCount > 100000, Math.Round(DownloadCount / 10000) & " 万", DownloadCount)} 次" 'CurseForge 的下载次数经常错误地返回 0
                    Yield $"更新于 {GetTimeSpanString(ReleaseDate - Date.Now, False)}"
                    If ReleaseType <> ReleaseTypes.Release Then Yield ReleaseTypeDisplay
                End Function().Join("，"c)

                '另存为按钮
                If SaveAsButtonHandler IsNot Nothing Then
                    Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = "另存为"}
                    ToolTipService.SetPlacement(BtnSave, Primitives.PlacementMode.Center)
                    ToolTipService.SetVerticalOffset(BtnSave, 30)
                    ToolTipService.SetHorizontalOffset(BtnSave, 2)
                    AddHandler BtnSave.Click, SaveAsButtonHandler
                    .Buttons = {BtnSave}
                End If

            End With
            Return Result
        End Function) With {.Height = 42}
    End Function
    ''' <summary>
    ''' 获取下载信息。
    ''' </summary>
    ''' <param name="LocalFileOrFolder">目标本地文件夹，或完整的文件路径。会自动判断类型。</param>
    Public Function ToNetFile(LocalFileOrFolder As String) As NetFile
        Return New NetFile(DownloadUrls, LocalFileOrFolder & If(LocalFileOrFolder.EndsWithF("\"), FileName, ""), New FileChecker(Hash:=Hash, ActualSize:=Size), SimulateBrowserHeaders:=True)
    End Function
    ''' <summary>
    ''' 重新整理 CurseForge 的下载地址。
    ''' </summary>
    Public Shared Function ParseCurseForgeDownloadUrls(Url As String) As List(Of String)
        Return {
            Url.Replace("-service.overwolf.wtf", ".forgecdn.net").Replace("://edge.", "://mediafilez.").Replace("://media.", "://mediafilez."),
            Url.Replace("://edge.", "://mediafilez.").Replace("://media.", "://mediafilez."),
            Url.Replace("-service.overwolf.wtf", ".forgecdn.net"),
            Url.Replace("://media.", "://edge."),
            Url
        }.Distinct.ToList
    End Function
    Public Overrides Function ToString() As String
        Return $"{Id}: {FileName}"
    End Function

End Class
