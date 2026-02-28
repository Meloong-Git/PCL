Imports System.Globalization
Imports System.Windows.Forms

Module Modi18n
    ''' <summary>
    ''' 获取译文，如果支持可在键值后方加上需要替换原文占位符（<c>{0}, {1}</c>等）的文本。
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <param name="Key">键值</param>
    ''' <param name="Param">字段中对应要展示的内容</param>
    ''' <returns>键值所对应的译文</returns>
    Public Function GetLang(Key As String, ParamArray Param As String()) As String
        Try
            If String.IsNullOrWhiteSpace(Key) Then Throw New Exception("Key 值未提供;No key value provided")
            Return String.Format(Application.Current.FindResource(Key), Param)
        Catch ex As FormatException
            Log(ex, $"[Location] 格式化文本失败：{Key};传入参数：{Param.Join(",")}", LogLevel.Hint)
            Return Application.Current.FindResource(Key)
        Catch ex As ResourceReferenceKeyNotFoundException
            Log(ex, $"[Location] 找不到对应的语言资源：{Key}")
            Return Key
        Catch ex As Exception
            Log(ex, $"[Location] 获取语言资源失败：{Key}（{ex.Message}）", LogLevel.Hint)
            Return Key
        End Try
    End Function

    ''' <summary>
    ''' 通过中文得到其它语言，应付一些 i18n 适配较困难的场景
    ''' </summary>
    ''' <param name="Word">中文文本</param>
    ''' <returns>对应语言的译文</returns>
    Public Function GetLangByWord(Word As String) As String
        If Lang = "zh-CN" Then Return Word '语言设置为中文时不需要处理，直接返回以节约处理时间
        Select Case Word
            Case "正式版"
                Return GetLang("LangDownloadRelease")
            Case "预览版", "快照版本"
                Return GetLang("LangDownloadBeta")
            Case "远古版", "远古版本"
                Return GetLang("LangDownloadAncientVersion")
            Case "愚人节版"
                Return GetLang("LangDownloadAprilFool")
            Case "未知版本"
                Return GetLang("LangDownloadUnknown")
            Case Else
                '这里不要输出未找到日志
                Return Word
        End Select
    End Function

    ''' <summary>
    ''' 获取当前语言的时间表达方式
    ''' </summary>
    ''' <param name="Time">时间</param>
    ''' <returns>当前地区的时间格式字符串</returns>
    Public Function GetLocalTimeFormat(Time As DateTime) As String
        Select Case Lang
            Case "ja-JP", "ko-KR", "lzh", "zh-CN", "zh-HK", "zh-MARS", "zh-MEME", "zh-TW" '2024/08/16 11:47
                Return Time.ToString("yyyy'/'MM'/'dd HH':'mm")
            Case "en-US" '11:47 08/16/2024
                Return Time.ToString("MM'/'dd'/'yyyy HH':'mm")
            Case Else '11:47 16/08/2024
                Return Time.ToString("dd'/'MM'/'yyyy HH':'mm")
        End Select
    End Function

    ''' <summary>
    ''' 切换 PCL 界面的字体
    ''' </summary>
    ''' <param name="Font">字体</param>
    Public Sub SwitchApplicationFont(Font As FontFamily)
        Try
            Application.Current.Resources("LaunchFontFamily") = Font
        Catch ex As Exception
            Log(ex, "[Location] 切换字体失败，这可能导致界面显示异常", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 地区检测缓存
    ''' -1 未检测
    ''' 0 非中国大陆
    ''' 1 中国大陆
    ''' </summary>
    Private _IsLocationZH As Integer = -1

    ''' <summary>
    ''' 地区是否为中国大陆
    ''' 君子协议
    ''' </summary>
    ''' <returns></returns>
    Public Function IsLocationZH() As Boolean
        'Return False '测试使用
        If Not _IsLocationZH.Equals(-1) Then Return _IsLocationZH.Equals(1)
        Dim IsZH As Boolean = CultureInfo.CurrentCulture.Name.Equals("zh-CN") '语言检测
        IsZH = IsZH And CultureInfo.CurrentUICulture.Name.Equals("zh-CN") '语言检测
        IsZH = IsZH And RegionInfo.CurrentRegion.ISOCurrencySymbol.Equals("CNY") '货币类型是否为 CNY
        IsZH = IsZH And TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).Equals(New TimeSpan(8, 0, 0)) '时区检测
        IsZH = IsZH And InputLanguage.InstalledInputLanguages.OfType(Of InputLanguage).ToList().Any(Function(i) i.Culture.Name.Equals("zh-CN")) '是否存在中文输入法
        _IsLocationZH = If(IsZH, 1, 0)
        Return IsZH
    End Function

    ''' <summary>
    ''' 获取当前系统的默认语言
    ''' </summary>
    ''' <returns>返回类似于 zh-CN 这样形式的文本</returns>
    Public Function GetDefaultLang() As String
        Dim CurrentCulture As String = CultureInfo.CurrentUICulture.Name
        Dim PrefixMap As New Dictionary(Of String, String) From {
            {"el-", "el-GR"},
            {"es-", "es-ES"},
            {"fr-", "fr-FR"},
            {"ja-", "ja-JP"},
            {"ko-", "ko-KR"},
            {"ru-", "ru-RU"},
            {"sk-", "sk-SK"}
        }

        For Each prefixPair In PrefixMap
            If CurrentCulture.StartsWith(prefixPair.Key) Then
                Return prefixPair.Value
            End If
        Next

        Select Case CurrentCulture '部分需要特殊匹配的语言
            Case "en-GB", "en-NZ", "en-AU", "en-CA"
                Return "en-GB"
            Case "zh-CN", "zh-SG", "zh-Hans"
                Return "zh-CN"
            Case "zh-HK", "zh-MO"
                Return "zh-HK"
            Case "zh-TW", "zh-Hant"
                Return "zh-TW"
        End Select

        Return "en-US" '无匹配则返回 en_us
    End Function

    ''' <summary>
    ''' 格式化本地化的数字描述
    ''' </summary>
    ''' <param name="Num">数量</param>
    ''' <returns>11 Million、2 万等这样的表示</returns>
    Public Function GetLocationNum(Num As Int32) As String
        Select Case Lang
            Case "ja-JP", "ko-KR", "lzh", "zh-CN", "zh-HK", "zh-MARS", "zh-MEME", "zh-TW"
                Return If(Num > 1000000000000, Math.Round(Num / 1000000000000, 2) & " " & GetLang("LangModCompModDigit3"), '兆
                If(Num > 100000000, Math.Round(Num / 100000000, 2) & " " & GetLang("LangModCompModDigit2"), '亿
                If(Num > 100000, Math.Round(Num / 10000, 0) & " " & GetLang("LangModCompModDigit1"), Num.ToString("N0") & " "))) '万
            Case Else 'en-US, en-GB, fr-FR etc.
                Return If(Num > 1000000000, Math.Round(Num / 1000000000, 2) & GetLang("LangModCompModDigit3"), 'Billion
                If(Num > 1000000, Math.Round(Num / 1000000, 2) & GetLang("LangModCompModDigit2"), 'Million
                If(Num > 10000, Math.Round(Num / 1000, 0) & GetLang("LangModCompModDigit1"), Num.ToString("N0")))) 'Thousand(K)
        End Select
    End Function

    ''' <summary>
    ''' 根据当前数量判断使用单数或复数形式，提供的键名需有对应加P的复数形式于语言文件
    ''' </summary>
    ''' <param name="Count">数量</param>
    ''' <param name="Key">调用的键名</param>
    ''' <param name="Param">字段中对应要展示的内容</param>
    ''' <returns>对应单复数形式的译文</returns>
    Public Function GetLangByNumIsPlural(Count As Int32, Key As String, ParamArray Param As String()) As String
        If Count <= 1 Then
            Return GetLang(Key, Param)
        Else
            Return GetLang(Key & "P", Param)
        End If
    End Function

End Module
