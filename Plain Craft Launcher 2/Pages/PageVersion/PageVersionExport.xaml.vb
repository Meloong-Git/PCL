Imports System.IO.Compression

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

Public Class PageVersionExport
    Implements IRefreshable

    Private CurrentVersion As String = ""
    Private Sub PageVersionExport_Loaded() Handles Me.Loaded
        AniControlEnabled += 1
        If CurrentVersion <> PageVersionLeft.Version.Path Then RefreshAll() '切换到了另一个版本，重置页面
        BtnAdvancedHelp.EventData = If(VersionBranchName = "Release", "指南/整合包制作 - Public.json", "指南/整合包制作 - Snapshot.json")
        AniControlEnabled -= 1
    End Sub
    Public Sub RefreshAll() Implements IRefreshable.Refresh
        Log($"[Export] 刷新导出页面")
        HintOptiFine.Visibility = If(PageVersionLeft.Version.Version.HasOptiFine, Visibility.Visible, Visibility.Collapsed)
        CurrentVersion = PageVersionLeft.Version.Path
        TextExportName.Text = ""
        TextExportName.HintText = PageVersionLeft.Version.Name
        TextExportVersion.Text = ""
        TextExportVersion.HintText = "1.0.0"
        CheckAdvancedInclude.Checked = False
        CheckAdvancedModrinth.Checked = False
        GetExportOption(CheckOptionsBasic).Description = PageVersionLeft.Version.GetDefaultDescription()
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
            Dim TargetFolder As New DirectoryInfo(PageVersionLeft.Version.PathIndie & Folder)
            If Not TargetFolder.Exists() Then Continue For
            '查找文件夹下的对应项
            If AcceptCompressedFile Then
                For Each File In TargetFolder.EnumerateFiles("*.zip").Concat(TargetFolder.EnumerateFiles("*.rar"))
                    If SubOptionBlackList.Any(Function(b) File.Name.ContainsF(b)) Then Continue For
                    Panel.Children.Add(New MyCheckBox With {. _
                        Tag = New ExportOption With {.Title = File.Name, .DefaultChecked = True, .Rules = EscapeLikePattern($"{Folder}/{File.Name}")}})
                Next
            End If
            If AcceptFolder Then
                For Each SubFolder In TargetFolder.EnumerateDirectories().OrderByDescending(Function(f) f.LastWriteTime)
                    If SubOptionBlackList.Any(Function(b) SubFolder.Name.ContainsF(b)) Then Continue For
                    If Not SubFolder.EnumerateFileSystemInfos().Any() Then Continue For
                    Dim NewCheckBox As New MyCheckBox With {. _
                        Tag = New ExportOption With {.Title = SubFolder.Name, .DefaultChecked = True, .Rules = EscapeLikePattern($"{Folder}/{SubFolder.Name}/")}}
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
                    Folder.EnumerateFileSystemInfos().Any(Function(i) Not SubOptionBlackList.Any(Function(b) i.Name.ContainsF(b)))
            Catch '一般是由于无法访问，或是一个指向已不存在的文件夹的链接（例如使用 mklink 创造的 resource 文件夹链接）
                Return False
            End Try
        End Function
        Dim PathInfo As New DirectoryInfo(PageVersionLeft.Version.PathIndie)
        AllEntries.AddRange(PathInfo.EnumerateFiles().Select(Function(f) f.Name))
        For Each SubFolder In PathInfo.EnumerateDirectories().Where(IsValidDirectory)
            AllEntries.Add($"{SubFolder.Name}\")
            AllEntries.AddRange(SubFolder.EnumerateFiles().Select(Function(f) $"{SubFolder.Name}\{f.Name}"))
            AllEntries.AddRange(SubFolder.EnumerateDirectories().Where(IsValidDirectory).Select(Function(d) $"{SubFolder.Name}\{d.Name}\"))
        Next
        Log($"[Export] 共发现 {AllEntries.Count} 个可行的二级文件/文件夹")
        '确认选项是否应该被显示
        Dim IsVisible =
        Function(TargetOption As ExportOption) As Boolean
            '检查需要 OptiFine 或 Mod 加载器
            If TargetOption.RequireOptiFine AndAlso Not PageVersionLeft.Version.Version.HasOptiFine Then Return False
            If TargetOption.RequireModLoader AndAlso Not PageVersionLeft.Version.Modable Then Return False
            If TargetOption.RequireModLoaderOrOptiFine AndAlso Not PageVersionLeft.Version.Version.HasOptiFine AndAlso Not PageVersionLeft.Version.Modable Then Return False
            '粗略检查是否可能有符合规则的文件/文件夹
            Return StandardizeLines(If(TargetOption.Rules, TargetOption.ShowRules).Split("|"c), True).Any(
            Function(Rule As String)
                If Rule.StartsWithF("!") Then Return False '只看正向规则
                '检查前两级
                Try
                    If AllEntries.Any(Function(Entry) Entry Like Rule) Then Return True
                Catch ex As Exception
                    Log(ex, $"错误的规则：{Rule}", LogLevel.Hint)
                    Return False
                End Try
                '粗略检查所有级
                Rule = Rule.Trim("*?".ToCharArray)
                If Rule.Split({"\"c}, StringSplitOptions.RemoveEmptyEntries).Count >= 3 Then
                    If Rule.EndsWithF("\") Then
                        Return IsValidDirectory(New DirectoryInfo(PageVersionLeft.Version.PathIndie & Rule)) '文件夹有效
                    Else
                        Return File.Exists(PageVersionLeft.Version.PathIndie & Rule) '文件有效
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
            Dim ConfigPath As String = SelectSaveFile("选择文件位置", "export_config.txt", "整合包导出配置(*.txt)|*.txt", Setup.Get("CacheExportConfig"))
            If String.IsNullOrEmpty(ConfigPath) Then Return
            Setup.Set("CacheExportConfig", ConfigPath)
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
            ConfigLines.Add("# 如果你想要打包上传到 Modrinth，启用此项会生成完全符合 Modrinth 要求的整合包文件。")
            ConfigLines.Add("# 由于 Modrinth 要求，只能从 CurseForge 下载的资源将无法联网下载，会被直接放入整合包中。")
            ConfigLines.Add("# 此选项与 IncludeLauncher、IncludeLauncherCustom、DontCheckHostedAssets 冲突。")
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
            WriteFile(ConfigPath, ConfigLines.Join(vbCrLf))
            Hint("已保存配置文件：" & ConfigPath, HintType.Finish)
            OpenExplorer(ConfigPath)
        Catch ex As Exception
            Log(ex, "保存配置失败", LogLevel.Msgbox)
        End Try
    End Sub
    '读取配置文件
    Private Sub ImportConfig() Handles BtnAdvancedImport.Click
        Try
            Dim ConfigPath As String = SelectFile("整合包导出配置(*.txt)|*.txt", "选择配置文件", Setup.Get("CacheExportConfig"))
            If String.IsNullOrEmpty(ConfigPath) Then Return
            Setup.Set("CacheExportConfig", ConfigPath)
            Dim Segments As String() = ReadFile(ConfigPath).Split(Sperator)
            'ini 段
            Dim Ini As New Dictionary(Of String, String)
            For Each Line In Segments(0).Split(vbCrLf.ToCharArray())
                Line = Line.Trim
                If Line = "" OrElse Line.StartsWithF("#") OrElse Line.StartsWithF("=") Then Continue For
                Dim Index As Integer = Line.IndexOfF(":")
                If Index > 0 Then Ini(Line.Substring(0, Index)) = Line.Substring(Index + 1)
            Next
            TextExportName.Text = Ini.GetOrDefault("Name", "")
            TextExportVersion.Text = Ini.GetOrDefault("Version", "")
            CheckOptionsPcl.Checked = Ini.GetOrDefault("IncludeLauncher", True)
            CheckOptionsPclCustom.Checked = Ini.GetOrDefault("IncludeLauncherCustom", True)
            CheckAdvancedModrinth.Checked = Ini.GetOrDefault("ModrinthUploadMode", False)
            CheckAdvancedInclude.Checked = Ini.GetOrDefault("DontCheckHostedAssets", False)
            ConfigPackPath = Ini.GetOrDefault("PackPath", Nothing)
            '导出内容段
            RulesOverrides = Segments(1).Replace(vbCr, vbLf).Replace(vbLf & vbLf, vbLf).Split(vbLf).ToList
            '追加内容段
            If Segments.Length > 2 Then
                ExtraFiles = Segments(2).Replace(vbCr, vbLf).Replace(vbLf & vbLf, vbLf).Split(vbLf).ToList
            Else
                ExtraFiles = Nothing
            End If
            '结束
            Hint("已读取配置文件：" & ConfigPath, HintType.Finish)
        Catch ex As Exception
            Log(ex, "读取配置失败", LogLevel.Msgbox)
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
                Directory.CreateDirectory(GetPathFromFullPath(ConfigPackPath))
                PackPath = ConfigPackPath
                Log($"[Export] 使用配置文件中指定的导出路径：{ConfigPackPath}")
            Catch ex As Exception
                Log(ex, $"无法使用配置文件中指定的导出路径（{ConfigPackPath}）", LogLevel.Debug)
                If MyMsgBox($"指定的路径：{ConfigPackPath}{vbCrLf}{vbCrLf}{GetExceptionDetail(ex)}", "无法使用配置文件中指定的导出路径", "确定", "取消") = 2 Then Return
            End Try
        End If
        If PackPath Is Nothing Then
            Dim Extensions As New List(Of String)
            If Not CheckAdvancedModrinth.Checked Then Extensions.Add("压缩文件(*.zip)|*.zip")
            If Not CheckOptionsPcl.Checked Then Extensions.Add("Modrinth 整合包文件(*.mrpack)|*.mrpack")
            PackPath = SelectSaveFile("选择导出位置",
                PackName & If(String.IsNullOrEmpty(TextExportVersion.Text), "", " " & TextExportVersion.Text), Extensions.Join("|"))
            Log($"[Export] 手动指定的导出路径：{PackPath}")
        End If
        If String.IsNullOrEmpty(PackPath) Then Return

        '缓存所需参数
        Dim CacheFolder = RequestTaskTempFolder()
        Dim OverridesFolder = CacheFolder & "modpack\overrides\"
        Dim McVersion = PageVersionLeft.Version
        Dim PathIndie As String = McVersion.PathIndie
        Dim CheckHostedAssets As Boolean = Not CheckAdvancedInclude.Checked
        Dim ModrinthUploadMode As Boolean = CheckAdvancedModrinth.Checked
        Dim IncludePCL As Boolean = CheckOptionsPcl.Checked
        Dim IncludePCLCustom As Boolean = IncludePCL AndAlso CheckOptionsPclCustom.Checked
        Dim AllRules = StandardizeLines(GetAllRules(), True).ToList()
        Dim AllExtraFiles = StandardizeLines(GetExtraFileLines(), False).ToList()
        Log($"[Export] 准备导出整合包，共有 {AllRules.Count} 条规则，{AllExtraFiles.Count} 条追加内容行")

        '构造步骤加载器
        Dim Loaders As New List(Of LoaderBase)

#Region "准备 PCL 文件"

#If Not BETA Then
        If IncludePCL Then
            Loaders.Add(New LoaderTask(Of Integer, Integer)("下载 PCL 正式版",
            Sub(Loader As LoaderTask(Of Integer, Integer))
                DownloadLatestPCL(Loader)
                CopyFile(PathTemp & "Latest.exe", CacheFolder & "Plain Craft Launcher.exe")
            End Sub) With {.ProgressWeight = 0.5, .Block = False})
        End If
#End If

#End Region

#Region "复制文件"

        Loaders.Add(New LoaderTask(Of Integer, List(Of McMod))("复制导出内容",
        Sub(Loader As LoaderTask(Of Integer, List(Of McMod)))
            Loader.Output = New List(Of McMod)
            '复制版本文件
            Dim Progress As Integer = 0
            Dim SearchFolder As Action(Of DirectoryInfo)
            SearchFolder =
            Sub(Folder As DirectoryInfo)
                '文件夹：进一步搜索
                For Each SubFolder In Folder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                    '跳过部分又没用文件又多的文件夹，加快搜索
                    If Folder.FullName = PathIndie AndAlso {"assets", "versions", "libraries"}.Contains(SubFolder.Name) Then Continue For
                    If {"structureCacheV1", ".fabric", ".git", "avatar-cache", "cosmetic-cache"}.Contains(SubFolder.Name) Then Continue For
                    SearchFolder(SubFolder)
                Next
                '文件：检查规则并复制
                For Each Entry In Folder.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    Dim RelativePath As String = Entry.FullName.AfterFirst(PathIndie)
                    '检查规则
                    Dim ShouldKeep As Boolean = False
                    For Each Rule In AllRules
                        Dim Revert = Rule.StartsWith("!")
                        If RelativePath Like Rule.TrimStart("!") Then ShouldKeep = Not Revert
                    Next
                    If Not ShouldKeep Then Continue For
                    Dim TargetPath As String = OverridesFolder & RelativePath
                    CopyFile(Entry.FullName, TargetPath)
                    '若为压缩包，考虑联网获取路径
                    If CheckHostedAssets AndAlso
                       {".zip", ".rar", ".jar", ".disabled", ".old"}.Contains(Entry.Extension.ToLower) AndAlso
                       {"mods", "packs", "openloader", "resource"}.Any(Function(s) RelativePath.Contains(s)) Then
                        Dim ModFile As New McMod(TargetPath)
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
            SearchFolder(New DirectoryInfo(PathIndie))
            Log($"[Export] 复制 overrides 文件完成，有 {Loader.Output.Count} 个文件需要联网检查")
            Loader.Progress = 0.95
            '复制追加内容到根目录
            Dim BaseFolder As String = If(IncludePCL, CacheFolder, CacheFolder & "modpack\")
            For Each Line In AllExtraFiles
                If Line.EndsWithF("\") OrElse Line.EndsWithF("/") Then
                    If Directory.Exists(Line) Then
                        CopyDirectory(Line, BaseFolder & GetFolderNameFromPath(Line) & "\")
                    Else
                        Hint($"未找到配置文件中指定的文件夹：{Line}", HintType.Critical)
                    End If
                Else
                    If File.Exists(Line) Then
                        CopyFile(Line, BaseFolder & GetFileNameFromPath(Line))
                    Else
                        Hint($"未找到配置文件中指定的单个文件：{Line}", HintType.Critical)
                    End If
                End If
            Next
            Loader.Progress = 0.97
            '复制 PCL 版本设置
            CopyDirectory(McVersion.Path & "PCL\", OverridesFolder & "PCL\")
#If BETA Then
            '复制 PCL 本体
            If IncludePCL Then CopyFile(PathWithName, CacheFolder & "Plain Craft Launcher.exe")
#End If
            '复制 PCL 个性化内容
            If IncludePCLCustom Then
                If Directory.Exists(Path & "PCL\Pictures\") Then CopyDirectory(Path & "PCL\Pictures\", CacheFolder & "PCL\Pictures\")
                If Directory.Exists(Path & "PCL\Musics\") Then CopyDirectory(Path & "PCL\Musics\", CacheFolder & "PCL\Musics\")
                If Directory.Exists(Path & "PCL\Help\") Then CopyDirectory(Path & "PCL\Help\", CacheFolder & "PCL\Help\")
                If File.Exists(Path & "PCL\Custom.xaml") Then CopyFile(Path & "PCL\Custom.xaml", CacheFolder & "PCL\Custom.xaml")
                If File.Exists(Path & "PCL\Setup.ini") Then CopyFile(Path & "PCL\Setup.ini", CacheFolder & "PCL\Setup.ini")
                If File.Exists(Path & "PCL\hints.txt") Then CopyFile(Path & "PCL\hints.txt", CacheFolder & "PCL\hints.txt")
                If File.Exists(Path & "PCL\Logo.png") Then CopyFile(Path & "PCL\Logo.png", CacheFolder & "PCL\Logo.png")
            End If
        End Sub) With {.ProgressWeight = 5})

#End Region

#Region "联网检查"

        Loaders.Add(New LoaderTask(Of List(Of McMod), Dictionary(Of McMod, List(Of String)))("联网获取文件信息",
        Sub(Loader As LoaderTask(Of List(Of McMod), Dictionary(Of McMod, List(Of String))))
            Loader.Output = New Dictionary(Of McMod, List(Of String))
            If Not CheckHostedAssets Then Log($"[Export] 要求跳过联网获取步骤") : Return
            If Not Loader.Input.Any Then Log($"[Export] 没有需要联网检查的文件，跳过联网获取步骤") : Return

            '分平台获取下载地址
            Dim EndedThreadCount As Integer = 0, FailedExceptions As New List(Of Exception)

            '从 Modrinth 获取信息
            RunInNewThread(
            Sub()
                Try
                    Dim ModrinthHashes = Loader.Input.Select(Function(m) m.ModrinthHash)
                    Dim ModrinthRaw = CType(GetJson(DlModRequest("https://api.modrinth.com/v2/version_files", "POST",
                        $"{{""hashes"": [""{ModrinthHashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")), JObject)
                    For Each ModFile In Loader.Input
                        '查找对应的文件
                        If Not ModrinthRaw.ContainsKey(ModFile.ModrinthHash) Then Continue For
                        If ModrinthRaw(ModFile.ModrinthHash)?("files")?(0)("hashes")?("sha1") <> ModFile.ModrinthHash Then Continue For
                        '写入下载地址
                        Loader.Output.AddToList(ModFile, ModrinthRaw(ModFile.ModrinthHash)("files")(0)("url"))
                    Next
                    Log($"[Export] 从 Modrinth 获取到 {ModrinthRaw.Count} 个本地资源项的对应信息")
                Catch ex As Exception
                    Log(ex, "从 Modrinth 获取本地 Mod 信息失败")
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
                    If ModrinthUploadMode Then Return 'Modrinth 上传模式下，不能从 CurseForge 获取信息
                    Dim CurseForgeHashes = Loader.Input.Select(Function(m) m.CurseForgeHash)
                    Dim CurseForgeRaw = CType(CType(GetJson(DlModRequest("https://api.curseforge.com/v1/fingerprints/432/", "POST",
                        $"{{""fingerprints"": [{CurseForgeHashes.Join(",")}]}}", "application/json")), JObject)("data")("exactMatches"), JContainer)
                    For Each ResultJson As JObject In CurseForgeRaw
                        If Not ResultJson.ContainsKey("file") Then Continue For
                        Dim File As JObject = ResultJson("file")
                        If String.IsNullOrEmpty(File("downloadUrl")) Then Continue For
                        '查找对应的文件
                        Dim ModFile As McMod = Loader.Input.FirstOrDefault(Function(m) m.CurseForgeHash = File("fileFingerprint").ToObject(Of UInteger))
                        If ModFile Is Nothing Then Continue For
                        '写入下载地址
                        For Each Address In CompFile.HandleCurseForgeDownloadUrls(File("downloadUrl").ToString)
                            Loader.Output.AddToList(ModFile, Address)
                        Next
                    Next
                    Log($"[Export] 从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地资源项的对应信息")
                Catch ex As Exception
                    Log(ex, "从 CurseForge 获取本地 Mod 信息失败")
                    FailedExceptions.Add(ex)
                Finally
                    EndedThreadCount += 1
                    Loader.Progress += 0.45
                End Try
            End Sub, "CurseForge - " & LoaderName)

            '等待线程结束
            Do Until EndedThreadCount = 2
                If Loader.IsAborted Then Return
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

        Loaders.Add(New LoaderTask(Of Dictionary(Of McMod, List(Of String)), Integer)("生成压缩包",
        Sub(Loader As LoaderTask(Of Dictionary(Of McMod, List(Of String)), Integer))
            '整理文件列表
            Dim Files As New JArray
            For Each Pair In Loader.Input
                Dim ModFile As McMod = Pair.Key
                Files.Add(New JObject From {
                    {"path", ModFile.Path.AfterFirst(OverridesFolder).Replace("\", "/")},
                    {"hashes", New JObject From {{"sha1", ModFile.ModrinthHash}, {"sha512", GetFileSHA512(ModFile.Path)}}},
                    {"downloads", New JArray(Pair.Value.OrderByDescending(Function(u) u.Contains("modrinth.com")))},
                    {"fileSize", New FileInfo(ModFile.Path).Length}
                })
                File.Delete(ModFile.Path)
            Next
            Loader.Progress = 0.2
            '导出最终 JSON 文件
            Dim Dependencies As New JObject From {{"minecraft", McVersion.Version.McName}}
            If McVersion.Version.HasForge Then Dependencies.Add("forge", McVersion.Version.ForgeVersion)
            If McVersion.Version.HasFabric Then Dependencies.Add("fabric-loader", McVersion.Version.FabricVersion)
            If McVersion.Version.HasNeoForge Then Dependencies.Add("neoforge", McVersion.Version.NeoForgeVersion)
            Dim ResultJson As New JObject From {
                {"game", "minecraft"},
                {"formatVersion", 1},
                {"versionId", PackVersion},
                {"name", PackName},
                {"summary", McVersion.Info},
                {"files", Files},
                {"dependencies", Dependencies}
            }
            File.WriteAllText(CacheFolder & "modpack\modrinth.index.json", ResultJson.ToString(Newtonsoft.Json.Formatting.Indented))
            '打包
            Directory.CreateDirectory(GetPathFromFullPath(PackPath))
            If File.Exists(PackPath) Then File.Delete(PackPath)
            If IncludePCL Then
                '首次压缩整合包
                ZipFile.CreateFromDirectory(CacheFolder & "modpack\", CacheFolder & "modpack.mrpack")
                Loader.Progress = 0.5
                Directory.Delete(CacheFolder & "modpack\", True)
                Loader.Progress = 0.6
                '二次压缩整合包
                ZipFile.CreateFromDirectory(CacheFolder, PackPath)
                Loader.Progress = 0.9
            Else
                '直接压缩整合包
                ZipFile.CreateFromDirectory(CacheFolder & "modpack\", PackPath)
                Loader.Progress = 0.8
            End If
            Directory.Delete(CacheFolder, True)
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

    '勾选 Modrinth 上传模式时，禁止打包 PCL
    Private Sub CheckAdvancedModrinth_Change(sender As Object, user As Boolean) Handles CheckAdvancedModrinth.Change
        If CheckAdvancedModrinth.Checked Then CheckOptionsPcl.Checked = False
        CheckOptionsPcl.IsEnabled = Not CheckAdvancedModrinth.Checked
    End Sub

    '勾选打包资源文件时，禁止开启 Modrinth 上传模式
    Private Sub CheckAdvancedInclude_Change(sender As Object, user As Boolean) Handles CheckAdvancedInclude.Change
        If CheckAdvancedInclude.Checked Then CheckAdvancedModrinth.Checked = False
        CheckAdvancedModrinth.IsEnabled = Not CheckAdvancedInclude.Checked
    End Sub

End Class
