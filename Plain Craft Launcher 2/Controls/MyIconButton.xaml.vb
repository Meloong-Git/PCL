Public Class MyIconButton

    '自定义事件
    Public Event Click(sender As Object, e As EventArgs)

    '自定义属性

    Public Uuid As Integer = GetUuid()
    Public Property Logo As String
        Get
            Return Path.Data.ToString
        End Get
        Set(value As String)
            Path.Data = (New GeometryConverter).ConvertFromString(value)
        End Set
    End Property

    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If Not IsNothing(Path) Then Path.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property

    Public Enum Themes
        Color
        White
        Black
        Red
        Custom
    End Enum
    Public Property Theme As Themes = Themes.Color

    Private _Foreground As New SolidColorBrush(Color.FromRgb(128, 128, 128))
    Public Property Foreground As SolidColorBrush
        Get
            Return _Foreground
        End Get
        Set(value As SolidColorBrush)
            _Foreground = value
            AniControlEnabled += 1
            RefreshAnim()
            AniControlEnabled -= 1
        End Set
    End Property

    '触发点击事件
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        Log("[Control] 按下图标按钮" & If(String.IsNullOrEmpty(Name), "", "：" & Name))
        RaiseEvent Click(sender, e)
        e.Handled = True
        Button_MouseUp()
        ModEvent.TryStartEvent(EventType, EventData)
    End Sub
    Public Property EventType As String
        Get
            Return GetValue(EventTypeProperty)
        End Get
        Set(value As String)
            SetValue(EventTypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventTypeProperty As DependencyProperty = DependencyProperty.Register("EventType", GetType(String), GetType(MyIconButton), New PropertyMetadata(Nothing))
    Public Property EventData As String
        Get
            Return GetValue(EventDataProperty)
        End Get
        Set(value As String)
            SetValue(EventDataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EventDataProperty As DependencyProperty = DependencyProperty.Register("EventData", GetType(String), GetType(MyIconButton), New PropertyMetadata(Nothing))

    '鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    Private IsMouseDown As Boolean = False
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        IsMouseDown = True
        Focus()
        '指向
        AniStart(AaScaleTransform(PanBack, 0.8 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 400,, New AniEaseOutFluent(AniEasePower.Strong)), "MyIconButton Scale " & Uuid)
    End Sub
    Private Sub Button_MouseUp() Handles Me.MouseLeftButtonUp
        If IsMouseDown Then
            IsMouseDown = False
            AniStart({
                     AaScaleTransform(PanBack, 1.05 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 250,, New AniEaseOutBack(AniEasePower.Weak)),
                     AaScaleTransform(PanBack, -0.05, 250,, New AniEaseOutFluent(AniEasePower.Strong))
                 }, "MyIconButton Scale " & Uuid)
        End If
        RefreshAnim() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
        AniStart({
                     AaScaleTransform(PanBack, 1 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 250,, New AniEaseOutFluent)
                 }, "MyIconButton Scale " & Uuid)
        RefreshAnim() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub

    '自定义事件
    '务必放在 IsMouseDown 更新之后
    Private Const AnimationColorIn As Integer = 120
    Private Const AnimationColorOut As Integer = 150
    Public Sub RefreshAnim() Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画

                If PanBack.Background Is Nothing Then PanBack.Background = New MyColor(0, 255, 255, 255)
                If Path.Fill Is Nothing Then
                    Select Case Theme
                        Case Themes.Red
                            Path.Fill = New MyColor(160, 255, 76, 76)
                        Case Themes.Black
                            Path.Fill = New MyColor(160, 0, 0, 0)
                        Case Themes.Custom
                            Path.Fill = New MyColor(160, Foreground)
                    End Select
                End If
                If IsMouseOver Then
                    '指向
                    Dim AnimList As New List(Of AniData)
                    Select Case Theme
                        Case Themes.Color
                            AnimList.Add(AaColor(Path, Shape.FillProperty, "ColorBrush2", AnimationColorIn))
                        Case Themes.White
                            AnimList.Add(AaColor(PanBack, BackgroundProperty, New MyColor(50, 255, 255, 255) - PanBack.Background, AnimationColorIn))
                        Case Themes.Red
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(255, 76, 76) - Path.Fill, AnimationColorIn))
                        Case Themes.Black
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(230, 0, 0, 0) - Path.Fill, AnimationColorIn))
                        Case Themes.Custom
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(255, Foreground) - Path.Fill, AnimationColorIn))
                    End Select
                    AniStart(AnimList, "MyIconButton Color " & Uuid)
                Else
                    '普通
                    Dim AnimList As New List(Of AniData)
                    Select Case Theme
                        Case Themes.Color
                            AnimList.Add(AaColor(Path, Shape.FillProperty, "ColorBrush4", AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.White
                            AnimList.Add(AaColor(Path, Shape.FillProperty, "ColorBrush8", AnimationColorOut))
                            AnimList.Add(AaColor(PanBack, BackgroundProperty, New MyColor(0, 255, 255, 255) - PanBack.Background, AnimationColorOut))
                        Case Themes.Red
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(160, 255, 76, 76) - Path.Fill, AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.Black
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(160, 0, 0, 0) - Path.Fill, AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.Custom
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(160, Foreground) - Path.Fill, AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                    End Select
                    AniStart(AnimList, "MyIconButton Color " & Uuid)
                End If

            Else

                AniStop("MyIconButton Color " & Uuid)
                Select Case Theme
                    Case Themes.Color
                        Path.SetResourceReference(Shape.FillProperty, "ColorBrush5")
                    Case Themes.White
                        Path.SetResourceReference(Shape.FillProperty, "ColorBrush8")
                    Case Themes.Red
                        Path.Fill = New MyColor(160, 255, 76, 76)
                    Case Themes.Black
                        Path.Fill = New MyColor(160, 0, 0, 0)
                    Case Themes.Custom
                        Path.Fill = New MyColor(160, Foreground)
                End Select
                PanBack.Background = New MyColor(0, 255, 255, 255)

            End If
        Catch ex As Exception
            Log(ex, "刷新图标按钮动画状态出错")
        End Try
    End Sub

End Class
Partial Public Module ModAnimation
    Public Sub AniDispose(Control As MyIconButton, RemoveFromChildren As Boolean, Optional CallBack As ParameterizedThreadStart = Nothing)
        If Not Control.IsHitTestVisible Then Return
        Control.IsHitTestVisible = False
        AniStart({
                 AaScaleTransform(Control, -1.5, 200,, New AniEaseInFluent),
                 AaCode(Sub()
                            If RemoveFromChildren Then
                                CType(Control.Parent, Object).Children.Remove(Control)
                            Else
                                Control.Visibility = Visibility.Collapsed
                            End If
                            If CallBack IsNot Nothing Then CallBack(Control)
                        End Sub,, True)
        }, "MyIconButton Dispose " & Control.Uuid)
    End Sub
End Module
