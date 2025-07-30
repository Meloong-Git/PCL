Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyIconTextButton

    '基础

    Public Uuid As Integer = GetUuid()
    Public Event Check(sender As Object, raiseByMouse As Boolean)
    Public Event Change(sender As Object, raiseByMouse As Boolean)
    Public Sub RaiseChange()
        RaiseEvent Change(Me, False)
    End Sub '使外部程序可以引发本控件的 Change 事件

    '自定义属性

    Public Property Logo As String
        Get
            Return ShapeLogo.Data.ToString
        End Get
        Set(value As String)
            ShapeLogo.Data = (New GeometryConverter).ConvertFromString(value)
        End Set
    End Property
    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If Not IsNothing(ShapeLogo) Then ShapeLogo.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property

    Public ReadOnly Property Inlines As InlineCollection
        Get
            Return LabText.Inlines
        End Get
    End Property
    Public Property Text As String
        Get
            Return GetValue(TextProperty)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property '内容
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyIconTextButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If Not IsNothing(sender) Then CType(sender, MyIconTextButton).LabText.Text = e.NewValue
    End Sub)))
    Public Enum ColorState
        Black
        Highlight
    End Enum
    Public Property ColorType As ColorState
        Get
            Return GetValue(ColorTypeProperty)
        End Get
        Set(value As ColorState)
            If ColorType = value Then Return
            SetValue(ColorTypeProperty, value)
            RefreshColor()
        End Set
    End Property '颜色类别
    Public Shared ReadOnly ColorTypeProperty As DependencyProperty =
        DependencyProperty.Register("ColorType", GetType(ColorState), GetType(MyIconTextButton), New PropertyMetadata(ColorState.Black))

    '点击事件

    Public Event Click(sender As Object, e As RouteEventArgs)
    Private IsMouseDown As Boolean = False
    Private Sub MyIconTextButton_MouseUp() Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        Log("[Control] 按下带图标按钮：" & Text)
        IsMouseDown = False
        RaiseEvent Click(Me, New RouteEventArgs(True))
        ModEvent.TryStartEvent(EventType, EventData)
        RefreshColor()
    End Sub
    Private Sub MyIconTextButton_MouseDown() Handles Me.MouseLeftButtonDown
        IsMouseDown = True
        RefreshColor()
    End Sub
    Private Sub MyIconTextButton_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
        RefreshColor()
    End Sub
    Public Property EventType As String
        Get
            Return GetValue(EventTypeProperty)
        End Get
        Set(value As String)
            SetValue(EventTypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventTypeProperty As DependencyProperty = DependencyProperty.Register("EventType", GetType(String), GetType(MyIconTextButton), New PropertyMetadata(Nothing))
    Public Property EventData As String
        Get
            Return GetValue(EventDataProperty)
        End Get
        Set(value As String)
            SetValue(EventDataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventDataProperty As DependencyProperty = DependencyProperty.Register("EventData", GetType(String), GetType(MyIconTextButton), New PropertyMetadata(Nothing))

    '动画

    Private Const AnimationTimeOfMouseIn As Integer = 100 '鼠标指向动画长度
    Private Const AnimationTimeOfMouseOut As Integer = 150 '鼠标移出动画长度
    Private Sub RefreshColor(Optional obj = Nothing, Optional e = Nothing) Handles Me.MouseEnter, Me.Loaded, Me.IsEnabledChanged
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 AndAlso Not False.Equals(e) Then '防止默认属性变更触发动画，若强制不执行动画，则 e 为 False

                Select Case ColorType
                    Case ColorState.Black
                        If IsMouseDown Then
                            '按下
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush6", 70), "MyIconTextButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            '指向
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseIn),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrushBg1", AnimationTimeOfMouseIn), "MyIconTextButton Color " & Uuid)
                        ElseIf IsEnabled Then
                            '正常
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush1", AnimationTimeOfMouseOut),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush1", AnimationTimeOfMouseOut)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        Else
                            '禁用
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrushGray5", 100),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray5", 100)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        End If
                    Case ColorState.Highlight
                        If IsMouseDown Then
                            '按下
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush6", 70), "MyIconTextButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            '指向
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseIn),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrushBg1", AnimationTimeOfMouseIn), "MyIconTextButton Color " & Uuid)
                        ElseIf IsEnabled Then
                            '正常
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseOut),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseOut)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        Else
                            '禁用
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrushGray5", 100),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray5", 100)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        End If
                End Select

            Else

                '不使用动画
                AniStop("MyIconTextButton Checked " & Uuid)
                AniStop("MyIconTextButton Color " & Uuid)
                Select Case ColorType
                    Case ColorState.Black
                        Background = ColorSemiTransparent
                        ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"))
                        LabText.SetResourceReference(TextBlock.ForegroundProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"))
                    Case ColorState.Highlight
                        Background = ColorSemiTransparent
                        ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, If(IsEnabled, "ColorBrush3", "ColorBrushGray5"))
                        LabText.SetResourceReference(TextBlock.ForegroundProperty, If(IsEnabled, "ColorBrush3", "ColorBrushGray5"))
                End Select

            End If
        Catch ex As Exception
            Log(ex, "刷新带图标按钮颜色出错")
        End Try
    End Sub

End Class
