using System.Security.Cryptography;
using System.Text;
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
    private static readonly string FILE = Constants.profileFile("guilds");

    private static readonly Logger logger = Logger.getClassLogger("GuildSettings");
    private static readonly Lock sync = new();
    private static Dictionary<ulong, GuildSettings> settings = new();

    public static void load() {
        lock (sync) {
            if (File.Exists(FILE)) {
                try {
                    var raw = File.ReadAllBytes(FILE);
                    var json = unprotect(raw, out var wasPlain);
                    settings = JsonConvert.DeserializeObject<Dictionary<ulong, GuildSettings>>(json) ?? new();
                    logger.info($"Loaded settings for {settings.Count} servers.");
                    if (wasPlain) {
                        // a file from before the settings were encrypted: rewrite it right away
                        save();
                        logger.info($"{FILE} was plain text, it's encrypted now.");
                    }
                }
                catch (Exception e) {
                    // don't let the next save flatten a file somebody might want to fix by hand
                    var broken = $"{FILE}.broken";
                    File.Move(FILE, broken, true);
                    logger.error($"{FILE} is unreadable, moved it to {broken} and starting empty: {e.Message}");
                }
            }

            if (Constants.profile == null && !settings.ContainsKey(Program.MY_GUILD)) {
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
            save();
            return result;
        }
    }

    /// NOTE: call with the lock held.
    private static void save() {
        // write-then-rename so a crash mid-write can't leave half a file behind
        var tmp = $"{FILE}.tmp";
        File.WriteAllBytes(tmp, protect(JsonConvert.SerializeObject(settings, Formatting.Indented)));
        File.Move(tmp, FILE, true);
    }

    // The settings file is encrypted at rest (discord's developer policy wants that for anything we got from the
    // API, and channel/role IDs count). On Windows that's DPAPI: the key belongs to the Windows user the bot runs
    // as, so there is no key file to lose or leak - but the file can't be read by another user or on another
    // machine either. Elsewhere it's AES-GCM with a base64 32-byte key in BOT_SETTINGS_KEY.
    private static readonly byte[] DPAPI = "EBS1"u8.ToArray();
    private static readonly byte[] AES = "EBS2"u8.ToArray();

    private static byte[] protect(string json) {
        var data = Encoding.UTF8.GetBytes(json);
        if (OperatingSystem.IsWindows()) {
            return [..DPAPI, ..ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser)];
        }

        if (aesKey() is not { } key) {
            logger.warn($"BOT_SETTINGS_KEY isn't set, so {FILE} is being written as plain text.");
            return data;
        }

        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipher = new byte[data.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, data, cipher, tag);
        return [..AES, ..nonce, ..tag, ..cipher];
    }

    private static string unprotect(byte[] raw, out bool wasPlain) {
        wasPlain = false;
        if (raw.AsSpan().StartsWith(DPAPI)) {
            if (!OperatingSystem.IsWindows()) {
                throw new PlatformNotSupportedException("this settings file was encrypted on Windows");
            }

            return Encoding.UTF8.GetString(ProtectedData.Unprotect(raw[DPAPI.Length..], null,
                DataProtectionScope.CurrentUser));
        }

        if (raw.AsSpan().StartsWith(AES)) {
            var key = aesKey() ?? throw new InvalidOperationException("BOT_SETTINGS_KEY is needed to read it");
            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            var tagSize = AesGcm.TagByteSizes.MaxSize;
            var body = raw.AsSpan(AES.Length);
            var plain = new byte[body.Length - nonceSize - tagSize];
            using var aes = new AesGcm(key, tagSize);
            aes.Decrypt(body[..nonceSize], body[(nonceSize + tagSize)..], body.Slice(nonceSize, tagSize), plain);
            return Encoding.UTF8.GetString(plain);
        }

        wasPlain = true;
        return Encoding.UTF8.GetString(raw);
    }

    private static byte[]? aesKey() {
        var text = Environment.GetEnvironmentVariable("BOT_SETTINGS_KEY");
        if (string.IsNullOrEmpty(text)) {
            return null;
        }

        var key = Convert.FromBase64String(text);
        return key.Length == 32 ? key : throw new InvalidOperationException("BOT_SETTINGS_KEY has to be 32 bytes");
    }
}
