Public Class PageOtherHelpDetail
    Implements IRefreshable
    Public Entry As HelpEntry

    Public Sub Refresh() Implements IRefreshable.Refresh
        Init(New HelpEntry(Entry.RawPath))
    End Sub

    Private Sub PageOtherHelpDetail_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToTop()
    End Sub

    ''' <summary>
    ''' 根据特定帮助项初始化页面 UI，返回是否成功加载。
    ''' </summary>
    Public Function Init(Entry As HelpEntry) As Boolean
        Dim Content = If(Entry.XamlContent, "")
        If Content = "" Then Throw New Exception("帮助 xaml 文件为空")
        Try
            '修改时应同时修改 PageLaunchRight.LoadContent
            Content = ArgumentReplace(Content, AddressOf EscapeXML)
            Do While Content.Contains("xmlns")
                Content = Content.RegexReplace("xmlns[^""']*(""|')[^""']*(""|')", "").Replace("xmlns", "") '禁止声明命名空间
            Loop
            Content = "<StackPanel xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:sys=""clr-namespace:System;assembly=mscorlib"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2"">" & Content & "</StackPanel>"
            Me.Entry = Entry
            PanCustom.Children.Clear()
            PanCustom.Children.Add(GetObjectFromXML(Content))
            Return True
        Catch ex As Exception
            Log("[System] 自定义信息内容：" & vbCrLf & Content)
            Log(ex, "加载帮助 XAML 文件失败", LogLevel.Msgbox)
            Return False
        End Try
    End Function

End Class
