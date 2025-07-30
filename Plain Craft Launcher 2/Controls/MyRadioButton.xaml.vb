Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyRadioButton

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

    Private _Checked As Boolean = False '是否选中
    Public Property Checked As Boolean
        Get
            Return _Checked
        End Get
        Set(value As Boolean)
            SetChecked(value, False, True)
        End Set
    End Property
    ''' <summary>
    ''' 手动设置 Checked 属性。
    ''' </summary>
    ''' <param name="value">新的 Checked 属性。</param>
    ''' <param name="user">是否由用户引发。</param>
    ''' <param name="anime">是否执行动画。</param>
    Public Sub SetChecked(value As Boolean, user As Boolean, anime As Boolean)
        Try

            '自定义属性基础

            Dim IsChanged As Boolean = False
            If IsLoaded AndAlso Not value = _Checked Then RaiseEvent Change(Me, user)
            If Not value = _Checked Then
                _Checked = value
                IsChanged = True
            End If

            '保证只有一个单选框选中

            If IsNothing(Parent) Then Return
            Dim RadioboxList As New List(Of MyRadioButton)
            Dim CheckedCount As Integer = 0
            '收集控件列表与选中个数
            For Each Control In CType(Parent, Object).Children
                If TypeOf Control Is MyRadioButton Then
                    RadioboxList.Add(Control)
                    If Control.Checked Then CheckedCount += 1
                End If
            Next
            '判断选中情况
            Select Case CheckedCount
                Case 0
                    '没有任何单选框被选中，选择第一个
                    RadioboxList(0).Checked = True
                Case Is > 1
                    '选中项目多于 1 个
                    If Me.Checked Then
                        '如果本控件选中，则取消其他所有控件的选中
                        For Each Control As MyRadioButton In RadioboxList
                            If Control.Checked AndAlso Not Control.Equals(Me) Then Control.Checked = False
                        Next
                    Else
                        '如果本控件未选中，则只保留第一个选中的控件
                        Dim FirstChecked = False
                        For Each Control As MyRadioButton In RadioboxList
                            If Control.Checked Then
                                If FirstChecked Then
                                    Control.Checked = False '修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                Else
                                    FirstChecked = True
                                End If
                            End If
                        Next
                    End If
            End Select

            '更改动画

            If Not IsChanged Then Return
            RefreshColor(Nothing, anime)

            '触发事件
            If Checked Then RaiseEvent Check(Me, user)

        Catch ex As Exception
            Log(ex, "单选按钮勾选改变错误", LogLevel.Hint)
        End Try
    End Sub

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
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyRadioButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender, e) If sender IsNot Nothing Then CType(sender, MyRadioButton).LabText.Text = e.NewValue)))
    Public Enum ColorState
        White
        Highlight
    End Enum
    Private _ColorType As ColorState = ColorState.White
    Public Property ColorType As ColorState
        Get
            Return _ColorType
        End Get
        Set(value As ColorState)
            _ColorType = value
            RefreshColor()
        End Set
    End Property '颜色类别

    '点击事件

    Public Event PreviewClick(sender As Object, e As RouteEventArgs)
    Private IsMouseDown As Boolean = False
    Private Sub Radiobox_MouseUp() Handles Me.MouseLeftButtonUp
        If Checked Then Return
        If Not IsMouseDown Then Return
        Log("[Control] 按下单选按钮：" & Text)
        IsMouseDown = False
        Dim e As New RouteEventArgs(True)
        RaiseEvent PreviewClick(Me, e)
        If e.Handled Then Return
        SetChecked(True, True, True)
    End Sub
    Private Sub Radiobox_MouseDown() Handles Me.MouseLeftButtonDown
        If Checked Then Return
        IsMouseDown = True
        RefreshColor()
    End Sub
    Private Sub Radiobox_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
    End Sub

    '动画

    Private Const AnimationTimeOfMouseIn As Integer = 90 '鼠标指向动画长度
    Private Const AnimationTimeOfMouseOut As Integer = 150 '鼠标移出动画长度
    Private Const AnimationTimeOfCheck As Integer = 120 '勾选状态变更动画长度
    Private Sub RefreshColor(Optional obj = Nothing, Optional e = Nothing) Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 AndAlso Not False.Equals(e) Then '防止默认属性变更触发动画，若强制不执行动画，则 e 为 False

                Select Case ColorType
                    Case ColorState.White
                        If Checked Then
                            '勾选
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfCheck),
                                         AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfCheck)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, New MyColor(255, 255, 255) - Background, AnimationTimeOfCheck), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseDown Then
                            '按下
                            AniStart(AaColor(Me, BackgroundProperty, New MyColor(120, Color8) - Background, 60), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            '指向
                            AniStart({
                                 AaColor(ShapeLogo, Shapes.Path.FillProperty, New MyColor(255, 255, 255) - ShapeLogo.Fill, AnimationTimeOfMouseIn),
                                 AaColor(LabText, TextBlock.ForegroundProperty, New MyColor(255, 255, 255) - LabText.Foreground, AnimationTimeOfMouseIn)
                            }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, New MyColor(50, Color8) - Background, AnimationTimeOfMouseIn), "MyRadioButton Color " & Uuid)
                        Else
                            '正常
                            AniStart({
                                 AaColor(ShapeLogo, Shapes.Path.FillProperty, New MyColor(255, 255, 255) - ShapeLogo.Fill, AnimationTimeOfMouseOut),
                                 AaColor(LabText, TextBlock.ForegroundProperty, New MyColor(255, 255, 255) - LabText.Foreground, AnimationTimeOfMouseOut)
                            }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyRadioButton Color " & Uuid)
                        End If
                    Case ColorState.Highlight
                        If Checked Then
                            '勾选
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, New MyColor(255, 255, 255) - ShapeLogo.Fill, AnimationTimeOfCheck),
                                         AaColor(LabText, TextBlock.ForegroundProperty, New MyColor(255, 255, 255) - LabText.Foreground, AnimationTimeOfCheck)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush3", AnimationTimeOfCheck), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseDown Then
                            '按下
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush6", AnimationTimeOfMouseIn), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            '指向
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseIn),
                                         AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush7", AnimationTimeOfMouseIn), "MyRadioButton Color " & Uuid)
                        Else
                            '正常
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseOut),
                                         AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseOut)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyRadioButton Color " & Uuid)
                        End If
                End Select

            Else

                '不使用动画
                AniStop("MyRadioButton Checked " & Uuid)
                AniStop("MyRadioButton Color " & Uuid)
                Select Case ColorType
                    Case ColorState.White
                        If Checked Then
                            Background = New MyColor(255, 255, 255)
                            ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, "ColorBrush3")
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                        Else
                            Background = ColorSemiTransparent
                            ShapeLogo.Fill = New MyColor(255, 255, 255)
                            LabText.Foreground = New MyColor(255, 255, 255)
                        End If
                    Case ColorState.Highlight
                        If Checked Then
                            SetResourceReference(BackgroundProperty, "ColorBrush3")
                            ShapeLogo.Fill = New MyColor(255, 255, 255)
                            LabText.Foreground = New MyColor(255, 255, 255)
                        Else
                            Background = ColorSemiTransparent
                            ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, "ColorBrush3")
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                        End If
                End Select

            End If
        Catch ex As Exception
            Log(ex, "刷新单选按钮颜色出错")
        End Try
    End Sub

End Class
