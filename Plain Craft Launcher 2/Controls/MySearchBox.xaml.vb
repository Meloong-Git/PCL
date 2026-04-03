Class MySearchBox
    Inherits MyCard

    Public Event TextChanged(sender As Object, e As EventArgs)
    Private Sub MySearchBox_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        TextBox.Focus()
    End Sub

    '属性
    Public Property HintText() As String
        Get
            Return TextBox.HintText
        End Get
        Set(value As String)
            TextBox.HintText = value
        End Set
    End Property
    Public Property Text() As String
        Get
            Return TextBox.Text
        End Get
        Set(value As String)
            TextBox.Text = value
        End Set
    End Property

    Private Sub Text_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextBox.TextChanged
        If String.IsNullOrEmpty(TextBox.Text) Then
            AniStart(AaOpacity(BtnClear, -BtnClear.Opacity, 90), "MySearchBox ClearBtn " & Uuid)
            BtnClear.IsHitTestVisible = False
        Else
            AniStart(AaOpacity(BtnClear, 1 - BtnClear.Opacity, 90), "MySearchBox ClearBtn " & Uuid)
            BtnClear.IsHitTestVisible = True
        End If
        RaiseEvent TextChanged(sender, e)
    End Sub
    Private Sub BtnClear_Click(sender As Object, e As EventArgs) Handles BtnClear.Click
        TextBox.Text = ""
        TextBox.Focus()
    End Sub

    Private Sub MySearchBox_KeyUp(sender As Object, e As KeyEventArgs) Handles Me.KeyUp
        If e.Key = Key.Enter Then RaiseCustomEvent()
    End Sub
End Class
