Class PageSpeedRight

    Private Sub Page_Loaded() Handles Me.Loaded
        '进入时就刷新一次显示
        Watcher()
        '如果在页面切换动画的 “上一页消失” 部分已经完成了下载，就直接尝试返回
        TryReturnToHome()

        PanBack.ScrollToHome()
    End Sub

    '由左侧页面调用
    Public Sub Watcher()
        Try
            For Each Loader As LoaderBase In LoaderTaskbar.ToList
                TaskRefresh(Loader)
            Next
        Catch ex As Exception
            Log(ex, "下载管理右栏监视出错", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 寻找对应这个Loader的下载卡片，找不到返回Nothing
    ''' </summary>
    Private Function FindTaskCard(Loader As LoaderBase) As MyTaskCard
        For Each Card In PanMain.Children
            If TryCast(Card, MyTaskCard)?.LoaderUuid = Loader.Uuid Then Return Card
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 刷新对应这个Loader的下载卡片
    ''' </summary>
    Public Sub TaskRefresh(Loader As LoaderBase)
        If Loader Is Nothing OrElse Not Loader.Show Then Exit Sub
        Dim TaskCard As MyTaskCard = FindTaskCard(Loader)
        Select Case Loader.State
            Case LoadState.Failed, LoadState.Loading, LoadState.Waiting
                '应该添加或刷新卡片
                If TaskCard IsNot Nothing Then
                    TaskCard.RefreshSubTasks(Loader)
                Else
                    Log($"[UI] 任务列表页面添加卡片：{Loader.Name}")
                    PanMain.Children.Add(New MyTaskCard(Loader) With {.Margin = New Thickness(0, 0, 0, 15)})
                End If
            Case LoadState.Finished, LoadState.Aborted
                '应当移除卡片
                If TaskCard IsNot Nothing Then
                    Log($"[UI] 任务列表页面监控到任务已结束或中止，删除卡片：{Loader.Name}")
                    AniDispose(TaskCard, True, AddressOf TryReturnToHome)
                End If
        End Select
    End Sub

    ''' <summary>
    ''' 删除对应这个Loader的下载卡片
    ''' </summary>
    Public Sub TaskRemove(Loader As LoaderBase)
        RunInUiWait(
            Sub()
                For Each Card In PanMain.Children
                    If TypeOf Card IsNot MyTaskCard OrElse CType(Card, MyTaskCard).LoaderUuid <> Loader.Uuid Then Continue For
                    Log($"[UI] 任务列表页面删除卡片：{Loader.Name}")
                    PanMain.Children.Remove(Card)
                Next
            End Sub)
    End Sub

    ''' <summary>
    ''' 若没有任务，返回上一页
    ''' </summary>
    Public Sub TryReturnToHome()
        If PanMain.Children.Count = 0 AndAlso FrmMain.PageCurrent = FormMain.PageType.DownloadManager Then
            Log("[UI] 任务列表页面无内容，返回上一页")
            FrmMain.PageBack()
        End If
    End Sub

End Class
