Public Class PageSelectRight

    '窗口基础
    Private Sub PageSelectRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
        PanBack.ScrollToHome()
    End Sub
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, McInstanceListLoader, AddressOf McInstanceListUI, AutoRun:=False)
    End Sub
    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If McInstanceListLoader.State = LoadState.Failed Then
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        End If
    End Sub

    '窗口属性
    ''' <summary>
    ''' 是否显示隐藏的 Minecraft 版本。
    ''' </summary>
    Public ShowHidden As Boolean = False

#Region "结果 UI 化"

    Private Sub McInstanceListUI(Loader As LoaderTask(Of String, Integer))
        Try
            '加载 UI
            PanMain.Children.Clear()

            For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList.ToArray
                '确认是否为隐藏版本显示状态
                If Card.Key = McInstanceCardType.Hidden Xor ShowHidden Then Continue For
#Region "确认卡片名称"
                Dim CardName As String = ""
                Select Case Card.Key
                    Case McInstanceCardType.OriginalLike
                        CardName = "常规版本"
                    Case McInstanceCardType.API
                        Dim IsForgeExists As Boolean = False
                        Dim IsNeoForgeExists As Boolean = False
                        Dim IsFabricExists As Boolean = False
                        Dim IsLiteExists As Boolean = False
                        For Each Instance As McInstance In Card.Value
                            If Instance.Version.HasFabric Then IsFabricExists = True
                            If Instance.Version.HasLiteLoader Then IsLiteExists = True
                            If Instance.Version.HasForge Then IsForgeExists = True
                            If Instance.Version.HasNeoForge Then IsNeoForgeExists = True
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
                    Case McInstanceCardType.Error
                        CardName = "错误的版本"
                    Case McInstanceCardType.Hidden
                        CardName = "隐藏的版本"
                    Case McInstanceCardType.Rubbish
                        CardName = "不常用版本"
                    Case McInstanceCardType.Star
                        CardName = "收藏夹"
                    Case McInstanceCardType.Fool
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
                If Card.Key = McInstanceCardType.Rubbish OrElse Card.Key = McInstanceCardType.Error OrElse Card.Key = McInstanceCardType.Fool Then
                    NewCard.IsSwapped = True
                Else
                    MyCard.StackInstall(NewStack, 0, CardTitle)
                End If
            Next

            '若只有一个卡片，则强制展开
            If PanMain.Children.Count = 1 AndAlso CType(PanMain.Children(0), MyCard).IsSwapped Then
                CType(PanMain.Children(0), MyCard).IsSwapped = False
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
    Public Shared Sub McInstanceListContent(sender As MyListItem, e As EventArgs)
        Dim Instance As McInstance = sender.Tag
        '注册点击事件
        AddHandler sender.Click, AddressOf Item_Click
        '图标按钮
        Dim BtnStar As New MyIconButton
        If Instance.IsStar Then
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
                                      WriteIni(Instance.PathVersion & "PCL\Setup.ini", "IsStar", Not Instance.IsStar)
                                      McInstanceListForceRefresh = True
                                      LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                                  End Sub
        Dim BtnDel As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonDelete}
        BtnDel.ToolTip = "删除"
        ToolTipService.SetPlacement(BtnDel, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnDel, 30)
        ToolTipService.SetHorizontalOffset(BtnDel, 2)
        AddHandler BtnDel.Click, Sub() DeleteInstance(sender, Instance)
        If Instance.State <> McInstanceState.Error Then
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonSetup}
            BtnCont.ToolTip = "设置"
            ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnCont, 30)
            ToolTipService.SetHorizontalOffset(BtnCont, 2)
            AddHandler BtnCont.Click,
            Sub()
                PageInstanceLeft.Instance = Instance
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            AddHandler sender.MouseRightButtonUp,
            Sub()
                PageInstanceLeft.Instance = Instance
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            sender.Buttons = {BtnStar, BtnDel, BtnCont}
        Else
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.15, .Logo = Logo.IconButtonOpen}
            BtnCont.ToolTip = "打开文件夹"
            ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnCont, 30)
            ToolTipService.SetHorizontalOffset(BtnCont, 2)
            AddHandler BtnCont.Click, Sub() PageInstanceOverall.OpenInstanceFolder(Instance)
            AddHandler sender.MouseRightButtonUp, Sub() PageInstanceOverall.OpenInstanceFolder(Instance)
            sender.Buttons = {BtnStar, BtnDel, BtnCont}
        End If
    End Sub

#End Region

#Region "页面事件"

    '点击选项
    Public Shared Sub Item_Click(sender As MyListItem, e As EventArgs)
        Dim Instance As McInstance = sender.Tag
        If New McInstance(Instance.PathVersion).Check Then
            '正常版本
            McInstanceSelected = Instance
            Setup.Set("LaunchVersionSelect", McInstanceSelected.Name)
            FrmMain.PageBack()
        Else
            '错误版本
            PageInstanceOverall.OpenInstanceFolder(Instance)
        End If
    End Sub

    Private Sub BtnDownload_Click(sender As Object, e As EventArgs) Handles BtnEmptyDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    End Sub

    '修改此代码时，同时修改 PageInstanceOverall 中的代码
    Public Shared Sub DeleteInstance(Item As MyListItem, Instance As McInstance)
        Try
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = Instance.State <> McInstanceState.Error AndAlso Instance.PathIndie <> McFolderSelected
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除版本 {Instance.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "由于该版本开启了版本隔离，删除版本时该版本对应的存档、资源包、Mod 等文件也将被一并删除！", ""),
                        "版本删除确认", , "取消",, True)
                Case 1
                    IniClearCache(Instance.PathIndie & "options.txt")
                    IniClearCache(Instance.PathVersion & "PCL\Setup.ini")
                    If IsShiftPressed Then
                        DeleteDirectory(Instance.PathVersion)
                    Else
                        FileIO.FileSystem.DeleteDirectory(Instance.PathVersion, FileIO.UIOption.AllDialogs, FileIO.RecycleOption.SendToRecycleBin)
                    End If
                    Hint("版本 " & Instance.Name & " 已删除！", HintType.Green)
                Case 2
                    Return
            End Select
            '从 UI 中移除
            If Instance.DisplayType = McInstanceCardType.Hidden OrElse Not Instance.IsStar Then
                '仅出现在当前卡片
                Dim Parent As StackPanel = Item.Parent
                If Parent.Children.Count > 2 Then '当前的项目与一个占位符
                    '删除后还有剩
                    Dim Card As MyCard = Parent.Parent
                    Card.Title = Card.Title.Replace(Parent.Children.Count - 1, Parent.Children.Count - 2) '有一个占位符
                    Parent.Children.Remove(Item)
                    If McInstanceSelected IsNot Nothing AndAlso Instance.PathVersion = McInstanceSelected.PathVersion Then
                        '删除当前版本就更改选择
                        McInstanceSelected = MyVirtualizingElement.TryInit(Parent.Children(0)).Tag
                    End If
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.UpdateOnly, MaxDepth:=1, ExtraPath:="versions\")
                Else
                    '删除后没剩了
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                End If
            Else
                '同时出现在当前卡片与收藏夹
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            End If
        Catch ex As OperationCanceledException
            Log(ex, "删除版本 " & Instance.Name & " 被主动取消")
        Catch ex As Exception
            Log(ex, "删除版本 " & Instance.Name & " 失败", LogLevel.Msgbox)
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
