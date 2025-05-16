using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class CodeAnalyzer
{
    static int totalLines = 0;
    static int emptyLines = 0;
    static int commentLines = 0;
    static int logicalLines = 0;
    static int physicalLines = 0;
    static int commentsCount = 0;

    readonly static string singleCommentRegex = @"//.*";
     readonly static string multiCommentRegex =  @"/\*.*?\*/";

    static string[] codeExtensions = { ".cs" };

    static void Main(string[] args)
    {
        Console.WriteLine("Enter path to code directory:");
        string path = Console.ReadLine();

        if (!Directory.Exists(path))
        {
            Console.WriteLine("Directory not found!");
            return;
        }

        AnalyzeDirectory(path);

        Console.WriteLine($"\n--- Code Analysis Report ---");
        Console.WriteLine($"Total physical lines:     {totalLines}");
        Console.WriteLine($"Empty lines:              {emptyLines}");
        Console.WriteLine($"Comment lines:            {commentLines}");
        Console.WriteLine($"Logical lines:            {logicalLines}");
        Console.WriteLine($"Physical lines:           {physicalLines}");
        Console.WriteLine($"Comment density:          {(totalLines > 0 ? (commentsCount * 100.0 / totalLines).ToString("F2") + "%" : "N/A")}");
    }

    static void AnalyzeDirectory(string path)
    {
        foreach (var file in Directory.GetFiles(path))
        {
            if (Array.Exists(codeExtensions, ext => file.EndsWith(ext)))
                AnalyzeFile(file);
        }

        foreach (var dir in Directory.GetDirectories(path))
            AnalyzeDirectory(dir);
    }

    static void AnalyzeFile(string filepath)
    {
        using (var writer = new StreamWriter("logical_statements_log.txt"))
        {
            string[] lines = File.ReadAllLines(filepath);
            bool inBlockComment = false;
            bool skipBrace = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                totalLines++;
                logicalLines += CountLogicalStatementsWithLogging(line, totalLines, writer, skipBrace);
                if (ContainsSelectionOrIteration(line) && totalLines < lines.Length && lines[totalLines].Contains("{"))
                {
                    skipBrace = true;
                }
                else
                {
                    skipBrace = false;
                }
                commentsCount += Regex.Matches(line, @"//|/\*").Count;

                if (string.IsNullOrWhiteSpace(line))
                {
                    emptyLines++;
                    continue;
                }

                if (inBlockComment)
                {
                    commentLines++;
                    if (line.Contains("*/"))
                        inBlockComment = false;
                    continue;
                }

                if (line.Contains("//"))
                {
                    commentLines++;
                    continue;
                }

                if (line.Contains("/*"))
                {
                    commentLines++;
                    if (!line.Contains("*/"))
                        inBlockComment = true;
                    continue;
                }

                if (line.Contains("/*"))
                {
                    commentLines++;
                    inBlockComment = true;
                }

            }
            writer.WriteLine($"Total logical statements: {logicalLines}");
            var emptyLineLimit = (int)(0.25 * totalLines);
            int allowedEmptyLines = Math.Min(emptyLines, emptyLineLimit);
            physicalLines = (totalLines - emptyLines) + allowedEmptyLines;
        }
        
    }

    static bool ContainsSelectionOrIteration(string codeLine)
    {
        string pattern = @"\b(if|else\s+if|else|switch|try|catch|for|while|do|foreach)\b";
        return Regex.IsMatch(codeLine, pattern);
    }

    static string RemoveComments(string line)
    {
        // Remove multi-line comments: /* ... */
        line = Regex.Replace(line, multiCommentRegex, "", RegexOptions.Singleline);

        // Remove single-line comments: //...
        line = Regex.Replace(line, singleCommentRegex, "");

        return line;
    }
    
    static string RemoveInterpolatedStrings(string line)
    {
        return Regex.Replace(line, @"\$""[^""]*""", "\"\"");
    }

    static bool EndsWithSemicolonBrace(string line)
    {
        return Regex.IsMatch(line.Trim(), @"\};\s*$");
    }
    
    static int CountLogicalStatementsWithLogging(string line, int lineNumber, StreamWriter logWriter, bool skipBrace = false)
    {
        int count = 0;
        string cleanedLine = RemoveComments(line);

        // Order 1: Selection
        var order1 = Regex.Matches(cleanedLine, @"\b(if|else\s+if|else|try|catch|switch|\?)\b");
        foreach (Match m in order1)
            logWriter.WriteLine($"Line {lineNumber}: [Order 1] {m.Value}");
        count += order1.Count;

        // Order 2: Iteration
        var order2 = Regex.Matches(cleanedLine, @"\b(for|while|do)\b");
        foreach (Match m in order2)
            logWriter.WriteLine($"Line {lineNumber}: [Order 2] {m.Value}");
        count += order2.Count;

        // Order 3: Control
        var order3 = Regex.Matches(cleanedLine, @"\b(return|break|goto|exit|continue|throw)\b");
        foreach (Match m in order3)
            logWriter.WriteLine($"Line {lineNumber}: [Order 3] {m.Value}");
        count += order3.Count;

        // Order 4: Assignment (excluding ==, >=, etc.)
        var order4a = Regex.Matches(cleanedLine, @"\b[\w?]+(?:\.[\w?]+)*\s*=\s*(?![=>])");
        foreach (Match m in order4a)
            logWriter.WriteLine($"Line {lineNumber}: [Order 4 - Assignment] {m.Value}");
        count += order4a.Count;

        // Order 4: Function calls (not keywords)
        var order4b = Regex.Matches(cleanedLine, @"(?<!\b(if|for|while|switch|catch|foreach|function|using|return|new|delegate|public|private|protected|internal|static|sealed|abstract|virtual|override|async|Type|void|var|class|struct|interface)\??\s+)\b\w+(\.\w+)*\s*\(([^(){};]*)\)");
        foreach (Match m in order4b)
        {
            if (!ContainsSelectionOrIteration(m.Value))
            {
                logWriter.WriteLine($"Line {lineNumber}: [Order 4 - Function Call] {m.Value}");
                count++;
            }
        }

        // Order 4: Empty statements for/while
        var order4c = Regex.Matches(cleanedLine, @"(for|if|while)\s*\([^)]*\)\s*;");
        foreach (Match m in order4c)
            logWriter.WriteLine($"Line {lineNumber}: [Order 4 - Empty Statement] {m.Value}");
        count += order4c.Count;

        // Order 4: Empty statements do..while
        var order4d = Regex.Matches(cleanedLine, @"do\s*;\s*while\s*\([^)]*\)\s*;");
        foreach (Match m in order4d)
            logWriter.WriteLine($"Line {lineNumber}: [Order 4 - Empty Do..While] {m.Value}");
        count += order4d.Count;

        // Order 5: Semicolons (not in for header)
        string lineNoFor = Regex.Replace(cleanedLine, @"for\s*\(.*?\)", "");
        var order5 = Regex.Matches(lineNoFor, @";");
        foreach (Match m in order5)
            logWriter.WriteLine($"Line {lineNumber}: [Order 5 - Semicolon] {m.Value}");
        count += order5.Count;

        // Order 6: Block delimiters
        if (!skipBrace)
        {
            var removeInterpolationLine = RemoveInterpolatedStrings(cleanedLine);
            var order6 = Regex.Matches(removeInterpolationLine, @"(?<!\b(if|else\s*if|else|for|while|switch|catch|try)\s*\([^)]*\)\s*)\{(?!;)");
            foreach (Match m in order6)
            {
                if (!EndsWithSemicolonBrace(removeInterpolationLine))
                {
                    logWriter.WriteLine($"Line {lineNumber}: [Order 6 - Brace] {m.Value}");
                    count++;
                }
            }
        }

        // Order 7: Compiler directives
        if (cleanedLine.TrimStart().StartsWith("#"))
        {
            logWriter.WriteLine($"Line {lineNumber}: [Order 7 - Compiler Directive] {cleanedLine.Trim()}");
            count++;
        }

        // Order 8: Data declarations
        var order8 = Regex.Matches(cleanedLine, @"(?<!for\s*\([^)]*)\b(?:bool|byte|char|decimal|double|float|int|long|sbyte|short|string|uint|ulong|ushort|var|const|internal|static|readonly|public)?\s*(?:\b[\w<>]+\s+)+\w+\s*(=|;)");
        foreach (Match m in order8)
            logWriter.WriteLine($"Line {lineNumber}: [Order 8 - Declaration] {m.Value}");
        count += order8.Count;

        return count;
    }

}