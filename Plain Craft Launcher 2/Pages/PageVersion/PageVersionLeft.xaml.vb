Public Class PageVersionLeft
    Implements IRefreshable

    ''' <summary>
    ''' 当前显示设置的 MC 版本。
    ''' </summary>
    Public Shared Version As McVersion = Nothing

    Public Sub RefreshModDisabled() Handles Me.Loaded
        If Version IsNot Nothing AndAlso Version.Modable Then
            ItemMod.Visibility = Visibility.Visible
            ItemModDisabled.Visibility = Visibility.Collapsed
        Else
            ItemMod.Visibility = Visibility.Collapsed
            ItemModDisabled.Visibility = Visibility.Visible
        End If
    End Sub

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。从 0 开始计算。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.Default

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As RouteEventArgs) Handles ItemOverall.Check, ItemMod.Check, ItemModDisabled.Check, ItemSetup.Check, ItemExport.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.VersionOverall
                If FrmVersionOverall Is Nothing Then FrmVersionOverall = New PageVersionOverall
                Return FrmVersionOverall
            Case FormMain.PageSubType.VersionMod
                If FrmVersionMod Is Nothing Then FrmVersionMod = New PageVersionMod
                Return FrmVersionMod
            Case FormMain.PageSubType.VersionModDisabled
                If FrmVersionModDisabled Is Nothing Then FrmVersionModDisabled = New PageVersionModDisabled
                Return FrmVersionModDisabled
            Case FormMain.PageSubType.VersionSetup
                If IsNothing(FrmVersionSetup) Then FrmVersionSetup = New PageVersionSetup
                Return FrmVersionSetup
            Case FormMain.PageSubType.VersionExport
                If FrmVersionExport Is Nothing Then FrmVersionExport = New PageVersionExport
                Return FrmVersionExport
            Case Else
                Throw New Exception("未知的版本设置子页面种类：" & ID)
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
            AaCode(Sub()
                       CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                       FrmMain.PanMainRight.Child = FrmMain.PageRight
                       FrmMain.PageRight.Opacity = 0
                   End Sub, 130),
            AaCode(Sub()
                       '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                       FrmMain.PageRight.Opacity = 1
                       FrmMain.PageRight.PageOnEnter()
                   End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

#End Region

    Public Sub Refresh(sender As Object, e As EventArgs) '由边栏按钮匿名调用
        Refresh(Val(sender.Tag))
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        Refresh(FrmMain.PageCurrentSub)
    End Sub
    Public Sub Refresh(SubType As FormMain.PageSubType)
        Select Case SubType
            Case FormMain.PageSubType.VersionMod
                PageVersionMod.Refresh()
                ItemMod.Checked = True
            Case FormMain.PageSubType.VersionExport
                If FrmVersionExport IsNot Nothing Then FrmVersionExport.RefreshAll()
                ItemExport.Checked = True
        End Select
    End Sub

    Public Sub Reset(sender As Object, e As EventArgs)
        If MyMsgBox("是否要初始化该版本的版本独立设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
            If IsNothing(FrmVersionSetup) Then FrmVersionSetup = New PageVersionSetup
            FrmVersionSetup.Reset()
            ItemSetup.Checked = True
        End If
    End Sub

End Class
