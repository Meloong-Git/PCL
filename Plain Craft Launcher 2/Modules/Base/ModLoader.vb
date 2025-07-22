Public Module ModLoader

    '各类加载器
    ''' <summary>
    ''' 加载器的统一基类。
    ''' </summary>
    Public MustInherit Class LoaderBase
        Implements ILoadingTrigger

        Public ReadOnly Property IsLoader As Boolean = True Implements ILoadingTrigger.IsLoader

        '基础属性
        ''' <summary>
        ''' 加载器的标识编号。
        ''' </summary>
        Public Uuid As Integer = GetUuid()
        ''' <summary>
        ''' 加载器的名称。
        ''' </summary>
        Public Name As String = "未命名任务 " & Uuid & "#"
        ''' <summary>
        ''' 用于状态改变检测的同步锁。
        ''' </summary>
        Public ReadOnly LockState As New Object
        ''' <summary>
        ''' 父加载器。
        ''' </summary>
        Public Parent As LoaderBase = Nothing
        ''' <summary>
        ''' 最上级的加载器。
        ''' </summary>
        Public ReadOnly Property RealParent As LoaderBase
            Get
                Try
                    RealParent = Parent
                    While RealParent IsNot Nothing AndAlso RealParent.Parent IsNot Nothing
                        RealParent = RealParent.Parent
                    End While
                Catch ex As Exception
                    Log(ex, "获取父加载器失败（" & Name & "）", LogLevel.Feedback)
                    Return Nothing
                End Try
            End Get
        End Property

        Public Overridable Sub InitParent(Parent As LoaderBase)
            Me.Parent = Parent
        End Sub

        '事件

        ''' <summary>
        ''' 当状态改变时，在工作线程触发代码。在添加事件后，必须将 HasOnStateChangedThread 设为 True。
        ''' </summary>
        Public Event OnStateChangedThread(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        Public HasOnStateChangedThread As Boolean = False

        ''' <summary>
        ''' 当状态改变时，在 UI 线程触发代码。
        ''' </summary>
        Public Event OnStateChangedUi(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        ''' <summary>
        ''' 简易的在 UI 线程添加触发事件的方式。主要用于在新建 Loader 时直接使用 With 绑定事件，以及进行老代码兼容。
        ''' </summary>
        Public WriteOnly Property OnStateChanged As Action(Of LoaderBase)
            Set(value As Action(Of LoaderBase))
                AddHandler OnStateChangedUi, Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState) value(Loader)
            End Set
        End Property

        ''' <summary>
        ''' 在加载器目标事件执行完成，加载器状态即将变为 Finish 时调用。可以视为扩展加载器目标事件。
        ''' </summary>
        Public Event PreviewFinish(Loader As LoaderBase)
        Protected Sub RaisePreviewFinish()
            RaiseEvent PreviewFinish(Me)
        End Sub

        '状态监控
        ''' <summary>
        ''' 加载器的状态。
        ''' </summary>
        Public Property State As LoadState
            Get
                Return _State
            End Get
            Set(value As LoadState)
                If _State = value Then Return
                Dim OldState = _State
                If value = LoadState.Finished AndAlso Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomInteger(100, 2000))
                _State = value
                Log("[Loader] 加载器 " & Name & " 状态改变：" & GetStringFromEnum(value))
                '实现 ILoadingTrigger 接口与 OnStateChanged 回调
                RunInUi(
                Sub()
                    Select Case value
                        Case LoadState.Loading
                            LoadingState = MyLoading.MyLoadingState.Run
                        Case LoadState.Failed
                            LoadingState = MyLoading.MyLoadingState.Error
                        Case Else
                            LoadingState = MyLoading.MyLoadingState.Stop
                    End Select
                    RaiseEvent OnStateChangedUi(Me, value, OldState)
                End Sub)
                If HasOnStateChangedThread Then RunInThread(Sub() RaiseEvent OnStateChangedThread(Me, value, OldState))
            End Set
        End Property
        Private _State As LoadState = LoadState.Waiting
        Public Property LoadingState As MyLoading.MyLoadingState Implements ILoadingTrigger.LoadingState
            Get
                Return _LoadingState
            End Get
            Set(value As MyLoading.MyLoadingState)
                If _LoadingState = value Then Return
                Dim OldState = _LoadingState
                _LoadingState = value
                RaiseEvent LoadingStateChanged(value, OldState)
            End Set
        End Property
        Private _LoadingState As MyLoading.MyLoadingState = MyLoading.MyLoadingState.Stop
        Public Event LoadingStateChanged(NewState As MyLoading.MyLoadingState, OldState As MyLoading.MyLoadingState) Implements ILoadingTrigger.LoadingStateChanged
        ''' <summary>
        ''' 若加载器出错，可提供给外部参考的异常。
        ''' </summary>
        Public Property [Error] As Exception = Nothing
        ''' <summary>
        ''' 使用 LoaderCombo 加载时，该任务是否会阻碍后续任务的进行。
        ''' </summary>
        Public Block As Boolean = True
        ''' <summary>
        ''' 该加载器是否显示在列表中。
        ''' </summary>
        Public Show As Boolean = True
        ''' <summary>
        ''' 当前加载器是否由 IsForceRestart 强制调起。
        ''' 这个属性自身不会干任何事，而是提供给加载器执行的函数，使得加载器调用另一个加载器时，可以继承强制重启属性。
        ''' </summary>
        Public IsForceRestarting As Boolean = False

        '进度监控
        ''' <summary>
        ''' 加载器的执行进度，为 0 至 1 的小数。
        ''' </summary>
        Public Overridable Property Progress As Double
            Get
                Select Case State
                    Case LoadState.Waiting
                        Return 0
                    Case LoadState.Loading
                        Return If(_Progress = -1, 0.02, _Progress)
                    Case Else
                        Return 1
                End Select
            End Get
            Set(value As Double)
                If _Progress = value Then Return
                Dim OldValue = _Progress
                _Progress = value
                RaiseEvent ProgressChanged(value, OldValue)
            End Set
        End Property
        Private _Progress As Double = -1
        Public Event ProgressChanged(NewProgress As Double, OldProgress As Double) Implements ILoadingTrigger.ProgressChanged
        ''' <summary>
        ''' 计算总进度时的权重。它应该为预计时间（秒）。
        ''' </summary>
        Public Property ProgressWeight As Double = 1

        '状态变化
        Public MustOverride Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
        Public MustOverride Sub Abort()

        '等待结束
        Public Const WaitForExitTimeoutMessage As String = "等待加载器执行超时。"

        ''' <summary>
        ''' 无限期地等待加载器完成，直到结束或抛出异常。若加载器尚未开始，则会开始执行。
        ''' </summary>
        Public Sub WaitForExit(Optional Input As Object = Nothing, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional IsForceRestart As Boolean = False)
            Start(Input, IsForceRestart)
            Do While State = LoadState.Loading
                If LoaderToSyncProgress IsNot Nothing Then LoaderToSyncProgress.Progress = Progress
                Thread.Sleep(10)
            Loop
            If State = LoadState.Finished Then
                Return
            ElseIf State = LoadState.Aborted Then
                Throw New ThreadInterruptedException("加载器执行已中断。")
            ElseIf IsNothing([Error]) Then
                Throw New Exception("未知错误！")
            Else
                Throw New Exception([Error].Message, [Error]) '保留调用堆栈，同时不影响信息输出与单元测试
            End If
        End Sub
        ''' <summary>
        ''' 等待加载器完成，直到结束、抛出异常或超时。若加载器尚未开始，则会开始执行。
        ''' </summary>
        ''' <param name="Timeout">等待的超时时间，以毫秒为单位。</param>
        ''' <param name="TimeoutMessage">若执行超时，将会抛出的异常信息。</param>
        Public Sub WaitForExitTime(Timeout As Integer, Optional Input As Object = Nothing, Optional TimeoutMessage As String = WaitForExitTimeoutMessage, Optional LoaderToSyncProgress As Object = Nothing, Optional IsForceRestart As Boolean = False)
            Start(Input, IsForceRestart)
            Do While State = LoadState.Loading
                If LoaderToSyncProgress IsNot Nothing Then LoaderToSyncProgress.Progress = Progress
                Thread.Sleep(10)
                Timeout -= 10
                If Timeout < 0 Then Throw New TimeoutException(TimeoutMessage)
            Loop
            If State = LoadState.Finished Then
                Return
            ElseIf State = LoadState.Aborted Then
                Throw New ThreadInterruptedException("加载器执行已中断。")
            ElseIf IsNothing([Error]) Then
                Throw New Exception("未知错误！")
            Else
                Throw [Error]
            End If
        End Sub

        '相同重载
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim base = TryCast(obj, LoaderBase)
            Return base IsNot Nothing AndAlso Uuid = base.Uuid
        End Function

    End Class
    ''' <summary>
    ''' 用于异步执行并监控单一函数的加载器。
    ''' </summary>
    Public Class LoaderTask(Of InputType, OutputType)
        Inherits LoaderBase

        '线程设定
        Protected Friend ThreadPriority As ThreadPriority
        '执行事件
        Protected Friend LoadDelegate As Action(Of LoaderTask(Of InputType, OutputType))
        Protected Friend InputDelegate As Func(Of Object)
        '状态指示
        ''' <summary>
        ''' 当前执行线程是否应当中断。只应用在加载器的工作线程中判断，不可跨线程调用。
        ''' </summary>
        Public ReadOnly Property IsAborted As Boolean
            Get
                Return IsAbortedWithThread(Thread.CurrentThread)
            End Get
        End Property
        ''' <summary>
        ''' 当前执行线程是否应当中断。需要手动提供加载器线程，用于需要跨线程检查的情况。
        ''' </summary>
        Public Function IsAbortedWithThread(Thread As Thread) As Boolean
            Return LastRunningThread Is Nothing OrElse Not ReferenceEquals(Thread, LastRunningThread) OrElse State = LoadState.Aborted
        End Function
        ''' <summary>
        ''' 在输入相同时使用原有结果的超时，单位为毫秒。
        ''' </summary>
        Public ReloadTimeout As Integer = -1
        ''' <summary>
        ''' 上次完成加载时的时间。
        ''' </summary>
        Public LastFinishedTime As Long = 0
        ''' <summary>
        ''' 最后一次运行加载器的线程。可能为 Nothing，或线程已结束。
        ''' </summary>
        Public LastRunningThread As Thread = Nothing
        '输入输出
        Public Input As InputType = Nothing
        Public Output As OutputType = Nothing

        '获取输入
        Public Function StartGetInput(Optional Input As InputType = Nothing, Optional InputDelegate As Func(Of Object) = Nothing) As InputType 'InputDelegate 参数存在匿名调用
            If InputDelegate Is Nothing Then InputDelegate = Me.InputDelegate
            Dim NewInput As InputType = Nothing '若 InputType 不能为 Nothing，则会导致 Input Is Nothing 永远失败，因此需要额外判断
            If (Input Is Nothing OrElse (NewInput IsNot Nothing AndAlso Input.Equals(NewInput))) AndAlso InputDelegate IsNot Nothing Then
                RunInUiWait(Sub() Input = InputDelegate())
            End If
            Return Input
        End Function
        '代码执行
        Public Function ShouldStart(ByRef Input As Object, Optional IsForceRestart As Boolean = False, Optional IgnoreReloadTimeout As Boolean = False) As Boolean
            '获取输入
            Try
                Input = StartGetInput(Input)
            Catch ex As Exception
                Log(ex, "加载输入获取失败（" & Name & "）", LogLevel.Hint)
                [Error] = ex
                SyncLock LockState
                    State = LoadState.Failed
                End SyncLock
            End Try
            '检验输入以确定情况
            If IsForceRestart Then Return True '强制要求重启
            If ((Input Is Nothing) <> (Me.Input Is Nothing)) OrElse (Input IsNot Nothing AndAlso Not Input.Equals(Me.Input)) Then Return True '输入不同
            If (State = LoadState.Loading OrElse State = LoadState.Finished) AndAlso '正在加载或已结束
               (IgnoreReloadTimeout OrElse ReloadTimeout = -1 OrElse LastFinishedTime = 0 OrElse GetTimeTick() - LastFinishedTime < ReloadTimeout) Then '没有超时
                Return False '则不重试
            Else
                Return True '需要开始
            End If
        End Function
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            '确认是否开始加载
            If ShouldStart(Input, IsForceRestart) Then
                '输入不同或失败，开始加载
                If State = LoadState.Loading Then TriggerThreadAbort()
                Me.Input = Input
                SyncLock LockState
                    State = LoadState.Loading
                    Progress = -1
                End SyncLock
            Else
                Return
            End If

            LastRunningThread = New Thread(
            Sub()
                Try
                    IsForceRestarting = IsForceRestart
                    If ModeDebug Then Log($"[Loader] 加载线程 {Name} ({Thread.CurrentThread.ManagedThreadId}) 已{If(IsForceRestarting, "强制", "")}启动")
                    LoadDelegate(Me)
                    If IsAborted Then
                        Log($"[Loader] 加载线程 {Name} ({Thread.CurrentThread.ManagedThreadId}) 已中断但线程正常运行至结束，输出被弃用（最新线程：{If(LastRunningThread Is Nothing, -1, LastRunningThread.ManagedThreadId)}）", LogLevel.Developer)
                        Return
                    End If
                    If ModeDebug Then Log($"[Loader] 加载线程 {Name} ({Thread.CurrentThread.ManagedThreadId}) 已完成")
                    RaisePreviewFinish()
                    State = LoadState.Finished
                    LastFinishedTime = GetTimeTick() '未中断，本次输出有效
                Catch ex As CancelledException
                    If ModeDebug Then Log(ex, $"加载线程 {Name} ({Thread.CurrentThread.ManagedThreadId}) 已触发取消中断，已完成 {Math.Round(Progress * 100)}%")
                    If Not IsAborted Then State = LoadState.Aborted
                Catch ex As ThreadInterruptedException
                    If ModeDebug Then Log(ex, $"加载线程 {Name} ({Thread.CurrentThread.ManagedThreadId}) 已触发线程中断，已完成 {Math.Round(Progress * 100)}%")
                    '如果线程是因为判断到 IsAborted 而提前中止，则代表已有新线程被重启，此时不应当改为 Aborted
                    '如果线程是在没有 IsAborted 时手动引发了 ThreadInterruptedException，则代表没有重启线程，这通常代表用户手动取消，应当改为 Aborted
                    If Not IsAborted Then State = LoadState.Aborted
                Catch ex As Exception
                    If IsAborted Then Return
                    Log(ex, $"加载线程 {Name} ({Thread.CurrentThread.ManagedThreadId}) 出错，已完成 {Math.Round(Progress * 100)}%", LogLevel.Developer)
                    [Error] = ex
                    State = LoadState.Failed
                End Try
            End Sub) With {.Name = Name, .Priority = ThreadPriority}
            LastRunningThread.Start() '不能使用 RunInNewThread，否则在函数返回前线程就会运行完，导致误判 IsAborted
        End Sub
        Public Overrides Sub Abort()
            If State <> LoadState.Loading Then Return
            SyncLock LockState
                State = LoadState.Aborted
            End SyncLock
            TriggerThreadAbort()
        End Sub
        Private Sub TriggerThreadAbort()
            If LastRunningThread Is Nothing Then Return
            If ModeDebug Then Log($"[Loader] 加载线程 {Name} ({LastRunningThread.ManagedThreadId}) 已中断")
            If LastRunningThread.IsAlive Then LastRunningThread.Interrupt()
            LastRunningThread = Nothing
        End Sub

        Public Sub New()
            '仅仅是为了避免一些智障报错（继承类必须重写 New 的情况）
        End Sub
        Public Sub New(Name As String, LoadDelegate As Action(Of LoaderTask(Of InputType, OutputType)), Optional InputDelegate As Func(Of InputType) = Nothing, Optional Priority As ThreadPriority = ThreadPriority.Normal)
            Me.Name = Name
            Me.LoadDelegate = LoadDelegate
            Me.InputDelegate = InputDelegate
            ThreadPriority = Priority
        End Sub

    End Class
    ''' <summary>
    ''' 支持多个加载器连续运作的复合加载器。
    ''' </summary>
    Public Class LoaderCombo(Of InputType)
        Inherits LoaderBase

        Public Overrides Property Progress() As Double
            Get
                Select Case State
                    Case LoadState.Waiting
                        Return 0
                    Case LoadState.Loading
                        Dim Total As Double = 0, Finished As Double = 0
                        For Each Loader In Loaders
                            Total += Loader.ProgressWeight
                            Finished += Loader.ProgressWeight * Loader.Progress
                        Next
                        If Total = 0 Then Return 0
                        Return Finished / Total
                    Case Else
                        Return 1
                End Select
            End Get
            Set(value As Double)
                Throw New Exception("多重加载器不支持设置进度")
            End Set
        End Property

        Public Loaders As New List(Of LoaderBase)
        Public Input As InputType
        Public Sub New(Name As String, Loaders As IEnumerable(Of LoaderBase))
            Me.Loaders.Clear()
            For Each Loader As LoaderBase In Loaders
                If Loader IsNot Nothing Then
                    Me.Loaders.Add(Loader)
                    AddHandler Loader.OnStateChangedThread, AddressOf SubTaskStateChanged
                    Loader.HasOnStateChangedThread = True
                End If
            Next
            InitParent(Nothing)
            Me.Name = Name
        End Sub
        Public Overrides Sub InitParent(Parent As LoaderBase)
            Me.Parent = Parent
            For Each Loader In Loaders
                Loader.InitParent(Me)
            Next
        End Sub
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            IsForceRestarting = IsForceRestart
            '改变状态
            SyncLock LockState
                If State = LoadState.Loading Then
                    Return
                Else
                    State = LoadState.Loading
                End If
            End SyncLock
            '启动加载
            Me.Input = Input
            If IsForceRestart Then
                For Each Loader In Loaders
                    Loader.State = LoadState.Waiting
                Next
            End If
            RunInThread(AddressOf Update)
        End Sub
        Public Overrides Sub Abort()
            '改变状态
            SyncLock LockState
                If State = LoadState.Loading OrElse State = LoadState.Waiting Then
                    State = LoadState.Aborted
                Else
                    Return
                End If
            End SyncLock
            RunInThread(
            Sub()
                '中断加载器
                For Each Loader In Loaders
                    Loader.Abort()
                Next
            End Sub)
        End Sub

        ''' <summary>
        ''' 子任务状态变更。
        ''' </summary>
        Private Sub SubTaskStateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
            Select Case NewState
                Case LoadState.Loading
                    '开始，啥都不干
                Case LoadState.Waiting
                    '子加载器可能由于外部输入改变而暂时变为 Waiting，之后会立即重新启动
                    '所以啥都不干就行
                Case LoadState.Finished
                    '正常结束，触发刷新
                    Update()
                Case LoadState.Aborted
                    '被中断，这个任务也中断
                    Abort()
                Case Else
                    '完蛋，出错了
                    SyncLock LockState
                        If State >= LoadState.Finished Then Return
                        [Error] = New Exception(Loader.Name & "失败", Loader.Error)
                        State = Loader.State
                    End SyncLock
                    For Each Loader In Loaders
                        Loader.Abort()
                    Next
                    FrmMain.BtnExtraDownload.ShowRefresh()
                    Return
            End Select
        End Sub
        ''' <summary>
        ''' 触发一次更新，以启动新加载器或完成。
        ''' </summary>
        Private Sub Update()
            If State = LoadState.Finished OrElse State = LoadState.Failed OrElse State = LoadState.Aborted Then Return
            Dim IsFinished As Boolean = True
            Dim Blocked As Boolean = False
            Dim Input As Object = Me.Input
            For Each Loader In Loaders
                Select Case Loader.State
                    Case LoadState.Finished
                        '检查是否需要重启
                        If Loader.GetType.Name.StartsWithF("LoaderTask") Then '类型名后面带有泛型，必须用 StartsWith
                            If CType(Loader, Object).ShouldStart(If(Input IsNot Nothing AndAlso Loader.GetType.GenericTypeArguments.First Is Input.GetType, Input, Nothing), IgnoreReloadTimeout:=True) Then
                                Log("[Loader] 由于输入条件变更，重启已完成的加载器 " & Loader.Name)
                                GoTo Restart
                            End If
                            '更新下一个加载器的输入
                            Input = CType(Loader, Object).Output
                        End If
                        '如果不让继续启动，且已有加载器正在加载中，就不继续启动
                        If Loader.Block AndAlso Not IsFinished Then Blocked = True
                    Case LoadState.Loading
                        '检查是否需要重启
                        If Loader.GetType.Name.StartsWithF("LoaderTask") Then
                            If CType(Loader, Object).ShouldStart(If(Input IsNot Nothing AndAlso Loader.GetType.GenericTypeArguments.First Is Input.GetType, Input, Nothing), IgnoreReloadTimeout:=True) Then
                                Log("[Loader] 由于输入条件变更，重启进行中的加载器 " & Loader.Name, LogLevel.Developer)
                                GoTo Restart
                            End If
                        End If
                        '已经有正在加载中的了，不需要再启动了
                        IsFinished = False
                        Blocked = True
                    Case Else
Restart:
                        '未启动，则启动加载器
                        IsFinished = False
                        If Blocked Then Continue For
                        If Input IsNot Nothing Then
                            '若输入类型与下一个加载器相同才继续
                            Dim LoaderType As String = Loader.GetType.Name
                            If LoaderType.StartsWithF("LoaderTask") OrElse LoaderType.StartsWithF("LoaderCombo") Then
                                Loader.Start(If(Loader.GetType.GenericTypeArguments.First Is Input.GetType, Input, Nothing), IsForceRestarting)
                            ElseIf LoaderType.StartsWithF("LoaderDownload") Then
                                Loader.Start(If(TypeOf Input Is List(Of NetFile), Input, Nothing), IsForceRestarting)
                            Else
                                Throw New Exception("未知的加载器类型（" & LoaderType & "）")
                            End If
                        Else
                            Loader.Start(IsForceRestart:=IsForceRestarting)
                        End If
                        '阻止继续
                        If Loader.Block Then Blocked = True
                End Select
            Next
            If IsFinished Then
                '顺利完成，贼棒
                RaisePreviewFinish()
                State = LoadState.Finished
                FrmMain.BtnExtraDownload.ShowRefresh()
            End If
        End Sub

        ''' <summary>
        ''' 获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        ''' </summary>
        Public Shared Sub GetLoaderList(Loader As Object, ByRef List As List(Of LoaderBase), Optional RequireShow As Boolean = True)
            For Each SubLoader In Loader.Loaders
                If SubLoader.Show OrElse Not RequireShow Then List.Add(SubLoader)
                If SubLoader.GetType.Name.StartsWithF("LoaderCombo") Then GetLoaderList(SubLoader, List)
            Next
        End Sub
        ''' <summary>
        ''' 获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        ''' </summary>
        Public Sub GetLoaderList(ByRef List As List(Of LoaderBase), Optional RequireShow As Boolean = True)
            GetLoaderList(Me, List, RequireShow)
        End Sub
        ''' <summary>
        ''' 获得最底层的，应被显示给用户的加载器列表。
        ''' </summary>
        Public Function GetLoaderList(Optional RequireShow As Boolean = True) As List(Of LoaderBase)
            Dim List As New List(Of LoaderBase)
            GetLoaderList(List, RequireShow)
            Return List
        End Function

    End Class

    '任务栏进度条
    Public LoaderTaskbar As New SafeList(Of LoaderBase)
    Public LoaderTaskbarProgress As Double = 0 '平滑后的进度
    Private LoaderTaskbarProgressLast As Shell.TaskbarItemProgressState = Shell.TaskbarItemProgressState.None

    Public Sub LoaderTaskbarAdd(Of T)(Loader As LoaderCombo(Of T))
        If FrmSpeedLeft IsNot Nothing Then FrmSpeedLeft.TaskRemove(Loader)
        LoaderTaskbar.Add(Loader)
        Log($"[Taskbar] {Loader.Name} 已加入任务列表")
    End Sub
    Public Sub LoaderTaskbarProgressRefresh()
        Try
            Dim NewState As Shell.TaskbarItemProgressState
            Dim NewProgress As Double = LoaderTaskbarProgressGet()
            '若单个任务已中止，或全部任务已完成，则刷新并移除
            For Each Task In LoaderTaskbar
                If LoaderTaskbar.All(Function(l) l.State <> LoadState.Loading) OrElse
                   (Task.State = LoadState.Waiting OrElse Task.State = LoadState.Aborted) Then
                    FrmSpeedLeft?.TaskRefresh(Task)
                    LoaderTaskbar.Remove(Task)
                    Log($"[Taskbar] {Task.Name} 已移出任务列表")
                End If
            Next
            '更新平滑后的进度
            If NewProgress <= 0 OrElse NewProgress >= 1 OrElse LoaderTaskbarProgress > NewProgress Then
                LoaderTaskbarProgress = NewProgress
            Else
                LoaderTaskbarProgress = LoaderTaskbarProgress * 0.9 + NewProgress * 0.1
            End If
            RunInUi(Sub() FrmMain.BtnExtraDownload.Progress = LoaderTaskbarProgress)
            '更新任务栏信息
            If Not LoaderTaskbar.Any() OrElse LoaderTaskbarProgress = 1 Then
                NewState = Shell.TaskbarItemProgressState.None
            ElseIf LoaderTaskbarProgress < 0.015 Then
                NewState = Shell.TaskbarItemProgressState.Indeterminate
            Else
                NewState = Shell.TaskbarItemProgressState.Normal
                FrmMain.TaskbarItemInfo.ProgressValue = LoaderTaskbarProgress
            End If
            If LoaderTaskbarProgressLast <> NewState Then
                LoaderTaskbarProgressLast = NewState
                FrmMain.TaskbarItemInfo.ProgressState = NewState
                FrmMain.BtnExtraDownload.ShowRefresh()
            End If
        Catch ex As Exception
            Log(ex, "刷新任务栏进度显示失败", LogLevel.Feedback)
        End Try
    End Sub
    Public Function LoaderTaskbarProgressGet() As Double
        Try
            If Not LoaderTaskbar.Any Then Return 1
            Return MathClamp(LoaderTaskbar.Select(Function(l) l.Progress).Average(), 0, 1)
        Catch ex As Exception
            Log(ex, "获取任务栏进度出错", LogLevel.Feedback)
            Return 0.5
        End Try
    End Function

    '文件夹刷新类委托
    Private LoaderFolderDictionary As New Dictionary(Of LoaderBase, LoaderFolderDictionaryEntry)
    Private Structure LoaderFolderDictionaryEntry
        Public LastCheckTime As Date?
        Public FolderPath As String
        Public Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj IsNot LoaderFolderDictionaryEntry Then Return False
            Dim entry = DirectCast(obj, LoaderFolderDictionaryEntry)
            Return EqualityComparer(Of Date?).Default.Equals(LastCheckTime, entry.LastCheckTime) AndAlso
                   FolderPath = entry.FolderPath
        End Function
    End Structure
    Public Enum LoaderFolderRunType
        RunOnUpdated
        ForceRun
        UpdateOnly
    End Enum
    ''' <summary>
    ''' 执行以文件夹检测作为输入的加载器。加载器需以文件夹路径为输入值。
    ''' 返回是否执行了加载器。
    ''' </summary>
    ''' <param name="ExtraPath">用于检查文件夹修改的额外路径。该路径不会传入加载器。</param>
    Public Function LoaderFolderRun(Loader As LoaderBase, FolderPath As String, Type As LoaderFolderRunType, Optional MaxDepth As Integer = 0, Optional ExtraPath As String = "", Optional WaitForExit As Boolean = False) As Boolean
        Dim FolderInfo As DirectoryInfo
        Dim Value As New LoaderFolderDictionaryEntry With {.FolderPath = FolderPath & ExtraPath, .LastCheckTime = Nothing}
        Try
            '获取数据
            FolderInfo = New DirectoryInfo(FolderPath & ExtraPath)
            Value.LastCheckTime = If(FolderInfo.Exists, GetActualLastWriteTimeUtc(FolderInfo, MaxDepth), DirectCast(Nothing, Date?))
            '如果已经检查过，则跳过
            If Type = LoaderFolderRunType.RunOnUpdated AndAlso LoaderFolderDictionary.ContainsKey(Loader) Then
                If FolderInfo.Exists Then
                    If LoaderFolderDictionary(Loader).LastCheckTime IsNot Nothing AndAlso
                       Value.Equals(LoaderFolderDictionary(Loader)) Then Return False
                Else
                    If LoaderFolderDictionary(Loader).LastCheckTime Is Nothing Then Return False
                End If
            End If
        Catch ex As Exception
            Log(ex, "文件夹加载器启动检测出错")
        End Try
        '写入检查数据
        LoaderFolderDictionary(Loader) = Value
        '开始检查
        If Type = LoaderFolderRunType.UpdateOnly Then Return False
        If WaitForExit Then
            Loader.WaitForExit(FolderPath, IsForceRestart:=True)
        Else
            Loader.Start(FolderPath, IsForceRestart:=True)
        End If
        Return True
    End Function
    Private Function GetActualLastWriteTimeUtc(FolderInfo As DirectoryInfo, MaxDepth As Integer) As Date
        Dim Time As Date = FolderInfo.LastWriteTimeUtc
        If MaxDepth > 0 Then
            For Each Folder In FolderInfo.EnumerateDirectories
                Dim FolderTime As Date = GetActualLastWriteTimeUtc(Folder, MaxDepth - 1)
                If FolderTime > Time Then Time = FolderTime
            Next
        End If
        Return Time
    End Function

End Module
