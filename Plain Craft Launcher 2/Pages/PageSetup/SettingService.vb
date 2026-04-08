Public Class SettingService

#Region "属性"

    ''' <summary>
    ''' 指定该控件所对应的设置项。
    ''' </summary>
    Public Shared Function GetKey(obj As DependencyObject) As String
        If obj Is Nothing Then Return Nothing
        Return obj.GetValue(KeyProperty)
    End Function
    Public Shared Sub SetKey(obj As DependencyObject, value As String)
        obj.SetValue(KeyProperty, value)
    End Sub
    Public Shared ReadOnly KeyProperty As DependencyProperty =
        DependencyProperty.RegisterAttached("Key", GetType(String), GetType(SettingService), New PropertyMetadata(Nothing))

    ''' <summary>
    ''' 指定该控件所对应的设置值。
    ''' </summary>
    Public Shared Function GetValue(obj As DependencyObject) As String
        If obj Is Nothing Then Return Nothing
        Return obj.GetValue(ValueProperty)
    End Function
    Public Shared Sub SetValue(obj As DependencyObject, value As String)
        obj.SetValue(ValueProperty, value)
    End Sub
    Public Shared ReadOnly ValueProperty As DependencyProperty =
        DependencyProperty.RegisterAttached("Value", GetType(String), GetType(SettingService), New PropertyMetadata(Nothing))

#End Region

    ''' <summary>
    ''' 重置该控件以及它所有的子控件的设置值。
    ''' </summary>
    Public Shared Sub ResetSettings(Target As DependencyObject)
        LogicalTreeHelper.GetChildren(Target).OfType(Of DependencyObject)().ForEach(Sub(c) ResetSettings(c)) '处理子控件
        Dim Key = GetKey(Target)
        If Key Is Nothing Then Return
        Settings.Reset(Key, Instance:=PageInstanceLeft.Instance)
    End Sub

    ''' <summary>
    ''' 更新该控件以及它所有的子控件当前显示的设置值。
    ''' </summary>
    Public Shared Sub RefreshSettings(Target As DependencyObject)
        LogicalTreeHelper.GetChildren(Target).OfType(Of DependencyObject)().ForEach(Sub(c) RefreshSettings(c)) '处理子控件
        Dim Key = GetKey(Target)
        If Key Is Nothing Then Return
        Dim NewValue = Settings.Get(Key, Instance:=PageInstanceLeft.Instance)
        If TypeOf Target Is ISettingControl Then DirectCast(Target, ISettingControl).RefreshSetting(NewValue)
    End Sub

    ''' <summary>
    ''' 当控件对应的设置改变时，将内容保存到设置中。
    ''' </summary>
    Public Shared Sub SaveSetting(sender As Object)
        If AniControlEnabled <> 0 Then Return
        If TypeOf sender IsNot ISettingControl Then Return
        Dim Key = GetKey(sender)
        If Key Is Nothing Then Return
        Dim NewValue As String = DirectCast(sender, ISettingControl).GetCurrentSetting()
        If NewValue Is Nothing Then Return
        Settings.Set(Key, NewValue, Instance:=PageInstanceLeft.Instance)
    End Sub

End Class

Public Interface ISettingControl

    ''' <summary>
    ''' 更新该控件当前所显示的设置值。
    ''' </summary>
    Sub RefreshSetting(NewValue As String)
    ''' <summary>
    ''' 获取该控件当前设定的设置值。
    ''' 若不应当关联该设置值，则返回 Nothing。
    ''' </summary>
    Function GetCurrentSetting() As String

End Interface
