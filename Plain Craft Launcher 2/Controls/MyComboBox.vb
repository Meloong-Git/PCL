Public Class MyComboBox
    Inherits ComboBox
    Public Event TextChanged(sender As Object, e As TextChangedEventArgs)

    '基础
    Public Uuid As Integer = GetUuid()
    Private TextBox As MyTextBox
    Public Property HintText As String = ""
    Public Overrides Sub OnApplyTemplate()
        MyBase.OnApplyTemplate()
        If Not IsEditable Then Return
        Try
            TextBox = Template.FindName("PART_EditableTextBox", Me)
            TextBox.AddHandler(LostFocusEvent, New RoutedEventHandler(AddressOf RefreshColor))
            TextBox.ChangedEventList.Add(New RoutedEventHandler(Sub(sender, e) RaiseEvent TextChanged(sender, e)))
            TextBox.Tag = Tag '有时需要用文本框的 Tag 来写入设置
            If Text = "" Then
                TextBox.Text = _Text
            Else
                RaiseEvent TextChanged(Me, Nothing)
            End If
            If HintText.Length > 0 Then TextBox.HintText = HintText
        Catch ex As Exception
            Log(ex, "初始化可编辑文本框失败（" & If(Name, "") & "）", LogLevel.Feedback)
        End Try
    End Sub
    Private _Text As String = SelectedItem
    Public Shadows Property Text As String
        Get
            If IsEditable Then
                If TextBox Is Nothing Then
                    Return If(_Text, "")
                Else
                    Return If(TextBox.Text, "")
                End If
            Else
                Return If(SelectedItem, "").ToString
            End If
        End Get
        Set(value As String)
            If IsEditable Then
                If IsNothing(TextBox) Then
                    _Text = value
                Else
                    TextBox.Text = value
                End If
            Else
                Throw New NotSupportedException("该 ComboBox 不支持修改文本。")
            End If
        End Set
    End Property

    '鼠标按下接口
    Private IsMouseDown As Boolean = False
    Private Sub MyComboBox_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        IsMouseDown = True
    End Sub
    Private Sub MyComboBox_PreviewMouseLeftButtonUp(sender As Object, e As EventArgs) Handles Me.PreviewMouseLeftButtonUp, Me.MouseLeave
        IsMouseDown = False
    End Sub

    '指向动画
    Public Sub RefreshColor() Handles Me.IsEnabledChanged, Me.MouseEnter, Me.MouseLeave, Me.PreviewMouseLeftButtonDown, Me.PreviewMouseLeftButtonUp, Me.GotKeyboardFocus
        '判断当前颜色
        Dim ForeColorName As String
        Dim BackColorName As String
        Dim Time As Integer
        If IsEnabled Then
            If IsMouseDown OrElse IsDropDownOpen OrElse (IsEditable AndAlso Template.FindName("PART_EditableTextBox", Me).IsFocused) Then
                ForeColorName = "ColorBrush3"
                BackColorName = "ColorBrush7"
                Time = 10
            ElseIf IsMouseOver Then
                ForeColorName = "ColorBrush4"
                BackColorName = "ColorBrush7"
                Time = 100
            Else
                ForeColorName = "ColorBrushBg0"
                BackColorName = "ColorBrushHalfWhite"
                Time = 100
            End If
        Else
            ForeColorName = "ColorBrushGray5"
            BackColorName = "ColorBrushGray6"
            Time = 200
        End If
        '触发颜色动画
        If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
            '有动画
            AniStart({
                     AaColor(Me, ForegroundProperty, ForeColorName, Time),
                     AaColor(Me, BackgroundProperty, BackColorName, Time)
                 }, "MyComboBox Color " & Uuid)
        Else
            '无动画
            AniStop("MyComboBox Color " & Uuid)
            SetResourceReference(ForegroundProperty, ForeColorName)
            SetResourceReference(BackgroundProperty, BackColorName)
        End If
    End Sub

    Private RealWidth As Double '由于下拉框 Popup 宽度与 Width 一致，故不能为 NaN（Auto）
    Private Sub MyComboBox_DropDownOpened(sender As Object, e As EventArgs) Handles Me.DropDownOpened
        RealWidth = Width
        Width = ActualWidth
        Try
            CType(Template.FindName("PanPopup", Me), Grid).Opacity = FrmMain.Opacity
        Catch ex As Exception
            Log(ex, "设置下拉框透明度失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub MyComboBox_DropDownClosed(sender As Object, e As EventArgs) Handles Me.DropDownClosed
        Width = RealWidth
    End Sub

    '修复 WPF Bug：下拉框文本修改后，依然误认为还选择着此前的选项，导致再次点击该选项时内容不变
    Private IsTextChanging As Boolean = False
    Private Sub MyComboBox_TextChanged(sender As Object, e As TextChangedEventArgs) Handles Me.TextChanged
        If IsTextChanging OrElse Not IsEditable Then Return
        If SelectedItem IsNot Nothing AndAlso Text <> SelectedItem.ToString Then
            Dim RawText As String = Text
            Dim RawSelectionStart As Integer = TextBox.SelectionStart
            IsTextChanging = True
            SelectedItem = Nothing
            Text = RawText
            TextBox.SelectionStart = RawSelectionStart
            IsTextChanging = False
        End If
    End Sub

    Public ReadOnly Property ContentPresenter As ContentPresenter
        Get
            Return Template.FindName("PART_Content", Me)
        End Get
    End Property

End Class
