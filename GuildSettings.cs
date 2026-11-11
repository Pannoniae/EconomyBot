using EconomyBot.Logging;
using Newtonsoft.Json;

namespace EconomyBot;

/// Per-server configuration. Only IDs live here - no user or message data, ever.
public class GuildSettings {
    /// Channel the "User joined" embeds go to, null if the join log is off.
    public ulong? joinLogChannel;

    /// Role every new member gets, null if there isn't one.
    public ulong? joinRole;

    /// Channel deleted messages' attachments (and purged messages) get reposted to, null if the deletion log is off.
    public ulong? deleteLogChannel;

    /// Ticketing setup, null if the server hasn't run .ticketsetup.
    public TicketConfig? tickets;

    /// Number the next ticket channel gets (ticket-0001, ticket-0002, ...).
    public int nextTicket = 1;
}

/// <param name="category">Category the ticket channels get created in.</param>
/// <param name="staffRole">Role which can see every ticket.</param>
/// <param name="transcriptChannel">Where transcripts of closed tickets get posted.</param>
public record struct TicketConfig(ulong category, ulong staffRole, ulong transcriptChannel);

/// Tiny JSON-backed store for <see cref="GuildSettings"/>, keyed by guild ID.
/// Everything goes through <see cref="read{T}"/> / <see cref="update"/> so nobody holds onto a settings object
/// while another thread is rewriting it.
public static class GuildSettingsStore {
    private const string FILE = "guilds.json";

    private static readonly Logger logger = Logger.getClassLogger("GuildSettings");
    private static readonly Lock sync = new();
    private static Dictionary<ulong, GuildSettings> settings = new();

    public static void load() {
        lock (sync) {
            if (File.Exists(FILE)) {
                try {
                    settings = JsonConvert.DeserializeObject<Dictionary<ulong, GuildSettings>>(File.ReadAllText(FILE)) ?? new();
                    logger.info($"Loaded settings for {settings.Count} servers.");
                }
                catch (Exception e) {
                    // don't let the next save flatten a file somebody might want to fix by hand
                    var broken = $"{FILE}.broken";
                    File.Move(FILE, broken, true);
                    logger.error($"{FILE} is unreadable, moved it to {broken} and starting empty: {e.Message}");
                }
            }

            if (!settings.ContainsKey(Program.MY_GUILD)) {
                settings[Program.MY_GUILD] = new GuildSettings {
                    deleteLogChannel = Program.LOG,
                    joinRole = 1352463350533197989
                };
            }
        }
    }

    public static T read<T>(ulong guild, Func<GuildSettings, T> getter) {
        lock (sync) {
            return getter(settings.GetValueOrDefault(guild) ?? new GuildSettings());
        }
    }

    public static void update(ulong guild, Action<GuildSettings> change) {
        updateAndGet(guild, s => {
            change(s);
            return 0;
        });
    }

    /// Like <see cref="update"/>, but hands back something computed under the lock (think "take the next number").
    public static T updateAndGet<T>(ulong guild, Func<GuildSettings, T> change) {
        lock (sync) {
            if (!settings.TryGetValue(guild, out var s)) {
                settings[guild] = s = new GuildSettings();
            }

            var result = change(s);

            // write-then-rename so a crash mid-write can't leave half a file behind
            var tmp = $"{FILE}.tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(settings, Formatting.Indented));
            File.Move(tmp, FILE, true);
            return result;
        }
    }
}
