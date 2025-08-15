Public Class PageDownloadNeoForge

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, DlNeoForgeListLoader, AddressOf Load_OnFinish)
    End Sub
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub

    Private Sub Load_OnFinish()
        '结果数据化
        Try
            '归类
            Dim Dict = DlNeoForgeListLoader.Output.Value.GroupBy(Function(d) d.Inherit).OrderByDescending(Function(g) g.Key).
                ToDictionary(Function(g) g.Key, Function(g) g.ToList())
            '清空当前
            PanMain.Children.Clear()
            '转化为 UI
            For Each Pair As KeyValuePair(Of String, List(Of DlNeoForgeListEntry)) In Dict
                If Not Pair.Value.Any() Then Continue For
                '增加卡片
                Dim NewCard As New MyCard With {.Title = Pair.Key & " (" & Pair.Value.Count & ")", .Margin = New Thickness(0, 0, 0, 15), .SwapType = 13}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = Pair.Value}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                NewCard.IsSwapped = True
                PanMain.Children.Add(NewCard)
            Next
        Catch ex As Exception
            Log(ex, "可视化 NeoForge 版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

End Class