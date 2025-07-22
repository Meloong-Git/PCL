Imports System
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Interop


Public Class DragFileHelper

    Public Event DragDrop As EventHandler

    Public Property DropFilePaths As String()
        Get
            Return _DropFilePathsBackingField
        End Get
        Private Set(value As String())
            _DropFilePathsBackingField = value
        End Set
    End Property
    Private _DropFilePathsBackingField As String()

    Public Property DropPoint As POINT
        Get
            Return _DropPointBackingField
        End Get
        Private Set(value As POINT)
            _DropPointBackingField = value
        End Set
    End Property
    Private _DropPointBackingField As POINT

    Public Property HwndIntPtrSource As HwndSource

    Public Sub AddHook()
        Me.RemoveDragHook()
        Me.HwndIntPtrSource.AddHook(AddressOf WndProc)
        Dim handle As IntPtr = Me.HwndIntPtrSource.Handle
        If IsUserAnAdmin() Then RevokeDragDrop(handle)
        DragAcceptFiles(handle, True)
        ChangeMessageFilter(handle)
    End Sub

    Public Sub RemoveDragHook()
        Me.HwndIntPtrSource.RemoveHook(AddressOf WndProc)
        DragAcceptFiles(Me.HwndIntPtrSource.Handle, False)
    End Sub

    Private Function WndProc(ByVal hwnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        Dim filePaths As String() = Nothing
        Dim point As POINT = New POINT()

        If TryGetDropInfo(msg, wParam, filePaths, point) Then
            DropPoint = point
            DropFilePaths = filePaths
            RaiseEvent DragDrop(Me, EventArgs.Empty)
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ChangeWindowMessageFilterEx(ByVal hWnd As IntPtr, ByVal msg As UInteger, ByVal action As UInteger, ByRef pChangeFilterStruct As CHANGEFILTERSTRUCT) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ChangeWindowMessageFilter(ByVal msg As UInteger, ByVal flags As UInteger) As Boolean
    End Function

    <DllImport("shell32.dll")>
    Private Shared Sub DragAcceptFiles(ByVal hWnd As IntPtr, ByVal fAccept As Boolean)
    End Sub

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function DragQueryFile(ByVal hWnd As IntPtr, ByVal iFile As UInteger, ByVal lpszFile As StringBuilder, ByVal cch As Integer) As UInteger
    End Function

    <DllImport("shell32.dll")>
    Private Shared Function DragQueryPoint(ByVal hDrop As IntPtr, ByRef lppt As POINT) As Boolean
    End Function

    <DllImport("shell32.dll")>
    Private Shared Sub DragFinish(ByVal hDrop As IntPtr)
    End Sub

    <DllImport("ole32.dll")>
    Private Shared Function RevokeDragDrop(ByVal hWnd As IntPtr) As Integer
    End Function

    <DllImport("shell32.dll")>
    Private Shared Function IsUserAnAdmin() As Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Public Structure POINT
        Public X As Integer
        Public Y As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure CHANGEFILTERSTRUCT
        Public cbSize As UInteger
        Public ExtStatus As UInteger
    End Structure

    Private Const WM_COPYGLOBALDATA As UInteger = &H49
    Private Const WM_COPYDATA As UInteger = &H4A
    Private Const WM_DROPFILES As UInteger = &H233
    Private Const MSGFLT_ALLOW As UInteger = 1
    Private Const MSGFLT_ADD As UInteger = 1
    Private Const MAX_PATH As Integer = 260

    Private Shared Sub ChangeMessageFilter(ByVal handle As IntPtr)
        Dim ver As Version = Environment.OSVersion.Version
        Dim isVistaOrHigher As Boolean = ver >= New Version(6, 0)
        Dim isNt61OrHiger As Boolean = ver >= New Version(6, 1)

        If isVistaOrHigher Then
            Dim status As CHANGEFILTERSTRUCT = New CHANGEFILTERSTRUCT With {.cbSize = 8}
            For Each msg As UInteger In New UInteger() {WM_DROPFILES, WM_COPYGLOBALDATA, WM_COPYDATA}
                Dim [error] As Boolean = False
                If isNt61OrHiger Then
                    [error] = Not ChangeWindowMessageFilterEx(handle, msg, MSGFLT_ALLOW, status)
                Else
                    [error] = Not ChangeWindowMessageFilter(msg, MSGFLT_ADD)
                End If

                If [error] Then Throw New Win32Exception(Marshal.GetLastWin32Error())
            Next
        End If
    End Sub

    Private Shared Function TryGetDropInfo(ByVal msg As Integer, ByVal wParam As IntPtr, ByRef dropFilePaths As String(), ByRef dropPoint As POINT) As Boolean
        dropFilePaths = Nothing
        dropPoint = New POINT()

        If msg <> WM_DROPFILES Then Return False

        Dim fileCount As UInteger = DragQueryFile(wParam, UInteger.MaxValue, Nothing, 0)
        ReDim dropFilePaths(CInt(fileCount) - 1)

        For i As UInteger = 0 To fileCount - 1
            Dim sb As New StringBuilder(MAX_PATH)
            Dim result As UInteger = DragQueryFile(wParam, i, sb, sb.Capacity)
            If result > 0 Then dropFilePaths(CInt(i)) = sb.ToString()
        Next

        DragQueryPoint(wParam, dropPoint)
        DragFinish(wParam)
        Return True
    End Function

End Class