Imports System.Windows.Interop
Imports System.Windows.Threading

Public Module ModMain

#Region "弹出提示"

    ''' <summary>
    ''' 提示信息的种类。
    ''' </summary>
    Public Enum HintType
        ''' <summary>
        ''' 信息，通常是蓝色的“i”。
        ''' </summary>
        ''' <remarks></remarks>
        Info
        ''' <summary>
        ''' 已完成，通常是绿色的“√”。
        ''' </summary>
        ''' <remarks></remarks>
        Finish
        ''' <summary>
        ''' 错误，通常是红色的“×”。
        ''' </summary>
        ''' <remarks></remarks>
        Critical
    End Enum
    Private Structure HintMessage
        Public Text As String
        Public Type As HintType
        Public Log As Boolean
    End Structure

    ''' <summary>
    ''' 等待弹出的提示列表。以 {String, HintType, Log As Boolean} 形式存储为数组。
    ''' </summary>
    Private HintWaiting As SafeList(Of HintMessage) = If(HintWaiting, New SafeList(Of HintMessage))
    ''' <summary>
    ''' 在窗口左下角弹出提示文本。
    ''' </summary>
    Public Sub Hint(Text As String, Optional Type As HintType = HintType.Info, Optional Log As Boolean = True)
        If HintWaiting Is Nothing Then HintWaiting = New SafeList(Of HintMessage)
        HintWaiting.Add(New HintMessage With {.Text = If(Text, ""), .Type = Type, .Log = Log})
    End Sub

    Private Sub HintTick()
        Try

            'Tag 存储了：{ 是否可以重用, Uuid }
            If Not HintWaiting.Any() Then Return
            Do While HintWaiting.Any
                ''清除空提示
                'If IsNothing(HintWaiting(0)) OrElse IsNothing(HintWaiting(0)(0)) Then
                '    HintWaiting.RemoveAt(0)
                '    Continue Do
                'End If
                Dim CurrentHint = HintWaiting(0)
                '去回车
                CurrentHint.Text = CurrentHint.Text.Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ")
                '超量提示直接忽略
                If FrmMain.PanHint.Children.Count >= 20 Then GoTo EndHint
                '检查是否有重复提示
                Dim DoubleStack As Border = Nothing
                For Each stack As Border In FrmMain.PanHint.Children
                    If stack.Tag(0) AndAlso CType(stack.Child, TextBlock).Text = CurrentHint.Text Then DoubleStack = stack
                Next
                '获取渐变颜色
                Dim TargetColor0, TargetColor1 As MyColor
                Dim Percent As Double = 0.3
                Select Case CurrentHint.Type
                    Case HintType.Info
                        TargetColor0 = New MyColor(215, 37, 155, 252)
                        TargetColor1 = New MyColor(215, 10, 142, 252)
                    Case HintType.Finish
                        TargetColor0 = New MyColor(215, 33, 177, 33)
                        TargetColor1 = New MyColor(215, 29, 160, 29)
                    Case Else 'HintType.Critical
                        TargetColor0 = New MyColor(215, 255, 53, 11)
                        TargetColor1 = New MyColor(215, 255, 43, 0)
                End Select
                If Not IsNothing(DoubleStack) Then
                    '有重复提示，且该提示的进入动画已播放
                    If Not AniIsRun("Hint Show " & DoubleStack.Tag(1)) Then
                        AniStop("Hint Hide " & DoubleStack.Tag(1))
                        Dim Delay As Double = (800 + MathClamp(CurrentHint.Text.Length, 5, 23) * 180) * AniSpeed
                        AniStart({
                            AaX(DoubleStack, -12 - DoubleStack.Margin.Left, 50,, New AniEaseOutFluent),
                            AaX(DoubleStack, -8, 50, 50, New AniEaseInFluent),
                            AaX(DoubleStack, 8, 50, 100, New AniEaseOutFluent),
                            AaX(DoubleStack, -8, 50, 150, New AniEaseInFluent),
                            AaDouble(Sub(i)
                                         Percent += i
                                         Dim Gradient As LinearGradientBrush = DoubleStack.Background
                                         Gradient.GradientStops(0).Color = TargetColor0 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                         Gradient.GradientStops(1).Color = TargetColor1 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                     End Sub, 0.7, 250),
                            AaX(DoubleStack, -50, 200, Delay, New AniEaseInFluent),
                            AaOpacity(DoubleStack, -1, 150, Delay),
                            AaCode(Sub() DoubleStack.Tag(0) = False, Delay),
                            AaHeight(DoubleStack, -26, 100,, New AniEaseOutFluent, True),
                            AaCode(Sub() FrmMain.PanHint.Children.Remove(DoubleStack), , True)
                      }, "Hint Hide " & DoubleStack.Tag(1))
                    End If
                Else
                    '准备控件
                    Dim NewHintControl As New Border With {.Tag = {True, GetUuid()}, .Margin = New Thickness(-70, 0, 20, 0), .Opacity = 0, .Height = 0, .HorizontalAlignment = HorizontalAlignment.Left, .CornerRadius = New CornerRadius(0, 6, 6, 0)}
                    NewHintControl.Background = New LinearGradientBrush(New GradientStopCollection(New List(Of GradientStop) From {
                        New GradientStop(TargetColor0 * Percent + New MyColor(255, 255, 255) * (1 - Percent), 0),
                        New GradientStop(TargetColor1 * Percent + New MyColor(255, 255, 255) * (1 - Percent), 1)}), 90)
                    NewHintControl.Child = New TextBlock With {.TextTrimming = TextTrimming.CharacterEllipsis, .FontSize = 13, .Text = CurrentHint.Text, .Foreground = New MyColor(255, 255, 255), .Margin = New Thickness(33, 5, 8, 5)}
                    'AddHandler NewHintControl.MouseLeftButtonDown, AddressOf HideAllHint
                    FrmMain.PanHint.Children.Add(NewHintControl)
                    '控件动画
                    Dim Animations As New List(Of AniData)
                    If FrmMain.PanHint.Children.Count > 1 Then
                        '已有提示
                        Animations.Add(AaHeight(NewHintControl, 26, 150, , New AniEaseOutFluent))
                    Else
                        '是唯一提示
                        NewHintControl.Height = 26
                    End If
                    '开始动画
                    Animations.AddRange({
                        AaX(NewHintControl, 30, 400, , New AniEaseOutElastic(AniEasePower.Weak)),
                        AaX(NewHintControl, 20, 200, , New AniEaseOutFluent),
                        AaOpacity(NewHintControl, 1, 100),
                        AaDouble(Sub(i)
                                     Percent += i
                                     Dim Gradient As LinearGradientBrush = NewHintControl.Background
                                     Gradient.GradientStops(0).Color = TargetColor0 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                     Gradient.GradientStops(1).Color = TargetColor1 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                 End Sub, 0.7, 250, 100)
                    })
                    AniStart(Animations, "Hint Show " & NewHintControl.Tag(1))
                    '结束动画
                    Dim Delay As Double = (800 + MathClamp(CurrentHint.Text.Length, 5, 23) * 180) * AniSpeed
                    AniStart({
                        AaX(NewHintControl, -50, 200, Delay, New AniEaseInFluent),
                        AaOpacity(NewHintControl, -1, 150, Delay),
                        AaCode(Sub() NewHintControl.Tag(0) = False, Delay),
                        AaHeight(NewHintControl, -26, 100,, New AniEaseOutFluent, True),
                        AaCode(Sub() FrmMain.PanHint.Children.Remove(NewHintControl), , True)
                    }, "Hint Hide " & NewHintControl.Tag(1))
                End If
                '结束处理
EndHint:
                If CurrentHint.Log Then Log("[UI] 弹出提示：" & CurrentHint.Text)
                HintWaiting.RemoveAt(0)
            Loop
        Catch ex As Exception
            Log(ex, "显示弹出提示失败", LogLevel.Normal)
        End Try
    End Sub
    Private Sub HideAllHint()
        For Each Control As Border In FrmMain.PanHint.Children
            Control.IsHitTestVisible = False
            AniStart({
                AaX(Control, -50, 200, , New AniEaseInFluent),
                AaOpacity(Control, -1, 150, , New AniEaseInFluent),
                AaCode(Sub() Control.Tag(0) = False),
                AaHeight(Control, -26, 100,, New AniEaseOutFluent, True),
                AaCode(Sub() FrmMain.PanHint.Children.Remove(Control), , True)
            }, "Hint Hide " & Control.Tag(1))
        Next
    End Sub

#End Region

#Region "弹窗"

    ''' <summary>
    ''' 存储弹窗信息的转换器。
    ''' </summary>
    Public Class MyMsgBoxConverter
        Public Type As MyMsgBoxType
        Public Title As String
        Public Text As String
        ''' <summary>
        ''' 输入模式：文本框的文本。
        ''' 选择模式：需要放进去的 List(Of MyListItem)。
        ''' 登录模式：登录步骤 1 中返回的 JSON。
        ''' </summary>
        Public Content As Object
        ''' <summary>
        ''' 输入模式：输入验证规则。
        ''' </summary>
        Public ValidateRules As ObjectModel.Collection(Of Validate)
        ''' <summary>
        ''' 输入模式：提示文本。
        ''' </summary>
        Public HintText As String = ""
        ''' <summary>
        ''' 有多个按钮时，是否给第一个按钮加高亮。
        ''' </summary>
        Public HighLight As Boolean
        Public Button1 As String = "确定"
        Public Button2 As String = ""
        Public Button3 As String = ""
        ''' <summary>
        ''' 点击第一个按钮将执行该方法，不关闭弹窗。
        ''' </summary>
        Public Button1Action As Action = Nothing
        ''' <summary>
        ''' 点击第二个按钮将执行该方法，不关闭弹窗。
        ''' </summary>
        Public Button2Action As Action = Nothing
        ''' <summary>
        ''' 点击第三个按钮将执行该方法，不关闭弹窗。
        ''' </summary>
        Public Button3Action As Action = Nothing
        Public IsWarn As Boolean = False
        Public ForceWait As Boolean = False
        Public WaitFrame As New DispatcherFrame(True)
        ''' <summary>
        ''' 弹窗是否已经关闭。
        ''' </summary>
        Public IsExited As Boolean = False
        ''' <summary>
        ''' 输入模式：输入的文本。若点击了 非 第一个按钮，则为 Nothing。
        ''' 选择模式：点击的按钮编号，从 1 开始。
        ''' 登录模式：字符串数组 {AccessToken, RefreshToken} 或一个 Exception。
        ''' </summary>
        Public Result As Object
    End Class
    Public Enum MyMsgBoxType
        Text
        [Select]
        Input
        Login
    End Enum

    ''' <summary>
    ''' 显示弹窗，返回点击按钮的编号（从 1 开始）。
    ''' </summary>
    ''' <param name="Title">弹窗的标题。</param>
    ''' <param name="Caption">弹窗的内容。</param>
    ''' <param name="Button1">显示的第一个按钮，默认为“确定”。</param>
    ''' <param name="Button2">显示的第二个按钮，默认为空。</param>
    ''' <param name="Button3">显示的第三个按钮，默认为空。</param>
    ''' <param name="Button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    ''' <param name="Button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    ''' <param name="Button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    ''' <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    Public Function MyMsgBox(Caption As String, Optional Title As String = "提示",
                             Optional Button1 As String = "确定", Optional Button2 As String = "", Optional Button3 As String = "",
                             Optional IsWarn As Boolean = False, Optional HighLight As Boolean = True, Optional ForceWait As Boolean = False,
                             Optional Button1Action As Action = Nothing, Optional Button2Action As Action = Nothing, Optional Button3Action As Action = Nothing) As Integer
        '将弹窗列入队列
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.Text, .Button1 = Button1, .Button2 = Button2, .Button3 = Button3, .Text = Caption, .IsWarn = IsWarn, .Title = Title, .HighLight = HighLight, .ForceWait = True, .Button1Action = Button1Action, .Button2Action = Button2Action, .Button3Action = Button3Action}
        WaitingMyMsgBox.Add(Converter)
        If RunInUi() Then
            '若为 UI 线程，立即执行弹窗刻， 避免快速（连点器）点击时多次弹窗
            MyMsgBoxTick()
        End If
        If Button2.Length > 0 OrElse ForceWait Then
            '若有多个按钮则开始等待
            If FrmMain Is Nothing OrElse FrmMain.PanMsg Is Nothing AndAlso RunInUi() Then
                '主窗体尚未加载，用老土的弹窗来替代
                WaitingMyMsgBox.Remove(Converter)
                If Button2.Length > 0 Then
                    Dim RawResult As MsgBoxResult = MsgBox(Caption, If(Button3.Length > 0, MsgBoxStyle.YesNoCancel, MsgBoxStyle.YesNo) + If(IsWarn, MsgBoxStyle.Critical, MsgBoxStyle.Question), Title)
                    Select Case RawResult
                        Case MsgBoxResult.Yes
                            Converter.Result = 1
                        Case MsgBoxResult.No
                            Converter.Result = 2
                        Case MsgBoxResult.Cancel
                            Converter.Result = 3
                    End Select
                Else
                    MsgBox(Caption, MsgBoxStyle.OkOnly + If(IsWarn, MsgBoxStyle.Critical, MsgBoxStyle.Question), Title)
                    Converter.Result = 1
                End If
                Log("[Control] 主窗体加载完成前出现意料外的等待弹窗：" & Button1 & "," & Button2 & "," & Button3, LogLevel.Debug)
            Else
                Try
                    FrmMain.DragStop()
                    ComponentDispatcher.PushModal()
                    Dispatcher.PushFrame(Converter.WaitFrame)
                Finally
                    ComponentDispatcher.PopModal()
                End Try
            End If
            Log("[Control] 普通弹框返回：" & If(Converter.Result, "null"))
            Return Converter.Result
        Else
            '不进行等待，直接返回
            Return 1
        End If
    End Function
    ''' <summary>
    ''' 显示输入框并返回输入的文本。若点击第二个按钮，则返回 Nothing。
    ''' </summary>
    ''' <param name="Title">弹窗的标题。</param>
    ''' <param name="ValidateRules">文本框的输入检测。</param>
    ''' <param name="Text">弹窗的介绍文本。</param>
    ''' <param name="DefaultInput">文本框的默认内容。</param>
    ''' <param name="HintText">文本框的提示内容。</param>
    ''' <param name="Button1">显示的第一个按钮，默认为“确定”。</param>
    ''' <param name="Button2">显示的第二个按钮，默认为“取消”。</param>
    ''' <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    Public Function MyMsgBoxInput(Title As String, Optional Text As String = "", Optional DefaultInput As String = "", Optional ValidateRules As ObjectModel.Collection(Of Validate) = Nothing, Optional HintText As String = "", Optional Button1 As String = "确定", Optional Button2 As String = "取消", Optional IsWarn As Boolean = False) As String
        '将弹窗列入队列
        Dim Converter As New MyMsgBoxConverter With {.Text = Text, .HintText = HintText, .Type = MyMsgBoxType.Input, .ValidateRules = If(ValidateRules, New ObjectModel.Collection(Of Validate)), .Button1 = Button1, .Button2 = Button2, .Content = DefaultInput, .IsWarn = IsWarn, .Title = Title}
        WaitingMyMsgBox.Add(Converter)
        '虽然我也不知道这是啥但是能用就成了 :)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Log("[Control] 输入弹框返回：" & If(Converter.Result, "null"))
        Return Converter.Result
    End Function
    ''' <summary>
    ''' 显示选择框并返回选择的第几项（从 0 开始）。若点击第二个按钮，则返回 Nothing。
    ''' </summary>
    ''' <param name="Title">弹窗的标题。</param>
    ''' <param name="Button1">显示的第一个按钮，默认为 “确定”。</param>
    ''' <param name="Button2">显示的第二个按钮，默认为空。</param>
    ''' <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    Public Function MyMsgBoxSelect(Selections As List(Of IMyRadio), Optional Title As String = "提示", Optional Button1 As String = "确定", Optional Button2 As String = "", Optional IsWarn As Boolean = False) As Integer?
        '将弹窗列入队列
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.Select, .Button1 = Button1, .Button2 = Button2, .Content = Selections, .IsWarn = IsWarn, .Title = Title}
        WaitingMyMsgBox.Add(Converter)
        '虽然我也不知道这是啥但是能用就成了 :)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Log("[Control] 选择弹框返回：" & If(Converter.Result, "null"))
        Return Converter.Result
    End Function

    ''' <summary>
    ''' 等待显示的弹窗。
    ''' </summary>
    Public WaitingMyMsgBox As List(Of MyMsgBoxConverter) = If(WaitingMyMsgBox, New List(Of MyMsgBoxConverter))
    Public Sub MyMsgBoxTick()
        Try
            If FrmMain Is Nothing OrElse FrmMain.PanMsg Is Nothing OrElse FrmMain.WindowState = WindowState.Minimized Then Return
            If FrmMain.PanMsg.Children.Count > 0 Then
                '弹窗中
                FrmMain.PanMsg.Visibility = Visibility.Visible
            ElseIf WaitingMyMsgBox.Any Then
                '没有弹窗，显示一个等待的弹窗
                FrmMain.PanMsg.Visibility = Visibility.Visible
                Select Case CType(WaitingMyMsgBox(0), MyMsgBoxConverter).Type
                    Case MyMsgBoxType.Input
                        FrmMain.PanMsg.Children.Add(New MyMsgInput(WaitingMyMsgBox(0)))
                    Case MyMsgBoxType.Select
                        FrmMain.PanMsg.Children.Add(New MyMsgSelect(WaitingMyMsgBox(0)))
                    Case MyMsgBoxType.Text
                        FrmMain.PanMsg.Children.Add(New MyMsgText(WaitingMyMsgBox(0)))
                    Case MyMsgBoxType.Login
                        FrmMain.PanMsg.Children.Add(New MyMsgLogin(WaitingMyMsgBox(0)))
                End Select
                WaitingMyMsgBox.RemoveAt(0)
            Else
                '没有弹窗，没有等待的弹窗
                If Not FrmMain.PanMsg.Visibility = Visibility.Collapsed Then FrmMain.PanMsg.Visibility = Visibility.Collapsed
            End If
        Catch ex As Exception
            Log(ex, "处理等待中的弹窗失败", LogLevel.Feedback)
        End Try
    End Sub

#End Region

#Region "页面声明"
    '在最后进行页面声明，避免颜色尚未加载完毕

    '窗体声明
    Public FrmMain As FormMain
    Public FrmStart As SplashScreen

    '页面声明（出于单元测试考虑，初始化页面已转入 FormMain 中）
    Public FrmLaunchLeft As PageLaunchLeft
    Public FrmLaunchRight As PageLaunchRight
    Public FrmSelectLeft As PageSelectLeft
    Public FrmSelectRight As PageSelectRight
    Public FrmSpeedLeft As PageSpeedLeft
    Public FrmSpeedRight As PageSpeedRight

    '联机页面声明
    Public FrmLinkLeft As PageLinkLeft
    Public FrmLinkIoi As PageLinkIoi
    Public FrmLinkHiper As PageLinkHiper
    Public FrmLinkHelp As PageOtherHelpDetail
    Public FrmLinkFeedback As PageLinkFeedback

    '下载页面声明
    Public FrmDownloadLeft As PageDownloadLeft
    Public FrmDownloadInstall As PageDownloadInstall
    Public FrmDownloadClient As PageDownloadClient
    Public FrmDownloadOptiFine As PageDownloadOptiFine
    Public FrmDownloadLiteLoader As PageDownloadLiteLoader
    Public FrmDownloadForge As PageDownloadForge
    Public FrmDownloadNeoForge As PageDownloadNeoForge
    Public FrmDownloadFabric As PageDownloadFabric
    Public FrmDownloadMod As PageDownloadMod
    Public FrmDownloadPack As PageDownloadPack
    Public FrmDownloadDataPack As PageDownloadDataPack
    Public FrmDownloadShader As PageDownloadShader
    Public FrmDownloadResourcePack As PageDownloadResourcePack

    '设置页面声明
    Public FrmSetupLeft As PageSetupLeft
    Public FrmSetupLaunch As PageSetupLaunch
    Public FrmSetupUI As PageSetupUI
    Public FrmSetupSystem As PageSetupSystem
    Public FrmSetupLink As PageSetupLink

    '其他页面声明
    Public FrmOtherLeft As PageOtherLeft
    Public FrmOtherHelp As PageOtherHelp
    Public FrmOtherAbout As PageOtherAbout
    Public FrmOtherTest As PageOtherTest

    '登录页面声明
    Public FrmLoginLegacy As PageLoginLegacy
    Public FrmLoginNide As PageLoginNide
    Public FrmLoginNideSkin As PageLoginNideSkin
    Public FrmLoginAuth As PageLoginAuth
    Public FrmLoginAuthSkin As PageLoginAuthSkin
    Public FrmLoginMs As PageLoginMs
    Public FrmLoginMsSkin As PageLoginMsSkin

    '版本设置页面声明
    Public FrmVersionLeft As PageVersionLeft
    Public FrmVersionOverall As PageVersionOverall
    Public FrmVersionMod As PageVersionMod
    Public FrmVersionModDisabled As PageVersionModDisabled
    Public FrmVersionSetup As PageVersionSetup
    Public FrmVersionExport As PageVersionExport

    '资源信息分页声明
    Public FrmDownloadCompDetail As PageDownloadCompDetail

#End Region

#Region "帮助"

    Public Class HelpEntry
        ''' <summary>
        ''' 原始信息路径。用于刷新。
        ''' </summary>
        Public RawPath As String

        '基础

        ''' <summary>
        ''' 显示标题。
        ''' </summary>
        Public Title As String
        ''' <summary>
        ''' 显示描述。
        ''' </summary>
        Public Desc As String
        ''' <summary>
        ''' 检索关键字。
        ''' </summary>
        Public Search As String
        ''' <summary>
        ''' 用于分类的标签列表。
        ''' </summary>
        Public Types As List(Of String)

        '显示（可选）

        ''' <summary>
        ''' 帮助项的自定义图标。可能为 Nothing。
        ''' </summary>
        Public Logo As String = Nothing
        ''' <summary>
        ''' 是否显示在搜索结果。默认为 True。
        ''' </summary>
        Public ShowInSearch As Boolean = True
        ''' <summary>
        ''' 是否在公开版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        ''' </summary>
        Public ShowInPublic As Boolean = True
        ''' <summary>
        ''' 是否在快照版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        ''' </summary>
        Public ShowInSnapshot As Boolean = True

        '动作

        ''' <summary>
        ''' 是否为 “执行事件”。
        ''' </summary>
        Public IsEvent As Boolean
        Public EventType As String
        Public EventData As String
        ''' <summary>
        ''' 若非执行事件，其对应的 .xaml 本地文件内容。
        ''' </summary>
        Public XamlContent As String

        '转换

        ''' <summary>
        ''' 从文件初始化 HelpEntry 对象，失败会抛出异常。
        ''' </summary>
        Public Sub New(FilePath As String)
            RawPath = FilePath
            Dim JsonData As JObject = GetJson(HelpArgumentReplace(ReadFile(FilePath)))
            If JsonData Is Nothing Then Throw New FileNotFoundException("未找到帮助文件：" & FilePath, FilePath)
            '加载常规信息
            If JsonData("Title") IsNot Nothing Then
                Title = JsonData("Title")
            Else
                Throw New ArgumentException("未找到 Title 项")
            End If
            Desc = If(JsonData("Description"), "")
            Search = If(JsonData("Keywords"), "")
            Logo = JsonData("Logo") '为保持 Nothing，不要加 If
            ShowInSearch = If(JsonData("ShowInSearch"), ShowInSearch)
            ShowInPublic = If(JsonData("ShowInPublic"), ShowInPublic)
            ShowInSnapshot = If(JsonData("ShowInSnapshot"), ShowInSnapshot)
            Types = New List(Of String)
            For Each NameOfType In If(JsonData("Types"), GetJson("[]"))
                Types.Add(NameOfType)
            Next
            '加载事件信息
            If If(JsonData("IsEvent"), False) Then
                EventType = JsonData("EventType")
                If EventType Is Nothing Then Throw New ArgumentException("未找到 EventType 项")
                EventData = If(JsonData("EventData"), "")
                IsEvent = True
            Else
                Dim XamlAddress As String = FilePath.ToLower.Replace(".json", ".xaml")
                If File.Exists(XamlAddress) Then
                    XamlContent = ReadFile(XamlAddress)
                    IsEvent = False
                Else
                    Throw New FileNotFoundException("未找到帮助条目 .json 对应的 .xaml 文件（" & XamlAddress & "）")
                End If
            End If
        End Sub
        ''' <summary>
        ''' 获取该 HelpEntry 对应的 MyListItem。
        ''' </summary>
        Public Function ToListItem() As MyListItem
            Return SetToListItem(New MyListItem)
        End Function
        ''' <summary>
        ''' 将属性设置入一个现有的 ListItem。
        ''' </summary>
        Public Function SetToListItem(Item As MyListItem) As MyListItem
            Dim Logo As String
            If IsEvent Then
                If EventType = "弹出窗口" Then
                    Logo = PathImage & "Blocks/GrassPath.png"
                Else
                    Logo = PathImage & "Blocks/CommandBlock.png"
                End If
            Else
                Logo = PathImage & "Blocks/Grass.png"
            End If
            '设置属性
            With Item
                .SnapsToDevicePixels = True
                .Title = Title
                .Info = Desc
                .Logo = If(Me.Logo, Logo)
                .Height = 42
                .Type = MyListItem.CheckType.Clickable
                .Tag = Me
                .EventType = Nothing
                .EventData = Nothing
            End With
            '项目的点击事件
            AddHandler Item.Click, Sub(sender, e) PageOtherHelp.OnItemClick(sender.Tag)
            Return Item
        End Function

    End Class

    Public HelpLoader As New LoaderTask(Of Integer, List(Of HelpEntry))("Help Page", AddressOf HelpLoad,, ThreadPriority.BelowNormal)
    Private ReadOnly HelpLoadLock As New Object
    ''' <summary>
    ''' 初始化帮助列表对象。
    ''' </summary>
    Private Sub HelpLoad(Loader As LoaderTask(Of Integer, List(Of HelpEntry)))
        SyncLock HelpLoadLock '避免重复解压文件导致出错
            Try

                '解压内置文件
                HelpTryExtract()

                '遍历文件
                Dim FileList As New List(Of String)
                Try
                    Dim IgnoreList As New List(Of String)
                    '读取自定义文件
                    If Directory.Exists(Path & "PCL\Help\") Then
                        For Each File In EnumerateFiles(Path & "PCL\Help\")
                            Select Case File.Extension.ToLower
                                Case ".helpignore"
                                    '加载忽略列表
                                    Log("[Help] 发现 .helpignore 文件：" & File.FullName)
                                    For Each Line In ReadFile(File.FullName).Split(vbCrLf.ToCharArray)
                                        Dim RealString As String = Line.BeforeFirst("#").Trim
                                        If String.IsNullOrWhiteSpace(RealString) Then Continue For
                                        IgnoreList.Add(RealString)
                                        If ModeDebug Then Log("[Help]  > " & RealString)
                                    Next
                                Case ".json"
                                    FileList.Add(File.FullName)
                            End Select
                        Next
                    End If
                    Log("[Help] 已扫描 PCL 文件夹下的帮助文件，目前总计 " & FileList.Count & " 条")
                    '读取自带文件
                    For Each File In EnumerateFiles(PathTemp & "Help")
                        '跳过非 json 文件与以 . 开头的文件夹
                        If File.Extension.ToLower <> ".json" OrElse File.Directory.FullName.Replace(PathTemp & "Help", "").Contains("\.") Then Continue For
                        '检查忽略列表
                        Dim RealPath As String = File.FullName.Replace(PathTemp & "Help\", "")
                        For Each Ignore In IgnoreList
                            If RegexCheck(RealPath, Ignore) Then
                                If ModeDebug Then Log("[Help] 已忽略 " & RealPath & "：" & Ignore)
                                GoTo NextFile
                            End If
                        Next
                        FileList.Add(File.FullName)
NextFile:
                    Next
                    Log("[Help] 已扫描缓存文件夹下的帮助文件，目前总计 " & FileList.Count & " 条")
                Catch ex As Exception
                    Log(ex, "检查帮助文件夹失败", LogLevel.Msgbox)
                End Try
                If Loader.IsAborted Then Return

                '将文件实例化
                Dim Dict As New List(Of HelpEntry)
                For Each FilePath As String In FileList
                    Try
                        Dim Entry As New HelpEntry(FilePath)
                        Dict.Add(Entry)
                        If ModeDebug Then Log("[Help] 已加载的帮助条目：" & Entry.Title & " ← " & FilePath)
                    Catch ex As Exception
                        Log(ex, "初始化帮助条目失败（" & FilePath & "）", LogLevel.Msgbox)
                    End Try
                Next

                '回设
                If Not Dict.Any() Then Throw New Exception("未找到可用的帮助；若不需要帮助页面，可以在 设置 → 个性化 → 功能隐藏 中将其隐藏")
                If Loader.IsAborted Then Return
                Loader.Output = Dict

            Catch ex As Exception
                Log(ex, "帮助列表初始化失败")
                Throw
            End Try
        End SyncLock
    End Sub
    ''' <summary>
    ''' 尝试解压内置帮助文件。
    ''' </summary>
    Public Sub HelpTryExtract()
        If Setup.Get("SystemHelpVersion") <> VersionCode OrElse Not File.Exists(PathTemp & "Help\启动器\备份设置.xaml") Then
            DeleteDirectory(PathTemp & "Help")
            Directory.CreateDirectory(PathTemp & "Help")
            WriteFile(PathTemp & "Cache\Help.zip", GetResources("Help"))
            ExtractFile(PathTemp & "Cache\Help.zip", PathTemp & "Help", Encoding.UTF8)
            Setup.Set("SystemHelpVersion", VersionCode)
            Log("[Help] 已解压内置帮助文件，目前状态：" & File.Exists(PathTemp & "Help\启动器\备份设置.xaml"), LogLevel.Debug)
        End If
    End Sub
    ''' <summary>
    ''' 对帮助文件约定的替换标记进行处理，如果遇到需要转义的字符会进行转义。
    ''' </summary>
    Public Function HelpArgumentReplace(Xaml As String) As String
        Dim Result = Xaml.Replace("{path}", EscapeXML(Path))
        Result = Result.RegexReplaceEach("\{hint\}", Function() EscapeXML(PageOtherTest.GetRandomHint()))
        Result = Result.RegexReplaceEach("\{cave\}", Function() EscapeXML(PageOtherTest.GetRandomCave()))
        Return Result
    End Function

#End Region

#Region "愚人节"

    Public IsAprilEnabled As Boolean = Date.Now.Month = 4 AndAlso Date.Now.Day = 1
    Public IsAprilGiveup As Boolean = False
    Private AprilSpeed As New Vector(0, 0)
    Private AprilIdieCount As Integer = 0, AprilMousePosLast As New Point(0, 0)
    Private AprilDistance As Integer = 0
    Private Sub TimerFool()
        Try
            If FrmLaunchLeft Is Nothing OrElse FrmLaunchLeft.AprilPosTrans Is Nothing OrElse FrmMain.lastMouseArg Is Nothing Then Return
            If IsAprilGiveup OrElse FrmMain.PageCurrent <> FormMain.PageType.Launch OrElse AniControlEnabled <> 0 OrElse Not FrmLaunchLeft.BtnLaunch.IsLoaded Then Return

            '计算是否空闲
            Dim MousePos = FrmMain.lastMouseArg.GetPosition(FrmMain)
            If MousePos = AprilMousePosLast Then
                AprilIdieCount += 1
            Else
                AprilMousePosLast = MousePos
                AprilIdieCount = 0
            End If
            '计算躲避移动
            Dim Direction As Vector
            Dim Distance As Double
            Dim ButtonWidth = FrmLaunchLeft.BtnLaunch.ActualWidth / 2, ButtonHeight = FrmLaunchLeft.BtnLaunch.ActualHeight / 2
            Dim Vec As Vector = FrmMain.lastMouseArg.GetPosition(FrmLaunchLeft.BtnLaunch) - New Vector(ButtonWidth, ButtonHeight)
            Dim Dir As New Vector(Vec.X, Vec.Y)
            Dir.Normalize()
            Direction = -Dir
            Distance = New Vector(Math.Max(0, Math.Abs(Vec.X) - ButtonWidth), Math.Max(0, Math.Abs(Vec.Y) - ButtonHeight)).Length
            Dim BreathScale = Math.Sin(Timer150Count / 37.5 * Math.PI)
            Dim Acc = Math.Max(0, BreathScale * 0.25 - 0.65 - Math.Log((Distance + 0.4) / 200)) * Direction '加速度
            '计算回归移动
            If AprilIdieCount >= 64 * 5 Then
                Dim SafeDist As Vector = FrmMain.lastMouseArg.GetPosition(FrmMain.PanMain) - New Vector(ButtonWidth, FrmMain.PanMain.ActualHeight - ButtonHeight * 3)
                Dim Back As New Vector(FrmLaunchLeft.AprilPosTrans.X, FrmLaunchLeft.AprilPosTrans.Y)
                If SafeDist.Length > 250 AndAlso Back.Length > 0.4 Then
                    Acc -= Back * 0.0005
                    Back.Normalize()
                    Acc -= Back * 0.15
                End If
            End If
            '回到边界
            Dim Relative As Point = FrmLaunchLeft.BtnLaunch.TranslatePoint(New Point(0, 0), FrmMain.PanForm)
            If Relative.X < -ButtonWidth * 2 Then
                FrmLaunchLeft.AprilPosTrans.X += FrmMain.PanForm.ActualWidth + ButtonWidth * 2 '离开左边界
                AprilSpeed.X -= 80
                If Relative.Y < 0 Then
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5
                ElseIf Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2 Then
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5
                End If
            ElseIf Relative.X > FrmMain.PanForm.ActualWidth Then
                FrmLaunchLeft.AprilPosTrans.X -= FrmMain.PanForm.ActualWidth + ButtonWidth * 2 '离开右边界
                AprilSpeed.X += 80
                If Relative.Y < 0 Then
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5
                ElseIf Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2 Then
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5
                End If
            ElseIf Relative.Y < -ButtonHeight * 2 Then
                FrmLaunchLeft.AprilPosTrans.Y += FrmMain.PanForm.ActualHeight + ButtonHeight * 2 '离开上边界
                AprilSpeed.Y -= 25
                If Relative.X < 0 Then
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2
                ElseIf Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2 Then
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2
                End If
            ElseIf Relative.Y > FrmMain.PanForm.ActualHeight Then
                FrmLaunchLeft.AprilPosTrans.Y -= FrmMain.PanForm.ActualHeight + ButtonHeight * 2 '离开下边界
                AprilSpeed.Y += 25
                If Relative.X < 0 Then
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2
                ElseIf Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2 Then
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2
                End If
            End If
            '移动
            AprilSpeed = AprilSpeed * 0.8 + Acc
            Dim SpeedValue = Math.Min(60, AprilSpeed.Length)
            If SpeedValue < 0.01 Then Return
            AprilSpeed.Normalize()
            AprilSpeed *= SpeedValue
            AprilDistance += SpeedValue
            FrmLaunchLeft.AprilPosTrans.X += AprilSpeed.X
            FrmLaunchLeft.AprilPosTrans.Y += AprilSpeed.Y
            '大小改变
            FrmLaunchLeft.AprilScaleTrans.ScaleX = MathClamp(1 - (Math.Abs(Direction.X) - Math.Abs(Direction.Y)) * (SpeedValue / 160), 0.2, 1.8)
            FrmLaunchLeft.AprilScaleTrans.ScaleY = MathClamp(1 - (Math.Abs(Direction.Y) - Math.Abs(Direction.X)) * (SpeedValue / 100), 0.2, 1.8)
            '放弃提示
            If AprilDistance > 4000 Then
                AprilDistance = -4000
                Select Case RandomInteger(0, 3)
                    Case 0
                        Hint("放弃吧！只需要点一下右下角的小白旗……")
                    Case 1
                        Hint("看到右下角的那面小白旗了吗？")
                    Case 2
                        Hint("这里建议点一下右下角的小白旗投降呢.jpg")
                    Case 3
                        Hint("右下角的小白旗永远等着你……")
                End Select
            End If

        Catch ex As Exception
            Log(ex, "愚人节移动出错", LogLevel.Feedback)
        End Try
    End Sub

#End Region

#Region "系统"

    ''' <summary>
    ''' 把某个 PCL 窗口拖到最前面。
    ''' </summary>
    Public Sub ShowWindowToTop(Handle As IntPtr)
        Try
            PostMessage(Handle, 400 * 16 + 2, 0, 0)
            SetForegroundWindow(Handle) '不在这里放不行，神秘 WinAPI，建议别动
        Catch ex As Exception
            Log(ex, "设置窗口置顶失败", LogLevel.Hint)
        End Try
    End Sub
    Public Declare Function FindWindow Lib "user32" Alias "FindWindowA" (ClassName As String, WindowName As String) As IntPtr
    Public Declare Function SetForegroundWindow Lib "user32" (hWnd As IntPtr) As Integer
    Private Declare Function PostMessage Lib "user32" Alias "PostMessageA" (hWnd As IntPtr, msg As UInteger, wParam As Long, lParam As Long) As Boolean

    ''' <summary>
    ''' 将特定程序设置为使用高性能显卡启动。
    ''' 如果失败，则抛出异常。
    ''' </summary>
    Public Sub SetGPUPreference(Executeable As String)
        Const REG_KEY As String = "Software\Microsoft\DirectX\UserGpuPreferences"
        Const REG_VALUE As String = "GpuPreference=2;"
        '查看现有设置
        Using ReadOnlyKey = My.Computer.Registry.CurrentUser.OpenSubKey(REG_KEY, False)
            If ReadOnlyKey IsNot Nothing Then
                Dim CurrentValue = ReadOnlyKey.GetValue(Executeable)
                If REG_VALUE = CurrentValue?.ToString() Then
                    Log($"[System] 无需调整显卡设置：{Executeable}")
                    Return
                End If
            Else
                '创建父级键
                Log($"[System] 需要创建显卡设置的父级键")
                My.Computer.Registry.CurrentUser.CreateSubKey(REG_KEY)
            End If
        End Using
        '写入新设置
        Using WriteKey = My.Computer.Registry.CurrentUser.OpenSubKey(REG_KEY, True)
            WriteKey.SetValue(Executeable, REG_VALUE)
            Log($"[System] 已调整显卡设置：{Executeable}")
        End Using
    End Sub

#End Region

#Region "任务缓存"

    Private IsTaskTempCleared As Boolean = False
    Private IsTaskTempClearing As Boolean = False

    ''' <summary>
    ''' 尝试清理任务缓存文件夹。
    ''' 在整次运行中只会实际清理一次。
    ''' </summary>
    Public Sub TryClearTaskTemp()
        If Not IsTaskTempCleared Then
            IsTaskTempCleared = True
            IsTaskTempClearing = True
            Try
                Log("[System] 开始清理任务缓存文件夹")
                DeleteDirectory($"{OsDrive}ProgramData\PCL\TaskTemp\")
                DeleteDirectory($"{PathTemp}TaskTemp\")
                Log("[System] 已清理任务缓存文件夹")
            Catch ex As Exception
                Log(ex, "清理任务缓存文件夹失败")
            Finally
                IsTaskTempClearing = False
            End Try
        ElseIf IsTaskTempClearing Then
            '等待另一个清理步骤完成
            Do While IsTaskTempClearing
                Thread.Sleep(1)
            Loop
        End If
    End Sub

    ''' <summary>
    ''' 申请一个可用于任务缓存的临时文件夹，以 \ 结尾。这些文件夹无需进行后续清理。
    ''' 若所有缓存位置均没有权限，会抛出异常。
    ''' </summary>
    ''' <param name="RequireNonSpace">是否要求路径不包含空格。</param>
    Public Function RequestTaskTempFolder(Optional RequireNonSpace As Boolean = False) As String
        TryClearTaskTemp()
        Dim ResultFolder As String
        Try
            ResultFolder = $"{PathTemp}TaskTemp\{GetUuid()}-{RandomInteger(0, 1000000)}\"
            If RequireNonSpace AndAlso ResultFolder.Contains(" ") Then Exit Try '带空格
            Directory.CreateDirectory(ResultFolder)
            CheckPermissionWithException(ResultFolder)
            Return ResultFolder
        Catch
        End Try
        '使用备用路径
        ResultFolder = $"{OsDrive}ProgramData\PCL\TaskTemp\{GetUuid()}-{RandomInteger(0, 1000000)}\"
        Directory.CreateDirectory(ResultFolder)
        CheckPermission(ResultFolder)
        Return ResultFolder
    End Function

#End Region

    Public DragControl = Nothing
    Private Timer4Count As Integer = 0
    Private Timer150Count As Integer = 0
    Private Sub TimerMain()
        Try
#Region "每 50ms 执行一次的代码"
            HintTick()
            MyMsgBoxTick()
            FrmMain.DragTick()
            LoaderTaskbarProgressRefresh()
            If ThemeDontClick = 2 Then ThemeRefresh()
#End Region
        Catch ex As Exception
            Log(ex, "短程主时钟执行异常", LogLevel.Critical)
        End Try
        Timer4Count += 1
        If Timer4Count = 4 Then
            Timer4Count = 0
            Try
#Region "每 250ms 执行一次的代码"
                If ThemeNow = 12 Then ThemeRefresh()
#End Region
            Catch ex As Exception
                Log(ex, "中程主时钟执行异常", LogLevel.Debug)
            End Try
        End If
        Timer150Count += 1
        If Timer150Count = 150 Then
            Timer150Count = 0
            Try
#Region "每 7.5s 执行一次的代码"
                If FrmMain.BtnExtraApril_ShowCheck AndAlso AprilDistance <> 0 Then FrmMain.BtnExtraApril.Ribble()
                '以未知原因窗口被丢到一边去的修复（Top、Left = -25600），还有 #745
                RunInUi(
                Sub()
                    If Not FrmMain.Hidden Then
                        If FrmMain.Top < -9000 Then FrmMain.Top = 100
                        If FrmMain.Left < -9000 Then FrmMain.Left = 100 '窗口拉至最大时 Left = -18.8
                    End If
                End Sub)
#End Region
            Catch ex As Exception
                Log(ex, "长程主时钟执行异常", LogLevel.Critical)
            End Try
        End If
    End Sub
    Public Sub TimerMainStart()
        RunInNewThread(
        Sub()
            Try
                Do While True
                    RunInUiWait(AddressOf TimerMain)
                    Thread.Sleep(50 * 0.98)
                Loop
            Catch ex As Exception
                Log(ex, "程序主时钟出错", LogLevel.Feedback)
            End Try
        End Sub, "Timer Main")
        If Not IsAprilEnabled Then Return
        RunInNewThread(
        Sub()
            Try
                Dim LastTime = My.Computer.Clock.TickCount
                Do While True
                    If LastTime <> My.Computer.Clock.TickCount Then
                        LastTime = My.Computer.Clock.TickCount
                        RunInUiWait(AddressOf TimerFool)
                    End If
                    Thread.Sleep(1)
                Loop
            Catch ex As Exception
                Log(ex, "愚人节主时钟出错", LogLevel.Feedback)
            End Try
        End Sub, "Timer Main Fool")
    End Sub

End Module
