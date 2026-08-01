Public Class PageInstanceOverall

    Private IsLoad As Boolean = False
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '更新设置
        Reload()

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True
        PanDisplay.TriggerForceResize()

    End Sub

    Public InstanceHeaderItem As MyListItem
    ''' <summary>
    ''' 确保当前页面上的信息已正确显示。
    ''' </summary>
    Private Sub Reload()
        AniControlEnabled += 1

        '刷新设置项目
        ComboDisplayType.SelectedIndex = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
        BtnDisplayStar.Text = If(PageInstanceLeft.Instance.IsStar, GetLang("LangPageVersionOverallCancelFavorite"), GetLang("LangPageVersionOverallFavorite"))
        BtnFolderMods.Visibility = If(PageInstanceLeft.Instance.Modable, Visibility.Visible, Visibility.Collapsed)
        '刷新版本显示
        PanDisplayItem.Children.Clear()
        InstanceHeaderItem = PageInstanceLeft.Instance.ToListItem().Init()
        InstanceHeaderItem.IsHitTestVisible = False
        PanDisplayItem.Children.Add(InstanceHeaderItem)
        FrmMain.PageNameRefresh()
        '刷新版本图标
        ComboDisplayLogo.SelectedIndex = 0
        Dim Logo As String = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "Logo", "")
        Dim LogoCustom As Boolean = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "LogoCustom", "False")
        If LogoCustom Then
            For Each Selection As MyComboBoxItem In ComboDisplayLogo.Items
                If Selection.Tag = Logo OrElse (Selection.Tag = "PCL\Logo.png" AndAlso Logo.EndsWithF("PCL\Logo.png")) Then
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
                WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", ComboDisplayType.SelectedIndex)
                PageInstanceLeft.Instance.DisplayType = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
                FrmInstanceLeft.RefreshModDisabled()

                WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Logger.Error(ex, $"修改版本分类失败（{PageInstanceLeft.Instance.Name}）")
            End Try
            Reload() '更新 “打开 Mod 文件夹” 按钮
        Else
            '改为隐藏
            Try
                If Not Settings.Get(Of Boolean)("HintHide") Then
                    If MyMsgBox(GetLang("LangPageVersionOverallDialogHideInstanceContent"), GetLang("LangPageVersionOverallDialogHideInstanceTitle"),, GetLang("LangDialogBtnCancel")) <> 1 Then
                        ComboDisplayType.SelectedIndex = 0
                        Return
                    End If
                    Settings.Set("HintHide", True)
                End If
                WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Hidden)
                WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Logger.Error(ex, $"隐藏版本 {PageInstanceLeft.Instance.Name} 失败")
            End Try
        End If
    End Sub

    '更改描述
    Private Sub BtnDisplayDesc_Click(sender As Object, e As EventArgs) Handles BtnDisplayDesc.Click
        Try
            Dim OldInfo As String = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "CustomInfo")
            Dim NewInfo As String = MyMsgBoxInput(GetLang("LangPageVersionOverallDialogEditDescTitle"), GetLang("LangPageVersionOverallDialogEditDescContent"), OldInfo, New ObjectModel.Collection(Of Validate), GetLang("LangPageVersionOverallDialogEditDescHint"))
            If NewInfo IsNot Nothing AndAlso OldInfo <> NewInfo Then WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "CustomInfo", NewInfo)
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, $"版本 {PageInstanceLeft.Instance.Name} 描述更改失败", LogBehavior.Alert)
        End Try
    End Sub

    '重命名版本
    Private Sub BtnDisplayRename_Click(sender As Object, e As EventArgs) Handles BtnDisplayRename.Click
        Try
            '确认输入的新名称
            Dim OldName As String = PageInstanceLeft.Instance.Name
            Dim OldPath As String = PageInstanceLeft.Instance.PathVersion
            '修改此部分的同时修改快速安装的版本名检测*
            Dim NewName As String = MyMsgBoxInput(GetLang("LangPageVersionOverallDialogEditNameTitle"), "", OldName, New ObjectModel.Collection(Of Validate) From {
                New ValidateFolderName(McFolderSelected & "versions", IgnoreCase:=False)})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            Dim NewPath As String = McFolderSelected & "versions\" & NewName & "\"
            '重新读取版本 JSON 信息，避免 JsonObject 中已被合并的项被重新存储
            Dim JsonObject As JObject
            Dim OldJsonPath As String = PageInstanceLeft.Instance.GetJsonPath()
            Try
                JsonObject = FileUtils.ReadAsJson(OldJsonPath)
            Catch ex As Exception
                Logger.Warn(ex, "在重命名读取 JSON 时失败")
                JsonObject = PageInstanceLeft.Instance.JsonObject
            End Try
            '重命名主文件夹
            DirectoryUtils.Move(OldPath, NewPath)
            '清理 ini 缓存
            PageInstanceLeft.Instance.ResetSettingsCache()
            '重命名 jar 文件与 natives 文件夹
            '不能进行遍历重命名，否则在版本名很短的时候容易误伤其他文件（#6443）
            If DirectoryUtils.Exists($"{NewPath}{OldName}-natives") Then DirectoryUtils.Move($"{NewPath}{OldName}-natives", $"{NewPath}{NewName}-natives")
            If FileUtils.Exists($"{NewPath}{OldName}.jar") Then FileUtils.Move($"{NewPath}{OldName}.jar", $"{NewPath}{NewName}.jar")
            '替换版本设置文件中的路径
            If FileUtils.Exists(NewPath & "PCL\Setup.ini") Then
                FileUtils.Write(NewPath & "PCL\Setup.ini", FileUtils.ReadAsString(NewPath & "PCL\Setup.ini").Replace(OldPath, NewPath))
            End If
            '更改已选中的版本
            If ReadIni(McFolderSelected & "PCL.ini", "Version") = OldName Then
                WriteIni(McFolderSelected & "PCL.ini", "Version", NewName)
            End If
            '更新版本 Json
            Try
                JsonObject("id") = NewName
                FileUtils.Delete(NewPath & PathUtils.GetLastPart(OldJsonPath))
                FileUtils.Write(NewPath & NewName & ".json", JsonObject.ToString)
            Catch ex As Exception
                Logger.Warn(ex, "重命名版本 json 失败")
            End Try
            '刷新与提示
            Hint(GetLang("LangPageVersionOverallHintEditNameSuccess"), HintType.Green)
            PageInstanceLeft.Instance = New McInstance(NewName).Load()
            If Not IsNothing(McInstanceSelected) AndAlso McInstanceSelected.Equals(PageInstanceLeft.Instance) Then WriteIni(McFolderSelected & "PCL.ini", "Version", NewName)
            Reload()
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, GetLang("LangPageVersionOverallEditNameFail"), LogBehavior.Alert)
        End Try
    End Sub

    '版本图标
    Private Sub ComboDisplayLogo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayLogo.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        '选择 自定义 时修改图片
        Try
            If ComboDisplayLogo.SelectedItem Is ItemDisplayLogoCustom Then
                Dim FileName As String = Dialogs.SelectFile(GetLang("LangDialogSelectImage"), False, filter:={({"png", "jpeg", "jpg", "gif", "webp"}, GetLang("LangDialogImageFileFilter"))}).FirstOrDefault()
                If String.IsNullOrEmpty(FileName) Then
                    Reload() '还原选项
                    Return
                End If
                FileUtils.Copy(FileName, PageInstanceLeft.Instance.PathVersion & "PCL\Logo.png")
            Else
                FileUtils.Delete(PageInstanceLeft.Instance.PathVersion & "PCL\Logo.png")
            End If
        Catch ex As Exception
            Logger.Error(ex, $"更改自定义版本图标失败（{PageInstanceLeft.Instance.Name}）")
        End Try
        '进行更改
        Try
            Dim NewLogo As String = ComboDisplayLogo.SelectedItem.Tag
            WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "Logo", NewLogo)
            WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "LogoCustom", Not NewLogo = "")
            '刷新显示
            WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "") '要求刷新缓存
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, $"更改版本图标失败（{PageInstanceLeft.Instance.Name}）")
        End Try
    End Sub

    '收藏夹
    Private Sub BtnDisplayStar_Click(sender As Object, e As EventArgs) Handles BtnDisplayStar.Click
        Try
            WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "IsStar", Not PageInstanceLeft.Instance.IsStar)
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            McInstanceListForceRefresh = True
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, $"版本 {PageInstanceLeft.Instance.Name} 收藏状态更改失败", LogBehavior.Alert)
        End Try
    End Sub

#End Region

#Region "卡片：快捷方式"

    '版本文件夹
    Private Sub BtnFolderVersion_Click() Handles BtnFolderVersion.Click
        OpenInstanceFolder(PageInstanceLeft.Instance)
    End Sub
    Public Shared Sub OpenInstanceFolder(Instance As McInstance)
        OpenExplorer(Instance.PathVersion)
    End Sub

    '存档文件夹
    Private Sub BtnFolderSaves_Click() Handles BtnFolderSaves.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "saves\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

    'Mod 文件夹
    Private Sub BtnFolderMods_Click() Handles BtnFolderMods.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "mods\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

#End Region

#Region "卡片：管理"

    '导出启动脚本
    Private Sub BtnManageScript_Click() Handles BtnManageScript.Click
        Try
            '弹窗要求指定脚本的保存位置
            Dim SavePath As String = Dialogs.SaveFile(GetLang("LangPageVersionOverallSelectSaveCommandFile"), "启动 " & PageInstanceLeft.Instance.Name & ".bat", filter:={("bat", GetLang("LangPageVersionOverallBatchFileFilter"))})
            If SavePath Is Nothing Then Return
            '检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            If McLaunchLoader.State = LoadState.Loading Then
                Hint(GetLang("LangPageVersionOverallHintWaitForTaskOver"), HintType.Red)
                Return
            End If
            '生成脚本
            If McLaunchStart(New McLaunchOptions With {.SaveBatch = SavePath, .Instance = PageInstanceLeft.Instance}) Then
                If Settings.Get(Of McLoginType)("LoginType") = McLoginType.Legacy Then
                    Hint(GetLang("LangPageVersionOverallHintExportingCommandA"))
                Else
                    Hint(GetLang("LangPageVersionOverallHintExportingCommandB"))
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, GetLang("LangPageVersionOverallHintExportingCommandFail") & "（" & PageInstanceLeft.Instance.Name & "）", LogBehavior.Alert)
        End Try
    End Sub

    '补全文件
    Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
        Try
            '忽略文件检查提示
            If ShouldIgnoreFileCheck(PageInstanceLeft.Instance) Then
                Hint(GetLang("LangPageVersionOverallHintEnableInstanceAssetsCheck"), HintType.Blue)
                Return
            End If
            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar
                If OngoingLoader.Name <> PageInstanceLeft.Instance.Name & " " & GetLang("LangPageVersionOverallTaskCompleteFile") Then Continue For
                Hint(GetLang("LangPageVersionOverallCompleteFileInTask"), HintType.Red)
                Return
            Next
            '启动
            Dim Loader As New LoaderCombo(Of String)(PageInstanceLeft.Instance.Name & " " & GetLang("LangPageVersionOverallTaskCompleteFile"), DlClientFix(PageInstanceLeft.Instance, True, False))
            Loader.OnStateChanged =
            Sub()
                Select Case Loader.State
                    Case LoadState.Finished
                        Hint(Loader.Name & GetLang("LangPageVersionOverallCompleteFileSuccess"), HintType.Green)
                    Case LoadState.Failed
                        Hint(Loader.Name & GetLang("LangPageVersionOverallCompleteFileFail") & Loader.Error.GetDisplay(False), HintType.Red)
                    Case LoadState.Canceled
                        Hint(Loader.Name & GetLang("LangTaskAbort"), HintType.Blue)
                End Select
            End Sub
            Loader.Start(PageInstanceLeft.Instance.Name)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Logger.Error(ex, $"尝试补全文件失败（{PageInstanceLeft.Instance.Name}）", LogBehavior.Alert)
        End Try
    End Sub

    '删除版本
    '修改此代码时，同时修改 PageSelectRight 中的代码
    Private Sub BtnManageDelete_Click(sender As Object, e As EventArgs) Handles BtnManageDelete.Click
        Try
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = PageInstanceLeft.Instance.State <> McInstanceState.Error AndAlso PageInstanceLeft.Instance.PathIndie <> McFolderSelected
            Dim MsgContent = ""
            If IsShiftPressed Then
                MsgContent = GetLang("LangPageVersionOverallDialogDeleteA", PageInstanceLeft.Instance.Name)
            Else
                MsgContent = GetLang("LangPageVersionOverallDialogDeleteB", PageInstanceLeft.Instance.Name)
            End If
            Select Case MyMsgBox(MsgContent,
                        GetLang("LangPageVersionOverallDialogDeleteTitle"), , GetLang("LangDialogBtnCancel"),, IsHintIndie OrElse IsShiftPressed)
                Case 1
                    PageInstanceLeft.Instance.ResetSettingsCache()
                    DirectoryUtils.Delete(PageInstanceLeft.Instance.PathVersion, Not IsShiftPressed)
                    Hint( GetLang("LangPageVersionOverallHintDeleteSuccess", PageInstanceLeft.Instance.Name), HintType.Green)
                Case 2
                    Return
            End Select
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            FrmMain.PageBack()
        Catch ex As OperationCanceledException
            Logger.Warn(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 被主动取消")
        Catch ex As Exception
            Logger.Error(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 失败", LogBehavior.Alert)
        End Try
    End Sub

#End Region

End Class
