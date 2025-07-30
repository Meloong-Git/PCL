Public Class PageSetupLeft

    Private IsLoad As Boolean = False
    Private IsPageSwitched As Boolean = False '如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次
    Private Sub PageSetupLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '是否处于隐藏的子页面
        Dim IsHiddenPage As Boolean = False
        If ItemLaunch.Checked AndAlso Setup.Get("UiHiddenSetupLaunch") Then IsHiddenPage = True
        If ItemUI.Checked AndAlso Setup.Get("UiHiddenSetupUi") Then IsHiddenPage = True
        If ItemSystem.Checked AndAlso Setup.Get("UiHiddenSetupSystem") Then IsHiddenPage = True
        If ItemLink.Checked AndAlso Setup.Get("UiHiddenSetupLink") Then IsHiddenPage = True
        If PageSetupUI.HiddenForceShow Then IsHiddenPage = False
        '若页面错误，或尚未加载，则继续
        If IsLoad AndAlso Not IsHiddenPage Then Return
        IsLoad = True
        '刷新子页面隐藏情况
        PageSetupUI.HiddenRefresh()
        '选择第一个未被禁用的子页面
        If IsPageSwitched Then Return
        If Not Setup.Get("UiHiddenSetupLaunch") Then
            ItemLaunch.SetChecked(True, False, False)
        ElseIf Not Setup.Get("UiHiddenSetupUi") Then
            ItemUI.SetChecked(True, False, False)
        ElseIf Not Setup.Get("UiHiddenSetupSystem") Then
            ItemSystem.SetChecked(True, False, False)
        ElseIf Not Setup.Get("UiHiddenSetupLink") Then
            ItemLink.SetChecked(True, False, False)
        Else
            ItemLaunch.SetChecked(True, False, False)
        End If
    End Sub
    Private Sub PageOtherLeft_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        IsPageSwitched = False
    End Sub

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。从左往右从 0 开始计算。
    ''' </summary>
    Public PageID As FormMain.PageSubType
    Public Sub New()
        InitializeComponent()
        '选择第一个未被禁用的子页面
        If Not Setup.Get("UiHiddenSetupLaunch") Then
            PageID = FormMain.PageSubType.SetupLaunch
        ElseIf Not Setup.Get("UiHiddenSetupUi") Then
            PageID = FormMain.PageSubType.SetupUI
        ElseIf Not Setup.Get("UiHiddenSetupSystem") Then
            PageID = FormMain.PageSubType.SetupSystem
        ElseIf Not Setup.Get("UiHiddenSetupLink") Then
            PageID = FormMain.PageSubType.SetupLink
        Else
            PageID = FormMain.PageSubType.SetupLaunch
        End If
    End Sub

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As EventArgs) Handles ItemLaunch.Check, ItemSystem.Check, ItemUI.Check, ItemLink.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会跳过切换，且由于 PageID 默认为 0 而切换到第一个页面
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    ''' <summary>
    ''' 获取当前导航指定的右页面。
    ''' </summary>
    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.SetupLaunch
                If FrmSetupLaunch Is Nothing Then FrmSetupLaunch = New PageSetupLaunch
                Return FrmSetupLaunch
            Case FormMain.PageSubType.SetupUI
                If FrmSetupUI Is Nothing Then FrmSetupUI = New PageSetupUI
                Return FrmSetupUI
            Case FormMain.PageSubType.SetupLink
                If FrmSetupLink Is Nothing Then FrmSetupLink = New PageSetupLink
                Return FrmSetupLink
            Case FormMain.PageSubType.SetupSystem
                If FrmSetupSystem Is Nothing Then FrmSetupSystem = New PageSetupSystem
                Return FrmSetupSystem
            Case Else
                Throw New Exception("未知的设置子页面种类：" & ID)
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

            Select Case ID
                Case FormMain.PageSubType.SetupLaunch
                    If IsNothing(FrmSetupLaunch) Then FrmSetupLaunch = New PageSetupLaunch
                    PageChangeRun(FrmSetupLaunch)
                Case FormMain.PageSubType.SetupUI
                    If IsNothing(FrmSetupUI) Then FrmSetupUI = New PageSetupUI
                    PageChangeRun(FrmSetupUI)
                Case FormMain.PageSubType.SetupLink
                    If IsNothing(FrmSetupLink) Then FrmSetupLink = New PageSetupLink
                    PageChangeRun(FrmSetupLink)
                Case FormMain.PageSubType.SetupSystem
                    If IsNothing(FrmSetupSystem) Then FrmSetupSystem = New PageSetupSystem
                    PageChangeRun(FrmSetupSystem)
                Case Else
                    Throw New Exception("未知的设置子页面种类：" & ID)
            End Select

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

    Public Sub Reset(sender As Object, e As EventArgs)
        Select Case Val(sender.Tag)
            Case FormMain.PageSubType.SetupLaunch
                If MyMsgBox("是否要初始化 启动 页面的所有设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
                    If IsNothing(FrmSetupLaunch) Then FrmSetupLaunch = New PageSetupLaunch
                    FrmSetupLaunch.Reset()
                    ItemLaunch.Checked = True
                End If
            Case FormMain.PageSubType.SetupUI
                If MyMsgBox("是否要初始化 个性化 页面的所有设置？该操作不可撤销。" & vbCrLf & "（背景图片与音乐、主页等外部文件不会被删除）", "初始化确认",, "取消", IsWarn:=True) = 1 Then
                    If IsNothing(FrmSetupUI) Then FrmSetupUI = New PageSetupUI
                    FrmSetupUI.Reset()
                    ItemUI.Checked = True
                End If
            Case FormMain.PageSubType.SetupSystem
                If MyMsgBox("是否要初始化 其他 页面的所有设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
                    If IsNothing(FrmSetupSystem) Then FrmSetupSystem = New PageSetupSystem
                    FrmSetupSystem.Reset()
                    ItemSystem.Checked = True
                End If
            Case FormMain.PageSubType.SetupLink
                If MyMsgBox("是否要初始化 联机 页面的所有设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
                    If IsNothing(FrmSetupLink) Then FrmSetupLink = New PageSetupLink
                    FrmSetupLink.Reset()
                    ItemLink.Checked = True
                End If
        End Select
    End Sub

End Class
