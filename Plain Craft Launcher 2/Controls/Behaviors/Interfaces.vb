Imports System.Windows.Threading

''' <summary>
''' 指示页面可以通过按下 F5 或以其他方式刷新。
''' </summary>
Public Interface IRefreshable
    Sub Refresh()
End Interface

''' <summary>
''' 指示页面可以处理未捕获的异常。
''' </summary>
Public Interface IDispatcherUnhandledException
    Sub DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)
End Interface

''' <summary>
''' 指示该控件是可勾选的。
''' </summary>
Public Interface IMyRadio
    Event Check(sender As Object, e As RouteEventArgs)
    Event Changed(sender As Object, e As RouteEventArgs)
End Interface
