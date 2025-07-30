'动画引擎模块
'使用 Ani 作为方法或属性的开头，使用 Aa 作为单个动画对象的开头（便于自动补全）

Public Module ModAnimation

#Region "声明"

    ''' <summary>
    ''' 动画速度。最大为 200。
    ''' </summary>
    Public AniSpeed As Double = 1
    ''' <summary>
    ''' 动画组列表。
    ''' </summary>
    Public AniGroups As New Dictionary(Of String, AniGroupEntry)
    Public Class AniGroupEntry
        Public Data As List(Of AniData)
        Public StartTick As Long
        Public Uuid As Integer = GetUuid()
    End Class
    ''' <summary>
    ''' 上一次记刻的时间。
    ''' </summary>
    Private AniLastTick As Long
    ''' <summary>
    ''' 动画模块是否正在运行。
    ''' </summary>
    Public AniRunning As Boolean = False
    Private _AniControlEnabled As Integer = 0
    Private ReadOnly AniControlEnabledLock As New Object
    ''' <summary>
    ''' 控件动画执行是否开启。先 +1，再 -1。
    ''' </summary>
    Public Property AniControlEnabled() As Integer
        Get
            Return _AniControlEnabled
        End Get
        Set(value As Integer)
            SyncLock AniControlEnabledLock
                _AniControlEnabled = value
            End SyncLock
        End Set
    End Property

#End Region

#Region "类与枚举"

    ''' <summary>
    ''' 单个动画对象。
    ''' </summary>
    ''' <remarks></remarks>
    Public Structure AniData

        ''' <summary>
        ''' 动画种类。
        ''' </summary>
        ''' <remarks></remarks>
        Public TypeMain As AniType
        ''' <summary>
        ''' 动画副种类。
        ''' </summary>
        ''' <remarks></remarks>
        Public TypeSub As AniTypeSub

        ''' <summary>
        ''' 动画总长度。
        ''' </summary>
        ''' <remarks></remarks>
        Public TimeTotal As Integer
        ''' <summary>
        ''' 已经执行的动画长度。如果为负数则为延迟。
        ''' </summary>
        ''' <remarks></remarks>
        Public TimeFinished As Integer
        ''' <summary>
        ''' 已经完成的百分比。
        ''' </summary>
        ''' <remarks></remarks>
        Public TimePercent As Double

        ''' <summary>
        ''' 是否为“以后”。
        ''' </summary>
        ''' <remarks></remarks>
        Public IsAfter As Boolean

        ''' <summary>
        ''' 插值器类型。
        ''' </summary>
        ''' <remarks></remarks>
        Public Ease As AniEase
        ''' <summary>
        ''' 动画对象。
        ''' </summary>
        ''' <remarks></remarks>
        Public Obj As Object
        ''' <summary>
        ''' 动画值。
        ''' </summary>
        ''' <remarks></remarks>
        Public Value As Object
        ''' <summary>
        ''' 上次执行时的动画值。
        ''' </summary>
        ''' <remarks></remarks>
        Public ValueLast As Object

        Public Overrides Function ToString() As String
            Return GetStringFromEnum(TypeMain) & " | " & TimeFinished & "/" & TimeTotal & "(" & Math.Round(TimePercent * 100) & "%)" & If(Obj Is Nothing, "", " | " & Obj.ToString & "(" & Obj.GetType.Name & ")")
        End Function

    End Structure

    ''' <summary>
    ''' 动画基础种类。
    ''' </summary>
    Public Enum AniType
        ''' <summary>
        ''' 单个Double的动画，包括位置、长宽、透明度等。这需要附属类型。
        ''' </summary>
        ''' <remarks></remarks>
        Number
        ''' <summary>
        ''' 颜色属性的动画。这需要附属类型。
        ''' </summary>
        ''' <remarks></remarks>
        Color
        ''' <summary>
        ''' 缩放控件大小。比起4个DoubleAnimation来说效率更高。
        ''' </summary>
        ''' <remarks></remarks>
        Scale
        ''' <summary>
        ''' 文字一个个出现。
        ''' </summary>
        ''' <remarks></remarks>
        TextAppear
        ''' <summary>
        ''' 执行代码。
        ''' </summary>
        ''' <remarks></remarks>
        Code
        ''' <summary>
        ''' 以 WPF 方式缩放控件。
        ''' </summary>
        ScaleTransform
        ''' <summary>
        ''' 以 WPF 方式旋转控件。
        ''' </summary>
        RotateTransform
    End Enum
    ''' <summary>
    ''' 动画扩展种类。
    ''' </summary>
    Public Enum AniTypeSub
        X
        Y
        Width
        Height
        Opacity
        Value
        Radius
        BorderThickness
        StrokeThickness
        TranslateX
        TranslateY
        [Double]
        DoubleParam
        GridLengthWidth
    End Enum

#End Region

#Region "种类"

    'DoubleAnimation

    ''' <summary>
    ''' 移动X轴的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">进行移动的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaX(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.X,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 移动Y轴的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">进行移动的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaY(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Y,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变宽度的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">宽度改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaWidth(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Width,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变高度的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">高度改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaHeight(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Height,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变透明度的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">透明度改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaOpacity(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Opacity,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变对象的Value属性的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">Value属性改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaValue(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Value,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变对象的Radius属性的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">Radius属性改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaRadius(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Radius,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变对象的BorderThickness属性的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">BorderThickness属性改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaBorderThickness(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.BorderThickness,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变对象的StrokeThickness属性的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">StrokeThickness属性改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    Public Function AaStrokeThickness(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.StrokeThickness,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 改变 Width 的 GridLength 属性的动画。必须为 Star。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">GridLength.Value 改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    Public Function AaGridLengthWidth(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.GridLengthWidth,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'DoubleAnimation（Obj, Prop, [Res]）

    ''' <summary>
    ''' 改变数字属性的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Prop">动画的依赖属性。</param>
    ''' <param name="Value">改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaDouble(Obj As Object, Prop As DependencyProperty, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Double, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = {Obj, Prop, ""}, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 获取数字动画值。
    ''' </summary>
    ''' <param name="Value">改变的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaDouble(Lambda As ParameterizedThreadStart, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.DoubleParam, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Lambda, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'ColorAnimation（Obj, Prop, [Res]）

    ''' <summary>
    ''' 改变颜色属性的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Prop">动画的依赖属性。</param>
    ''' <param name="Value">颜色改变的值。以RGB加减法进行计算。不用担心超额。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaColor(Obj As FrameworkElement, Prop As DependencyProperty, Value As MyColor, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Color, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = {Obj, Prop, ""}, .Value = Value, .IsAfter = After, .TimeFinished = -Delay, .ValueLast = New MyColor(0, 0, 0, 0)}
    End Function
    ''' <summary>
    ''' 改变颜色属性为一个资源的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Prop">动画的依赖属性。</param>
    ''' <param name="Res">要将颜色改变为该资源值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaColor(Obj As FrameworkElement, Prop As DependencyProperty, Res As String, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Color, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = {Obj, Prop, Res}, .Value = New MyColor(Application.Current.FindResource(Res)) - New MyColor(Obj.GetValue(Prop)), .IsAfter = After, .TimeFinished = -Delay, .ValueLast = New MyColor(0, 0, 0, 0)}
    End Function

    'Scale

    ''' <summary>
    ''' 缩放控件的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">大小改变的百分比（如-0.6）或值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <param name="Absolute">大小改变是否为绝对值。若为 True 则为绝对像素，若为 False 则为相对百分比。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaScale(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False, Optional Absolute As Boolean = False) As AniData
        Dim ChangeRect As MyRect
        If Absolute Then
            ChangeRect = New MyRect(-0.5 * Value, -0.5 * Value, Value, Value)
        Else
            ChangeRect = New MyRect(-0.5 * Obj.ActualWidth * Value, -0.5 * Obj.ActualHeight * Value, Obj.ActualWidth * Value, Obj.ActualHeight * Value)
        End If
        Return New AniData With {.TypeMain = AniType.Scale, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = ChangeRect, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'TextAppear

    ''' <summary>
    ''' 让一段文字一个个字出现或消失的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。必须是Label或TextBlock。</param>
    ''' <param name="Hide">是否为一个个字隐藏。默认为False（一个个字出现）。这些字必须已经存在了。</param>
    ''' <param name="TimePerText">是否采用根据文本长度决定时间的方式。</param>
    ''' <param name="Time">动画长度（毫秒）。若TimePerText为True，这代表每个字所占据的时间。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaTextAppear(Obj As Object, Optional Hide As Boolean = False, Optional TimePerText As Boolean = True, Optional Time As Integer = 70, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        'Are we cool yet？
        Return New AniData With {.TypeMain = AniType.TextAppear, .Ease = If(Ease, New AniEaseLinear), .TimeTotal = If(TimePerText, Time * If(TypeOf Obj Is TextBlock, Obj.Text, Obj.Context.ToString).ToString.Length, Time), .Obj = Obj, .Value = {If(TypeOf Obj Is TextBlock, Obj.Text, Obj.Context.ToString), Hide}, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'Code

    ''' <summary>
    ''' 执行代码。
    ''' </summary>
    ''' <param name="Code">一个ThreadStart。这将会在执行时在主线程调用。</param>
    ''' <param name="Delay">代码延迟执行的时间（毫秒）。</param>
    ''' <param name="After">是否等到以前的动画完成后才执行。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaCode(Code As ThreadStart, Optional Delay As Integer = 0, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Code,
                                   .TimeTotal = 1, .Value = Code, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'ScaleTransform

    ''' <summary>
    ''' 按照 WPF 方式缩放控件的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    ''' <param name="Value">大小改变的百分比（如-0.6）。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaScaleTransform(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.ScaleTransform, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'RotateTransform

    ''' <summary>
    ''' 按照 WPF 方式旋转控件的动画。
    ''' </summary>
    ''' <param name="Obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    ''' <param name="Value">大小改变的百分比（如-0.6）。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function AaRotateTransform(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.RotateTransform, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'TranslateTransform

    ''' <summary>
    ''' 利用 TranslateTransform 移动 X 轴的动画，这不会造成布局更新。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">进行移动的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    Public Function AaTranslateX(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.TranslateX,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' 利用 TranslateTransform 移动 Y 轴的动画，这不会造成布局更新。
    ''' </summary>
    ''' <param name="Obj">动画的对象。</param>
    ''' <param name="Value">进行移动的值。</param>
    ''' <param name="Time">动画长度（毫秒）。</param>
    ''' <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    ''' <param name="Ease">插值器类型。</param>
    ''' <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    Public Function AaTranslateY(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.TranslateY,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    '特殊

    ''' <summary>
    ''' 将一个StackPanel中的各个项目依次显示。
    ''' </summary>
    ''' <remarks></remarks>
    Public Function AaStack(Stack As StackPanel, Optional Time As Integer = 100, Optional Delay As Integer = 25) As List(Of AniData)
        AaStack = New List(Of AniData)
        Dim AniDelay As Integer = 0
        For Each Item In Stack.Children
            Item.Opacity = 0
            AaStack.Add(AaOpacity(Item, 1, Time, AniDelay))
            AniDelay += Delay
        Next
    End Function

#End Region

#Region "缓动函数"

    '基类
    Public Enum AniEasePower As Integer
        Weak = 2
        Middle = 3
        Strong = 4
        ExtraStrong = 5
    End Enum
    ''' <summary>
    ''' 缓动函数基类。
    ''' </summary>
    Public MustInherit Class AniEase
        ''' <summary>
        ''' 获取函数值。
        ''' </summary>
        ''' <param name="t">时间百分比。</param>
        Public MustOverride Function GetValue(t As Double) As Double
        ''' <summary>
        ''' 获取增量值。
        ''' </summary>
        ''' <param name="t1">较大的 X。</param>
        ''' <param name="t0">较小的 X。</param>
        Public Overridable Function GetDelta(t1 As Double, t0 As Double) As Double
            Return GetValue(t1) - GetValue(t0)
        End Function
    End Class
    ''' <summary>
    ''' 渐入渐出组合。
    ''' </summary>
    Public Class AniEaseInout
        Inherits AniEase
        Private ReadOnly EaseIn As AniEase, EaseOut As AniEase, EaseInPercent As Double
        Public Sub New(EaseIn As AniEase, EaseOut As AniEase, Optional EaseInPercent As Double = 0.5)
            Me.EaseIn = EaseIn : Me.EaseOut = EaseOut : Me.EaseInPercent = EaseInPercent
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            If t < EaseInPercent Then
                Return EaseInPercent * EaseIn.GetValue(t / EaseInPercent)
            Else
                Return (1 - EaseInPercent) * EaseOut.GetValue((t - EaseInPercent) / (1 - EaseInPercent)) + EaseInPercent
            End If
        End Function
    End Class

    'Linear / 线性
    ''' <summary>
    ''' 线性，无缓动。
    ''' </summary>
    Public Class AniEaseLinear
        Inherits AniEase
        Public Overrides Function GetValue(t As Double) As Double
            Return MathClamp(t, 0, 1)
        End Function
        Public Overrides Function GetDelta(t1 As Double, t0 As Double) As Double
            Return MathClamp(t1, 0, 1) - MathClamp(t0, 0, 1)
        End Function
    End Class

    'Fluent / 平滑
    ''' <summary>
    ''' 平滑开始。
    ''' </summary>
    Public Class AniEaseInFluent
        Inherits AniEase
        Private ReadOnly p As AniEasePower
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return MathClamp(t, 0, 1) ^ p
        End Function
    End Class
    ''' <summary>
    ''' 平滑结束。
    ''' </summary>
    Public Class AniEaseOutFluent
        Inherits AniEase
        Private ReadOnly p As AniEasePower
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return 1 - MathClamp(1 - t, 0, 1) ^ p
        End Function
    End Class
    ''' <summary>
    ''' 平滑开始与结束。
    ''' </summary>
    Public Class AniEaseInoutFluent
        Inherits AniEase
        Private Ease As AniEaseInout
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle, Optional Middle As Double = 0.5)
            Ease = New AniEaseInout(New AniEaseInFluent(Power), New AniEaseOutFluent(Power), Middle)
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return Ease.GetValue(t)
        End Function
    End Class
    ''' <summary>
    ''' 以特定速度开始的平滑结束。
    ''' </summary>
    Public Class AniEaseOutFluentWithInitial
        Inherits AniEase
        Private ReadOnly alpha As Double '(初速度 / 平均速度) – 1
        ''' <param name="InitialPixelPerSecond">初速度，px/s</param>
        ''' <param name="TotalSecond">总时长，s</param>
        ''' <param name="TotalDistance">总路程，px</param>
        Public Sub New(InitialPixelPerSecond As Double, TotalSecond As Double, TotalDistance As Double)
            Dim v0_norm As Double = InitialPixelPerSecond * TotalSecond / TotalDistance '归一化初速度
            alpha = v0_norm - 1.0
            If alpha < 0 Then alpha = 0 '初速度小于平均速度时，退化为线性
        End Sub
        Public Overrides Function GetValue(percent As Double) As Double
            Dim p As Double = MathClamp(percent, 0, 1)
            If alpha = 0 Then Return p '退化到线性
            Return (alpha + 1) * p / (1 + alpha * p)
        End Function
    End Class

    'Back / 回弹
    ''' <summary>
    ''' 回弹开始。有效时间为 1/3。
    ''' </summary>
    Public Class AniEaseInBack
        Inherits AniEase
        Private ReadOnly p As Double
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = 3 - Power * 0.5
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = MathClamp(t, 0, 1)
            Return t ^ p * Math.Cos(1.5 * Math.PI * (1 - t))
        End Function
    End Class
    ''' <summary>
    ''' 回弹结束。有效时间为 1/3。
    ''' </summary>
    Public Class AniEaseOutBack
        Inherits AniEase
        Private ReadOnly p As Double
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = 3 - Power * 0.5
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = MathClamp(t, 0, 1)
            Return 1 - (1 - t) ^ p * Math.Cos(1.5 * Math.PI * t)
        End Function
    End Class

    'Car / 平滑-回弹
    ''' <summary>
    ''' 回弹开始，短平滑结束。
    ''' </summary>
    Public Class AniEaseInCar
        Inherits AniEase
        Private Ease As AniEaseInout
        Public Sub New(Optional Middle As Double = 0.7, Optional Power As AniEasePower = AniEasePower.Middle)
            Ease = New AniEaseInout(New AniEaseInBack(Power), New AniEaseOutFluent(Power), Middle)
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return Ease.GetValue(t)
        End Function
    End Class
    ''' <summary>
    ''' 短平滑开始，回弹结束。
    ''' </summary>
    Public Class AniEaseOutCar
        Inherits AniEase
        Private Ease As AniEaseInout
        Public Sub New(Optional Middle As Double = 0.3, Optional Power As AniEasePower = AniEasePower.Middle)
            Ease = New AniEaseInout(New AniEaseInFluent(Power), New AniEaseOutBack(Power), Middle)
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return Ease.GetValue(t)
        End Function
    End Class

    'Elastic / 弹簧
    ''' <summary>
    ''' 弹簧开始。约在 60% 到达最小值。
    ''' </summary>
    Public Class AniEaseInElastic
        Inherits AniEase
        Private ReadOnly p As Integer '6~9
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power + 4
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = MathClamp(t, 0, 1)
            Return t ^ ((p - 1) * 0.25) * Math.Cos((p - 3.5) * Math.PI * (1 - t) ^ 1.5)
        End Function
    End Class
    ''' <summary>
    ''' 弹簧结束。约在 40% 到达最大值。
    ''' </summary>
    Public Class AniEaseOutElastic
        Inherits AniEase
        Private ReadOnly p As Integer
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power + 4
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = 1 - MathClamp(t, 0, 1)
            Return 1 - t ^ ((p - 1) * 0.25) * Math.Cos((p - 3.5) * Math.PI * (1 - t) ^ 1.5)
        End Function
    End Class

#End Region

#Region "接口（开始、中断、检测）"

    ''' <summary>
    ''' 开始一个动画组。
    ''' </summary>
    ''' <param name="AniGroup">由 Aa 开头的函数初始化的 AniData 对象集合。</param>
    ''' <param name="Name">动画组的名称。如果重复会直接停止同名动画组。</param>
    Public Sub AniStart(AniGroup As IList, Optional Name As String = "", Optional RefreshTime As Boolean = False)
        If RefreshTime Then AniLastTick = GetTimeTick() '避免处理动画时已经造成了极大的延迟，导致动画突然结束
        '添加到正在执行的动画组
        Dim NewEntry As New AniGroupEntry With {.Data = GetFullList(Of AniData)(AniGroup), .StartTick = GetTimeTick()}
        If Name = "" Then
            Name = NewEntry.Uuid
        Else
            AniStop(Name)
        End If
        AniGroups.Add(Name, NewEntry)
    End Sub
    ''' <summary>
    ''' 开始一个动画组。
    ''' </summary>
    Public Sub AniStart(AniGroup As AniData, Optional Name As String = "", Optional RefreshTime As Boolean = False)
        AniStart(New List(Of AniData) From {AniGroup}, Name, RefreshTime)
    End Sub
    ''' <summary>
    ''' 直接停止一个动画组。
    ''' </summary>
    ''' <param name="name">需要停止的动画组的名称。</param>
    Public Sub AniStop(Name As String)
        AniGroups.Remove(Name)
    End Sub
    ''' <summary>
    ''' 获取动画是否正在进行中。
    ''' </summary>
    Public Function AniIsRun(Name As String) As Boolean
        Return AniGroups.ContainsKey(Name)
    End Function

#End Region

    Private AniCount As Integer = 0
    Private AniFPSCounter As Integer = 0
    Private AniFPSTimer As Long = 0
    ''' <summary>
    ''' 当前的动画 FPS。
    ''' </summary>
    Public AniFPS As Integer = 0

    ''' <summary>
    ''' 开始动画执行。
    ''' </summary>
    Public Sub AniStart()
        '初始化计时器
        AniLastTick = GetTimeTick()
        AniFPSTimer = AniLastTick
        AniRunning = True '标记动画执行开始

        RunInNewThread(Sub()
                           Try
                               Log("[Animation] 动画线程开始")
                               Do While True
                                   '两帧之间的间隔时间
                                   Dim DeltaTime As Long = MathClamp(GetTimeTick() - AniLastTick, 0, 100000)
                                   If DeltaTime < 3 Then GoTo Sleeper
                                   AniLastTick = GetTimeTick()
                                   '记录 FPS
                                   If ModeDebug Then
                                       If MathClamp(AniLastTick - AniFPSTimer, 0, 100000) >= 500 Then
                                           AniFPS = AniFPSCounter
                                           AniFPSCounter = 0
                                           AniFPSTimer = AniLastTick
                                       End If
                                       AniFPSCounter += 2
                                   End If
                                   '执行动画
                                   RunInUiWait(Sub()
                                                   AniCount = 0
                                                   AniTimer(DeltaTime * AniSpeed)
                                                   '#If DEBUG Then
                                                   '    FrmMain.Title = "F " & AniFPS & ", A " & AniCount & ", R " & NetManage.FileRemain
                                                   '#Else
                                                   '    If ModeDebug Then FrmMain.Title = "FPS " & AniFPS & ", 动画 " & AniCount & ", 下载中 " & NetManage.FileRemain
                                                   '#End If
                                                   If RandomInteger(0, 64 * If(ModeDebug, 5, 30)) = 0 AndAlso ((AniFPS < 62 AndAlso AniFPS > 0) OrElse AniCount > 4 OrElse NetManager.FileRemain <> 0) Then
                                                       Log("[Report] FPS " & AniFPS & ", 动画 " & AniCount & ", 下载中 " & NetManager.FileRemain & "（" & GetString(NetManager.Speed) & "/s）")
                                                   End If
                                               End Sub)
Sleeper:
                                   '控制 FPS
                                   Thread.Sleep(1)
                               Loop
                           Catch ex As Exception
                               Log(ex, "动画帧执行失败", LogLevel.Critical)
                           End Try
                       End Sub, "Animation", ThreadPriority.AboveNormal)
    End Sub

    ''' <summary>
    ''' 动画定时器事件。
    ''' </summary>
    Public Sub AniTimer(DeltaTick As Integer)
        Try

            If DeltaTick / AniSpeed > 200 Then Log("[Animation] 两个动画帧间隔 " & DeltaTick & " ms", LogLevel.Developer)
            Dim i As Integer = -1
            '循环每个动画组
            Do While i + 1 < AniGroups.Count
                i += 1
                '初始化
                Dim Entry As AniGroupEntry = AniGroups.Values(i)
                If Entry.StartTick > AniLastTick Then Continue Do '跳过本刻之后开始的动画
                Dim CanRemoveAfter = True '是否应该去除“之后”标记
                Dim ii = 0

                '循环每个动画
                Do While ii < Entry.Data.Count
                    Dim Anim As AniData = Entry.Data(ii)
                    '执行种类
                    If Anim.IsAfter = False Then '之前
                        CanRemoveAfter = False '取消“之后”标记 
                        '增加执行时间
                        Anim.TimeFinished += DeltaTick
                        '执行动画
                        If Anim.TimeFinished > 0 Then
                            Anim = AniRun(Anim)
                            AniCount += 1
                        End If
                        '如果当前动画已执行完毕
                        If Anim.TimeFinished >= Anim.TimeTotal Then
                            '如果是去向颜色资源的动画，设置引用
                            If Anim.TypeMain = AniType.Color AndAlso Not Anim.Obj(2) = "" Then Anim.Obj(0).SetResourceReference(Anim.Obj(1), Anim.Obj(2))
                            '删除
                            Entry.Data.RemoveAt(ii)
                            GoTo NextAni
                        End If
                        Entry.Data(ii) = Anim
                    Else '之后
                        If CanRemoveAfter Then
                            '之后改为之前
                            CanRemoveAfter = False
                            Anim.IsAfter = False
                            Entry.Data(ii) = Anim
                            '重新循环该动画
                            GoTo NextAni
                        Else
                            '不能去除该“之后”标记，结束该动画组
                            Exit Do
                        End If
                    End If
                    ii += 1
NextAni:
                Loop

                '如果当前动画组都执行完毕则删除
                If Not Entry.Data.Any() Then
                    '为了避免新添加的动画影响顺序，不能 RemoveAt(i)
                    '为了允许动画在执行中添加同名动画组，不能按名字移除
                    For Current = 0 To AniGroups.Count - 1
                        If AniGroups.ElementAt(Current).Value.Uuid = Entry.Uuid Then
                            AniGroups.Remove(AniGroups.ElementAt(Current).Key)
                            Exit For
                        End If
                    Next
                    i -= 1
                End If
            Loop

        Catch ex As Exception
            Log(ex, "动画刻执行失败", LogLevel.Hint)
        End Try
    End Sub

    ''' <summary>
    ''' 执行一个动画。
    ''' </summary>
    ''' <param name="Ani">执行的动画对象。</param>
    Private Function AniRun(Ani As AniData) As AniData
        Try
            Select Case Ani.TypeMain

                Case AniType.Number
                    Dim Delta As Double = MathPercent(0, Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent))
                    If Delta <> 0 Then
                        Select Case Ani.TypeSub
                            Case AniTypeSub.X
                                DeltaLeft(Ani.Obj, Delta)
                            Case AniTypeSub.Y
                                DeltaTop(Ani.Obj, Delta)
                            Case AniTypeSub.Opacity
                                Ani.Obj.Opacity = MathClamp(Ani.Obj.Opacity + Delta, 0, 1)
                            Case AniTypeSub.Width
                                Dim Obj As FrameworkElement = Ani.Obj
                                Obj.Width = Math.Max(If(Double.IsNaN(Obj.Width), Obj.ActualWidth, Obj.Width) + Delta, 0)
                            Case AniTypeSub.Height
                                Dim Obj As FrameworkElement = Ani.Obj
                                Obj.Height = Math.Max(If(Double.IsNaN(Obj.Height), Obj.ActualHeight, Obj.Height) + Delta, 0)
                            Case AniTypeSub.Value
                                Ani.Obj.Value += Delta
                            Case AniTypeSub.Radius
                                Ani.Obj.Radius += Delta
                            Case AniTypeSub.StrokeThickness
                                Ani.Obj.StrokeThickness = Math.Max(Ani.Obj.StrokeThickness + Delta, 0)
                            Case AniTypeSub.BorderThickness
                                Ani.Obj.BorderThickness = New Thickness(CType(Ani.Obj.BorderThickness, Thickness).Bottom + Delta)
                            Case AniTypeSub.TranslateX
                                If IsNothing(Ani.Obj.RenderTransform) OrElse TypeOf Ani.Obj.RenderTransform IsNot TranslateTransform Then Ani.Obj.RenderTransform = New TranslateTransform(0, 0)
                                CType(Ani.Obj.RenderTransform, TranslateTransform).X += Delta
                            Case AniTypeSub.TranslateY
                                If IsNothing(Ani.Obj.RenderTransform) OrElse TypeOf Ani.Obj.RenderTransform IsNot TranslateTransform Then Ani.Obj.RenderTransform = New TranslateTransform(0, 0)
                                CType(Ani.Obj.RenderTransform, TranslateTransform).Y += Delta
                            Case AniTypeSub.Double
                                Ani.Obj(0).SetValue(Ani.Obj(1), Ani.Obj(0).GetValue(Ani.Obj(1)) + Delta)
                            Case AniTypeSub.DoubleParam
                                CType(Ani.Obj, ParameterizedThreadStart)(Delta)
                            Case AniTypeSub.GridLengthWidth
                                Ani.Obj.Width = New GridLength(Math.Max(Ani.Obj.Width.Value + Delta, 0), GridUnitType.Star)
                        End Select
                    End If

                Case AniType.Color
                    '利用 Last 记录了余下的小数值
                    Dim Delta As MyColor = MathPercent(New MyColor(0, 0, 0, 0), Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent)) + Ani.ValueLast
                    Dim Obj As FrameworkElement = Ani.Obj(0)
                    Dim Prop As DependencyProperty = Ani.Obj(1)
                    Dim NewColor As MyColor = New MyColor(Obj.GetValue(Prop)) + Delta
                    Obj.SetValue(Prop, If(Prop.PropertyType.Name = "Color", CType(NewColor, Color), CType(NewColor, SolidColorBrush)))
                    Ani.ValueLast = NewColor - New MyColor(Obj.GetValue(Prop))

                Case AniType.Scale
                    Dim Obj As FrameworkElement = Ani.Obj
                    Dim Delta As Double = Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent)
                    Obj.Margin = New Thickness(Obj.Margin.Left + MathPercent(0, Ani.Value.Left, Delta), Obj.Margin.Top + MathPercent(0, Ani.Value.Top, Delta), Obj.Margin.Right + MathPercent(0, Ani.Value.Left, Delta), Obj.Margin.Bottom + MathPercent(0, Ani.Value.Top, Delta))
                    Obj.Width = Math.Max(Obj.Width + MathPercent(0, Ani.Value.Width, Delta), 0)
                    Obj.Height = Math.Max(Obj.Height + MathPercent(0, Ani.Value.Height, Delta), 0)

                Case AniType.TextAppear
                    Dim TextCount As Integer = If(Ani.Value(1), Ani.Value(0).ToString.Length, 0) +
                                               Math.Round(Ani.Value(0).ToString.Length * If(Ani.Value(1), -1, 1) * Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, 0))
                    Dim NewText As String = Mid(Ani.Value(0), 1, TextCount)
                    '添加乱码
                    If TextCount < Ani.Value(0).ToString.Length Then
                        Dim NextText As String = Mid(Ani.Value(0), TextCount + 1, 1)
                        If Convert.ToInt32(Convert.ToChar(NextText)) >= Convert.ToInt32(Convert.ToChar(128)) Then
                            NewText &= Encoding.GetEncoding("GB18030").GetString({RandomInteger(16 + 160, 87 + 160), RandomInteger(1 + 160, 89 + 160)})
                        Else
                            NewText &= RandomOne("0123456789./*-+\[]{};':/?,!@#$%^&*()_+-=qwwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM".ToCharArray)
                        End If
                    End If
                    '设置文本
                    If TypeOf Ani.Obj Is TextBlock Then
                        Ani.Obj.Text = NewText
                    Else
                        Ani.Obj.Context = NewText
                    End If

                Case AniType.Code
                    CType(Ani.Value, ThreadStart)()

                Case AniType.ScaleTransform
                    Dim Obj As FrameworkElement = Ani.Obj
                    If TypeOf Obj.RenderTransform IsNot ScaleTransform Then
                        Obj.RenderTransformOrigin = New Point(0.5, 0.5)
                        Obj.RenderTransform = New ScaleTransform(1, 1)
                    End If
                    Dim Delta As Double = MathPercent(0, Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent))
                    CType(Obj.RenderTransform, ScaleTransform).ScaleX = Math.Max(CType(Obj.RenderTransform, ScaleTransform).ScaleX + Delta, 0)
                    CType(Obj.RenderTransform, ScaleTransform).ScaleY = Math.Max(CType(Obj.RenderTransform, ScaleTransform).ScaleY + Delta, 0)

                Case AniType.RotateTransform
                    Dim Obj As FrameworkElement = Ani.Obj
                    If TypeOf Obj.RenderTransform IsNot RotateTransform Then
                        Obj.RenderTransformOrigin = New Point(0.5, 0.5)
                        Obj.RenderTransform = New RotateTransform(0)
                    End If
                    Dim Delta As Double = MathPercent(0, Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent))
                    CType(Obj.RenderTransform, RotateTransform).Angle = CType(Obj.RenderTransform, RotateTransform).Angle + Delta

            End Select
            Ani.TimePercent = Ani.TimeFinished / Ani.TimeTotal '修改执行百分比
        Catch ex As Exception
            Log(ex, "执行动画失败：" & Ani.ToString, LogLevel.Hint)
        End Try
        Return Ani
    End Function

End Module
