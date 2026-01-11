Public Module LazyLoadBehavior

    ''' <summary>
    ''' 指定首次进入 ScrollViewer 的可视范围时执行的操作。
    ''' </summary>
    <Runtime.CompilerServices.Extension()>
    Public Sub OnFirstEnterScrollViewerViewport(obj As DependencyObject, value As Action)
        obj.SetValue(IsInViewportProperty, value)
    End Sub

    Private ReadOnly IsInViewportProperty As DependencyProperty =
        DependencyProperty.RegisterAttached("IsInViewport", GetType(Action), GetType(LazyLoadBehavior),
        New PropertyMetadata(Nothing, AddressOf OnIsInViewportChanged))

    Private Sub OnIsInViewportChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim Element = TryCast(d, FrameworkElement)
        If Element Is Nothing OrElse e.NewValue Is Nothing Then Return

        Dim Handled As Boolean = False
        Dim Handler As EventHandler =
        Sub()
            If Handled Then Return
            '判断元素是否可见
            If Element.RenderSize.Width < Double.Epsilon Then Return
            If Not Element.IsVisible Then Return
            '判断是否在 ScrollViewer 的可视区域内
            Dim Scroll = FindParentScrollViewer(Element)
            If Scroll Is Nothing Then Return
            If Not New Rect(0, 0, Scroll.ViewportWidth, Scroll.ViewportHeight).IntersectsWith(
               Element.TransformToAncestor(Scroll).TransformBounds(New Rect(New Point(0, 0), Element.RenderSize))) Then Return
            '执行
            Handled = True
            CType(e.NewValue, Action).Invoke()
            RemoveHandler Element.LayoutUpdated, Handler
        End Sub
        AddHandler Element.LayoutUpdated, Handler
    End Sub

    Private Function FindParentScrollViewer(d As DependencyObject) As ScrollViewer
        While d IsNot Nothing
            If TypeOf d Is ScrollViewer Then Return CType(d, ScrollViewer)
            d = VisualTreeHelper.GetParent(d)
        End While
        Return Nothing
    End Function

End Module
