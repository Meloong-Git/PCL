Imports System.Threading.Tasks

Public Class PageSpeedLeft
    Private Const WatcherInterval As Integer = 300

    '初始化
    Private IsLoad As Boolean = False
    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '进入时就刷新一次显示
        Watcher()

        If IsLoad Then Return
        IsLoad = True

        '监控定时器
        Dim timer As New Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 0, WatcherInterval)}
        AddHandler timer.Tick,
            Sub()
                If FrmMain.PageCurrent = FormMain.PageType.DownloadManager Then
                    Watcher()
                    FrmSpeedRight?.Watcher()
                End If
            End Sub
        Log("[UI] 任务列表页面监控定时器启动")
        timer.Start()

        '非调试模式隐藏线程数
        If Not ModeDebug Then
            RowDefinitions(12).Height = New GridLength(0)
            RowDefinitions(13).Height = New GridLength(0)
            RowDefinitions(14).Height = New GridLength(0)
            RowDefinitions(15).Height = New GridLength(0)
        End If

    End Sub

    '定时器任务
    Private ReadOnly RightCards As New Dictionary(Of String, MyCard)
    Private Sub Watcher()
        Try
            If Not LoaderTaskbar.Any() Then
                '无任务
                LabProgress.Text = "100 %"
                LabSpeed.Text = "0 B/s"
                LabFile.Text = "0"
                LabThread.Text = "0 / " & NetTaskThreadLimit
            Else
                '有任务，输出基本信息
                Dim Tasks = LoaderTaskbar.Where(Function(l) l.Show).ToList() '筛选掉启动 MC 的任务（#6270）
                Dim RawPercent As Double = If(Tasks.Any, MathClamp(Tasks.Select(Function(l) l.Progress).Average(), 0, 1), 1)
                Dim PredictText As String = Math.Floor(RawPercent * 100) & "." & StrFill(Math.Floor((RawPercent * 100 - Math.Floor(RawPercent * 100)) * 100), "0", 2) & " %"
                LabProgress.Text = If(RawPercent > 0.999999, "100 %", PredictText)
                LabSpeed.Text = GetString(NetManager.Speed) & "/s"
                LabFile.Text = If(NetManager.FileRemain < 0, "0*", NetManager.FileRemain)
                LabThread.Text = NetTaskThreadCount & " / " & NetTaskThreadLimit
            End If
        Catch ex As Exception
            Log(ex, "下载管理左栏监视出错", LogLevel.Feedback)
        End Try
    End Sub

End Class
