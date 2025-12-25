Public Class PageLogRight
    Public Sub Init() Handles Me.Initialized
        PanLogCard.Inlines.Clear()
        PanLogCard.Inlines.Add(New Run("实时日志"))
        PanLogCard.Inlines.Add(New Run(" | "))
        LabDebug = New Run("0 Debug") With {.Foreground = Application.Current.Resources("ColorBrushDebug")}
        PanLogCard.Inlines.Add(LabDebug)
        PanLogCard.Inlines.Add(New Run(" | "))
        LabInfo = New Run("0 Info") With {.Foreground = Application.Current.Resources("ColorBrushInfo")}
        PanLogCard.Inlines.Add(LabInfo)
        PanLogCard.Inlines.Add(New Run(" | "))
        LabWarn = New Run("0 Warn") With {.Foreground = Application.Current.Resources("ColorBrushWarn")}
        PanLogCard.Inlines.Add(LabWarn)
        PanLogCard.Inlines.Add(New Run(" | "))
        LabError = New Run("0 Error") With {.Foreground = Application.Current.Resources("ColorBrushError")}
        PanLogCard.Inlines.Add(LabError)
        PanLogCard.Inlines.Add(New Run(" | "))
        LabFatal = New Run("0 Fatal") With {.Foreground = Application.Current.Resources("ColorBrushFatal")}
        PanLogCard.Inlines.Add(LabFatal)
    End Sub
    Public Sub Refresh()
        '初始化
        If FrmLogLeft.CurrentLog Is Nothing OrElse FrmLogLeft.CurrentUuid <= 0 OrElse FrmLogLeft.ShownLogs.Count = 0 Then
            FrmMain.PageChange(FrmMain.PageCurrent)
            Return
        End If
        PanAllBack.Visibility = Visibility.Visible
        CardOperation.Visibility = Visibility.Visible
        BtnOperationKill.IsEnabled = Not FrmLogLeft.CurrentLog.GameProcess.HasExited
        '绑定日志输出
        PanLog.Document = FrmLogLeft.FlowDocuments(FrmLogLeft.CurrentUuid)
        '绑定事件
        AddHandler FrmLogLeft.CurrentLog.LogOutput, AddressOf OnLogOutput
        AddHandler FrmLogLeft.CurrentLog.GameExit, AddressOf OnGameExit
        RefreshLabText()
    End Sub

    Private Sub RefreshLabText()
        '刷新计数器
        LabFatal.Text = $"{FrmLogLeft.CurrentLog.CountFatal} Fatal"
        LabError.Text = $"{FrmLogLeft.CurrentLog.CountError} Error"
        LabWarn.Text = $"{FrmLogLeft.CurrentLog.CountWarn} Warn"
        LabInfo.Text = $"{FrmLogLeft.CurrentLog.CountInfo} Info"
        LabDebug.Text = $"{FrmLogLeft.CurrentLog.CountDebug} Debug"
    End Sub

    Public LabDebug As Run = Nothing
    Public LabInfo As Run = Nothing
    Public LabWarn As Run = Nothing
    Public LabError As Run = Nothing
    Public LabFatal As Run = Nothing

    Private Sub PageLogRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Refresh()
    End Sub
    Private Sub OnLogOutput(sender As Watcher, e As LogOutputEventArgs)
        RunInUi(Sub()
                    If FrmLogLeft.CurrentLog IsNot Nothing Then
                        If CheckAutoScroll.Checked Then PanBack.ScrollToBottom()
                        RefreshLabText()
                    End If
                End Sub)
    End Sub

#Region "卡片按钮"
    Private Sub BtnOperationClear_Click(sender As Object, e As RouteEventArgs) Handles BtnOperationClear.Click
        FrmLogLeft.FlowDocuments(FrmLogLeft.CurrentUuid).Blocks.Clear()
    End Sub

    Private Sub BtnOperationExport_Click(sender As Object, e As RouteEventArgs) Handles BtnOperationExport.Click
        'TODO(i18n): 文本 @ 文件选择弹窗 - 窗口标题 & 类型选择器选项
        Dim SavePath As String = SelectSaveFile("选择导出位置", $"游戏日志 - {FrmLogLeft.CurrentLog.Version.Name}.log", "游戏日志(*.log)|*.log")
        If SavePath.Length < 3 Then Exit Sub
        File.WriteAllLines(SavePath, FrmLogLeft.CurrentLog.FullLog)
        'TODO(i18n): 文本 @ 左下角提示 - 导出成功提示
        Hint("日志已导出！", HintType.Finish)
        OpenExplorer($"/select,""{SavePath}""")
    End Sub

    Private Sub BtnOperationKill_Click(sender As Object, e As RouteEventArgs) Handles BtnOperationKill.Click
        If FrmLogLeft.CurrentLog.State <= Watcher.MinecraftState.Running Then
            FrmLogLeft.CurrentLog.Kill()
            'TODO(i18n): 文本 @ 左下角提示 - 客户端关闭提示
            Hint($"已关闭游戏 {FrmLogLeft.CurrentLog.Version.Name}！", HintType.Finish)
        End If
    End Sub

    Private Sub OnGameExit()
        RunInUi(Sub() BtnOperationKill.IsEnabled = False)
    End Sub
#End Region

End Class