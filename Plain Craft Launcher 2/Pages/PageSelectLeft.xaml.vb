Imports PCL.Core.IO

Public Class PageSelectLeft
    Implements IRefreshable

    Private Sub PageSelectLeft_Initialized(sender As Object, e As EventArgs) Handles Me.Initialized
        AddHandler McFolderListLoader.PreviewFinish, Sub() If FrmSelectLeft IsNot Nothing Then RunInUiWait(AddressOf McFolderListUI)
    End Sub
    Private IsFirstLoad As Boolean = True
    Private Sub PageSelectLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsFirstLoad Then McFolderListUI() '若已经执行完成，触发首次加载
        IsFirstLoad = False
    End Sub
    Private Sub McFolderListUI()
        Try

            '确认数据有变化
            If McFolderListLast IsNot Nothing AndAlso McFolderListLast.Equals(McFolderList) Then
                Dim IsEqual As Boolean = True
                For i = 0 To McFolderListLast.Count - 1
                    If Not McFolderListLast(i).Equals(McFolderList(i)) Then
                        IsEqual = False
                        Exit For
                    End If
                Next
                If IsEqual Then Return
            End If
            McFolderListLast = New List(Of McFolder)(McFolderList)

            '创建 UI
            FrmSelectLeft.PanList.Children.Clear()

            '文件夹列表
            FrmSelectLeft.PanList.Children.Add(New TextBlock With {.Text = "文件夹列表", .Margin = New Thickness(13, 18, 5, 4), .Opacity = 0.6, .FontSize = 12})
            For i = 0 To McFolderList.Count - 1
                Dim Folder As McFolder = McFolderList(i)
                Log(Folder.Name)
                Log(Folder.Type)
                '添加控件
                Dim ContMenu As ContextMenu = Nothing
                Select Case Folder.Type
                    Case McFolderType.Original
                        ContMenu = GetObjectFromXML(
                                <ContextMenu xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:PCL;assembly=Plain Craft Launcher 2">
                                    <local:MyMenuItem x:Name="Rename" Header="重命名" Padding="0,2,0,0" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="MoveUp" Header="上移" Icon="M104.704 685.248a64 64 0 0 0 90.496 0L512 368.448l316.8 316.8a64 64 0 0 0 90.496-90.496L557.248 232.704a64 64 0 0 0-90.496 0L104.704 594.752a64 64 0 0 0 0 90.496z"/>
                                    <local:MyMenuItem x:Name="MoveDown" Header="下移" Icon="M104.704 338.752a64 64 0 0 1 90.496 0L512 655.552l316.8-316.8a64 64 0 0 1 90.496 90.496l-362.048 362.048a64 64 0 0 1-90.496 0L104.704 429.248a64 64 0 0 1 0-90.496z"/>
                                    <local:MyMenuItem x:Name="Open" Header="打开" Icon="F1 M 19,50L 28,34L 63,34L 54,50L 19,50 Z M 19,28.0001L 35,28C 36,25 37.4999,24.0001 37.4999,24.0001L 48.75,24C 49.3023,24 50,24.6977 50,25.25L 50,28L 54,28.0001L 54,32L 27,32L 19,46.4L 19,28.0001 Z"/>
                                    <local:MyMenuItem x:Name="Refresh" Header="刷新" Icon="F1 M 38,20.5833C 42.9908,20.5833 47.4912,22.6825 50.6667,26.046L 50.6667,17.4167L 55.4166,22.1667L 55.4167,34.8333L 42.75,34.8333L 38,30.0833L 46.8512,30.0833C 44.6768,27.6539 41.517,26.125 38,26.125C 31.9785,26.125 27.0037,30.6068 26.2296,36.4167L 20.6543,36.4167C 21.4543,27.5397 28.9148,20.5833 38,20.5833 Z M 38,49.875C 44.0215,49.875 48.9963,45.3932 49.7703,39.5833L 55.3457,39.5833C 54.5457,48.4603 47.0852,55.4167 38,55.4167C 33.0092,55.4167 28.5088,53.3175 25.3333,49.954L 25.3333,58.5833L 20.5833,53.8333L 20.5833,41.1667L 33.25,41.1667L 38,45.9167L 29.1487,45.9167C 31.3231,48.3461 34.483,49.875 38,49.875 Z"/>
                                    <local:MyMenuItem x:Name="Delete" Header="删除" Padding="0,0,0,2" Icon="F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z "/>
                                </ContextMenu>
                        )
                    Case McFolderType.RenamedOriginal
                        ContMenu = GetObjectFromXML(
                                <ContextMenu xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:PCL;assembly=Plain Craft Launcher 2">
                                    <local:MyMenuItem x:Name="Restore" Header="复原名称" Padding="0,2,0,0" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="Rename" Header="重命名" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="MoveUp" Header="上移" Icon="M104.704 685.248a64 64 0 0 0 90.496 0L512 368.448l316.8 316.8a64 64 0 0 0 90.496-90.496L557.248 232.704a64 64 0 0 0-90.496 0L104.704 594.752a64 64 0 0 0 0 90.496z"/>
                                    <local:MyMenuItem x:Name="MoveDown" Header="下移" Icon="M104.704 338.752a64 64 0 0 1 90.496 0L512 655.552l316.8-316.8a64 64 0 0 1 90.496 90.496l-362.048 362.048a64 64 0 0 1-90.496 0L104.704 429.248a64 64 0 0 1 0-90.496z"/>
                                    <local:MyMenuItem x:Name="Open" Header="打开" Icon="F1 M 19,50L 28,34L 63,34L 54,50L 19,50 Z M 19,28.0001L 35,28C 36,25 37.4999,24.0001 37.4999,24.0001L 48.75,24C 49.3023,24 50,24.6977 50,25.25L 50,28L 54,28.0001L 54,32L 27,32L 19,46.4L 19,28.0001 Z"/>
                                    <local:MyMenuItem x:Name="Refresh" Header="刷新" Icon="F1 M 38,20.5833C 42.9908,20.5833 47.4912,22.6825 50.6667,26.046L 50.6667,17.4167L 55.4166,22.1667L 55.4167,34.8333L 42.75,34.8333L 38,30.0833L 46.8512,30.0833C 44.6768,27.6539 41.517,26.125 38,26.125C 31.9785,26.125 27.0037,30.6068 26.2296,36.4167L 20.6543,36.4167C 21.4543,27.5397 28.9148,20.5833 38,20.5833 Z M 38,49.875C 44.0215,49.875 48.9963,45.3932 49.7703,39.5833L 55.3457,39.5833C 54.5457,48.4603 47.0852,55.4167 38,55.4167C 33.0092,55.4167 28.5088,53.3175 25.3333,49.954L 25.3333,58.5833L 20.5833,53.8333L 20.5833,41.1667L 33.25,41.1667L 38,45.9167L 29.1487,45.9167C 31.3231,48.3461 34.483,49.875 38,49.875 Z"/>
                                    <local:MyMenuItem x:Name="Delete" Header="删除" Padding="0,0,0,2" Icon="F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z "/>
                                </ContextMenu>
                        )
                    Case McFolderType.Custom
                        ContMenu = GetObjectFromXML(
                                <ContextMenu xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:PCL;assembly=Plain Craft Launcher 2">
                                    <local:MyMenuItem x:Name="Rename" Header="重命名" Padding="0,2,0,0" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="MoveUp" Header="上移" Icon="M104.704 685.248a64 64 0 0 0 90.496 0L512 368.448l316.8 316.8a64 64 0 0 0 90.496-90.496L557.248 232.704a64 64 0 0 0-90.496 0L104.704 594.752a64 64 0 0 0 0 90.496z"/>
                                    <local:MyMenuItem x:Name="MoveDown" Header="下移" Icon="M104.704 338.752a64 64 0 0 1 90.496 0L512 655.552l316.8-316.8a64 64 0 0 1 90.496 90.496l-362.048 362.048a64 64 0 0 1-90.496 0L104.704 429.248a64 64 0 0 1 0-90.496z"/>
                                    <local:MyMenuItem x:Name="Open" Header="打开" Icon="F1 M 19,50L 28,34L 63,34L 54,50L 19,50 Z M 19,28.0001L 35,28C 36,25 37.4999,24.0001 37.4999,24.0001L 48.75,24C 49.3023,24 50,24.6977 50,25.25L 50,28L 54,28.0001L 54,32L 27,32L 19,46.4L 19,28.0001 Z"/>
                                    <local:MyMenuItem x:Name="Refresh" Header="刷新" Icon="F1 M 38,20.5833C 42.9908,20.5833 47.4912,22.6825 50.6667,26.046L 50.6667,17.4167L 55.4166,22.1667L 55.4167,34.8333L 42.75,34.8333L 38,30.0833L 46.8512,30.0833C 44.6768,27.6539 41.517,26.125 38,26.125C 31.9785,26.125 27.0037,30.6068 26.2296,36.4167L 20.6543,36.4167C 21.4543,27.5397 28.9148,20.5833 38,20.5833 Z M 38,49.875C 44.0215,49.875 48.9963,45.3932 49.7703,39.5833L 55.3457,39.5833C 54.5457,48.4603 47.0852,55.4167 38,55.4167C 33.0092,55.4167 28.5088,53.3175 25.3333,49.954L 25.3333,58.5833L 20.5833,53.8333L 20.5833,41.1667L 33.25,41.1667L 38,45.9167L 29.1487,45.9167C 31.3231,48.3461 34.483,49.875 38,49.875 Z"/>
                                    <local:MyMenuItem x:Name="Remove" Header="移出列表" Icon="F1 M 23.3428,25.205L 23.3805,25.4461C 23.9229,27.177 30.261,29.0992 38,29.0992C 45.7386,29.0992 52.0765,27.1771 52.6194,25.4463L 52.6571,25.205C 52.6571,23.3616 46.0949,21.3109 38,21.3109C 29.9051,21.3109 23.3428,23.3616 23.3428,25.205 Z M 23.3428,53.0204L 19.1571,26.2111C 19.0534,25.8817 19,25.5459 19,25.205C 19,20.9036 27.5066,17.4167 38,17.4167C 48.4934,17.4167 57,20.9036 57,25.205C 57,25.5459 56.9466,25.8818 56.8429,26.2112L 52.6571,53.0204L 52.5974,53.0204C 51.9241,56.1393 45.6457,58.5833 38,58.5833C 30.3543,58.5833 24.076,56.1393 23.4026,53.0204L 23.3428,53.0204 Z M 51.8228,30.5485C 48.3585,32.0537 43.4469,32.9933 38,32.9933C 32.5531,32.9933 27.6415,32.0537 24.1771,30.5484L 27.5988,52.464L 27.6857,52.464C 27.6857,53.3857 32.3036,54.6892 38,54.6892C 43.6964,54.6892 48.3143,53.3857 48.3143,52.464L 48.4011,52.464L 51.8228,30.5485 Z "/>
                                    <local:MyMenuItem x:Name="Delete" Header="删除" Padding="0,0,0,2" Icon="F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z "/>
                                </ContextMenu>
                        )
                End Select
                
                '根据位置控制上移和下移按钮的显示
                Dim moveUpItem = CType(ContMenu.FindName("MoveUp"), MyMenuItem)
                Dim moveDownItem = CType(ContMenu.FindName("MoveDown"), MyMenuItem)
                
                ' 如果是第一个项目，隐藏上移按钮
                If i = 0 Then
                    moveUpItem.Visibility = Visibility.Collapsed
                End If
                
                ' 如果是最后一个项目，隐藏下移按钮
                If i = McFolderList.Count - 1 Then
                    moveDownItem.Visibility = Visibility.Collapsed
                End If
                
                If (Folder.Type = McFolderType.Original OrElse Folder.Type = McFolderType.RenamedOriginal) AndAlso Folder.Path = Path & ".minecraft\" AndAlso McFolderList.Count = 1 Then CType(ContMenu.FindName("Delete"), MyMenuItem).Header = "清空"
                '注册事件
                If Folder.Type = McFolderType.Custom Then CType(ContMenu.FindName("Remove"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Remove_Click))
                If Folder.Type = McFolderType.RenamedOriginal Then CType(ContMenu.FindName("Restore"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Restore_Click))
                moveUpItem.AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.MoveUp_Click))
                moveDownItem.AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.MoveDown_Click))
                CType(ContMenu.FindName("Open"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Open_Click))
                CType(ContMenu.FindName("Delete"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Delete_Click))
                CType(ContMenu.FindName("Rename"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Rename_Click))
                CType(ContMenu.FindName("Refresh"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Refresh_Click))

                ' 构建框架与图表按钮
                Dim NewItem As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.RadioBox, .MinPaddingRight = 30, .Title = Folder.Name, .Info = Folder.Path, .Height = 40, .ContextMenu = ContMenu, .Tag = Folder}
                AddHandler NewItem.Changed, AddressOf FrmSelectLeft.Folder_Change
                
                ' 启用拖拽功能
                NewItem.AllowDrop = True
                AddHandler NewItem.MouseMove, AddressOf FrmSelectLeft.Item_MouseMove
                AddHandler NewItem.DragEnter, AddressOf FrmSelectLeft.Item_DragEnter
                AddHandler NewItem.DragOver, AddressOf FrmSelectLeft.Item_DragOver
                AddHandler NewItem.DragLeave, AddressOf FrmSelectLeft.Item_DragLeave
                AddHandler NewItem.Drop, AddressOf FrmSelectLeft.Item_Drop
                
                Dim NewIconButton As New MyIconButton With {.Logo = Logo.IconButtonSetup, .LogoScale = 1.1}
                AddHandler NewIconButton.Click, Sub(sender, e)
                                                    ContMenu.PlacementTarget = NewItem
                                                    ContMenu.IsOpen = True
                                                End Sub
                NewItem.Buttons = {NewIconButton}
                FrmSelectLeft.PanList.Children.Add(NewItem)
                Log("[Minecraft] 有效的 Minecraft 文件夹：" & Folder.Name & " > " & Folder.Path)
            Next

            '标题文本
            FrmSelectLeft.PanList.Children.Add(New TextBlock With {.Text = "添加或导入", .Margin = New Thickness(13, 18, 5, 4), .Opacity = 0.6, .FontSize = 12})

            '确认创建按钮状态
            If Not Directory.Exists(Path & ".minecraft\") Then
                Dim ItemCreate As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "新建 .minecraft 文件夹", .Height = 34,
                    .ToolTip = "在 PCL 当前所在文件夹下创建新的 .minecraft 文件夹",
                    .LogoScale = 0.9,
                    .Logo = Logo.IconButtonCreate}
                ToolTipService.SetPlacement(ItemCreate, Primitives.PlacementMode.Right)
                ToolTipService.SetHorizontalOffset(ItemCreate, -50)
                ToolTipService.SetVerticalOffset(ItemCreate, 2.5)
                FrmSelectLeft.PanList.Children.Add(ItemCreate)
                AddHandler ItemCreate.Click, AddressOf FrmSelectLeft.Create_Click
            End If

            '添加按钮
            Dim ItemAdd As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "添加已有文件夹", .Height = 34,
                .ToolTip = "将一个已有的 Minecraft 文件夹添加到列表",
                .Logo = Logo.IconButtonAdd}
            ToolTipService.SetPlacement(ItemAdd, Primitives.PlacementMode.Right)
            ToolTipService.SetHorizontalOffset(ItemAdd, -50)
            ToolTipService.SetVerticalOffset(ItemAdd, 2.5)
            FrmSelectLeft.PanList.Children.Add(ItemAdd)
            AddHandler ItemAdd.Click, AddressOf FrmSelectLeft.Add_Click

            '安装按钮
            Dim ItemInstall As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "导入整合包", .Height = 34,
                .ToolTip = "在当前选择的 Minecraft 文件夹下安装整合包",
                .Logo = "M512 40.96C249.344 40.96 35.84 252.416 35.84 512s213.504 471.04 476.16 471.04c103.424 0 202.752-33.28 286.72-96.256l1.536-1.536c5.12-5.632 7.68-12.8 7.68-19.968 0-16.896-13.824-30.208-30.72-30.208-7.68 0-15.36 2.56-20.992 7.68h-0.512c-71.68 52.224-155.648 79.36-243.712 79.36-227.328 0-412.16-182.784-412.16-407.552 0-224.768 184.832-407.552 412.16-407.552s412.16 182.784 412.16 407.552c0 68.608-15.872 132.608-46.592 190.464-0.512 1.024-1.024 2.048-1.024 3.072-0.512 2.048-1.536 4.608-1.536 8.192 0 16.896 13.824 30.208 30.72 30.208 12.288 0 23.04-7.168 28.16-18.432 35.84-68.608 53.76-141.312 53.76-216.064 0.512-259.584-212.992-471.04-475.648-471.04z M812.032 483.328c-31.744-20.992-71.68 1.536-78.848 6.144-1.024 0.512-104.448 61.44-128 74.752-8.192 4.608-22.528-0.512-27.136-4.096-31.232-36.352-54.272-70.656-68.608-102.4-13.312-29.184 0.512-41.472 3.072-43.52 7.168-4.608 114.688-68.608 143.36-83.456 24.064-12.288 40.96-25.088 46.08-45.056 3.072-13.312 0-27.136-9.216-39.936-22.016-31.744-172.544-84.992-311.296-3.584-157.184 91.648-152.064 242.688-150.528 292.352v9.216c0 18.944-12.8 37.376-14.848 40.448l-20.992 21.504c-6.144 6.144-9.216 13.824-9.216 22.528 0 8.704 3.584 16.384 9.728 22.528 12.8 12.288 32.768 11.776 45.056-0.512l22.528-23.552 0.512-0.512c3.072-3.584 30.208-38.4 30.208-81.92l-0.512-11.264c-1.536-44.544-5.632-162.816 119.296-235.52 88.064-51.2 173.056-32.256 208.896-19.968-36.864 19.456-143.36 83.456-144.896 84.48-22.016 14.336-55.808 58.88-26.112 122.88 17.408 37.376 43.52 76.8 80.896 120.32 14.336 17.408 62.976 37.376 103.424 15.36 24.576-13.312 125.44-73.216 130.048-75.776 2.048-1.024 4.608-2.56 7.68-3.584 0 2.56-0.512 6.144-1.024 10.752-5.632 35.84-35.328 155.136-191.488 181.76-49.664 8.704-89.6 3.584-121.856-0.512h-0.512c-37.888-4.608-73.216-9.216-101.888 14.336-31.232 26.112-40.96 34.304-35.84 54.272 3.584 14.336 16.384 24.064 30.72 24.064 2.56 0 5.12-0.512 7.68-1.024 6.656-1.536 12.8-5.632 16.896-10.752 2.048-2.048 7.68-6.656 20.992-18.432 6.656-5.632 25.088-3.584 52.736 0 34.816 4.608 81.92 10.24 141.312 0.512 157.184-26.624 228.864-138.752 243.2-234.496 7.68-38.912 0-64.512-21.504-78.336z"}
            ToolTipService.SetPlacement(ItemInstall, Primitives.PlacementMode.Right)
            ToolTipService.SetHorizontalOffset(ItemInstall, -50)
            ToolTipService.SetVerticalOffset(ItemInstall, 2.5)
            FrmSelectLeft.PanList.Children.Add(ItemInstall)
            AddHandler ItemInstall.Click, AddressOf ModpackInstall

            '边距
            FrmSelectLeft.PanList.Children.Add(New FrameworkElement With {.Height = 10, .IsHitTestVisible = False})

            '确认勾选状态
            For i = 0 To McFolderList.Count - 1
                If McFolderList(i).Path = PathMcFolder Then
                    CType(FrmSelectLeft.PanList.Children(i + 1), MyListItem).Checked = True '去掉第一个标题
                    Return
                End If
            Next
            If Not McFolderList.Any() Then
                Throw New ArgumentNullException("没有可用的 Minecraft 文件夹")
            Else
                Setup.Set("LaunchFolderSelect", McFolderList(0).Path.Replace(Path, "$"))
                CType(FrmSelectLeft.PanList.Children(1), MyListItem).Checked = True
            End If

        Catch ex As Exception
            Log(ex, "构建 Minecraft 文件夹列表 UI 出错", LogLevel.Feedback)
        Finally
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\") '刷新实例列表
        End Try
    End Sub
    Private McFolderListLast As List(Of McFolder)

    Private Sub MoveUp_Click(sender As Object, e As RoutedEventArgs)
        Dim folder As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        Dim index = McFolderList.IndexOf(folder)
        If index > 0 Then
            McFolderList.RemoveAt(index)
            McFolderList.Insert(index - 1, folder)
            UpdateFolderOrder()
        End If
    End Sub

    Private Sub MoveDown_Click(sender As Object, e As RoutedEventArgs)
        Dim folder As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        Dim index = McFolderList.IndexOf(folder)
        If index < McFolderList.Count - 1 Then
            McFolderList.RemoveAt(index)
            McFolderList.Insert(index + 1, folder)
            UpdateFolderOrder()
        End If
    End Sub

    Private Sub UpdateFolderOrder()
        Dim folders As New List(Of String)
        For Each folder As McFolder In McFolderList
            folders.Add(folder.Name & ">" & folder.Path)
        Next
        Setup.Set("LaunchFolders", Join(folders.ToArray, "|"))
        McFolderListUi()
    End Sub
    
    Private Sub Restore_Click(sender As Object, e As RoutedEventArgs)
        Dim folder As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        Dim index = McFolderList.IndexOf(folder)
        McFolderList(index).Type = McFolderType.Original
        McFolderList(index).Name = "官方启动器文件夹"
        UpdateFolderOrder()
    End Sub

    '添加文件夹
    Private Sub Add_Click()
        Dim NewFolder As String = ""
        '检查是否有下载任务
        If HasDownloadingTask() Then
            Hint("在下载任务进行时，无法添加游戏文件夹！", HintType.Critical)
            Return
        End If
        Try
            '获取输入
            NewFolder = SelectFolder()
            If NewFolder = "" Then Return
            If NewFolder.Contains("!") OrElse NewFolder.Contains(";") Then Hint("Minecraft 文件夹路径中不能含有感叹号或分号！", HintType.Critical) : Return
            '要求输入显示名称
            Dim SplitedNames As String() = NewFolder.TrimEnd("\").Split("\")
            Dim DefaultName As String = If(SplitedNames.Last = ".minecraft", If(SplitedNames.Count >= 3, SplitedNames(SplitedNames.Count - 2), ""), SplitedNames.Last)
            If DefaultName.Length > 40 Then DefaultName = DefaultName.Substring(0, 39)
            Dim NewName As String = MyMsgBoxInput("输入显示名称", "输入该文件夹在左边栏列表中显示的名称。", DefaultName,
                                              New ObjectModel.Collection(Of Validate) From {New ValidateNullOrWhiteSpace, New ValidateLength(1, 30), New ValidateExcept({">", "|"})})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            '添加文件夹
            AddFolder(NewFolder, NewName, True)
        Catch ex As Exception
            Log(ex, "添加文件夹失败（" & NewFolder & "）", LogLevel.Feedback)
        End Try
    End Sub
    ''' <summary>
    ''' 将指定文件夹添加到 Minecraft 文件夹列表，并选中它。
    ''' </summary>
    Public Shared Sub AddFolder(FolderPath As String, DisplayName As String, ShowHint As Boolean)
        RunInThread(
        Sub()
            Try
                If Not FolderPath.EndsWith("\") Then FolderPath &= "\" '加上斜杠……
                '检查文件夹权限
                If Not Files.CheckPermission(FolderPath) Then
                    If ShowHint Then
                        Hint("添加文件夹失败：PCL 没有访问该文件夹的权限！", HintType.Critical)
                        Return
                    Else
                        Throw New Exception("PCL 没有访问文件夹的权限：" & FolderPath)
                    End If
                End If
                '检查实际的 Minecraft 文件夹位置（没有问题，或是在子文件夹中）
                If Not Files.CheckPermission(FolderPath & "versions\") Then
                    For Each Folder As DirectoryInfo In New DirectoryInfo(FolderPath).GetDirectories
                        If Files.CheckPermission(Folder.FullName & "\versions\") Then
                            FolderPath = Folder.FullName & "\"
                            Exit For
                        End If
                    Next
                End If
                '判断是否已经添加过，若添加过则直接修改自定义名
                Dim Folders As New List(Of String)(Setup.Get("LaunchFolders").ToString.Split("|"))
                Dim IsAdded As Boolean = False
                Dim IsReplace As Boolean = False
                For i = 0 To Folders.Count - 1
                    Dim Folder As String = Folders(i)
                    If Folder = "" Then Continue For
                    If Folder.Split(">")(1) = FolderPath Then
                        IsAdded = True
                        If Folder.Split(">")(0) = DisplayName Then
                            If ShowHint Then Hint("此文件夹已在列表中！", HintType.Info)
                            Return
                        Else
                            Folders(i) = DisplayName & ">" & FolderPath
                            IsReplace = True
                            If ShowHint Then Hint("文件夹名称已更新为 " & DisplayName & " ！", HintType.Finish)
                        End If
                        Exit For
                    End If
                Next
                '如果没有添加过，则添加进去
                If Not IsAdded Then Folders.Add(DisplayName & ">" & FolderPath)
                '保存
                Setup.Set("LaunchFolders", Join(Folders.ToArray, "|"))
                '切换选择并更新列表
                Setup.Set("LaunchFolderSelect", FolderPath.Replace(Path, "$"))
                McFolderListLoader.Start(IsForceRestart:=True)
                '提示
                If IsReplace Then Return
                If ShowHint Then Hint("文件夹 " & DisplayName & " 已添加！", HintType.Finish)
                '检查是否为根目录整合包，自动关闭版本隔离
                '1. 根目录中存在数个 Mod
                Dim ModFolder As New DirectoryInfo(FolderPath & "mods\")
                If Not (ModFolder.Exists AndAlso ModFolder.EnumerateFiles.Count >= 3) Then Return
                '2. 实例数较少，可能为整合包
                Dim VersionFolder As New DirectoryInfo(FolderPath & "versions\")
                If Not (VersionFolder.Exists AndAlso VersionFolder.EnumerateDirectories.Count <= 3) Then Return
                '3. 能够找到可安装 Mod 的实例
                For Each VersionPath In VersionFolder.EnumerateDirectories
                    Dim Version As New McInstance(VersionPath.FullName)
                    Version.Load()
                    If Not Version.Modable Then Continue For
                    '4. 该实例的隔离文件夹下不存在 mods
                    Dim ModIndieFolder As New DirectoryInfo(Version.Path & "mods\")
                    If ModIndieFolder.Exists AndAlso ModIndieFolder.EnumerateFiles.Any Then Return
                    '满足以上全部条件则视为根目录整合包
                    Setup.Set("VersionArgumentIndie", 2, instance:=Version)
                    Setup.Set("VersionArgumentIndieV2", False, instance:=Version)
                    Log("[Setup] 已自动关闭单版本隔离：" & Version.Name, LogLevel.Debug)
                Next
            Catch ex As Exception
                Log(ex, "向文件夹列表中添加新文件夹失败", LogLevel.Feedback)
            End Try
        End Sub)
    End Sub

    '创建文件夹
    Public Sub Create_Click()
        '检查是否有下载任务
        If HasDownloadingTask() Then
            Hint("在下载任务进行时，无法创建游戏文件夹！", HintType.Critical)
            Return
        End If
        If Not Directory.Exists(Path & ".minecraft\") Then
            Directory.CreateDirectory(Path & ".minecraft\")
            Directory.CreateDirectory(Path & ".minecraft\versions\")
            Setup.Set("LaunchFolderSelect", "$.minecraft\")
            McFolderLauncherProfilesJsonCreate(Path & ".minecraft\")
            Hint("新建 .minecraft 文件夹成功！", HintType.Finish)
        End If
        McFolderListLoader.Start(IsForceRestart:=True)
    End Sub

    '右键菜单
    Public Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        Try

            Dim Folder As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
            Select Case MyMsgBox("是否需要清理 PCL 在该文件夹中的配置文件？" & vbCrLf & "这包括各个实例的独立设置（如自定义图标、第三方登录配置）等，对游戏本身没有影响。", "配置文件清理", "删除", "保留", "取消")
                Case 1
                    '删除配置文件
                    If File.Exists(Folder.Path & "PCL.ini") Then File.Delete(Folder.Path & "PCL.ini")
                    If Directory.Exists(Folder.Path & "versions\") Then
                        For Each Version In New DirectoryInfo(Folder.Path & "versions\").EnumerateDirectories
                            If Directory.Exists(Version.FullName & "\PCL\") Then Directory.Delete(Version.FullName & "\PCL\", True)
                        Next
                    End If
                Case 2
                '不删除
                Case 3
                    '取消
                    Return
            End Select
            '若修改了本部分代码，应对应修改 Delete_Click 中的代码
            '获取并删除列表项
            Dim Folders As New List(Of String)(Setup.Get("LaunchFolders").ToString.Split("|"))
            Dim Name As String = ""
            For i = 0 To Folders.Count - 1
                If Folders(i) = "" Then Exit For
                If Folders(i).ToString.EndsWith(Folder.Path) Then
                    Name = Folders(i).ToString.BeforeFirst(">")
                    Folders.RemoveAt(i)
                    Exit For
                End If
            Next
            '保存
            Setup.Set("LaunchFolders", If(Not Folders.Any(), "", Join(Folders.ToArray, "|")))
            Hint(If(Folder.Type = McFolderType.Custom, "文件夹 " & Name & " 已从列表中移除！", "文件夹名称已复原！"), HintType.Finish)
            McFolderListLoader.Start(IsForceRestart:=True)

        Catch ex As Exception
            Log(ex, "从列表中移除游戏文件夹失败", LogLevel.Feedback)
        End Try
    End Sub
    Public Sub Delete_Click(sender As Object, e As RoutedEventArgs)
        Dim Folder As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        Dim DeleteText As String = If((Folder.Type = McFolderType.Original OrElse Folder.Type = McFolderType.RenamedOriginal) AndAlso Folder.Path = Path & ".minecraft\" AndAlso McFolderList.Count = 1, "清空", "删除")
        If MyMsgBox("你确定要" & DeleteText & "这个文件夹吗？" & vbCrLf & "目标文件夹：" & Folder.Path & vbCrLf & vbCrLf & "这会导致该文件夹中的所有存档与其他文件永久丢失，且不可恢复！", "删除警告", "取消", "确认", "取消") <> 2 Then Return
        If MyMsgBox("如果你在该文件夹中存放了除 MC 以外的其他文件，这些文件也会被一同删除！" & vbCrLf & "继续删除会导致该文件夹中的所有文件永久丢失，请在仔细确认后再继续！" & vbCrLf & "目标文件夹：" & Folder.Path & vbCrLf & vbCrLf & "这是最后一次警告！", "删除警告", "确认" & DeleteText, "取消", IsWarn:=True) <> 1 Then Return
        '移出列表
        If Folder.Type = McFolderType.Custom Then
            Dim Folders As New List(Of String)(Setup.Get("LaunchFolders").ToString.Split("|"))
            For i = 0 To Folders.Count - 1
                If Folders(i) = "" Then Exit For
                If Folders(i).ToString.EndsWith(Folder.Path) Then
                    'Name = Folders(i).ToString.Before(">")
                    Folders.RemoveAt(i)
                    Exit For
                End If
            Next
            Setup.Set("LaunchFolders", If(Not Folders.Any(), "", Join(Folders.ToArray, "|")))
        End If
        RunInNewThread(
        Sub()
            '删除文件夹
            Try
                Hint("正在" & DeleteText & "文件夹 " & Folder.Name & "！", HintType.Info)
                DeleteDirectory(Folder.Path)
                If DeleteText = "清空" Then Directory.CreateDirectory(Folder.Path)
                Hint("已" & DeleteText & "文件夹 " & Folder.Name & "！", HintType.Finish)
            Catch ex As Exception
                Log(ex, DeleteText & "文件夹 " & Folder.Name & " 失败", LogLevel.Hint)
            Finally
                '刷新列表
                McFolderListLoader.Start(IsForceRestart:=True)
            End Try
        End Sub, "Folder Delete " & GetUuid(), ThreadPriority.BelowNormal)
    End Sub
    Public Sub Open_Click(sender As Object, e As RoutedEventArgs)
        OpenExplorer(CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Info)
    End Sub
    Public Sub Refresh_Click(sender As Object, e As RoutedEventArgs)
        Dim Data As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        RefreshCurrent(Data.Path)
    End Sub
    Public Sub RefreshCurrent() Implements IRefreshable.Refresh
        RefreshCurrent(PathMcFolder)
    End Sub
    Public Shared Sub RefreshCurrent(Folder As String)
        WriteIni(Folder & "PCL.ini", "InstanceCache", "") '删除缓存以强制要求下一次加载时更新列表
        If Folder = PathMcFolder Then LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub
    Public Sub Rename_Click(sender As Object, e As RoutedEventArgs)
        Dim Folder As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        Try
            '获取输入
            Dim NewName As String =
                MyMsgBoxInput("输入新名称", "", Folder.Name,
                              New ObjectModel.Collection(Of Validate) From {New ValidateNullOrWhiteSpace, New ValidateLength(1, 30), New ValidateExcept({">", "|"})})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            '修改自定义名
            Dim Folders As New List(Of String)(Setup.Get("LaunchFolders").ToString.Split("|"))
            Dim IsAdded As Boolean = False
            For i = 0 To Folders.Count - 1
                Dim FolderCurrent As String = Folders(i)
                If FolderCurrent = "" Then Continue For
                If FolderCurrent.Split(">")(1) = Folder.Path Then
                    IsAdded = True
                    If FolderCurrent.Split(">")(0) = NewName Then
                        '名称未修改
                        Return
                    Else
                        Folders(i) = NewName & ">" & Folder.Path
                    End If
                    Exit For
                End If
            Next
            '如果没有添加过，则添加进去（因为修改了默认项的名称）
            If Not IsAdded Then Folders.Add(NewName & ">" & Folder.Path)
            Hint("文件夹名称已更新为 " & NewName & " ！", HintType.Finish)
            '保存
            Setup.Set("LaunchFolders", Join(Folders.ToArray, "|"))
            McFolderListLoader.Start(IsForceRestart:=True)
        Catch ex As Exception
            Log(ex, "重命名文件夹失败", LogLevel.Feedback)
        End Try
    End Sub

    '点击选项
    Public Sub Folder_Change(sender As MyListItem, e As RouteEventArgs)
        If Not e.RaiseByMouse OrElse Not sender.Checked Then Return
        '检查是否有下载任务
        If HasDownloadingTask(True) Then
            Hint("在下载任务进行时，无法切换游戏文件夹！", HintType.Critical)
            e.Handled = True
            Return
        End If
        '更换
        Setup.Set("LaunchFolderSelect", CType(sender.Tag, McFolder).Path.Replace(Path, "$"))
        McFolderListLoader.Start(IsForceRestart:=True)
        LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\") '刷新实例列表
    End Sub

#Region "拖拽排序功能"
    
    ' 拖拽开始时的鼠标移动处理
    Private Sub Item_MouseMove(sender As Object, e As MouseEventArgs)
        Dim Item As MyListItem = CType(sender, MyListItem)
        ' 当按住鼠标左键时开始拖拽操作
        If e.LeftButton = MouseButtonState.Pressed Then
            Try
                DragDrop.DoDragDrop(Item, Item.Tag, DragDropEffects.Move)
            Catch ex As Exception
                Log(ex, "开始拖拽操作失败", LogLevel.Debug)
            End Try
        End If
    End Sub

    ' 拖拽进入时的处理
    Private Sub Item_DragEnter(sender As Object, e As DragEventArgs)
        Try
            If e.Data.GetDataPresent(GetType(McFolder)) Then
                e.Effects = DragDropEffects.Move
                ' 添加视觉反馈
                Dim Item As MyListItem = CType(sender, MyListItem)
                Item.Opacity = 0.7
            Else
                e.Effects = DragDropEffects.None
            End If
        Catch ex As Exception
            e.Effects = DragDropEffects.None
        End Try
        e.Handled = True
    End Sub

    ' 拖拽悬停时的处理
    Private Sub Item_DragOver(sender As Object, e As DragEventArgs)
        Try
            If e.Data.GetDataPresent(GetType(McFolder)) Then
                e.Effects = DragDropEffects.Move
            Else
                e.Effects = DragDropEffects.None
            End If
        Catch ex As Exception
            e.Effects = DragDropEffects.None
        End Try
        e.Handled = True
    End Sub

    ' 拖拽离开时的处理
    Private Sub Item_DragLeave(sender As Object, e As DragEventArgs)
        Try
            ' 恢复视觉状态
            Dim Item As MyListItem = CType(sender, MyListItem)
            Item.Opacity = 1.0
        Catch ex As Exception
            Log(ex, "拖拽离开处理失败", LogLevel.Debug)
        End Try
        e.Handled = True
    End Sub

    ' 拖拽放下时的处理
    Private Sub Item_Drop(sender As Object, e As DragEventArgs)
        Try
            Dim TargetItem As MyListItem = CType(sender, MyListItem)
            Dim TargetFolder As McFolder = CType(TargetItem.Tag, McFolder)
            
            ' 恢复视觉状态
            TargetItem.Opacity = 1.0
            
            ' 检查数据有效性
            If Not e.Data.GetDataPresent(GetType(McFolder)) Then
                e.Handled = True
                Return
            End If
            
            Dim SourceFolder As McFolder = CType(e.Data.GetData(GetType(McFolder)), McFolder)
            
            ' 检查是否为有效的拖拽操作
            If SourceFolder Is Nothing OrElse SourceFolder Is TargetFolder Then
                e.Handled = True
                Return
            End If
            
            ' 检查文件夹是否在列表中
            If Not McFolderList.Contains(SourceFolder) OrElse Not McFolderList.Contains(TargetFolder) Then
                e.Handled = True
                Return
            End If

            ' 获取源文件夹和目标文件夹的索引
            Dim SourceIndex As Integer = McFolderList.IndexOf(SourceFolder)
            Dim TargetIndex As Integer = McFolderList.IndexOf(TargetFolder)

            ' 执行移动操作
            If SourceIndex <> TargetIndex Then
                ' 先移除源文件夹
                McFolderList.RemoveAt(SourceIndex)
                
                ' 计算新的插入位置
                Dim NewTargetIndex As Integer
                
                If SourceIndex < TargetIndex Then
                    ' 向下拖拽：插入到目标项目的后面
                    ' 由于移除了源项目，目标索引已经自动减1，所以直接使用TargetIndex就是插入到目标后面
                    NewTargetIndex = TargetIndex
                Else
                    ' 向上拖拽：插入到目标项目的前面
                    NewTargetIndex = TargetIndex
                End If
                
                ' 确保插入位置不超出列表范围
                If NewTargetIndex > McFolderList.Count Then
                    NewTargetIndex = McFolderList.Count
                ElseIf NewTargetIndex < 0 Then
                    NewTargetIndex = 0
                End If
                
                ' 插入到新位置
                McFolderList.Insert(NewTargetIndex, SourceFolder)
                
                ' 更新文件夹顺序并刷新UI
                UpdateFolderOrder()
                
                Dim Direction As String = If(SourceIndex < TargetIndex, "后面", "前面")
                Log("[Control] 文件夹拖拽排序：" & SourceFolder.Name & " -> 位置 " & NewTargetIndex & " (在 " & TargetFolder.Name & " " & Direction & ")", LogLevel.Debug)
            End If

        Catch ex As Exception
            Log(ex, "拖拽放下操作失败", LogLevel.Feedback)
        Finally
            e.Handled = True
        End Try
    End Sub

#End Region

End Class
