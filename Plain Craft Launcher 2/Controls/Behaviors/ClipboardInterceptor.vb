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

Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Documents
Imports System.Linq

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
            Return CBool(element.GetValue(EnableSafeClipboardProperty))
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
            tb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Cut, AddressOf OnCutTextBox))
            tb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Paste, AddressOf OnPasteTextBox))
        End Sub

        Private Shared Sub AddCommandBindingsToRichTextBox(rtb As RichTextBox)
            rtb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Copy, AddressOf OnCopyRichTextBox))
            rtb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Cut, AddressOf OnCutRichTextBox))
            rtb.CommandBindings.Add(New CommandBinding(ApplicationCommands.Paste, AddressOf OnPasteRichTextBox))
        End Sub

        Private Shared Sub AddCommandBindingsToDataGrid(dg As DataGrid)
            dg.CommandBindings.Add(New CommandBinding(ApplicationCommands.Copy, AddressOf OnCopyDataGrid))
        End Sub

        Private Shared Sub OnCopyTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim tb = TryCast(sender, TextBox)
            If tb Is Nothing OrElse tb.SelectionLength <= 0 Then Return

            Try
                Forms.Clipboard.Clear()
                Forms.Clipboard.SetDataObject(tb.SelectedText, True)
            Catch
            End Try

            e.Handled = True
        End Sub

        Private Shared Sub OnCutTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim tb = TryCast(sender, TextBox)
            If tb Is Nothing OrElse tb.SelectionLength <= 0 Then Return

            Try
                Forms.Clipboard.Clear()
                Forms.Clipboard.SetDataObject(tb.SelectedText, True)
            Catch
            End Try

            tb.SelectedText = String.Empty
            e.Handled = True
        End Sub

        Private Shared Sub OnPasteTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim tb = TryCast(sender, TextBox)
            If tb Is Nothing Then Return

            If Forms.Clipboard.ContainsText() Then
                Dim pasteText = Forms.Clipboard.GetText()
                Dim start = tb.SelectionStart

                tb.SelectedText = pasteText
                tb.CaretIndex = start + pasteText.Length
                tb.SelectionLength = 0
            End If

            e.Handled = True
        End Sub

        Private Shared Sub OnCopyRichTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim rtb = TryCast(sender, RichTextBox)
            If rtb Is Nothing Then Return

            Dim textRange = New TextRange(rtb.Selection.Start, rtb.Selection.End)
            If String.IsNullOrEmpty(textRange.Text) Then Return

            Try
                Forms.Clipboard.Clear()
                Forms.Clipboard.SetDataObject(textRange.Text, True)
            Catch
            End Try

            e.Handled = True
        End Sub

        Private Shared Sub OnCutRichTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim rtb = TryCast(sender, RichTextBox)
            If rtb Is Nothing Then Return

            Dim selection = New TextRange(rtb.Selection.Start, rtb.Selection.End)
            If String.IsNullOrEmpty(selection.Text) Then Return

            Try
                Forms.Clipboard.Clear()
                Forms.Clipboard.SetDataObject(selection.Text, True)
            Catch
            End Try

            selection.Text = String.Empty
            e.Handled = True
        End Sub

        Private Shared Sub OnPasteRichTextBox(sender As Object, e As ExecutedRoutedEventArgs)
            Dim rtb = TryCast(sender, RichTextBox)
            If rtb Is Nothing Then Return

            If Not Forms.Clipboard.ContainsText() Then Return

            Dim pasteText = Forms.Clipboard.GetText()
            Dim selection = rtb.Selection

            selection.Text = pasteText

            Dim caretPos = selection.End
            rtb.CaretPosition = caretPos
            rtb.Selection.Select(caretPos, caretPos)

            e.Handled = True
        End Sub

        Private Shared Sub OnCopyDataGrid(sender As Object, e As ExecutedRoutedEventArgs)
            Dim dg = TryCast(sender, DataGrid)
            If dg Is Nothing OrElse dg.SelectedCells Is Nothing OrElse dg.SelectedCells.Count = 0 Then Return

            Dim sb = New System.Text.StringBuilder()
            Dim rowGroups = dg.SelectedCells.GroupBy(Function(c) c.Item)

            For Each row In rowGroups
                Dim rowText = String.Join(vbTab, row.Select(Function(cell)
                                                                Dim tb = TryCast(cell.Column.GetCellContent(cell.Item), TextBlock)
                                                                Return If(tb IsNot Nothing, tb.Text, "")
                                                            End Function))
                sb.AppendLine(rowText)
            Next

            Dim sbStr = sb.ToString().TrimEnd(ControlChars.Cr, ControlChars.Lf)

            Try
                Forms.Clipboard.Clear()
                Forms.Clipboard.SetDataObject(sbStr, True)
            Catch
            End Try

            e.Handled = True
        End Sub
    End Class
End Namespace
