Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Windows.Threading

Public Class MyResizer

    Private Delegate Sub RefreshDelegate()

    Private Structure POINT
        Public x As Integer
        Public y As Integer
        Public Sub New(x As Integer, y As Integer)
            Me.x = x
            Me.y = y
        End Sub
    End Structure
    Private Structure MINMAXINFO
        Public ptReserved As POINT
        Public ptMaxSize As POINT
        Public ptMaxPosition As POINT
        Public ptMinTrackSize As POINT
        Public ptMaxTrackSize As POINT
    End Structure
    Private Structure RECT
        Public left As Integer
        Public top As Integer
        Public right As Integer
        Public bottom As Integer
        Public Shared Empty As RECT = Nothing

        Public ReadOnly Property Width() As Integer
            Get
                Return Math.Abs(Me.right - Me.left)
            End Get
        End Property
        Public ReadOnly Property Height() As Integer
            Get
                Return Me.bottom - Me.top
            End Get
        End Property

        Public Sub New(left As Integer, top As Integer, right As Integer, bottom As Integer)
            Me.left = left
            Me.top = top
            Me.right = right
            Me.bottom = bottom
        End Sub
        Public Sub New(rcSrc As RECT)
            Me.left = rcSrc.left
            Me.top = rcSrc.top
            Me.right = rcSrc.right
            Me.bottom = rcSrc.bottom
        End Sub
    End Structure

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Private Class MONITORINFO
        Public cbSize As Integer = Marshal.SizeOf(GetType(MONITORINFO))
        Public rcMonitor As RECT = Nothing
        Public rcWork As RECT = Nothing
        Public dwFlags As Integer = 0
    End Class

    Private Structure PointAPI
        Public X As Integer
        Public Y As Integer
    End Structure

    Private ReadOnly target As Window = Nothing

    Private resizeRight As Boolean = False
    Private resizeLeft As Boolean = False
    Private resizeUp As Boolean = False
    Private resizeDown As Boolean = False

    Private ReadOnly leftElements As New Dictionary(Of UIElement, Short)()
    Private ReadOnly rightElements As New Dictionary(Of UIElement, Short)()
    Private ReadOnly upElements As New Dictionary(Of UIElement, Short)()
    Private ReadOnly downElements As New Dictionary(Of UIElement, Short)()

    Private startMousePoint As PointAPI = Nothing
    Private startWindowSize As Size = Nothing
    Private startWindowLeftUpPoint As POINT = Nothing

    Private Shared workAreaMaxHeight As Integer = -1

    Private hs As HwndSource

    Public Sub New(target As Window)
        Me.target = target
        If IsNothing(target) Then Throw New Exception("Invalid Window handle")
        AddHandler target.SourceInitialized, AddressOf Me.MyMacClass_SourceInitialized
    End Sub

    Private Sub MyMacClass_SourceInitialized(sender As Object, e As EventArgs)
        Me.hs = (TryCast(PresentationSource.FromVisual(CType(sender, Visual)), HwndSource))
        Me.hs.AddHook(AddressOf Me.WndProc)
    End Sub

    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = 36 Then
            WmGetMinMaxInfo(hwnd, lParam)
            handled = True
        End If
        Return CType(0, IntPtr)
    End Function

    Private Shared Sub WmGetMinMaxInfo(hwnd As IntPtr, lParam As IntPtr)
        Dim mINMAXINFO As MINMAXINFO = CType(Marshal.PtrToStructure(lParam, GetType(MINMAXINFO)), MINMAXINFO)
        Dim flags As Integer = 2
        Dim intPtr As IntPtr = MonitorFromWindow(hwnd, flags)
        Dim flag As Boolean = intPtr <> IntPtr.Zero
        If flag Then
            Dim mONITORINFO As New MONITORINFO()
            GetMonitorInfo(intPtr, mONITORINFO)
            Dim rcWork As RECT = mONITORINFO.rcWork
            Dim rcMonitor As RECT = mONITORINFO.rcMonitor
            mINMAXINFO.ptMaxPosition.x = Math.Abs(rcWork.left - rcMonitor.left)
            mINMAXINFO.ptMaxPosition.y = Math.Abs(rcWork.top - rcMonitor.top)
            mINMAXINFO.ptMaxSize.y = Math.Abs(rcWork.bottom - rcWork.top)
            workAreaMaxHeight = mINMAXINFO.ptMaxSize.y
            If rcWork.Height = rcMonitor.Height Then
                mINMAXINFO.ptMaxSize.y -= 2
            End If
        End If
        Marshal.StructureToPtr(mINMAXINFO, lParam, True)
    End Sub

    Private Declare Function GetMonitorInfo Lib "user32" (hMonitor As IntPtr, lpmi As MONITORINFO) As Boolean

    Private Declare Function MonitorFromWindow Lib "User32" (handle As IntPtr, flags As Integer) As IntPtr

    Private Sub connectMouseHandlers(element As UIElement)
        AddHandler element.MouseLeftButtonDown, AddressOf Me.element_MouseLeftButtonDown
    End Sub

    Public Sub addResizerRight(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.rightElements.Add(element, 0)
    End Sub

    Public Sub addResizerLeft(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.leftElements.Add(element, 0)
    End Sub

    Public Sub addResizerUp(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.upElements.Add(element, 0)
    End Sub

    Public Sub addResizerDown(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.downElements.Add(element, 0)
    End Sub

    Public Sub addResizerRightDown(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.rightElements.Add(element, 0)
        Me.downElements.Add(element, 0)
    End Sub

    Public Sub addResizerLeftDown(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.leftElements.Add(element, 0)
        Me.downElements.Add(element, 0)
    End Sub

    Public Sub addResizerRightUp(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.rightElements.Add(element, 0)
        Me.upElements.Add(element, 0)
    End Sub

    Public Sub addResizerLeftUp(element As UIElement)
        Me.connectMouseHandlers(element)
        Me.leftElements.Add(element, 0)
        Me.upElements.Add(element, 0)
    End Sub

    Private Sub element_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        GetCursorPos(startMousePoint)
        startMousePoint.X = GetWPFSize(startMousePoint.X)
        startMousePoint.Y = GetWPFSize(startMousePoint.Y)
        startWindowSize = New Size(target.Width, target.Height)
        startWindowLeftUpPoint = New POINT(target.Left, target.Top)
        Dim key As UIElement = CType(sender, UIElement)
        If leftElements.ContainsKey(key) Then resizeLeft = True
        If rightElements.ContainsKey(key) Then resizeRight = True
        If upElements.ContainsKey(key) Then resizeUp = True
        If downElements.ContainsKey(key) Then resizeDown = True
        RunInNewThread(AddressOf updateSizeLoop, "窗口大小调整检测")
    End Sub

    Private Sub updateSizeLoop()
        Try
            While resizeDown OrElse resizeLeft OrElse resizeRight OrElse resizeUp
                target.Dispatcher.Invoke(AddressOf updateSize, DispatcherPriority.Render)
                target.Dispatcher.Invoke(AddressOf updateMouseDown, DispatcherPriority.Render)
                Thread.Sleep(0)
            End While
        Catch
        End Try
    End Sub

    Private Sub updateSize()
        Dim pointAPI As PointAPI = Nothing
        GetCursorPos(pointAPI)
        pointAPI.X = GetWPFSize(pointAPI.X)
        pointAPI.Y = GetWPFSize(pointAPI.Y)
        Try

            Dim NewWidth As Double = -1
            Dim NewHeight As Double = -1
            Dim NewLeft As Double = -10000
            Dim NewTop As Double = -10000

            If resizeRight Then
                If target.Width = target.MinWidth Then
                    If startMousePoint.X < pointAPI.X Then
                        NewWidth = startWindowSize.Width - (startMousePoint.X - pointAPI.X)
                    End If
                Else
                    If startWindowSize.Width - ((startMousePoint.X - pointAPI.X)) >= target.MinWidth Then
                        NewWidth = startWindowSize.Width - (startMousePoint.X - pointAPI.X)
                    Else
                        NewWidth = target.MinWidth
                    End If
                End If
            End If

            If resizeDown Then
                If target.Height = target.MinHeight Then
                    If startMousePoint.Y < pointAPI.Y Then
                        If workAreaMaxHeight > 0 Then
                            NewHeight = If((startWindowSize.Height - (startMousePoint.Y - pointAPI.Y) + target.Top <= workAreaMaxHeight), (startWindowSize.Height - ((startMousePoint.Y - pointAPI.Y))), ((workAreaMaxHeight) - target.Top))
                        Else
                            NewHeight = startWindowSize.Height - (startMousePoint.Y - pointAPI.Y)
                        End If
                    End If
                Else
                    If startWindowSize.Height - (startMousePoint.Y - pointAPI.Y) >= target.MinHeight Then
                        If workAreaMaxHeight > 0 Then
                            NewHeight = If((startWindowSize.Height - (startMousePoint.Y - pointAPI.Y) + target.Top <= (workAreaMaxHeight)), (startWindowSize.Height - (startMousePoint.Y - pointAPI.Y)), ((workAreaMaxHeight) - target.Top))
                        Else
                            NewHeight = startWindowSize.Height - (startMousePoint.Y - pointAPI.Y)
                        End If
                    Else
                        NewHeight = target.MinHeight
                    End If
                End If
            End If

            If resizeLeft Then
                If target.Width = target.MinWidth Then
                    If startMousePoint.X > pointAPI.X Then
                        NewWidth = startWindowSize.Width + startMousePoint.X - pointAPI.X
                        NewLeft = startWindowLeftUpPoint.x - (startMousePoint.X - pointAPI.X)
                    Else
                        NewWidth = target.MinWidth
                        NewLeft = startWindowLeftUpPoint.x + startWindowSize.Width - target.Width
                    End If
                Else
                    If startWindowSize.Width + (startMousePoint.X - pointAPI.X) >= target.MinWidth Then
                        NewWidth = startWindowSize.Width + (startMousePoint.X - pointAPI.X)
                        NewLeft = startWindowLeftUpPoint.x - (startMousePoint.X - pointAPI.X)
                    Else
                        NewWidth = target.MinWidth
                        NewLeft = startWindowLeftUpPoint.x + startWindowSize.Width - target.Width
                    End If
                End If
            End If

            If resizeUp Then
                If target.Height = target.MinHeight Then
                    If startMousePoint.Y > pointAPI.Y Then
                        NewHeight = startWindowSize.Height + startMousePoint.Y - pointAPI.Y
                        NewTop = startWindowLeftUpPoint.y - (startMousePoint.Y - pointAPI.Y)
                    Else
                        NewHeight = target.MinHeight
                        NewTop = startWindowLeftUpPoint.y + startWindowSize.Height - target.Height
                    End If
                Else
                    If startWindowSize.Height + (startMousePoint.Y - pointAPI.Y) >= target.MinHeight Then
                        NewHeight = startWindowSize.Height + startMousePoint.Y - pointAPI.Y
                        NewTop = startWindowLeftUpPoint.y - (startMousePoint.Y - pointAPI.Y)
                    Else
                        NewHeight = target.MinHeight
                        NewTop = startWindowLeftUpPoint.y + startWindowSize.Height - target.Height
                    End If
                End If
            End If

            If NewWidth > 10 AndAlso Math.Abs(NewWidth - target.Width) > 0.7 Then target.Width = NewWidth
            If NewHeight > 10 AndAlso Math.Abs(NewHeight - target.Height) > 0.7 Then target.Height = NewHeight
            If NewLeft > -9999 AndAlso Math.Abs(NewLeft - target.Left) > 0.7 Then target.Left = NewLeft
            If NewTop > -9999 AndAlso Math.Abs(NewTop - target.Top) > 0.7 Then target.Top = NewTop

        Catch
        End Try
    End Sub

    Private Sub updateMouseDown()
        Dim flag = (GetAsyncKeyState(&H1) And &H8000) = 0 '调用原生API判断鼠标是否抬起，如果使用WPF的API的话鼠标不在窗口上时不会更新状态 (#5655)
        If flag Then
            resizeRight = False
            resizeLeft = False
            resizeUp = False
            resizeDown = False
        End If
    End Sub

    Private Declare Function GetCursorPos Lib "user32.dll" (<Out()> ByRef lpPoint As PointAPI) As Boolean
    Private Declare Function GetAsyncKeyState Lib "user32.dll" (vKey As Integer) As Short
End Class
