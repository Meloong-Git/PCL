Public Class ResourceSearcher
    Private Const RESULT_PAGE_SIZE = 40
    Private Const MSG_NO_CHINESE_RESULT As String = "无搜索结果，请尝试搜索其英文名称"

    '输入
    Public Class SearchRequest

        '结果要求

        ''' <summary>
        ''' 结果存储器，会从中获取需加载的页面等。
        ''' 加载后也应输出到此处。
        ''' </summary>
        Public Storage As SearchResult
        ''' <summary>
        ''' 应当尽量达成的结果数量。
        ''' </summary>
        Public TargetResultCount As Integer
        ''' <summary>
        ''' 根据加载位置记录，是否还可以继续获取内容。
        ''' </summary>
        Public ReadOnly Property CanContinue As Boolean
            Get
                If Tag.StartsWithF("/") OrElse Not Sources.HasFlag(ResourcePlatforms.CurseForge) Then Storage.CurseForgeTotal = 0
                If Tag.EndsWithF("/") OrElse Not Sources.HasFlag(ResourcePlatforms.Modrinth) Then Storage.ModrinthTotal = 0
                If Storage.CurseForgeTotal = -1 OrElse Storage.ModrinthTotal = -1 Then Return True
                Return Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal
            End Get
        End Property

        '输入内容

        ''' <summary>
        ''' 筛选资源种类。
        ''' </summary>
        Public Type As ResourceTypes
        ''' <summary>
        ''' 筛选资源标签。空字符串代表不限制。格式例如 "406/worldgen"，分别是 CurseForge 和 Modrinth 的 ID。
        ''' </summary>
        Public Tag As String = ""
        ''' <summary>
        ''' 筛选 Mod 加载器类别。
        ''' </summary>
        Public ModLoader As ModLoaderTypes = ModLoaderTypes.Any
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
        Public Sources As ResourcePlatforms = ResourcePlatforms.Any
        Public Sub New(Type As ResourceTypes, Storage As SearchResult, TargetResultCount As Integer)
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
            Dim Address As String = $"https://api.curseforge.com/v1/mods/search?gameId=432&sortField=2&sortOrder=desc&pageSize={RESULT_PAGE_SIZE}"
            Select Case Type
                Case ResourceTypes.Mod
                    Address += "&classId=6"
                Case ResourceTypes.ModPack
                    Address += "&classId=4471"
                Case ResourceTypes.DataPack
                    Address += "&classId=6945"
                Case ResourceTypes.Shader
                    Address += "&classId=6552"
                Case ResourceTypes.ResourcePack
                    Address += "&classId=12"
            End Select
            If Tag <> "" Then Address += "&categoryId=" & Tag.BeforeFirst("/")
            If ModLoader <> ModLoaderTypes.Any AndAlso Not IgnoreModLoaderFilter Then Address += "&modLoaderType=" & CType(ModLoader, Integer)
            If Not String.IsNullOrEmpty(GameVersion) Then Address += "&gameVersion=" & GameVersion
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&searchFilter=" & EscapeUtils.UrlEscape(SearchText)
            If Storage.CurseForgeOffset > 0 Then Address += "&index=" & Storage.CurseForgeOffset
            Return Address
        End Function
        ''' <summary>
        ''' 获取对应的 Modrinth API 请求链接。
        ''' </summary>
        Public Function GetModrinthAddress(SearchText As String, IgnoreModLoaderFilter As Boolean) As String
            If Tag.EndsWithF("/") Then Storage.ModrinthTotal = 0
            '应用筛选参数
            Dim Address As String = $"https://api.modrinth.com/v2/search?limit={RESULT_PAGE_SIZE}&index=relevance"
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&query=" & EscapeUtils.UrlEscape(SearchText)
            If Storage.ModrinthOffset > 0 Then Address += "&offset=" & Storage.ModrinthOffset
            'facets=[["categories:'game-mechanics'"],["categories:'forge'"],["versions:1.19.3"],["project_type:mod"]]
            Dim Facets As New List(Of String)
            Facets.Add($"[""project_type:{Type.ToString.Lower}""]")
            If Not String.IsNullOrEmpty(Tag) Then Facets.Add($"[""categories:'{Tag.AfterLast("/")}'""]")
            If ModLoader <> ModLoaderTypes.Any AndAlso Not IgnoreModLoaderFilter Then Facets.Add($"[""categories:'{GetStringFromEnum(ModLoader).Lower}'""]")
            If Not String.IsNullOrEmpty(GameVersion) Then Facets.Add($"[""versions:'{GameVersion}'""]")
            Address += "&facets=[" & String.Join(",", Facets) & "]"
            Return Address
        End Function

        '相同判断
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim request = TryCast(obj, SearchRequest)
            Return request IsNot Nothing AndAlso
                Type = request.Type AndAlso TargetResultCount = request.TargetResultCount AndAlso
                Tag = request.Tag AndAlso ModLoader = request.ModLoader AndAlso Sources = request.Sources AndAlso
                GameVersion = request.GameVersion AndAlso SearchText = request.SearchText
        End Function
        Public Shared Operator =(left As SearchRequest, right As SearchRequest) As Boolean
            Return EqualityComparer(Of SearchRequest).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As SearchRequest, right As SearchRequest) As Boolean
            Return Not left = right
        End Operator
        Public Overrides Function GetHashCode() As Integer
            Return (Type, Tag, ModLoader, GameVersion, SearchText, Sources).GetHashCode()
        End Function

    End Class

    '输出
    Public Class SearchResult

        '加载位置记录

        Public CurseForgeOffset As Integer = 0
        Public CurseForgeTotal As Integer = -1

        Public ModrinthOffset As Integer = 0
        Public ModrinthTotal As Integer = -1

        '结果列表

        ''' <summary>
        ''' 可供展示的所有工程的列表。
        ''' </summary>
        Public Results As New List(Of ResourceProject)
        ''' <summary>
        ''' 当前的错误信息。如果没有则为 Nothing。
        ''' </summary>
        Public ErrorMessage As String = Nothing

    End Class

    ''' <summary>
    ''' 根据搜索请求获取一系列的工程列表。
    ''' </summary>
    Public Shared Function GetLoader(InputDelegate As Func(Of SearchRequest)) As LoaderTask(Of SearchRequest, Integer)
        Return New LoaderTask(Of SearchRequest, Integer)("社区资源获取", AddressOf Run, InputDelegate) With {.ReloadTimeout = 60 * 1000}
    End Function
    Private Shared Sub Run(Task As LoaderTask(Of SearchRequest, Integer))
        Dim Request As SearchRequest = Task.Input
        Dim Storage = Request.Storage '避免其他线程对 Request.Storage 重新进行了赋值

#Region "前置检查"

        If Storage.Results.Count >= Request.TargetResultCount Then
            Log($"[Resource] 已有 {Storage.Results.Count} 个结果，多于所需的 {Request.TargetResultCount} 个结果，结束处理")
            Return
        ElseIf Not Request.CanContinue Then
            If Not Storage.Results.Any() Then
                Throw New Exception("没有符合条件的结果")
            Else
                Log($"[Resource] 已有 {Storage.Results.Count} 个结果，少于所需的 {Request.TargetResultCount} 个结果，但无法继续获取，结束处理")
                Return
            End If
        End If

        '拒绝 1.13- Quilt（这个版本根本没有 Quilt）
        If Request.ModLoader = ModLoaderTypes.Quilt AndAlso CompareVersion(If(Request.GameVersion, "1.15"), "1.14") = -1 Then
            Throw New Exception("Quilt 不支持 Minecraft " & Request.GameVersion)
        End If

#End Region

#Region "中文搜索"

        Dim RawSearchText As String = If(Request.SearchText, "").Trim
        RawSearchText = RawSearchText.Lower
        Log("[Resource] 工程列表搜索原始文本：" & RawSearchText)

        Dim IsChineseSearch As Boolean =
            RegexCheck(RawSearchText, "[\u4e00-\u9fbb]") AndAlso Not String.IsNullOrEmpty(RawSearchText) AndAlso
            (Request.Type = ResourceTypes.Mod OrElse Request.Type = ResourceTypes.DataPack) '目前仅对 Mod 和数据包进行中文搜索，注意整合包的名称可能已经有中文了
        Dim CurseForgeAltSearchText As String = Nothing, ModrinthAltSearchText As String = Nothing, ModrinthSlugs As New List(Of String) '从中文转为英文的替代搜索内容
        If IsChineseSearch Then
            '帮助方法：从搜索项提取可能的英文单词
            Dim ExtractWords =
            Function(Result As SearchEntry(Of WikiEntry), Source As ResourcePlatforms) As IEnumerable(Of String)
                '从各个可能的来源提取候选
                Dim Candidates As New List(Of String)
                If Result.Item.CurseForgeSlug IsNot Nothing AndAlso Source = ResourcePlatforms.CurseForge Then
                    Candidates.Add(Result.Item.CurseForgeSlug.Replace("-", " ").Replace("/", " "))
                End If
                If Result.Item.ModrinthSlug IsNot Nothing AndAlso Source = ResourcePlatforms.Modrinth Then
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
            If Request.Sources.HasFlag(ResourcePlatforms.CurseForge) Then
                '数据库搜索
                Static CurseForgeSearchEntries As List(Of SearchEntry(Of WikiEntry)) =
                WikiEntry.All.Where(Function(Entry) Entry.CurseForgeSlug IsNot Nothing).Select(
                Function(Entry) New SearchEntry(Of WikiEntry) With {
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
                    CurseForgeAltSearchText = ExtractWords(CurseForgeTarget, ResourcePlatforms.CurseForge).Join(" ")
                    Log("[Resource] 中文搜索关键词（CurseForge）：" & CurseForgeAltSearchText, NotifyLevel.DebugModeOnly)
                End If
            End If
            'Modrinth
            If Request.Sources.HasFlag(ResourcePlatforms.Modrinth) Then
                '数据库搜索
                Static ModrinthSearchEntries As List(Of SearchEntry(Of WikiEntry)) =
                WikiEntry.All.Where(Function(Entry) Entry.ModrinthSlug IsNot Nothing).Select(
                Function(Entry) New SearchEntry(Of WikiEntry) With {
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
                        For Each Word In ExtractWords(Result, ResourcePlatforms.Modrinth)
                            If Not WordWeights.ContainsKey(Word) Then WordWeights.Add(Word, 0)
                            Dim Similarity = If(Result.SearchSource.Any(Function(s) s.Aliases.Contains(RawSearchText)), 1000, Result.Similarity) '完全匹配为 1000
                            WordWeights(Word) += Similarity * Result.Item.Popularity '权重 += 相似度 * 受欢迎程度
                        Next
                    Next
                    ModrinthAltSearchText = WordWeights.MaxBy(Function(w) w.Value).Key
                    Log("[Resource] 中文搜索关键词（Modrinth）：" & ModrinthAltSearchText, NotifyLevel.DebugModeOnly)
                    '直接请求工程
                    ModrinthSlugs = ModrinthSearchResults.Take(100).Select(Function(r) r.Item.ModrinthSlug).ToList
                End If
            End If
            '结束
            If String.IsNullOrEmpty(CurseForgeAltSearchText) AndAlso String.IsNullOrEmpty(ModrinthAltSearchText) AndAlso Not ModrinthSlugs.Any Then
                Throw New Exception(MSG_NO_CHINESE_RESULT)
            End If
        End If
        Task.Progress = 0.05

#End Region

        Dim RealResults As New List(Of ResourceProject)
NextPage:
        Dim RawResults As New ConcurrentList(Of ResourceProject)

#Region "从 CurseForge 和 Modrinth 获取结果列表，存储于 RawResults"

        Dim WorkThreads As New List(Of Thread)
        Dim Errors As New ConcurrentList(Of (Ex As Exception, Source As ResourcePlatforms))

        '在 1.14-，部分老 Mod 没有设置支持的加载器，因此添加 Forge 筛选就会出现遗漏
        '所以，在发起请求时不筛选加载器，然后在返回的结果中自行筛除不是 Forge 的 Mod
        Dim IgnoreModLoaderFilter = Request.ModLoader = ModLoaderTypes.Forge AndAlso McVersion.VersionToDrop(Request.GameVersion) < 140
        Try

            'CurseForge 搜索
            If Request.Sources.HasFlag(ResourcePlatforms.CurseForge) AndAlso
               Not (Storage.CurseForgeTotal > -1 AndAlso Storage.CurseForgeTotal <= Storage.CurseForgeOffset) AndAlso '剩余的未显示的搜索结果不足
               (Not IsChineseSearch OrElse (IsChineseSearch AndAlso Not String.IsNullOrEmpty(CurseForgeAltSearchText))) Then '如果是中文搜索，就只在有对应搜索关键词的时候才继续
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        '获取工程列表
                        Dim CurseForgeUrl As String = Request.GetCurseForgeAddress(If(CurseForgeAltSearchText, RawSearchText), IgnoreModLoaderFilter)
                        Log("[Resource] 开始 CurseForge 搜索：" & CurseForgeUrl)
                        Dim RequestResult As JObject = DlModRequest(CurseForgeUrl)
                        Dim ProjectList As New List(Of ResourceProject)
                        For Each JsonEntry As JObject In RequestResult("data")
                            Dim Project As New ResourceProject(JsonEntry)
                            If Request.Type = ResourceTypes.ResourcePack AndAlso Project.Tags.Contains("数据包") Then Continue For 'CurseForge 将一些数据包分类成了资源包
                            ProjectList.Add(Project)
                        Next
                        '更新结果
                        ProjectList.ForEach(Sub(p) RawResults.Add(p))
                        Storage.CurseForgeOffset += RequestResult("data").Count
                        Storage.CurseForgeTotal = RequestResult("pagination")("totalCount").ToObject(Of Integer)
                        Log($"[Resource] 从 CurseForge 搜索到了 {ProjectList.Count} 个工程（总计已获取 {Storage.CurseForgeOffset} 个，共 {Storage.CurseForgeTotal} 个）")
                    Catch ex As Exception
                        Log(ex, "CurseForge 搜索失败")
                        Storage.CurseForgeTotal = -1
                        Errors.Add((ex, ResourcePlatforms.CurseForge))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 '可能重复加载多页，所以不能直接给够
                End Sub, "CurseForge Search"))
            End If

            'Modrinth 搜索
            If Request.Sources.HasFlag(ResourcePlatforms.Modrinth) AndAlso
               Not (Storage.ModrinthTotal > -1 AndAlso Storage.ModrinthTotal <= Storage.ModrinthOffset) AndAlso '剩余的未显示的搜索结果不足
               (Not IsChineseSearch OrElse (IsChineseSearch AndAlso Not String.IsNullOrEmpty(ModrinthAltSearchText))) Then '如果是中文搜索，就只在有对应搜索关键词的时候才继续
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        Dim ModrinthUrl As String = Request.GetModrinthAddress(If(ModrinthAltSearchText, RawSearchText), IgnoreModLoaderFilter)
                        Log("[Resource] 开始 Modrinth 搜索：" & ModrinthUrl)
                        Dim RequestResult As JObject = DlModRequest(ModrinthUrl)
                        Dim ProjectList As New List(Of ResourceProject)
                        For Each JsonEntry As JObject In RequestResult("hits")
                            ProjectList.Add(New ResourceProject(JsonEntry))
                        Next
                        '更新结果
                        ProjectList.ForEach(Sub(p) RawResults.Add(p))
                        Storage.ModrinthOffset += RequestResult("hits").Count
                        Storage.ModrinthTotal = RequestResult("total_hits").ToObject(Of Integer)
                        Log($"[Resource] 从 Modrinth 搜索到了 {ProjectList.Count} 个工程（总计已获取 {Storage.ModrinthOffset} 个，共 {Storage.ModrinthTotal} 个）")
                    Catch ex As Exception
                        Log(ex, "Modrinth 搜索失败")
                        Storage.ModrinthTotal = -1
                        Errors.Add((ex, ResourcePlatforms.Modrinth))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 '可能重复加载多页，所以不能直接给够
                End Sub, "Modrinth Search"))
            End If

            'Modrinth 直接获取工程
            If Request.Sources.HasFlag(ResourcePlatforms.Modrinth) AndAlso
               Not (Storage.ModrinthTotal > -1 AndAlso Storage.ModrinthTotal <= Storage.ModrinthOffset) AndAlso '剩余的未显示的搜索结果不足
               ModrinthSlugs.Any Then '有直接获取的 Slug
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        Dim ModrinthUrl As String = $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthSlugs.Join(""",""")}""]"
                        Log("[Resource] 开始 Modrinth 直接获取：" & ModrinthUrl)
                        Dim ProjectList As New List(Of ResourceProject)
                        For Each JsonEntry As JObject In DlModRequest(ModrinthUrl)
                            Dim Project As New ResourceProject(JsonEntry)
                            '应用筛选
                            If Request.Type <> ResourceTypes.Any AndAlso Not Project.Types.HasFlag(Request.Type) Then Continue For
                            If Not String.IsNullOrEmpty(Request.Tag) AndAlso
                                Not JsonEntry("categories").Any(Function(c) c.ToString = Request.Tag.AfterLast("/")) Then Continue For 'Project.Tags 已经转换成中文了，只能从 json 判
                            If Request.ModLoader <> ModLoaderTypes.Any AndAlso Not IgnoreModLoaderFilter AndAlso
                                Not Project.Loaders.Any(Function(m) m = Request.ModLoader) Then Continue For
                            If Not String.IsNullOrEmpty(Request.GameVersion) AndAlso
                                Not Project.UnsafeGameVersions.Any(Function(d) d = Request.GameVersion) Then Continue For
                            ProjectList.Add(Project)
                        Next
                        '更新结果
                        ProjectList.ForEach(Sub(p) RawResults.Add(p))
                        Log($"[Resource] 从 Modrinth 直接获取到了 {ProjectList.Count} 个工程")
                        ModrinthSlugs.Clear() '防止重试/加载下一页时重复获取
                    Catch ex As Exception
                        Log(ex, "Modrinth 直接获取失败")
                        Errors.Add((ex, ResourcePlatforms.Modrinth))
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
                RawResults = RawResults.Where(Function(p) Not p.Loaders.Any() OrElse p.Loaders.Contains(ModLoaderTypes.Forge)).ToList
            End If

            '确保存在结果
            Storage.ErrorMessage = Nothing
            If Not RawResults.Any() Then
                If Errors.Any() Then
                    Throw Errors.First.Ex
                Else
                    If IsChineseSearch AndAlso Not (Request.Type = ResourceTypes.Mod OrElse Request.Type = ResourceTypes.DataPack) Then
                        Throw New Exception(MSG_NO_CHINESE_RESULT)
                    ElseIf Request.Sources = ResourcePlatforms.CurseForge AndAlso Request.Tag.StartsWithF("/") Then
                        Throw New Exception("CurseForge 不兼容所选的类型")
                    ElseIf Request.Sources = ResourcePlatforms.Modrinth AndAlso Request.Tag.EndsWithF("/") Then
                        Throw New Exception("Modrinth 不兼容所选的类型")
                    Else
                        Throw New Exception("没有搜索结果")
                    End If
                End If
            ElseIf Errors.Any() Then
                '有结果但是有错误
                If Errors.Any(Function(e) e.Source = ResourcePlatforms.CurseForge) Then
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
        RawResults = RawResults.Where(Function(x) x.Platform = ResourcePlatforms.CurseForge).Concat(RawResults.Where(Function(x) x.Platform <> ResourcePlatforms.CurseForge)).ToList
        '去重
        RawResults = RawResults.Distinct(Function(a, b) a.IsLike(b)).ToList
        '已有内容去重
        RawResults = RawResults.Where(Function(r) Not RealResults.Any(Function(b) r.IsLike(b)) AndAlso
                                                  Not Storage.Results.Any(Function(b) r.IsLike(b))).ToList
        '加入列表
        RealResults.AddRange(RawResults)
        Log($"[Resource] 去重、筛选后累计新增结果 {RealResults.Count} 个（目前已有结果 {Storage.Results.Count} 个）")

#End Region

#Region "检查结果数量，如果不足且可继续，会继续加载下一页"

        If RealResults.Count + Storage.Results.Count < Request.TargetResultCount Then
            Log($"[Resource] 总结果数需求最少 {Request.TargetResultCount} 个，仅获得了 {RealResults.Count + Storage.Results.Count} 个")
            If Request.CanContinue AndAlso Not Errors.Any Then '如果有某个源失败则不再重试，这时候重试可能导致无限循环
                Log("[Resource] 将继续尝试加载下一页")
                GoTo NextPage
            Else
                Log("[Resource] 无法继续加载，将强制结束")
            End If
        End If

#End Region

#Region "将结果排序并添加"

        Dim Scores As New Dictionary(Of ResourceProject, Double) '排序分
        Dim GetDownloadCountMult =
        Function(Project As ResourceProject) As Double
            Select Case Request.Type
                Case ResourceTypes.Mod, ResourceTypes.ModPack
                    Return If(Project.Platform = ResourcePlatforms.CurseForge, 1, 5)
                Case ResourceTypes.DataPack
                    Return If(Project.Platform = ResourcePlatforms.CurseForge, 10, 1)
                Case ResourceTypes.ResourcePack, ResourceTypes.Shader
                    Return If(Project.Platform = ResourcePlatforms.CurseForge, 1, 4)
                Case Else
                    Return 1
            End Select
        End Function
        If String.IsNullOrEmpty(RawSearchText) Then
            '如果没有搜索文本，按下载量将结果排序
            For Each Result As ResourceProject In RealResults
                Scores(Result) = Result.DownloadCount * GetDownloadCountMult(Result)
            Next
        Else
            '如果有搜索文本，按关联度将结果排序
            '排序分 = 搜索相对相似度 (1) + 下载量权重 (对数，10 亿时为 1) + 有中文名 (0.2)
            Dim SearchEntries As New List(Of SearchEntry(Of ResourceProject))
            For Each Result As ResourceProject In RealResults
                Scores(Result) = If(Result.WikiId > 0, 0.2, 0) +
                           Math.Log10(Math.Max(Result.DownloadCount, 1) * GetDownloadCountMult(Result)) / 9
                SearchEntries.Add(New SearchEntry(Of ResourceProject) With {.Item = Result, .SearchSource = New List(Of SearchSource) From {
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

End Class
