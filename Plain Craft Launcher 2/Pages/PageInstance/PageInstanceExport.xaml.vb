Public Class ExportOption
    Public Property Title As String
    Public Property Description As String
    Public Property Rules As String
    ''' <summary>
    ''' 如果 Rules 为空，则根据 ShowRules 的内容判断是否应该显示这个复选框。
    ''' 如果 ShowRules 也为空，则始终显示。
    ''' </summary>
    Public Property ShowRules As String
    Public Property DefaultChecked As Boolean
    Public Property RequireModLoader As Boolean = False
    Public Property RequireOptiFine As Boolean = False
    Public Property RequireModLoaderOrOptiFine As Boolean = False
End Class

Public Class PageInstanceExport
    Implements IRefreshable

    Private CurrentInstance As String = ""
    Private Sub PageInstanceExport_Loaded() Handles Me.Loaded
        AniControlEnabled += 1
        If CurrentInstance <> PageInstanceLeft.Instance.PathVersion Then RefreshAll() '切换到了另一个版本，重置页面
        CustomEventService.SetEventData(BtnAdvancedHelp,
            If(BuildType = BuildTypes.Release, "指南/整合包制作 - Public.json", "指南/整合包制作 - Snapshot.json"))
        AniControlEnabled -= 1
    End Sub
    Public Sub RefreshAll() Implements IRefreshable.Refresh
        Logger.Info($"刷新导出页面")
        HintOptiFine.Visibility = If(PageInstanceLeft.Instance.Version.HasOptiFine, Visibility.Visible, Visibility.Collapsed)
        CurrentInstance = PageInstanceLeft.Instance.PathVersion
        TextExportName.Text = ""
        TextExportName.HintText = PageInstanceLeft.Instance.Name
        TextExportVersion.Text = ""
        TextExportVersion.HintText = "1.0.0"
        CheckAdvancedInclude.Checked = False
        CheckAdvancedModrinth.Checked = False
        GetExportOption(CheckOptionsBasic).Description = PageInstanceLeft.Instance.VersionDisplayName()
        ResetConfigOverrides()
        ReloadAllSubOptions()
        RefreshAllOptionsUI()
        PanBack.ScrollToHome()
    End Sub

#Region "子选项"
    Private SubOptionBlackList As String() = {"Quark Programmer Art.zip", "+ EuphoriaPatches_"}

    ''' <summary>
    ''' 动态生成子文件夹下的选项，例如资源包、存档等。
    ''' </summary>
    Private Sub ReloadAllSubOptions()
        ReloadSubOptions(PanOptionsResourcePacks, True, True, "resourcepacks", "texturepacks")
        ReloadSubOptions(PanOptionsSaves, False, True, "saves")
        ReloadSubOptions(PanOptionsShaderPacks, True, True, "shaderpacks")
    End Sub

    Private Sub ReloadSubOptions(Panel As StackPanel, AcceptCompressedFile As Boolean, AcceptFolder As Boolean, ParamArray Folders As String())
        Panel.Children.Clear()
        For Each Folder In Folders
            Dim TargetFolder = DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie & Folder)
            If Not TargetFolder.Exists() Then Continue For
            '查找文件夹下的对应项
            If AcceptCompressedFile Then
                For Each File In TargetFolder.EnumerateFiles("*.zip").Concat(TargetFolder.EnumerateFiles("*.rar"))
                    If SubOptionBlackList.Any(Function(b) File.Name.Contains(b)) Then Continue For
                    Panel.Children.Add(New MyCheckBox With {. _
                        Tag = New ExportOption With {.Title = File.Name, .DefaultChecked = True, .Rules = EscapeUtils.LikePatternEscape($"{Folder}/{File.Name}")}})
                Next
            End If
            If AcceptFolder Then
                For Each SubFolder In TargetFolder.EnumerateDirectories().OrderByDescending(Function(f) f.LastWriteTime)
                    If SubOptionBlackList.Any(Function(b) SubFolder.Name.Contains(b)) Then Continue For
                    If Not SubFolder.EnumerateFileSystemInfos().Any() Then Continue For
                    Dim NewCheckBox As New MyCheckBox With {. _
                        Tag = New ExportOption With {.Title = SubFolder.Name, .DefaultChecked = True, .Rules = EscapeUtils.LikePatternEscape($"{Folder}/{SubFolder.Name}/")}}
                    If Panel Is PanOptionsSaves Then GetExportOption(NewCheckBox).Description = SubFolder.LastWriteTime.ToString("yyyy'/'MM'/'dd HH':'mm")
                    Panel.Children.Add(NewCheckBox)
                Next
            End If
        Next
    End Sub

#End Region

#Region "选项"

    ''' <summary>
    ''' 重新确认是否应该显示每个选项，并将 ExportOption 同步到 UI。
    ''' </summary>
    Private Sub RefreshAllOptionsUI()
        '预先归纳所有至多二级的文件/文件夹
        Dim AllEntries As New List(Of String)
        Dim IsValidDirectory = '检查文件夹不为空
        Function(Folder As DirectoryInfo) As Boolean
            Try
                Return Folder.Exists AndAlso
                    Folder.EnumerateFileSystemInfos().Any(Function(i) Not SubOptionBlackList.Any(Function(b) i.Name.Contains(b)))
            Catch '一般是由于无法访问，或是一个指向已不存在的文件夹的链接（例如使用 mklink 创造的 resource 文件夹链接）
                Return False
            End Try
        End Function
        Dim PathInfo = DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie)
        AllEntries.AddRange(PathInfo.EnumerateFiles().Select(Function(f) f.Name))
        For Each SubFolder In PathInfo.EnumerateDirectories().Where(IsValidDirectory)
            AllEntries.Add($"{SubFolder.Name}\")
            AllEntries.AddRange(SubFolder.EnumerateFiles().Select(Function(f) $"{SubFolder.Name}\{f.Name}"))
            AllEntries.AddRange(SubFolder.EnumerateDirectories().Where(IsValidDirectory).Select(Function(d) $"{SubFolder.Name}\{d.Name}\"))
        Next
        Logger.Info($"共发现 {AllEntries.Count} 个可行的二级文件/文件夹")
        '确认选项是否应该被显示
        Dim IsVisible =
        Function(TargetOption As ExportOption) As Boolean
            '检查需要 OptiFine 或 Mod 加载器 (改写为显式 End If 块，防止折行导致编译报错)
            If TargetOption.RequireOptiFine AndAlso Not PageInstanceLeft.Instance.Version.HasOptiFine Then
                Return False
            End If
            If TargetOption.RequireModLoader AndAlso Not PageInstanceLeft.Instance.Modable Then
                Return False
            End If
            If TargetOption.RequireModLoaderOrOptiFine AndAlso Not PageInstanceLeft.Instance.Version.HasOptiFine AndAlso Not PageInstanceLeft.Instance.Modable Then
                Return False
            End If
            '粗略检查是否可能有符合规则的文件/文件夹
            Return StandardizeLines(If(TargetOption.Rules, TargetOption.ShowRules).Split("|"c), True).Any(
            Function(Rule As String)
                If Rule.StartsWithF("!") Then Return False '只看正向规则
                '检查前两级
                Try
                    If AllEntries.Any(Function(Entry) Entry.Lower Like Rule.Lower) Then Return True
                Catch ex As Exception
                    Logger.Error(ex, $"错误的规则：{Rule}", LogBehavior.Toast)
                    Return False
                End Try
                '粗略检查所有级
                Rule = Rule.Trim("*?".ToCharArray)
                If Rule.Split("\"c, True).Count >= 3 Then
                    If Rule.EndsWithF("\") Then
                        Return IsValidDirectory(DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie & Rule)) '文件夹有效
                    Else
                        Return FileUtils.Exists(PageInstanceLeft.Instance.PathIndie & Rule) '文件有效
                    End If
                Else
                    Return False
                End If
            End Function)
        End Function
        '逐个检查选项
        For Each CheckBox In GetAllOptions(True)
            Dim TargetOption = GetExportOption(CheckBox)
            '名称与简介
            CheckBox.Inlines.Clear()
            CheckBox.Inlines.Add(New Run(TargetOption.Title))
            If Not String.IsNullOrEmpty(TargetOption.Description) Then
                CheckBox.Inlines.Add(New Run("   " & TargetOption.Description) With {.Foreground = ColorGray5})
            End If
            '可见性、默认勾选
            If String.IsNullOrEmpty(TargetOption.Rules) AndAlso String.IsNullOrEmpty(TargetOption.ShowRules) Then
                CheckBox.Visibility = Visibility.Visible
                CheckBox.Checked = TargetOption.DefaultChecked
            Else
                Dim Pass As Boolean = IsVisible(TargetOption)
                CheckBox.Visibility = If(Pass, Visibility.Visible, Visibility.Collapsed)
                CheckBox.Checked = TargetOption.DefaultChecked AndAlso Pass
            End If
        Next
    End Sub

    ''' <summary>
    ''' 对文本行进行标准化处理，以便使用 Like 进行匹配。
    ''' </summary>
    Private Iterator Function StandardizeLines(Raw As IEnumerable(Of String), AddSuffixStarToFolderPath As Boolean) As IEnumerable(Of String)
        For Each IgnoreLine In Raw
            IgnoreLine = IgnoreLine.Trim
            If IgnoreLine = "" OrElse IgnoreLine.StartsWithF("#") OrElse IgnoreLine.StartsWithF("=") Then Continue For
            IgnoreLine = IgnoreLine.Replace("/", "\")
            Yield IgnoreLine & If(IgnoreLine.EndsWithF("\") AndAlso AddSuffixStarToFolderPath, "*", "")
        Next
    End Function

    ''' <summary>
    ''' 获取所有可作为选项的 CheckBox。
    ''' </summary>
    Private Iterator Function GetAllOptions(IncludeHidden As Boolean) As IEnumerable(Of MyCheckBox)
        For Each Element In PanOptions.Children
            If Not IncludeHidden AndAlso Element.Visibility <> Visibility.Visible Then Continue For
            If TypeOf Element Is MyCheckBox Then
                Yield Element
            ElseIf TypeOf Element Is StackPanel Then
                For Each SubElement In DirectCast(Element, StackPanel).Children
                    If Not IncludeHidden AndAlso SubElement.Visibility <> Visibility.Visible Then Continue For
                    If TypeOf SubElement Is MyCheckBox Then Yield SubElement
                Next
            End If
        Next
    End Function

    ''' <summary>
    ''' 获取该 CheckBox 对应的 ExportOption。
    ''' </summary>
    Private Function GetExportOption(CheckBox As MyCheckBox) As ExportOption
        Return CType(CheckBox.Tag, ExportOption)
    End Function

#End Region

#Region "配置文件"
    Private Const Sperator As String = "=============================================================="

    ' ================ 导出内容段 ================

    ''' <summary>
    ''' 从配置文件中读取的规则。
    ''' 如果不为 Nothing，则会覆写当前勾选的规则并禁用对应 UI。
    ''' </summary>
    Private Property RulesOverrides As List(Of String)
        Get
            Return _RulesOverrides
        End Get
        Set(value As List(Of String))
            _RulesOverrides = value
            If value Is Nothing Then
                BtnOverrideCancel.Visibility = Visibility.Collapsed
                PanOptions.Visibility = Visibility.Visible
                CardOptions.Inlines.Clear()
                CardOptions.Inlines.Add(New Run("导出内容列表") With {.FontWeight = FontWeights.Bold})
            Else
                BtnOverrideCancel.Visibility = Visibility.Visible
                PanOptions.Visibility = Visibility.Collapsed
                CardOptions.Inlines.Clear()
                CardOptions.Inlines.Add(New Run("导出内容列表:    ") With {.FontWeight = FontWeights.Bold})
                CardOptions.Inlines.Add(New Run("从配置文件中读取") With {.FontWeight = FontWeights.Normal})
            End If
        End Set
    End Property
    Private _RulesOverrides As List(Of String) = Nothing
    ''' <summary>
    ''' 获取当前实际生效的所有规则。
    ''' </summary>
    Private Iterator Function GetAllRules() As IEnumerable(Of String)
        If RulesOverrides IsNot Nothing Then
            '返回覆盖的列表
            For Each Rule In RulesOverrides
                Yield Rule
            Next
        Else
            '从当前勾选的所有选项中获取所有规则行
            Yield ""
            Yield "# 修改下方的规则以控制需要导出的内容。"
            Yield "# 以 ! 开头以反选。可以使用 *、?、[] 通配符。靠后的行覆盖靠前的。"
            Yield ""
            For Each CheckBox In GetAllOptions(False)
                If Not CheckBox.Checked Then Continue For
                Dim TargetOption = GetExportOption(CheckBox)
                If TargetOption.Rules Is Nothing Then Continue For
                Yield $"# {TargetOption.Title}"
                For Each Rule In TargetOption.Rules.Split("|"c)
                    Yield Rule
                Next
                Yield ""
            Next
            Yield "# 排除的文件"
            Yield "!*.log"
            Yield "!*.dat_old"
            Yield "!*.BakaCoreInfo"
            Yield "!hmclversion.cfg"
            Yield "!log4j2.xml"
            Yield ""
        End If
    End Function

    ' ================ 追加内容段 ================

    Private ExtraFiles As List(Of String) = Nothing
    ''' <summary>
    ''' 获取当前实际生效的追加内容。
    ''' </summary>
    Private Iterator Function GetExtraFileLines() As IEnumerable(Of String)
        If ExtraFiles IsNot Nothing Then
            '返回覆盖的列表
            For Each File In ExtraFiles
                Yield File
            Next
        Else
            '从当前勾选的所有选项中获取所有规则行
            Yield ""
            Yield "# 如果想将额外的文件自动放到压缩包根目录中，可以将它们的路径写在下方。"
            Yield "# 必须是完整路径。每行中，若以 \ 结尾则代表是文件夹，不以 \ 结尾则代表是文件。"
            Yield ""
        End If
    End Function

    ' ================ 重置 ================

    ''' <summary>
    ''' 重置配置文件所带来的影响。
    ''' </summary>
    Private Sub ResetConfigOverrides()
        RulesOverrides = Nothing
        ConfigPackPath = Nothing
        ExtraFiles = Nothing
        PanBack.ScrollToHome()
    End Sub
    Private Sub CardOptions_MouseLeftButtonDown() Handles CardOptions.MouseLeftButtonDown
        If RulesOverrides Is Nothing Then Return
        ResetConfigOverrides()
    End Sub

    ' ================ 保存 / 读取 ================

    '保存配置文件
    Private Sub ExportConfig() Handles BtnAdvancedExport.Click
        Try
            Dim ConfigPath As String = Dialogs.SaveFile("选择文件位置", "export_config.txt", Settings.Get(Of String)("CacheExportConfig"), {("txt", "整合包导出配置")})
            If ConfigPath Is Nothing Then Return
            Settings.Set("CacheExportConfig", ConfigPath)
            Dim ConfigLines As New List(Of String)
            'ini 段
            ConfigLines.Add("Name:" & TextExportName.Text)
            ConfigLines.Add("Version:" & TextExportVersion.Text)
            ConfigLines.Add("")
            ConfigLines.Add("# 是否打包正式版 PCL，以便没有启动器的玩家安装整合包。")
            ConfigLines.Add("IncludeLauncher:" & CheckOptionsPcl.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("# 是否打包 PCL 个性化内容，例如功能隐藏设置、主页、背景音乐和图片等。")
            ConfigLines.Add("IncludeLauncherCustom:" & CheckOptionsPclCustom.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("# 是否将 Mod、资源包、光影包的文件直接放入整合包中，这样在导入时就无需联网下载它们。")
            ConfigLines.Add("# 建议仅在无法稳定连接 CurseForge 或 Modrinth 时才考虑启用。")
            ConfigLines.Add("# 二次分发可能违反使用协议，请尽量不要公开发布包含资源文件的整合包！")
            ConfigLines.Add("DontCheckHostedAssets:" & CheckAdvancedInclude.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("# 若资源未在 Modrinth 上托管，则将其原始文件直接放入整合包中。")
            ConfigLines.Add("# 如果你想将整合包上传到 Modrinth，这能帮助你分辨哪些文件未在 Modrinth 上托管。")
            ConfigLines.Add("# 此选项与 DontCheckHostedAssets 冲突。")
            ConfigLines.Add("ModrinthUploadMode:" & CheckAdvancedModrinth.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("# 导出的文件的存放位置。")
            ConfigLines.Add("# 若设置了此项，在导出时会直接将文件放到此路径，不会弹窗要求选择。")
            ConfigLines.Add("# 若 IncludeLauncher 为 True，应以 .zip 结尾；若为 False，应以 .mrpack 结尾。")
            ConfigLines.Add("PackPath:" & If(ConfigPackPath, ""))
            ConfigLines.Add("")
            '导出内容段
            ConfigLines.Add(Sperator)
            ConfigLines.AddRange(GetAllRules())
            '追加内容段
            ConfigLines.Add(Sperator)
            ConfigLines.AddRange(GetExtraFileLines())
            '结束
            FileUtils.Write(ConfigPath, ConfigLines.Join(vbCrLf))
            Hint("已保存配置文件：" & ConfigPath, HintType.Green)
            OpenExplorer(ConfigPath)
        Catch ex As Exception
            Logger.Error(ex, "保存配置失败", LogBehavior.Alert)
        End Try
    End Sub
    '读取配置文件
    Private Sub ImportConfig() Handles BtnAdvancedImport.Click
        Try
            Dim ConfigPath As String = Dialogs.SelectFile("选择配置文件", False, Settings.Get(Of String)("CacheExportConfig"), {({"txt"}, "整合包导出配置")}).FirstOrDefault()
            If String.IsNullOrEmpty(ConfigPath) Then Return
            Settings.Set("CacheExportConfig", ConfigPath)
            Dim Segments As String() = FileUtils.ReadAsString(ConfigPath).Split(Sperator)
            'ini 段
            Dim Ini As New Dictionary(Of String, String)
            For Each Line In Segments(0).SplitLines(True)
                Line = Line.Trim
                If Line = "" OrElse Line.StartsWithF("#") OrElse Line.StartsWithF("=") Then Continue For
                Dim Index As Integer = Line.IndexOfF(":")
                If Index > 0 Then Ini(Line.Substring(0, Index)) = Line.Substring(Index + 1)
            Next
            TextExportName.Text = Ini.GetOrDefault("Name", "")
            TextExportVersion.Text = Ini.GetOrDefault("Version", "")
            CheckOptionsPcl.Checked = Ini.GetOrDefault("IncludeLauncher", True)
            CheckOptionsPclCustom.Checked = Ini.GetOrDefault("IncludeLauncherCustom", True)
            CheckAdvancedInclude.Checked = Ini.GetOrDefault("DontCheckHostedAssets", False)
            If Not CheckAdvancedInclude.Checked Then CheckAdvancedModrinth.Checked = Ini.GetOrDefault("ModrinthUploadMode", False) '#7979，加个特判
            ConfigPackPath = Ini.GetOrDefault("PackPath", Nothing)
            '导出内容段
            RulesOverrides = Segments(1).SplitLines(True).ToList
            '追加内容段
            If Segments.Length > 2 Then
                ExtraFiles = Segments(2).SplitLines(True).ToList
            Else
                ExtraFiles = Nothing
            End If
            '结束
            Hint("已读取配置文件：" & ConfigPath, HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "读取配置失败", LogBehavior.Alert)
        End Try
    End Sub

#End Region

#Region "导出"

    ''' <summary>
    ''' 配置文件中指定的导出位置。
    ''' </summary>
    Private ConfigPackPath As String = Nothing

    ''' <summary>
    ''' 开始导出。
    ''' </summary>
    Private Sub StartExport() Handles BtnExport.Click
        Dim PackName As String = If(String.IsNullOrEmpty(TextExportName.Text), TextExportName.HintText, TextExportName.Text)
        Dim PackVersion As String = If(String.IsNullOrEmpty(TextExportVersion.Text), "1.0.0", TextExportVersion.Text)

        '重复任务检查
        Dim LoaderName As String = $"导出整合包：" & PackName
        For Each OngoingLoader In LoaderTaskbar
            If OngoingLoader.Name <> LoaderName Then Continue For
            FrmMain.PageChange(FormMain.PageType.DownloadManager)
            Return
        Next

        '确认导出位置
        Dim PackPath As String = Nothing
        If Not String.IsNullOrWhiteSpace(ConfigPackPath) AndAlso
           (Not ConfigPackPath.EndsWithF("\") AndAlso Not ConfigPackPath.EndsWithF("/")) Then
            Try
                DirectoryUtils.Create(PathUtils.RemoveLastPart(ConfigPackPath))
                PackPath = ConfigPackPath
                Logger.Info($"使用配置文件中指定的导出路径：{ConfigPackPath}")
            Catch ex As Exception
                Logger.Warn(ex, $"无法使用配置文件中指定的导出路径（{ConfigPackPath}）")
                If MyMsgBox($"指定的路径：{ConfigPackPath}{vbCrLf}{vbCrLf}{ex.GetDisplay(True)}", "无法使用配置文件中指定的导出路径", "确定", "取消") = 2 Then Return
            End Try
        End If
        If PackPath Is Nothing Then
            Dim Extensions As New List(Of (Extension As String, Display As String))
            Extensions.Add(("zip", "压缩文件"))
            If Not CheckOptionsPcl.Checked Then Extensions.Add(("mrpack", "Modrinth 整合包文件"))
            PackPath = Dialogs.SaveFile("选择导出位置",
                PackName & If(String.IsNullOrEmpty(TextExportVersion.Text), "", " " & TextExportVersion.Text), filter:=Extensions)
            Logger.Info($"手动指定的导出路径：{PackPath}")
        End If
        If PackPath Is Nothing Then Return

        '缓存所需参数（主线程缓存，避免跨线程访问 UI 崩溃）
        Dim CacheFolder = RequestTaskTempFolder()
        Dim OverridesFolder = CacheFolder & "modpack\overrides\"
        Dim McVersion = PageInstanceLeft.Instance
        Dim PathIndie As String = McVersion.PathIndie
        Dim CheckHostedAssets As Boolean = Not CheckAdvancedInclude.Checked
        Dim ModrinthUploadMode As Boolean = CheckAdvancedModrinth.Checked
        Dim IncludePCL As Boolean = CheckOptionsPcl.Checked
        Dim IncludePCLCustom As Boolean = IncludePCL AndAlso CheckOptionsPclCustom.Checked
        Dim AllRules = StandardizeLines(GetAllRules(), True).ToList()
        Dim AllExtraFiles = StandardizeLines(GetExtraFileLines(), False).ToList()
        
        ' 缓存 CheckBox 状态变量，避免异步线程访问 UI 崩溃
        Dim ExcludeClientOnly As Boolean = CheckExcludeClientOnly.Checked
        Dim ExcludeServerOnly As Boolean = CheckExcludeServerOnly.Checked
        
        ' 判定是否为 1.12.2 及以下版本（老版本不含 data/ 目录）
        Dim IsLegacy As Boolean = False
        Try
            Dim versionParts = McVersion.Version.VanillaName.Split("."c)
            If versionParts.Length >= 2 AndAlso Val(versionParts(1)) <= 12 Then IsLegacy = True
        Catch ex As Exception
        End Try
        ' ========================================================

        Logger.Info($"准备导出整合包，共有 {AllRules.Count} 条规则，{AllExtraFiles.Count} 条追加内容行")

        '构造步骤加载器
        Dim Loaders As New List(Of LoaderBase)

#Region "准备 PCL 文件"

        If BuildType <> BuildTypes.Release AndAlso IncludePCL Then
            Loaders.Add(New LoaderTask(Of Integer, Integer)("下载 PCL 正式版",
            Sub(Loader As LoaderTask(Of Integer, Integer))
                Try
                    DownloadLatestPCL(Loader)
                Catch ex As Exception
                    Logger.Warn(ex, "下载 PCL 正式版失败，将使用当前运行的程序作为备用")
                End Try
                
                ' 如果下载失败（未在 PathTemp 下找到 Latest.exe），则使用当前运行的程序作为备用
                Dim SourceExePath As String = PathTemp & "Latest.exe"
                If Not FileUtils.Exists(SourceExePath) Then
                    SourceExePath = PathExe
                    Logger.Info($"[Export] 下载最新 PCL 失败，已使用当前运行的程序进行备用: {PathExe}")
                End If
                
                FileUtils.Copy(SourceExePath, CacheFolder & "Plain Craft Launcher.exe")
            End Sub) With {.ProgressWeight = 0.5, .Block = False})
        End If

#End Region

#Region "复制文件"

        Loaders.Add(New LoaderTask(Of Integer, List(Of LocalResourceFile))("复制导出内容",
        Sub(Loader As LoaderTask(Of Integer, List(Of LocalResourceFile)))
            Loader.Output = New List(Of LocalResourceFile)
            
            ' ========================================================
            ' 动态排除：批量联网分析 mods 目录下的环境归属
            ' ========================================================
            If ExcludeClientOnly OrElse ExcludeServerOnly Then
                Dim ModsDir As String = Path.Combine(PathIndie, "mods")
                If Directory.Exists(ModsDir) Then
                    Dim JarFiles = Directory.GetFiles(ModsDir, "*.jar", SearchOption.TopDirectoryOnly).ToList()
                    If JarFiles.Count > 0 Then
                        Logger.Info($"[Export] 开始分析 {JarFiles.Count} 个 Mod 的环境归属...")
                        ModSideDetector.PreScanModsOnline(JarFiles, IsLegacy)
                        Logger.Info("[Export] 模组归属分析判定完毕。")
                    End If
                End If
            End If
            ' ========================================================

            '复制版本文件
            Dim Progress As Integer = 0
            Dim SearchFolder As Action(Of DirectoryInfo)
            SearchFolder =
            Sub(Folder As DirectoryInfo)
                '文件夹：进一步搜索
                For Each SubFolder In Folder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                    '跳过部分又没用文件又多的文件夹，加快搜索
                    If {"assets", "versions", "libraries"}.Contains(SubFolder.Name) AndAlso PathIndie = PathUtils.RemoveExtendedPrefix(Folder.FullName) Then Continue For
                    If {"structureCacheV1", ".fabric", ".git", "avatar-cache", "cosmetic-cache"}.Contains(SubFolder.Name) Then Continue For
                    SearchFolder(SubFolder)
                Next
                '文件：检查规则并复制
                For Each Entry In Folder.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    Dim RelativePath As String = Entry.FullName.AfterFirst(PathIndie)
                    '检查规则
                    Dim ShouldKeep As Boolean = False
                    For Each Rule In AllRules
                        Dim Revert = Rule.StartsWithF("!")
                        If RelativePath.Lower Like Rule.TrimStart("!").Lower Then ShouldKeep = Not Revert
                    Next
                    If Not ShouldKeep Then Continue For

                    ' ==========================================
                    ' 过滤仅客户端/仅服务端 Mod
                    ' ==========================================
                    If RelativePath.StartsWithF("mods\") AndAlso RelativePath.EndsWithF(".jar") Then
                        ' 排除仅客户端 Mod
                        If ExcludeClientOnly Then
                            If ModSideDetector.GetModSide(Entry.FullName, IsLegacy) = ModSideDetector.ModSide.ClientOnly Then
                                Logger.Info($"[Export] 已过滤仅客户端 Mod: {RelativePath}")
                                Continue For
                            End If
                        End If

                        ' 排除仅服务端 Mod
                        If ExcludeServerOnly Then
                            If ModSideDetector.GetModSide(Entry.FullName, IsLegacy) = ModSideDetector.ModSide.ServerOnly Then
                                Logger.Info($"[Export] 已过滤仅服务端 Mod: {RelativePath}")
                                Continue For
                            End If
                        End If
                    End If
                    ' ==========================================

                    Dim TargetPath As String = OverridesFolder & RelativePath
                    FileUtils.Copy(Entry.FullName, TargetPath)
                    '若为压缩包，考虑联网获取路径
                    If CheckHostedAssets AndAlso
                       {".zip", ".rar", ".jar", ".disabled", ".old"}.Contains(Entry.Extension.Lower) AndAlso
                       {"mods", "packs", "openloader", "resource"}.Any(Function(s) RelativePath.Contains(s)) Then
                        Dim ModFile As New LocalResourceFile(TargetPath)
                        Dim Unused = ModFile.ModrinthHash '提前计算 Hash
                        Unused = ModFile.CurseForgeHash
                        Loader.Output.Add(ModFile)
                    End If
                    '更新进度（进度并不准确，主要突出一个我还没似）
                    Progress += 1
                    If Progress = 25 Then
                        Loader.Progress += (0.94 - Loader.Progress) * 0.012
                        Progress = 0
                    End If
                Next
            End Sub
            SearchFolder(DirectoryUtils.GetInfo(PathIndie))
            Logger.Info($"复制 overrides 文件完成，有 {Loader.Output.Count} 个文件需要联网检查")
            Loader.Progress = 0.95
            '复制追加内容到根目录
            Dim BaseFolder As String = If(IncludePCL, CacheFolder, CacheFolder & "modpack\")
            For Each Line In AllExtraFiles
                If Line.EndsWithF("\") OrElse Line.EndsWithF("/") Then
                    If DirectoryUtils.Exists(Line) Then
                        DirectoryUtils.Copy(Line, BaseFolder & PathUtils.GetLastPart(Line) & "\")
                    Else
                        Hint($"未找到配置文件中指定的文件夹：{Line}", HintType.Red)
                    End If
                Else
                    If FileUtils.Exists(Line) Then
                        FileUtils.Copy(Line, BaseFolder & PathUtils.GetLastPart(Line))
                    Else
                        Hint($"未找到配置文件中指定的单个文件：{Line}", HintType.Red)
                    End If
                End If
            Next
            Loader.Progress = 0.97
            '复制 PCL 版本设置
            If DirectoryUtils.Exists(McVersion.PathVersion & "PCL\") Then DirectoryUtils.Copy(McVersion.PathVersion & "PCL\", OverridesFolder & "PCL\")
            WriteIni(OverridesFolder & "PCL\Setup.ini", "IsStar", False)
            '复制 PCL 本体（正式版）
            If BuildType = BuildTypes.Release AndAlso IncludePCL Then FileUtils.Copy(PathExe, CacheFolder & "Plain Craft Launcher.exe")
            '复制 PCL 个性化内容
            If IncludePCLCustom Then
                If DirectoryUtils.Exists(PathExeFolder & "PCL\Pictures\") Then DirectoryUtils.Copy(PathExeFolder & "PCL\Pictures\", CacheFolder & "PCL\Pictures\")
                If DirectoryUtils.Exists(PathExeFolder & "PCL\Musics\") Then DirectoryUtils.Copy(PathExeFolder & "PCL\Musics\", CacheFolder & "PCL\Musics\")
                If DirectoryUtils.Exists(PathExeFolder & "PCL\Help\") Then DirectoryUtils.Copy(PathExeFolder & "PCL\Help\", CacheFolder & "PCL\Help\")
                If FileUtils.Exists(PathExeFolder & "PCL\Custom.xaml") Then FileUtils.Copy(PathExeFolder & "PCL\Custom.xaml", CacheFolder & "PCL\Custom.xaml")
                If FileUtils.Exists(PathExeFolder & "PCL\Setup.ini") Then FileUtils.Copy(PathExeFolder & "PCL\Setup.ini", CacheFolder & "PCL\Setup.ini")
                If FileUtils.Exists(PathExeFolder & "PCL\hints.txt") Then FileUtils.Copy(PathExeFolder & "PCL\hints.txt", CacheFolder & "PCL\hints.txt")
                If FileUtils.Exists(PathExeFolder & "PCL\Logo.png") Then FileUtils.Copy(PathExeFolder & "PCL\Logo.png", CacheFolder & "PCL\Logo.png")
            End If
        End Sub) With {.ProgressWeight = 5})

#End Region

#Region "联网检查"

        Loaders.Add(New LoaderTask(Of List(Of LocalResourceFile), Dictionary(Of LocalResourceFile, List(Of String)))("联网获取文件信息",
        Sub(Loader As LoaderTask(Of List(Of LocalResourceFile), Dictionary(Of LocalResourceFile, List(Of String))))
            Loader.Output = New Dictionary(Of LocalResourceFile, List(Of String))
            If Not CheckHostedAssets Then Logger.Info($"要求跳过联网获取步骤") : Return
            If Not Loader.Input.Any Then Logger.Info($"没有需要联网检查的文件，跳过联网获取步骤") : Return

            '分平台获取下载地址
            Dim EndedThreadCount As Integer = 0, FailedExceptions As New List(Of Exception)

            '从 Modrinth 获取信息
            RunInNewThread(
            Sub()
                Try
                    Dim ModrinthHashes = Loader.Input.Select(Function(m) m.ModrinthHash)
                    Dim ModrinthRaw As JObject = DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                        $"{{""hashes"": [""{ModrinthHashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")
                    For Each ModFile In Loader.Input
                        '查找对应的文件
                        If Not ModrinthRaw.ContainsKey(ModFile.ModrinthHash) Then Continue For
                        If ModrinthRaw(ModFile.ModrinthHash)?("files")?(0)("hashes")?("sha1") <> ModFile.ModrinthHash Then Continue For
                        '写入下载地址
                        Loader.Output.AddIntoValueCollection(ModFile, ModrinthRaw(ModFile.ModrinthHash)("files")(0)("url"))
                    Next
                    Logger.Info($"从 Modrinth 获取到 {ModrinthRaw.Count} 个本地资源项的对应信息")
                Catch ex As Exception
                    Logger.Warn(ex, "从 Modrinth 获取本地 Mod 信息失败")
                    FailedExceptions.Add(ex)
                Finally
                    EndedThreadCount += 1
                    Loader.Progress += 0.45
                End Try
            End Sub, "Modrinth - " & LoaderName)

            '从 CurseForge 获取信息
            RunInNewThread(
            Sub()
                Try
                    If ModrinthUploadMode Then Return 'Modrinth 上传模式下，不从 CurseForge 获取信息
                    Dim CurseForgeHashes = Loader.Input.Select(Function(m) m.CurseForgeHash)
                    Dim CurseForgeRaw As JContainer = DlModRequest("https://api.curseforge.com/v1/fingerprints/432/", HttpMethod.Post,
                        $"{{""fingerprints"": [{CurseForgeHashes.Join(",")}]}}", "application/json")("data")("exactMatches")
                    For Each ResultJson As JObject In CurseForgeRaw
                        If Not ResultJson.ContainsKey("file") Then Continue For
                        Dim File = ResultJson("file")
                        If String.IsNullOrEmpty(File("downloadUrl")) Then Continue For
                        '查找对应的文件
                        Dim ModFile As LocalResourceFile = Loader.Input.FirstOrDefault(Function(m) m.CurseForgeHash = File("fileFingerprint").ToObject(Of UInteger))
                        If ModFile Is Nothing Then Continue For
                        '写入下载地址
                        For Each Address In ResourceVersion.ParseCurseForgeDownloadUrls(File("downloadUrl").ToString)
                            Loader.Output.AddIntoValueCollection(ModFile, Address)
                        Next
                    Next
                    Logger.Info($"从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地资源项的对应信息")
                Catch ex As Exception
                    Logger.Warn(ex, "从 CurseForge 获取本地 Mod 信息失败")
                    FailedExceptions.Add(ex)
                Finally
                    EndedThreadCount += 1
                    Loader.Progress += 0.45
                End Try
            End Sub, "CurseForge - " & LoaderName)

            '等待线程结束
            Do Until EndedThreadCount = 2
                If Loader.IsInterrupted Then Return
                Thread.Sleep(10)
            Loop

            '若失败，确认是否继续
            If FailedExceptions.Count = 1 Then
                If MyMsgBox("联网获取部分文件信息失败，是否继续导出？" & vbCrLf & vbCrLf &
                            "若继续，无法获取信息的文件将被直接打包。" & vbCrLf &
                            "由于二次分发可能违反使用协议，请尽量不要公开发布导出的整合包！",
                            "部分文件信息获取失败", "继续", "取消") = 2 Then Throw FailedExceptions.First
            ElseIf FailedExceptions.Count > 1 Then
                If MyMsgBox("联网获取文件信息失败，是否继续导出？" & vbCrLf & vbCrLf &
                            "若继续，所有文件都将被直接打包。" & vbCrLf &
                            "由于二次分发可能违反使用协议，请尽量不要公开发布导出的整合包！",
                            "文件信息获取失败", "继续", "取消") = 2 Then Throw FailedExceptions.First
            End If
        End Sub) With {.Show = CheckHostedAssets, .ProgressWeight = If(CheckHostedAssets, 2, 0.01)})

#End Region

#Region "生成压缩包"

        Loaders.Add(New LoaderTask(Of Dictionary(Of LocalResourceFile, List(Of String)), Integer)("生成压缩包",
        Sub(Loader As LoaderTask(Of Dictionary(Of LocalResourceFile, List(Of String)), Integer))
            '整理文件列表
            Dim Files As New JArray
            For Each Pair In Loader.Input
                Dim ModFile As LocalResourceFile = Pair.Key
                Files.Add(New JObject From {
                    {"path", PathUtils.RemoveExtendedPrefix(ModFile.File.FullName).AfterFirst(OverridesFolder).Replace("\", "/")},
                    {"hashes", New JObject From {{"sha1", ModFile.ModrinthHash}, {"sha512", CryptographyUtils.ComputeFileHash(ModFile.File.FullName, CryptographyUtils.HashMethod.Sha512)}}},
                    {"downloads", New JArray(Pair.Value.OrderBy(Function(u) u.Contains("modrinth.com")))}, '不优先选择 Modrinth
                    {"fileSize", ModFile.File.Length}
                })
                ModFile.File.Delete()
            Next
            Loader.Progress = 0.2
            '导出最终 JSON 文件
            Dim Dependencies As New JArray
            Dim McVersionStr As String = McVersion.Version.VanillaName
            Dependencies.Add(New JObject From {{"id", "minecraft"}, {"version", McVersionStr}})
            If McVersion.Version.HasForge Then Dependencies.Add(New JObject From {{"id", "forge"}, {"version", McVersion.Version.Forge}})
            If McVersion.Version.HasFabric Then Dependencies.Add(New JObject From {{"id", "fabric-loader"}, {"version", McVersion.Version.Fabric}})
            If McVersion.Version.HasNeoForge Then Dependencies.Add(New JObject From {{"id", "neoforge"}, {"version", McVersion.Version.NeoForge}})
            Dim ResultJson As New JObject From {
                {"game", "minecraft"},
                {"formatVersion", 1},
                {"versionId", PackVersion},
                {"name", PackName},
                {"summary", McVersion.Info},
                {"files", Files},
                {"dependencies", Dependencies}
            }
            FileUtils.Write(CacheFolder & "modpack\modrinth.index.json", ResultJson.ToString(Newtonsoft.Json.Formatting.Indented))
            '打包
            If IncludePCL Then
                '首次压缩整合包
                FileUtils.CreateZipFromDirectory(CacheFolder & "modpack.mrpack", CacheFolder & "modpack\")
                Loader.Progress = 0.5
                DirectoryUtils.Delete(CacheFolder & "modpack\")
                Loader.Progress = 0.6
                '二次压缩整合包
                FileUtils.CreateZipFromDirectory(PackPath, CacheFolder)
                Loader.Progress = 0.9
            Else
                '直接压缩整合包
                FileUtils.CreateZipFromDirectory(PackPath, CacheFolder & "modpack\")
                Loader.Progress = 0.8
            End If
            OpenExplorer(PackPath)
        End Sub) With {.ProgressWeight = 6})

#End Region

        '启动
        Dim MainLoader As New LoaderCombo(Of String)(LoaderName, Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
        MainLoader.Start()
        LoaderTaskbarAdd(MainLoader)
        FrmMain.BtnExtraDownload.ShowRefresh()
        FrmMain.BtnExtraDownload.Ribble()
        FrmMain.PageChange(FormMain.PageType.DownloadManager)
    End Sub

#End Region

    '自动填写整合包名称
    Private Sub TextExportName_GotFocus() Handles TextExportName.GotFocus
        If TextExportName.Text = "" Then
            TextExportName.Text = TextExportName.HintText
            TextExportName.SelectionStart = TextExportName.Text.Length
        End If
    End Sub

    '勾选打包资源文件时，禁止开启 Modrinth 上传模式
    Private Sub CheckAdvancedInclude_Change(sender As Object, user As Boolean) Handles CheckAdvancedInclude.Change
        If CheckAdvancedInclude.Checked Then CheckAdvancedModrinth.Checked = False
        CheckAdvancedModrinth.IsEnabled = Not CheckAdvancedInclude.Checked
    End Sub

End Class

Public Class ModSideDetector

    Public Enum ModSide
        Both        ' 双端通用 / 核心
        ClientOnly  ' 仅客户端
        ServerOnly  ' 仅服务端
    End Enum

    ' 线程安全的缓存字典，防并发访问冲突
    Private Shared ReadOnly Cache As New System.Collections.Concurrent.ConcurrentDictionary(Of String, ModSide)()

    ''' <summary>
    ''' 批量分析 mods 目录下的环境归属
    ''' </summary>
    Public Shared Sub PreScanModsOnline(jarPaths As List(Of String), isLegacy As Boolean)
        ' 收集需要在线查询环境的 Mod 哈希值与路径映射
        Dim undecidedHashes As New Dictionary(Of String, String)() ' sha1 -> jarPath
        
        For Each jarPath In jarPaths
            Dim side As ModSide = ModSide.Both
            If Cache.TryGetValue(jarPath, side) Then Continue For
            
            Dim localDetected As Boolean = False
            Try
                Using archive As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.OpenRead(jarPath)
                    ' 1.1 尝试通过 Fabric/Quilt 元数据官方环境字段进行判定
                    Dim fabricEntry As System.IO.Compression.ZipArchiveEntry = archive.GetEntry("fabric.mod.json")
                    If fabricEntry IsNot Nothing Then
                        Using reader As New System.IO.StreamReader(fabricEntry.Open())
                            Dim text As String = reader.ReadToEnd()
                            Dim match = System.Text.RegularExpressions.Regex.Match(text, """environment""\s*:\s*""([^""]+)""")
                            If match.Success Then
                                Dim env = match.Groups(1).Value.ToLower()
                                If env = "client" Then
                                    side = ModSide.ClientOnly
                                    localDetected = True
                                ElseIf env = "server" Then
                                    side = ModSide.ServerOnly
                                    localDetected = True
                                ElseIf env = "*" Then
                                    side = ModSide.Both
                                    localDetected = True
                                End If
                            End If
                        End Using
                    End If

                    ' 1.2 判定 Forge / NeoForge 官方环境字段 (mods.toml)
                    If Not localDetected Then
                        Dim modsTomlEntry As System.IO.Compression.ZipArchiveEntry = archive.GetEntry("META-INF/mods.toml")
                        If modsTomlEntry Is Nothing Then modsTomlEntry = archive.GetEntry("META-INF/neoforge.mods.toml")
                        
                        If modsTomlEntry IsNot Nothing Then
                            Using reader As New System.IO.StreamReader(modsTomlEntry.Open())
                                Dim text As String = reader.ReadToEnd()
                                If System.Text.RegularExpressions.Regex.IsMatch(text, "clientSideOnly\s*=\s*(?:true|""true"")", System.Text.RegularExpressions.RegexOptions.IgnoreCase) Then
                                    side = ModSide.ClientOnly
                                    localDetected = True
                                ElseIf System.Text.RegularExpressions.Regex.IsMatch(text, "serverSideOnly\s*=\s*(?:true|""true"")", System.Text.RegularExpressions.RegexOptions.IgnoreCase) Then
                                    side = ModSide.ServerOnly
                                    localDetected = True
                                End If
                            End Using
                        End If
                    End If
                End Using
            Catch ex As Exception
                ' 忽略读取异常
            End Try

            ' 如果本地明确判定了，直接存入 Cache
            If localDetected Then
                Cache(jarPath) = side
            Else
                ' 本地无法判定时放入未决定队列，准备后续进行批量哈希 API 查询
                Try
                    Dim sha1 As String = GetFileSha1(jarPath)
                    undecidedHashes(sha1) = jarPath
                Catch ex As Exception
                End Try
            End If
        Next

        ' 如果本地已全部分析完成，直接返回
        If undecidedHashes.Count = 0 Then Return

        ' 2. 联网查询：批量查询 Modrinth API 获取项目的环境属性
        Try
            Using client As New System.Net.Http.HttpClient()
                ' 携带 User-Agent 避免 API 请求被拦截
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL2-CustomExporter/1.0")
                client.Timeout = System.TimeSpan.FromSeconds(10)

                Dim ModrinthHashes = undecidedHashes.Select(Function(kv) kv.Key).ToList()
                
                ' 2.1 步骤 1：批量映射 Project_id
                Dim reqJson As String = "{""hashes"": [""" & String.Join(""",""", ModrinthHashes) & """], ""algorithm"": ""sha1""}"
                Dim responseTask = client.PostAsync("https://api.modrinth.com/v2/version_files", 
                    New System.Net.Http.StringContent(reqJson, System.Text.Encoding.UTF8, "application/json"))
                responseTask.Wait()
                Dim response = responseTask.Result
                
                If response.IsSuccessStatusCode Then
                    Dim contentTask = response.Content.ReadAsStringAsync()
                    contentTask.Wait()
                    Dim jsonText As String = contentTask.Result
                    
                    ' 解析哈希对应的 Project_ID，建立映射关系
                    Dim ProjectIdToHashes As New Dictionary(Of String, List(Of String))()
                    For Each hash In ModrinthHashes
                        ' 从响应体中匹配每个 hash 对应的 project_id
                        Dim projectPattern As String = $"""{hash}""[\s\S]*?""project_id""\s*:\s*""([^""]+)"""
                        Dim projMatch = System.Text.RegularExpressions.Regex.Match(jsonText, projectPattern)
                        If projMatch.Success Then
                            Dim pid As String = projMatch.Groups(1).Value
                            If Not ProjectIdToHashes.ContainsKey(pid) Then ProjectIdToHashes(pid) = New List(Of String)()
                            ProjectIdToHashes(pid).Add(hash)
                        End If
                    Next

                    If ProjectIdToHashes.Count > 0 Then
                        ' 2.2 步骤 2：批量获取这些 Project 的 client_side 和 server_side 字段
                        Dim projectIds As New List(Of String)(ProjectIdToHashes.Keys)
                        Dim idsParam As String = "[""" & String.Join(""",""", projectIds) & """]"
                        
                        Dim projectsResponseTask = client.GetAsync("https://api.modrinth.com/v2/projects?ids=" & System.Uri.EscapeDataString(idsParam))
                        projectsResponseTask.Wait()
                        Dim projectsResponse = projectsResponseTask.Result
                        
                        If projectsResponse.IsSuccessStatusCode Then
                            Dim projectsContentTask = projectsResponse.Content.ReadAsStringAsync()
                            projectsContentTask.Wait()
                            Dim projectsJsonText As String = projectsContentTask.Result

                            ' 逐个解析项目区块获取环境配置，避免引入额外库，在此使用正则分割处理
                            Dim projectBlocks = projectsJsonText.Split(New String() {"},{", "}, {"}, StringSplitOptions.None)
                            For Each block In projectBlocks
                                Dim idMatch = System.Text.RegularExpressions.Regex.Match(block, """id""\s*:\s*""([^""]+)""")
                                If idMatch.Success Then
                                    Dim pid As String = idMatch.Groups(1).Value
                                    Dim clientSide As String = System.Text.RegularExpressions.Regex.Match(block, """client_side""\s*:\s*""([^""]+)""").Groups(1).Value
                                    Dim serverSide As String = System.Text.RegularExpressions.Regex.Match(block, """server_side""\s*:\s*""([^""]+)""").Groups(1).Value

                                    ' 根据环境属性进行判定。如果两侧均为必需或可选，则默认双端保留 (Both)
                                    Dim determinedSide As ModSide = ModSide.Both
                                    If clientSide = "unsupported" Then
                                        determinedSide = ModSide.ServerOnly
                                    ElseIf serverSide = "unsupported" Then
                                        determinedSide = ModSide.ClientOnly
                                    End If

                                    ' 将判定结果写入对应下属 jar 包的缓存
                                    If ProjectIdToHashes.ContainsKey(pid) Then
                                        For Each hash In ProjectIdToHashes(pid)
                                            Dim jarPath As String = undecidedHashes(hash)
                                            Cache(jarPath) = determinedSide
                                        Next
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            End Using
        Catch ex As Exception
            ' 忽略异常
        End Try

        ' 3. 兜底处理：网络查询失败或未匹配到时，默认作为双端保留 (Both)
        For Each jarPath In undecidedHashes.Values
            If Not Cache.ContainsKey(jarPath) Then
                Cache(jarPath) = ModSide.Both
            End If
        Next
    End Sub

    ''' <summary>
    ''' 获取已经决定完毕的 Mod 环境运行侧类型
    ''' </summary>
    Public Shared Function GetModSide(jarPath As String, isLegacy As Boolean) As ModSide
        Dim side As ModSide = ModSide.Both
        If Cache.TryGetValue(jarPath, side) Then Return side
        Return ModSide.Both
    End Function

    ' 计算 SHA-1 校验和
    Private Shared Function GetFileSha1(filePath As String) As String
        Using sha1 As System.Security.Cryptography.SHA1 = System.Security.Cryptography.SHA1.Create()
            Using stream As System.IO.FileStream = System.IO.File.OpenRead(filePath)
                Dim hashBytes() As Byte = sha1.ComputeHash(stream)
                Return BitConverter.ToString(hashBytes).Replace("-", "").ToLower()
            End Using
        End Using
    End Function
End Class