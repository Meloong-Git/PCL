Public Class MyCompItem

#Region "基础属性"
    Public Uuid As Integer = GetUuid()

    'Logo
    Public Property Logo As String
        Get
            Return PathLogo.Source
        End Get
        Set(value As String)
            PathLogo.Source = value
        End Set
    End Property

    '标题
    Public Property Title As String
        Get
            Return LabTitle.Text
        End Get
        Set(value As String)
            If LabTitle.Text = value Then Return
            LabTitle.Text = value
        End Set
    End Property

    '副标题
    Public Property SubTitle As String
        Get
            Return If(LabTitleRaw?.Text, "")
        End Get
        Set(value As String)
            If LabTitleRaw.Text = value Then Return
            LabTitleRaw.Text = value
            LabTitleRaw.Visibility = If(value = "", Visibility.Collapsed, Visibility.Visible)
        End Set
    End Property

    '描述
    Public Property Description As String
        Get
            Return LabInfo.Text
        End Get
        Set(value As String)
            If LabInfo.Text = value Then Return
            LabInfo.Text = value
        End Set
    End Property
    '指向时扩展描述
    Private Sub LabInfo_MouseEnter(sender As Object, e As MouseEventArgs) Handles LabInfo.MouseEnter
        If IsTextTrimmed(LabInfo) Then
            ToolTipInfo.Content = LabInfo.Text
            ToolTipInfo.Width = LabInfo.ActualWidth + 25
            LabInfo.ToolTip = ToolTipInfo
        Else
            LabInfo.ToolTip = Nothing
        End If
    End Sub
    Private Function IsTextTrimmed(textBlock As TextBlock) As Boolean
        Dim typeface As New Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch)
        Dim formattedText As New FormattedText(textBlock.Text, Thread.CurrentThread.CurrentCulture, textBlock.FlowDirection, typeface, textBlock.FontSize, textBlock.Foreground, DPI)
        Return formattedText.Width > textBlock.ActualWidth
    End Function

    'Tag
    Public WriteOnly Property Tags As List(Of String)
        Set(value As List(Of String))
            PanTags.Children.Clear()
            PanTags.Visibility = If(value.Any(), Visibility.Visible, Visibility.Collapsed)
            For Each TagText In value
                Dim NewTag = GetObjectFromXML(
                "<Border xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                         Background=""#11000000"" Padding=""3,1"" CornerRadius=""3"" Margin=""0,0,3,0"" 
                         SnapsToDevicePixels=""True"" UseLayoutRounding=""False"">
                   <TextBlock Text=""" & TagText & """ Foreground=""#868686"" FontSize=""11"" />
                </Border>")
                PanTags.Children.Add(NewTag)
            Next
        End Set
    End Property

#End Region

#Region "点击"

    '触发点击事件
    Public Event Click(sender As Object, e As MouseButtonEventArgs)
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonUp
        If IsMouseDown Then
            RaiseEvent Click(sender, e)
            If e.Handled Then Return
            Log("[Control] 按下资源工程列表项：" & LabTitle.Text)
        End If
    End Sub
    Private Sub MyCompItem_Click(sender As MyCompItem, e As EventArgs) Handles Me.Click
        '记录当前展开的卡片标题（#2712）
        Dim Titles As New List(Of String)
        If FrmMain.PageCurrent.Page = FormMain.PageType.CompDetail Then
            For Each Card As MyCard In FrmDownloadCompDetail.PanResults.Children
                If Card.Title <> "" AndAlso Not Card.IsSwaped Then Titles.Add(Card.Title)
            Next
            Log("[Comp] 记录当前已展开的卡片：" & String.Join("、", Titles))
            FrmMain.PageCurrent.Additional(1) = Titles
        End If
        '打开详情页
        Dim TargetType As CompType
        Dim TargetVersion As String = Nothing
        Dim TargetLoader As CompModLoaderType = CompModLoaderType.Any
        If FrmMain.PageCurrent.Page = FormMain.PageType.Download Then
            '从下载页进入
            Select Case FrmMain.PageCurrentSub
                Case FormMain.PageSubType.DownloadMod
                    TargetType = CompType.Mod
                    TargetVersion = FrmDownloadMod.Content.Loader.Input.GameVersion
                    TargetLoader = FrmDownloadMod.Content.Loader.Input.ModLoader
                Case FormMain.PageSubType.DownloadPack
                    TargetType = CompType.ModPack
                    TargetVersion = FrmDownloadPack.Content.Loader.Input.GameVersion
                Case FormMain.PageSubType.DownloadDataPack
                    TargetType = CompType.DataPack
                    TargetVersion = FrmDownloadDataPack.Content.Loader.Input.GameVersion
                Case FormMain.PageSubType.DownloadResourcePack
                    TargetType = CompType.ResourcePack
                    TargetVersion = FrmDownloadResourcePack.Content.Loader.Input.GameVersion
                Case FormMain.PageSubType.DownloadShader
                    TargetType = CompType.Shader
                    TargetVersion = FrmDownloadShader.Content.Loader.Input.GameVersion
            End Select
        Else
            '从详情页进入（查看前置）
            TargetType = CompType.Any '允许任意类别
            TargetVersion = FrmMain.PageCurrent.Additional(2)
            TargetLoader = FrmMain.PageCurrent.Additional(3)
        End If
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.CompDetail,
                           .Additional = {sender.Tag, New List(Of String), TargetVersion, TargetLoader, TargetType}})
    End Sub

    '鼠标点击判定
    Private IsMouseDown As Boolean = False
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        If IsMouseOver AndAlso CanInteraction Then IsMouseDown = True
    End Sub
    Private Sub Button_MouseLeave(sender As Object, e As Object) Handles Me.MouseLeave, Me.PreviewMouseLeftButtonUp
        IsMouseDown = False
    End Sub

#End Region

#Region "后加载指向背景"

    Private _RectBack As Border = Nothing
    Public ReadOnly Property RectBack As Border
        Get
            If _RectBack Is Nothing Then
                Dim Rect As New Border With {
                    .Name = "RectBack",
                    .CornerRadius = New CornerRadius(3),
                    .RenderTransform = New ScaleTransform(0.8, 0.8),
                    .RenderTransformOrigin = New Point(0.5, 0.5),
                    .BorderThickness = New Thickness(GetWPFSize(1)),
                    .SnapsToDevicePixels = True,
                    .IsHitTestVisible = False,
                    .Opacity = 0
                }
                Rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7")
                Rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6")
                SetColumnSpan(Rect, 999)
                SetRowSpan(Rect, 999)
                Children.Insert(0, Rect)
                _RectBack = Rect
                '<!--<Border x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True" 
                'IsHitTestVisible = "False" Opacity="0" BorderThickness="1" 
                'Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
            End If
            Return _RectBack
        End Get
    End Property

#End Region

    Private StateLast As String
    ''' <summary>
    ''' 是否允许交互。目前仅用于 PageDownloadCompDetail 的顶部栏展示：若关闭碰撞检测，则无法展开 Tooltip。
    ''' </summary>
    Public Property CanInteraction As Boolean = True
    Public Sub RefreshColor(sender As Object, e As EventArgs) Handles Me.MouseEnter, Me.MouseLeave, Me.MouseLeftButtonDown, Me.MouseLeftButtonUp
        If Not CanInteraction Then Return
        '判断当前颜色
        Dim StateNew As String, Time As Integer
        If IsMouseOver Then
            If IsMouseDown Then
                StateNew = "MouseDown"
                Time = 120
            Else
                StateNew = "MouseOver"
                Time = 120
            End If
        Else
            StateNew = "Idle"
            Time = 180
        End If
        If StateLast = StateNew Then Return
        StateLast = StateNew
        '触发颜色动画
        If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
            '有动画
            Dim Ani As New List(Of AniData)
            If IsMouseOver Then
                Ani.AddRange({
                             AaColor(RectBack, Border.BackgroundProperty, If(IsMouseDown, "ColorBrush6", "ColorBrushBg1"), Time),
                             AaOpacity(RectBack, 1 - RectBack.Opacity, Time,, New AniEaseOutFluent)
                         })
                If IsMouseDown Then
                    Ani.Add(AaScaleTransform(RectBack, 0.996 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time * 1.2,, New AniEaseOutFluent))
                Else
                    Ani.Add(AaScaleTransform(RectBack, 1 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time * 1.2,, New AniEaseOutFluent))
                End If
            Else
                Ani.AddRange({
                             AaOpacity(RectBack, -RectBack.Opacity, Time),
                             AaColor(RectBack, Border.BackgroundProperty, If(IsMouseDown, "ColorBrush6", "ColorBrush7"), Time),
                             AaScaleTransform(RectBack, 0.996 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time,, New AniEaseOutFluent),
                             AaScaleTransform(RectBack, -0.196, 1,,, True)
                         })
            End If
            AniStart(Ani, "CompItem Color " & Uuid)
        Else
            '无动画
            AniStop("CompItem Color " & Uuid)
            If _RectBack IsNot Nothing Then RectBack.Opacity = 0
        End If
    End Sub

End Class
