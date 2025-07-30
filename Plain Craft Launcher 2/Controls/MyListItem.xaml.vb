Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyListItem
    Implements IMyRadio

    Public Event Click(sender As Object, e As MouseButtonEventArgs)
    Public Event LogoClick(sender As Object, e As MouseButtonEventArgs)
    Public Event Check(sender As Object, e As RouteEventArgs) Implements IMyRadio.Check
    Public Event Changed(sender As Object, e As RouteEventArgs) Implements IMyRadio.Changed

#Region "后加载控件"

    '指向背景
    Private _RectBack As Border = Nothing
    Public ReadOnly Property RectBack As Border
        Get
            If _RectBack Is Nothing Then
                Dim Rect As New Border With {
                    .Name = "RectBack",
                    .CornerRadius = New CornerRadius(If(IsScaleAnimationEnabled OrElse Height > 40, 6, 0)),
                    .RenderTransform = If(IsScaleAnimationEnabled, New ScaleTransform(0.8, 0.8), Nothing),
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

    '按钮
    Public ButtonStack As FrameworkElement

    '图标
    Public PathLogo As FrameworkElement

    '勾选条
    Public RectCheck As Border

    '副文本
    Private _LabInfo As TextBlock = Nothing
    Public ReadOnly Property LabInfo As TextBlock
        Get
            If _LabInfo Is Nothing Then
                Dim Lab As New TextBlock With {
                    .Name = "LabInfo",
                    .SnapsToDevicePixels = False,
                    .UseLayoutRounding = False,
                    .HorizontalAlignment = HorizontalAlignment.Left,
                    .IsHitTestVisible = False,
                    .TextTrimming = TextTrimming.CharacterEllipsis,
                    .Visibility = Visibility.Collapsed,
                    .FontSize = 12,
                    .Margin = New Thickness(4, 0, 0, 0),
                    .Opacity = 0.6
                }
                SetColumn(Lab, 3)
                SetRow(Lab, 2)
                Children.Add(Lab)
                _LabInfo = Lab
                '<TextBlock Grid.Row="2" SnapsToDevicePixels="False" UseLayoutRounding="False" HorizontalAlignment="Left" x:Name = "LabInfo" IsHitTestVisible="False" Grid.Column="2" 
                'TextTrimming = "CharacterEllipsis" Visibility="Collapsed" FontSize="12" Foreground="{StaticResource ColorBrushGray2}" Margin="4,0,0,0" />
            End If
            Return _LabInfo
        End Get
    End Property

#End Region

#Region "自定义属性"

    'Uuid
    Public Uuid As Integer = GetUuid()

    ''' <summary>
    ''' 是否启用缩放动画。
    ''' </summary>
    Public Property IsScaleAnimationEnabled As Boolean
        Get
            Return _IsScaleAnimationEnabled
        End Get
        Set
            _IsScaleAnimationEnabled = Value
            If _RectBack IsNot Nothing Then RectBack.CornerRadius = New CornerRadius(If(Value, 6, 0))
        End Set
    End Property
    Private _IsScaleAnimationEnabled As Boolean = True

    '边距
    Public Property PaddingLeft As Integer
        Get
            Return ColumnPaddingLeft.Width.Value
        End Get
        Set(value As Integer)
            ColumnPaddingLeft.Width = New GridLength(value)
        End Set
    End Property
    ''' <summary>
    ''' 右边距的最小值。
    ''' 在存在右侧按钮时，右边距会被自动设置为 5 + 按钮数 * 25。
    ''' </summary>
    Public Property MinPaddingRight As Integer = 4

    '按钮
    Private _Buttons As IEnumerable(Of MyIconButton)
    Public Property Buttons As IEnumerable(Of MyIconButton)
        Get
            Return _Buttons
        End Get
        Set(value As IEnumerable(Of MyIconButton))
            _Buttons = value
            '没有特殊按钮，移除原 Stack
            If ButtonStack IsNot Nothing Then
                Children.Remove(ButtonStack)
                ButtonStack = Nothing
            End If
            '添加新 Stack
            Select Case value.Count
                Case 0
                    '没有按钮，不添加新的
                Case 1
                    '只有一个按钮
                    For Each Btn As MyIconButton In value
                        If Btn.Height.Equals(Double.NaN) Then Btn.Height = 25
                        If Btn.Width.Equals(Double.NaN) Then Btn.Width = 25
                        With Btn
                            .Opacity = 0
                            .Margin = New Thickness(0, 0, 5, 0)
                            .SnapsToDevicePixels = False
                            .HorizontalAlignment = HorizontalAlignment.Right
                            .VerticalAlignment = VerticalAlignment.Center
                            .SnapsToDevicePixels = False
                            .UseLayoutRounding = False
                        End With
                        SetColumnSpan(Btn, 10) : SetRowSpan(Btn, 10)
                        Children.Add(Btn)
                        ButtonStack = Btn
                    Next
                Case Else
                    '有复数按钮，使用 StackPanel
                    ButtonStack = New StackPanel With {.Opacity = 0, .Margin = New Thickness(0, 0, 5, 0), .SnapsToDevicePixels = False, .Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right, .VerticalAlignment = VerticalAlignment.Center, .UseLayoutRounding = False}
                    SetColumnSpan(ButtonStack, 10) : SetRowSpan(ButtonStack, 10)
                    '构造按钮
                    For Each Btn As MyIconButton In value
                        If Btn.Height.Equals(Double.NaN) Then Btn.Height = 25
                        If Btn.Width.Equals(Double.NaN) Then Btn.Width = 25
                        CType(ButtonStack, StackPanel).Children.Add(Btn)
                    Next
                    Children.Add(ButtonStack)
            End Select
        End Set
    End Property

    '标题
    Public ReadOnly Property Inlines As InlineCollection
        Get
            Return LabTitle.Inlines
        End Get
    End Property
    Public Property Title As String
        Get
            Return GetValue(TitleProperty)
        End Get
        Set(value As String)
            SetValue(TitleProperty, value.Replace(vbCr, "").Replace(vbLf, ""))
        End Set
    End Property
    Public Shared ReadOnly TitleProperty As DependencyProperty = DependencyProperty.Register("Title", GetType(String), GetType(MyListItem))

    '字号
    Public Property FontSize As Double
        Get
            Return GetValue(FontSizeProperty)
        End Get
        Set(value As Double)
            SetValue(FontSizeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly FontSizeProperty As DependencyProperty = DependencyProperty.Register("FontSize", GetType(Double), GetType(MyListItem), New PropertyMetadata(CType(14, Double)))

    '信息
    Private _Info As String = ""
    Public Property Info As String
        Get
            Return _Info
        End Get
        Set(value As String)
            If _Info = value Then Return
            value = value.Replace(vbCr, "").Replace(vbLf, "")
            _Info = value
            LabInfo.Text = value
            LabInfo.Visibility = If(value = "", Visibility.Collapsed, Visibility.Visible)
        End Set
    End Property

    '图片
    Private _Logo As String = ""
    Public Property Logo As String
        Get
            Return _Logo
        End Get
        Set(value As String)
            If _Logo = value Then Return
            _Logo = value
            '删除旧 Logo
            If Not IsNothing(PathLogo) Then Children.Remove(PathLogo)
            '添加新 Logo
            If Not _Logo = "" Then
                If _Logo.StartsWithF("http", True) Then
                    '网络图片
                    PathLogo = New MyImage With {
                            .Tag = Me,
                            .IsHitTestVisible = LogoClickable,
                            .Source = _Logo,
                            .RenderTransformOrigin = New Point(0.5, 0.5),
                            .RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale},
                            .SnapsToDevicePixels = True, .UseLayoutRounding = False}
                    RenderOptions.SetBitmapScalingMode(PathLogo, BitmapScalingMode.Linear)
                ElseIf _Logo.EndsWithF(".png", True) OrElse _Logo.EndsWithF(".jpg", True) OrElse _Logo.EndsWithF(".webp", True) Then
                    '位图
                    PathLogo = New Canvas With {
                            .Tag = Me,
                            .IsHitTestVisible = LogoClickable,
                            .Background = New MyBitmap(_Logo),
                            .RenderTransformOrigin = New Point(0.5, 0.5),
                            .RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale},
                            .SnapsToDevicePixels = True, .UseLayoutRounding = False,
                            .HorizontalAlignment = HorizontalAlignment.Stretch, .VerticalAlignment = VerticalAlignment.Stretch
                    }
                    RenderOptions.SetBitmapScalingMode(PathLogo, BitmapScalingMode.Linear)
                Else
                    '矢量图
                    PathLogo = New Shapes.Path With {
                        .Tag = Me,
                        .IsHitTestVisible = LogoClickable, .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center, .Stretch = Stretch.Uniform,
                        .Data = (New GeometryConverter).ConvertFromString(_Logo),
                        .RenderTransformOrigin = New Point(0.5, 0.5),
                        .RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale},
                        .SnapsToDevicePixels = False, .UseLayoutRounding = False}
                    PathLogo.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = Me})
                End If
                SetColumn(PathLogo, 2)
                SetRowSpan(PathLogo, 4)
                OnSizeChanged() '设置边距
                Children.Add(PathLogo)
                '图标的点击事件
                If LogoClickable Then
                    AddHandler PathLogo.MouseLeave, Sub(sender, e) IsLogoDown = False
                    AddHandler PathLogo.MouseLeftButtonDown, Sub(sender, e) IsLogoDown = True
                    AddHandler PathLogo.MouseLeftButtonUp, Sub(sender, e) If IsLogoDown Then IsLogoDown = False : RaiseEvent LogoClick(sender.Tag, e)
                End If
            End If
            '改变行距
            ColumnLogo.Width = New GridLength(If(_Logo = "", 0, 34) + If(Height < 40, 0, 4))
        End Set
    End Property
    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If Not IsNothing(PathLogo) Then PathLogo.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property

    '图标的点击
    ''' <summary>
    ''' 该 Logo 是否可用点击触发事件。需要在 Logo 属性之前设置。
    ''' </summary>
    Public Property LogoClickable As Boolean = False
    Private IsLogoDown As Boolean = False

    '勾选选项
    Public Enum CheckType
        None
        Clickable
        RadioBox
        CheckBox
    End Enum
    Private _Type As CheckType = CheckType.None
    Public Property Type As CheckType
        Get
            Return _Type
        End Get
        Set(value As CheckType)
            If _Type = value Then Return
            _Type = value
            '切换左栏大小
            ColumnCheck.Width = New GridLength(If(_Type = CheckType.None OrElse _Type = CheckType.Clickable, If(Height < 40, 4, 2), 6))
            '切换竖条控件
            If _Type = CheckType.None OrElse _Type = CheckType.Clickable Then
                '移除竖条控件
                If Not IsNothing(RectCheck) Then
                    Children.Remove(RectCheck)
                    RectCheck = Nothing
                End If
                SetChecked(False, False, False)
            Else
                '添加竖条控件
                If IsNothing(RectCheck) Then
                    RectCheck = New Border With {.Width = 5, .Height = If(Checked, Double.NaN, 0), .CornerRadius = New CornerRadius(2, 2, 2, 2),
                        .VerticalAlignment = If(Checked, VerticalAlignment.Stretch, VerticalAlignment.Center),
                        .HorizontalAlignment = HorizontalAlignment.Left, .UseLayoutRounding = False, .SnapsToDevicePixels = False,
                        .Margin = If(Checked, New Thickness(-1, 6, 0, 6), New Thickness(-1, 0, 0, 0))}
                    RectCheck.SetResourceReference(Border.BackgroundProperty, "ColorBrush3")
                    SetRowSpan(RectCheck, 4)
                    Children.Add(RectCheck)
                End If
            End If
        End Set
    End Property

    '适应尺寸
    Private Sub OnSizeChanged() Handles Me.SizeChanged
        ColumnCheck.Width = New GridLength(If(_Type = CheckType.None OrElse _Type = CheckType.Clickable, If(Height < 40, 4, 2), 6))
        ColumnLogo.Width = New GridLength(If(_Logo = "", 0, 34) + If(Height < 40, 0, 4))
        If PathLogo IsNot Nothing Then
            If _Logo.EndsWithF(".png", True) OrElse _Logo.EndsWithF(".jpg", True) OrElse _Logo.EndsWithF(".webp", True) Then
                PathLogo.Margin = New Thickness(4, 5, 3, 5)
            Else
                PathLogo.Margin = New Thickness(If(Height < 40, 6, 8), 8, If(Height < 40, 4, 6), 8)
            End If
        End If
        LabTitle.Margin = New Thickness(4, 0, 0, If(Height < 40, 0, 2))
    End Sub

    '勾选状态
    Private _Checked As Boolean = False
    Public Property Checked As Boolean
        Get
            Return _Checked
        End Get
        Set(value As Boolean)
            SetChecked(value, False, value <> _Checked) '仅在值发生变化时触发动画 (#4596)
        End Set
    End Property
    ''' <summary>
    ''' 手动设置 Checked 属性。
    ''' </summary>
    ''' <param name="value">新的 Checked 属性。</param>
    ''' <param name="user">是否由用户引发。</param>
    ''' <param name="anime">是否执行动画。</param>
    Public Sub SetChecked(value As Boolean, user As Boolean, anime As Boolean)
        Try

            '自定义属性基础

            Dim ChangedEventArgs As New RouteEventArgs(user)
            Dim RawValue = _Checked
            If Type = CheckType.RadioBox Then
                If IsInitialized AndAlso Not value = _Checked Then
                    _Checked = value
                    RaiseEvent Changed(Me, ChangedEventArgs)
                    If ChangedEventArgs.Handled Then
                        _Checked = RawValue
                        Return
                    End If
                End If
                _Checked = value
            Else
                If value = _Checked Then Return
                _Checked = value
                If IsInitialized Then
                    RaiseEvent Changed(Me, ChangedEventArgs)
                    If ChangedEventArgs.Handled Then
                        _Checked = RawValue
                        Return
                    End If
                End If
            End If
            If value Then
                Dim CheckEventArgs As New RouteEventArgs(user)
                RaiseEvent Check(Me, CheckEventArgs)
                If CheckEventArgs.Handled Then Return
            End If

            '保证只有一个单选 ListItem 选中

            If Type = CheckType.RadioBox Then
                If IsNothing(Parent) Then Return
                Dim RadioboxList As New List(Of MyListItem)
                Dim CheckedCount As Integer = 0
                '收集控件列表与选中个数
                For Each Control In CType(Parent, Object).Children
                    If TypeOf Control Is MyListItem AndAlso CType(Control, MyListItem).Type = CheckType.RadioBox Then
                        RadioboxList.Add(Control)
                        If Control.Checked Then CheckedCount += 1
                    End If
                Next
                '判断选中情况
                Select Case CheckedCount
                    Case 0
                        '没有任何单选框被选中，选择第一个
                        RadioboxList(0).Checked = True
                    Case Is > 1
                        '选中项目多于 1 个
                        If Me.Checked Then
                            '如果本控件选中，则取消其他所有控件的选中
                            For Each Control As MyListItem In RadioboxList
                                If Control.Checked AndAlso Not Control.Equals(Me) Then Control.Checked = False
                            Next
                        Else
                            '如果本控件未选中，则只保留第一个选中的控件
                            Dim FirstChecked = False
                            For Each Control As MyListItem In RadioboxList
                                If Control.Checked Then
                                    If FirstChecked Then
                                        Control.Checked = False '修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                    Else
                                        FirstChecked = True
                                    End If
                                End If
                            Next
                        End If
                End Select
            End If

            '更改动画

            If IsLoaded AndAlso AniControlEnabled = 0 AndAlso anime Then '防止默认属性变更触发动画
                Dim Anim As New List(Of AniData)
                If Checked Then
                    '由无变有
                    If Not IsNothing(RectCheck) Then
                        Dim Delta = ActualHeight - RectCheck.ActualHeight - 12
                        Anim.Add(AaHeight(RectCheck, Delta * 0.4, 200,, New AniEaseOutFluent(AniEasePower.Weak)))
                        Anim.Add(AaHeight(RectCheck, Delta * 0.6, 300,, New AniEaseOutBack(AniEasePower.Weak)))
                        Anim.Add(AaOpacity(RectCheck, 1 - RectCheck.Opacity, 30))
                        RectCheck.VerticalAlignment = VerticalAlignment.Center
                        RectCheck.Margin = New Thickness(-1, 0, 0, 0)
                    End If
                    Anim.Add(AaColor(Me, ForegroundProperty, If(Height < 40, "ColorBrush3", "ColorBrush2"), 200))
                Else
                    '由有变无
                    If Not IsNothing(RectCheck) Then
                        'Anim.Add(AaWidth(RectCheck, -RectCheck.Width, 120,, New AniEaseInFluent))
                        Anim.Add(AaHeight(RectCheck, -RectCheck.ActualHeight, 120,, New AniEaseInFluent(AniEasePower.Weak)))
                        Anim.Add(AaOpacity(RectCheck, -RectCheck.Opacity, 70, 40))
                        RectCheck.VerticalAlignment = VerticalAlignment.Center
                    End If
                    Anim.Add(AaColor(Me, ForegroundProperty, "ColorBrush1", 120))
                End If
                AniStart(Anim, "MyListItem Checked " & Uuid)
            Else
                '不使用动画
                AniStop("MyListItem Checked " & Uuid)
                If Checked Then
                    If Not IsNothing(RectCheck) Then
                        RectCheck.Height = Double.NaN
                        RectCheck.Margin = New Thickness(-1, 6, 0, 6)
                        RectCheck.Opacity = 1
                        RectCheck.VerticalAlignment = VerticalAlignment.Stretch
                    End If
                    SetResourceReference(ForegroundProperty, If(Height < 40, "ColorBrush3", "ColorBrush2"))
                Else
                    If Not IsNothing(RectCheck) Then
                        RectCheck.Height = 0
                        RectCheck.Margin = New Thickness(-1, 0, 0, 0)
                        RectCheck.Opacity = 0
                        RectCheck.VerticalAlignment = VerticalAlignment.Center
                    End If
                    SetResourceReference(ForegroundProperty, "ColorBrush1")
                End If
            End If

        Catch ex As Exception
            Log(ex, "设置 Checked 失败")
        End Try
    End Sub

    '前景色绑定
    Public Property Foreground As Brush
        Get
            Return GetValue(ForegroundProperty)
        End Get
        Set(value As Brush)
            SetValue(ForegroundProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ForegroundProperty As DependencyProperty = DependencyProperty.Register("Foreground", GetType(Brush), GetType(MyListItem), New PropertyMetadata(CType(Color1, SolidColorBrush)))

    '菜单与按钮绑定
    Public ContentHandler As Action(Of MyListItem, EventArgs)

#End Region

#Region "点击"

    '触发点击事件
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonUp
        If Not IsMouseDown Then Return
        RaiseEvent Click(sender, e)
        If e.Handled Then Return
        '触发自定义事件
        If Not String.IsNullOrEmpty(EventType) Then
            ModEvent.TryStartEvent(EventType, EventData)
            e.Handled = True
        End If
        If e.Handled Then Return
        '实际的单击处理
        Select Case Type
            Case CheckType.Clickable
                Log("[Control] 按下单击列表项：" & Title)
            Case CheckType.RadioBox
                Log("[Control] 按下单选列表项：" & Title)
                If Not Checked Then SetChecked(True, True, True)
            Case CheckType.CheckBox
                Log("[Control] 按下复选列表项（" & (Not Checked).ToString & "）：" & Title)
                SetChecked(Not Checked, True, True)
        End Select
    End Sub

    '鼠标点击判定
    Private IsMouseDown As Boolean = False
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        If IsMouseDirectlyOver AndAlso Not Type = CheckType.None Then
            IsMouseDown = True
            If ButtonStack IsNot Nothing Then ButtonStack.IsHitTestVisible = False
        End If
    End Sub
    Private Sub Button_MouseLeave(sender As Object, e As Object) Handles Me.MouseLeave, Me.PreviewMouseLeftButtonUp
        IsMouseDown = False
        If ButtonStack IsNot Nothing Then ButtonStack.IsHitTestVisible = True
    End Sub

    '实现自定义事件
    Public Property EventType As String
        Get
            Return GetValue(EventTypeProperty)
        End Get
        Set(value As String)
            SetValue(EventTypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventTypeProperty As DependencyProperty = DependencyProperty.Register("EventType", GetType(String), GetType(MyListItem), New PropertyMetadata(Nothing))
    Public Property EventData As String
        Get
            Return GetValue(EventDataProperty)
        End Get
        Set(value As String)
            SetValue(EventDataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventDataProperty As DependencyProperty = DependencyProperty.Register("EventData", GetType(String), GetType(MyListItem), New PropertyMetadata(Nothing))

#End Region

    Private StateLast As String
    Public IsMouseOverAnimationEnabled As Boolean = True
    Public Sub RefreshColor(sender As Object, e As EventArgs) Handles Me.MouseEnter, Me.MouseLeave, Me.MouseLeftButtonDown, Me.MouseLeftButtonUp
        '菜单虚拟化检测
        If ContentHandler IsNot Nothing Then
            ContentHandler(sender, e)
            ContentHandler = Nothing
        End If
        '判断当前颜色
        Dim StateNew As String, Time As Integer
        If IsMouseDown AndAlso Not (Type = CheckType.RadioBox AndAlso Checked) Then
            StateNew = "MouseDown"
            Time = 120
        Else
            If IsMouseOver AndAlso IsMouseOverAnimationEnabled Then
                StateNew = "MouseOver"
                Time = 120
            Else
                StateNew = "Idle"
                Time = 180
            End If
        End If
        If StateLast = StateNew Then Return
        StateLast = StateNew
        '触发颜色动画
        If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
            '有动画
            Dim Ani As New List(Of AniData)
            If IsMouseOver AndAlso IsMouseOverAnimationEnabled Then
                If ButtonStack IsNot Nothing Then
                    Ani.Add(AaOpacity(ButtonStack, 1 - ButtonStack.Opacity, Time * 0.7, Time * 0.3))
                    Ani.Add(AaDouble(Sub(i) ColumnPaddingRight.Width = New GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + i)),
                                     Math.Max(MinPaddingRight, 5 + Buttons.Count * 25) - ColumnPaddingRight.Width.Value, Time * 0.3, Time * 0.7))
                End If
                Ani.AddRange({
                             AaColor(RectBack, Border.BackgroundProperty, If(IsMouseDown, "ColorBrush6", "ColorBrushBg1"), Time),
                             AaOpacity(RectBack, 1 - RectBack.Opacity, Time,, New AniEaseOutFluent)
                         })
                If IsScaleAnimationEnabled Then
                    Ani.Add(AaScaleTransform(RectBack, 1 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time * 1.6,, New AniEaseOutFluent))
                    If IsMouseDown Then
                        Ani.Add(AaScaleTransform(Me, 0.98 - CType(Me.RenderTransform, ScaleTransform).ScaleX, Time * 0.9,, New AniEaseOutFluent))
                    Else
                        Ani.Add(AaScaleTransform(Me, 1 - CType(Me.RenderTransform, ScaleTransform).ScaleX, Time * 1.2,, New AniEaseOutFluent))
                    End If
                End If
            Else
                If ButtonStack IsNot Nothing Then
                    Ani.Add(AaOpacity(ButtonStack, -ButtonStack.Opacity, Time * 0.4))
                    Ani.Add(AaDouble(Sub(i) ColumnPaddingRight.Width = New GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + i)),
                                     MinPaddingRight - ColumnPaddingRight.Width.Value, Time * 0.4))
                End If
                Ani.Add(AaOpacity(RectBack, -RectBack.Opacity, Time))
                If IsScaleAnimationEnabled Then
                    Ani.AddRange({
                        AaColor(RectBack, Border.BackgroundProperty, If(IsMouseDown, "ColorBrush6", "ColorBrush7"), Time),
                        AaScaleTransform(Me, 1 - CType(RenderTransform, ScaleTransform).ScaleX, Time * 3,, New AniEaseOutFluent),
                        AaScaleTransform(RectBack, 0.996 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time,, New AniEaseOutFluent),
                        AaScaleTransform(RectBack, -0.246, 1,,, True)
                    })
                End If
            End If
            AniStart(Ani, "ListItem Color " & Uuid)
        Else
            '无动画
            If IsMouseOver AndAlso IsMouseOverAnimationEnabled Then
                If ButtonStack IsNot Nothing Then
                    ButtonStack.Opacity = 1
                    ColumnPaddingRight.Width = New GridLength(Math.Max(MinPaddingRight, 5 + Buttons.Count * 25))
                End If
                '由于鼠标已经移入，所以直接实例化 RectBack
                RectBack.Background = ColorBg1
                RectBack.Opacity = 1
                RectBack.RenderTransform = New ScaleTransform(1, 1)
                Me.RenderTransform = New ScaleTransform(1, 1)
            Else
                If ButtonStack IsNot Nothing Then
                    ButtonStack.Opacity = 0
                    ColumnPaddingRight.Width = New GridLength(MinPaddingRight)
                End If
                Me.RenderTransform = New ScaleTransform(1, 1)
                If _RectBack IsNot Nothing Then
                    If IsScaleAnimationEnabled Then RectBack.RenderTransform = New ScaleTransform(0.75, 0.75)
                    RectBack.Background = Color7
                    RectBack.Opacity = 0
                End If
            End If
            AniStop("ListItem Color " & Uuid)
        End If
    End Sub

    Private Sub MyListItem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Checked Then
            SetResourceReference(ForegroundProperty, If(Height < 40, "ColorBrush3", "ColorBrush2"))
        Else
            SetResourceReference(ForegroundProperty, "ColorBrush1")
        End If
        ColumnPaddingRight.Width = New GridLength(MinPaddingRight)
        If EventType = "打开帮助" AndAlso Not (Title <> "" AndAlso Info <> "") Then '#3266
            Try
                Dim Unused = New HelpEntry(GetEventAbsoluteUrls(EventData, EventType)(0)).SetToListItem(Me)
            Catch ex As Exception
                Log(ex, "设置帮助 MyListItem 失败", LogLevel.Msgbox)
                EventType = Nothing
                EventData = Nothing
            End Try
        End If
    End Sub
    Public Overrides Function ToString() As String
        Return Title
    End Function

End Class
