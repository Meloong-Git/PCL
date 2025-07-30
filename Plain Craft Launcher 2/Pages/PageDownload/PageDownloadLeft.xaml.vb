Public Class PageDownloadLeft
    Implements IRefreshable

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.DownloadInstall

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As RouteEventArgs) Handles ItemInstall.Check, ItemClient.Check, ItemOptiFine.Check, ItemForge.Check, ItemNeoForge.Check, ItemLiteLoader.Check, ItemMod.Check, ItemFabric.Check, ItemPack.Check, ItemResourcePack.Check, ItemShader.Check, ItemDataPack.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.DownloadInstall
                If FrmDownloadInstall Is Nothing Then FrmDownloadInstall = New PageDownloadInstall
                Return FrmDownloadInstall
            Case FormMain.PageSubType.DownloadClient
                If FrmDownloadClient Is Nothing Then FrmDownloadClient = New PageDownloadClient
                Return FrmDownloadClient
            Case FormMain.PageSubType.DownloadOptiFine
                If FrmDownloadOptiFine Is Nothing Then FrmDownloadOptiFine = New PageDownloadOptiFine
                Return FrmDownloadOptiFine
            Case FormMain.PageSubType.DownloadForge
                If FrmDownloadForge Is Nothing Then FrmDownloadForge = New PageDownloadForge
                Return FrmDownloadForge
            Case FormMain.PageSubType.DownloadNeoForge
                If FrmDownloadNeoForge Is Nothing Then FrmDownloadNeoForge = New PageDownloadNeoForge
                Return FrmDownloadNeoForge
            Case FormMain.PageSubType.DownloadLiteLoader
                If FrmDownloadLiteLoader Is Nothing Then FrmDownloadLiteLoader = New PageDownloadLiteLoader
                Return FrmDownloadLiteLoader
            Case FormMain.PageSubType.DownloadFabric
                If FrmDownloadFabric Is Nothing Then FrmDownloadFabric = New PageDownloadFabric
                Return FrmDownloadFabric
            Case FormMain.PageSubType.DownloadMod
                If FrmDownloadMod Is Nothing Then FrmDownloadMod = New PageDownloadMod
                Return FrmDownloadMod
            Case FormMain.PageSubType.DownloadPack
                If FrmDownloadPack Is Nothing Then FrmDownloadPack = New PageDownloadPack
                Return FrmDownloadPack
            Case FormMain.PageSubType.DownloadResourcePack
                If FrmDownloadResourcePack Is Nothing Then FrmDownloadResourcePack = New PageDownloadResourcePack
                Return FrmDownloadResourcePack
            Case FormMain.PageSubType.DownloadShader
                If FrmDownloadShader Is Nothing Then FrmDownloadShader = New PageDownloadShader
                Return FrmDownloadShader
            Case FormMain.PageSubType.DownloadDataPack
                If FrmDownloadDataPack Is Nothing Then FrmDownloadDataPack = New PageDownloadDataPack
                Return FrmDownloadDataPack
            Case Else
                Throw New Exception("未知的下载子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
        Catch ex As Exception
            Log(ex, "切换分页面失败（ID " & ID & "）", LogLevel.Feedback)
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") '停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(
            Sub()
                CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                FrmMain.PanMainRight.Child = FrmMain.PageRight
                FrmMain.PageRight.Opacity = 0
            End Sub, 130),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                FrmMain.PageRight.Opacity = 1
                FrmMain.PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

#End Region

    '强制刷新
    Public Sub Refresh(sender As Object, e As EventArgs) '由边栏按钮匿名调用
        Refresh(Val(sender.Tag))
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        Refresh(FrmMain.PageCurrentSub)
    End Sub
    Public Sub Refresh(SubType As FormMain.PageSubType)
        Select Case SubType
            Case FormMain.PageSubType.DownloadInstall
                DlClientListLoader.Start(IsForceRestart:=True)
                DlOptiFineListLoader.Start(IsForceRestart:=True)
                DlForgeListLoader.Start(IsForceRestart:=True)
                DlNeoForgeListLoader.Start(IsForceRestart:=True)
                DlLiteLoaderListLoader.Start(IsForceRestart:=True)
                DlFabricListLoader.Start(IsForceRestart:=True)
                DlFabricApiLoader.Start(IsForceRestart:=True)
                DlOptiFabricLoader.Start(IsForceRestart:=True)
                ItemInstall.Checked = True
            Case FormMain.PageSubType.DownloadMod
                CompProjectCache.Clear()
                CompFilesCache.Clear()
                If FrmDownloadMod IsNot Nothing Then
                    FrmDownloadMod.Content.Storage = New CompProjectStorage
                    FrmDownloadMod.Content.Page = 0
                    FrmDownloadMod.PageLoaderRestart()
                End If
                ItemMod.Checked = True
            Case FormMain.PageSubType.DownloadPack
                CompProjectCache.Clear()
                CompFilesCache.Clear()
                If FrmDownloadPack IsNot Nothing Then
                    FrmDownloadPack.Content.Storage = New CompProjectStorage
                    FrmDownloadPack.Content.Page = 0
                    FrmDownloadPack.PageLoaderRestart()
                End If
                ItemPack.Checked = True
            Case FormMain.PageSubType.DownloadResourcePack
                CompProjectCache.Clear()
                CompFilesCache.Clear()
                If FrmDownloadResourcePack IsNot Nothing Then
                    FrmDownloadResourcePack.Content.Storage = New CompProjectStorage
                    FrmDownloadResourcePack.Content.Page = 0
                    FrmDownloadResourcePack.PageLoaderRestart()
                End If
                ItemResourcePack.Checked = True
            Case FormMain.PageSubType.DownloadShader
                CompProjectCache.Clear()
                CompFilesCache.Clear()
                If FrmDownloadShader IsNot Nothing Then
                    FrmDownloadShader.Content.Storage = New CompProjectStorage
                    FrmDownloadShader.Content.Page = 0
                    FrmDownloadShader.PageLoaderRestart()
                End If
                ItemShader.Checked = True
            Case FormMain.PageSubType.DownloadDataPack
                CompProjectCache.Clear()
                CompFilesCache.Clear()
                If FrmDownloadDataPack IsNot Nothing Then
                    FrmDownloadDataPack.Content.Storage = New CompProjectStorage
                    FrmDownloadDataPack.Content.Page = 0
                    FrmDownloadDataPack.PageLoaderRestart()
                End If
                ItemDataPack.Checked = True
            Case FormMain.PageSubType.DownloadClient
                DlClientListLoader.Start(IsForceRestart:=True)
                ItemClient.Checked = True
            Case FormMain.PageSubType.DownloadOptiFine
                DlOptiFineListLoader.Start(IsForceRestart:=True)
                ItemOptiFine.Checked = True
            Case FormMain.PageSubType.DownloadForge
                DlForgeListLoader.Start(IsForceRestart:=True)
                ItemForge.Checked = True
            Case FormMain.PageSubType.DownloadNeoForge
                DlNeoForgeListLoader.Start(IsForceRestart:=True)
                ItemNeoForge.Checked = True
            Case FormMain.PageSubType.DownloadLiteLoader
                DlLiteLoaderListLoader.Start(IsForceRestart:=True)
                ItemLiteLoader.Checked = True
            Case FormMain.PageSubType.DownloadFabric
                DlFabricListLoader.Start(IsForceRestart:=True)
                ItemFabric.Checked = True
        End Select
        Hint("正在刷新……", Log:=False)
    End Sub

    '点击返回
    Private Sub ItemInstall_Click(sender As Object, e As MouseButtonEventArgs) Handles ItemInstall.Click
        If Not ItemInstall.Checked Then Return
        FrmDownloadInstall.ExitSelectPage()
    End Sub

    '展开手动安装
    Private Sub ItemHand_Click(sender As Object, e As RouteEventArgs) Handles ItemHand.Changed
        If ItemHand.Checked = False Then Return
        e.Handled = True
        AniControlEnabled += 1
        If Not Setup.Get("HintHandInstall") Then
            Setup.Set("HintHandInstall", True)
            If MyMsgBox("手动安装包功能提供了 OptiFine、Forge 等组件的 .jar 安装文件下载，但无法自动安装。" & vbCrLf &
                        "在自动安装页面先选择 MC 版本，然后就可以选择 OptiFine、Forge 等组件，让 PCL 自动进行安装了。", "自动安装提示", "返回自动安装", "继续下载手动安装包") = 1 Then
                FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Download}, FormMain.PageSubType.DownloadInstall)
                AniControlEnabled -= 1
                Return
            End If
        End If
        ItemHand.Visibility = Visibility.Collapsed
        LabGame.Visibility = Visibility.Collapsed
        LabHand.Visibility = Visibility.Visible
        ItemClient.Visibility = Visibility.Visible
        ItemOptiFine.Visibility = Visibility.Visible
        ItemFabric.Visibility = Visibility.Visible
        ItemForge.Visibility = Visibility.Visible
        ItemNeoForge.Visibility = Visibility.Visible
        ItemLiteLoader.Visibility = Visibility.Visible
        RunInThread(
        Sub()
            Thread.Sleep(20)
            RunInUiWait(Sub() ItemClient.SetChecked(True, True, True))
            AniControlEnabled -= 1
        End Sub)
    End Sub
    '折叠手动安装
    Private Sub LabHand_Click(sender As Object, e As MouseButtonEventArgs) Handles LabHand.MouseLeftButtonUp
        e.Handled = True
        AniControlEnabled += 1
        ItemHand.Visibility = Visibility.Visible
        LabGame.Visibility = Visibility.Visible
        LabHand.Visibility = Visibility.Collapsed
        ItemClient.Visibility = Visibility.Collapsed
        ItemOptiFine.Visibility = Visibility.Collapsed
        ItemNeoForge.Visibility = Visibility.Collapsed
        ItemFabric.Visibility = Visibility.Collapsed
        ItemForge.Visibility = Visibility.Collapsed
        ItemLiteLoader.Visibility = Visibility.Collapsed
        RunInThread(
        Sub()
            Thread.Sleep(20)
            RunInUiWait(Sub() ItemInstall.SetChecked(True, True, True))
            AniControlEnabled -= 1
        End Sub)
    End Sub

End Class
