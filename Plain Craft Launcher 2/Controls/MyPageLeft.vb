Public Class MyPageLeft
    Inherits Grid
    Private Uuid As Integer = GetUuid()

    '执行逐个进入动画的控件
    Public Property AnimatedControl As FrameworkElement
        Get
            Return GetValue(AnimatedControlProperty)
        End Get
        Set(value As FrameworkElement)
            SetValue(AnimatedControlProperty, value)
        End Set
    End Property
    Public Shared ReadOnly AnimatedControlProperty As DependencyProperty = DependencyProperty.Register("AnimatedControl", GetType(FrameworkElement), GetType(MyPageLeft), New PropertyMetadata(Nothing))

    Public Sub TriggerShowAnimation()
        If AnimatedControl Is Nothing Then
            '缩放动画
            If TypeOf RenderTransform IsNot ScaleTransform Then
                RenderTransform = New ScaleTransform(0.96, 0.96)
                RenderTransformOrigin = New Point(0.5, 0.5)
            End If
            Opacity = 0
            AniStart({
                AaScaleTransform(Me, 1 - CType(RenderTransform, ScaleTransform).ScaleX, 400,, New AniEaseOutBack(2)),
                AaOpacity(Me, 1, 100)
            }, "PageLeft PageChange " & Uuid)
        Else
            '逐个进入动画
            Dim AniList As New List(Of AniData)
            Dim Id As Integer = 0, Delay As Integer = 0
            For Each Element As FrameworkElement In GetAllAnimControls(True)
                If Element.Visibility = Visibility.Collapsed Then
                    '还原之前的隐藏动画可能导致的改变（#2436）
                    Element.Opacity = 1
                    Element.RenderTransform = New TranslateTransform(0, 0)
                    If TypeOf Element Is MyListItem Then CType(Element, MyListItem).IsMouseOverAnimationEnabled = True
                Else
                    Element.Opacity = 0
                    Element.RenderTransform = New TranslateTransform(-25, 0)
                    If TypeOf Element Is MyListItem Then CType(Element, MyListItem).IsMouseOverAnimationEnabled = False
                    AniList.Add(AaOpacity(Element, If(TypeOf Element Is TextBlock, 0.6, 1), 100, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
                    AniList.Add(AaTranslateX(Element, 5, 200, Delay, New AniEaseOutFluent))
                    AniList.Add(AaTranslateX(Element, 20, 300, Delay, New AniEaseOutBack(AniEasePower.Weak)))
                    If TypeOf Element Is MyListItem Then
                        AniList.Add(AaCode(
                        Sub()
                            CType(Element, MyListItem).IsMouseOverAnimationEnabled = True
                            CType(Element, MyListItem).RefreshColor(Me, New EventArgs)
                        End Sub, Delay + 280))
                    End If
                    Delay += Math.Max(15 - Id, 7) * 2
                    Id += 1
                End If
            Next
            AniStart(AniList, "PageLeft PageChange " & Uuid)
        End If
    End Sub
    Public Sub TriggerHideAnimation()
        If AnimatedControl Is Nothing Then
            '缩放动画
            If TypeOf RenderTransform IsNot ScaleTransform Then
                RenderTransform = New ScaleTransform(1, 1)
                RenderTransformOrigin = New Point(0.5, 0.5)
            End If
            AniStart({
                AaScaleTransform(Me, 0.95 - CType(RenderTransform, ScaleTransform).ScaleX, 110,, New AniEaseInFluent(AniEasePower.Weak)),
                AaOpacity(Me, -Opacity, 80, 30)
            }, "PageLeft PageChange " & Uuid)
        Else
            '逐个退出动画
            Dim AniList As New List(Of AniData)
            Dim Id As Integer = 0
            Dim Controls = GetAllAnimControls()
            For Each Element As FrameworkElement In Controls
                AniList.Add(AaOpacity(Element, -Element.Opacity, 50, 70 / Controls.Count * Id))
                AniList.Add(AaTranslateX(Element, -6, 50, 70 / Controls.Count * Id))
                Id += 1
            Next
            AniStart(AniList, "PageLeft PageChange " & Uuid)
        End If
    End Sub

    '遍历获取所有需要生成动画的控件
    Private Function GetAllAnimControls(Optional IgnoreInvisibility As Boolean = False) As List(Of FrameworkElement)
        Dim AllControls As New List(Of FrameworkElement)
        GetAllAnimControls(AnimatedControl, AllControls, IgnoreInvisibility)
        Return AllControls
    End Function
    Private Sub GetAllAnimControls(Element As FrameworkElement, ByRef AllControls As List(Of FrameworkElement), IgnoreInvisibility As Boolean)
        If Not IgnoreInvisibility AndAlso Element.Visibility = Visibility.Collapsed Then Return
        If TypeOf Element Is MyTextButton Then
            AllControls.Add(Element)
        ElseIf TypeOf Element Is MyListItem Then
            AllControls.Add(Element)
        ElseIf TypeOf Element Is ContentControl Then
            GetAllAnimControls(CType(Element, ContentControl).Content, AllControls, IgnoreInvisibility)
        ElseIf TypeOf Element Is Panel Then
            For Each Element2 As FrameworkElement In CType(Element, Panel).Children
                GetAllAnimControls(Element2, AllControls, IgnoreInvisibility)
            Next
        Else
            AllControls.Add(Element)
        End If
    End Sub

End Class

Public Interface IRefreshable
    Sub Refresh()
End Interface