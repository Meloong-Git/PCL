' Author: uye (owner of the MaaAssistantArknights team)
' Original Source: MaaAssistantArknights project - https://github.com/MaaAssistantArknights/MaaAssistantArknights
' License: Apache License 2.0 (this file only)
'
' This file is originally developed for MaaAssistantArknights and licensed under AGPL v3.0.
' As the original author and copyright holder,
' I hereby re-license this file under the Apache License 2.0 for use in the Plain Craft Launcher project.
'
' This file is contributed under the spirit of good faith and open cooperation.
' It does not implement any core launcher functions of PCL.
'
' Description:
' Provides a WPF clipboard handling fix to avoid OpenClipboard exceptions
' in TextBox, RichTextBox, and DataGrid under Windows focus issues or clipboard hooks.
'
' Date: 2025-07-03

Namespace Controls.Behaviors
    Public NotInheritable Class ClipboardInterceptor
        Private Sub New()
        End Sub

        Public Shared ReadOnly EnableSafeClipboardProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EnableSafeClipboard", GetType(Boolean), GetType(ClipboardInterceptor),
                                                New PropertyMetadata(False, AddressOf OnEnableSafeClipboardChanged))

        Public Shared Sub SetEnableSafeClipboard(element As DependencyObject, value As Boolean)
            element.SetValue(EnableSafeClipboardProperty, value)
        End Sub

        Public Shared Function GetEnableSafeClipboard(element As DependencyObject) As Boolean
            Return element.GetValue(EnableSafeClipboardProperty)
        End Function

        Private Shared Sub OnEnableSafeClipboardChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
            If TypeOf d Is TextBox AndAlso CBool(e.NewValue) Then
                AddCommandBindingsToTextBox(DirectCast(d, TextBox))
            ElseIf TypeOf d Is RichTextBox AndAlso CBool(e.NewValue) Then
                AddCommandBindingsToRichTextBox(DirectCast(d, RichTextBox))
            ElseIf TypeOf d Is DataGrid AndAlso CBool(e.NewValue) Then
                AddCommandBindingsToDataGrid(DirectCast(d, DataGrid))
            End If
        End Sub

        Private Shared Sub AddCommandBindingsToTextBox(tb As TextBox)
            tb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Copy, AddressOf OnCopyTextBox))
            tb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Cut, AddressOf OnCutTextBox, AddressOf CanEditTextBox))
            tb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Paste, AddressOf OnPasteTextBox, AddressOf CanEditTextBox))
        End Sub

        Private Shared Sub AddCommandBindingsToRichTextBox(rtb As RichTextBox)
            rtb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Copy, AddressOf OnCopyRichTextBox))
            rtb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Cut, AddressOf OnCutRichTextBox))
            rtb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Paste, AddressOf OnPasteRichTextBox))
        End Sub

        Private Shared Sub AddCommandBindingsToDataGrid(dg As DataGrid)
            dg.CommandBindings.Add(New CommandBinding(ApplicationCommands.Copy, AddressOf OnCopyDataGrid))
        End Sub

        Private Shared Sub SetClipboardText(text As String)
            Forms.Clipboard.SetDataObject(text, False, retryTimes:=10, retryDelay:=100) '实际的复制
            Try
                System.Windows.Clipboard.Flush() '将数据持久化到系统剪贴板，避免应用程序退出后数据丢失
            Catch ex As Exception '持久化失败不抛出异常
                Logger.Warn(ex, $"剪贴板持久化失败（错误码：0x{ex.HResult:X8}）")
            End Try
        End Sub

        Private Shared Sub OnCopyTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim tb = TryCast(sender, TextBox)
            If tb Is Nothing OrElse tb.SelectionLength <= 0 Then Return

            Try
                SetClipboardText(tb.SelectedText)
            Catch ex As Exception
                Logger.Warn(ex, $"复制失败，请稍后再试（错误码：0x{ex.HResult:X8}）", LogBehavior.Toast)
            End Try

            e.Handled = True
        End Sub

        Private Shared Sub CanEditTextBox(sender As Object, e As CanExecuteRoutedEventArgs)
            Dim tb = TryCast(sender, TextBox)
            e.CanExecute = tb IsNot Nothing AndAlso Not tb.IsReadOnly AndAlso
                (e.Command IsNot ApplicationCommands.Cut OrElse tb.SelectionLength > 0)
            e.Handled = True
        End Sub

        Private Shared Sub OnCutTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim tb = TryCast(sender, TextBox)
            e.Handled = True
            If tb Is Nothing OrElse tb.IsReadOnly OrElse tb.SelectionLength <= 0 Then Return

            Try
                SetClipboardText(tb.SelectedText)
            Catch ex As Exception
                Logger.Warn(ex, $"剪切失败，请稍后再试（错误码：0x{ex.HResult:X8}）", LogBehavior.Toast)
                Return
            End Try

            tb.SelectedText = String.Empty
        End Sub

        Private Shared Sub OnPasteTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim tb = TryCast(sender, TextBox)
            e.Handled = True
            If tb Is Nothing OrElse tb.IsReadOnly Then Return

            Try
                Dim pasteText = Retrier.Attempt(func:=
                Function(Attempt) As String
                    Return If(Forms.Clipboard.ContainsText(), Forms.Clipboard.GetText(), Nothing)
                End Function, maxAttempts:=10, delay:=Function() TimeSpan.FromMilliseconds(100))
                If pasteText IsNot Nothing Then

                    '将换行符替换为空格（#7825）
                    If Not tb.AcceptsReturn Then pasteText = pasteText.ReplaceLineEndings(" ", mergeMultiple:=True)

                    Dim start = tb.SelectionStart

                    tb.SelectedText = pasteText
                    tb.CaretIndex = start + pasteText.Length
                    tb.SelectionLength = 0
                End If
            Catch ex As Exception
                Logger.Warn(ex, $"粘贴失败，请稍后再试（错误码：0x{ex.HResult:X8}）", LogBehavior.Toast)
            End Try
        End Sub

        Private Shared Sub OnCopyRichTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim rtb = TryCast(sender, RichTextBox)
            If rtb Is Nothing Then Return

            Dim textRange = New TextRange(rtb.Selection.Start, rtb.Selection.End)
            If String.IsNullOrEmpty(textRange.Text) Then Return

            Try
                SetClipboardText(textRange.Text)
            Catch ex As Exception
                Logger.Warn(ex, $"复制失败，请稍后再试（错误码：0x{ex.HResult:X8}）", LogBehavior.Toast)
            End Try

            e.Handled = True
        End Sub

        Private Shared Sub OnCutRichTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim rtb = TryCast(sender, RichTextBox)
            If rtb Is Nothing Then Return

            Dim selection = New TextRange(rtb.Selection.Start, rtb.Selection.End)
            If String.IsNullOrEmpty(selection.Text) Then Return
            e.Handled = True

            Try
                SetClipboardText(selection.Text)
            Catch ex As Exception
                Logger.Warn(ex, $"剪切失败，请稍后再试（错误码：0x{ex.HResult:X8}）", LogBehavior.Toast)
                Return
            End Try

            selection.Text = String.Empty
        End Sub

        Private Shared Sub OnPasteRichTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim rtb = TryCast(sender, RichTextBox)
            If rtb Is Nothing Then Return

            Try
                Dim pasteText = Retrier.Attempt(func:=
                Function(Attempt) As String
                    Return If(Forms.Clipboard.ContainsText(), Forms.Clipboard.GetText(), Nothing)
                End Function, maxAttempts:=2, delay:=Function() TimeSpan.FromMilliseconds(200))
                If pasteText Is Nothing Then Return
                Dim selection = rtb.Selection

                selection.Text = pasteText

                Dim caretPos = selection.End
                rtb.CaretPosition = caretPos
                rtb.Selection.Select(caretPos, caretPos)
            Catch ex As Exception
                Logger.Warn(ex, $"粘贴失败，请稍后再试（错误码：0x{ex.HResult:X8}）", LogBehavior.Toast)
            End Try

            e.Handled = True
        End Sub

        Private Shared Sub OnCopyDataGrid(sender As Object, e As ExecutedRoutedEventArgs)
            Dim dg = TryCast(sender, DataGrid)
            If dg Is Nothing OrElse dg.SelectedCells Is Nothing OrElse dg.SelectedCells.Count = 0 Then Return

            Dim sb = New StringBuilder
            Dim rowGroups = dg.SelectedCells.GroupBy(Function(c) c.Item)

            For Each row In rowGroups
                Dim rowText = String.Join(vbTab, row.Select(
                Function(cell)
                    Dim tb = TryCast(cell.Column.GetCellContent(cell.Item), TextBlock)
                    Return If(tb IsNot Nothing, tb.Text, "")
                End Function))
                sb.AppendLine(rowText)
            Next

            Dim sbStr = sb.ToString().TrimEnd(ControlChars.Cr, ControlChars.Lf)

            Try
                SetClipboardText(sbStr)
            Catch ex As Exception
                Logger.Warn(ex, $"复制失败，请稍后再试（错误码：0x{ex.HResult:X8}）", LogBehavior.Toast)
            End Try

            e.Handled = True
        End Sub
    End Class
End Namespace
