Public Class MyTextButton
    Inherits Label

    Public Event Click(sender As Object, e As EventArgs)

    '基础

    Public Uuid As Integer = GetUuid()
    Public Sub New()
        SetResourceReference(ForegroundProperty, "ColorBrush1")
        Background = ColorSemiTransparent
    End Sub

    '文本

    Public Property Text As String
        Get
            Return GetValue(TextProperty)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TextProperty As DependencyProperty =
        DependencyProperty.Register("Text", GetType(String), GetType(MyTextButton), New PropertyMetadata("", Sub(sender As MyTextButton, e As DependencyPropertyChangedEventArgs)
                                                                                                                 If Not e.OldValue = e.NewValue Then
                                                                                                                     AniStart({
                                                                                                                              AaOpacity(sender, -sender.Opacity, 50),
                                                                                                                              AaCode(Sub() sender.Content = e.NewValue,, True),
                                                                                                                              AaOpacity(sender, 1, 170)
                                                                                                                        }, "MyTextButton Text " & sender.Uuid)
                                                                                                                 End If
                                                                                                             End Sub))

    '鼠标事件

    Public IsMouseDown As Boolean = False
    Private Sub MyTextButton_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        IsMouseDown = True
        e.Handled = True
    End Sub
    Private Sub MyTextButton_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
    End Sub
    Private Sub MyTextButton_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonUp
        If IsMouseDown Then
            IsMouseDown = False
            Log("[Control] 按下文本按钮：" & Text)
            RaiseEvent Click(Me, Nothing)
            ModEvent.TryStartEvent(EventType, EventData)
            e.Handled = True
        End If
    End Sub

    '指向动画

    Private Const AnimationTimeIn As Integer = 100
    Private Const AnimationTimeOut As Integer = 200
    Private ColorName As String
    Private Sub RefreshColor() Handles Me.MouseEnter, Me.MouseLeave, Me.IsEnabledChanged, Me.MouseLeftButtonDown， Me.MouseLeftButtonUp
        '判断当前颜色
        Dim ForeName As String
        Dim Time As Integer
        If IsMouseDown Then
            ForeName = "ColorBrush4"
            Time = 30
        ElseIf IsMouseOver Then
            ForeName = "ColorBrush3"
            Time = AnimationTimeIn
        Else
            ForeName = "ColorBrush1"
            Time = AnimationTimeOut
        End If
        '重复性验证
        If ColorName = ForeName Then Return
        ColorName = ForeName
        '触发颜色动画
        If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
            '有动画
            AniStart(AaColor(Me, ForegroundProperty, ForeName, Time), "MyTextButton Color " & Uuid)
        Else
            '无动画
            AniStop("MyTextButton Color " & Uuid)
            SetResourceReference(ForegroundProperty, ForeName)
        End If
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
    Public Shared ReadOnly EventTypeProperty As DependencyProperty = DependencyProperty.Register(
        "EventType", GetType(String), GetType(MyTextButton), New PropertyMetadata(Nothing))
    Public Property EventData As String
        Get
            Return GetValue(EventDataProperty)
        End Get
        Set(value As String)
            SetValue(EventDataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventDataProperty As DependencyProperty = DependencyProperty.Register(
        "EventData", GetType(String), GetType(MyTextButton), New PropertyMetadata(Nothing))

End Class
