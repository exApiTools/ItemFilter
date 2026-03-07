using ExileCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Linq.Expressions;

namespace ItemFilterLibrary;

public class ItemQuery<T> where T : ItemData
{
    public string Query { get; set; }
    public string RawQuery { get; set; }
    public Func<T, bool> CompiledQuery { get; set; }
    public int InitialLine { get; set; }
    public bool FailedToCompile => Error != null;
    public string Error { get; set; }

    public override string ToString()
    {
        return $"InitialLine({InitialLine}) Query({Query.Replace("\n", "")}) RawQuery({RawQuery.Replace("\n", "")}) Failed?({FailedToCompile})";
    }

    public bool Matches(T item)
    {
        return Matches(item, false);
    }

    public bool Matches(T item, bool enableDebug)
    {
        if (item?.Entity?.IsValid != true || FailedToCompile)
            return false;

        try
        {
            if (CompiledQuery(item))
            {
                if (enableDebug)
                    DebugWindow.LogMsg($"[ItemQueryProcessor] {RawQuery} matched item {item.BaseName}", 10);
                return true;
            }
        }
        catch (Exception ex)
        {
            DebugWindow.LogError($"[ItemQueryProcessor] Evaluation error for query {RawQuery}. Item {item?.BaseName ?? "null"}\n{ex}");
            return false;
        }

        return false;
    }
}

public class ItemQuery : ItemQuery<ItemData>
{
    private static readonly ParsingConfig ParsingConfig = new ParsingConfig()
    {
        AllowNewToEvaluateAnyType = true,
        ResolveTypesBySimpleName = true,
        CustomTypeProvider = new CustomDynamicLinqCustomTypeProvider(),
    };

    public static ItemQuery Load(string query) => Load(query, query, 0);

    public static ItemQuery<T> Load<T>(string query) where T : ItemData
    {
        return Load<T>(query, query, 0);
    }

    public static ItemQuery Load(string query, string rawQuery, int line)
    {
        var generic = Load<ItemData>(query, rawQuery, line);
        return new ItemQuery
        {
            RawQuery = generic.RawQuery,
            CompiledQuery = generic.CompiledQuery,
            InitialLine = generic.InitialLine,
            Query = generic.Query,
            Error = generic.Error,
        };
    }

    public static ItemQuery<T> Load<T>(string query, string rawQuery, int line) where T : ItemData
    {
        try
        {
            var lambda = ParseItemDataLambda<T>(query);
            var compiledLambda = lambda.Compile();

            return new ItemQuery<T>
            {
                Query = query,
                RawQuery = rawQuery,
                CompiledQuery = compiledLambda,
                InitialLine = line
            };
        }
        catch (Exception ex)
        {
            var exMessage = ex is ParseException parseEx
                ? $"{parseEx.Message} (at index {parseEx.Position})"
                : ex.ToString();

            DebugWindow.LogError($"[ItemQueryProcessor] Error processing query ({rawQuery}) on Line # {line}: {exMessage}", 15);

            return new ItemQuery<T>
            {
                Query = query,
                RawQuery = rawQuery,
                CompiledQuery = null,
                InitialLine = line,
                Error = exMessage, // to use with stashie to output the same number of inputs and match up the syntax style correctly
            };
        }
    }

    private static Expression<Func<T, bool>> ParseItemDataLambda<T>(string expression) where T : ItemData
    {
        return DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig, false, expression);
    }
}

public class ItemFilter : ItemFilter<ItemData>
{
    public ItemFilter(ItemFilter<ItemData> genericFilter) : base(genericFilter.Queries.ToList())
    {
    }

    public ItemFilter(List<ItemQuery> queries) : base(queries.Cast<ItemQuery<ItemData>>().ToList())
    {
    }

    public ItemFilter(List<(ItemQuery, bool isNegative)> queries) : base(queries.Select(x => ((ItemQuery<ItemData>)x.Item1, x.isNegative)).ToList())
    {
    }

    private static List<(ItemQuery<T>, bool isNegative)> GetQueries<T>(string filterFilePath, string[] rawLines) where T : ItemData
    {
        var compiledQueries = new List<(ItemQuery<T>, bool isNegative)>();
        var lines = SplitQueries(rawLines);

        foreach (var (query, rawQuery, initialLine, isNegative) in lines)
        {
            compiledQueries.Add((ItemQuery.Load<T>(query, rawQuery, initialLine), isNegative));
        }

        DebugWindow.LogMsg($@"[ItemQueryProcessor] Processed {filterFilePath.Split("\\").LastOrDefault()} with {compiledQueries.Count} queries", 2);
        return compiledQueries;
    }

    private static List<(string section, string rawSection, int sectionStartLine, bool isNegative)> SplitQueries(string[] rawLines)
    {
        string section = null;
        string rawSection = null;
        bool isNegative = false;
        var sectionStartLine = 0;
        var lines = new List<(string section, string, int sectionStartLine, bool isNegative)>();

        foreach (var (line, index) in rawLines.Append("").Select((value, i) => (value, i)))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                var lineWithoutComment = line.IndexOf("//", StringComparison.Ordinal) is var commentIndex and not -1
                    ? line[..commentIndex]
                    : line;
                lineWithoutComment = lineWithoutComment.Trim();
                if (section == null)
                {
                    if (!string.IsNullOrWhiteSpace(lineWithoutComment))
                    {
                        sectionStartLine = index + 1; // Set at the start of each section
                        if (lineWithoutComment[0] == '^')
                        {
                            lineWithoutComment = lineWithoutComment[1..];
                            isNegative = true;
                        }
                    }
                    else
                    {
                        //skip comment lines at the beginning of a section
                        continue;
                    }
                }

                section += $"{lineWithoutComment}\n";
                rawSection += $"{line}\n";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(section))
                {
                    lines.Add((section, rawSection.TrimEnd('\n'), sectionStartLine, isNegative));
                }

                section = null;
                rawSection = null;
                isNegative = false;
            }
        }

        return lines;
    }

    public static ItemFilter LoadFromPath(string filterFilePath) => new ItemFilter(LoadFromPath<ItemData>(filterFilePath));
    public static ItemFilter<T> LoadFromPath<T>(string filterFilePath) where T : ItemData
    {
        return new ItemFilter<T>(GetQueries<T>(filterFilePath, File.ReadAllLines(filterFilePath)));
    }

    public static ItemFilter LoadFromList(string filterName, IEnumerable<string> list) => new ItemFilter(LoadFromList<ItemData>(filterName, list));
    public static ItemFilter<T> LoadFromList<T>(string filterName, IEnumerable<string> list) where T : ItemData
    {
        var compiledQueries = list.Select((query, i) => ItemQuery.Load<T>(query, query, i + 1)).ToList();
        DebugWindow.LogMsg($@"[ItemQueryProcessor] Processed {filterName.Split("\\").LastOrDefault()} with {compiledQueries.Count} queries", 2);
        return new ItemFilter<T>(compiledQueries);
    }

    public static ItemFilter LoadFromString(string @string) => new ItemFilter(LoadFromString<ItemData>(@string));
    public static ItemFilter<T> LoadFromString<T>(string @string) where T : ItemData
    {
        return new ItemFilter<T>(GetQueries<T>("memory", @string.ReplaceLineEndings("\n").Split("\n")));
    }
}

public class ItemFilter<T> where T : ItemData
{
    private readonly List<(ItemQuery<T> Query, bool IsNegative)> _queries;

    public IReadOnlyCollection<(ItemQuery<T> Query, bool IsNegative)> Queries => _queries;

    public ItemFilter(List<ItemQuery<T>> queries)
    {
        _queries = queries.Select(x=>(x, false)).ToList();
    }
    
    public ItemFilter(List<(ItemQuery<T>, bool isNegative)> queries)
    {
        _queries = queries;
    }

   public bool Matches(T item)
    {
        return Matches(item, false);
    }

    public bool Matches(T item, bool enableDebug)
    {
        if (item?.Entity?.IsValid != true)
            return false;

        foreach (var (query, isNegative) in _queries)
        {
            try
            {
                if (!query.FailedToCompile && query.CompiledQuery(item))
                {
                    if (enableDebug)
                        DebugWindow.LogMsg($"[ItemQueryProcessor] Matches an Item\nLine # {query.InitialLine}\nItem({item.BaseName})\n{query.RawQuery}", 10);

                    return !isNegative;
                }
            }
            catch (Exception ex)
            {
                // huge issue when the amount of catching starts creeping up
                // 4500 lines that produce an error on one item take 50ms per Tick() vs handling the error taking 0.2ms
                DebugWindow.LogError($"Evaluation Error! Line # {query.InitialLine} Entry: '{query.RawQuery}' Item {item?.BaseName ?? "null"}\n{ex}");
            }
        }

        return false;
    }
}