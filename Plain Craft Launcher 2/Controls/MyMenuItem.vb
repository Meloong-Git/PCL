Public Class MyMenuItem
    Inherits MenuItem

    Private Sub MyMenuItem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Icon IsNot Nothing Then
            Dim IconControl As Shapes.Path = GetTemplateChild("Icon")
            If IconControl IsNot Nothing Then IconControl.Data = (New GeometryConverter).ConvertFromString(Icon)
        End If
        '对父级设置透明度
        CType(Parent, ContextMenu).Opacity = Setup.Get("UiLauncherTransparent") / 1000 + 0.4
    End Sub

    '基础

    Public Uuid As Integer = GetUuid()

    '指向动画

    Private Const AnimationTimeIn As Integer = 100
    Private Const AnimationTimeOut As Integer = 200
    Private ColorName As String
    Private Sub RefreshColor() Handles Me.MouseEnter, Me.MouseLeave, Me.IsEnabledChanged
        '判断当前颜色
        Dim BackName As String, ForeName As String
        Dim Time As Integer
        If Not IsEnabled Then
            BackName = "ColorBrushTransparent"
            ForeName = "ColorBrushGray5"
            Time = AnimationTimeOut
        ElseIf IsMouseOver Then
            BackName = "ColorBrush6"
            ForeName = "ColorBrush2"
            Time = AnimationTimeIn
        Else
            BackName = "ColorBrushTransparent"
            ForeName = "ColorBrush1"
            Time = AnimationTimeOut
        End If
        '重复性验证
        If ColorName = BackName Then Return
        ColorName = BackName
        '触发颜色动画
        If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
            '有动画
            AniStart({
                     AaColor(Me, BackgroundProperty, BackName, Time),
                     AaColor(Me, ForegroundProperty, ForeName, Time)
                 }, "MyMenuItem Color " & Uuid)
        Else
            '无动画
            AniStop("MyMenuItem Color " & Uuid)
            SetResourceReference(BackgroundProperty, BackName)
            SetResourceReference(ForegroundProperty, ForeName)
        End If
    End Sub

End Class
