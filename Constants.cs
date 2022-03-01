namespace EconomyBot; 

public static class Constants {
    public static ulong szerepjatek = 886722112310632499;
    public static ulong server = 828296966324224020;

    public static string token;

    public static void init() {
        token = File.ReadAllText("token");
    }
}