Public Class PageDownloadLiteLoader

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, DlLiteLoaderListLoader, AddressOf Load_OnFinish)
    End Sub
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub

    Private Sub Load_OnFinish()
        '结果数据化
        Try
            '归类
            Dim Dict As New Dictionary(Of String, List(Of DlLiteLoaderListEntry))
            For VersionCode As Integer = 30 To 0 Step -1
                Dict.Add("1." & VersionCode, New List(Of DlLiteLoaderListEntry))
            Next
            Dict.Add("未知版本", New List(Of DlLiteLoaderListEntry))
            For Each Version As DlLiteLoaderListEntry In DlLiteLoaderListLoader.Output.Value
                Dim MainVersion As String = "1." & Version.Inherit.Split(".")(1)
                If Dict.ContainsKey(MainVersion) Then
                    Dict(MainVersion).Add(Version)
                Else
                    Dict("未知版本").Add(Version)
                End If
            Next
            '清空当前
            PanMain.Children.Clear()
            '转化为 UI
            For Each Pair As KeyValuePair(Of String, List(Of DlLiteLoaderListEntry)) In Dict
                If Not Pair.Value.Any() Then Continue For
                '增加卡片
                Dim NewCard As New MyCard With {.Title = Pair.Key & " (" & Pair.Value.Count & ")", .Margin = New Thickness(0, 0, 0, 15), .SwapType = 10}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = Pair.Value}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                NewCard.IsSwapped = True
                PanMain.Children.Add(NewCard)
            Next
        Catch ex As Exception
            Log(ex, "可视化 LiteLoader 版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    Public Sub DownloadStart(sender As MyListItem, e As Object)
        McDownloadLiteLoader(sender.Tag)
    End Sub
    Private Sub BtnWeb_Click(sender As Object, e As EventArgs) Handles BtnWeb.Click
        OpenWebsite("https://www.liteloader.com")
    End Sub

End Class
