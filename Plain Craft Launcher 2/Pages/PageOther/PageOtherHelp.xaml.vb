Public Class PageOtherHelp
    Implements IRefreshable

#Region "初始化"

    '滚动条
    Private Sub PageOther_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub
    '初始化加载器信息
    Private Sub PageOther_Inited(sender As Object, e As EventArgs) Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanBack, Nothing, HelpLoader, AddressOf HelpListLoad)
    End Sub

#End Region

    ''' <summary>
    ''' 将帮助列表对象实例化为主页 UI。
    ''' </summary>
    Private Sub HelpListLoad(Loader As LoaderTask(Of Integer, List(Of HelpEntry)))
        Try

            '初始化
            PanList.Children.Clear()
            PanBack.ScrollToHome()
            Dim HelpItems = Loader.Output
            '获取全部分类
            Dim Types As New List(Of String)
            For Each Item As HelpEntry In HelpItems
                If Val(VersionBranchCode) = 50 AndAlso Not Item.ShowInPublic Then Continue For
                If Val(VersionBranchCode) <> 50 AndAlso Not Item.ShowInSnapshot Then Continue For
                For Each Type In Item.Types
                    If Not Types.Contains(Type) Then Types.Add(Type)
                Next
            Next
            '将指南页面置顶
            If Types.Contains("指南") Then
                Types.Remove("指南")
                Types.Insert(0, "指南")
            End If
            '转化为 UI
            For Each Type As String In Types
                '确认所属该分类的项目
                Dim TypeItems As New List(Of HelpEntry)
                For Each Item In HelpItems
                    If Val(VersionBranchCode) = 50 AndAlso Not Item.ShowInPublic Then Continue For
                    If Val(VersionBranchCode) <> 50 AndAlso Not Item.ShowInSnapshot Then Continue For
                    If Item.Types.Contains(Type) Then TypeItems.Add(Item)
                Next
                '增加卡片
                Dim NewCard As New MyCard With {.Title = Type, .Margin = New Thickness(0, 0, 0, 15), .SwapType = 11}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = TypeItems}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                If Type = "指南" Then
                    MyCard.StackInstall(NewStack, 11, "指南")
                Else
                    NewCard.IsSwapped = True
                End If
                PanList.Children.Add(NewCard)
            Next

        Catch ex As Exception
            Log(ex, "加载帮助列表 UI 失败", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 帮助项目的点击事件。
    ''' </summary>
    Public Shared Sub OnItemClick(Entry As HelpEntry)
        Try
            If Entry.IsEvent Then
                ModEvent.TryStartEvent(Entry.EventType, Entry.EventData)
            Else
                EnterHelpPage(Entry)
            End If
        Catch ex As Exception
            Log(ex, "处理帮助项目点击时发生意外错误", LogLevel.Feedback)
        End Try
    End Sub
    Public Shared Sub EnterHelpPage(Location As String)
        RunInThread(
        Sub()
            If Not HelpLoader.State = LoadState.Finished Then HelpLoader.WaitForExit(GetUuid)
            Dim Entry As New HelpEntry(Location)
            RunInUi(
            Sub()
                Dim FrmHelpDetail As New PageOtherHelpDetail
                If FrmHelpDetail.Init(Entry) Then
                    FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.HelpDetail, .Additional = {Entry, FrmHelpDetail}})
                Else
                    Log("[Help] 已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃", LogLevel.Debug)
                End If
            End Sub)
        End Sub)
    End Sub
    Public Shared Sub EnterHelpPage(Entry As HelpEntry)
        RunInThread(
        Sub()
            If Not HelpLoader.State = LoadState.Finished Then HelpLoader.WaitForExit(GetUuid)
            RunInUi(
            Sub()
                Dim FrmHelpDetail As New PageOtherHelpDetail
                If FrmHelpDetail.Init(Entry) Then
                    FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.HelpDetail, .Additional = {Entry, FrmHelpDetail}})
                Else
                    Log("[Help] 已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃", LogLevel.Debug)
                End If
            End Sub)
        End Sub)
    End Sub
    Public Shared Function GetHelpPage(Location As String) As PageOtherHelpDetail
        If Not HelpLoader.State = LoadState.Finished Then HelpLoader.WaitForExit(GetUuid)
        Dim FrmHelpDetail As New PageOtherHelpDetail
        If FrmHelpDetail.Init(New HelpEntry(Location)) Then
            Return FrmHelpDetail
        Else
            Throw New Exception("已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃")
        End If
    End Function

    ''' <summary>
    ''' 搜索帮助。
    ''' </summary>
    Public Sub SearchRun() Handles SearchBox.TextChanged
        If String.IsNullOrWhiteSpace(SearchBox.Text) Then
            '隐藏
            AniStart({
                 AaOpacity(PanSearch, -PanSearch.Opacity, 100),
                 AaCode(
                 Sub()
                     PanSearch.Height = 0
                     PanSearch.Visibility = Visibility.Collapsed
                     PanList.Visibility = Visibility.Visible
                 End Sub,, True),
                 AaOpacity(PanList, 1 - PanList.Opacity, 150, 30)
            }, "FrmOtherHelp Search Switch")
        Else
            '构造请求
            Dim QueryList As New List(Of SearchEntry(Of HelpEntry))
            For Each Entry As HelpEntry In HelpLoader.Output
                If Not Entry.ShowInSearch OrElse (Val(VersionBranchCode) = 50 AndAlso Not Entry.ShowInPublic) Then Continue For
                If Not Entry.ShowInSearch OrElse (Val(VersionBranchCode) <> 50 AndAlso Not Entry.ShowInSnapshot) Then Continue For
                QueryList.Add(New SearchEntry(Of HelpEntry) With {
                    .Item = Entry,
                    .SearchSource = New List(Of KeyValuePair(Of String, Double)) From {
                        New KeyValuePair(Of String, Double)(Entry.Title, 1),
                        New KeyValuePair(Of String, Double)(Entry.Desc, 0.5),
                        New KeyValuePair(Of String, Double)(Entry.Search, 1.5)
                    }
                })
                'New KeyValuePair(Of String, Double)(If(Entry.IsEvent, If(Entry.EventData, ""), Entry.XamlContent), 0.2)
            Next
            '进行搜索，构造列表
            Dim SearchResult = Search(QueryList, SearchBox.Text, MaxBlurCount:=5, MinBlurSimilarity:=0.08)
            PanSearchList.Children.Clear()
            If Not SearchResult.Any() Then
                PanSearch.Title = "无搜索结果"
                PanSearchList.Visibility = Visibility.Collapsed
            Else
                PanSearch.Title = "搜索结果"
                For Each Result In SearchResult
                    Dim Item = Result.Item.ToListItem
                    If ModeDebug Then Item.Info = If(Result.AbsoluteRight, "完全匹配，", "") & "相似度：" & Math.Round(Result.Similarity, 3) & "，" & Item.Info
                    PanSearchList.Children.Add(Item)
                Next
                PanSearchList.Visibility = Visibility.Visible
            End If
            '显示
            AniStart({
                 AaOpacity(PanList, -PanList.Opacity, 100),
                 AaCode(
                 Sub()
                     PanList.Visibility = Visibility.Collapsed
                     PanSearch.Visibility = Visibility.Visible
                     PanSearch.TriggerForceResize()
                 End Sub,, True),
                 AaOpacity(PanSearch, 1 - PanSearch.Opacity, 150, 30)
            }, "FrmOtherHelp Search Switch")
        End If
    End Sub

    Public Sub Refresh() Implements IRefreshable.Refresh
        PageOtherLeft.RefreshHelp()
    End Sub
End Class
