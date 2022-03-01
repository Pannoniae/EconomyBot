using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace EconomyBot;

public class EconomyModule : BaseCommandModule {
    [Command("ping"), Description("Ping the bot if you are alone.")]
    public async Task Ping(CommandContext ctx) {
        await ctx.RespondAsync("*Pong!*");
    }

    [Command("n"), Description("Test for numbers.")]
    public async Task Ping2(CommandContext ctx, [Description("The number.")] int a) {
        await ctx.RespondAsync($"{a}");
    }

    [Command("error")]
    public async Task JustError(CommandContext ctx) {
        throw new ArgumentOutOfRangeException();
        await ctx.RespondAsync("A");
    }

    [Group("money"), Description("Money commands.")]
    public class MoneyCommands : BaseCommandModule {
        [Command("add"), Description("Add money to the specified user."),
         RequireUserPermissions(Permissions.ManageChannels)]
        public async Task addMoney(CommandContext ctx, DiscordMember member, int amount) {
            var economy = await getEconomy();
            economy.getEntry(member.Id).money += amount;
            await ctx.RespondAsync($"Added money to {ctx.Member.Mention}.");
            await saveEconomy(economy);
        }

        [Command("give"), Description("Add money to the specified user."),
         RequireUserPermissions(Permissions.ManageChannels)]
        public async Task giveMoney(CommandContext ctx, DiscordMember member, int amount) {
            var economy = await getEconomy();

            if (amount < 0) {
                await ctx.RespondAsync("Don't try to steal...");
                return;
            }

            if (ctx.Member.Id == member.Id) {
                await ctx.RespondAsync("You can't give money to yourself!");
                return;
            }

            // if economy contains your id
            var self = economy.getEntry(ctx.Member.Id);
            var target = economy.getEntry(member.Id);
            if (self.money < amount) {
                await ctx.RespondAsync("You don't have enough money to send!");
                return;
            }

            self.money -= amount;
            target.money += amount;
            await ctx.RespondAsync($"Gave money to {member.Mention}.");
            await saveEconomy(economy);
        }

        [Command("top"), Description("Display a leaderboard with the wealthiest users.")]
        public async Task displayLeaderboard(CommandContext ctx) {
            var economy = await getEconomy();

            const int num = 10;
            var wealthiest = economy.entries.OrderByDescending(u => u.Value.money).Take(num).ToList();
            var builder = new StringBuilder();
            for (var i = 0; i < wealthiest.Count; i++) {
                var (key, user) = wealthiest[i];
                var mention = await ctx.Guild.GetMemberAsync(key);
                builder.Append($"{i + 1}.: {mention.Mention}: {user.money}\n");
            }

            await ctx.RespondAsync($"The top users:\n" +
                                   $"{builder}");


            await saveEconomy(economy);
        }
    }

    private static async Task<string> open(string fileName) {
        if (!File.Exists(fileName)) {
            await File.WriteAllTextAsync(fileName, "{}");
        }

        return await File.ReadAllTextAsync(fileName);
    }

    private static async Task<Economy> getEconomy() {
        var economyJson = await open("economy.json");
        var economy = JsonSerializer.Deserialize<Economy>(economyJson) ?? new Economy();
        return economy;
    }

    private static async Task saveEconomy(Economy economy) {
        var output = JsonSerializer.Serialize(economy);
        await File.WriteAllTextAsync("economy.json", output);
    }
}

public class Economy {
    public Economy() {
        entries = new Dictionary<ulong, EconomyUser>();
    }

    public Dictionary<ulong, EconomyUser> entries { get; set; }

    public EconomyUser getEntry(ulong id) {
        if (entries.ContainsKey(id)) {
            return entries[id];
        }

        entries.Add(id, new EconomyUser());
        return entries[id];
    }
}

public class EconomyUser {
    public EconomyUser() {
    }

    public int money { get; set; }
}