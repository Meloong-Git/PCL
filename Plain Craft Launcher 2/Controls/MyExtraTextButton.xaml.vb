Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyExtraTextButton

    '声明
    Public Event Click(sender As Object, e As MouseButtonEventArgs) '自定义事件

    '自定义属性
    Public Uuid As Integer = GetUuid()
    Private _Logo As String = ""
    Public Property Logo As String
        Get
            Return _Logo
        End Get
        Set(value As String)
            If value = _Logo Then Return
            _Logo = value
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
            If Path IsNot Nothing Then Path.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property
    '显示文本
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
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyExtraTextButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If sender IsNot Nothing Then CType(sender, MyExtraTextButton).LabText.Text = e.NewValue
    End Sub)))

    '动画
    Private _Show As Boolean = False
    Public Property Show As Boolean
        Get
            Return _Show
        End Get
        Set(value As Boolean)
            If _Show = value Then Return
            _Show = value
            RunInUi(
            Sub()
                If value Then
                    '有了
                    Opacity = 0
                    AniStart({
                        AaOpacity(Me, 1 - Opacity, 80, 50),
                        AaScaleTransform(Me, 0.15 - CType(RenderTransform, ScaleTransform).ScaleX, 400, 50, New AniEaseOutBack),
                        AaScaleTransform(Me, 0.85, 160, 50, New AniEaseOutFluent(AniEasePower.Middle))
                    }, "MyExtraTextButton MainScale " & Uuid)
                Else
                    '没了
                    AniStart({
                        AaOpacity(Me, -Opacity, 50, 50),
                        AaScaleTransform(Me, -CType(RenderTransform, ScaleTransform).ScaleX, 100,, New AniEaseInFluent(AniEasePower.Weak))
                    }, "MyExtraTextButton MainScale " & Uuid)
                End If
                IsHitTestVisible = value '防止缩放动画中依然可以点进去
            End Sub)
        End Set
    End Property

    '触发点击事件
    Private Sub Button_LeftMouseUp(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonUp
        If IsLeftMouseHeld Then
            Log("[Control] 按下附加图标按钮：" & Text)
            RaiseEvent Click(sender, e)
            e.Handled = True
            Button_LeftMouseUp()
        End If
    End Sub

    '鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    Private IsLeftMouseHeld As Boolean = False
    Private Sub Button_LeftMouseDown(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonDown
        If Not IsLeftMouseHeld Then
            AniStart({
                AaScaleTransform(PanScale, 0.85 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaScaleTransform(PanScale, -0.05, 60,, New AniEaseOutFluent)
            }, "MyExtraTextButton Scale " & Uuid)
        End If
        IsLeftMouseHeld = True
        Focus()
    End Sub
    Private Sub Button_LeftMouseUp() Handles PanClick.MouseLeftButtonUp
        AniStart({
            AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
        }, "MyExtraTextButton Scale " & Uuid)
        IsLeftMouseHeld = False
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_RightMouseUp() Handles PanClick.MouseRightButtonUp
        If Not IsLeftMouseHeld Then
            AniStart({
                AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
            }, "MyExtraTextButton Scale " & Uuid)
        End If
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_MouseLeave() Handles PanClick.MouseLeave
        IsLeftMouseHeld = False
        AniStart({
            AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 500,, New AniEaseOutFluent)
        }, "MyExtraTextButton Scale " & Uuid)
        RefreshColor() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub

    '自定义事件
    '务必放在 IsMouseDown 更新之后
    Private Const AnimationColorIn As Integer = 120
    Private Const AnimationColorOut As Integer = 150
    Public Sub RefreshColor() Handles PanClick.MouseEnter, PanClick.MouseLeave, Me.Loaded, Me.IsEnabledChanged
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画

                If Not IsEnabled Then
                    '禁用
                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrushGray4", AnimationColorIn), "MyExtraTextButton Color " & Uuid)
                ElseIf IsMouseOver Then
                    '指向
                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush4", AnimationColorIn), "MyExtraTextButton Color " & Uuid)
                Else
                    '普通
                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush3", AnimationColorOut), "MyExtraTextButton Color " & Uuid)
                End If

            Else

                AniStop("MyExtraTextButton Color " & Uuid)
                If Not IsEnabled Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrushGray4")
                ElseIf IsMouseOver Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush4")
                Else
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush3")
                End If

            End If
        Catch ex As Exception
            Log(ex, "刷新附加图标按钮颜色出错")
        End Try
    End Sub

End Class
