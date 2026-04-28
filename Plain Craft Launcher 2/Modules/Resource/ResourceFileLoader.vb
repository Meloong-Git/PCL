Public Module ResourceFileLoader
    Private Const ResourceFileCacheVersion As Integer = 16

    '加载 Mod 列表
    Public McModLoader As New LoaderTask(Of String, List(Of ResourceFile))("Mod List Loader", AddressOf McModLoad)
    Private Sub McModLoad(Loader As LoaderTask(Of String, List(Of ResourceFile)))
        Try
            RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.ShowProgress = False)

            '等待 Mod 更新完成
            If PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input) Then
                Log($"[Mod] 等待 Mod 更新完成后才能继续加载 Mod 列表：" & Loader.Input)
                Try
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在更新 Mod")
                    Do Until Not PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input)
                        If Loader.IsInterrupted Then Return
                        Thread.Sleep(100)
                    Loop
                Finally
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在加载 Mod 列表")
                End Try
                FrmInstanceMod.LoaderRun(LoaderFolderRunType.UpdateOnly)
            End If

            '获取 Mod 文件夹下的可用文件列表
            Dim ModFileList As New List(Of FileInfo)
            If Directory.Exists(Loader.Input) Then
                Dim RawName As String = Loader.Input.Lower
                For Each File As FileInfo In EnumerateFiles(Loader.Input)
                    If File.DirectoryName.Lower & "\" <> RawName Then
                        '仅当 Forge 1.13- 且文件夹名与版本号相同时，才加载该子文件夹下的 Mod
                        If Not (PageInstanceLeft.Instance IsNot Nothing AndAlso PageInstanceLeft.Instance.Version.HasForge AndAlso
                                PageInstanceLeft.Instance.Version.Vanilla.Major < 13 AndAlso
                                File.Directory.Name = $"1.{PageInstanceLeft.Instance.Version.Vanilla.Major}.{PageInstanceLeft.Instance.Version.Vanilla.Build}") Then
                            Continue For
                        End If
                    End If
                    Static IsModFile As Func(Of String, Boolean) =
                    Function(FullName As String)
                        If FullName Is Nothing OrElse Not FullName.Contains(".") Then Return False
                        FullName = FullName.Lower
                        If FullName.EndsWithF(".jar", True) OrElse FullName.EndsWithF(".zip", True) OrElse FullName.EndsWithF(".litemod", True) OrElse
                            FullName.EndsWithF(".jar.disabled", True) OrElse FullName.EndsWithF(".zip.disabled", True) OrElse FullName.EndsWithF(".litemod.disabled", True) OrElse
                            FullName.EndsWithF(".jar.old", True) OrElse FullName.EndsWithF(".zip.old", True) OrElse FullName.EndsWithF(".litemod.old", True) Then Return True
                        Return False
                    End Function
                    If IsModFile(File.FullName) Then ModFileList.Add(File)
                Next
            End If

            '获取本地文件缓存
            Dim CachePath As String = PathTemp & "Cache\LocalMod.json"
            Dim Cache As New JObject
            Try
                Dim CacheContent As String = ReadFile(CachePath)
                If Not String.IsNullOrWhiteSpace(CacheContent) Then
                    Cache = GetJson(CacheContent)
                    If Not Cache.ContainsKey("version") OrElse Cache("version").ToObject(Of Integer) <> ResourceFileCacheVersion Then
                        Log($"[Mod] 本地 Mod 信息缓存版本已过期，将弃用这些缓存信息", NotifyLevel.DebugModeOnly)
                        Cache = New JObject
                    End If
                End If
            Catch ex As Exception
                Log(ex, "读取本地 Mod 信息缓存失败，已重置")
                Cache = New JObject
            End Try
            Cache("version") = ResourceFileCacheVersion

            '加载 Mod 列表
            Dim ModList As New List(Of ResourceFile)
            For Each ModFile As FileInfo In ModFileList
                If Loader.IsInterrupted Then Return
                Dim ModEntry As New ResourceFile(ModFile)
                Dim DumpMod As ResourceFile = ModList.FirstOrDefault(Function(m) m.EnabledName = ModEntry.EnabledName) '存在两个文件，名称相同，但一个启用一个禁用
                If DumpMod IsNot Nothing Then
                    Dim DisabledMod As ResourceFile = If(DumpMod.IsEnabled, ModEntry, DumpMod)
                    Log($"[Mod] 重复的 Mod 文件：{DumpMod.File.Name} 与 {ModEntry.File.Name}，已忽略 {DisabledMod.File.Name}", NotifyLevel.DebugModeOnly)
                    If DisabledMod Is ModEntry Then
                        Continue For
                    Else
                        ModList.Remove(DisabledMod)
                    End If
                End If
                ModList.Add(ModEntry)
            Next
            Log($"[Mod] 共发现 {ModList.Count} 个 Mod")

            '排序
            ModList = ModList.OrderBy(Function(m) m.File.Name).ToList

            '回设
            If Loader.IsInterrupted Then Return
            Loader.Output = ModList

            '开始联网加载
            'TODO: 添加信息获取中提示
            McModDetailLoader.Start(New KeyValuePair(Of List(Of ResourceFile), JObject)(ModList, Cache), IsForceRestart:=True)

        Catch ex As Exception
            Log(ex, "Mod 列表加载失败", NotifyLevel.DebugModeOnly)
            Throw
        End Try
    End Sub

    '联网加载 Mod 详情
    Public McModDetailLoader As New LoaderTask(Of KeyValuePair(Of List(Of ResourceFile), JObject), Integer)("Mod List Detail Loader", AddressOf McModDetailLoad)
    Private Sub McModDetailLoad(Loader As LoaderTask(Of KeyValuePair(Of List(Of ResourceFile), JObject), Integer))
        Dim Mods As New List(Of ResourceFile)
        Dim Cache As JObject = Loader.Input.Value
        Static GetTargetModLoaders As Func(Of List(Of ModLoaderTypes)) =
        Function() As List(Of ModLoaderTypes)
            Dim Loaders As New List(Of ModLoaderTypes)
            If PageInstanceLeft.Instance.Version.HasForge Then Loaders.Add(ModLoaderTypes.Forge)
            If PageInstanceLeft.Instance.Version.HasNeoForge Then Loaders.Add(ModLoaderTypes.NeoForge)
            If PageInstanceLeft.Instance.Version.HasFabric Then Loaders.Add(ModLoaderTypes.Fabric)
            If PageInstanceLeft.Instance.Version.HasLiteLoader Then Loaders.Add(ModLoaderTypes.LiteLoader)
            If Not Loaders.Any() Then Loaders.AddRange({ModLoaderTypes.Forge, ModLoaderTypes.NeoForge, ModLoaderTypes.Fabric, ModLoaderTypes.LiteLoader, ModLoaderTypes.Quilt})
            Return Loaders
        End Function
        '读取缓存，获取需要更新的 Mod 列表
        For Each ModEntry As ResourceFile In Loader.Input.Key
            If Loader.IsInterrupted Then Return
            Dim CacheKey = ModEntry.ModrinthHash & PageInstanceLeft.Instance.Version.VanillaName & GetTargetModLoaders().Join("")
            If Cache.ContainsKey(CacheKey) Then
                ModEntry.FromJson(Cache(CacheKey))
                '如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                If ModEntry.OnlineDataLoaded AndAlso Date.Now - Cache(CacheKey)("Project")("CacheTime").ToObject(Of Date) < New TimeSpan(6, 0, 0) Then Continue For
            End If
            Mods.Add(ModEntry)
        Next
        Log($"[Mod] 有 {Mods.Where(Function(m) m.Project Is Nothing).Count} 个 Mod 需要联网获取信息，{Mods.Where(Function(m) m.Project IsNot Nothing).Count} 个 Mod 需要更新信息")
        If Not Mods.Any Then
            Loader.Input.Key.Where(Function(m) m.Version Is Nothing).ForAll(Sub(m) m.LoadMetadataFromJar())  '从 JAR 中获取缺失的版本信息（下面有另一个分支）
            Return
        End If
        '获取作为检查目标的加载器和版本
        '此处不应向下扩展检查的 MC 小版本，例如 Mod 在更新 1.16.5 后，对早期的 1.16.2 版本发布了修补补丁，这会导致 PCL 将 1.16.5 版本的 Mod 降级到 1.16.2
        Dim TargetMcVersion As McVersion = PageInstanceLeft.Instance.Version
        Dim ModLoaders = GetTargetModLoaders()
        Dim VanillaVersion = TargetMcVersion.VanillaName
        '开始网络获取
        Log($"[Mod] 目标加载器：{ModLoaders.Join("/")}，版本：{VanillaVersion}")
        Dim EndedThreadCount As Integer = 0, IsFailed As Boolean = False
        Dim CurrentTaskThread As Thread = Thread.CurrentThread
        '从 Modrinth 获取信息
        RunInNewThread(
        Sub()
            Try
                '步骤 1：获取 Hash 与对应的工程 ID
                Dim ModrinthHashes = Mods.Select(Function(m) m.ModrinthHash).ToList()
                Dim ModrinthVersion As JObject = DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                    $"{{""hashes"": [""{ModrinthHashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")
                Log($"[Mod] 从 Modrinth 获取到 {ModrinthVersion.Count} 个本地 Mod 的对应信息")
                '步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                If ModrinthVersion.Count = 0 Then Return
                Dim ModrinthMapping As New Dictionary(Of String, List(Of ResourceFile))
                For Each Entry In Mods
                    If Not ModrinthVersion.ContainsKey(Entry.ModrinthHash) Then Continue For
                    If ModrinthVersion(Entry.ModrinthHash)("files")(0)("hashes")("sha1") <> Entry.ModrinthHash Then Continue For
                    Dim ProjectId = ModrinthVersion(Entry.ModrinthHash)("project_id").ToString
                    If ResourceProject.Cache.ContainsKey(ProjectId) AndAlso Entry.Project Is Nothing Then Entry.Project = ResourceProject.Cache(ProjectId) '读取已加载的缓存，加快结果出现速度
                    If Not ModrinthMapping.ContainsKey(ProjectId) Then ModrinthMapping(ProjectId) = New List(Of ResourceFile)
                    ModrinthMapping(ProjectId).Add(Entry)
                    '记录对应的 ProjectVersion
                    Dim File = ResourceVersion.FromPlatformJson(ModrinthVersion(Entry.ModrinthHash), ResourceTypes.Mod)
                    If Entry.ProjectVersion Is Nothing OrElse Entry.ProjectVersion.ReleaseDate < File.ReleaseDate Then
                        Entry.ProjectVersion = File
                    Else
                        Entry.ProjectVersion.Version = File.Version '使用来自 Modrinth 的版本号
                    End If
                Next
                If Loader.IsInterruptedWithThread(CurrentTaskThread) Then Return
                Log($"[Mod] 需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息")
                '步骤 3：获取工程信息
                If Not ModrinthMapping.Any() Then Return
                Dim ModrinthProject As JArray = DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthMapping.Keys.Join(""",""")}""]")
                For Each ProjectJson In ModrinthProject
                    Dim Project As New ResourceProject(ProjectJson)
                    For Each Entry In ModrinthMapping(Project.Id)
                        Entry.Project = Project
                    Next
                Next
                Log($"[Mod] 已从 Modrinth 获取本地 Mod 信息，继续获取更新信息")
                '步骤 4：获取更新信息
                Dim ModrinthUpdate As JObject = DlModRequest("https://api.modrinth.com/v2/version_files/update", HttpMethod.Post,
                    $"{{""hashes"": [""{ModrinthMapping.SelectMany(Function(l) l.Value.Select(Function(m) m.ModrinthHash)).Join(""",""")}""], ""algorithm"": ""sha1"", 
                    ""loaders"": [""{ModLoaders.Join(""",""").Lower}""],""game_versions"": [""{VanillaVersion}""]}}", "application/json")
                For Each Entry In Mods
                    If Not ModrinthUpdate.ContainsKey(Entry.ModrinthHash) OrElse Entry.ProjectVersion Is Nothing Then Continue For
                    Dim UpdateFile = ResourceVersion.FromPlatformJson(ModrinthUpdate(Entry.ModrinthHash), ResourceTypes.Mod)
                    If Not UpdateFile.DownloadAvailable Then Continue For
                    If ModeDebug Then Log($"[Mod] 本地文件 {Entry.ProjectVersion.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}")
                    If Entry.ProjectVersion.ReleaseDate >= UpdateFile.ReleaseDate OrElse Entry.ProjectVersion.Hash = UpdateFile.Hash Then Continue For
                    '设置更新日志与更新文件
                    If Entry.UpdateFile IsNot Nothing AndAlso UpdateFile.Hash = Entry.UpdateFile.Hash Then '合并
                        Entry.ChangelogUrls.Add($"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={VanillaVersion}")
                        UpdateFile.DownloadUrls.AddRange(Entry.UpdateFile.DownloadUrls) '合并下载源
                        Entry.UpdateFile = UpdateFile '优先使用 Modrinth 的文件
                    ElseIf Entry.UpdateFile Is Nothing OrElse UpdateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate Then '替换
                        Entry.ChangelogUrls = New List(Of String) From {$"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={VanillaVersion}"}
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
                    If Loader.IsInterruptedWithThread(CurrentTaskThread) Then Return
                Next
                Dim CurseForgeRaw As JContainer = DlModRequest("https://api.curseforge.com/v1/fingerprints/432", HttpMethod.Post,
                    $"{{""fingerprints"": [{CurseForgeHashes.Join(",")}]}}", "application/json")("data")("exactMatches")
                Log($"[Mod] 从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地 Mod 的对应信息")
                '步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                If Not CurseForgeRaw.Any() Then Return
                Dim CurseForgeMapping As New Dictionary(Of Integer, List(Of ResourceFile))
                For Each Project In CurseForgeRaw
                    Dim ProjectId = Project("id").ToString
                    Dim Hash As UInteger = Project("file")("fileFingerprint")
                    For Each Entry In Mods
                        If Entry.CurseForgeHash <> Hash Then Continue For
                        If ResourceProject.Cache.ContainsKey(ProjectId) AndAlso Entry.Project Is Nothing Then Entry.Project = ResourceProject.Cache(ProjectId) '读取已加载的缓存，加快结果出现速度
                        If Not CurseForgeMapping.ContainsKey(ProjectId) Then CurseForgeMapping(ProjectId) = New List(Of ResourceFile)
                        CurseForgeMapping(ProjectId).Add(Entry)
                        '记录对应的 ProjectVersion
                        Dim File = ResourceVersion.FromPlatformJson(Project("file"), ResourceTypes.Mod)
                        If Entry.ProjectVersion Is Nothing OrElse Entry.ProjectVersion.ReleaseDate < File.ReleaseDate Then Entry.ProjectVersion = File
                    Next
                Next
                If Loader.IsInterruptedWithThread(CurrentTaskThread) Then Return
                Log($"[Mod] 需要从 CurseForge 获取 {CurseForgeMapping.Count} 个本地 Mod 的工程信息")
                '步骤 3：获取工程信息
                If Not CurseForgeMapping.Any() Then Return
                Dim CurseForgeProject = DlModRequest("https://api.curseforge.com/v1/mods", HttpMethod.Post,
                    $"{{""modIds"": [{CurseForgeMapping.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFileIds As New Dictionary(Of Integer, List(Of ResourceFile)) 'FileId -> 本地 Mod 文件列表
                Dim FileIdToProjectSlug As New Dictionary(Of Integer, String)
                For Each ProjectJson In CurseForgeProject
                    If ProjectJson("isAvailable") IsNot Nothing AndAlso Not ProjectJson("isAvailable").ToObject(Of Boolean) Then Continue For
                    '设置 Entry 中的工程信息
                    Dim Project As New ResourceProject(ProjectJson)
                    For Each Entry In CurseForgeMapping(Project.Id) '倒查防止 CurseForge 返回的内容有漏
                        If Entry.Project IsNot Nothing AndAlso Entry.Project.Platform = ResourcePlatforms.Modrinth Then
                            Entry.Project = Entry.Project '再次触发修改事件
                            Continue For
                        End If
                        Entry.Project = Project
                    Next
                    '查找或许版本更新的文件列表
                    If ModLoaders.IsSingle Then
                        Dim NewestVersion As String = Nothing
                        Dim NewestFileIds As New List(Of Integer)
                        For Each IndexEntry In ProjectJson("latestFilesIndexes")
                            If IndexEntry("modLoader") Is Nothing OrElse ModLoaders.Single <> IndexEntry("modLoader").ToObject(Of Integer) Then Continue For 'ModLoader 唯一且匹配
                            Dim IndexVersion As String = IndexEntry("gameVersion")
                            If IndexVersion <> VanillaVersion Then Continue For 'MC 版本匹配
                            '由于 latestFilesIndexes 是按时间从新到老排序的，所以只需取第一个；如果需要检查多个 releaseType 下的文件，将 > -1 改为 = 1，但这应当并不会获取到更新的文件
                            If NewestVersion IsNot Nothing AndAlso CompareVersion(NewestVersion, IndexVersion) > -1 Then Continue For '只保留最新 MC 版本
                            If NewestVersion <> IndexVersion Then
                                NewestVersion = IndexVersion
                                NewestFileIds.Clear()
                            End If
                            NewestFileIds.Add(IndexEntry("fileId").ToObject(Of Integer))
                        Next
                        For Each FileId In NewestFileIds
                            If Not UpdateFileIds.ContainsKey(FileId) Then UpdateFileIds(FileId) = New List(Of ResourceFile)
                            UpdateFileIds(FileId).AddRange(CurseForgeMapping(Project.Id))
                            FileIdToProjectSlug(FileId) = Project.Slug
                        Next
                    End If
                Next
                Log($"[Mod] 已从 CurseForge 获取本地 Mod 信息，需要获取 {UpdateFileIds.Count} 个用于检查更新的文件信息")
                '步骤 4：获取更新文件信息
                If Not UpdateFileIds.Any() Then Return
                Dim CurseForgeFiles = DlModRequest("https://api.curseforge.com/v1/mods/files", HttpMethod.Post,
                                    $"{{""fileIds"": [{UpdateFileIds.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFiles As New Dictionary(Of ResourceFile, ResourceVersion)
                For Each FileJson In CurseForgeFiles
                    Dim File = ResourceVersion.FromPlatformJson(FileJson, ResourceTypes.Mod)
                    If Not File.DownloadAvailable Then Continue For
                    For Each Entry As ResourceFile In UpdateFileIds(File.Id)
                        If UpdateFiles.ContainsKey(Entry) AndAlso UpdateFiles(Entry).ReleaseDate >= File.ReleaseDate Then Continue For
                        UpdateFiles(Entry) = File
                    Next
                Next
                For Each Pair In UpdateFiles
                    Dim Entry As ResourceFile = Pair.Key
                    Dim UpdateFile As ResourceVersion = Pair.Value
                    If ModeDebug Then Log($"[Mod] 本地文件 {Entry.ProjectVersion.FileName} 在 CurseForge 上的最新版为 {UpdateFile.FileName}")
                    If Entry.ProjectVersion.ReleaseDate >= UpdateFile.ReleaseDate OrElse Entry.ProjectVersion.Hash = UpdateFile.Hash Then Continue For
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
        '从 JAR 中获取缺失的版本信息（上面有另一个分支）
        Loader.Input.Key.Where(Function(m) m.Version Is Nothing).ForAll(Sub(m) m.LoadMetadataFromJar())
        '等待线程结束
        Do Until EndedThreadCount = 2
            Thread.Sleep(10)
            If Loader.IsInterrupted Then Return
        Loop
        '保存缓存
        Mods = Mods.Where(Function(m) m.Project IsNot Nothing).ToList()
        Log($"[Mod] 联网获取本地 Mod 信息完成，为 {Mods.Count} 个 Mod 更新缓存")
        If Not Mods.Any() Then Return
        For Each Entry In Mods
            Entry.OnlineDataLoaded = Not IsFailed
            Cache(Entry.ModrinthHash & VanillaVersion & ModLoaders.Join("")) = Entry.ToJson()
        Next
        FileUtils.Write(PathTemp & "Cache\LocalMod.json", Cache.ToString(If(ModeDebug, Newtonsoft.Json.Formatting.Indented, Newtonsoft.Json.Formatting.None)))
        '刷新边栏
        If Loader.IsInterrupted Then Return
        If FrmInstanceMod?.Filter = PageInstanceMod.FilterType.CanUpdate Then
            RunInUi(Sub() FrmInstanceMod?.RefreshUI()) '同步 “可更新” 列表 (#4677)
        Else
            RunInUi(Sub() FrmInstanceMod?.RefreshBars())
        End If
    End Sub

End Module