namespace OD.EditMediaInfo.Services;

public static class ArgHelpers
{
    public static string[] Split(string s) => s.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static string Next(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {name}");
        i++;
        return args[i];
    }
}