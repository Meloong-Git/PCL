'获取所有条目：WikiEntry.All

''' <summary>
''' MC 百科条目。
''' </summary>
Public Class WikiEntry

    '属性

    ''' <summary>
    ''' 在 MC 百科中的对应 ID。
    ''' </summary>
    Public Id As Integer
    ''' <summary>
    ''' 中文译名。空字符串代表没有翻译。
    ''' </summary>
    Public ChineseName As String = ""
    ''' <summary>
    ''' CurseForge Slug（例如 advanced-solar-panels）。
    ''' </summary>
    Public CurseForgeSlug As String = Nothing
    ''' <summary>
    ''' Modrinth Slug（例如 advanced-solar-panels）。
    ''' </summary>
    Public ModrinthSlug As String = Nothing
    ''' <summary>
    ''' MC 百科的浏览量逆序排行，1 代表浏览量最低。
    ''' </summary>
    Public Popularity As Integer
    Public Overrides Function ToString() As String
        Return If(CurseForgeSlug, "") & "&" & If(ModrinthSlug, "") & "|" & Id & "|" & ChineseName & ", Rank " & Popularity
    End Function

    '读取

    ''' <summary>
    ''' 内置数据库中的所有 MC 百科条目。
    ''' </summary>
    Public Shared ReadOnly Property All As List(Of WikiEntry)
        Get
            Static Cache As List(Of WikiEntry) = Nothing
            If Cache IsNot Nothing Then Return Cache
            '实际加载
            Cache = New List(Of WikiEntry)
            Dim Data As String
            Using Archive As New ZipArchive(New MemoryStream(GetResources("ModData")), ZipArchiveMode.Read)
                Data = ReadFile(Archive.GetEntry("moddata.txt").Open(), Encoding.UTF8)
            End Using
            Dim i As Integer = 0
            For Each Line In Data.ReplaceLineEndings(vbLf).Split(vbLf)
                i += 1
                If Line = "" Then Continue For
                For Each EntryData As String In Line.Split("¨")
                    Dim Entry = New WikiEntry
                    Dim Splited = EntryData.Split("|")
                    Entry.Popularity = Val(Splited(0))
                    If Splited(1).StartsWithF("@") Then
                        Entry.CurseForgeSlug = Nothing
                        Entry.ModrinthSlug = Splited(1).Replace("@", "")
                    ElseIf Splited(1).EndsWithF("@") Then
                        Entry.CurseForgeSlug = Splited(1).TrimEnd("@")
                        Entry.ModrinthSlug = Entry.CurseForgeSlug
                    ElseIf Splited(1).Contains("@") Then
                        Entry.CurseForgeSlug = Splited(1).Split("@")(0)
                        Entry.ModrinthSlug = Splited(1).Split("@")(1)
                    Else
                        Entry.CurseForgeSlug = Splited(1)
                        Entry.ModrinthSlug = Nothing
                    End If
                    Entry.Id = i
                    If Splited.Count >= 3 Then
                        Entry.ChineseName = Splited.Last
                        If Entry.ChineseName.Contains("*") Then '处理 *
                            Entry.ChineseName = Entry.ChineseName.Replace("*",
                                    $" ({If(Entry.CurseForgeSlug, Entry.ModrinthSlug).Replace("-", " ").Capitalize})")
                        End If
                    End If
                    Cache.Add(Entry)
                Next
            Next
            Return Cache
        End Get
    End Property

End Class
