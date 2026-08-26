Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Partial Public Class PageSelectLeft

#Region "拖拽"

    ''' <summary>
    ''' 拖拽"文件夹列表"中的文件夹项时附带的数据。
    ''' </summary>
    Public Class FolderDragData
        Public SourceLocation As String
    End Class

    ''' <summary>
    ''' 为"文件夹列表"中的 <see cref="MyListItem"/> 启用拖拽排序（仅在文件夹项之间有效）。
    ''' </summary>
    Public Sub EnableFolderItemDrag(Item As MyListItem)
        Try
            '仅当 Tag 是 McFolder 时启用拖拽，避免按钮等其他条目被误触发
            Dim Folder As McFolder = TryCast(Item.Tag, McFolder)
            If Folder Is Nothing Then Return
            Item.AllowDrop = True
            Dim StartPoint As Point = New Point(0, 0)
            Dim DragStarted As Boolean = False
            Dim SrcLocation As String = Folder.Location
            AddHandler Item.PreviewMouseLeftButtonDown, Sub(s, e)
                StartPoint = e.GetPosition(Nothing)
                Dim Src As McFolder = TryCast(Item.Tag, McFolder)
                SrcLocation = If(Src Is Nothing, "", Src.Location)
                DragStarted = False
            End Sub
            AddHandler Item.PreviewMouseMove, Sub(s, e)
                Dim SrcItem As MyListItem = CType(s, MyListItem)
                If DragStarted OrElse e.LeftButton <> MouseButtonState.Pressed Then Return
                Dim Cur = e.GetPosition(Nothing)
                If Math.Abs(Cur.X - StartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance OrElse
                   Math.Abs(Cur.Y - StartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance Then
                    If String.IsNullOrEmpty(SrcLocation) Then Return
                    DragStarted = True
                    Try
                        Dim Payload As New FolderDragData With {.SourceLocation = SrcLocation}
                        Dim DataObj As New DataObject(GetType(FolderDragData), Payload)
                        DragDrop.DoDragDrop(SrcItem, DataObj, DragDropEffects.Move)
                    Finally
                        DragStarted = False
                    End Try
                End If
            End Sub
            AddHandler Item.PreviewDragOver, Sub(s, e)
                Dim Data = TryCast(e.Data.GetData(GetType(FolderDragData)), FolderDragData)
                Dim TargetFolder As McFolder = TryCast(Item.Tag, McFolder)
                If Data IsNot Nothing AndAlso TargetFolder IsNot Nothing AndAlso Data.SourceLocation <> TargetFolder.Location Then
                    e.Effects = DragDropEffects.Move
                Else
                    e.Effects = DragDropEffects.None
                End If
                e.Handled = True
            End Sub
            AddHandler Item.PreviewDrop, Sub(s, e)
                Dim Data = TryCast(e.Data.GetData(GetType(FolderDragData)), FolderDragData)
                If Data Is Nothing Then Return
                Dim TargetFolder As McFolder = TryCast(Item.Tag, McFolder)
                If TargetFolder Is Nothing Then Return
                If Data.SourceLocation = TargetFolder.Location Then Return
                Try
                    ReorderFoldersInSetup(Data.SourceLocation, TargetFolder.Location)
                    Hint("文件夹顺序已调整", HintType.Green)
                    McFolderListLoader.Start(IsForceRestart:=True)
                Catch ex As Exception
                    Logger.Error(ex, "拖拽调整文件夹顺序失败")
                End Try
                e.Handled = True
            End Sub
        Catch ex As Exception
            Logger.Warn(ex, "为文件夹项启用拖拽失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把 Settings("LaunchFolders") 中 <paramref name="SourceLocation"/> 对应的条目
    ''' 调整到 <paramref name="TargetLocation"/> 对应条目的位置（保留相同 DisplayName）。
    ''' </summary>
    Private Shared Sub ReorderFoldersInSetup(SourceLocation As String, TargetLocation As String)
        Dim RawEntries = Settings.Get(Of String)("LaunchFolders").Split("|"c, StringSplitOptions.RemoveEmptyEntries).ToList()
        Dim SrcIdx = -1
        Dim TgtIdx = -1
        For i = 0 To RawEntries.Count - 1
            Dim Parts = RawEntries(i).Split(">"c, 2)
            If Parts.Length < 2 Then Continue For
            Dim Loc = Parts(1).TrimEnd("\"c) & "\"
            If String.Equals(Loc, SourceLocation.TrimEnd("\"c) & "\", StringComparison.OrdinalIgnoreCase) Then
                SrcIdx = i
            ElseIf String.Equals(Loc, TargetLocation.TrimEnd("\"c) & "\", StringComparison.OrdinalIgnoreCase) Then
                TgtIdx = i
            End If
            If SrcIdx >= 0 AndAlso TgtIdx >= 0 Then Exit For
        Next
        If SrcIdx < 0 OrElse TgtIdx < 0 OrElse SrcIdx = TgtIdx Then Return
        Dim Entry = RawEntries(SrcIdx)
        RawEntries.RemoveAt(SrcIdx)
        Dim InsertAt As Integer = Math.Min(TgtIdx, RawEntries.Count)
        RawEntries.Insert(InsertAt, Entry)
        Settings.Set("LaunchFolders", RawEntries.Join("|"))
    End Sub

#End Region

End Class
