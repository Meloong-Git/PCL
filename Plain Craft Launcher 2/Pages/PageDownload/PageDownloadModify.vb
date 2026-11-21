''' <summary>
''' 版本修改页：完整还原已安装组件后，复用安装页的选择交互。
''' </summary>
Public Class PageDownloadModify
    Inherits PageDownloadInstall

    Private InitLoader As LoaderTask(Of McInstance, Object)
    Private ForgeVersionLoader As LoaderTask(Of String, List(Of DlForgeVersionEntry))
    Private InitialTarget As McComponents '进入时的组件快照
    ''' <summary>
    ''' 原版的版本号是否可靠（不是通过 json 模糊分析）。
    ''' </summary>
    Private IsVanillaVersionReliable As Boolean
    Private OriginalName As String
    Private CustomLogo As String
    ''' <summary>
    ''' 开始修改 MC 版本时，记录当前选择的组件，以在修改 MC 版本后尝试自动选中相同组件。
    ''' </summary>
    Private ReadOnly PendingComponents As New Dictionary(Of MyLoading, String)
    Private IsRestoringComponents As Boolean

    Public Overrides Sub LoaderInit() '父级 Handles Me.Initialized
        ControlInit()
        InitLoader = New LoaderTask(Of McInstance, Object)("初始化版本修改", AddressOf LoadInstance)
        PageLoaderInit(LoadMinecraft, PanLoad, PanAllBack, Nothing, InitLoader, AddressOf LoadInstanceFinished, Function() PageInstanceLeft.Instance)
    End Sub
    Public Overrides Sub Init() '父级 Handles Me.Loaded
        PanBack.ScrollToHome()
        Static IsLoad As Boolean = False
        If IsLoad Then PageLoaderRestart()
        IsLoad = True
    End Sub
    Private Sub PageUnloaded() Handles Me.Unloaded
        InitLoader.Cancel()
    End Sub

    ''' <summary>
    ''' 读取或初始化版本的安装清单。
    ''' </summary>
    Private Sub LoadInstance(Task As LoaderTask(Of McInstance, Object))

        '旧状态清理
        RunInUiWait(
        Sub()
            AniStop("FrmDownloadInstall SelectPageSwitch")
            IsSelectNameEdited = False
            ClearSelected()
            PendingComponents.Clear()
            InitialTarget = Nothing
            IsInSelectPage = False
            PanAllBack.IsHitTestVisible = False
        End Sub)
        Task.Input.Config.DiscardCache()

        '获取 Components
        Dim Manifest = Configs.McInstallManifest.Get(Task.Input.Config)
        If Manifest IsNot Nothing AndAlso Manifest.SchemaVersion > Versions.McInstallManifestVersion Then
            Logger.Warn($"安装清单由更新版本的 PCL 创建（清单版本：{Manifest.SchemaVersion}，当前版本：{Versions.McInstallManifestVersion}）")
            Throw New Exception($"更新 PCL 后才能修改它！点击【设置 → 其他 → 检查更新】以更新。")
        End If
        If Manifest IsNot Nothing Then
            Dim LatestManifest = Manifest.RefreshModComponents(Task.Input.PathIndie)
            If LatestManifest IsNot Manifest Then
                Manifest = LatestManifest
                Configs.McInstallManifest.Set(Manifest, Task.Input.Config)
                Task.Input.Config.Save()
            End If
        End If
        Dim Components As McComponents
        Dim OptiFineVersion As String = Nothing
        If Manifest IsNot Nothing Then
            '有清单：从清单读取
            Components = Manifest.Components
            If Components Is Nothing Then Throw New Exception("安装清单缺少组件信息，无法还原已安装组件。")
            IsVanillaVersionReliable = True
        Else
            '无清单：通过版本 JSON 获取
            Dim VersionInfo = New McInstance(Task.Input.PathVersion).Version
            If VersionInfo.VanillaName = "Unknown" Then Throw New Exception("无法确定 Minecraft 版本，无法修改")
            Dim UnknownLoaders = VersionInfo.ModLoaderVersions.
                Where(Function(Loader) String.IsNullOrWhiteSpace(Loader.Value) OrElse Loader.Value = "未知版本").
                Select(Function(Loader) Loader.Key.ToString()).ToList()
            If UnknownLoaders.Any() Then Throw New Exception($"无法确定 {UnknownLoaders.Join("、")} 的版本，无法修改")
            Components = New McComponents(VersionInfo.VanillaName, VersionInfo.ModLoaderVersions)
            OptiFineVersion = VersionInfo.OptiFine
            IsVanillaVersionReliable = VersionInfo.Reliable
        End If
        Dim MinecraftVersion = Components.MinecraftVersion
        If String.IsNullOrWhiteSpace(MinecraftVersion) Then Throw New Exception("无法确定当前 Minecraft 版本，请检查版本文件")
        '等待子列表加载器完成获取
        Dim LoaderList As New Dictionary(Of String, LoaderBase) From {
            {"Minecraft", DlClientListLoader}, {"OptiFine", DlOptiFineListLoader}, {"LiteLoader", DlLiteLoaderListLoader},
            {"NeoForge", DlNeoForgeListLoader}, {"Fabric", DlFabricListLoader}, {"Fabric API", DlFabricApiLoader}, {"OptiFabric", DlOptiFabricLoader}}
        Dim ForgeLoader As LoaderTask(Of String, List(Of DlForgeVersionEntry)) = Nothing
        If McVersion.IsStandardFormat(MinecraftVersion) Then
            ForgeLoader = New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion " & MinecraftVersion, AddressOf DlForgeVersionMain)
            LoaderList.Add("Forge", ForgeLoader)
        End If
        For Each Loader In LoaderList.Values
            Loader.Start(If(Loader Is ForgeLoader, MinecraftVersion, Nothing), Task.IsForceRestarting)
        Next
        Do
            If Task.IsCanceled Then Return
            For Each Pair In LoaderList
                Dim Loader = Pair.Value
                If Loader.State = LoadState.Failed Then
                    If Loader Is ForgeLoader AndAlso Loader.Error.Message = "无" Then Continue For 'Forge 无版本时不报错
                    Throw New Exception($"获取 {Pair.Key} 版本列表失败，请稍后再试，或使用 VPN 改善网络环境", Loader.Error)
                ElseIf Loader.State = LoadState.Canceled Then
                    Throw New Exception($"获取 {Pair.Key} 版本列表被中断，请稍后再试，或使用 VPN 改善网络环境")
                End If
            Next
            If LoaderList.Values.All(Function(Loader) Loader.State = LoadState.Finished OrElse Loader.State = LoadState.Failed) Then Exit Do
            Task.Progress = 0.7 * LoaderList.Values.Average(Function(Loader) Loader.Progress)
            Thread.Sleep(10)
        Loop
        If Task.IsCanceled Then Return
        Task.Progress = 0.7
        'Minecraft
        MinecraftVersions = CType(DlClientListLoader.Output.Value("versions").DeepClone(), JArray)
        VanillaData = MinecraftVersions.OfType(Of JObject).FirstOrDefault(
            Function(v) v("id").ToString.Replace("∞", "infinite") = MinecraftVersion.Replace("∞", "infinite"))
        If VanillaData Is Nothing Then Throw New Exception($"Minecraft 列表中没有已安装的 {MinecraftVersion}")
        VanillaData("id") = MinecraftVersion
        'OptiFine
        If Manifest IsNot Nothing Then
            SelectedOptiFine = Components.OptiFineEntry?.ToObject(Of DlOptiFineListEntry)()
            If SelectedOptiFine IsNot Nothing AndAlso Not DlOptiFineListLoader.Output.Value.Any(Function(v) v.FileName = SelectedOptiFine.FileName) Then
                '如果 OptiFine 是 pre 版，可能只在 BMCLAPI 下有，官方源删了。如果是这种缺失数据的情况，换用 BMCLAPI 源作为输出
                If DlOptiFineListBmclapiLoader.State <> LoadState.Finished Then DlOptiFineListBmclapiLoader.WaitForExit()
                DlOptiFineListLoader.Output = DlOptiFineListBmclapiLoader.Output
            End If
        ElseIf OptiFineVersion IsNot Nothing AndAlso OptiFineVersion <> "未知版本" Then
            SelectedOptiFine = DlOptiFineListLoader.Output.Value.FirstOrDefault(
                Function(v) v.Inherit = MinecraftVersion AndAlso
                    v.InstanceName.AfterLast("-OptiFine_").Replace("HD_U_", "") = OptiFineVersion.Replace("HD_U_", ""))
            If SelectedOptiFine Is Nothing Then Throw New Exception($"OptiFine 列表中没有已安装的 {OptiFineVersion}")
        End If
        'Forge
        If Components.ModLoaderVersions.ContainsKey(ModLoaders.Forge) Then
            SelectedForge = ForgeLoader?.Output?.FirstOrDefault(
                Function(v) v.Inherit = MinecraftVersion AndAlso v.VersionName = Components.ModLoaderVersions(ModLoaders.Forge) AndAlso
                            v.Category <> "universal" AndAlso v.Category <> "client" AndAlso (SelectedOptiFine Is Nothing OrElse IsOptiFineSuitForForge(SelectedOptiFine, v)))
            If SelectedForge Is Nothing Then Throw New Exception($"Forge 列表中没有已安装的 {Components.ModLoaderVersions(ModLoaders.Forge)}")
        End If
        'NeoForge
        If Components.ModLoaderVersions.ContainsKey(ModLoaders.NeoForge) Then
            SelectedNeoForge = DlNeoForgeListLoader.Output.Value.FirstOrDefault(
                Function(v) v.Inherit = MinecraftVersion AndAlso v.VersionName = Components.ModLoaderVersions(ModLoaders.NeoForge))
            If SelectedNeoForge Is Nothing Then Throw New Exception($"NeoForge 列表中没有已安装的 {Components.ModLoaderVersions(ModLoaders.NeoForge)}")
        End If
        'Fabric
        If Components.ModLoaderVersions.ContainsKey(ModLoaders.Fabric) Then
            Dim FabricEntry = DlFabricListLoader.Output.Value("loader").OfType(Of JObject).FirstOrDefault(
                Function(v) v("version").ToString = Components.ModLoaderVersions(ModLoaders.Fabric),
                Function(v) v("version").ToString.Replace("+build", "") = Components.ModLoaderVersions(ModLoaders.Fabric))
            If FabricEntry Is Nothing OrElse Not DlFabricListLoader.Output.Value("game").OfType(Of JObject).
                    Any(Function(v) v("version").ToString = MinecraftVersion.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3")) Then
                Throw New Exception($"Fabric 列表中没有已安装的 {Components.ModLoaderVersions(ModLoaders.Fabric)}")
            End If
            SelectedFabric = FabricEntry("version").ToString
        End If
        'LiteLoader
        If Components.ModLoaderVersions.ContainsKey(ModLoaders.LiteLoader) Then
            SelectedLiteLoader = DlLiteLoaderListLoader.Output.Value.FirstOrDefault(
                Function(v) v.Inherit = MinecraftVersion AndAlso v.JsonToken("version").ToString = Components.ModLoaderVersions(ModLoaders.LiteLoader))
            If SelectedLiteLoader Is Nothing Then Throw New Exception($"LiteLoader 列表中没有已安装的 {Components.ModLoaderVersions(ModLoaders.LiteLoader)}")
        End If
        'Fabric API
        If Components.FabricApiVersion IsNot Nothing Then
            SelectedFabricApi = DlFabricApiLoader.Output.FirstOrDefault(
                Function(v) (v.Version = Components.FabricApiVersion OrElse v.Id = Components.FabricApiVersion OrElse
                             v.Display = Components.FabricApiVersion) AndAlso IsFabricApiCompatible(v, MinecraftVersion))
            If SelectedFabricApi Is Nothing Then Throw New Exception($"Fabric API 列表中没有已安装的 {Components.FabricApiVersion}")
        End If
        'OptiFabric
        If Components.OptiFabricVersion IsNot Nothing Then
            SelectedOptiFabric = DlOptiFabricLoader.Output.FirstOrDefault(
                Function(v) (v.Version = Components.OptiFabricVersion OrElse v.Id = Components.OptiFabricVersion OrElse
                             v.Display = Components.OptiFabricVersion) AndAlso IsOptiFabricCompatible(v, MinecraftVersion))
            If SelectedOptiFabric Is Nothing Then Throw New Exception($"OptiFabric 列表中没有已安装的 {Components.OptiFabricVersion}")
        End If

        '扫描尚未识别的 Mod 组件
        Dim ManagedFiles As New List(Of McInstallManifest.ManagedFile)
        If Manifest IsNot Nothing Then ManagedFiles.AddRange(Manifest.ManagedFiles)

#Region "从 mods 文件夹中获取以 Mod 形式安装的组件信息"

        '获取 mods 文件夹下的 jar 文件与 SHA1
        Dim Mods As New Lazy(Of List(Of (File As FileInfo, Sha1 As String)))(
            Function()
                Dim ModsFolder = Task.Input.PathIndie & "mods\"
                Dim Files = DirectoryUtils.EnumerateFiles(ModsFolder, searchPattern:="*.jar").ToList()
                If SelectedLiteLoader IsNot Nothing Then 'LiteLoader 的 OptiFine 安装目录
                    Files.AddRange(DirectoryUtils.EnumerateFiles(Path.Combine(ModsFolder, MinecraftVersion), searchPattern:="*.jar"))
                End If
                Dim Entries As New List(Of (File As FileInfo, Sha1 As String))
                For Each FilePath In Files
                    If Task.IsCanceled Then Return Entries
                    Dim File = FileUtils.GetInfo(FilePath)
                    Entries.Add((File, New LocalResourceFile(File).ModrinthHash))
                    Task.Progress = 0.7 + 0.28 * Entries.Count / Files.Count
                Next
                Return Entries
            End Function)
        'Fabric API
        If SelectedFabric IsNot Nothing AndAlso SelectedFabricApi Is Nothing Then
            Dim FabricApiList = DlFabricApiLoader.Output.Where(Function(v) IsFabricApiCompatible(v, MinecraftVersion)).ToList()
            Dim Matches As New List(Of (File As FileInfo, Sha1 As String, Version As ResourceVersion))
            For Each Entry In Mods.Value
                Dim Match = FabricApiList.FirstOrDefault(Function(v) v.Hash = Entry.Sha1)
                If Match Is Nothing Then Continue For
                Matches.Add((Entry.File, Entry.Sha1, Match))
            Next
            Dim SelectedFile = SelectMod("Fabric API", Matches)
            If SelectedFile IsNot Nothing Then
                SelectedFabricApi = SelectedFile.Value.Version
                ManagedFiles.Add(New McInstallManifest.ManagedFile("mods/" & SelectedFile.Value.File.Name.Lower(), SelectedFile.Value.Sha1, SelectedFile.Value.File.LastWriteTimeUtc, "FabricApi"))
            End If
        End If
        If Task.IsCanceled Then Return
        'OptiFabric
        If SelectedFabric IsNot Nothing AndAlso SelectedOptiFabric Is Nothing Then
            Dim OptiFabricList = DlOptiFabricLoader.Output.Where(Function(v) IsOptiFabricCompatible(v, MinecraftVersion)).ToList()
            Dim HasMd5 = OptiFabricList.Any(Function(v) If(v.Hash, "").Length = 32) 'CurseForge 的 OptiFabric 信息可能提供 SHA1 或 MD5
            Dim Matches As New List(Of (File As FileInfo, Sha1 As String, Version As ResourceVersion))
            For Each Entry In Mods.Value
                Dim Match = OptiFabricList.FirstOrDefault(Function(v) v.Hash = Entry.Sha1)
                If Match Is Nothing AndAlso HasMd5 Then
                    Dim Md5 = CryptographyUtils.ComputeFileHash(Entry.File.FullName, CryptographyUtils.HashMethod.Md5)
                    Match = OptiFabricList.FirstOrDefault(Function(v) v.Hash = Md5)
                End If
                If Match Is Nothing Then Continue For
                Matches.Add((Entry.File, Entry.Sha1, Match))
            Next
            Dim SelectedFile = SelectMod("OptiFabric", Matches)
            If SelectedFile IsNot Nothing Then
                SelectedOptiFabric = SelectedFile.Value.Version
                ManagedFiles.Add(New McInstallManifest.ManagedFile("mods/" & SelectedFile.Value.File.Name.Lower(), SelectedFile.Value.Sha1, SelectedFile.Value.File.LastWriteTimeUtc, "OptiFabric"))
            End If
        End If
        If Task.IsCanceled Then Return
        'OptiFine
        Dim OptiFineFiles As New List(Of (File As FileInfo, Sha1 As String, OriginalName As String))
        If SelectedOptiFine Is Nothing AndAlso (SelectedForge IsNot Nothing OrElse SelectedLiteLoader IsNot Nothing OrElse SelectedFabric IsNot Nothing) Then 'OptiFine 只在有 Mod 加载器时才看在不在 mods 文件夹中
            Static OptiFineHashes As JObject = FileUtils.ReadAsJson("Minecraft\OptiFineHashes.json", type:=GetType(Resource)) 'JSON 内容：{"SHA1": "原始文件名", ...}
            OptiFineFiles = Mods.Value.
                    Select(Function(Entry) (Entry.File, Entry.Sha1, OriginalName:=OptiFineHashes(Entry.Sha1)?.ToString())). '用本地文件的 SHA1 匹配下载时的原始文件名，再根据原始文件名取得下载源信息
                    Where(Function(Entry) Entry.OriginalName IsNot Nothing).ToList()
            Dim Matches As New List(Of (File As FileInfo, Sha1 As String, Version As DlOptiFineListEntry))
            For Each Entry In OptiFineFiles '遍历 hash 匹配到了的本地文件
                Dim Match = DlOptiFineListLoader.Output.Value.FirstOrDefault(
                        Function(v) v.Inherit = MinecraftVersion AndAlso v.FileName = Entry.OriginalName AndAlso (SelectedForge Is Nothing OrElse IsOptiFineSuitForForge(v, SelectedForge)))
                If Match Is Nothing Then
                    Hint($"无法从 OptiFine 列表还原 {Entry.File.Name} 对应的 {Entry.OriginalName}，将跳过该文件。")
                    Continue For
                End If
                Matches.Add((Entry.File, Entry.Sha1, Match))
            Next
            Dim SelectedFile = SelectMod("OptiFine", Matches)
            If SelectedFile IsNot Nothing Then
                SelectedOptiFine = SelectedFile.Value.Version
                Dim RelativePath = PathUtils.RemoveExtendedPrefix(SelectedFile.Value.File.FullName).Substring(Task.Input.PathIndie.Length).Replace("\", "/").Lower()
                ManagedFiles.Add(New McInstallManifest.ManagedFile(RelativePath, SelectedFile.Value.Sha1, SelectedFile.Value.File.LastWriteTimeUtc, "OptiFine"))
            End If
        End If
        If Task.IsCanceled Then Return

#End Region

        '保存 Manifest
        If IsVanillaVersionReliable Then
            Components = New McComponents(MinecraftVersion, Components.ModLoaderVersions,
                    If(SelectedOptiFine Is Nothing, Nothing, JObject.FromObject(SelectedOptiFine)),
                    If(SelectedFabricApi?.Version, If(SelectedFabricApi?.Id, SelectedFabricApi?.Display)),
                    If(SelectedOptiFabric?.Version, If(SelectedOptiFabric?.Id, SelectedOptiFabric?.Display)))
            If Manifest Is Nothing OrElse Not Components.IsEquivalentTo(Manifest.Components) OrElse Not ManagedFiles.SequenceEqual(Manifest.ManagedFiles) Then
                Configs.McInstallManifest.Set(New McInstallManifest(Versions.McInstallManifestVersion, Components, Date.UtcNow, ManagedFiles), Task.Input.Config)
                Task.Input.Config.Save()
                Logger.Info($"已为 {Task.Input.Name} 更新安装清单")
            End If
        End If
        If Task.IsCanceled Then Return
        ForgeVersionLoader = ForgeLoader
    End Sub
    Private Function SelectMod(Of T)(ComponentName As String, Matches As List(Of (File As FileInfo, Sha1 As String, Version As T))) As (File As FileInfo, Sha1 As String, Version As T)?
        If Matches.Count = 0 Then Return Nothing
        If Matches.Count = 1 Then Return Matches(0)
        Dim Choice As Integer? = Nothing
        RunInUiWait(Sub() Choice = MyMsgBoxSelect(Matches.Select(Function(Entry) CType(New MyRadioBox With {.Text = Entry.File.Name}, IMyRadio)), $"选择当前使用的 {ComponentName}"))
        If Not Choice.HasValue Then Throw New Exception($"发现了多个 {ComponentName}")
        Return Matches(Choice.Value)
    End Function

    ''' <summary>
    ''' 初始化修改页
    ''' </summary>
    Private Sub LoadInstanceFinished(Task As LoaderTask(Of McInstance, Object))
        '还原原版信息
        OriginalName = Task.Input.Name
        CustomLogo = Nothing
        If ReadIni(Task.Input.PathVersion & "PCL\Setup.ini", "LogoCustom", False) Then
            CustomLogo = If(Task.Input.Logo.EndsWithF("PCL\Logo.png"), Task.Input.PathVersion & "PCL\Logo.png", Task.Input.Logo)
        End If
        Dim Item = McDownloadListItem(VanillaData, Nothing, False).Init()
        VanillaName = Item.Title
        VanillaIcon = Item.Logo
        LabMinecraftVersion.Text = VanillaName
        TextSelectName.Text = OriginalName
        IsSelectNameEdited = Not String.Equals(OriginalName, GetSelectName(), StringComparison.Ordinal)
        TextSelectName.ValidateRules = New ObjectModel.Collection(Of Validate) From {
            New ValidateFolderName(McFolderSelected & "versions", IgnoreList:=New List(Of String) From {OriginalName})}
        '修改页 UI
        CardMinecraft.Visibility = If(RestrictMcDownload, Visibility.Collapsed, Visibility.Visible)
        BtnBack.Visibility = Visibility.Collapsed
        ImgLogo.Margin = New Thickness(15, 15, 0, 0)
        TextSelectName.Margin = New Thickness(56, 16, 16, 0)
        LabSelectName.Text = $"原名 :   {OriginalName}"
        UpdateOriginalNameVisibility()
        BtnStart.Text = "开始修改"
        BtnStart.Logo = FrmInstanceLeft.ItemModify.Logo
        AutoSelectedFabricApi = SelectedFabric IsNot Nothing
        AutoSelectedOptiFabric = SelectedFabric IsNot Nothing AndAlso SelectedOptiFine IsNot Nothing
        If ForgeVersionLoader IsNot Nothing Then LoadForge.State = ForgeVersionLoader
        PrepareSelectPage()
        ReloadComponentLists()
        '进入动画
        PanMinecraft.Visibility = Visibility.Collapsed
        PanMinecraft.Opacity = 0
        PanMinecraft.RenderTransform = New TranslateTransform(-50, 0)
        PanSelect.Opacity = 1
        PanSelect.RenderTransform = New TranslateTransform
        PanBack.IsHitTestVisible = True
        PanAllBack.IsHitTestVisible = True
        InitialTarget = GetSelectedComponents()
        '设置原始数据
        BtnMinecraftRestore.Tag = VanillaData
        BtnOptiFineRestore.Tag = SelectedOptiFine
        BtnForgeRestore.Tag = SelectedForge
        BtnNeoForgeRestore.Tag = SelectedNeoForge
        BtnFabricRestore.Tag = SelectedFabric
        BtnFabricApiRestore.Tag = SelectedFabricApi
        BtnOptiFabricRestore.Tag = SelectedOptiFabric
        BtnLiteLoaderRestore.Tag = SelectedLiteLoader
        ImgMinecraftOld.Source = VanillaIcon
        LabMinecraftOld.Text = LabMinecraftVersion.Text
        LabOptiFineOld.Text = If(SelectedOptiFine Is Nothing, "无", LabOptiFine.Text)
        LabForgeOld.Text = If(SelectedForge Is Nothing, "无", LabForge.Text)
        LabNeoForgeOld.Text = If(SelectedNeoForge Is Nothing, "无", LabNeoForge.Text)
        LabFabricOld.Text = If(SelectedFabric Is Nothing, "无", LabFabric.Text)
        LabLiteLoaderOld.Text = If(SelectedLiteLoader Is Nothing, "无", LabLiteLoader.Text)
        LabFabricApiOld.Text = If(SelectedFabricApi Is Nothing, "无", LabFabricApi.Text)
        LabOptiFabricOld.Text = If(SelectedOptiFabric Is Nothing, "无", LabOptiFabric.Text)
        RefreshComparisons()
    End Sub

    Public Overrides Function GetSelectLogo() As String
        Return If(CustomLogo, MyBase.GetSelectLogo())
    End Function

    Public Overrides Sub ShowMinecraftVersionList()
        If IsInSelectPage Then
            '把当前已选择的组件记录到 PendingComponents
            Dim Components = GetSelectedComponents()
            For Each Component In {
                (Loader:=LoadOptiFine, Version:=Components.OptiFineVersion),
                (Loader:=LoadFabricApi, Version:=Components.FabricApiVersion),
                (Loader:=LoadOptiFabric, Version:=Components.OptiFabricVersion),
                (Loader:=LoadForge, Version:=SelectedForge?.VersionName),
                (Loader:=LoadNeoForge, Version:=SelectedNeoForge?.VersionName),
                (Loader:=LoadFabric, Version:=SelectedFabric),
                (Loader:=LoadLiteLoader, Version:=SelectedLiteLoader?.JsonToken?("version").ToString())}
                '保留尚未加载完成的组件选择
                If Component.Version IsNot Nothing Then PendingComponents(Component.Loader) = Component.Version
            Next
        End If
        MyBase.ShowMinecraftVersionList()
    End Sub

    Public Overrides Sub ReloadSelected()
        If IsRestoringComponents Then Return
        If IsInSelectPage AndAlso VanillaName IsNot Nothing AndAlso PendingComponents.Any() Then
            IsRestoringComponents = True
            Try
                Dim HasProcessed As Boolean = False
                '优先恢复 Mod 加载器，再匹配兼容的附加组件
                For Each LoadingComponent In {LoadFabric, LoadForge, LoadNeoForge, LoadLiteLoader, LoadOptiFine, LoadFabricApi, LoadOptiFabric}
                    Dim Version As String = Nothing
                    If Not PendingComponents.TryGetValue(LoadingComponent, Version) Then Continue For
                    If Not LoadingComponent.State.IsLoader Then Continue For
                    Dim Loader = DirectCast(LoadingComponent.State, LoaderBase)
                    '检查前置组件
                    If LoadingComponent Is LoadOptiFine Then
                        If {LoadFabric, LoadForge, LoadNeoForge, LoadLiteLoader}.Any(Function(Component) PendingComponents.ContainsKey(Component)) Then Continue For
                    ElseIf LoadingComponent Is LoadFabricApi Then
                        If PendingComponents.ContainsKey(LoadFabric) Then Continue For
                    ElseIf LoadingComponent Is LoadForge Then
                        If Not McVersion.IsStandardFormat(VanillaName) Then
                            PendingComponents.Remove(LoadingComponent)
                            Continue For
                        End If
                        If DirectCast(Loader, LoaderTask(Of String, List(Of DlForgeVersionEntry))).Input <> VanillaName Then Continue For
                        If Loader.State = LoadState.Failed AndAlso Loader.Error.Message = "无" Then
                            PendingComponents.Remove(LoadingComponent)
                            Continue For
                        End If
                    ElseIf LoadingComponent Is LoadOptiFabric Then
                        If PendingComponents.ContainsKey(LoadFabric) OrElse PendingComponents.ContainsKey(LoadOptiFine) Then Continue For
                    End If
                    If Loader.State <> LoadState.Finished Then Continue For
                    '移除已处理的组件
                    PendingComponents.Remove(LoadingComponent)
                    HasProcessed = True
                    '匹配目标 MC 版本下兼容的组件
                    If LoadingComponent Is LoadFabric Then
                        If SelectedFabric Is Nothing AndAlso LoadFabricGetError() Is Nothing Then
                            Dim Versions = DlFabricListLoader.Output.Value("loader").OfType(Of JObject)
                            SelectedFabric = If(Versions.FirstOrDefault(Function(v) v("version").ToString = Version), Versions.FirstOrDefault())?("version").ToString()
                        End If
                    ElseIf LoadingComponent Is LoadOptiFine Then
                        If SelectedOptiFine Is Nothing AndAlso LoadOptiFineGetError() Is Nothing Then
                            Dim Versions = DlOptiFineListLoader.Output.Value.Where(Function(v) v.Inherit = VanillaName AndAlso
                                    (SelectedForge Is Nothing OrElse IsOptiFineSuitForForge(v, SelectedForge)))
                            SelectedOptiFine = If(Versions.FirstOrDefault(Function(v) v.InstanceName.AfterLast("-OptiFine_") = Version),
                                Versions.OrderBy(Function(v) v.IsPreview).
                                    ThenByDescending(Function(v) v.DisplayName, Comparer(Of String).Create(AddressOf McUtils.CompareVersion)).FirstOrDefault())
                        End If
                    ElseIf LoadingComponent Is LoadForge Then
                        If SelectedForge Is Nothing AndAlso LoadForgeGetError() Is Nothing Then
                            Dim Versions = DirectCast(Loader, LoaderTask(Of String, List(Of DlForgeVersionEntry))).Output.Where(
                                Function(v) v.Inherit = VanillaName AndAlso
                                    v.Category <> "universal" AndAlso v.Category <> "client" AndAlso
                                    (SelectedOptiFine Is Nothing OrElse IsOptiFineSuitForForge(SelectedOptiFine, v)))
                            SelectedForge = If(Versions.FirstOrDefault(Function(v) v.VersionName = Version), Versions.OrderByDescending(Function(v) v).FirstOrDefault())
                        End If
                    ElseIf LoadingComponent Is LoadNeoForge Then
                        If SelectedNeoForge Is Nothing AndAlso LoadNeoForgeGetError() Is Nothing Then
                            Dim Versions = DlNeoForgeListLoader.Output.Value.Where(Function(v) v.Inherit = VanillaName)
                            SelectedNeoForge = If(Versions.FirstOrDefault(Function(v) v.VersionName = Version), Versions.FirstOrDefault())
                        End If
                    ElseIf LoadingComponent Is LoadLiteLoader Then
                        If SelectedLiteLoader Is Nothing AndAlso LoadLiteLoaderGetError() Is Nothing Then
                            Dim Versions = DlLiteLoaderListLoader.Output.Value.Where(Function(v) v.Inherit = VanillaName)
                            SelectedLiteLoader = If(Versions.FirstOrDefault(Function(v) v.JsonToken("version").ToString = Version), Versions.FirstOrDefault())
                        End If
                    ElseIf LoadingComponent Is LoadFabricApi Then
                        If SelectedFabricApi Is Nothing AndAlso LoadFabricApiGetError() Is Nothing Then
                            Dim Versions = DlFabricApiLoader.Output.Where(Function(v) IsFabricApiCompatible(v, VanillaName))
                            SelectedFabricApi = If(Versions.FirstOrDefault(Function(v) v.Version = Version OrElse v.Id = Version OrElse v.Display = Version),
                                Versions.OrderByDescending(Function(v) v.ReleaseDate).FirstOrDefault())
                        End If
                        If SelectedFabricApi IsNot Nothing Then AutoSelectedFabricApi = True
                    ElseIf LoadingComponent Is LoadOptiFabric Then
                        If SelectedOptiFabric Is Nothing AndAlso LoadOptiFabricGetError() Is Nothing Then
                            Dim Versions = DlOptiFabricLoader.Output.Where(Function(v) IsOptiFabricCompatible(v, VanillaName))
                            SelectedOptiFabric = If(Versions.FirstOrDefault(Function(v) v.Version = Version OrElse v.Id = Version OrElse v.Display = Version),
                                Versions.OrderByDescending(Function(v) v.ReleaseDate).FirstOrDefault())
                        End If
                        If SelectedOptiFabric IsNot Nothing Then AutoSelectedOptiFabric = True
                    End If
                Next
                '更新组件列表
                If HasProcessed Then ReloadComponentLists()
            Finally
                IsRestoringComponents = False
            End Try
        End If
        '刷新当前选择与原始版本的对比
        MyBase.ReloadSelected()
        If InitialTarget IsNot Nothing Then RefreshComparisons()
    End Sub

    Private Sub RefreshComparisons()
        Dim Current = GetSelectedComponents()
        ImgMinecraftNew.Source = VanillaIcon
        'Minecraft
        Dim IsMinecraftUnchanged = InitialTarget.MinecraftVersion = Current.MinecraftVersion
        ShowComparison(PanMinecraftCompare, PanMinecraftInfo, InitialTarget.MinecraftVersion, Current.MinecraftVersion,
                       LabMinecraftVersion.Text, BtnMinecraftRestore, True)
        'OptiFine
        Dim OriginalOptiFine = TryCast(BtnOptiFineRestore.Tag, DlOptiFineListEntry)
        ShowComparison(PanOptiFineCompare, PanOptiFineInfo, InitialTarget.OptiFineVersion, Current.OptiFineVersion,
                       LabOptiFine.Text, BtnOptiFineRestore, InitialTarget.OptiFineEntry Is Nothing OrElse
                           IsMinecraftUnchanged AndAlso CardOptiFine.MainSwap.Visibility = Visibility.Visible AndAlso OriginalOptiFine IsNot Nothing AndAlso
                           (SelectedForge Is Nothing OrElse IsOptiFineSuitForForge(OriginalOptiFine, SelectedForge)), BtnOptiFineClear)
        'Fabric API
        ShowComparison(PanFabricApiCompare, PanFabricApiInfo, InitialTarget.FabricApiVersion, Current.FabricApiVersion,
                       LabFabricApi.Text, BtnFabricApiRestore, InitialTarget.FabricApiVersion Is Nothing OrElse
                             IsMinecraftUnchanged AndAlso SelectedFabric IsNot Nothing AndAlso CardFabricApi.MainSwap.Visibility = Visibility.Visible, BtnFabricApiClear)
        'OptiFabric
        ShowComparison(PanOptiFabricCompare, PanOptiFabricInfo, InitialTarget.OptiFabricVersion, Current.OptiFabricVersion,
                       LabOptiFabric.Text, BtnOptiFabricRestore, InitialTarget.OptiFabricVersion Is Nothing OrElse
                             IsMinecraftUnchanged AndAlso SelectedFabric IsNot Nothing AndAlso SelectedOptiFine IsNot Nothing AndAlso CardOptiFabric.MainSwap.Visibility = Visibility.Visible, BtnOptiFabricClear)
        'Mod Loader
        For Each Loader In {
            (Kind:=ModLoaders.Forge, Card:=CardForge, ComparePanel:=PanForgeCompare, CurrentPanel:=PanForgeInfo, Label:=LabForge, RestoreButton:=BtnForgeRestore, ClearButton:=BtnForgeClear),
            (Kind:=ModLoaders.NeoForge, Card:=CardNeoForge, ComparePanel:=PanNeoForgeCompare, CurrentPanel:=PanNeoForgeInfo, Label:=LabNeoForge, RestoreButton:=BtnNeoForgeRestore, ClearButton:=BtnNeoForgeClear),
            (Kind:=ModLoaders.Fabric, Card:=CardFabric, ComparePanel:=PanFabricCompare, CurrentPanel:=PanFabricInfo, Label:=LabFabric, RestoreButton:=BtnFabricRestore, ClearButton:=BtnFabricClear),
            (Kind:=ModLoaders.LiteLoader, Card:=CardLiteLoader, ComparePanel:=PanLiteLoaderCompare, CurrentPanel:=PanLiteLoaderInfo, Label:=LabLiteLoader, RestoreButton:=BtnLiteLoaderRestore, ClearButton:=BtnLiteLoaderClear)}
            Dim OldVersion As String = Nothing, NewVersion As String = Nothing
            InitialTarget.ModLoaderVersions.TryGetValue(Loader.Kind, OldVersion)
            Current.ModLoaderVersions.TryGetValue(Loader.Kind, NewVersion)
            Dim CanRestore = OldVersion Is Nothing OrElse IsMinecraftUnchanged AndAlso
                Loader.Card.MainSwap.Visibility = Visibility.Visible AndAlso Loader.RestoreButton.Tag IsNot Nothing
            If Loader.Kind = ModLoaders.Forge AndAlso OldVersion IsNot Nothing AndAlso CanRestore Then
                CanRestore = SelectedOptiFine Is Nothing OrElse IsOptiFineSuitForForge(SelectedOptiFine, DirectCast(Loader.RestoreButton.Tag, DlForgeVersionEntry))
            End If
            If Loader.Kind = ModLoaders.Fabric AndAlso OldVersion IsNot Nothing Then
                Dim TargetVersion = VanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3")
                CanRestore = CardFabric.Visibility = Visibility.Visible AndAlso CardFabric.MainSwap.Visibility = Visibility.Visible AndAlso
                    DlFabricListLoader.Output.Value("loader").OfType(Of JObject).Any(Function(v) v("version").ToString = OldVersion) AndAlso
                    DlFabricListLoader.Output.Value("game").OfType(Of JObject).Any(Function(v) v("version").ToString = TargetVersion)
            End If
            ShowComparison(Loader.ComparePanel, Loader.CurrentPanel, OldVersion, NewVersion, Loader.Label.Text,
                           Loader.RestoreButton, CanRestore, Loader.ClearButton)
        Next
        UpdateStartButton()
    End Sub

    Private Sub ShowComparison(ComparePanel As Grid, CurrentPanel As FrameworkElement, OldVersion As String, NewVersion As String, NewText As String,
                               RestoreButton As MyIconButton, CanRestore As Boolean, Optional ClearButton As FrameworkElement = Nothing)
        '获取不可安装的原因
        Dim UnavailableReason As String = Nothing
        If NewVersion Is Nothing Then
            If RestoreButton Is BtnOptiFineRestore Then
                UnavailableReason = LoadOptiFineGetError()
            ElseIf RestoreButton Is BtnForgeRestore Then
                UnavailableReason = If(McVersion.IsStandardFormat(VanillaName), LoadForgeGetError(), "无")
            ElseIf RestoreButton Is BtnNeoForgeRestore Then
                UnavailableReason = If(CardNeoForge.Visibility = Visibility.Collapsed, "无", LoadNeoForgeGetError())
            ElseIf RestoreButton Is BtnFabricRestore Then
                UnavailableReason = If(CardFabric.Visibility = Visibility.Collapsed, "无", LoadFabricGetError())
            ElseIf RestoreButton Is BtnLiteLoaderRestore Then
                UnavailableReason = If(CardLiteLoader.Visibility = Visibility.Collapsed, "无", LoadLiteLoaderGetError())
            ElseIf RestoreButton Is BtnFabricApiRestore Then
                UnavailableReason = LoadFabricApiGetError()
            ElseIf RestoreButton Is BtnOptiFabricRestore Then
                UnavailableReason = LoadOptiFabricGetError()
            End If
            NewText = If(UnavailableReason, "可以添加")
        End If
        '修改对比
        Dim IsPending = NewVersion Is Nothing AndAlso (NewText = "获取中……" OrElse NewText.StartsWithF("获取失败："))
        Dim IsChanged = OldVersion <> NewVersion AndAlso Not IsPending
        CanRestore = IsChanged AndAlso OldVersion IsNot Nothing AndAlso CanRestore
        ComparePanel.Visibility = If(IsChanged, Visibility.Visible, Visibility.Collapsed)
        CurrentPanel.Visibility = If(IsChanged, Visibility.Collapsed, Visibility.Visible)
        '还原与取消安装按钮
        RestoreButton.Visibility = If(CanRestore, Visibility.Visible, Visibility.Collapsed)
        If ClearButton IsNot Nothing Then ClearButton.ToolTip = If(OldVersion Is Nothing, "取消安装", "移除")
        Dim HasClearButton = ClearButton IsNot Nothing AndAlso ClearButton.Visibility = Visibility.Visible
        RestoreButton.Margin = New Thickness(0, 5, If(CanRestore AndAlso HasClearButton, 60, 32), 0)
        ComparePanel.Margin = New Thickness(ComparePanel.Margin.Left, ComparePanel.Margin.Top,
                                            If(CanRestore AndAlso HasClearButton, 110, 80), ComparePanel.Margin.Bottom)
        If Not IsChanged Then Return
        '显示修改前已安装组件的对比卡片
        If OldVersion IsNot Nothing Then
            Dim Card = CType(ComparePanel.Parent, MyCard)
            If Card.Visibility = Visibility.Collapsed Then
                Card.IsSwapped = True
                If Card.MainSwap IsNot Nothing Then Card.MainSwap.Visibility = Visibility.Collapsed
                Card.Visibility = Visibility.Visible
            End If
        End If
        Dim OldSide = CType(ComparePanel.Children(0), DockPanel)
        Dim NewSide = CType(ComparePanel.Children(2), DockPanel)
        OldSide.Children(0).Visibility = If(OldVersion Is Nothing, Visibility.Collapsed, Visibility.Visible)
        CType(OldSide.Children(1), TextBlock).Margin = If(OldVersion Is Nothing, New Thickness(0), New Thickness(7, 0, 0, 0))
        NewSide.Children(0).Visibility = If(NewVersion Is Nothing, Visibility.Collapsed, Visibility.Visible)
        Dim NewLabel = CType(NewSide.Children(1), TextBlock)
        NewLabel.Margin = If(NewVersion Is Nothing, New Thickness(0), New Thickness(7, 0, 0, 0))
        NewLabel.Text = If(NewVersion Is Nothing, "移除", NewText)
        If NewVersion Is Nothing AndAlso UnavailableReason IsNot Nothing Then
            NewLabel.Inlines.Add(New Run($"（{If(UnavailableReason = "无", "无可用版本", UnavailableReason)}）") With {.Foreground = ColorGray4})
        End If
    End Sub

    Private Sub RestoreComponent(sender As Object, e As EventArgs) Handles BtnMinecraftRestore.Click, BtnOptiFineRestore.Click, BtnForgeRestore.Click,
                BtnNeoForgeRestore.Click, BtnFabricRestore.Click, BtnFabricApiRestore.Click, BtnOptiFabricRestore.Click, BtnLiteLoaderRestore.Click
        Dim Button = DirectCast(sender, MyIconButton)
        '恢复初始选择
        If Button Is BtnMinecraftRestore Then
            IsRestoringComponents = True
            Try
                PendingComponents.Clear()
                MinecraftSelected(McDownloadListItem(DirectCast(Button.Tag, JObject), Nothing, False).Init(), Nothing)
                If ForgeVersionLoader IsNot Nothing Then LoadForge.State = ForgeVersionLoader
                '恢复初始的组件
                SelectedOptiFine = TryCast(BtnOptiFineRestore.Tag, DlOptiFineListEntry)
                SelectedForge = TryCast(BtnForgeRestore.Tag, DlForgeVersionEntry)
                SelectedNeoForge = TryCast(BtnNeoForgeRestore.Tag, DlNeoForgeListEntry)
                SelectedFabric = TryCast(BtnFabricRestore.Tag, String)
                SelectedLiteLoader = TryCast(BtnLiteLoaderRestore.Tag, DlLiteLoaderListEntry)
                SelectedFabricApi = TryCast(BtnFabricApiRestore.Tag, ResourceVersion)
                SelectedOptiFabric = TryCast(BtnOptiFabricRestore.Tag, ResourceVersion)
                AutoSelectedFabricApi = SelectedFabric IsNot Nothing
                AutoSelectedOptiFabric = SelectedFabric IsNot Nothing AndAlso SelectedOptiFine IsNot Nothing
            Finally
                IsRestoringComponents = False
            End Try
            ReloadComponentLists()
            Return
        ElseIf Button Is BtnOptiFineRestore Then
            SelectedOptiFine = TryCast(Button.Tag, DlOptiFineListEntry)
            AutoSelectedOptiFabric = SelectedOptiFine IsNot Nothing AndAlso SelectedFabric IsNot Nothing
            If SelectedOptiFine Is Nothing Then SelectedOptiFabric = Nothing
        ElseIf Button Is BtnForgeRestore Then
            SelectedForge = TryCast(Button.Tag, DlForgeVersionEntry)
        ElseIf Button Is BtnNeoForgeRestore Then
            SelectedNeoForge = TryCast(Button.Tag, DlNeoForgeListEntry)
        ElseIf Button Is BtnFabricRestore Then
            SelectedFabric = TryCast(Button.Tag, String)
            AutoSelectedFabricApi = SelectedFabric IsNot Nothing
            AutoSelectedOptiFabric = SelectedFabric IsNot Nothing AndAlso SelectedOptiFine IsNot Nothing
            If SelectedFabric Is Nothing Then
                SelectedFabricApi = Nothing
                SelectedOptiFabric = Nothing
            End If
        ElseIf Button Is BtnFabricApiRestore Then
            SelectedFabricApi = TryCast(Button.Tag, ResourceVersion)
            AutoSelectedFabricApi = True
        ElseIf Button Is BtnOptiFabricRestore Then
            SelectedOptiFabric = TryCast(Button.Tag, ResourceVersion)
            AutoSelectedOptiFabric = True
        ElseIf Button Is BtnLiteLoaderRestore Then
            SelectedLiteLoader = TryCast(Button.Tag, DlLiteLoaderListEntry)
        End If
        DirectCast(Button.Parent, MyCard).IsSwapped = True
        '刷新组件
        ReloadComponentLists()
    End Sub

    Private Sub CardMinecraft_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles CardMinecraft.MouseLeftButtonUp
        ShowMinecraftVersionList()
        e.Handled = True
    End Sub

    Private Sub UpdateStartButton() Handles TextSelectName.TextChanged, TextSelectName.ValidateChanged
        BtnStart.IsEnabled = TextSelectName.IsValidated AndAlso InitialTarget IsNot Nothing AndAlso
            Not PendingComponents.Any() AndAlso
            (Not IsVanillaVersionReliable OrElse Not InitialTarget.IsEquivalentTo(GetSelectedComponents()) OrElse
             Not String.Equals(TextSelectName.Text, OriginalName, StringComparison.Ordinal))
    End Sub

    Private Sub UpdateOriginalNameVisibility() Handles TextSelectName.TextChanged
        Dim IsNameChanged = Not String.Equals(TextSelectName.Text, OriginalName, StringComparison.Ordinal)
        Dim NewVisibility = If(IsNameChanged, Visibility.Visible, Visibility.Collapsed)
        If LabSelectName.Visibility = NewVisibility Then Return
        LabSelectName.Visibility = NewVisibility
        BtnOriginalNameRestore.Visibility = NewVisibility
        Dim RightMargin = If(IsNameChanged, 40, 16)
        If IsInSelectPage AndAlso AniControlEnabled = 0 Then
            AniStart(AaDouble(Sub(Delta) TextSelectName.Margin = New Thickness(56, 16, TextSelectName.Margin.Right + Delta, 0),
                              RightMargin - TextSelectName.Margin.Right, 150,, New AniEaseOutFluent), "DownloadModify Name Margin")
        Else
            AniStop("DownloadModify Name Margin")
            TextSelectName.Margin = New Thickness(56, 16, RightMargin, 0)
        End If
    End Sub

    Private Sub BtnOriginalNameRestore_Click(sender As Object, e As EventArgs) Handles BtnOriginalNameRestore.Click
        TextSelectName.Text = OriginalName
        IsSelectNameEdited = Not String.Equals(OriginalName, GetSelectName(), StringComparison.Ordinal)
    End Sub

    Public Overrides Sub StartInstall(Request As McInstallRequest)
        UpdateStartButton()
        If Not BtnStart.IsEnabled Then Return
        Dim TargetInstance = InitLoader.Input
        Dim Target = Request.Components
        Dim InstanceName = Request.NewInstanceName
        Dim IsNameChanged = Not String.Equals(InstanceName, TargetInstance.Name, StringComparison.Ordinal)
        Request.NewInstanceName = If(IsNameChanged, InstanceName, Nothing)
        Request.InstanceFolder = TargetInstance.PathVersion
        '警告
        Dim Warnings As New List(Of String)
        If (SelectedForge IsNot Nothing OrElse SelectedNeoForge IsNot Nothing OrElse SelectedFabric IsNot Nothing) AndAlso
           Not PathUtils.IsSame(TargetInstance.PathIndie, TargetInstance.PathVersion) Then
            Warnings.Add("该版本尚未开启版本隔离，可能与别的 MC 版本共用同一个 Mod 和存档文件夹。" & vbCrLf &
                         "修改版本后，已有的 Mod、存档等需要手动迁移到新的游戏目录。")
        End If
        Dim IsMinecraftVersionChanged = InitialTarget.MinecraftVersion <> Target.MinecraftVersion
        If Not IsVanillaVersionReliable AndAlso Not IsMinecraftVersionChanged Then
            Warnings.Add($"PCL 未能精准识别该版本，请确认它是不是 Minecraft {InitialTarget.MinecraftVersion}，如果不正确，请取消并选择正确的版本！")
        End If
        If Not Configs.HintModifyInstanceWarning.Get() Then
            Warnings.Add("如果该版本安装了 PCL 不兼容的加载器，或经过第三方修改，这些改动将会丢失！")
        End If
        Dim IsModLoaderChanged As Boolean = False
        For Each Loader In InitialTarget.ModLoaderVersions.Keys.Union(Target.ModLoaderVersions.Keys)
            Dim CurrentVersion As String = Nothing, TargetVersion As String = Nothing
            Dim HasCurrent = InitialTarget.ModLoaderVersions.TryGetValue(Loader, CurrentVersion)
            Dim HasTarget = Target.ModLoaderVersions.TryGetValue(Loader, TargetVersion)
            If HasCurrent <> HasTarget OrElse CurrentVersion <> TargetVersion Then
                IsModLoaderChanged = True
                Exit For
            End If
        Next
        If (IsModLoaderChanged OrElse IsMinecraftVersionChanged) AndAlso InitialTarget.ModLoaderVersions.Any Then
            Warnings.Add("现有的 Mod、资源包等在修改后可能会不兼容！")
        End If
        If Warnings.Any() Then
            If MyMsgBox("请注意：" & vbCrLf & "• " & Warnings.Join(vbCrLf & "• "), "修改确认", "开始修改", "取消", IsWarn:=True) <> 1 Then Return
        End If
        If Not Configs.HintModifyInstanceWarning.Get() Then Configs.HintModifyInstanceWarning.Set(True)
        '开始修改
        If IsVanillaVersionReliable AndAlso InitialTarget.IsEquivalentTo(Target) Then
            '仅重命名
            Try
                Dim OldPath = TargetInstance.PathVersion
                Dim RenamedInstance = TargetInstance.Rename(InstanceName)
                If PageInstanceLeft.Instance IsNot Nothing AndAlso PathUtils.IsSame(PageInstanceLeft.Instance.PathVersion, OldPath) Then PageInstanceLeft.Instance = RenamedInstance
                If McInstanceSelected IsNot Nothing AndAlso PathUtils.IsSame(McInstanceSelected.PathVersion, OldPath) Then McInstanceSelected = RenamedInstance
                PageInstanceLeft.ReloadCurrentJava()
                Hint("重命名成功！", HintType.Green)
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                FrmMain.PageBack()
            Catch ex As Exception
                Logger.Error(ex, "重命名版本失败", LogBehavior.Alert)
            End Try
        Else
            '实际修改
            If McModificationStart(Request, TargetInstance) Then FrmMain.PageBack()
        End If
    End Sub
End Class
