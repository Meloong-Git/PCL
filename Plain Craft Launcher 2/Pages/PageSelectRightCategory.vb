Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Collections.ObjectModel

Partial Public Class PageSelectRight

#Region "渲染"

    ''' <summary>
    ''' 在主 McInstanceListUI 的最后阶段调用，用于渲染所有用户自定义分类卡片和"新建分类"按钮。
    ''' </summary>
    Public Sub RenderCustomCategoryCards()
        If McCustomCategories Is Nothing Then Return
        Try
            '清理已不属于现有版本文件夹的实例名映射
            Dim AllInstances = McInstanceList.Values.SelectMany(Function(l) l).ToList()
            Dim NameToInstance As New Dictionary(Of String, McInstance)
            For Each Inst In AllInstances
                If Not NameToInstance.ContainsKey(Inst.Name) Then NameToInstance(Inst.Name) = Inst
            Next
            Dim HasTrimmed As Boolean = False
            For Each Cat In McCustomCategories.Values.ToList
                Dim Trimmed = Cat.InstanceNames.Where(Function(n) NameToInstance.ContainsKey(n)).ToList()
                If Trimmed.Count <> Cat.InstanceNames.Count Then
                    Cat.InstanceNames = Trimmed
                    HasTrimmed = True
                End If
            Next
            If HasTrimmed Then SaveCustomCategories(McFolderSelected)

            '按字典顺序遍历，便于稳定显示
            For Each Cat In McCustomCategories.Values.OrderBy(Function(c) c.Name).ToList()
                Dim InstList As New List(Of McInstance)
                For Each Iname In Cat.InstanceNames.ToList()
                    If NameToInstance.ContainsKey(Iname) Then InstList.Add(NameToInstance(Iname))
                Next

                '若分类已空，跳过渲染
                If Not InstList.Any Then Continue For

                '构造卡片
                Dim Card As New MyCard With {
                    .Title = Cat.Name & " (" & InstList.Count & ")",
                    .Margin = New Thickness(0, 0, 0, 15),
                    .SwapType = 0
                }
                Dim StackInner As New StackPanel With {
                    .Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0),
                    .VerticalAlignment = VerticalAlignment.Top,
                    .RenderTransform = New TranslateTransform(0, 0),
                    .Tag = Cat.Name
                }
                Card.Children.Add(StackInner)
                Card.SwapControl = StackInner
                AddCustomCategoryCardContextMenu(Card, Cat.Name)
                PanMain.Children.Add(Card)
                '让卡片以展开状态开始，并初始化占位
                MyCard.StackInstall(StackInner, 0, Card.Title)

                '添加实例项
                For Each Inst In InstList
                    Dim Item As New MyListItem With {
                        .IsScaleAnimationEnabled = False,
                        .Type = MyListItem.CheckType.Clickable,
                        .MinPaddingRight = 30,
                        .Title = Inst.Name,
                        .Info = Inst.Info,
                        .Logo = Inst.Logo,
                        .Height = 42,
                        .Tag = Inst
                    }
                    AddHandler Item.Click, AddressOf Item_Click
                    McInstanceListContent(Item, Nothing)
                    '为自定义分类项额外加一个"从本分类移除"按钮
                    Dim BtnRemoveFromCat As New MyIconButton With {
                        .Logo = IconDataMinus,
                        .LogoScale = 0.85,
                        .ToolTip = $"从自定义分类 {Cat.Name} 中移除"
                    }
                    AddHandler BtnRemoveFromCat.Click, Sub() PromptRemoveInstanceFromCategory(Cat.Name, Inst)
                    Item.Buttons = Item.Buttons.Concat({BtnRemoveFromCat}).ToArray()
                    StackInner.Children.Add(Item)
                    '为该项启用自定义分类卡内的拖拽排序
                    EnableCustomCategoryItemDrag(Item, Cat.Name)
                Next

                '添加"加入版本到此分类"入口
                Dim AddItem As New MyListItem With {
                    .IsScaleAnimationEnabled = False,
                    .Type = MyListItem.CheckType.Clickable,
                    .Title = $"+   把版本加入 {Cat.Name}",
                    .Height = 34,
                    .ToolTip = "将一个仍未归入此分类的版本加入此分类"
                }
                AddHandler AddItem.Click, Sub() AddInstanceToCategoryPicker(Cat.Name, InstList, NameToInstance)
                StackInner.Children.Add(AddItem)
            Next

            '"新建分类"按钮（在所有自定义分类卡之后）
            Dim NewCategoryItem As New MyListItem With {
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.Clickable,
                .Title = "+   新建分类",
                .Height = 34,
                .ToolTip = "新建一个用于对版本进行自定义分类的分类卡"
            }
            AddHandler NewCategoryItem.Click, Sub() PromptCreateCustomCategory()
            PanMain.Children.Add(NewCategoryItem)
        Catch ex As Exception
            Logger.Error(ex, "渲染自定义分类卡片失败")
        End Try
    End Sub

    ''' <summary>
    ''' 为自定义分类卡片自身附加右键菜单（重命名 / 删除）。
    ''' 实现为在每次 Build 时附加 Tag，菜单在卡片标题点击时触发。
    ''' 由于 MyCard 没有内置标题右键，此处改用卡片顶部附加一个 MyIconButton 来弹出菜单。
    ''' </summary>
    Private Sub AddCustomCategoryCardContextMenu(Card As MyCard, CategoryName As String)
        Try
            Dim Menu As New ContextMenu
            Dim RenameItem As New MyMenuItem With {.Header = "重命名分类", Icon = "M53.2929,21.2929L54.7071,22.7071C56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L52.2323,31.5459L44.4541,23.7677L46.9289,21.2929C48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M31.7262,52.052L23.948,44.2738L43.0399,25.182L50.818,32.9601L31.7262,52.052 Z M23.2409,47.1023L28.8977,52.7591L21.0463,54.9537L23.2409,47.1023 Z"}
            AddHandler RenameItem.Click, Sub() PromptRenameCustomCategory(CategoryName)
            Menu.Items.Add(RenameItem)
            Dim DeleteItem As New MyMenuItem With {.Header = "删除分类", Icon = "M26.9166,22.1667L37.9999,33.25L49.0832,22.1668L53.8332,26.9168L42.7499,38L53.8332,49.0834L49.0833,53.8334L37.9999,42.75L26.9166,53.8334L22.1666,49.0833L33.25,38L22.1667,26.9167L26.9166,22.1667 Z "}
            AddHandler DeleteItem.Click, Sub() DeleteCustomCategoryByName(CategoryName)
            Menu.Items.Add(DeleteItem)
            Card.ContextMenu = Menu
        Catch ex As Exception
            Logger.Warn(ex, "附加自定义分类右键菜单失败")
        End Try
    End Sub

#End Region

#Region "自定义分类管理"

    ''' <summary>弹出新建自定义分类的对话框并创建分类。</summary>
    Public Sub PromptCreateCustomCategory()
        Try
            Dim Name As String = MyMsgBoxInput("新建自定义分类", "请输入分类的名称（用于在版本列表中标识该分类）", "",
                New ObjectModel.Collection(Of Validate) From {
                    New ValidateNullOrWhiteSpace,
                    New ValidateLength(1, 20),
                    New ValidateExcept({">", "|", ":"})
                })
            If String.IsNullOrWhiteSpace(Name) Then Return
            If McCustomCategories.ContainsKey(Name) Then
                Hint($"已存在同名分类 {Name}！", HintType.Red)
                Return
            End If
            If CreateCustomCategory(McFolderSelected, Name) Then
                Hint($"已创建自定义分类 {Name}", HintType.Green)
                RefreshAfterCustomCategoryChange()
            End If
        Catch ex As Exception
            Logger.Error(ex, "新建自定义分类失败")
        End Try
    End Sub

    ''' <summary>弹窗确认后重命名一个自定义分类。</summary>
    Public Sub PromptRenameCustomCategory(OldName As String)
        Try
            Dim NewName As String = MyMsgBoxInput("重命名分类", $"将分类 {OldName} 重命名为：", OldName,
                New ObjectModel.Collection(Of Validate) From {
                    New ValidateNullOrWhiteSpace,
                    New ValidateLength(1, 20),
                    New ValidateExcept({">", "|", ":"})
                })
            If String.IsNullOrWhiteSpace(NewName) OrElse NewName = OldName Then Return
            If McCustomCategories.ContainsKey(NewName) Then
                Hint($"已存在同名分类 {NewName}！", HintType.Red)
                Return
            End If
            If RenameCustomCategory(McFolderSelected, OldName, NewName) Then
                Hint($"分类 {OldName} 已重命名为 {NewName}", HintType.Green)
                RefreshAfterCustomCategoryChange()
            End If
        Catch ex As Exception
            Logger.Error(ex, "重命名自定义分类失败")
        End Try
    End Sub

    ''' <summary>删除自定义分类。</summary>
    Public Sub DeleteCustomCategoryByName(CategoryName As String)
        Try
            If MyMsgBox($"确定要删除自定义分类 {CategoryName} 吗？" & vbCrLf & "该分类下的版本关联将被一并清除（不会影响版本本身）。",
                       "删除自定义分类", "删除", "取消") <> 1 Then Return
            If DeleteCustomCategory(McFolderSelected, CategoryName) Then
                Hint($"已删除自定义分类 {CategoryName}", HintType.Green)
                RefreshAfterCustomCategoryChange()
            End If
        Catch ex As Exception
            Logger.Error(ex, "删除自定义分类失败")
        End Try
    End Sub

    ''' <summary>从自定义分类中移除一个版本（带提示与刷新）。</summary>
    Public Sub PromptRemoveInstanceFromCategory(CategoryName As String, Instance As McInstance)
        Try
            If RemoveInstanceFromCategory(McFolderSelected, CategoryName, Instance.Name) Then
                Hint($"已从 {CategoryName} 中移除 {Instance.Name}", HintType.Green)
                RefreshAfterCustomCategoryChange()
            End If
        Catch ex As Exception
            Logger.Error(ex, "从自定义分类中移除版本失败")
        End Try
    End Sub

    ''' <summary>
    ''' 在分类卡底部打开一个选择器，把版本加入此分类（先过滤掉已经在该分类中的）。
    ''' </summary>
    Private Sub AddInstanceToCategoryPicker(CategoryName As String, AlreadyInCategory As List(Of McInstance), NameToInstance As Dictionary(Of String, McInstance))
        Try
            Dim Cat = McCustomCategories(CategoryName)
            Dim InCat As New HashSet(Of String)(Cat.InstanceNames)
            Dim Candidates = NameToInstance.Values.
                Where(Function(i) Not InCat.Contains(i.Name)).
                OrderBy(Function(i) i.Name).
                ToList()
            If Not Candidates.Any Then
                Hint("已经所有版本都在此分类中了", HintType.Blue)
                Return
            End If
            'MyMsgBoxSelect 要求第一个参数为 IEnumerable(Of IMyRadio)，并以 0-based 索引返回（Nothing = 取消）。
            Dim Selections As New List(Of IMyRadio)
            For Each Inst In Candidates
                Selections.Add(New MyRadioBox With {.Text = Inst.Name})
            Next
            Dim SelectedIndex As Integer? = MyMsgBoxSelect(Selections,
                $"请选择要加入 {CategoryName} 的版本",
                "加入", "取消")
            If SelectedIndex Is Nothing Then Return
            If SelectedIndex < 0 OrElse SelectedIndex >= Candidates.Count Then Return
            Dim Selected As McInstance = Candidates(SelectedIndex.Value)
            If AddInstanceToCategory(McFolderSelected, CategoryName, Selected.Name) Then
                Hint($"已把 {Selected.Name} 加入 {CategoryName}", HintType.Green)
                RefreshAfterCustomCategoryChange()
            End If
        Catch ex As Exception
            Logger.Error(ex, "把版本加入自定义分类失败")
        End Try
    End Sub

    ''' <summary>统一的"自定义分类"变更后的 UI 刷新入口。</summary>
    Private Sub RefreshAfterCustomCategoryChange()
        McInstanceListForceRefresh = True
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub

#End Region

#Region "拖拽"

    ' 减号图标的 Path Data（MyIconButton.Logo 接受 path data 字符串）
    Private Const IconDataMinus As String = "M0,9 L24,9 L24,15 L0,15 Z"

    ''' <summary>
    ''' 在自定义分类卡内拖拽排序时附带的数据。
    ''' </summary>
    Public Class CustomCategoryDragData
        Public CategoryName As String
        Public InstanceName As String
    End Class

    ''' <summary>
    ''' 为单个自定义分类卡内的 <see cref="MyListItem"/> 启用拖拽排序。
    ''' 仅在同一个分类卡的项之间拖动生效；不允许跨分类卡的拖拽。
    ''' </summary>
    Public Sub EnableCustomCategoryItemDrag(Item As MyListItem, CategoryName As String)
        Try
            Item.AllowDrop = True
            Dim StartPoint As Point = New Point(0, 0)
            Dim DragStarted As Boolean = False
            Dim SrcCategory As String = CategoryName
            Dim SrcInstanceName As String = ""
            AddHandler Item.PreviewMouseLeftButtonDown, Sub(s, e)
                StartPoint = e.GetPosition(Nothing)
                Dim SrcInst As McInstance = TryCast(Item.Tag, McInstance)
                SrcInstanceName = If(SrcInst Is Nothing, "", SrcInst.Name)
                DragStarted = False
            End Sub
            AddHandler Item.PreviewMouseMove, Sub(s, e)
                Dim SrcItem As MyListItem = CType(s, MyListItem)
                If DragStarted OrElse e.LeftButton <> MouseButtonState.Pressed Then Return
                Dim Cur = e.GetPosition(Nothing)
                If Math.Abs(Cur.X - StartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance OrElse
                   Math.Abs(Cur.Y - StartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance Then
                    If String.IsNullOrEmpty(SrcInstanceName) Then Return
                    DragStarted = True
                    Try
                        Dim Payload As New CustomCategoryDragData With {.CategoryName = SrcCategory, .InstanceName = SrcInstanceName}
                        Dim DataObj As New DataObject(GetType(CustomCategoryDragData), Payload)
                        DragDrop.DoDragDrop(SrcItem, DataObj, DragDropEffects.Move)
                    Finally
                        DragStarted = False
                    End Try
                End If
            End Sub
            AddHandler Item.PreviewDragOver, Sub(s, e)
                Dim Data = TryCast(e.Data.GetData(GetType(CustomCategoryDragData)), CustomCategoryDragData)
                Dim TargetInst As McInstance = TryCast(Item.Tag, McInstance)
                If Data IsNot Nothing AndAlso TargetInst IsNot Nothing AndAlso
                   Data.CategoryName = CategoryName AndAlso Data.InstanceName <> TargetInst.Name Then
                    e.Effects = DragDropEffects.Move
                Else
                    e.Effects = DragDropEffects.None
                End If
                e.Handled = True
            End Sub
            AddHandler Item.PreviewDrop, Sub(s, e)
                Dim Data = TryCast(e.Data.GetData(GetType(CustomCategoryDragData)), CustomCategoryDragData)
                If Data Is Nothing OrElse Data.CategoryName <> CategoryName Then Return
                Dim TargetInst As McInstance = TryCast(Item.Tag, McInstance)
                If TargetInst Is Nothing Then Return
                Try
                    Dim CatList As List(Of String) = McCustomCategories(CategoryName).InstanceNames
                    Dim SrcIdx As Integer = CatList.IndexOf(Data.InstanceName)
                    Dim TgtIdx As Integer = CatList.IndexOf(TargetInst.Name)
                    If SrcIdx < 0 OrElse TgtIdx < 0 OrElse SrcIdx = TgtIdx Then Return
                    MoveInstanceInCategory(McFolderSelected, CategoryName, SrcIdx, TgtIdx)
                    Hint($"已在 {CategoryName} 内调整顺序", HintType.Green)
                    RefreshAfterCustomCategoryChange()
                Catch ex As Exception
                    Logger.Error(ex, "拖拽换位失败")
                End Try
                e.Handled = True
            End Sub
        Catch ex As Exception
            Logger.Warn(ex, "为自定义分类项启用拖拽失败")
        End Try
    End Sub

#End Region

End Class
