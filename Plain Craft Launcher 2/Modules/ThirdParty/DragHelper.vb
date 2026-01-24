'来源：https://blog.csdn.net/simpleman2000/article/details/140952294

Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Windows.Interop

Public Class DragHelper

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
        RemoveDragHook()
        HwndIntPtrSource.AddHook(AddressOf WndProc)
        Dim handle As IntPtr = HwndIntPtrSource.Handle
        If IsUserAnAdmin() Then RevokeDragDrop(handle)
        DragAcceptFiles(handle, True)
        ChangeMessageFilter(handle)
    End Sub

    Public Sub RemoveDragHook()
        HwndIntPtrSource.RemoveHook(AddressOf WndProc)
        DragAcceptFiles(HwndIntPtrSource.Handle, False)
    End Sub

    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        Dim filePaths As String() = Nothing
        Dim point As New POINT()

        If TryGetDropInfo(msg, wParam, filePaths, point) Then
            DropPoint = point
            DropFilePaths = filePaths
            RaiseEvent DragDrop(Me, EventArgs.Empty)
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ChangeWindowMessageFilterEx(hWnd As IntPtr, msg As UInteger, action As UInteger, ByRef pChangeFilterStruct As CHANGEFILTERSTRUCT) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ChangeWindowMessageFilter(msg As UInteger, flags As UInteger) As Boolean
    End Function

    <DllImport("shell32.dll")>
    Private Shared Sub DragAcceptFiles(hWnd As IntPtr, fAccept As Boolean)
    End Sub

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function DragQueryFile(hWnd As IntPtr, iFile As UInteger, lpszFile As StringBuilder, cch As Integer) As UInteger
    End Function

    <DllImport("shell32.dll")>
    Private Shared Function DragQueryPoint(hDrop As IntPtr, ByRef lppt As POINT) As Boolean
    End Function

    <DllImport("shell32.dll")>
    Private Shared Sub DragFinish(hDrop As IntPtr)
    End Sub

    <DllImport("ole32.dll")>
    Private Shared Function RevokeDragDrop(hWnd As IntPtr) As Integer
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

    Private Shared Sub ChangeMessageFilter(handle As IntPtr)
        Dim ver As Version = Environment.OSVersion.Version
        Dim isVistaOrHigher As Boolean = ver >= New Version(6, 0)
        Dim isNt61OrHiger As Boolean = ver >= New Version(6, 1)

        If isVistaOrHigher Then
            Dim status As New CHANGEFILTERSTRUCT With {.cbSize = 8}
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

    Private Shared Function TryGetDropInfo(msg As Integer, wParam As IntPtr, ByRef dropFilePaths As String(), ByRef dropPoint As POINT) As Boolean
        dropFilePaths = Nothing
        dropPoint = New POINT()

        If msg <> WM_DROPFILES Then Return False

        Dim fileCount As UInteger = DragQueryFile(wParam, UInteger.MaxValue, Nothing, 0)
        ReDim dropFilePaths(CInt(fileCount) - 1)

        For i As UInteger = 0 To CInt(fileCount) - 1
            Dim sb As New StringBuilder(MAX_PATH)
            Dim result As UInteger = DragQueryFile(wParam, i, sb, sb.Capacity)
            If result > 0 Then dropFilePaths(i) = sb.ToString()
        Next

        DragQueryPoint(wParam, dropPoint)
        DragFinish(wParam)
        Return True
    End Function

End Class
