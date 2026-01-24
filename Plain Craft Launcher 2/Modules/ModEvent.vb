
#Region "附加属性"

''' <summary>
''' 用于在 XAML 中初始化列表对象。
''' 附加属性无法在 XAML 中为每个对象初始化独立的列表对象，因此需要一个包装类，然后在 XAML 中显式初始化。
''' </summary>
<Markup.ContentProperty("Events")>
Public Class CustomEventCollection
    Implements IEnumerable(Of CustomEvent)
    Dim _Events As New List(Of CustomEvent)
    Public ReadOnly Property Events As List(Of CustomEvent)
        Get
            Return _Events
        End Get
    End Property
    Public Function GetEnumerator() As IEnumerator(Of CustomEvent) Implements IEnumerable(Of CustomEvent).GetEnumerator
        Return DirectCast(Events, IEnumerable(Of CustomEvent)).GetEnumerator()
    End Function
    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return DirectCast(Events, IEnumerable).GetEnumerator()
    End Function
End Class

''' <summary>
''' 提供自定义事件的附加属性。
''' </summary>
Public Class CustomEventService

    'Events
    Public Shared ReadOnly EventsProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("Events", GetType(CustomEventCollection), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEvents(d As DependencyObject, value As CustomEventCollection)
        d.SetValue(EventsProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEvents(d As DependencyObject) As CustomEventCollection
        If d.GetValue(EventsProperty) Is Nothing Then d.SetValue(EventsProperty, New CustomEventCollection)
        Return d.GetValue(EventsProperty)
    End Function

    'EventType
    Public Shared ReadOnly EventTypeProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventType", GetType(CustomEvent.EventType), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventType(d As DependencyObject, value As CustomEvent.EventType)
        d.SetValue(EventTypeProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventType(d As DependencyObject) As CustomEvent.EventType
        Return d.GetValue(EventTypeProperty)
    End Function

    'EventData
    Public Shared ReadOnly EventDataProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventData", GetType(String), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventData(d As DependencyObject, value As String)
        d.SetValue(EventDataProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventData(d As DependencyObject) As String
        Return d.GetValue(EventDataProperty)
    End Function

End Class

Partial Public Module ModMain

    ''' <summary>
    ''' 触发该控件上的自定义事件。
    ''' 事件会在新线程中执行。
    ''' </summary>
    <Runtime.CompilerServices.Extension>
    Public Sub RaiseCustomEvent(Control As DependencyObject)
        '收集事件列表
        Dim Events = CustomEventService.GetEvents(Control).ToList
        Dim EventType = CustomEventService.GetEventType(Control)
        If EventType <> CustomEvent.EventType.None Then Events.Add(New CustomEvent(EventType, CustomEventService.GetEventData(Control)))
        '执行事件
        If Not Events.Any Then Return
        RunInNewThread(
        Sub()
            For Each e In Events
                e.Raise()
            Next
        End Sub, "执行自定义事件 " & GetUuid())
    End Sub

End Module

#End Region

''' <summary>
''' 自定义事件。
''' </summary>
Public Class CustomEvent
    Inherits DependencyObject

#Region "属性与触发"

    Public Property Type As EventType
        Get
            Dim Value As EventType = EventType.None
            RunInUiWait(Sub() Value = GetValue(TypeProperty))
            Return Value
        End Get
        Set(value As EventType)
            SetValue(TypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TypeProperty As DependencyProperty =
        DependencyProperty.Register("Type", GetType(EventType), GetType(CustomEvent), New PropertyMetadata(EventType.None))

    Public Property Data As String
        Get
            Dim Value As String = Nothing
            RunInUiWait(Sub() Value = GetValue(DataProperty))
            Return Value
        End Get
        Set(value As String)
            SetValue(DataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly DataProperty As DependencyProperty =
        DependencyProperty.Register("Data", GetType(String), GetType(CustomEvent), New PropertyMetadata(Nothing))

    Public Sub New()
    End Sub
    Public Sub New(Type As EventType, Data As String)
        Me.Type = Type
        Me.Data = Data
    End Sub

    ''' <summary>
    ''' 在当前线程中触发该自定义事件。
    ''' </summary>
    Public Sub Raise()
        Raise(Type, Data)
    End Sub

#End Region

    Public Enum EventType
        None = 0
        打开网页
        打开文件
        打开帮助
        执行命令
        启动游戏
        复制文本
        刷新主页
        刷新页面
        刷新帮助
        今日人品
        内存优化
        清理垃圾
        弹出窗口
        弹出提示
        切换页面
        导入整合包
        安装整合包
        下载文件
        修改设置
        写入设置
        修改变量
        写入变量
        加入房间
        检查更新
    End Enum

    ''' <summary>
    ''' 在当前线程中触发一个自定义事件。
    ''' </summary>
    Public Shared Sub Raise(Type As EventType, Arg As String)
        If Type = EventType.None Then Return
        Log($"[Control] 执行自定义事件：{Type}, {Arg}")
        If Arg Is Nothing Then Arg = ""
        Dim Args As String() = Arg.Split("|")
        Try
            Select Case Type
                Case EventType.打开网页
                    Arg = Arg.Replace("\", "/")
                    If Not Arg.Contains("://") OrElse Arg.StartsWithF("file", True) Then '为了支持更多协议（#2200）
                        MyMsgBox("EventData 必须为一个网址。" & vbCrLf & "如果想要启动程序，请将 EventType 改为 打开文件。", "事件执行失败")
                        Return
                    End If
                    Hint("正在开启中，请稍候：" & Arg)
                    RunInThread(Sub() OpenWebsite(Arg))

                Case EventType.打开文件, EventType.打开帮助, EventType.执行命令
                    RunInThread(
                    Sub()
                        Try
                            '确认实际路径
                            Dim ActualPaths = GetAbsoluteUrls(Args(0), Type)
                            Dim Location = ActualPaths(0), WorkingDir = ActualPaths(1)
                            Log($"[Control] 打开类自定义事件实际路径：{Location}，工作目录：{WorkingDir}")
                            '执行
                            If Type = EventType.打开帮助 Then
                                PageOtherHelp.EnterHelpPage(Location)
                            Else
                                If Not EventSafetyConfirm("即将执行：" & Location & If(Args.Length >= 2, " " & Args(1), "")) Then Return
                                Dim Info As New ProcessStartInfo With {
                                    .Arguments = If(Args.Length >= 2, Args(1), ""),
                                    .FileName = Location,
                                    .WorkingDirectory = WorkingDir
                                }
                                StartProcess(Info)
                            End If
                        Catch ex As Exception
                            Log(ex, "执行打开类自定义事件失败", LogLevel.Msgbox)
                        End Try
                    End Sub)

                Case EventType.启动游戏
                    If Args(0) = "\current" Then
                        If McInstanceSelected Is Nothing Then
                            Hint("请先选择一个 Minecraft 版本！", HintType.Red)
                            Return
                        Else
                            Args(0) = McInstanceSelected.Name
                        End If
                    End If
                    RunInUi(
                    Sub()
                        If McLaunchStart(New McLaunchOptions With
                                {.ServerIp = If(Args.Length >= 2, Args(1), Nothing), .Instance = New McInstance(Args(0))}) Then
                            Hint($"正在启动 {Args(0)}……")
                        End If
                    End Sub)

                Case EventType.复制文本
                    ClipboardSet(Arg)

                Case EventType.刷新主页
                    RunInUi(
                    Sub()
                        FrmLaunchRight.ForceRefresh()
                        If String.IsNullOrEmpty(Arg) Then Hint("已刷新！", HintType.Green)
                    End Sub)

                Case EventType.刷新页面
                    If TypeOf FrmMain.PageRight Is IRefreshable Then
                        RunInUiWait(Sub() CType(FrmMain.PageRight, IRefreshable).Refresh())
                        If String.IsNullOrEmpty(Arg) Then Hint("已刷新！", HintType.Green)
                    Else
                        Hint("当前页面不支持刷新操作！", HintType.Red)
                    End If

                Case EventType.刷新帮助
                    RunInUiWait(Sub() PageOtherLeft.RefreshHelp())
                    If String.IsNullOrEmpty(Arg) Then Hint("已刷新！", HintType.Green)

                Case EventType.今日人品
                    PageOtherTest.Jrrp()

                Case EventType.内存优化
                    RunInThread(Sub() PageOtherTest.MemoryOptimize(True))

                Case EventType.清理垃圾
                    RunInThread(Sub() PageOtherTest.RubbishClear())

                Case EventType.弹出窗口
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 弹窗标题|弹窗内容")
                    MyMsgBox(Args(1).Replace("\n", vbCrLf), Args(0).Replace("\n", vbCrLf), If(Args.Length > 2, Args(2), "确定"))

                Case EventType.弹出提示
                    Hint(Args(0).Replace("\n", vbCrLf), If(Args.Length = 1, HintType.Blue, Args(1).ParseToEnum(Of HintType)))

                Case EventType.切换页面
                    RunInUi(Sub() FrmMain.PageChange(
                                Args(0).ParseToEnum(Of FormMain.PageType),
                                If(Args.Length = 1, FormMain.PageSubType.Default, Args(1).ParseToEnum(Of FormMain.PageSubType))))

                Case EventType.导入整合包, EventType.安装整合包
                    RunInUi(Sub() ModpackInstall())

                Case EventType.下载文件
                    Args(0) = Args(0).Replace("\", "/")
                    If Not (Args(0).StartsWithF("http://", True) OrElse Args(0).StartsWithF("https://", True)) Then
                        MyMsgBox("EventData 必须为以 http:// 或 https:// 开头的网址。" & vbCrLf & "PCL 不支持其他乱七八糟的下载协议。", "事件执行失败")
                        Return
                    End If
                    If Not EventSafetyConfirm("即将从该网址下载文件：" & vbCrLf & Args(0)) Then Return
                    RunInUi(
                    Sub()
                        Try
                            Select Case Args.Length
                                Case 1
                                    PageOtherTest.StartCustomDownload(Args(0), GetFileNameFromPath(Args(0)))
                                Case 2
                                    PageOtherTest.StartCustomDownload(Args(0), Args(1))
                                Case Else
                                    PageOtherTest.StartCustomDownload(Args(0), Args(1), Args(2))
                            End Select
                        Catch
                            PageOtherTest.StartCustomDownload(Args(0), "未知")
                        End Try
                    End Sub)

                Case EventType.修改设置, EventType.写入设置
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 UiLauncherTransparent|400")
                    Setup.SetSafe(Args(0), Args(1), Instance:=McInstanceSelected)
                    If Args.Length = 2 Then Hint($"已写入设置：{Args(0)} → {Args(1)}", HintType.Green)

                Case EventType.修改变量, EventType.写入变量
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 VariableName|SomeValue")
                    WriteReg("CustomEvent" & Args(0), Args(1))
                    If Args.Length = 2 Then Hint($"已写入变量：{Args(0)} → {Args(1)}", HintType.Green)

                Case EventType.加入房间
                    RunInUi(
                    Sub()
                        If String.IsNullOrWhiteSpace(Arg) Then
                            PageLinkMain.Join()
                        Else
                            If Not EventSafetyConfirm("即将加入第三方联机房间。") Then Return
                            PageLinkMain.Join(Arg)
                        End If
                    End Sub)

                Case EventType.检查更新
                    UpdateCheckByButton()

                Case Else
                    MyMsgBox("未知的事件类型：" & Type & vbCrLf & "请检查事件类型填写是否正确，或者 PCL 是否为最新版本。", "事件执行失败")
            End Select
        Catch ex As Exception
            Log(ex, $"事件执行失败（{Type}, {Arg}）", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 返回自定义事件的绝对 Url。实际返回 {绝对 Url, WorkingDir}。
    ''' 失败会抛出异常。
    ''' </summary>
    Public Shared Function GetAbsoluteUrls(RelativeUrl As String, Type As EventType) As String()

        '网页确认
        If RelativeUrl.StartsWithF("http", True) Then
            If RunInUi() Then
                Throw New Exception("能打开联网帮助页面的 MyListItem 必须手动设置 Title、Info 属性！")
            End If
            '获取文件名
            Dim RawFileName As String
            Try
                RawFileName = GetFileNameFromPath(RelativeUrl)
                If Not RawFileName.EndsWithF(".json", True) Then Throw New Exception("未指向 .json 后缀的文件")
            Catch ex As Exception
                Throw New Exception("联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！" & vbCrLf &
                                    "例如：" & vbCrLf &
                                    " - https://www.baidu.com/test.json（填写这个路径）" & vbCrLf &
                                    " - https://www.baidu.com/test.xaml（同时也需要包含这个文件）", ex)
            End Try
            '下载文件
            Dim LocalTemp As String = RequestTaskTempFolder() & RawFileName
            Log("[Event] 转换网络资源：" & RelativeUrl & " -> " & LocalTemp)
            Try
                NetDownloadByClient(RelativeUrl, LocalTemp)
                NetDownloadByClient(RelativeUrl.Replace(".json", ".xaml"), LocalTemp.Replace(".json", ".xaml"))
            Catch ex As Exception
                Throw New Exception("下载指定的文件失败！" & vbCrLf &
                                    "注意，联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！" & vbCrLf &
                                    "例如：" & vbCrLf &
                                    " - https://www.baidu.com/test.json（填写这个路径）" & vbCrLf &
                                    " - https://www.baidu.com/test.xaml（同时也需要包含这个文件）", ex)
            End Try
            RelativeUrl = LocalTemp
        End If
        RelativeUrl = RelativeUrl.Replace("/", "\").ToLower.TrimStart("\")

        '确认实际路径
        Dim Location As String, WorkingDir As String = Path & "PCL"
        HelpTryExtract()
        If RelativeUrl.Contains(":\") Then
            '绝对路径
            Location = RelativeUrl
            Log("[Control] 自定义事件中由绝对路径" & Type & "：" & Location)
        ElseIf File.Exists(Path & "PCL\" & RelativeUrl) Then
            '相对 PCL 文件夹的路径
            Location = Path & "PCL\" & RelativeUrl
            Log("[Control] 自定义事件中由相对 PCL 文件夹的路径" & Type & "：" & Location)
        ElseIf File.Exists(Path & "PCL\Help\" & RelativeUrl) Then
            '相对 PCL 本地帮助文件夹的路径
            Location = Path & "PCL\Help\" & RelativeUrl
            WorkingDir = Path & "PCL\Help\"
            Log("[Control] 自定义事件中由相对 PCL 本地帮助文件夹的路径" & Type & "：" & Location)
        ElseIf Type = EventType.打开帮助 AndAlso File.Exists(PathTemp & "Help\" & RelativeUrl) Then
            '相对 PCL 自带帮助文件夹的路径
            Location = PathTemp & "Help\" & RelativeUrl
            WorkingDir = PathTemp & "Help\"
            Log("[Control] 自定义事件中由相对 PCL 自带帮助文件夹的路径" & Type & "：" & Location)
        ElseIf Type = EventType.打开文件 OrElse Type = EventType.执行命令 Then
            '直接使用原有路径启动程序
            Location = RelativeUrl
            Log("[Control] 自定义事件中直接" & Type & "：" & Location)
        Else
            '打开帮助，但是格式不对劲
            Throw New FileNotFoundException("未找到 EventData 指向的本地 xaml 文件：" & RelativeUrl, RelativeUrl)
        End If

        Return {Location, WorkingDir}
    End Function

    ''' <summary>
    ''' 弹出安全确认弹窗。返回是否继续执行。
    ''' </summary>
    Private Shared Function EventSafetyConfirm(Message As String) As Boolean
        If Setup.Get("HintCustomCommand") Then Return True
        Select Case MyMsgBox(Message & vbCrLf & "请在确认没有安全隐患后再继续。", "执行确认", "继续", "继续且今后不再要求确认", "取消")
            Case 1
                Return True
            Case 2
                Setup.Set("HintCustomCommand", True)
                Return True
            Case Else
                Return False
        End Select
    End Function

End Class
