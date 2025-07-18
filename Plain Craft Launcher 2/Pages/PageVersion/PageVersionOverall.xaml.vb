Public Class PageVersionOverall

    Private IsLoad As Boolean = False
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '更新设置
        ItemDisplayLogoCustom.Tag = "PCL\Logo.png"
        Reload()

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True
        PanDisplay.TriggerForceResize()

    End Sub

    Public ItemVersion As MyListItem
    ''' <summary>
    ''' 确保当前页面上的信息已正确显示。
    ''' </summary>
    Private Sub Reload()
        AniControlEnabled += 1

        '刷新设置项目
        ComboDisplayType.SelectedIndex = ReadIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "DisplayType", McVersionCardType.Auto)
        BtnDisplayStar.Text = If(PageVersionLeft.Version.IsStar, "从收藏夹中移除", "加入收藏夹")
        BtnFolderMods.Visibility = If(PageVersionLeft.Version.Modable, Visibility.Visible, Visibility.Collapsed)
        '刷新版本显示
        PanDisplayItem.Children.Clear()
        ItemVersion = PageSelectRight.McVersionListItem(PageVersionLeft.Version)
        ItemVersion.IsHitTestVisible = False
        PanDisplayItem.Children.Add(ItemVersion)
        FrmMain.PageNameRefresh()
        '刷新版本图标
        ComboDisplayLogo.SelectedIndex = 0
        Dim Logo As String = ReadIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "Logo", "")
        Dim LogoCustom As Boolean = ReadIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "LogoCustom", "False")
        If LogoCustom Then
            For Each Selection As MyComboBoxItem In ComboDisplayLogo.Items
                If Selection.Tag = Logo OrElse (Selection.Tag = "PCL\Logo.png" AndAlso Logo.EndsWith("PCL\Logo.png")) Then
                    ComboDisplayLogo.SelectedItem = Selection
                    Exit For
                End If
            Next
        End If

        AniControlEnabled -= 1
    End Sub

#Region "卡片：个性化"

    '版本分类
    Private Sub ComboDisplayType_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayType.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        If ComboDisplayType.SelectedIndex <> 1 Then
            '改为不隐藏
            Try
                '若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                WriteIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "DisplayType", ComboDisplayType.SelectedIndex)
                PageVersionLeft.Version.DisplayType = ReadIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "DisplayType", McVersionCardType.Auto)
                FrmVersionLeft.RefreshModDisabled()

                WriteIni(PathMcFolder & "PCL.ini", "VersionCache", "") '要求刷新缓存
                LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Log(ex, "修改版本分类失败（" & PageVersionLeft.Version.Name & "）", LogLevel.Feedback)
            End Try
            Reload() '更新 “打开 Mod 文件夹” 按钮
        Else
            '改为隐藏
            Try
                If Not Setup.Get("HintHide") Then
                    If MyMsgBox("确认要从版本列表中隐藏该版本吗？隐藏该版本后，它将不再出现于 PCL 显示的版本列表中。" & vbCrLf & "此后，在版本列表页面按下 F11 才可以查看被隐藏的版本。", "隐藏版本提示",, "取消") <> 1 Then
                        ComboDisplayType.SelectedIndex = 0
                        Return
                    End If
                    Setup.Set("HintHide", True)
                End If
                WriteIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "DisplayType", McVersionCardType.Hidden)
                WriteIni(PathMcFolder & "PCL.ini", "VersionCache", "") '要求刷新缓存
                LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Log(ex, "隐藏版本 " & PageVersionLeft.Version.Name & " 失败", LogLevel.Feedback)
            End Try
        End If
    End Sub

    '更改描述
    Private Sub BtnDisplayDesc_Click(sender As Object, e As EventArgs) Handles BtnDisplayDesc.Click
        Try
            Dim OldInfo As String = ReadIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "CustomInfo")
            Dim NewInfo As String = MyMsgBoxInput("更改描述", "修改版本的描述文本，留空则使用 PCL 的默认描述。", OldInfo, New ObjectModel.Collection(Of Validate), "默认描述")
            If NewInfo IsNot Nothing AndAlso OldInfo <> NewInfo Then WriteIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "CustomInfo", NewInfo)
            PageVersionLeft.Version = New McVersion(PageVersionLeft.Version.Name).Load()
            Reload()
            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "版本 " & PageVersionLeft.Version.Name & " 描述更改失败", LogLevel.Msgbox)
        End Try
    End Sub

    '重命名版本
    Private Sub BtnDisplayRename_Click(sender As Object, e As EventArgs) Handles BtnDisplayRename.Click
        Try
            '确认输入的新名称
            Dim OldName As String = PageVersionLeft.Version.Name
            Dim OldPath As String = PageVersionLeft.Version.Path
            '修改此部分的同时修改快速安装的版本名检测*
            Dim NewName As String = MyMsgBoxInput("重命名版本", "", OldName, New ObjectModel.Collection(Of Validate) From {New ValidateFolderName(PathMcFolder & "versions", IgnoreCase:=False)})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            Dim NewPath As String = PathMcFolder & "versions\" & NewName & "\"
            '获取临时中间名，以防止仅修改大小写的重命名失败
            Dim TempName As String = NewName & "_temp"
            Dim TempPath As String = PathMcFolder & "versions\" & TempName & "\"
            Dim IsCaseChangedOnly As Boolean = NewName.ToLower = OldName.ToLower
            '重新加载版本 Json 信息，避免 HMCL 项被合并
            Dim JsonObject As JObject
            Try
                JsonObject = GetJson(ReadFile(PageVersionLeft.Version.Path & PageVersionLeft.Version.Name & ".json"))
            Catch ex As Exception
                Log(ex, "重命名读取 json 时失败")
                JsonObject = PageVersionLeft.Version.JsonObject
            End Try
            '重命名主文件夹
            My.Computer.FileSystem.RenameDirectory(OldPath, TempName)
            My.Computer.FileSystem.RenameDirectory(TempPath, NewName)
            '清理 ini 缓存
            IniClearCache(PageVersionLeft.Version.PathIndie & "options.txt")
            IniClearCache(PageVersionLeft.Version.Path & "PCL\Setup.ini")
            '遍历重命名所有文件与文件夹
            For Each Entry As DirectoryInfo In New DirectoryInfo(NewPath).EnumerateDirectories
                If Not Entry.Name.Contains(OldName) Then Continue For
                If IsCaseChangedOnly Then
                    My.Computer.FileSystem.RenameDirectory(Entry.FullName, Entry.Name & "_temp")
                    My.Computer.FileSystem.RenameDirectory(Entry.FullName & "_temp", Entry.Name.Replace(OldName, NewName))
                Else
                    DeleteDirectory(NewPath & Entry.Name.Replace(OldName, NewName))
                    My.Computer.FileSystem.RenameDirectory(Entry.FullName, Entry.Name.Replace(OldName, NewName))
                End If
            Next
            For Each Entry As FileInfo In New DirectoryInfo(NewPath).EnumerateFiles
                If Not Entry.Name.Contains(OldName) Then Continue For
                If IsCaseChangedOnly Then
                    My.Computer.FileSystem.RenameFile(Entry.FullName, Entry.Name & "_temp")
                    My.Computer.FileSystem.RenameFile(Entry.FullName & "_temp", Entry.Name.Replace(OldName, NewName))
                Else
                    If File.Exists(NewPath & Entry.Name.Replace(OldName, NewName)) Then File.Delete(NewPath & Entry.Name.Replace(OldName, NewName))
                    My.Computer.FileSystem.RenameFile(Entry.FullName, Entry.Name.Replace(OldName, NewName))
                End If
            Next
            '替换版本设置文件中的路径
            If File.Exists(NewPath & "PCL\Setup.ini") Then
                WriteFile(NewPath & "PCL\Setup.ini", ReadFile(NewPath & "PCL\Setup.ini").Replace(OldPath, NewPath))
            End If
            '更改已选中的版本
            If ReadIni(PathMcFolder & "PCL.ini", "Version") = OldName Then
                WriteIni(PathMcFolder & "PCL.ini", "Version", NewName)
            End If
            '更改版本 Json
            If File.Exists(NewPath & NewName & ".json") Then
                Try
                    JsonObject("id") = NewName
                    WriteFile(NewPath & NewName & ".json", JsonObject.ToString)
                Catch ex As Exception
                    Log(ex, "重命名版本 json 失败")
                End Try
            End If
            '刷新与提示
            Hint("重命名成功！", HintType.Finish)
            PageVersionLeft.Version = New McVersion(NewName).Load()
            If Not IsNothing(McVersionCurrent) AndAlso McVersionCurrent.Equals(PageVersionLeft.Version) Then WriteIni(PathMcFolder & "PCL.ini", "Version", NewName)
            Reload()
            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "重命名版本失败", LogLevel.Msgbox)
        End Try
    End Sub

    '版本图标
    Private Sub ComboDisplayLogo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayLogo.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        '选择 自定义 时修改图片
        Try
            If ComboDisplayLogo.SelectedItem Is ItemDisplayLogoCustom Then
                Dim FileName As String = SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.jpeg)|*.png;*.jpg;*.gif;*.jpeg", "选择图片")
                If FileName = "" Then
                    Reload() '还原选项
                    Return
                End If
                CopyFile(FileName, PageVersionLeft.Version.Path & "PCL\Logo.png")
            Else
                File.Delete(PageVersionLeft.Version.Path & "PCL\Logo.png")
            End If
        Catch ex As Exception
            Log(ex, "更改自定义版本图标失败（" & PageVersionLeft.Version.Name & "）", LogLevel.Feedback)
        End Try
        '进行更改
        Try
            Dim NewLogo As String = ComboDisplayLogo.SelectedItem.Tag
            WriteIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "Logo", NewLogo)
            WriteIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "LogoCustom", Not NewLogo = "")
            '刷新显示
            WriteIni(PathMcFolder & "PCL.ini", "VersionCache", "") '要求刷新缓存
            PageVersionLeft.Version = New McVersion(PageVersionLeft.Version.Name).Load()
            Reload()
            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "更改版本图标失败（" & PageVersionLeft.Version.Name & "）", LogLevel.Feedback)
        End Try
    End Sub

    '收藏夹
    Private Sub BtnDisplayStar_Click(sender As Object, e As EventArgs) Handles BtnDisplayStar.Click
        Try
            WriteIni(PageVersionLeft.Version.Path & "PCL\Setup.ini", "IsStar", Not PageVersionLeft.Version.IsStar)
            PageVersionLeft.Version = New McVersion(PageVersionLeft.Version.Name).Load()
            Reload()
            McVersionListForceRefresh = True
            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "版本 " & PageVersionLeft.Version.Name & " 收藏状态更改失败", LogLevel.Msgbox)
        End Try
    End Sub

#End Region

#Region "卡片：快捷方式"

    '版本文件夹
    Private Sub BtnFolderVersion_Click() Handles BtnFolderVersion.Click
        OpenVersionFolder(PageVersionLeft.Version)
    End Sub
    Public Shared Sub OpenVersionFolder(Version As McVersion)
        OpenExplorer(Version.Path)
    End Sub

    '存档文件夹
    Private Sub BtnFolderSaves_Click() Handles BtnFolderSaves.Click
        Dim FolderPath As String = PageVersionLeft.Version.PathIndie & "saves\"
        Directory.CreateDirectory(FolderPath)
        OpenExplorer(FolderPath)
    End Sub

    'Mod 文件夹
    Private Sub BtnFolderMods_Click() Handles BtnFolderMods.Click
        Dim FolderPath As String = PageVersionLeft.Version.PathIndie & "mods\"
        Directory.CreateDirectory(FolderPath)
        OpenExplorer(FolderPath)
    End Sub

#End Region

#Region "卡片：管理"

    '导出启动脚本
    Private Sub BtnManageScript_Click() Handles BtnManageScript.Click
        Try
            '弹窗要求指定脚本的保存位置
            Dim SavePath As String = SelectSaveFile("选择脚本保存位置", "启动 " & PageVersionLeft.Version.Name & ".bat", "批处理文件(*.bat)|*.bat")
            If SavePath = "" Then Return
            '检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            If McLaunchLoader.State = LoadState.Loading Then
                Hint("请在当前启动任务结束后再试！", HintType.Critical)
                Return
            End If
            '生成脚本
            If McLaunchStart(New McLaunchOptions With {.SaveBatch = SavePath, .Version = PageVersionLeft.Version}) Then
                If Setup.Get("LoginType") = McLoginType.Legacy Then
                    Hint("正在导出启动脚本……")
                Else
                    Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）")
                End If
            End If
        Catch ex As Exception
            Log(ex, "导出启动脚本失败（" & PageVersionLeft.Version.Name & "）", LogLevel.Msgbox)
        End Try
    End Sub

    '补全文件
    Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
        Try
            '忽略文件检查提示
            If ShouldIgnoreFileCheck(PageVersionLeft.Version) Then
                Hint("请先关闭 [版本设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！", HintType.Info)
                Return
            End If
            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar
                If OngoingLoader.Name <> PageVersionLeft.Version.Name & " 文件补全" Then Continue For
                Hint("正在处理中，请稍候！", HintType.Critical)
                Return
            Next
            '启动
            Dim Loader As New LoaderCombo(Of String)(PageVersionLeft.Version.Name & " 文件补全", DlClientFix(PageVersionLeft.Version, True, AssetsIndexExistsBehaviour.AlwaysDownload))
            Loader.OnStateChanged =
            Sub()
                Select Case Loader.State
                    Case LoadState.Finished
                        Hint(Loader.Name & "成功！", HintType.Finish)
                    Case LoadState.Failed
                        Hint(Loader.Name & "失败：" & GetExceptionSummary(Loader.Error), HintType.Critical)
                    Case LoadState.Aborted
                        Hint(Loader.Name & "已取消！", HintType.Info)
                End Select
            End Sub
            Loader.Start(PageVersionLeft.Version.Name)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Log(ex, "尝试补全文件失败（" & PageVersionLeft.Version.Name & "）", LogLevel.Msgbox)
        End Try
    End Sub

    '删除版本
    '修改此代码时，同时修改 PageSelectRight 中的代码
    Private Sub BtnManageDelete_Click(sender As Object, e As EventArgs) Handles BtnManageDelete.Click
        Try
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = PageVersionLeft.Version.State <> McVersionState.Error AndAlso PageVersionLeft.Version.PathIndie <> PathMcFolder
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除版本 {PageVersionLeft.Version.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "由于该版本开启了版本隔离，删除版本时该版本对应的存档、资源包、Mod 等文件也将被一并删除！", ""),
                        "版本删除确认", , "取消",, IsHintIndie OrElse IsShiftPressed)
                Case 1
                    IniClearCache(PageVersionLeft.Version.PathIndie & "options.txt")
                    IniClearCache(PageVersionLeft.Version.Path & "PCL\Setup.ini")
                    If IsShiftPressed Then
                        DeleteDirectory(PageVersionLeft.Version.Path)
                        Hint("版本 " & PageVersionLeft.Version.Name & " 已永久删除！", HintType.Finish)
                    Else
                        FileIO.FileSystem.DeleteDirectory(PageVersionLeft.Version.Path, FileIO.UIOption.AllDialogs, FileIO.RecycleOption.SendToRecycleBin)
                        Hint("版本 " & PageVersionLeft.Version.Name & " 已删除到回收站！", HintType.Finish)
                    End If
                Case 2
                    Return
            End Select
            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            FrmMain.PageBack()
        Catch ex As OperationCanceledException
            Log(ex, "删除版本 " & PageVersionLeft.Version.Name & " 被主动取消")
        Catch ex As Exception
            Log(ex, "删除版本 " & PageVersionLeft.Version.Name & " 失败", LogLevel.Msgbox)
        End Try
    End Sub

#End Region

End Class
