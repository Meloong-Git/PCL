Public Class PageSelectRight

    '窗口基础
    Private Sub PageSelectRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
        PanBack.ScrollToHome()
    End Sub
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, McVersionListLoader, AddressOf McVersionListUI, AutoRun:=False)
    End Sub
    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If McVersionListLoader.State = LoadState.Failed Then
            LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        End If
    End Sub

    '窗口属性
    ''' <summary>
    ''' 是否显示隐藏的 Minecraft 版本。
    ''' </summary>
    Public ShowHidden As Boolean = False

#Region "结果 UI 化"

    Private Sub McVersionListUI(Loader As LoaderTask(Of String, Integer))
        Try
            Dim Path As String = Loader.Input
            '加载 UI
            PanMain.Children.Clear()

            For Each Card As KeyValuePair(Of McVersionCardType, List(Of McVersion)) In McVersionList.ToArray
                '确认是否为隐藏版本显示状态
                If Card.Key = McVersionCardType.Hidden Xor ShowHidden Then Continue For
#Region "确认卡片名称"
                Dim CardName As String = ""
                Select Case Card.Key
                    Case McVersionCardType.OriginalLike
                        CardName = "常规版本"
                    Case McVersionCardType.API
                        Dim IsForgeExists As Boolean = False
                        Dim IsNeoForgeExists As Boolean = False
                        Dim IsFabricExists As Boolean = False
                        Dim IsLiteExists As Boolean = False
                        For Each Version As McVersion In Card.Value
                            If Version.Version.HasFabric Then IsFabricExists = True
                            If Version.Version.HasLiteLoader Then IsLiteExists = True
                            If Version.Version.HasForge Then IsForgeExists = True
                            If Version.Version.HasNeoForge Then IsNeoForgeExists = True
                        Next
                        If If(IsLiteExists, 1, 0) + If(IsForgeExists, 1, 0) + If(IsFabricExists, 1, 0) + If(IsNeoForgeExists, 1, 0) > 1 Then
                            CardName = "可安装 Mod"
                        ElseIf IsForgeExists Then
                            CardName = "Forge 版本"
                        ElseIf IsNeoForgeExists Then
                            CardName = "NeoForge 版本"
                        ElseIf IsLiteExists Then
                            CardName = "LiteLoader 版本"
                        Else
                            CardName = "Fabric 版本"
                        End If
                    Case McVersionCardType.Error
                        CardName = "错误的版本"
                    Case McVersionCardType.Hidden
                        CardName = "隐藏的版本"
                    Case McVersionCardType.Rubbish
                        CardName = "不常用版本"
                    Case McVersionCardType.Star
                        CardName = "收藏夹"
                    Case McVersionCardType.Fool
                        CardName = "愚人节版本"
                    Case Else
                        Throw New ArgumentException("未知的卡片种类（" & Card.Key & "）")
                End Select
#End Region
                '建立控件
                Dim CardTitle As String = CardName & If(CardName = "收藏夹", "", " (" & Card.Value.Count & ")")
                Dim NewCard As New MyCard With {.Title = CardTitle, .Margin = New Thickness(0, 0, 0, 15), .SwapType = 0}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = Card.Value}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                PanMain.Children.Add(NewCard)
                '确定卡片是否展开
                If Card.Key = McVersionCardType.Rubbish OrElse Card.Key = McVersionCardType.Error OrElse Card.Key = McVersionCardType.Fool Then
                    NewCard.IsSwaped = True
                Else
                    MyCard.StackInstall(NewStack, 0, CardTitle)
                End If
            Next

            '若只有一个卡片，则强制展开
            If PanMain.Children.Count = 1 AndAlso CType(PanMain.Children(0), MyCard).IsSwaped Then
                CType(PanMain.Children(0), MyCard).IsSwaped = False
            End If

            '判断应该显示哪一个页面
            If PanMain.Children.Count = 0 Then
                PanEmpty.Visibility = Visibility.Visible
                PanBack.Visibility = Visibility.Collapsed
                If ShowHidden Then
                    LabEmptyTitle.Text = "无隐藏版本"
                    LabEmptyContent.Text = "没有版本被隐藏，你可以在版本设置的版本分类选项中隐藏版本。" & vbCrLf & "再次按下 F11 即可退出隐藏版本查看模式。"
                    BtnEmptyDownload.Visibility = Visibility.Collapsed
                Else
                    LabEmptyTitle.Text = "无可用版本"
                    LabEmptyContent.Text = "未找到任何版本的游戏，请先下载任意版本的游戏。" & vbCrLf & "若有已存在的游戏，请在左边的列表中选择添加文件夹，选择 .minecraft 文件夹将其导入。"
                    BtnEmptyDownload.Visibility = If(Setup.Get("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
                End If
            Else
                PanBack.Visibility = Visibility.Visible
                PanEmpty.Visibility = Visibility.Collapsed
            End If

        Catch ex As Exception
            Log(ex, "将版本列表转换显示时失败", LogLevel.Feedback)
        End Try
    End Sub
    Public Shared Function McVersionListItem(Version As McVersion) As MyListItem
        Dim NewItem As New MyListItem With {.Title = Version.Name, .Info = Version.Info, .Height = 42, .Tag = Version, .SnapsToDevicePixels = True, .Type = MyListItem.CheckType.Clickable}
        Try
            If Version.Logo.EndsWith("PCL\Logo.png") Then
                NewItem.Logo = Version.Path & "PCL\Logo.png" '修复老版本中，存储的自定义 Logo 使用完整路径，导致移动后无法加载的 Bug
            Else
                NewItem.Logo = Version.Logo
            End If
        Catch ex As Exception
            Log(ex, "加载版本图标失败", LogLevel.Hint)
            NewItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png"
        End Try
        NewItem.ContentHandler = AddressOf McVersionListContent
        Return NewItem
    End Function
    Private Shared Sub McVersionListContent(sender As MyListItem, e As EventArgs)
        Dim Version As McVersion = sender.Tag
        '注册点击事件
        AddHandler sender.Click, AddressOf Item_Click
        '图标按钮
        Dim BtnStar As New MyIconButton
        If Version.IsStar Then
            BtnStar.ToolTip = "取消收藏"
            ToolTipService.SetPlacement(BtnStar, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnStar, 30)
            ToolTipService.SetHorizontalOffset(BtnStar, 2)
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeFill
        Else
            BtnStar.ToolTip = "收藏"
            ToolTipService.SetPlacement(BtnStar, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnStar, 30)
            ToolTipService.SetHorizontalOffset(BtnStar, 2)
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeLine
        End If
        AddHandler BtnStar.Click, Sub()
                                      WriteIni(Version.Path & "PCL\Setup.ini", "IsStar", Not Version.IsStar)
                                      McVersionListForceRefresh = True
                                      LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                                  End Sub
        Dim BtnDel As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonDelete}
        BtnDel.ToolTip = "删除"
        ToolTipService.SetPlacement(BtnDel, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnDel, 30)
        ToolTipService.SetHorizontalOffset(BtnDel, 2)
        AddHandler BtnDel.Click, Sub() DeleteVersion(sender, Version)
        If Version.State <> McVersionState.Error Then
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonSetup}
            BtnCont.ToolTip = "设置"
            ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnCont, 30)
            ToolTipService.SetHorizontalOffset(BtnCont, 2)
            AddHandler BtnCont.Click,
            Sub()
                PageVersionLeft.Version = Version
                FrmMain.PageChange(FormMain.PageType.VersionSetup, 0)
            End Sub
            AddHandler sender.MouseRightButtonUp,
            Sub()
                PageVersionLeft.Version = Version
                FrmMain.PageChange(FormMain.PageType.VersionSetup, 0)
            End Sub
            sender.Buttons = {BtnStar, BtnDel, BtnCont}
        Else
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.15, .Logo = Logo.IconButtonOpen}
            BtnCont.ToolTip = "打开文件夹"
            ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnCont, 30)
            ToolTipService.SetHorizontalOffset(BtnCont, 2)
            AddHandler BtnCont.Click, Sub() PageVersionOverall.OpenVersionFolder(Version)
            AddHandler sender.MouseRightButtonUp, Sub() PageVersionOverall.OpenVersionFolder(Version)
            sender.Buttons = {BtnStar, BtnDel, BtnCont}
        End If
    End Sub

#End Region

#Region "页面事件"

    '点击选项
    Public Shared Sub Item_Click(sender As MyListItem, e As EventArgs)
        Dim Version As McVersion = sender.Tag
        If New McVersion(Version.Path).Check Then
            '正常版本
            McVersionCurrent = Version
            Setup.Set("LaunchVersionSelect", McVersionCurrent.Name)
            FrmMain.PageBack()
        Else
            '错误版本
            PageVersionOverall.OpenVersionFolder(Version)
        End If
    End Sub

    Private Sub BtnDownload_Click(sender As Object, e As EventArgs) Handles BtnEmptyDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    End Sub

    '修改此代码时，同时修改 PageVersionOverall 中的代码
    Public Shared Sub DeleteVersion(Item As MyListItem, Version As McVersion)
        Try
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = Version.State <> McVersionState.Error AndAlso Version.PathIndie <> PathMcFolder
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除版本 {Version.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "由于该版本开启了版本隔离，删除版本时该版本对应的存档、资源包、Mod 等文件也将被一并删除！", ""),
                        "版本删除确认", , "取消",, True)
                Case 1
                    IniClearCache(Version.PathIndie & "options.txt")
                    IniClearCache(Version.Path & "PCL\Setup.ini")
                    If IsShiftPressed Then
                        DeleteDirectory(Version.Path)
                        Hint("版本 " & Version.Name & " 已永久删除！", HintType.Finish)
                    Else
                        FileIO.FileSystem.DeleteDirectory(Version.Path, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                        Hint("版本 " & Version.Name & " 已删除到回收站！", HintType.Finish)
                    End If
                Case 2
                    Return
            End Select
            '从 UI 中移除
            If Version.DisplayType = McVersionCardType.Hidden OrElse Not Version.IsStar Then
                '仅出现在当前卡片
                Dim Parent As StackPanel = Item.Parent
                If Parent.Children.Count > 2 Then '当前的项目与一个占位符
                    '删除后还有剩
                    Dim Card As MyCard = Parent.Parent
                    Card.Title = Card.Title.Replace(Parent.Children.Count - 1, Parent.Children.Count - 2) '有一个占位符
                    Parent.Children.Remove(Item)
                    If McVersionCurrent IsNot Nothing AndAlso Version.Path = McVersionCurrent.Path Then
                        '删除当前版本就更改选择
                        McVersionCurrent = CType(Parent.Children(0), MyListItem).Tag
                    End If
                    LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.UpdateOnly, MaxDepth:=1, ExtraPath:="versions\")
                Else
                    '删除后没剩了
                    LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                End If
            Else
                '同时出现在当前卡片与收藏夹
                LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            End If
        Catch ex As OperationCanceledException
            Log(ex, "删除版本 " & Version.Name & " 被主动取消")
        Catch ex As Exception
            Log(ex, "删除版本 " & Version.Name & " 失败", LogLevel.Msgbox)
        End Try
    End Sub

    Public Sub BtnEmptyDownload_Loaded() Handles BtnEmptyDownload.Loaded
        Dim NewVisibility = If((Setup.Get("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow) OrElse ShowHidden, Visibility.Collapsed, Visibility.Visible)
        If BtnEmptyDownload.Visibility <> NewVisibility Then
            BtnEmptyDownload.Visibility = NewVisibility
            PanLoad.TriggerForceResize()
        End If
    End Sub

#End Region

End Class
