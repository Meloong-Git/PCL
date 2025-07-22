Public Class PageOtherLeft

    Private IsLoad As Boolean = False
    Private IsPageSwitched As Boolean = False '如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次
    Private Sub PageOtherLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '是否处于隐藏的子页面
        Dim IsHiddenPage As Boolean = False
        If ItemHelp.Checked AndAlso Setup.Get("UiHiddenOtherHelp") Then IsHiddenPage = True
        If ItemAbout.Checked AndAlso Setup.Get("UiHiddenOtherAbout") Then IsHiddenPage = True
        If ItemTest.Checked AndAlso Setup.Get("UiHiddenOtherTest") Then IsHiddenPage = True
        If PageSetupUI.HiddenForceShow Then IsHiddenPage = False
        '若页面错误，或尚未加载，则继续
        If IsLoad AndAlso Not IsHiddenPage Then Return
        IsLoad = True
        '刷新子页面隐藏情况
        PageSetupUI.HiddenRefresh()
        '选择第一个未被禁用的子页面
        If IsPageSwitched Then Return
        If Not Setup.Get("UiHiddenOtherHelp") Then
            ItemHelp.SetChecked(True, False, False)
        ElseIf Not Setup.Get("UiHiddenOtherAbout") Then
            ItemAbout.SetChecked(True, False, False)
        Else
            ItemTest.SetChecked(True, False, False)
        End If
    End Sub
    Private Sub PageOtherLeft_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        IsPageSwitched = False
    End Sub

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。从 0 开始计算。
    ''' </summary>
    Public PageID As FormMain.PageSubType
    Public Sub New()
        InitializeComponent()
        '选择第一个未被禁用的子页面
        If Not Setup.Get("UiHiddenOtherHelp") Then
            PageID = FormMain.PageSubType.OtherHelp
        ElseIf Not Setup.Get("UiHiddenOtherAbout") Then
            PageID = FormMain.PageSubType.OtherAbout
        Else
            PageID = FormMain.PageSubType.OtherTest
        End If
    End Sub

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As RouteEventArgs) Handles ItemAbout.Check, ItemHelp.Check, ItemTest.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.OtherHelp
                If FrmOtherHelp Is Nothing Then FrmOtherHelp = New PageOtherHelp
                Return FrmOtherHelp
            Case FormMain.PageSubType.OtherAbout
                If FrmOtherAbout Is Nothing Then FrmOtherAbout = New PageOtherAbout
                Return FrmOtherAbout
            Case FormMain.PageSubType.OtherTest
                If FrmOtherTest Is Nothing Then FrmOtherTest = New PageOtherTest
                Return FrmOtherTest
            Case Else
                Throw New Exception("未知的更多子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        IsPageSwitched = True
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

    '强制刷新
    Public Sub Refresh(sender As Object, e As EventArgs) '由边栏按钮匿名调用
        Select Case Val(sender.Tag)
            Case FormMain.PageSubType.OtherHelp
                RefreshHelp()
                ItemHelp.Checked = True
        End Select
        Hint("正在刷新……", Log:=False)
    End Sub
    Public Shared Sub RefreshHelp()
        Setup.Set("SystemHelpVersion", 0) '强制重新解压文件
        FrmOtherHelp.PageLoaderRestart()
        FrmOtherHelp.SearchBox.Text = ""
    End Sub

    '打开网页
    Private Sub TryFeedback(sender As Object, e As RouteEventArgs) Handles ItemFeedback.Changed
        If Not ItemFeedback.Checked Then Return
        TryFeedback()
        e.Handled = True
    End Sub
    Public Shared Sub TryFeedback()
        If Not CanFeedback(True) Then Return
        Select Case MyMsgBox("在提交新反馈前，建议先搜索反馈列表，以避免重复提交。" & vbCrLf & "如果无法打开该网页，请使用 VPN 以改善网络环境。",
                    "反馈", "提交新反馈", "查看反馈列表", "取消")
            Case 1
                Feedback(True, False)
            Case 2
                OpenWebsite("https://github.com/Meloong-Git/PCL/issues/")
        End Select
    End Sub
    Private Sub TryVote(sender As Object, e As RouteEventArgs) Handles ItemVote.Changed
        If Not ItemVote.Checked Then Return
        TryVote()
        e.Handled = True
    End Sub
    Public Shared Sub TryVote()
        If MyMsgBox("是否要打开新功能投票网页？" & vbCrLf & "如果无法打开该网页，请使用 VPN 以改善网络环境。",
                    "新功能投票", "打开", "取消") = 2 Then Return
        OpenWebsite("https://github.com/Meloong-Git/PCL/discussions/categories/%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8?discussions_q=category%3A%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8+sort%3Adate_created")
    End Sub

End Class
