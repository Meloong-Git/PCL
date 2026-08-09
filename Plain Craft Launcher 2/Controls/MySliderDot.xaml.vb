Public Class MySliderDot

    '基础

    Public Uuid As Integer = GetUuid()
    Public Event Change(sender As Object, user As Boolean)
    Public Event PreviewChange(sender As Object, e As RouteEventArgs)

    '自定义属性

    Private _MaxValue As Integer = 100
    Public Property MaxValue As Integer
        Get
            Return _MaxValue
        End Get
        Set(value As Integer)
            If value = _MaxValue Then Exit Property
            _MaxValue = value
            RefreshHeight(Nothing, Nothing)
        End Set
    End Property
    Private ChangeByKey As Boolean = False
    Private _Value As Integer = 0
    Public Property Value As Integer
        Get
            Return _Value
        End Get
        Set(newValue As Integer)
            Try

                newValue = MathRange(newValue, 0, MaxValue)
                If _Value = newValue Then Exit Property

                '触发前瞻事件，修改新值
                Dim OldValue = _Value
                _Value = newValue
                If AniControlEnabled = 0 Then
                    Dim e = New RouteEventArgs(False)
                    RaiseEvent PreviewChange(Me, e)
                    If e.Handled Then
                        _Value = OldValue
                        Exit Property
                    End If
                End If

                If IsLoaded AndAlso AniControlEnabled = 0 Then
                    If ActualHeight < 16 Then Exit Property
                    Dim NewHeight As Double = _Value / MaxValue * (ActualHeight - 16)
                    Dim DeltaProcess As Double = Math.Abs(LineFore.Height / (ActualHeight - 16) - _Value / MaxValue)
                    Dim Time As Double = (1 - Math.Pow(1 - DeltaProcess, 3)) * 400 + If(ChangeByKey, 100, 0)
                    AniStart({
                            AaHeight(LineFore, Math.Max(0, NewHeight + If(NewHeight < 0.5, 0, 0.5)) - LineFore.Height, Time,, If(Time > 50, New AniEaseOutFluent, New AniEaseLinear)),
                            AaHeight(LineBack, Math.Max(0, ActualHeight - 16 - NewHeight + If(ActualHeight - 16 - NewHeight < 0.5, 0, 0.5)) - LineBack.Height, Time,, If(Time > 50, New AniEaseOutFluent, New AniEaseLinear)),
                            AaY(ShapeDot, NewHeight - ShapeDot.Margin.Top, Time,, If(Time > 50, New AniEaseOutFluent, New AniEaseLinear))
                         }, "MySlider Progress " & Uuid)
                Else
                    RefreshHeight(Nothing, Nothing)
                End If
                If AniControlEnabled = 0 Then RaiseEvent Change(Me, False)

            Catch ex As Exception
                Logger.Warning(ex, "滑动条进度改变出错", LogLevel.Hint)
            End Try
        End Set
    End Property
    Private Sub RefreshHeight(sender As Object, e As SizeChangedEventArgs) Handles Me.SizeChanged
        If Not IsNothing(e) Then PanMain.Height = e.NewSize.Height
        AniStop("MySlider Progress " & Uuid)
        Dim NewHeight As Double = _Value / MaxValue * (ActualHeight - 16)
        LineFore.Height = Math.Max(0, NewHeight + If(NewHeight < 0.5, 0, 0.5))
        LineBack.Height = Math.Max(0, ActualHeight - 16 - NewHeight + If(ActualHeight - 16 - NewHeight < 0.5, 0, 0.5))
        SetTop(ShapeDot, NewHeight)
    End Sub

    '拖动

    Public GetHintText As [Delegate]
    Private Sub DragStart(sender As Object, e As MouseButtonEventArgs) Handles ShapeDot.MouseLeftButtonDown
        e.Handled = True '防止 ScrollViewer 失焦问题
        DragControl = Me
        AniStart({
                 AaScaleTransform(ShapeDot, 2 - CType(ShapeDot.RenderTransform, ScaleTransform).ScaleX, 100,, New AniEaseOutFluent)
            }, "MySlider Scale " & Uuid)
    End Sub
    Public Sub DragDoing()
        Dim Percent As Double = MathRange((Mouse.GetPosition(PanMain).Y - 8) / (ActualHeight - 16), 0, 1)
        Dim NewValue As Integer = Percent * MaxValue
        If Not NewValue = Value Then Value = NewValue
    End Sub
    Public Sub DragStop()
        RefreshColor()
        AniStart({
                 AaScaleTransform(ShapeDot, 2.3 - CType(ShapeDot.RenderTransform, ScaleTransform).ScaleX, 200,, New AniEaseOutFluent)
            }, "MySlider Scale " & Uuid)
        If Value > 0 Then
            Value = MaxValue
        Else
            FrmMain.SliderDrag_Finish()
        End If
    End Sub

    '指向动画

    Private Sub RefreshColor() Handles Me.IsEnabledChanged, Me.MouseEnter, Me.MouseLeave
        'Try

        '    '判断当前颜色
        '    Dim ColorName As String
        '    Dim AnimationTime As Integer
        '    If IsEnabled Then
        '        If IsMouseOver OrElse (Not IsNothing(DragControl) AndAlso DragControl.Equals(Me)) Then
        '            ColorName = "ColorBrush3"
        '            AnimationTime = 100
        '        Else
        '            ColorName = "ColorBrush1"
        '            AnimationTime = 200
        '        End If
        '    Else
        '        ColorName = "ColorBrushGray4"
        '        AnimationTime = 200
        '    End If
        '    '触发颜色动画
        '    If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
        '        '有动画
        '        AniStart({AaColor(Me, BorderBrushProperty, ColorName, AnimationTime)}, "MySlider Color " & Uuid)
        '    Else
        '        '无动画
        '        AniStop("MySlider Color " & Uuid)
        '        SetResourceReference(BorderBrushProperty, ColorName)
        '    End If

        'Catch ex As Exception
        '    Log(ex, "滑动条颜色改变出错")
        'End Try
    End Sub

    '按键改变

    'Public Property ValueByKey As UInteger = 1
    'Private Sub MySlider_MouseEnter() Handles Me.MouseEnter
    '    Focus() '确保按键能改变值
    'End Sub
    'Private Sub MySlider_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
    '    '拒绝一边拖动一边用按键改变
    '    If ReferenceEquals(Me, DragControl) Then Exit Sub
    '    '改变值
    '    If e.Key = Key.Left Then
    '        ChangeByKey = True
    '        Value -= ValueByKey
    '        ChangeByKey = False
    '        e.Handled = True
    '    ElseIf e.Key = Key.Right Then
    '        ChangeByKey = True
    '        Value += ValueByKey
    '        ChangeByKey = False
    '        e.Handled = True
    '    Else
    '        Exit Sub
    '    End If
    '    '更新 Popup
    '    If GetHintText IsNot Nothing Then
    '        TextHint.Text = GetHintText.DynamicInvoke(Value)
    '        Popup.IsOpen = True
    '        AniStop("MySlider KeyPopup " & Uuid)
    '        AniStart(AaCode(Sub() Popup.IsOpen = False, 700 * AniSpeed), "MySlider KeyPopup " & Uuid)
    '    End If
    'End Sub

End Class