Public Module ModEvent

    Public Sub TryStartEvent(Type As String, Data As String)
        If String.IsNullOrWhiteSpace(Type) Then Return
        Dim RealData As String() = {""}
        If Data IsNot Nothing Then RealData = Data.Split("|")
        StartEvent(Type, RealData)
    End Sub
    Public Sub StartEvent(Type As String, Data As String())
        Try
            Log("[Control] 执行自定义事件：" & Type & ", " & Join(Data, ", "))
            Select Case Type

                Case "打开网页"
                    Data(0) = Data(0).Replace("\", "/")
                    If Not Data(0).Contains("://") OrElse Data(0).StartsWithF("file", True) Then '为了支持更多协议（#2200）
                        MyMsgBox("EventData 必须为一个网址。" & vbCrLf & "如果想要启动程序，请将 EventType 改为 打开文件。", "事件执行失败")
                        Return
                    End If
                    Hint("正在开启中，请稍候……")
                    OpenWebsite(Data(0))

                Case "打开文件", "打开帮助", "执行命令"
                    RunInThread(
                    Sub()
                        Try
                            '确认实际路径
                            Dim ActualPaths = GetEventAbsoluteUrls(Data(0), Type)
                            Dim Location = ActualPaths(0), WorkingDir = ActualPaths(1)
                            Log($"[Control] 打开类自定义事件实际路径：{Location}，工作目录：{WorkingDir}")
                            '执行
                            If Type = "打开帮助" Then
                                PageOtherHelp.EnterHelpPage(Location)
                            Else
                                If Not Setup.Get("HintCustomCommand") Then
                                    Select Case MyMsgBox(
                                    "即将执行：" & Location & If(Data.Length >= 2, " " & Data(1), "") & vbCrLf &
                                    "请在确认该操作没有安全隐患后继续。", "执行确认", "继续", "继续且今后不再要求确认", "取消")
                                        Case 2
                                            Setup.Set("HintCustomCommand", True)
                                        Case 3
                                            Return
                                    End Select
                                End If
                                Dim Info As New ProcessStartInfo With {
                                    .Arguments = If(Data.Length >= 2, Data(1), ""),
                                    .FileName = Location,
                                    .WorkingDirectory = ShortenPath(WorkingDir)
                                }
                                Process.Start(Info)
                            End If
                        Catch ex As Exception
                            Log(ex, "执行打开类自定义事件失败", LogLevel.Msgbox)
                        End Try
                    End Sub)

                Case "启动游戏"
                    If Data(0) = "\current" Then
                        If McVersionCurrent Is Nothing Then
                            Hint("请先选择一个 Minecraft 版本！", HintType.Critical)
                            Return
                        Else
                            Data(0) = McVersionCurrent.Name
                        End If
                    End If
                    If McLaunchStart(New McLaunchOptions With
                                     {.ServerIp = If(Data.Length >= 2, Data(1), Nothing), .Version = New McVersion(Data(0))}) Then
                        Hint("正在启动 " & Data(0) & "……")
                    End If

                Case "复制文本"
                    ClipboardSet(Join(Data, "|"))

                Case "刷新主页"
                    FrmLaunchRight.ForceRefresh()
                    If Data(0) = "" Then Hint("已刷新主页！", HintType.Finish)

                Case "刷新帮助"
                    PageOtherLeft.RefreshHelp()

                Case "今日人品"
                    PageOtherTest.Jrrp()

                Case "内存优化"
                    RunInThread(Sub() PageOtherTest.MemoryOptimize(True))

                Case "清理垃圾"
                    RunInThread(Sub() PageOtherTest.RubbishClear())

                Case "弹出窗口"
                    MyMsgBox(Data(1).Replace("\n", vbCrLf), Data(0).Replace("\n", vbCrLf))

                Case "切换页面"
                    FrmMain.PageChange(Val(Data(0)), Val(Data(1)))

                Case "导入整合包", "安装整合包"
                    RunInUi(Sub() ModpackInstall())

                Case "下载文件"
                    Data(0) = Data(0).Replace("\", "/")
                    If Not (Data(0).StartsWithF("http://", True) OrElse Data(0).StartsWithF("https://", True)) Then
                        MyMsgBox("EventData 必须为以 http:// 或 https:// 开头的网址。" & vbCrLf & "PCL 不支持其他乱七八糟的下载协议。", "事件执行失败")
                        Return
                    End If
                    Try
                        Select Case Data.Length
                            Case 1
                                PageOtherTest.StartCustomDownload(Data(0), GetFileNameFromPath(Data(0)))
                            Case 2
                                PageOtherTest.StartCustomDownload(Data(0), Data(1))
                            Case Else
                                PageOtherTest.StartCustomDownload(Data(0), Data(1), Data(2))
                        End Select
                    Catch
                        PageOtherTest.StartCustomDownload(Data(0), "未知")
                    End Try

                Case Else
                    MyMsgBox("未知的事件类型：" & Type & vbCrLf & "请检查事件类型填写是否正确，或者 PCL 是否为最新版本。", "事件执行失败")
            End Select
        Catch ex As Exception
            Log(ex, "事件执行失败", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 返回自定义事件的绝对 Url。实际返回 {绝对 Url, WorkingDir}。
    ''' 失败会抛出异常。
    ''' </summary>
    Public Function GetEventAbsoluteUrls(RelativeUrl As String, EventType As String) As String()

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
            Log("[Control] 自定义事件中由绝对路径" & EventType & "：" & Location)
        ElseIf File.Exists(Path & "PCL\" & RelativeUrl) Then
            '相对 PCL 文件夹的路径
            Location = Path & "PCL\" & RelativeUrl
            Log("[Control] 自定义事件中由相对 PCL 文件夹的路径" & EventType & "：" & Location)
        ElseIf File.Exists(Path & "PCL\Help\" & RelativeUrl) Then
            '相对 PCL 本地帮助文件夹的路径
            Location = Path & "PCL\Help\" & RelativeUrl
            WorkingDir = Path & "PCL\Help\"
            Log("[Control] 自定义事件中由相对 PCL 本地帮助文件夹的路径" & EventType & "：" & Location)
        ElseIf EventType = "打开帮助" AndAlso File.Exists(PathTemp & "Help\" & RelativeUrl) Then
            '相对 PCL 自带帮助文件夹的路径
            Location = PathTemp & "Help\" & RelativeUrl
            WorkingDir = PathTemp & "Help\"
            Log("[Control] 自定义事件中由相对 PCL 自带帮助文件夹的路径" & EventType & "：" & Location)
        ElseIf EventType = "打开文件" OrElse EventType = "执行命令" Then
            '直接使用原有路径启动程序
            Location = RelativeUrl
            Log("[Control] 自定义事件中直接" & EventType & "：" & Location)
        Else
            '打开帮助，但是格式不对劲
            Throw New FileNotFoundException("未找到 EventData 指向的本地 xaml 文件：" & RelativeUrl, RelativeUrl)
        End If

        Return {Location, WorkingDir}
    End Function

End Module
