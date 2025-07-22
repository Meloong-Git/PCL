Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyHint
    Public Uuid As Integer = GetUuid()

    '边框
    Public Property HasBorder As Boolean
        Get
            Return BorderThickness.Top > 0
        End Get
        Set(value As Boolean)
            If value Then
                BorderThickness = New Thickness(3, GetWPFSize(1), GetWPFSize(1), GetWPFSize(1))
            Else
                BorderThickness = New Thickness(3, 0, 0, 0)
            End If
        End Set
    End Property

    '配色
    Public Enum Themes
        Blue = 0
        Red = 1
        Yellow = 2
    End Enum
    Public Property Theme As Themes
        Get
            Return _ColorType
        End Get
        Set(value As Themes)
            _ColorType = value
            UpdateUI()
        End Set
    End Property
    Private _ColorType As Themes = Themes.Red
    Private Sub UpdateUI() Handles Me.Loaded
        Select Case Theme
            Case Themes.Blue
                Background = New MyColor("#d9ecff")
                BorderBrush = New MyColor("#1172D4")
                LabText.Foreground = New MyColor("#0F64B8")
                BtnClose.Foreground = New MyColor("#0F64B8")
            Case Themes.Red
                Background = New MyColor("#ffdddf")
                BorderBrush = New MyColor("#d82929")
                LabText.Foreground = New MyColor("#bf0b0b")
                BtnClose.Foreground = New MyColor("#bf0b0b")
            Case Themes.Yellow
                Background = New MyColor("#ffebd7")
                BorderBrush = New MyColor("#f57a00")
                LabText.Foreground = New MyColor("#d86c00")
                BtnClose.Foreground = New MyColor("#d86c00")
        End Select
    End Sub
    <Obsolete("IsWarn 已过时。请换用 Theme 属性。")>
    Public Property IsWarn As Boolean
        Get
            Return Theme = Themes.Red
        End Get
        Set(value As Boolean)
            Theme = If(value, Themes.Red, Themes.Blue)
        End Set
    End Property
    Public Shared ReadOnly IsWarnProperty As DependencyProperty =
        DependencyProperty.Register("IsWarn", GetType(Boolean), GetType(MyHint), New PropertyMetadata(True,
        Sub(d As MyHint, e As DependencyPropertyChangedEventArgs)
            d.Theme = If(e.NewValue, Themes.Red, Themes.Blue)
        End Sub))

    '文本
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
    End Property
    Public Shared ReadOnly TextProperty As DependencyProperty =
        DependencyProperty.Register("Text", GetType(String), GetType(MyHint), New PropertyMetadata("",
        Sub(d As MyHint, e As DependencyPropertyChangedEventArgs)
            d.LabText.Text = e.NewValue
        End Sub))

    '关闭按钮
    Public Property CanClose As Boolean
        Get
            Return BtnClose.Visibility = Visibility.Visible
        End Get
        Set(value As Boolean)
            BtnClose.Visibility = If(value, Visibility.Visible, Visibility.Collapsed)
        End Set
    End Property
    Public Property RelativeSetup As String = ""
    Private Sub MyHint_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If CanClose AndAlso Setup.Get(RelativeSetup) Then Visibility = Visibility.Collapsed
    End Sub
    Private Sub BtnClose_Click(sender As Object, e As EventArgs) Handles BtnClose.Click
        Setup.Set(RelativeSetup, True)
        AniDispose(Me, False)
    End Sub

    '触发点击事件
    Private IsMouseDown As Boolean = False
    Private Sub MyHint_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        IsMouseDown = False
        Log("[Control] 按下提示条" & If(String.IsNullOrEmpty(Name), "", "：" & Name))
        e.Handled = True
        ModEvent.TryStartEvent(EventType, EventData)
    End Sub
    Private Sub MyHint_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        IsMouseDown = True
    End Sub
    Private Sub MyHint_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
    End Sub
    Public Property EventType As String
        Get
            Return GetValue(EventTypeProperty)
        End Get
        Set(value As String)
            SetValue(EventTypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventTypeProperty As DependencyProperty = DependencyProperty.Register("EventType", GetType(String), GetType(MyHint), New PropertyMetadata(Nothing))
    Public Property EventData As String
        Get
            Return GetValue(EventDataProperty)
        End Get
        Set(value As String)
            SetValue(EventDataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventDataProperty As DependencyProperty = DependencyProperty.Register("EventData", GetType(String), GetType(MyHint), New PropertyMetadata(Nothing))

End Class
Partial Public Module ModAnimation
    Public Sub AniDispose(Control As MyHint, RemoveFromChildren As Boolean, Optional CallBack As ParameterizedThreadStart = Nothing)
        If Not Control.IsHitTestVisible Then Return
        Control.IsHitTestVisible = False
        AniStart({
            AaScaleTransform(Control, -0.08, 200,, New AniEaseInFluent),
            AaOpacity(Control, -1, 200,, New AniEaseOutFluent),
            AaHeight(Control, -Control.ActualHeight, 150, 100, New AniEaseOutFluent),
            AaCode(
            Sub()
                If RemoveFromChildren Then
                    CType(Control.Parent, Object).Children.Remove(Control)
                Else
                    Control.Visibility = Visibility.Collapsed
                End If
                If CallBack IsNot Nothing Then CallBack(Control)
            End Sub,, True)
        }, "MyCard Dispose " & Control.Uuid)
    End Sub
End Module
