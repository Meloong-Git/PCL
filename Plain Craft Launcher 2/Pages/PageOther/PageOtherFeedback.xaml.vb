Imports PCL.Core.Utils

Public Class PageOtherFeedback

    Public Class Feedback
        Public Property User As String
        Public Property Title As String
        Public Property Time As Date
        Public Property Content As String
        Public Property Url As String
        Public Property ID As String
        Public Property Tags As New List(Of String)
        Public Property Open As Boolean = True
        Public Property Type As String
    End Class

    Enum TagID As Int64
        Processing = 6820804544 '处理中
        WaitingProcess = 6820804546 '等待处理
        Completed = 6820804547 '完成
        Decline = 6820804539 '拒绝
        Ignored = 8064650117 '忽略
        Duplicate = 6820804541 '重复
        Wait = 8743070786
        Pause = 8558220235
        Upnext = 8550609020
    End Enum

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherFeedback_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageLoaderInit(Load, PanLoad, PanContent, PanInfo, Loader, AddressOf RefreshList)
        '重复加载部分
        PanBack.ScrollToHome()
        '非重复加载部分
        If IsLoaded Then Exit Sub
        IsLoaded = True

    End Sub

    Public Loader As New LoaderTask(Of Integer, List(Of Feedback))("FeedbackList", AddressOf FeedbackListGet)

    Public Sub FeedbackListGet(Task As LoaderTask(Of Integer, List(Of Feedback)))
        Dim list As JArray
        list = NetGetCodeByRequestRetry("https://api.github.com/repos/PCL-Community/PCL2-CE/issues?state=all&sort=created&per_page=200", IsJson:=True, UseBrowserUserAgent:=True) ' 获取近期 200 条数据就够了
        If list Is Nothing Then Throw New Exception("无法获取到内容")
        Dim res As List(Of Feedback) = New List(Of Feedback)
        For Each i As JObject In list
            Dim item As Feedback = New Feedback With {.Title = i("title").ToString(),
                .Url = i("html_url").ToString(),
                .Content = i("body").ToString(),
                .Time = Date.Parse(i("created_at").ToString()),
                .User = i("user")("login").ToString(),
                .ID = i("number"),
                .Open = i("state").ToString().Equals("open")}
            Dim issueType As String = "未分类"
            Dim typeToken As JToken = i("type")
            If typeToken IsNot Nothing AndAlso typeToken.Type = JTokenType.Object Then
                Dim typeNameToken As JToken = typeToken("name")
                If typeNameToken IsNot Nothing Then
                    issueType = typeNameToken.ToString().ToLower()
                End If
            End If
            item.Type = issueType
            Dim thisTags As JArray = i("labels")
            For Each thisTag As JObject In thisTags
                item.Tags.Add(thisTag("id"))
            Next
            res.Add(item)
        Next
        Task.Output = res
    End Sub
    Private Function AppendTypeToStatus(status As String, typeName As String) As String
        If String.IsNullOrEmpty(typeName) Then Return status

        ' 统一转为小写比较
        Dim lowerType = typeName.ToLower()

        ' 允许追加的类型列表
        Dim allowedTypes As New List(Of String) From {"bug", "崩溃", "新功能", "优化", "未分类", "网络"}

        ' 如果类型在允许列表中，则追加
        If allowedTypes.Contains(lowerType) Then
            Return status & "-" & typeName
        End If

        Return status
    End Function
    Public Sub RefreshList()
        PanListProcessing.Children.Clear()
        PanListWaitingProcess.Children.Clear()
        PanListWait.Children.Clear()
        PanListPause.Children.Clear()
        PanListUpnext.Children.Clear()
        PanListCompleted.Children.Clear()
        PanListDecline.Children.Clear()
        PanListIgnored.Children.Clear()
        For Each item In Loader.Output
            Dim ele As New MyListItem With {.Title = item.Title, .Type = MyListItem.CheckType.Clickable}
            Dim StatusDesc As String = "???"
            Dim commonInfo = $"{item.User} | {item.Time} | 类型: {item.Type}"

            Dim clickHandler As Action = Sub()
                                             Select Case MyMsgBoxMarkdown(
                                                 $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                                                 $"状态：{item.Tags} | 类型：{item.Type}" & vbCrLf & vbCrLf &
                                                 $"{item.Content}",
                                                 $"#{item.ID} {item.Title}",
                                                 Button2:="查看详情")
                                                 Case 2
                                                     OpenWebsite(item.Url) ' 打开 GitHub Issue 链接
                                             End Select
                                         End Sub

            ' 正在处理

            If item.Tags.Contains(TagID.Processing) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/CommandBlock.png"
                    .Tags = AppendTypeToStatus("处理中", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListProcessing.Children.Add(li)
            End If

            '等待处理

            If item.Tags.Contains(TagID.WaitingProcess) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/RedstoneBlock.png"
                    .Tags = AppendTypeToStatus("等待处理", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListWaitingProcess.Children.Add(li)
            End If

            'WAIT

            If item.Tags.Contains(TagID.Wait) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/Anvil.png"
                    .Tags = AppendTypeToStatus("已确认，等待社区开发者接管该内容的处理", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListWait.Children.Add(li)
            End If

            'PAUSE

            If item.Tags.Contains(TagID.Pause) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/RedstoneLampOff.png"
                    .Tags = AppendTypeToStatus("近期不计划制作此功能", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListPause.Children.Add(li)
            End If

            'UP NEXT

            If item.Tags.Contains(TagID.Upnext) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/RedstoneLampOn.png"
                    .Tags = AppendTypeToStatus("即将开工的内容", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListUpnext.Children.Add(li)
            End If

            '已完成

            If item.Tags.Contains(TagID.Completed) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/Grass.png"
                    .Tags = AppendTypeToStatus("已完成", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListCompleted.Children.Add(li)
            End If

            '已拒绝

            If item.Tags.Contains(TagID.Decline) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/CobbleStone.png"
                    .Tags = AppendTypeToStatus("已拒绝", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListDecline.Children.Add(li)
            End If

            '已忽略

            If item.Tags.Contains(TagID.Ignored) Then
                Dim li As New MyListItem()
                With li
                    .Title = item.Title
                    .Type = MyListItem.CheckType.Clickable
                    .Info = commonInfo
                    .Logo = PathImage & "Blocks/CobbleStone.png"
                    .Tags = AppendTypeToStatus("已忽略", item.Type)
                End With

                AddHandler li.Click,
            Sub(sender As Object, e As RoutedEventArgs)
                Select Case MyMsgBoxMarkdown(
                    $"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）" & vbCrLf &
                    $"类型：{item.Type}" & vbCrLf & vbCrLf &
                    $"{item.Content}",
                    $"#{item.ID} {item.Title}",
                    Button2:="查看详情")
                    Case 2
                        OpenWebsite(item.Url)
                End Select
            End Sub

                PanListIgnored.Children.Add(li)
            End If
            ele.Info = item.User & " | " & item.Time
            ele.Tags = StatusDesc
            AddHandler ele.Click, Sub()
                                      Select Case MyMsgBoxMarkdown($"提交者：{item.User}（{TimeUtils.GetTimeSpanString(item.Time - DateTime.Now, False)}）{vbCrLf}状态：{StatusDesc}{vbCrLf}{vbCrLf}{item.Content}",
                                               "#" & item.ID & " " & item.Title,
                                               Button2:="查看详情")
                                          Case 2
                                              OpenWebsite(item.Url)
                                      End Select
                                  End Sub
            If StatusDesc.StartsWithF("处理中") Then
                PanListProcessing.Children.Add(ele)
            ElseIf StatusDesc.Equals("等待处理") Then
                PanListWaitingProcess.Children.Add(ele)
            ElseIf StatusDesc.Equals("已完成") Then
                PanListCompleted.Children.Add(ele)
            ElseIf StatusDesc.Equals("已拒绝") Then
                PanListDecline.Children.Add(ele)
            ElseIf StatusDesc.Equals("已忽略") Then
                PanListIgnored.Children.Add(ele)
            ElseIf StatusDesc.Equals("已确认，等待社区开发者接管该内容的处理") Then
                PanListWait.Children.Add(ele)
            ElseIf StatusDesc.Equals("近期不计划制作此功能") Then
                PanListPause.Children.Add(ele)
            ElseIf StatusDesc.Equals("即将开工的内容") Then
                PanListUpnext.Children.Add(ele)
            End If
            PanContentDecline.Visibility = If(PanListDecline.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentCompleted.Visibility = If(PanListCompleted.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentWaitingProcess.Visibility = If(PanListWaitingProcess.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentProcessing.Visibility = If(PanListProcessing.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentIgnored.Visibility = If(PanListIgnored.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentWait.Visibility = If(PanListWait.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentPause.Visibility = If(PanListPause.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
            PanContentUpnext.Visibility = If(PanListUpnext.Children.Count.Equals(0), Visibility.Collapsed, Visibility.Visible)
        Next
    End Sub

    Private Sub Feedback_Click(sender As Object, e As MouseButtonEventArgs)
        PageOtherLeft.TryFeedback()
    End Sub
End Class
