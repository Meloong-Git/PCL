Public Class MyCard

    '控件
    Inherits Grid
    Private ReadOnly MainGrid As Grid
    Public ReadOnly Property MainChrome As MyDropShadow
    Private ReadOnly MainBorder As Border
    Public Property BorderChild As UIElement
        Get
            Return MainBorder.Child
        End Get
        Set(value As UIElement)
            MainBorder.Child = value
        End Set
    End Property
    Private _MainTextBlock As TextBlock
    Public Property MainTextBlock As TextBlock
        Get
            Init() '当父级触发 Loaded 时，本卡片可能尚未触发 Loaded（该事件从父级向子级调用），因此这会是 null。手动触发以确保控件已加载。
            Return _MainTextBlock
        End Get
        Set(value As TextBlock)
            _MainTextBlock = value
        End Set
    End Property
    Private _MainSwap As Shapes.Path
    Public Property MainSwap As Shapes.Path
        Get
            Init()
            Return _MainSwap
        End Get
        Set(value As Shapes.Path)
            _MainSwap = value
        End Set
    End Property

    '属性
    Public Uuid As Integer = GetUuid()
    Public ReadOnly Property Inlines As InlineCollection
        Get
            Return MainTextBlock.Inlines
        End Get
    End Property
    Public Property CornerRadius As CornerRadius
        Get
            Return MainChrome.CornerRadius
        End Get
        Set(value As CornerRadius)
            MainChrome.CornerRadius = value
            MainBorder.CornerRadius = value
        End Set
    End Property
    Public Property Title As String
        Get
            Return GetValue(TitleProperty)
        End Get
        Set(value As String)
            SetValue(TitleProperty, value)
            If _MainTextBlock IsNot Nothing Then MainTextBlock.Text = value
        End Set
    End Property
    Public Shared ReadOnly TitleProperty As DependencyProperty = DependencyProperty.Register("Title", GetType(String), GetType(MyCard), New PropertyMetadata(""))

    'UI 建立
    Public Sub New()
        MainChrome = New MyDropShadow With {
            .Margin = New Thickness(-3, -3, -3, -3 - GetWPFSize(1)), .ShadowRadius = 3, .Opacity = DropShadowIdleOpacity, .CornerRadius = New CornerRadius(5)}
        MainChrome.SetResourceReference(MyDropShadow.ColorProperty, "ColorObject1")
        Children.Insert(0, MainChrome)
        MainBorder = New Border With {.Background = New SolidColorBrush(Color.FromArgb(245, 255, 255, 255)), .CornerRadius = New CornerRadius(5), .IsHitTestVisible = False}
        Children.Insert(1, MainBorder)
        MainGrid = New Grid
        Children.Add(MainGrid)
    End Sub
    Private IsLoad As Boolean = False
    Private Sub Init() Handles Me.Loaded
        If IsLoad Then Return
        IsLoad = True
        '初次加载限定
        If MainTextBlock Is Nothing Then
            MainTextBlock = New TextBlock With {.HorizontalAlignment = HorizontalAlignment.Left, .VerticalAlignment = VerticalAlignment.Top, .Margin = New Thickness(15, 12, 0, 0), .FontWeight = FontWeights.Bold, .FontSize = 13, .IsHitTestVisible = False}
            MainTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            MainTextBlock.SetBinding(TextBlock.TextProperty, New Binding("Title") With {.Source = Me, .Mode = BindingMode.OneWay})
            MainGrid.Children.Add(MainTextBlock)
        End If
        If CanSwap OrElse SwapControl IsNot Nothing Then
            If SwapControl Is Nothing AndAlso Children.Count > 3 Then SwapControl = Children(3)
            MainSwap = New Shapes.Path With {.HorizontalAlignment = HorizontalAlignment.Right, .Stretch = Stretch.Uniform, .Height = 6, .Width = 10, .VerticalAlignment = VerticalAlignment.Top, .Margin = New Thickness(0, 17, 16, 0), .Data = New GeometryConverter().ConvertFromString("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"), .RenderTransform = New RotateTransform(180), .RenderTransformOrigin = New Point(0.5, 0.5)}
            MainSwap.SetResourceReference(Shapes.Path.FillProperty, "ColorBrush1")
            MainGrid.Children.Add(MainSwap)
        End If
        '改变默认的折叠
        If IsSwaped AndAlso SwapControl IsNot Nothing Then
            MainSwap.RenderTransform = New RotateTransform(If(SwapLogoRight, 270, 0))
            '取消由于高度变化被迫触发的高度动画
            Dim RawUseAnimation As Boolean = UseAnimation
            UseAnimation = False
            Height = SwapedHeight
            AniStop("MyCard Height " & Uuid)
            IsHeightAnimating = False
            RunInUi(Sub() UseAnimation = RawUseAnimation, True)
        End If
    End Sub
    Public Sub StackInstall()
        StackInstall(SwapControl, SwapType, Title)
        TriggerForceResize()
    End Sub
    Public Shared Sub StackInstall(ByRef Stack As StackPanel, Type As Integer, Optional CardTitle As String = "")
        '这一部分的代码是好几年前留下的究极屎坑，当时还不知道该咋正确调用这种方法，就写了这么一坨屎
        '但是现在……反正勉强能用……懒得改了就这样吧.jpg
        '别骂了别骂了.jpg
        If IsNothing(Stack.Tag) Then Return
        '排序
        Select Case Type
            Case 3
                Stack.Tag = CType(Stack.Tag, List(Of DlOptiFineListEntry)).Sort(Function(a, b) VersionSortBoolean(a.NameDisplay, b.NameDisplay))
            Case 4, 10
                Stack.Tag = CType(Stack.Tag, List(Of DlLiteLoaderListEntry)).Sort(Function(a, b) VersionSortBoolean(a.Inherit, b.Inherit))
            Case 6
                Stack.Tag = CType(Stack.Tag, List(Of DlForgeVersionEntry)).Sort(Function(a, b) a.Version > b.Version)
            Case 8, 9
                Stack.Tag = CType(Stack.Tag, List(Of CompFile)).Sort(Function(a, b) a.ReleaseDate > b.ReleaseDate)
        End Select
        '控件转换
        Select Case Type
            Case 5
                Dim LoadingPickaxe As New MyLoading With {.Text = "正在获取版本列表", .Margin = New Thickness(5)}
                Dim Loader = New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion Main", AddressOf DlForgeVersionMain)
                LoadingPickaxe.State = Loader
                Loader.Start(Stack.Tag)
                AddHandler LoadingPickaxe.StateChanged, AddressOf FrmDownloadForge.Forge_StateChanged
                AddHandler LoadingPickaxe.Click, AddressOf FrmDownloadForge.Forge_Click
                Stack.Children.Add(LoadingPickaxe)
            Case 6
                ForgeDownloadListItemPreload(Stack, Stack.Tag, AddressOf ForgeSave_Click, True)
            Case 8
                CompFilesCardPreload(Stack, Stack.Tag)
        End Select
        '实现控件虚拟化
        For Each Data As Object In Stack.Tag
            Select Case Type
                Case 0
                    Stack.Children.Add(PageSelectRight.McVersionListItem(Data))
                Case 2
                    Stack.Children.Add(McDownloadListItem(Data, AddressOf McDownloadMenuSave, True))
                Case 3
                    Stack.Children.Add(OptiFineDownloadListItem(Data, AddressOf OptiFineSave_Click, True))
                Case 4
                    Stack.Children.Add(LiteLoaderDownloadListItem(Data, AddressOf FrmDownloadLiteLoader.DownloadStart, False))
                Case 5
                Case 6
                    Stack.Children.Add(ForgeDownloadListItem(Data, AddressOf ForgeSave_Click, True))
                Case 7
                    '不能使用 AddressOf，这导致了 #535，原因完全不明，疑似是编译器 Bug
                    Stack.Children.Add(McDownloadListItem(Data, Sub(sender, e) FrmDownloadInstall.MinecraftSelected(sender, e), False))
                Case 8
                    If CType(Stack.Tag, List(Of CompFile)).Distinct(Function(a, b) a.DisplayName = b.DisplayName).Count <>
                       CType(Stack.Tag, List(Of CompFile)).Count Then
                        '存在重复的名称（#1344）
                        Stack.Children.Add(CType(Data, CompFile).ToListItem(AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=True))
                    Else
                        '不存在重复的名称，正常加载
                        Stack.Children.Add(CType(Data, CompFile).ToListItem(AddressOf FrmDownloadCompDetail.Save_Click))
                    End If
                Case 9
                    If CType(Stack.Tag, List(Of CompFile)).Distinct(Function(a, b) a.DisplayName = b.DisplayName).Count <>
                       CType(Stack.Tag, List(Of CompFile)).Count Then
                        '存在重复的名称（#1344）
                        Stack.Children.Add(CType(Data, CompFile).ToListItem(AddressOf FrmDownloadCompDetail.Install_Click, AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=True))
                    Else
                        '不存在重复的名称，正常加载
                        Stack.Children.Add(CType(Data, CompFile).ToListItem(AddressOf FrmDownloadCompDetail.Install_Click, AddressOf FrmDownloadCompDetail.Save_Click))
                    End If
                Case 10
                    Stack.Children.Add(LiteLoaderDownloadListItem(Data, AddressOf LiteLoaderSave_Click, True))
                Case 11
                    Stack.Children.Add(CType(Data, HelpEntry).ToListItem)
                Case 12
                    Stack.Children.Add(FabricDownloadListItem(CType(Data, JObject), AddressOf FrmDownloadInstall.Fabric_Selected))
                Case 13
                    Stack.Children.Add(NeoForgeDownloadListItem(Data, AddressOf NeoForgeSave_Click, True))
                Case Else
                    Log("未知的虚拟化种类：" & Type, LogLevel.Feedback)
            End Select
        Next
        Stack.Children.Add(New FrameworkElement With {.Height = 18}) '下边距，同时适应折叠
        Stack.Tag = Nothing
    End Sub

    '动画
    Private Const DropShadowIdleOpacity As Double = 0.07
    Private Const DropShadowHoverOpacity As Double = 0.4
    Public Property HasMouseAnimation As Boolean = True
    Private Sub MyCard_MouseEnter(sender As Object, e As MouseEventArgs) Handles Me.MouseEnter
        If Not HasMouseAnimation Then Return
        Dim AniList As New List(Of AniData)
        If Not IsNothing(MainTextBlock) Then AniList.Add(AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush2", 90))
        If Not IsNothing(MainSwap) Then AniList.Add(AaColor(MainSwap, Shapes.Path.FillProperty, "ColorBrush2", 90))
        AniList.AddRange({
            AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject4", 90),
            AaOpacity(MainChrome, DropShadowHoverOpacity - MainChrome.Opacity, 90)
        })
        AniStart(AniList, "MyCard Mouse " & Uuid)
    End Sub
    Private Sub MyCard_MouseLeave(sender As Object, e As MouseEventArgs) Handles Me.MouseLeave
        If Not HasMouseAnimation Then Return
        Dim AniList As New List(Of AniData)
        If Not IsNothing(MainTextBlock) Then AniList.Add(AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush1", 90))
        If Not IsNothing(MainSwap) Then AniList.Add(AaColor(MainSwap, Shapes.Path.FillProperty, "ColorBrush1", 90))
        AniList.AddRange({
            AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject1", 90),
            AaOpacity(MainChrome, DropShadowIdleOpacity - MainChrome.Opacity, 90)
        })
        AniStart(AniList, "MyCard Mouse " & Uuid)
    End Sub

#Region "高度改变动画"

    ''' <summary>
    ''' 是否启用高度改变动画。
    ''' </summary>
    Public Property UseAnimation As Boolean = True
    Private IsHeightAnimating As Boolean = False
    Private ActualUsedHeight As Double '回滚实际高度（例如 NaN）
    Private Sub MySizeChanged(sender As Object, e As SizeChangedEventArgs) Handles Me.SizeChanged
        If Not UseAnimation Then Return
        Dim DeltaHeight As Double = If(IsSwaped, SwapedHeight, e.NewSize.Height) - e.PreviousSize.Height
        '卡片的进入时动画已被页面通用切换动画替代
        If e.PreviousSize.Height = 0 OrElse IsHeightAnimating OrElse Math.Abs(DeltaHeight) < 1 OrElse ActualHeight = 0 Then Return
        StartHeightAnimation(DeltaHeight, e.PreviousSize.Height, False)
    End Sub
    Private Sub StartHeightAnimation(Delta As Double, PreviousHeight As Double, IsLoadAnimation As Boolean)
        If IsHeightAnimating OrElse FrmMain Is Nothing Then Return '避免 XAML 设计器出错

        Dim AnimList As New List(Of AniData)
        Dim AbsDelta = Math.Abs(Delta)

        If AbsDelta <= 800 Then
            '短距离，直接使用 150ms 的缓动动画
            AnimList.Add(AaHeight(Me, Delta, 150,, New AniEaseOutFluent(AniEasePower.ExtraStrong)))
        Else
            Dim EaseLength As Integer, EaseTime As Integer
            Dim InitSpeed As Integer '到达缓动区前的初速度
            If Delta < 0 AndAlso AbsDelta - EaseLength > 5000 * 0.1 Then
                '收回距离过长 (>0.1s)，强制以 100ms 完成匀速段，然后让减速段更长
                EaseLength = 200
                EaseTime = 150
                InitSpeed = (AbsDelta - EaseLength) / 0.1
            ElseIf Delta > 0 AndAlso AbsDelta - EaseLength > 5000 * 0.6 Then
                '展开距离过长 (>0.6s)，以 5000 速度展示 300ms 匀速段，剩下的距离全部归入减速段
                InitSpeed = 5000
                EaseLength = AbsDelta - InitSpeed * 0.3
                EaseTime = 400
            Else
                '中程，匀速地快速展开（或收回）
                EaseLength = 150
                EaseTime = 200
                InitSpeed = 4000
            End If
            '匀速段
            AnimList.Add(AaHeight(Me, (AbsDelta - EaseLength) * Math.Sign(Delta),
                (AbsDelta - EaseLength) / InitSpeed * 1000))
            '减速段
            AnimList.Add(AaHeight(Me, EaseLength * Math.Sign(Delta),
                EaseTime,, New AniEaseOutFluentWithInitial(InitSpeed, EaseTime / 1000, EaseLength), True))
        End If

        AnimList.Add(AaCode(
        Sub()
            IsHeightAnimating = False
            Height = ActualUsedHeight
            If IsSwaped AndAlso SwapControl IsNot Nothing Then SwapControl.Visibility = Visibility.Collapsed
        End Sub,, True))
        AniStart(AnimList, "MyCard Height " & Uuid)
        IsHeightAnimating = True
        ActualUsedHeight = If(IsSwaped, SwapedHeight, Height)
        Height = PreviousHeight
    End Sub
    ''' <summary>
    ''' 通知 MyCard，控件内容已改变，需要中断动画并更新高度。
    ''' </summary>
    Public Sub TriggerForceResize()
        Height = If(IsSwaped, SwapedHeight, Double.NaN)
        AniStop("MyCard Height " & Uuid)
        IsHeightAnimating = False
    End Sub

#End Region

#Region "折叠"

    '若设置了 CanSwap，或 SwapControl 不为空，则判定为会进行折叠
    '这是因为不能直接在 XAML 中设置 SwapControl
    Public SwapControl As Object
    Public Property CanSwap As Boolean = False
    ''' <summary>
    ''' 被折叠的种类，用于控件虚拟化。
    ''' </summary>
    Public Property SwapType As Integer
    ''' <summary>
    ''' 是否已被折叠。
    ''' </summary>
    Public Property IsSwaped As Boolean
        Get
            Return _IsSwaped
        End Get
        Set(value As Boolean)
            If _IsSwaped = value Then Return
            _IsSwaped = value
            If SwapControl Is Nothing Then Return
            '展开
            If Not IsSwaped AndAlso TypeOf SwapControl Is StackPanel Then StackInstall(SwapControl, SwapType, Title)
            '若尚未加载，会在 Loaded 事件中触发无动画的折叠，不需要在这里进行
            If Not IsLoaded Then Return
            '更新高度
            SwapControl.Visibility = Visibility.Visible
            TriggerForceResize()
            '改变箭头
            AniStart(AaRotateTransform(MainSwap, If(_IsSwaped, If(SwapLogoRight, 270, 0), 180) - CType(MainSwap.RenderTransform, RotateTransform).Angle, 250,, New AniEaseOutFluent(AniEasePower.ExtraStrong)), "MyCard Swap " & Uuid, True)
        End Set
    End Property
    Private _IsSwaped As Boolean = False

    Public Property SwapLogoRight As Boolean = False
    Private IsMouseDown As Boolean = False
    Public Event PreviewSwap(sender As Object, e As RouteEventArgs)
    Public Event Swap(sender As Object, e As RouteEventArgs)
    Public Const SwapedHeight As Integer = 40
    Private Sub MyCard_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        Dim Pos As Double = Mouse.GetPosition(Me).Y
        If Not IsSwaped AndAlso
            (SwapControl Is Nothing OrElse Pos > If(IsSwaped, SwapedHeight, SwapedHeight - 6) OrElse (Pos = 0 AndAlso Not IsMouseDirectlyOver)) Then Return '检测点击位置；或已经不在可视树上的误判
        IsMouseDown = True
    End Sub
    Private Sub MyCard_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        IsMouseDown = False

        Dim Pos As Double = Mouse.GetPosition(Me).Y
        If Not IsSwaped AndAlso
            (SwapControl Is Nothing OrElse Pos > If(IsSwaped, SwapedHeight, SwapedHeight - 6) OrElse (Pos = 0 AndAlso Not IsMouseDirectlyOver)) Then Return '检测点击位置；或已经不在可视树上的误判

        Dim ee = New RouteEventArgs(True)
        RaiseEvent PreviewSwap(Me, ee)
        If ee.Handled Then
            IsMouseDown = False
            Return
        End If

        IsSwaped = Not IsSwaped
        Log("[Control] " & If(IsSwaped, "折叠卡片", "展开卡片") & If(Title Is Nothing, "", "：" & Title))
        RaiseEvent Swap(Me, ee)
    End Sub
    Private Sub MyCard_MouseLeave_Swap(sender As Object, e As MouseEventArgs) Handles Me.MouseLeave
        IsMouseDown = False
    End Sub

#End Region

End Class
Partial Public Module ModAnimation
    Public Sub AniDispose(Control As MyCard, RemoveFromChildren As Boolean, Optional CallBack As ParameterizedThreadStart = Nothing)
        If Control.IsHitTestVisible Then
            Control.IsHitTestVisible = False
            AniStart({
                AaScaleTransform(Control, -0.08, 200,, New AniEaseInFluent),
                AaOpacity(Control, -1, 200,, New AniEaseOutFluent),
                AaHeight(Control, -Control.ActualHeight, 150, 100, New AniEaseOutFluent),
                AaCode(
                Sub()
                    If RemoveFromChildren Then
                        If Control.Parent Is Nothing Then Return
                        CType(Control.Parent, Object).Children.Remove(Control)
                    Else
                        Control.Visibility = Visibility.Collapsed
                    End If
                    If CallBack IsNot Nothing Then CallBack(Control)
                End Sub,, True)
            }, "MyCard Dispose " & Control.Uuid)
        Else
            If RemoveFromChildren Then
                If Control.Parent Is Nothing Then Return
                CType(Control.Parent, Object).Children.Remove(Control)
            Else
                Control.Visibility = Visibility.Collapsed
            End If
            If CallBack IsNot Nothing Then CallBack(Control)
        End If
    End Sub
End Module