using Spectre.Console;
using SqlWarehouseNet.Services;

namespace SqlWarehouseNet.CLI;

public class InteractiveShell
{
    private readonly SqlCompletionService _completionService;

    public InteractiveShell(SqlCompletionService completionService)
    {
        _completionService = completionService;
    }

    public string ReadLineWithHistory(List<string> history, HashSet<string> tablesCache, HashSet<string> schemasCache)
    {
        var input = new List<char>();
        int cursorPosition = 0;
        int historyIndex = history.Count;
        string? tempInput = null;
        
        int startLeft = Console.CursorLeft;
        int startTop = Console.CursorTop;
        int lastObservedLines = 1;
        
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            bool isExecutionTriggered = (key.Key == ConsoleKey.Enter && (key.Modifiers & ConsoleModifiers.Control) != 0);
            if (key.KeyChar == 10) isExecutionTriggered = true;
            if (key.Key == ConsoleKey.Enter && input.Count > 0 && input[0] == '/') isExecutionTriggered = true;

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    Console.WriteLine();
                    AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");
                    Environment.Exit(0);
                    break;
                    
                case ConsoleKey.Enter:
                    if (isExecutionTriggered)
                    {
                        MoveCursorToLineEnd(startLeft, startTop, input);
                        Console.WriteLine();
                        return new string(input.ToArray());
                    }
                    else
                    {
                        input.Insert(cursorPosition, '\n');
                        cursorPosition++;
                        lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                    }
                    break;
                    
                case ConsoleKey.UpArrow:
                    if (history.Count > 0 && historyIndex > 0)
                    {
                        if (historyIndex == history.Count) tempInput = new string(input.ToArray());
                        historyIndex--;
                        input.Clear();
                        input.AddRange(history[historyIndex]);
                        cursorPosition = input.Count;
                        lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                    }
                    break;
                    
                case ConsoleKey.DownArrow:
                    if (historyIndex < history.Count)
                    {
                        historyIndex++;
                        input.Clear();
                        if (historyIndex == history.Count) input.AddRange(tempInput ?? "");
                        else input.AddRange(history[historyIndex]);
                        cursorPosition = input.Count;
                        lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                    }
                    break;
                    
                case ConsoleKey.LeftArrow:
                    if (cursorPosition > 0)
                    {
                        if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                        {
                            var separators = new[] { ' ', '\n', '\t', '.', ',', '(', ')', '=', '+', '-', '*', '/', '<', '>', '[', ']' };
                            int wordStart = cursorPosition - 1;
                            while (wordStart >= 0 && separators.Contains(input[wordStart])) wordStart--;
                            while (wordStart >= 0 && !separators.Contains(input[wordStart])) wordStart--;
                            wordStart++;
                            cursorPosition = wordStart;
                        }
                        else cursorPosition--;
                        MoveCursor(startLeft, startTop, input, cursorPosition);
                    }
                    break;
                    
                case ConsoleKey.RightArrow:
                    if (cursorPosition < input.Count)
                    {
                        if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                        {
                            var separators = new[] { ' ', '\n', '\t', '.', ',', '(', ')', '=', '+', '-', '*', '/', '<', '>', '[', ']' };
                            int wordEnd = cursorPosition;
                            while (wordEnd < input.Count && separators.Contains(input[wordEnd])) wordEnd++;
                            while (wordEnd < input.Count && !separators.Contains(input[wordEnd])) wordEnd++;
                            cursorPosition = wordEnd;
                        }
                        else cursorPosition++;
                        MoveCursor(startLeft, startTop, input, cursorPosition);
                    }
                    break;
                    
                case ConsoleKey.Backspace:
                    if (cursorPosition > 0)
                    {
                        if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                        {
                            var separators = new[] { ' ', '\n', '\t', '.', ',', '(', ')', '=', '+', '-', '*', '/', '<', '>', '[', ']' };
                            int wordStart = cursorPosition - 1;
                            while (wordStart >= 0 && separators.Contains(input[wordStart])) wordStart--;
                            while (wordStart >= 0 && !separators.Contains(input[wordStart])) wordStart--;
                            wordStart++;
                            int charsToDelete = cursorPosition - wordStart;
                            if (charsToDelete > 0)
                            {
                                input.RemoveRange(wordStart, charsToDelete);
                                cursorPosition = wordStart;
                            }
                        }
                        else
                        {
                            input.RemoveAt(cursorPosition - 1);
                            cursorPosition--;
                        }
                        lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                    }
                    break;
                    
                case ConsoleKey.Delete:
                    if (cursorPosition < input.Count)
                    {
                        input.RemoveAt(cursorPosition);
                        lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                    }
                    break;

                case ConsoleKey.Home:
                    cursorPosition = 0;
                    MoveCursor(startLeft, startTop, input, cursorPosition);
                    break;

                case ConsoleKey.End:
                    cursorPosition = input.Count;
                    MoveCursor(startLeft, startTop, input, cursorPosition);
                    break;

                case ConsoleKey.Tab:
                    var currentInput = new string(input.Take(cursorPosition).ToArray()).Trim();
                    if (string.IsNullOrEmpty(currentInput)) break;
                    
                    if (currentInput.StartsWith("/"))
                    {
                        var commands = new[] { "/catalogs ", "/schemas ", "/tables ", "/export ", "/profile ", "/help", "/clear", "/quit" };
                        var match = commands.FirstOrDefault(c => c.StartsWith(currentInput, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            var completion = match.Substring(currentInput.Length);
                            input.InsertRange(cursorPosition, completion.ToCharArray());
                            cursorPosition += completion.Length;
                            lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                        }
                    }
                    else
                    {
                        var lastWord = ExtractLastWord(currentInput);
                        var sqlSuggestions = _completionService.GetSqlSuggestions(currentInput, lastWord, tablesCache, schemasCache);
                        
                        if (sqlSuggestions.Count > 0)
                        {
                            var match = sqlSuggestions.FirstOrDefault(s => s.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                int wordStartInInput = cursorPosition - lastWord.Length;
                                input.RemoveRange(wordStartInInput, lastWord.Length);
                                input.InsertRange(wordStartInInput, match.ToCharArray());
                                cursorPosition = wordStartInInput + match.Length;
                                lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                            }
                        }
                    }
                    break;
                    
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        input.Insert(cursorPosition, key.KeyChar);
                        cursorPosition++;
                        lastObservedLines = RefreshLine(startLeft, ref startTop, input, cursorPosition, lastObservedLines);
                    }
                    break;
            }
        }
    }

    private string ExtractLastWord(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var trimmedInput = input.TrimEnd();
        int lastSpaceIndex = trimmedInput.LastIndexOfAny(new[] { ' ', '\n', '\t', '(', ')', ',', '=' });
        return lastSpaceIndex == -1 ? trimmedInput : trimmedInput.Substring(lastSpaceIndex + 1);
    }

    private int GetLineCount(int startLeft, List<char> input)
    {
        int currentLineLength = startLeft;
        int lineCount = 1;
        foreach (var c in input)
        {
            if (c == '\n') { lineCount++; currentLineLength = 0; }
            else { currentLineLength++; if (currentLineLength >= Console.WindowWidth) { lineCount++; currentLineLength = 0; } }
        }
        return lineCount;
    }

    private int RefreshLine(int startLeft, ref int startTop, List<char> input, int cursorPosition, int prevLines)
    {
        if (startTop >= Console.BufferHeight) startTop = Console.BufferHeight - 1;

        for (int i = 0; i < prevLines; i++)
        {
            int targetTop = startTop + i;
            if (targetTop >= 0 && targetTop < Console.BufferHeight)
            {
                if (i == 0)
                {
                    Console.SetCursorPosition(startLeft, targetTop);
                    int charsToErase = Math.Max(0, Console.WindowWidth - startLeft);
                    Console.Write(new string(' ', charsToErase));
                }
                else
                {
                    Console.SetCursorPosition(0, targetTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                }
            }
        }
        
        if (startTop < 0) startTop = 0;
        Console.SetCursorPosition(startLeft, startTop);
        foreach (var c in input) Console.Write(c);

        int newLines = GetLineCount(startLeft, input);
        MoveCursor(startLeft, startTop, input, cursorPosition);
        return newLines;
    }

    private void MoveCursor(int startLeft, int startTop, List<char> input, int cursorPosition)
    {
        int currentLeft = startLeft;
        int currentTop = startTop;
        for (int i = 0; i < cursorPosition; i++)
        {
            if (input[i] == '\n') { currentTop++; currentLeft = 0; }
            else { currentLeft++; if (currentLeft >= Console.WindowWidth) { currentTop++; currentLeft = 0; } }
        }
        if (currentTop >= Console.BufferHeight) currentTop = Console.BufferHeight - 1;
        if (currentTop < 0) currentTop = 0;
        Console.SetCursorPosition(currentLeft, currentTop);
    }

    private void MoveCursorToLineEnd(int startLeft, int startTop, List<char> input)
    {
        MoveCursor(startLeft, startTop, input, input.Count);
    }
}
