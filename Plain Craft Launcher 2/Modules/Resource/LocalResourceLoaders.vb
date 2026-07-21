Public Module LocalResourceLoaders

    '加载 Mod 列表
    Public LocalResourceLoader As New LoaderTask(Of String, List(Of LocalResourceFile))("LocalResourceLoader", AddressOf LocalResourceFileLoad)
    Private Sub LocalResourceFileLoad(Loader As LoaderTask(Of String, List(Of LocalResourceFile)))
        Try
            RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.ShowProgress = False)

            '等待 Mod 更新完成
            If PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input) Then
                Logger.Info($"等待 Mod 更新完成后才能继续加载 Mod 列表：{Loader.Input}")
                Try
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在更新 Mod")
                    Do Until Not PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input)
                        If Loader.IsCanceled Then Return
                        Thread.Sleep(100)
                    Loop
                Finally
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在加载 Mod 列表")
                End Try
                FrmInstanceMod.LoaderRun(LoaderFolderRunType.UpdateOnly)
            End If

            '获取 Mod 文件夹下的可用文件列表
            Dim ModFileList As New List(Of String)
            If DirectoryUtils.Exists(Loader.Input) Then
                Dim RawName As String = Loader.Input.Lower
                For Each File In DirectoryUtils.EnumerateFiles(Loader.Input, True)
                    If File.BeforeLast("\").Lower & "\" <> RawName Then
                        '仅当 Forge 1.13- 且文件夹名与版本号相同时，才加载该子文件夹下的 Mod
                        If Not (PageInstanceLeft.Instance IsNot Nothing AndAlso PageInstanceLeft.Instance.Version.HasForge AndAlso
                                PageInstanceLeft.Instance.Version.Vanilla.Major < 13 AndAlso
                                PathUtils.GetLastPart(PathUtils.RemoveLastPart(File)) = $"1.{PageInstanceLeft.Instance.Version.Vanilla.Major}.{PageInstanceLeft.Instance.Version.Vanilla.Build}") Then
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
                    If IsModFile(File) Then ModFileList.Add(File)
                Next
            End If

            '获取本地文件缓存
            Dim CachePath As String = PathTemp & "Cache\LocalMod.json"
            Dim Cache As New JObject
            Try
                Dim CacheContent As String = FileUtils.TryReadAsString(CachePath)
                If Not String.IsNullOrWhiteSpace(CacheContent) Then
                    Cache = CacheContent.DeserializeJson()
                    If Not Cache.ContainsKey("version") OrElse Cache("version").ToObject(Of Integer) <> Versions.LocalModCacheVersion Then
                        Logger.Warn($"本地 Mod 信息缓存版本已过期，将弃用这些缓存信息")
                        Cache = New JObject
                    End If
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取本地 Mod 信息缓存失败，已重置")
                Cache = New JObject
            End Try
            Cache("version") = Versions.LocalModCacheVersion

            '加载 Mod 列表
            Dim ModList As New List(Of LocalResourceFile)
            For Each ModFile In ModFileList
                If Loader.IsCanceled Then Return
                Dim ModEntry As New LocalResourceFile(ModFile)
                Dim DumpMod As LocalResourceFile = ModList.FirstOrDefault(Function(m) m.EnabledName = ModEntry.EnabledName) '存在两个文件，名称相同，但一个启用一个禁用
                If DumpMod IsNot Nothing Then
                    Dim DisabledMod As LocalResourceFile = If(DumpMod.IsEnabled, ModEntry, DumpMod)
                    Logger.Warn($"重复的 Mod 文件：{DumpMod.File.Name} 与 {ModEntry.File.Name}，已忽略 {DisabledMod.File.Name}")
                    If DisabledMod Is ModEntry Then
                        Continue For
                    Else
                        ModList.Remove(DisabledMod)
                    End If
                End If
                ModList.Add(ModEntry)
            Next
            Logger.Info($"共发现 {ModList.Count} 个 Mod")

            '排序
            ModList = ModList.OrderBy(Function(m) m.File.Name).ToList

            '回设
            If Loader.IsCanceled Then Return
            Loader.Output = ModList

            '开始联网加载
            'TODO: 添加信息获取中提示
            LocalResourceOnlineLoader.Start((ModList, Cache, PageInstanceLeft.Instance.Version), IsForceRestart:=True)

        Catch ex As Exception
            Logger.Warn(ex, "Mod 列表加载失败")
            Throw
        End Try
    End Sub

    '联网加载 Mod 详情
    Public LocalResourceOnlineLoader As New LoaderTask(Of (ModList As List(Of LocalResourceFile), Cache As JObject, Target As McVersion), Integer)("LocalResourceOnlineLoader", AddressOf LocalResourceOnlineLoad)
    Private Sub LocalResourceOnlineLoad(Loader As LoaderTask(Of (ModList As List(Of LocalResourceFile), Cache As JObject, Target As McVersion), Integer))
        Dim Mods As New List(Of LocalResourceFile)
        Dim Cache As JObject = Loader.Input.Cache
        Dim ModLoaders = Loader.Input.Target.ModLoaders
        '读取缓存，获取需要更新的 Mod 列表
        For Each ModEntry As LocalResourceFile In Loader.Input.ModList
            If Loader.IsCanceled Then Return
            Dim CacheKey = ModEntry.ModrinthHash & Loader.Input.Target.VanillaName & ModLoaders
            If Cache.ContainsKey(CacheKey) Then
                ModEntry.FromJson(Cache(CacheKey))
                '如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                If ModEntry.OnlineDataLoaded AndAlso Date.Now - Cache(CacheKey)("Project")("CacheTime").ToObject(Of Date) < New TimeSpan(6, 0, 0) Then Continue For
            End If
            Mods.Add(ModEntry)
        Next
        Logger.Info($"有 {Mods.Where(Function(m) m.Project Is Nothing).Count} 个 Mod 需要联网获取信息，{Mods.Where(Function(m) m.Project IsNot Nothing).Count} 个 Mod 需要更新信息")
        If Not Mods.Any Then Return
        '获取作为检查目标的加载器和版本
        '此处不应向下扩展检查的 MC 小版本，例如 Mod 在更新 1.16.5 后，对早期的 1.16.2 版本发布了修补补丁，这会导致 PCL 将 1.16.5 版本的 Mod 降级到 1.16.2
        Dim TargetMcVersion As McVersion = Loader.Input.Target
        Dim VanillaVersion = TargetMcVersion.VanillaName
        '开始网络获取
        Logger.Info($"目标加载器：{ModLoaders}，版本：{VanillaVersion}")
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
                Logger.Info($"从 Modrinth 获取到 {ModrinthVersion.Count} 个本地 Mod 的对应信息")
                '步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                If ModrinthVersion.Count = 0 Then Return
                Dim ModrinthMapping As New Dictionary(Of String, List(Of LocalResourceFile))
                For Each Entry In Mods
                    If Not ModrinthVersion.ContainsKey(Entry.ModrinthHash) Then Continue For
                    If ModrinthVersion(Entry.ModrinthHash)("files")(0)("hashes")("sha1") <> Entry.ModrinthHash Then Continue For
                    Dim ProjectId = ModrinthVersion(Entry.ModrinthHash)("project_id").ToString
                    If ResourceProject.Cache.ContainsKey(ProjectId) AndAlso Entry.Project Is Nothing Then Entry.Project = ResourceProject.Cache(ProjectId) '读取已加载的缓存，加快结果出现速度
                    If Not ModrinthMapping.ContainsKey(ProjectId) Then ModrinthMapping(ProjectId) = New List(Of LocalResourceFile)
                    ModrinthMapping(ProjectId).Add(Entry)
                    '记录对应的 ProjectVersion
                    Dim File = ResourceVersion.FromPlatformJson(ModrinthVersion(Entry.ModrinthHash), ResourceTypes.Mod)
                    If Entry.ProjectVersion Is Nothing OrElse Entry.ProjectVersion.ReleaseDate < File.ReleaseDate Then
                        Entry.ProjectVersion = File
                    Else
                        Entry.ProjectVersion.Version = File.Version '使用来自 Modrinth 的版本号
                    End If
                Next
                If Loader.IsCanceledWithThread(CurrentTaskThread) Then Return
                Logger.Info($"需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息")
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
                Logger.Info($"已从 Modrinth 获取本地 Mod 信息，继续获取更新信息")
                '步骤 4：获取更新信息
                If ModLoaders = ModLoaders.None Then
                    Logger.Warn("该 Minecraft 版本没有可用的 Mod 加载器，不获取 Mod 更新信息")
                    Return
                End If
                Dim ModrinthUpdate As JObject = DlModRequest("https://api.modrinth.com/v2/version_files/update", HttpMethod.Post,
                    $"{{""hashes"": [""{ModrinthMapping.SelectMany(Function(l) l.Value.Select(Function(m) m.ModrinthHash)).Join(""",""")}""], ""algorithm"": ""sha1"", 
                    ""loaders"": [""{ModLoaders.Flags.Join(""",""").Lower}""],""game_versions"": [""{VanillaVersion}""]}}", "application/json")
                For Each Entry In Mods
                    If Not ModrinthUpdate.ContainsKey(Entry.ModrinthHash) OrElse Entry.ProjectVersion Is Nothing Then Continue For
                    Dim UpdateFile = ResourceVersion.FromPlatformJson(ModrinthUpdate(Entry.ModrinthHash), ResourceTypes.Mod)
                    If Not UpdateFile.DownloadAvailable Then Continue For
                    Logger.Trace(Function() $"本地文件 {Entry.ProjectVersion.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}")
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
                Logger.Info($"从 Modrinth 获取本地 Mod 信息结束")
            Catch ex As Exception
                Logger.Warn(ex, "从 Modrinth 获取本地 Mod 信息失败")
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
                Dim Hashes As New List(Of UInteger)
                For Each Entry In Mods
                    Hashes.Add(Entry.CurseForgeHash)
                    If Loader.IsCanceledWithThread(CurrentTaskThread) Then Return
                Next
                Dim CurseForgeRaw As JContainer = DlModRequest("https://api.curseforge.com/v1/fingerprints/432", HttpMethod.Post,
                    $"{{""fingerprints"": [{Hashes.Join(",")}]}}", "application/json")("data")("exactMatches")
                Logger.Info($"从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地 Mod 的对应信息")
                If Not CurseForgeRaw.Any() Then Return

                '步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                Dim ProjectIdToLocalFiles As New Dictionary(Of Integer, List(Of LocalResourceFile))
                For Each Project In CurseForgeRaw
                    Dim ProjectId = Project("id").ToString
                    Dim Hash As UInteger = Project("file")("fileFingerprint")
                    For Each Entry In Mods
                        If Entry.CurseForgeHash <> Hash Then Continue For
                        If ResourceProject.Cache.ContainsKey(ProjectId) AndAlso Entry.Project Is Nothing Then Entry.Project = ResourceProject.Cache(ProjectId) '读取已加载的缓存，加快结果出现速度
                        ProjectIdToLocalFiles.AddIntoValueCollection(ProjectId, Entry)
                        '记录对应的 ProjectVersion
                        Dim File = ResourceVersion.FromPlatformJson(Project("file"), ResourceTypes.Mod)
                        If Entry.ProjectVersion Is Nothing OrElse Entry.ProjectVersion.ReleaseDate < File.ReleaseDate Then Entry.ProjectVersion = File
                    Next
                Next
                If Loader.IsCanceledWithThread(CurrentTaskThread) Then Return
                Logger.Info($"需要从 CurseForge 获取 {ProjectIdToLocalFiles.Count} 个本地 Mod 的工程信息")
                If Not ProjectIdToLocalFiles.Any() Then Return

                '步骤 3：获取工程信息
                Dim CurseForgeProject = DlModRequest("https://api.curseforge.com/v1/mods", HttpMethod.Post,
                    $"{{""modIds"": [{ProjectIdToLocalFiles.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFileIds As New Dictionary(Of Integer, List(Of LocalResourceFile)) 'FileId -> 本地 Mod 文件列表
                Dim FileIdToProjectSlug As New Dictionary(Of Integer, String)
                For Each ProjectJson In CurseForgeProject
                    If ProjectJson("isAvailable") IsNot Nothing AndAlso Not ProjectJson("isAvailable").ToObject(Of Boolean) Then Continue For
                    '设置 Entry 中的工程信息
                    Dim Project As New ResourceProject(ProjectJson)
                    Dim TargetLoaders As ModLoaders = ModLoaders.None
                    For Each LocalFile In ProjectIdToLocalFiles(Project.Id) '倒查防止 CurseForge 返回的内容有漏
                        If LocalFile.Project?.Platform = ResourcePlatforms.Modrinth Then
                            LocalFile.Project = LocalFile.Project '再次触发修改事件
                        Else
                            LocalFile.Project = Project
                            TargetLoaders = TargetLoaders Or LocalFile.Project.ModLoaders
                        End If
                    Next
                    TargetLoaders = TargetLoaders And ModLoaders '与目标 MC 版本的加载器取交集
                    '查找或许版本更新的文件列表
                    If ModLoaders = ModLoaders.None Then Continue For '无法判断 MC 版本使用的加载器，跳过更新检查
                    Dim LatestFiles = ProjectJson("latestFilesIndexes").Where(
                    Function(Entry)
                        'Mod Loader 匹配
                        If Entry("modLoader") Is Nothing Then Return False
                        Dim EntryLoader As ModLoaders = Resource.FromCurseForgeModLoaderType(Entry("modLoader").ToObject(Of Integer))
                        If Not TargetLoaders.HasFlag(EntryLoader) Then Return False
                        'MC 版本匹配
                        Dim EntryVersion As String = Entry("gameVersion")
                        Return EntryVersion IsNot Nothing AndAlso EntryVersion = VanillaVersion
                    End Function).
                    MaxByAll(Function(Entry) Entry("gameVersion").ToString, New VersionComparer) '只保留最新的 MC 版本
                    For Each Entry In LatestFiles
                        Dim FileId = Entry("fileId").ToObject(Of Integer)
                        If Not UpdateFileIds.ContainsKey(FileId) Then UpdateFileIds(FileId) = New List(Of LocalResourceFile)
                        UpdateFileIds(FileId).AddRange(ProjectIdToLocalFiles(Project.Id))
                        FileIdToProjectSlug(FileId) = Project.Slug
                    Next
                Next
                Logger.Info($"已从 CurseForge 获取本地 Mod 信息，需要获取 {UpdateFileIds.Count} 个用于检查更新的文件信息")
                If Not UpdateFileIds.Any() Then Return

                '步骤 4：获取更新文件信息
                Dim CurseForgeFiles = DlModRequest("https://api.curseforge.com/v1/mods/files", HttpMethod.Post,
                                    $"{{""fileIds"": [{UpdateFileIds.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFiles As New Dictionary(Of LocalResourceFile, ResourceVersion)
                For Each FileJson In CurseForgeFiles
                    Dim File = ResourceVersion.FromPlatformJson(FileJson, ResourceTypes.Mod)
                    If Not File.DownloadAvailable Then Continue For
                    For Each Entry As LocalResourceFile In UpdateFileIds(File.Id)
                        If UpdateFiles.ContainsKey(Entry) AndAlso UpdateFiles(Entry).ReleaseDate >= File.ReleaseDate Then Continue For
                        UpdateFiles(Entry) = File
                    Next
                Next
                For Each Pair In UpdateFiles
                    Dim Entry As LocalResourceFile = Pair.Key
                    Dim UpdateFile As ResourceVersion = Pair.Value
                    Logger.Trace(Function() $"本地文件 {Entry.ProjectVersion.FileName} 在 CurseForge 上的最新版为 {UpdateFile.FileName}")
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

                Logger.Info($"从 CurseForge 获取 Mod 更新信息结束")
            Catch ex As Exception
                Logger.Warn(ex, "从 CurseForge 获取本地 Mod 信息失败")
                IsFailed = True
            Finally
                EndedThreadCount += 1
            End Try
        End Sub, "Mod List Detail Loader CurseForge")
        '等待线程结束
        Do Until EndedThreadCount = 2
            Thread.Sleep(10)
            If Loader.IsCanceled Then Return
        Loop
        '保存缓存
        Mods = Mods.Where(Function(m) m.Project IsNot Nothing).ToList()
        Logger.Info($"联网获取本地 Mod 信息完成，为 {Mods.Count} 个 Mod 更新缓存")
        If Not Mods.Any() Then Return
        If Loader.IsCanceled Then Return
        For Each Entry In Mods
            Entry.OnlineDataLoaded = Not IsFailed
            Cache(Entry.ModrinthHash & VanillaVersion & ModLoaders) = Entry.ToJson()
        Next
        FileUtils.Write(PathTemp & "Cache\LocalMod.json", Cache.ToString(If(ModeDebug, Newtonsoft.Json.Formatting.Indented, Newtonsoft.Json.Formatting.None)))
        '刷新边栏
        If Loader.IsCanceled Then Return
        If FrmInstanceMod?.Filter = PageInstanceMod.FilterType.CanUpdate Then
            RunInUi(Sub() FrmInstanceMod?.RefreshUI()) '同步 “可更新” 列表 (#4677)
        Else
            RunInUi(Sub() FrmInstanceMod?.RefreshBars())
        End If
    End Sub

End Module
