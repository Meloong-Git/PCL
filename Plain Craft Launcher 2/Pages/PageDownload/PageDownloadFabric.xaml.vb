Public Class PageDownloadFabric

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, DlFabricListLoader, AddressOf Load_OnFinish)
    End Sub
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub

    Private Sub Load_OnFinish()
        '结果数据化
        Try
            Dim Versions As JArray = DlFabricListLoader.Output.Value("installer")
            PanVersions.Children.Clear()
            For Each Version In Versions
                PanVersions.Children.Add(FabricDownloadListItem(Version, AddressOf Fabric_Selected))
            Next
            CardVersions.Title = GetLang("LangDownloadFabricVersionList") & " (" & Versions.Count & ")"
        Catch ex As Exception
            Log(ex, "可视化 Fabric 版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    Private Sub Fabric_Selected(sender As MyListItem, e As EventArgs)
        McDownloadFabricLoaderSave(sender.Tag)
    End Sub

    Private Sub BtnWeb_Click(sender As Object, e As EventArgs) Handles BtnWeb.Click
        OpenWebsite("https://www.fabricmc.net")
    End Sub

End Class
