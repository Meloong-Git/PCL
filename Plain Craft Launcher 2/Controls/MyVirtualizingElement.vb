Public Class MyVirtualizingElement(Of T As FrameworkElement)
    Inherits FrameworkElement

    Private Initializer As Func(Of T)
    Public Sub New(Initializer As Func(Of T))
        Me.Initializer = Initializer
        OnFirstEnterScrollViewerViewport(AddressOf Init)
    End Sub

    ''' <summary>
    ''' 实例化此控件。
    ''' </summary>
    Public Function Init() As T
        Dim Element As T = Initializer()
        If Parent IsNot Nothing Then
            If TypeOf Parent IsNot Panel Then Throw New Exception("MyVirtualizingElement 的父级必须是一个 Panel")
            Dim ParentPanel As Panel = Parent
            Dim CurrentIndex As Integer = ParentPanel.Children.IndexOf(Me)
            ParentPanel.Children.RemoveAt(CurrentIndex)
            ParentPanel.Children.Insert(CurrentIndex, Element)
        End If
        Return Element
    End Function
    Public Shared Widening Operator CType(Virtualized As MyVirtualizingElement(Of T)) As T
        Return Virtualized.Init()
    End Operator

End Class

'非泛型形式
Public Class MyVirtualizingElement
    Inherits FrameworkElement

    Private Initializer As Func(Of FrameworkElement)
    Public Sub New(Initializer As Func(Of FrameworkElement))
        Me.Initializer = Initializer
        OnFirstEnterScrollViewerViewport(AddressOf Init)
    End Sub

    ''' <summary>
    ''' 实例化此控件。
    ''' </summary>
    Public Function Init() As FrameworkElement
        Dim Element As FrameworkElement = Initializer()
        If Parent IsNot Nothing Then
            If TypeOf Parent IsNot Panel Then Throw New Exception("MyVirtualizingElement 的父级必须是一个 Panel")
            Dim ParentPanel As Panel = Parent
            Dim CurrentIndex As Integer = ParentPanel.Children.IndexOf(Me)
            ParentPanel.Children.RemoveAt(CurrentIndex)
            ParentPanel.Children.Insert(CurrentIndex, Element)
        End If
        Return Element
    End Function

    ''' <summary>
    ''' 获取实例化后的控件。
    ''' 如果该控件没有实例化，则会立即实例化。
    ''' 如果类型错误，则返回原值。
    ''' </summary>
    Public Shared Function TryInit(Element As FrameworkElement) As FrameworkElement
        If GetType(MyVirtualizingElement(Of )).IsInstanceOfGenericType(Element) Then
            Return CType(Element, Object).Init()
        ElseIf TypeOf Element Is MyVirtualizingElement Then
            Return CType(Element, MyVirtualizingElement).Init()
        Else
            Return Element
        End If
    End Function

End Class
