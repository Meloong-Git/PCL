Public Class ExceptionCatcherStackPanel
    Inherits StackPanel

    ''' <summary>
    ''' 抓到异常之后会触发这个事件，Handled 为 False 的话会再抛出去，应用程序此时正在被挂起，请不要使用 MyMsgBox
    ''' </summary>
    Public Custom Event ExceptionOccured As ExceptionOccuredEventHandler
        AddHandler(value As ExceptionOccuredEventHandler)
            [AddHandler](ExceptionOccuredEvent, value)
        End AddHandler
        RemoveHandler(value As ExceptionOccuredEventHandler)
            [RemoveHandler](ExceptionOccuredEvent, value)
        End RemoveHandler
        RaiseEvent(sender As Object, e As ExceptionOccuredEventArgs)
            [RaiseEvent](e)
        End RaiseEvent
    End Event
    Public Shared ReadOnly ExceptionOccuredEvent As RoutedEvent =
        EventManager.RegisterRoutedEvent("ExceptionOccured", RoutingStrategy.Direct, GetType(ExceptionOccuredEventHandler), GetType(ExceptionCatcherStackPanel))
    Public Delegate Sub ExceptionOccuredEventHandler(sender As Object, e As ExceptionOccuredEventArgs)
    Public Class ExceptionOccuredEventArgs
        Inherits RoutedEventArgs
        Public ReadOnly Ex As Exception
        Public Sub New(Ex As Exception)
            Me.Ex = Ex
        End Sub
    End Class
    Private Function RaiseExEventAndReturnIfHandled(Ex As Exception) As Boolean
        Dim e As New ExceptionOccuredEventArgs(Ex) With {.RoutedEvent = ExceptionOccuredEvent}
        RaiseEvent ExceptionOccured(Me, e)
        Return e.Handled
    End Function

    Protected Overrides Function MeasureOverride(constraint As Size) As Size
        Try
            Return MyBase.MeasureOverride(constraint)
        Catch ex As Exception
            If Not RaiseExEventAndReturnIfHandled(ex) Then Throw
        End Try
    End Function

    Protected Overrides Function ArrangeOverride(arrangeSize As Size) As Size
        Try
            Return MyBase.ArrangeOverride(arrangeSize)
        Catch ex As Exception
            If Not RaiseExEventAndReturnIfHandled(ex) Then Throw
        End Try
    End Function

End Class
