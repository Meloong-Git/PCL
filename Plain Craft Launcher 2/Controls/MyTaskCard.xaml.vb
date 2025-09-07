Imports System.Collections.ObjectModel
Imports System.ComponentModel

Public Class MyTaskCard
    Inherits MyCard

    ''' <summary>
    ''' 卡片中每一条子下载任务的数据模型，将Loader的Uuid作为唯一标识符
    ''' </summary>
    Public Class MySubTaskEntry
        Implements INotifyPropertyChanged

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

        Public Sub New(Loader As LoaderBase)
            LoaderUuid = Loader.Uuid
            _LoaderState = Loader.State
            _Progress = Loader.Progress
            _Descreption = Loader.Name
        End Sub

        ''' <summary>
        ''' 检查值有无改变以及通知前端
        ''' </summary>
        Public Sub SyncValuesToUI(Loader As LoaderBase)
            If (Not Loader.State = _LoaderState) Then
                _LoaderState = Loader.State
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs("LoaderStateUIElement"))
            End If
            If (Not Loader.Progress = _Progress) Then
                _Progress = Loader.Progress
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs("PercentStr"))
            End If
        End Sub

        ''' <summary>
        ''' 使用Loader的Uuid作为唯一标识符
        ''' </summary>
        Public LoaderUuid As Integer

        Private _LoaderState As LoadState
        ''' <summary>
        ''' 把LoaderState转换为UI元素显示到前端
        ''' </summary>
        Public ReadOnly Property LoaderStateUIElement As FrameworkElement
            Get
                Select Case _LoaderState
                    Case LoadState.Waiting
                        Dim Ret As New Shapes.Path With {
                            .Stretch = Stretch.Uniform,
                            .Data = Geometry.Parse("F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z"),
                            .Width = 18, .Height = 6,
                            .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Top,
                            .Margin = New Thickness(0, 7, 0, 0)
                        }
                        Ret.SetResourceReference(Shape.FillProperty, "ColorBrush3")
                        Return Ret
                    Case LoadState.Loading
                        Dim Ret As New TextBlock With {
                            .HorizontalAlignment = HorizontalAlignment.Center
                        }
                        Ret.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                        Ret.SetBinding(TextBlock.TextProperty, New Binding("PercentStr") With {.Mode = BindingMode.OneWay})
                        Return Ret
                    Case LoadState.Finished
                        Dim Ret As New Shapes.Path With {
                            .Stretch = Stretch.Uniform,
                            .Data = Geometry.Parse("F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z"),
                            .Width = 16, .Height = 15,
                            .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Top,
                            .Margin = New Thickness(0, 3, 0, 0)
                        }
                        Ret.SetResourceReference(Shape.FillProperty, "ColorBrush3")
                        Return Ret
                    Case Else 'Failed, Aborted
                        Dim Ret As New Shapes.Path With {
                            .Stretch = Stretch.Uniform,
                            .Data = Geometry.Parse("F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z"),
                            .Width = 15, .Height = 15,
                            .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Top,
                            .Margin = New Thickness(0, 1, 0, 0)
                        }
                        Ret.SetResourceReference(Shape.FillProperty, "ColorBrush3")
                        Return Ret
                End Select
            End Get
        End Property

        Private _Progress As Double
        Public ReadOnly Property PercentStr As String
            Get
                Return Math.Floor(_Progress * 100) & "%"
            End Get
        End Property

        Private _Descreption As String
        Public ReadOnly Property Descreption As String
            Get
                Return _Descreption
            End Get
        End Property
    End Class

    ''' <summary>
    ''' TasksList的数据源
    ''' </summary>
    Public ReadOnly Property TaskEntries As New ObservableCollection(Of MySubTaskEntry)

    ''' <summary>
    ''' 使用LoaderCombo的Uuid作为唯一标识符
    ''' </summary>
    Public LoaderUuid As Integer

    ''' <summary>
    ''' 获取所有子下载任务
    ''' </summary>
    Private Function GetSubTasks(Loader As Object) As List(Of LoaderBase)
        Return Loader.GetLoaderList()
    End Function

    ''' <summary>
    ''' 是否已经失败
    ''' </summary>
    Private IsFailed As Boolean

    Public Sub New(Loader As LoaderBase)
        InitializeComponent()
        LoaderUuid = Loader.Uuid
        Title = Loader.Name
        RefreshSubTasks(Loader)
        TasksList.ItemsSource = TaskEntries
    End Sub

    ''' <summary>
    ''' 同步前端状态（是否已失败、初次调用时添加子任务显示条目、刷新子任务显示条目的信息）
    ''' </summary>
    Public Sub RefreshSubTasks(Loader As LoaderBase)
        If IsFailed Then Exit Sub
        Try
            If Loader.State = LoadState.Failed Then
                IsFailed = True
                ExceptionHint.Text = GetExceptionDetail(Loader.Error)
                ExceptionHint.Visibility = Visibility.Visible
                TasksList.Visibility = Visibility.Collapsed
            Else
                For Each SubTask As LoaderBase In GetSubTasks(Loader)
                    Dim TaskEntry = TaskEntries.FirstOrDefault(Function(t) t.LoaderUuid = SubTask.Uuid)
                    If TaskEntry Is Nothing Then '除了第一次调用之外不会进入这个case，因为LoaderCombo的子加载任务不会增加
                        TaskEntries.Add(New MySubTaskEntry(SubTask))
                    Else
                        TaskEntry.SyncValuesToUI(SubTask)
                    End If
                Next
            End If
        Catch ex As Exception
            Log(ex, "刷新下载管理显示失败", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 点击失败提示卡片之后复制错误信息到剪贴板
    ''' </summary>
    Private Sub CopyExceptionDetail(sender As MyHint, e As EventArgs) Handles ExceptionHint.MouseLeftButtonDown
        ClipboardSet(sender.Text, False)
        Hint("已复制错误详情！", HintType.Finish)
    End Sub

    ''' <summary>
    ''' 点击取消按钮之后播放关闭动画、中止Loader
    ''' </summary>
    Private Sub Cancel(sender As MyIconButton, e As EventArgs) Handles BtnCancel.Click
        Log($"[UI] 通过任务列表页面点击按钮中止任务：{Title}")
        AniDispose(sender, False)
        AniDispose(Me, True, Sub() FrmSpeedRight?.TryReturnToHome())
        RunInThread(
            Sub()
                For Each Loader As LoaderBase In LoaderTaskbar.Where(Function(lo) LoaderUuid = lo.Uuid).ToList()
                    Loader.Abort()
                    LoaderTaskbar.Remove(Loader)
                Next
            End Sub)
    End Sub

End Class
