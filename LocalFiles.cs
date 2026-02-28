using System.Reflection;

public static class LocalFiles
{
    public static string Folder_PATH { get; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    public static string Jsondump_PATH { get; } = Path.Combine(Folder_PATH, "dump.json");
    public static string SavefileWith(string addendum, string filetype)
    {
        return Path.Combine(Folder_PATH, $"ap_savedat_{addendum}.{filetype}");
    }
}
