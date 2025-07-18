Public Class PageDownloadCompDetail
    Private CompItem As MyCompItem = Nothing

#Region "加载器"

    Private CompFileLoader As New LoaderTask(Of Integer, List(Of CompFile))(
        "Comp File",
        Sub(Task)
            LoadTargetFromAdditional()
            Dim Result = CompFilesGet(Project.Id, Project.FromCurseForge)
            If Task.IsAborted Then Return
            Task.Output = Result
        End Sub)

    '初始化加载器信息
    Private Sub PageDownloadCompDetail_Inited(sender As Object, e As EventArgs) Handles Me.Initialized
        LoadTargetFromAdditional()
        PageLoaderInit(Load, PanLoad, PanMain, CardIntro, CompFileLoader, AddressOf Load_OnFinish)
    End Sub
    Public Sub LoadTargetFromAdditional() Handles Me.Loaded
        Project = FrmMain.PageCurrent.Additional(0)
        TargetVersion = FrmMain.PageCurrent.Additional(2)
        TargetLoader = FrmMain.PageCurrent.Additional(3)
        PageType = FrmMain.PageCurrent.Additional(4)
    End Sub
    Private Project As CompProject
    Private TargetVersion As String, TargetLoader As CompModLoaderType
    ''' <summary>
    ''' 当前页面应展示的内容类别。可能为 Any。
    ''' </summary>
    Private PageType As CompType
    '自动重试
    Private Sub Load_State(sender As Object, state As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState) Handles Load.StateChanged
        Select Case CompFileLoader.State
            Case LoadState.Failed
                Dim ErrorMessage As String = ""
                If CompFileLoader.Error IsNot Nothing Then ErrorMessage = CompFileLoader.Error.Message
                If ErrorMessage.Contains("不是有效的 json 文件") Then
                    Log("[Comp] 下载的文件 json 列表损坏，已自动重试", LogLevel.Debug)
                    PageLoaderRestart()
                End If
        End Select
    End Sub
    '结果 UI 化
    Private Class CardSorter
        Implements IComparer(Of String)
        Public Topmost As String = ""
        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            '相同
            If x = y Then Return 0
            '置顶
            If x = Topmost Then Return -1
            If y = Topmost Then Return 1
            '特殊版本
            Dim IsXSpecial As Boolean = x.EndsWithF("版本")
            Dim IsYSpecial As Boolean = y.EndsWithF("版本")
            If IsXSpecial AndAlso IsYSpecial Then Return x.CompareTo(y)
            If IsXSpecial Then Return 1
            If IsYSpecial Then Return -1
            '比较版本号
            Dim VersionCodeSort = -VersionSortInteger(x.Replace(x.BeforeFirst(" ") & " ", ""), y.Replace(y.BeforeFirst(" ") & " ", ""))
            If VersionCodeSort <> 0 Then Return VersionCodeSort
            '比较全部
            Return -VersionSortInteger(x, y)
        End Function
        Public Sub New(Optional Topmost As String = "")
            Me.Topmost = If(Topmost, "")
        End Sub
    End Class

    Private VersionFilter As String
    Private IsMajorVersionFilter As Boolean '是否按大版本号筛选（1.21 / 1.20 / 1.19 / ...）而非小版本号（1.21.1 / 1.21 / 1.20.4 / ...）
    '筛选类型相同的结果（Modrinth 会返回 Mod、服务端插件、数据包混合的列表）
    Private Function GetResults() As List(Of CompFile)
        Dim Results As List(Of CompFile) = CompFileLoader.Output
        If PageType = CompType.Any Then
            Results = Results.Where(Function(r) r.Type <> CompType.Plugin).ToList
        ElseIf PageType = CompType.Shader OrElse PageType = CompType.ResourcePack Then
            Results = Results.ToList
        Else
            Results = Results.Where(Function(r) r.Type = PageType).ToList
        End If
        Return Results
    End Function
    Private Sub Load_OnFinish()
        Dim Results = GetResults()

        '初始化筛选器
        Dim VersionFilters As List(Of String)

        '按小版本号筛选？
        IsMajorVersionFilter = False
        VersionFilters = Results.SelectMany(Function(v) v.GameVersions).Select(Function(v) GetGroupedVersionName(v, IsMajorVersionFilter, True)).
            Distinct.OrderByDescending(Function(s) s, New VersionComparer).ToList
        '按大版本号筛选？
        If VersionFilters.Count >= 9 Then
            IsMajorVersionFilter = True
            VersionFilters = Results.SelectMany(Function(v) v.GameVersions).Select(Function(v) GetGroupedVersionName(v, IsMajorVersionFilter, True)).
                Distinct.OrderByDescending(Function(s) s, New VersionComparer).ToList
        End If

        'UI 化筛选器
        PanFilter.Children.Clear()
        If VersionFilters.Count < 2 Then
            CardFilter.Visibility = Visibility.Collapsed
            VersionFilter = Nothing
        Else
            CardFilter.Visibility = Visibility.Visible
            VersionFilters.Insert(0, "全部")
            '转化为按钮
            For Each Version As String In VersionFilters
                Dim NewButton As New MyRadioButton With {
                    .Text = Version, .Margin = New Thickness(2, 0, 2, 0), .ColorType = MyRadioButton.ColorState.Highlight}
                NewButton.LabText.Margin = New Thickness(-2, 0, 8, 0)
                AddHandler NewButton.Check,
                Sub(sender As MyRadioButton, raiseByMouse As Boolean)
                    VersionFilter = If(sender.Text = "全部", Nothing, sender.Text)
                    UpdateFilterResult()
                End Sub
                PanFilter.Children.Add(NewButton)
            Next
            '自动选择
            Dim ToCheck As MyRadioButton = Nothing
            If TargetVersion <> "" Then
                Dim TargetFile = Results.FirstOrDefault(Function(v) v.GameVersions.Contains(TargetVersion))
                If TargetFile IsNot Nothing Then
                    Dim TargetGroup = GetGroupedVersionName(TargetVersion, IsMajorVersionFilter, True)
                    For Each Button As MyRadioButton In PanFilter.Children
                        If Button.Text <> TargetGroup Then Continue For
                        ToCheck = Button
                        Exit For
                    Next
                End If
            End If
            If ToCheck Is Nothing Then ToCheck = PanFilter.Children(0)
            ToCheck.Checked = True
        End If

        '更新筛选结果（文件列表 UI 化）
        UpdateFilterResult()
    End Sub
    Private Sub UpdateFilterResult()
        Dim Results = GetResults()

        Dim TargetCardName As String = If(TargetVersion <> "" OrElse TargetLoader <> CompModLoaderType.Any,
            $"所选版本：{If(TargetLoader <> CompModLoaderType.Any, TargetLoader.ToString & " ", "")}{TargetVersion}", "")
        '归类到卡片下
        Dim Dict As New SortedDictionary(Of String, List(Of CompFile))(New CardSorter(TargetCardName))
        Dict.Add("其他版本", New List(Of CompFile))
        Dim SupportedLoaders As New List(Of Integer)([Enum].GetValues(GetType(CompModLoaderType)))
        For Each Version As CompFile In Results
            For Each GameVersion In Version.GameVersions
                '检查是否符合版本筛选器
                If VersionFilter IsNot Nothing AndAlso
                    GetGroupedVersionName(GameVersion, IsMajorVersionFilter, True) <> VersionFilter Then Continue For
                '决定添加到哪个卡片
                Dim VerName As String = GetGroupedVersionName(GameVersion, False, False)
                '遍历加入的加载器列表
                Dim Loaders As New List(Of String)
                If Project.ModLoaders.Count > 1 AndAlso '工程至少有两个加载器
                    Version.Type = CompType.Mod AndAlso '是 Mod
                    VerName.StartsWith("1.") Then '不是 “快照版本” 之类的
                    For Each Loader In Version.ModLoaders
                        If Loader = CompModLoaderType.Quilt AndAlso Setup.Get("ToolDownloadIgnoreQuilt") Then Continue For
                        If SupportedLoaders.Contains(Loader) Then Loaders.Add(Loader.ToString & " ")
                    Next
                End If
                If Not Loaders.Any() Then Loaders.Add("") '保底加一个空的，确保它在一张卡片里
                '实际添加
                For Each Loader In Loaders
                    Dim TargetCard As String = Loader & VerName
                    If Not Dict.ContainsKey(TargetCard) Then Dict.Add(TargetCard, New List(Of CompFile))
                    If Not Dict(TargetCard).Contains(Version) Then Dict(TargetCard).Add(Version)
                Next
            Next
        Next
        '添加筛选的版本的卡片
        If TargetCardName <> "" AndAlso (VersionFilter Is Nothing OrElse GetGroupedVersionName(TargetVersion, IsMajorVersionFilter, True).StartsWithF(VersionFilter)) Then
            Dict.Add(TargetCardName, New List(Of CompFile))
            For Each Version As CompFile In Results
                If Version.GameVersions.Contains(TargetVersion) AndAlso
                   (TargetLoader = CompModLoaderType.Any OrElse Version.ModLoaders.Contains(TargetLoader)) Then
                    '检查是否符合版本筛选器
                    If VersionFilter IsNot Nothing AndAlso
                        Not Version.GameVersions.Any(Function(v) GetGroupedVersionName(v, IsMajorVersionFilter, True) = VersionFilter) Then Continue For
                    If Not Dict(TargetCardName).Contains(Version) Then Dict(TargetCardName).Add(Version)
                End If
            Next
        End If
        '转化为 UI
        Try
            PanResults.Children.Clear()
            For Each Pair As KeyValuePair(Of String, List(Of CompFile)) In Dict
                If Not Pair.Value.Any() Then Continue For
                '增加卡片
                Dim NewCard As New MyCard With {.Title = Pair.Key, .Margin = New Thickness(0, 0, 0, 15), .SwapType = If(PageType = CompType.ModPack, 9, 8)} '9 是安装，8 是另存为
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = Pair.Value}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                PanResults.Children.Add(NewCard)
                '确定卡片是否展开
                If Pair.Key = TargetCardName OrElse
                   (FrmMain.PageCurrent.Additional IsNot Nothing AndAlso '#2761
                   CType(FrmMain.PageCurrent.Additional(1), List(Of String)).Contains(NewCard.Title)) Then
                    MyCard.StackInstall(NewStack, If(PageType = CompType.ModPack, 9, 8), Pair.Key) '9 是安装，8 是另存为
                Else
                    NewCard.IsSwaped = True
                End If
                '增加提示
                If Pair.Key = "其他版本" Then
                    NewStack.Children.Add(New MyHint With {.Text = "由于版本信息更新缓慢，可能无法识别刚更新的 MC 版本。几天后即可正常识别。", .Theme = MyHint.Themes.Yellow, .Margin = New Thickness(5, 0, 0, 8)})
                End If
            Next
            '如果只有一张卡片，展开第一张卡片
            If PanResults.Children.Count = 1 Then
                CType(PanResults.Children(0), MyCard).IsSwaped = False
            End If
        Catch ex As Exception
            Log(ex, "可视化工程下载列表出错", LogLevel.Feedback)
        End Try
    End Sub
    Private Function GetGroupedVersionName(Name As String, MajorOnly As Boolean, FoldOldRelease As Boolean) As String
        If Name Is Nothing Then
            Return "其他版本"
        ElseIf Name.Contains("w") Then
            Return "快照版本"
        ElseIf Name.StartsWith("1.0") OrElse Not Name.StartsWith("1.") OrElse (FoldOldRelease AndAlso Val(Name.Split(".")(1)) < 10) Then
            Return "远古版本"
        Else
            Return If(MajorOnly, "1." & Name.Split(".")(1).BeforeFirst(" "), Name)
        End If
    End Function

#End Region

    Private IsFirstInit As Boolean = True
    Public Sub Init() Handles Me.PageEnter
        AniControlEnabled += 1
        Project = FrmMain.PageCurrent.Additional(0)
        PanBack.ScrollToHome()

        '重启加载器
        If IsFirstInit Then
            '在 Me.Initialized 已经初始化了加载器，不再重复初始化
            IsFirstInit = False
        Else
            PageLoaderRestart(IsForceRestart:=True)
        End If

        '放置当前工程
        If CompItem IsNot Nothing Then PanIntro.Children.Remove(CompItem)
        CompItem = Project.ToCompItem(True, True)
        CompItem.CanInteraction = False
        CompItem.Margin = New Thickness(-7, -7, 0, 8)
        PanIntro.Children.Insert(0, CompItem)

        '决定按钮显示
        BtnIntroWeb.Text = If(Project.FromCurseForge, "转到 CurseForge", "转到 Modrinth")
        BtnIntroWiki.Visibility = If(Project.WikiId = 0, Visibility.Collapsed, Visibility.Visible)

        AniControlEnabled -= 1
    End Sub

    '整合包安装
    Public Sub Install_Click(sender As MyListItem, e As EventArgs)
        Try

            '获取基本信息
            Dim File As CompFile = sender.Tag
            Dim LoaderName As String = $"{If(Project.FromCurseForge, "CurseForge", "Modrinth")} 整合包下载：{Project.TranslatedName} "

            '获取版本名
            Dim PackName As String = Project.TranslatedName.Replace(".zip", "").Replace(".rar", "").Replace(".mrpack", "").Replace("\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("""", "").Replace("： ", "：")
            Dim Validate As New ValidateFolderName(PathMcFolder & "versions")
            If Validate.Validate(PackName) <> "" Then PackName = ""
            Dim VersionName As String = MyMsgBoxInput("输入版本名称", "", PackName, New ObjectModel.Collection(Of Validate) From {Validate})
            If String.IsNullOrEmpty(VersionName) Then Return

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            Dim Target As String = $"{PathMcFolder}versions\{VersionName}\原始整合包.{If(Project.FromCurseForge, "zip", "mrpack")}"
            Dim LogoFileAddress As String = MyImage.GetTempPath(CompItem.Logo)
            Loaders.Add(New LoaderDownload("下载整合包文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 10, .Block = True})
            Loaders.Add(New LoaderTask(Of Integer, Integer)("准备安装整合包",
            Sub() ModpackInstall(Target, VersionName, If(IO.File.Exists(LogoFileAddress), LogoFileAddress, Nothing))) With {.ProgressWeight = 0.1})

            '启动
            Dim Loader As New LoaderCombo(Of String)(LoaderName, Loaders) With {.OnStateChanged =
            Sub(MyLoader)
                Select Case MyLoader.State
                    Case LoadState.Failed
                        Hint(MyLoader.Name & "失败：" & GetExceptionSummary(MyLoader.Error), HintType.Critical)
                    Case LoadState.Aborted
                        Hint(MyLoader.Name & "已取消！", HintType.Info)
                    Case LoadState.Loading
                        Return '不重新加载版本列表
                End Select
                McInstallFailedClearFolder(MyLoader)
            End Sub}
            Loader.Start(PathMcFolder & "versions\" & VersionName & "\")
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "下载资源整合包失败", LogLevel.Feedback)
        End Try
    End Sub
    '资源下载；整合包另存为
    Public Shared CachedFolder As String = Nothing '仅在本次缓存的下载文件夹
    Public Sub Save_Click(sender As Object, e As EventArgs)
        Dim File As CompFile = If(TypeOf sender Is MyListItem, sender, sender.Parent).Tag
        RunInNewThread(
        Sub()
            Try
                Dim Desc As String = Nothing
                Select Case File.Type
                    Case CompType.ModPack : Desc = "整合包"
                    Case CompType.Mod : Desc = "Mod "
                    Case CompType.ResourcePack : Desc = "资源包"
                    Case CompType.Shader : Desc = "光影包"
                    Case CompType.DataPack : Desc = "数据包"
                End Select
                '确认默认保存位置
                Dim DefaultFolder As String = Nothing
                If File.Type <> CompType.ModPack Then
                    Dim SubFolder As String = Nothing
                    Select Case Project.Type
                        Case CompType.Mod : SubFolder = "mods\"
                        Case CompType.ResourcePack : SubFolder = "resourcepacks\"
                        Case CompType.Shader : SubFolder = "shaderpacks\"
                        Case CompType.DataPack : SubFolder = "" '导航到版本根目录
                    End Select
                    Dim IsVersionSuitable As Func(Of McVersion, Boolean) = Nothing
                    '获取资源所需的加载器
                    Dim AllowedLoaders As New List(Of CompModLoaderType)
                    If File.ModLoaders.Any Then
                        AllowedLoaders = File.ModLoaders
                    ElseIf Project.ModLoaders.Any Then
                        AllowedLoaders = Project.ModLoaders
                    End If
                    Log($"[Comp] {Desc}要求的加载器种类：" & If(AllowedLoaders.Any(), AllowedLoaders.Join(" / "), "无要求"))
                    '判断某个版本是否符合资源要求
                    IsVersionSuitable =
                    Function(Version)
                        If Not Version.IsLoaded Then Version.Load()
                        '只对 Mod 和数据包进行版本检测
                        If Project.Type = CompType.Mod OrElse Project.Type = CompType.DataPack Then
                            If File.GameVersions.Any(Function(v) v.Contains(".")) AndAlso
                               Not File.GameVersions.Any(Function(v) v.Contains(".") AndAlso v = Version.Version.McName) Then Return False
                        End If
                        '加载器
                        If Not AllowedLoaders.Any() Then Return True '无要求
                        If AllowedLoaders.Contains(CompModLoaderType.Forge) AndAlso Version.Version.HasForge Then Return True
                        If AllowedLoaders.Contains(CompModLoaderType.Fabric) AndAlso Version.Version.HasFabric Then Return True
                        If AllowedLoaders.Contains(CompModLoaderType.NeoForge) AndAlso Version.Version.HasNeoForge Then Return True
                        If AllowedLoaders.Contains(CompModLoaderType.LiteLoader) AndAlso Version.Version.HasLiteLoader Then Return True
                        Return False
                    End Function
                    '获取常规资源默认下载位置
                    If CachedFolder IsNot Nothing Then
                        DefaultFolder = CachedFolder
                        Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{DefaultFolder}")
                    ElseIf McVersionCurrent IsNot Nothing AndAlso IsVersionSuitable(McVersionCurrent) Then
                        DefaultFolder = $"{McVersionCurrent.PathIndie}{SubFolder}"
                        Directory.CreateDirectory(DefaultFolder)
                        Log($"[Comp] 使用当前版本作为默认下载位置：{DefaultFolder}")
                    Else
                        '查找所有可能的版本
                        Dim NeedLoad As Boolean = McVersionListLoader.State <> LoadState.Finished
                        If NeedLoad Then
                            Hint("正在查找适合的游戏版本……")
                            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                        End If
                        Dim SuitableVersions = McVersionList.Values.SelectMany(Function(l) l).Where(Function(v) IsVersionSuitable(v)).
                            Select(Function(v) New DirectoryInfo($"{v.PathIndie}{SubFolder}"))
                        If SuitableVersions.Any Then
                            Dim SelectedVersion = SuitableVersions.
                                OrderByDescending(Function(Dir) If(Dir.Exists, Dir.LastWriteTimeUtc, Date.MinValue)). '先按文件夹更改时间降序
                                ThenByDescending(Function(Dir) If(Dir.Exists, Dir.GetFiles().Length, -1)). '再按文件夹中的文件数量降序
                                First()
                            DefaultFolder = SelectedVersion.FullName
                            Directory.CreateDirectory(DefaultFolder)
                            Log($"[Comp] 使用适合的游戏版本作为默认下载位置：{DefaultFolder}")
                        Else
                            DefaultFolder = PathMcFolder
                            If NeedLoad Then
                                Hint("当前 MC 文件夹中没有找到适合此资源的版本！")
                            Else
                                Log("[Comp] 由于当前版本不兼容，使用当前的 MC 文件夹作为默认下载位置")
                            End If
                        End If
                    End If
                End If
                '获取基本信息
                Dim FileName As String
                If Project.TranslatedName = Project.RawName Then
                    FileName = File.FileName
                Else
                    Dim ChineseName As String = Project.TranslatedName.BeforeFirst(" (").BeforeFirst(" - ").
                        Replace("\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("""", "").Replace("： ", "：")
                    Select Case Setup.Get("ToolDownloadTranslateV2")
                        Case 0
                            FileName = $"【{ChineseName}】{File.FileName}"
                        Case 1
                            FileName = $"[{ChineseName}] {File.FileName}"
                        Case 2
                            FileName = $"{ChineseName}-{File.FileName}"
                        Case 3
                            FileName = $"{File.FileName}-{ChineseName}"
                        Case Else
                            FileName = File.FileName
                    End Select
                End If
                RunInUi(
                Sub()
                    '弹窗要求选择保存位置
                    Dim Target As String
                    Target = SelectSaveFile("选择保存位置", FileName,
                        Desc & "文件|" &
                        If(File.Type = CompType.Mod,
                            If(File.FileName.EndsWith(".litemod"), "*.litemod", "*.jar"),
                            If(File.FileName.EndsWith(".mrpack"), "*.mrpack", "*.zip")), DefaultFolder)
                    If Not Target.Contains("\") Then Return
                    '构造步骤加载器
                    Dim LoaderName As String = Desc & "下载：" & GetFileNameWithoutExtentionFromPath(Target) & " "
                    If Target <> DefaultFolder AndAlso File.Type = CompType.Mod Then CachedFolder = GetPathFromFullPath(Target)
                    Dim Loaders As New List(Of LoaderBase)
                    Loaders.Add(New LoaderDownload("下载文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 6, .Block = True})
                    '启动
                    Dim Loader As New LoaderCombo(Of Integer)(LoaderName, Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
                    Loader.Start(1)
                    LoaderTaskbarAdd(Loader)
                    FrmMain.BtnExtraDownload.ShowRefresh()
                    FrmMain.BtnExtraDownload.Ribble()
                End Sub)
            Catch ex As Exception
                Log(ex, "保存资源文件失败", LogLevel.Feedback)
            End Try
        End Sub, "Download CompDetail Save")
    End Sub

    Private Sub BtnIntroWeb_Click(sender As Object, e As EventArgs) Handles BtnIntroWeb.Click
        OpenWebsite(Project.Website)
    End Sub
    Private Sub BtnIntroWiki_Click(sender As Object, e As EventArgs) Handles BtnIntroWiki.Click
        OpenWebsite("https://www.mcmod.cn/class/" & Project.WikiId & ".html")
    End Sub
    Private Sub BtnIntroCopy_Click(sender As Object, e As EventArgs) Handles BtnIntroCopy.Click
        ClipboardSet(CompItem.LabTitle.Text & CompItem.LabTitleRaw.Text)
    End Sub

End Class
