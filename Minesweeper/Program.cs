using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
//using System.Runtime.InteropServices;

namespace Minesweeper
{
    class Program
    {
        static bool run = true;

        /*[DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [STAThread]*/
        static void Main(string[] args)
        {
            MainAsync(/*GetConsoleWindow()*/).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync(/*IntPtr handle*/)
        {
            var ms = new Minesweeper();

            while (true)
            {
                InfoMessage("[Token] Введите токен бота, пожалуйста: ");
                if (!await ms.ConnectAsync(Console.ReadLine()))
                    ErrorMessage("[Token] [Error] Некорректный токен!");
                else
                    break;
            }

            //ShowWindow(handle, 0);
            while (run) { }
        }

        class Minesweeper
        {
            DiscordClient bot;
            Random rand;

            public async Task<bool> ConnectAsync(string token)
            {
                DiscordClient bot = new DiscordClient(new DiscordConfiguration
                {
                    Token = token,
                    TokenType = TokenType.Bot,
                    UseInternalLogHandler = true,
                    LogLevel = LogLevel.Debug
                });

                await bot.ConnectAsync();
                bool result = (bot.CurrentUser?.IsCurrent).GetValueOrDefault();

                if (result)
                {
                    this.bot = bot;
                    this.bot.MessageCreated += MessageCreatedAsync;
                    rand = new Random();
                }

                return result;
            }

            async Task<DiscordEmbedBuilder> GetFieldAsync(DiscordUser sender, int sizeX = 10, int sizeY = 10, int bombsCount = 10)
            {
                var field = await CreateFieldAsync(sizeX > 20 || sizeX < 5 ? 10 : sizeX, sizeY > 20 || sizeY < 5 ? 10 : sizeY, bombsCount < 3 || bombsCount > 20 ? 10 : bombsCount);

                var embed = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Yellow,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = GetType().Name,
                        IconUrl = "https://is3-ssl.mzstatic.com/image/thumb/Purple118/v4/55/65/8b/55658bae-56e0-e391-d183-6ace7cbe8ae1/AppIcon.png/1200x630bb.png"
                    },
                    Description = field.ToString()
                };

                var author = await bot.GetUserAsync(480024641428127744u);

                if (author != null)
                    embed.Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"by {author.Username}",
                        IconUrl = author.AvatarUrl
                    };

                return embed;
            }

            async Task<Field> CreateFieldAsync(int sizeX, int sizeY, int bombsCount)
            {
                Field field = new Field
                {
                    field = new Cell[sizeX, sizeY]
                };

                try
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0, X, Y, tryCount = 0; i < bombsCount; i++, tryCount = 0)
                        {
                            do
                            {
                                X = rand.Next(sizeX);
                                await Task.Delay(150);
                                Y = rand.Next(sizeY);
                                tryCount++;

                            }
                            while (field.field[X, Y].type == CellTypes.bomb && tryCount < 100);

                            if (tryCount < 100)
                                field.field[X, Y].type = CellTypes.bomb;
                        }

                        for (int x = 0, y, X, Y, count = 0; x < sizeX; x++)
                            for (y = 0, count = 0; y < sizeY; y++, count = 0)
                            {
                                if (field.field[x, y].type == CellTypes.bomb)
                                    continue;

                                for (X = -1; X <= 1; X++)
                                    for (Y = -1; Y <= 1; Y++)
                                        if (x + X >= 0 && x + X < sizeX && y + Y >= 0 && y + Y < sizeY && field.field[X + x, Y + y].type == CellTypes.bomb)
                                            count++;
                                field.field[x, y].type = (CellTypes)count;
                            }
                    });
                }
                catch (Exception ex)
                {
                    ErrorMessage($"[Field generation] [Error] {ex.GetType().Name}: {ex.Message}");
                }

                return field;
            }

            async Task MessageCreatedAsync(MessageCreateEventArgs e)
            {
                if (!e.Author.IsBot && e.Message.Content.ToLowerInvariant().StartsWith("!saper") && (e.Message.Content.Length == 6 || e.Message.Content[6] == ' '))
                {
                    if (e.Message.Content.Length < 7 || e.Message.Content.Skip(6).All(c => c == ' '))
                        await e.Channel.SendMessageAsync("```Syntax: /saper x y [bombs count]```");
                    else
                    {
                        var args = e.Message.Content.Split(' ').Skip(1).ToArray();

                        if (args.Length > 0 && args[0].ToLowerInvariant() == "stop" && (await e.Guild.GetMemberAsync(e.Author.Id)).Roles.Any(r => new ulong[] { 492958341283446784u, 255317437149478912u }.Any(ul => ul == r.Id)))
                        {
                            await bot.DisconnectAsync();
                            run = false;
                            return;
                        }

                        int sizeX = 10, sizeY = 10, bombsCount = 10;

                        if (args.Length < 2 || !int.TryParse(args[0], out sizeX) || !int.TryParse(args[1], out sizeY) || (args.Length == 2 ? false : !int.TryParse(args[2], out bombsCount)))
                            await e.Channel.SendMessageAsync("```diff\n- Invalid syntax! Syntax: /saper x y [bombs count] -\n```");
                        else
                            await e.Channel.SendMessageAsync(string.Empty, false, await GetFieldAsync(e.Author, sizeX, sizeY, bombsCount));
                    }
                }
            }

            struct Field
            {
                public Cell[,] field;

                public override string ToString()
                {
                    StringBuilder builder = new StringBuilder();

                    for (int x = 0; x < field.GetLength(0); x++)
                    {
                        for (int y = 0; y < field.GetLength(1); y++)
                            builder.Append($" {field[x, y].ToString()} ");
                        builder.Append("\n");
                    }

                    return builder.ToString();
                }
            }

            struct Cell
            {
                public CellTypes type;

                public override string ToString() => $"||:{type.ToString()}:||";
            }

            enum CellTypes
            {
                zero, one, two, three, four, five, six, seven, eight, bomb
            }
        }

        static void ErrorMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(DateTime.Now.ToString() + message);
            Console.ResetColor();
        }

        static void InfoMessage(string message, bool newLine = false)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            if (newLine)
                Console.WriteLine(DateTime.Now.ToString() + message);
            else
                Console.Write(message);
            Console.ResetColor();
        }

        static void SuccessMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(DateTime.Now.ToString() + message);
            Console.ResetColor();
        }
    }
}
