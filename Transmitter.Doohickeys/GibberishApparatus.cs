namespace Transmitter.Doohickeys;

/// <summary>
/// The main source of whimsy
/// </summary>
public static class GibberishApparatus
{
    private static readonly string[] words;

    static GibberishApparatus()
    {
        try
        {
            words = File.ReadAllLines("Oxford 5000.txt");
        }
        catch (FileNotFoundException)
        {
            words = "failed to load file :(".Split(' ');
        }
    }

    public static string Crank() => Crank(Random.Shared.Next(1, 30));

    public static string Crank(int n) => string.Join(' ', Enumerable.Range(0, n).Select(_ => CrankALil()));

    public static string CrankALil() => words[Random.Shared.Next(words.Length)];
}
