Public Class MyResourceGridItem

#Region "属性"
    Public Uuid As Integer = GetUuid()

    ''' <summary>
    ''' 是否允许交互。目前仅用于 PageDownloadResourceDetail 的顶部栏展示：若关闭碰撞检测，则无法展开 Tooltip。
    ''' </summary>
    Public Property CanInteraction As Boolean = True

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

    'Tag列表
    Public WriteOnly Property Tags As List(Of String)
        Set(Tags As List(Of String))
            If Tags.Count > 0 Then
                Tag1.Text = Tags(0)
                TagBorder1.Visibility = Visibility.Visible
            End If
            If Tags.Count > 1 Then
                Tag2.Text = Tags(1)
                TagBorder2.Visibility = Visibility.Visible
            End If
        End Set
    End Property

    '是否显示收藏按钮
    Public Property ShowFavoriteBtn As Boolean
        Get
            Return PanButtons.Visibility = Visibility.Visible
        End Get
        Set(value As Boolean)
            PanButtons.Visibility = If(value, Visibility.Visible, Visibility.Collapsed)
        End Set
    End Property

#End Region

#Region "点击事件"
    '触发点击事件
    Public Event Click(sender As Object, e As MouseButtonEventArgs)

    Private IsMouseDown As Boolean = False

    Private Sub MyResourceGridItem_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        If IsMouseOver AndAlso CanInteraction Then IsMouseDown = True
    End Sub

    Private Sub MyResourceGridItem_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonUp
        If IsMouseDown Then
            IsMouseDown = False
            RaiseEvent Click(sender, e)
            If e.Handled Then Return
            Logger.Info($"按下资源工程网格项：{LabTitle.Text}")
        End If
    End Sub

    Private Sub MyResourceGridItem_Click(sender As MyResourceGridItem, e As EventArgs) Handles Me.Click
        '记录当前展开的卡片标题（#2712）
        Dim Titles As New List(Of String)
        If FrmMain.PageCurrent.Page = FormMain.PageType.ResourceDetail Then
            For Each Card As MyCard In FrmDownloadResourceDetail.PanResults.Children
                If Card.Title <> "" AndAlso Not Card.IsSwapped Then Titles.Add(Card.Title)
            Next
            Logger.Info($"记录当前已展开的卡片：{String.Join("、", Titles)}")
            FrmMain.PageCurrent.Additional(1) = Titles
        End If
        '打开详情页
        Dim TargetType As ResourceTypes
        Dim TargetInstance As String = Nothing
        Dim TargetLoader As ModLoaders = ModLoaders.None
        If FrmMain.PageCurrent.Page = FormMain.PageType.Download Then
            '从下载页进入
            Select Case FrmMain.PageCurrentSub
                Case FormMain.PageSubType.DownloadMod
                    TargetType = ResourceTypes.Mod
                    TargetInstance = FrmDownloadMod.Content.Loader.Input.GameVersion
                    TargetLoader = FrmDownloadMod.Content.Loader.Input.ModLoaders
                Case FormMain.PageSubType.DownloadPack
                    TargetType = ResourceTypes.ModPack
                    TargetInstance = FrmDownloadPack.Content.Loader.Input.GameVersion
                Case FormMain.PageSubType.DownloadDataPack
                    TargetType = ResourceTypes.DataPack
                    TargetInstance = FrmDownloadDataPack.Content.Loader.Input.GameVersion
                Case FormMain.PageSubType.DownloadResourcePack
                    TargetType = ResourceTypes.ResourcePack
                    TargetInstance = FrmDownloadResourcePack.Content.Loader.Input.GameVersion
                Case FormMain.PageSubType.DownloadShader
                    TargetType = ResourceTypes.Shader
                    TargetInstance = FrmDownloadShader.Content.Loader.Input.GameVersion
            End Select
        Else
            '从详情页进入（查看前置）
            TargetType = ResourceTypes.Any '允许任意类别
            TargetInstance = FrmMain.PageCurrent.Additional(2)
            TargetLoader = FrmMain.PageCurrent.Additional(3)
        End If
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.ResourceDetail,
                           .Additional = {sender.Tag, New List(Of String), TargetInstance, TargetLoader, TargetType}})
    End Sub

    Private Sub MyResourceGridItem_MouseLeave(sender As Object, e As Object) Handles Me.MouseLeave
        IsMouseDown = False
    End Sub

    Private Sub MyResourceGridItem_MouseEnter(sender As Object, e As EventArgs) Handles Me.MouseEnter, Me.MouseLeave
        RefreshColor(sender, e)
    End Sub

    Private Sub MyResourceGridItem_MouseLeftButtonDown1(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown, Me.MouseLeftButtonUp
        RefreshColor(sender, e)
    End Sub
#End Region

#Region "颜色状态"
    Private StateLast As String = ""
    Private Sub RefreshColor(sender As Object, e As EventArgs)
        If Not CanInteraction Then Return
        Dim StateNew As String = If(IsMouseOver, "MouseOver", "Idle")
        If (If(StateLast, "")) = (If(StateNew, "")) Then Return
        StateLast = StateNew
        If IsMouseOver Then
            If PanButtons IsNot Nothing AndAlso ShowFavoriteBtn Then
                PanButtons.Opacity = 1
            End If
            PanBack.BorderBrush = CType(FindResource("ColorBrush2"), Brush)
            PanBack.BorderThickness = New Thickness(2)
        Else
            If PanButtons IsNot Nothing Then
                PanButtons.Opacity = 0
            End If
            PanBack.BorderBrush = CType(FindResource("ColorBrush6"), Brush)
            PanBack.BorderThickness = New Thickness(1)
        End If
    End Sub

#End Region

End Class