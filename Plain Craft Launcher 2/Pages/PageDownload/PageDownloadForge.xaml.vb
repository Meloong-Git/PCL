Public Class PageDownloadForge

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, DlForgeListLoader, AddressOf Load_OnFinish)
    End Sub
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub

    Private Sub Load_OnFinish()
        '结果数据化
        Try
            '清空当前
            PanMain.Children.Clear()
            '转化为 UI
            For Each Version As String In DlForgeListLoader.Output.Value.Sort(AddressOf VersionSortBoolean)
                '增加卡片
                Dim NewCard As New MyCard With {.Title = Version.Replace("_p", " P"), .Margin = New Thickness(0, 0, 0, 15), .SwapType = 5}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = Version}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                NewCard.IsSwaped = True
                PanMain.Children.Add(NewCard)
            Next
            ''非官方源警示
            'If Setup.Get("ToolDownloadOutOfDate") AndAlso Not DlForgeListLoader.Output.IsOfficial Then
            '    Dim CardWarn As New MyCard With {.Title = "过期提示", .Margin = New Thickness(0, 0, 0, 15)}
            '    CardWarn.Children.Add(New TextBlock With {
            '                          .Margin = New Thickness(25, MyCard.SwapedHeight, 15, 15), .VerticalAlignment = VerticalAlignment.Top, .HorizontalAlignment = HorizontalAlignment.Left, .TextTrimming = TextTrimming.None, .TextWrapping = TextWrapping.Wrap,
            '                          .Text = "获取官方源失败，正在使用 " & DlForgeListLoader.Output.SourceName & " 镜像源，版本列表可能并非最新。" & vbCrLf & "官方源错误原因：" & If(DlForgeListLoader.Output.OfficialError, New Exception("连接服务器超时")).Message})
            '    PanMain.Children.Insert(0, CardWarn)
            'End If
        Catch ex As Exception
            Log(ex, "可视化 Forge 版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    'Forge 版本列表加载
    Public Sub Forge_Click(sender As MyLoading, e As MouseButtonEventArgs)
        If sender.State.LoadingState = MyLoading.MyLoadingState.Error Then
            CType(sender.State, LoaderTask(Of String, List(Of DlForgeVersionEntry))).Start(IsForceRestart:=True)
        End If
    End Sub
    Public Sub Forge_StateChanged(sender As MyLoading, newState As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState)
        If newState <> MyLoading.MyLoadingState.Stop Then Return

        Dim Card As MyCard = CType(sender.Parent, FrameworkElement).Parent
        Dim Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)) = sender.State
        '载入列表
        Card.SwapControl.Children.Clear()
        Card.SwapControl.Tag = Loader.Output
        Card.SwapType = 6
        Card.StackInstall()
    End Sub

    '介绍栏
    Private Sub BtnWeb_Click(sender As Object, e As EventArgs) Handles BtnWeb.Click
        OpenWebsite("https://files.minecraftforge.net")
    End Sub

End Class
