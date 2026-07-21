Public Class MyScrollBar
    Inherits Primitives.ScrollBar

    '基础

    Public Uuid As Integer = GetUuid()

    '指向动画

    Private Sub RefreshColor() Handles Me.IsEnabledChanged, Me.GotMouseCapture, Me.LostMouseCapture, Me.MouseEnter, Me.MouseLeave, Me.IsVisibleChanged
        Try

            '判断当前颜色
            Dim NewOpacity As Double, NewColor As String, Time As Integer
            If Not IsVisible Then
                NewOpacity = 0
                Time = 20 '防止错误的尺寸判断导致闪烁
                NewColor = "ColorBrush4"
            ElseIf IsMouseCaptureWithin Then
                NewOpacity = 1
                NewColor = "ColorBrush4"
                Time = 50
            ElseIf IsMouseOver Then
                NewOpacity = 0.9
                NewColor = "ColorBrush3"
                Time = 130
            Else
                NewOpacity = 0.5
                NewColor = "ColorBrush4"
                Time = 180
            End If
            '触发颜色动画
            If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
                '有动画
                AniStart({
                         AaColor(Me, ForegroundProperty, NewColor, Time),
                         AaOpacity(Me, NewOpacity - Opacity, Time)
                 }, "MyScrollBar Color " & Uuid)
            Else
                '无动画
                AniStop("MyScrollBar Color " & Uuid)
                SetResourceReference(ForegroundProperty, NewColor)
                Opacity = NewOpacity
            End If

        Catch ex As Exception
            Logger.Warn(ex, "滚动条颜色改变出错")
        End Try
    End Sub

End Class
